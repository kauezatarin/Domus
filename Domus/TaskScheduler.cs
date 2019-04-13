using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Domus
{
    public abstract class TaskScheduler
    {
        private static BlockingCollection<ScheduledTask> _scheduledTasks = new BlockingCollection<ScheduledTask>(new ConcurrentQueue<ScheduledTask>());
        private static BlockingCollection<double> _deletTasks = new BlockingCollection<double>(new ConcurrentQueue<double>());
        private Thread _schedulerWorker;
        private bool _cancelAll = false;
        private ulong _idCounter = 0;

        /// <summary>
        /// Represents the repeating time of a task
        /// </summary>
        public enum TaskSchedulerRepeatOnceA
        {
            Never,
            Week,
            Day,
            Hour,
            HafHour
        }

        /// <summary>
        /// The constructor of the TaskScheduler Class
        /// </summary>
        protected TaskScheduler()
        {
            _schedulerWorker = new Thread(SchedulerThread);
            _schedulerWorker.Name = "Scheduler";
            _schedulerWorker.IsBackground = true;
            _schedulerWorker.Start();
        }

        /// <summary>
        /// Schedules a new task
        /// </summary>
        /// <param name="runDateTime">The date that the task should run</param>
        /// <param name="taskFunc">The function that the task will run</param>
        /// <param name="repeat">The repeating time of the task. Use it to auto renew the task.</param>
        /// <returns>Returns the task id</returns>
        public ulong ScheduleTask(DateTime runDateTime, Func<Task> taskFunc, TaskSchedulerRepeatOnceA repeat = TaskSchedulerRepeatOnceA.Never)
        {
            ulong taskId = GetNextId();

            ScheduledTask temp = new ScheduledTask(runDateTime, taskFunc, repeat, taskId);

            temp.Scheduler.AutoReset = false;

            _scheduledTasks.Add(temp);

            return taskId;
        }

        /// <summary>
        /// Deletes a task that corresponds to the given id
        /// </summary>
        /// <param name="taskId">The Id of the task that will be deleted</param>
        /// <returns>True if the task was deleted</returns>
        public bool DeleteTask(double taskId)
        {
            bool success = true;

            int tasksCount = _scheduledTasks.Count;

            try
            {
                _deletTasks.Add(taskId);

                Log("Task " + taskId + "deleted.");
            }
            catch(Exception e)
            {
                success = false;

                Log("Fail on delete task" + taskId + " - " + e.Message, e);
            }
            
            return success;
        }

        /// <summary>
        /// Deletes all scheduled tasks
        /// </summary>
        public void DeleteAllTasks()
        {
            _cancelAll = true;
        }

        /// <summary>
        /// Counts the number of scheduled tasks
        /// </summary>
        /// <returns>The number of scheduled tasks</returns>
        public ulong TasksCount()
        {
            return _scheduledTasks.Count;
        }

        /// <summary>
        /// Return the next valid date to the given week day
        /// </summary>
        /// <param name="start">Date to look forward</param>
        /// <param name="day">Day of the week to look at</param>
        /// <returns>The next data when the week day will occur</returns>
        public DateTime GetNextWeekday(DateTime start, DayOfWeek day)
        {
            if ((start - DateTime.Now).TotalMilliseconds < 0 && start.DayOfWeek == day)//caso seja necessário adicionar 7 dias
            {
                return start.AddDays(7);
            }
            else
            {
                // O (... + 7) % 7 garante que seja obtido um valor entre [0, 6]
                int daysToAdd = ((int)day - (int)start.DayOfWeek + 7) % 7;

                return start.AddDays(daysToAdd);
            }
        }

        /// <summary>
        /// Gets the next task id
        /// </summary>
        /// <returns>An id</returns>
        private ulong GetNextId()
        {
            return ++_idCounter;
        }

        /// <summary>
        /// Gets the renew date based on the given frequency
        /// </summary>
        /// <param name="actualTriggerDate">Actual date that the task ran</param>
        /// <param name="repeat">The repeat frequency</param>
        /// <returns></returns>
        private DateTime GetRenewDate(DateTime actualTriggerDate, TaskSchedulerRepeatOnceA repeat)
        {
            try
            {
                if (repeat == TaskSchedulerRepeatOnceA.Week)
                {
                    return GetNextWeekday(actualTriggerDate, actualTriggerDate.DayOfWeek);
                }
                else if (repeat == TaskSchedulerRepeatOnceA.Day)
                {
                    return actualTriggerDate.AddDays(1);
                }
                else if (repeat == TaskSchedulerRepeatOnceA.Hour)
                {
                    return actualTriggerDate.AddHours(1);
                }
                else if (repeat == TaskSchedulerRepeatOnceA.HafHour)
                {
                    return actualTriggerDate.AddMinutes(30);
                }
                else
                {
                    throw new Exception("Renew mode '" + repeat + "' not found.");
                }
            }
            catch (Exception e)
            {
                Log("Fail to get new run date. - " + e.Message ,e);

                return actualTriggerDate;
            }
        }
        
        /// <summary>
        /// Method that handles a task life time
        /// </summary>
        private void SchedulerThread()
        {
            while (true)
            {
                ScheduledTask temp;
                double tempId;
                int tasksCount = _scheduledTasks.Count;
                int toDeleteCount = _deletTasks.Count;

                if (toDeleteCount > 0)//case there are itens to delete
                {
                    for (int i = 0; i < toDeleteCount; i++)
                    {
                        if (_deletTasks.TryTake(out tempId))
                        {
                            for (int j = 0; j < tasksCount; j++)
                            {

                                if (_scheduledTasks.TryTake(out temp))
                                {
                                    if (temp.TaskId == tempId)//item found
                                    {
                                        if (temp.Scheduler.Enabled)
                                            temp.Scheduler.Stop();

                                        temp.Scheduler.Dispose();

                                        break;
                                    }
                                    else
                                    {
                                        _scheduledTasks.Add(temp);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (!_cancelAll)//case the flag cancelAll is false
                {
                    for (int i = 0; i < tasksCount; i++)
                    {
                        if(_scheduledTasks.TryTake(out temp))
                        {
                            if (temp.Scheduler.Enabled == false)//if times is stopped
                            {
                                if (temp.Repeat != TaskSchedulerRepeatOnceA.Never)//and is set to repeat
                                {
                                    DateTime newDateTime = GetRenewDate(temp.TriggerDate, temp.Repeat);

                                    if (newDateTime != temp.TriggerDate)//case receives a new date (if receives the same date means that an error was occurred on getting a new date.)
                                    {
                                        temp.Renew(newDateTime);//updates trigger time

                                        _scheduledTasks.Add(temp);//add it back to the list

                                        Log("Task " + temp.TaskId + " was renewed and will run at " + newDateTime.ToString(new CultureInfo("pt-BR")));
                                    }
                                    
                                }
                                else
                                {
                                    temp.Scheduler.Dispose();//dispose the timer and thus the ScheduledTask
                                }
                            }
                            else
                            {
                                _scheduledTasks.Add(temp);//add it back to the list
                            }
                        }
                    }
                }
                else//case cancelAll is true
                {
                    for (int i = 0; i < tasksCount; i++)
                    {
                        if (_scheduledTasks.TryTake(out temp))
                        {
                            if (temp.Scheduler.Enabled == false)//if times is stopped
                            {
                                temp.Scheduler.Dispose();//dispose the timer and thus the ScheduledTask
                            }
                            else
                            {
                                temp.Scheduler.Stop();
                                temp.Scheduler.Dispose();
                            }
                        }
                    }

                    _cancelAll = false;
                }

                Thread.Sleep(3000);
            }
        }

        /// <summary>
        /// Method that logs the scheduler messages
        /// </summary>
        /// <param name="message">Message to be logged</param>
        /// <param name="e">Exception occurred</param>
        protected abstract void Log(string message, Exception e = null);
    }

    /// <summary>
    /// Class that represents a scheduled Domus system task
    /// </summary>
    class ScheduledTask
    {
        /// <summary>
        /// The constructor of the <see cref="ScheduledTask"/> class
        /// </summary>
        /// <param name="triggerDate">The <see cref="DateTime"/> where the task should run at</param>
        /// <param name="task">The Method that the task will execute</param>
        /// <param name="repeat">Inform if the task should repeat</param>
        /// <param name="taskId">The id of the task</param>
        public ScheduledTask(DateTime triggerDate, Func<Task> task, TaskScheduler.TaskSchedulerRepeatOnceA repeat, ulong taskId)
        {
            TriggerDate = triggerDate;

            Repeat = repeat;

            TaskId = taskId;

            double temp = (triggerDate - DateTime.Now).TotalMilliseconds;

            Scheduler = new System.Timers.Timer(temp);

            Scheduler.Elapsed += async (sender, e) => await task();

            Scheduler.Start();
        }

        /// <summary>
        /// Gets the current task id
        /// </summary>
        public ulong TaskId { get; private set; }

        /// <summary>
        /// Gets when the task should repeat
        /// </summary>
        public TaskScheduler.TaskSchedulerRepeatOnceA Repeat { get; private set; }

        /// <summary>
        /// Gets or sets the date that the task will run
        /// </summary>
        public DateTime TriggerDate { get; set; }

        /// <summary>
        /// Gets the Timer of the task
        /// </summary>
        public System.Timers.Timer Scheduler { get; private set; }

        /// <summary>
        /// Renews the current task to a given <see cref="DateTime"/>
        /// </summary>
        /// <param name="renewTo">The next date when the task should run</param>
        /// <returns>True if the task was successfully renewed</returns>
        public bool Renew(DateTime renewTo)
        {
            try
            {
                Scheduler.Interval = (renewTo - DateTime.Now).TotalMilliseconds;

                TriggerDate = renewTo;

                Scheduler.Start();

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
