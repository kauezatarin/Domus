using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Domus
{
    class TaskScheduler
    {
        private static BlockingCollection<ScheduledTask> scheduledTasks = new BlockingCollection<ScheduledTask>(new ConcurrentQueue<ScheduledTask>());
        private static BlockingCollection<double> deletTasks = new BlockingCollection<double>(new ConcurrentQueue<double>());
        private Thread schedulerWorker;
        private bool cancelAll = false;
        private double idCounter = 0;

        public TaskScheduler()
        {
            schedulerWorker = new Thread(SchedulerThread);
            schedulerWorker.Name = "Scheduler";
            schedulerWorker.IsBackground = true;
            schedulerWorker.Start();
        }

        public double ScheduleTask(DateTime runDateTime, Func<Task> taskFunc, string repeat = "no")
        {
            double taskId = GetNextId();

            ScheduledTask temp = new ScheduledTask(runDateTime, taskFunc, repeat, taskId);

            temp.Scheduler.AutoReset = false;

            scheduledTasks.Add(temp);

            return taskId;
        }

        public bool DeleteTask(double taskId)
        {
            bool success = true;

            int tasksCount = scheduledTasks.Count;

            try
            {
                deletTasks.Add(taskId);
            }
            catch
            {
                success = false;
            }
            
            return success;
        }

        public void DeleteAllTasks()
        {
            cancelAll = true;
        }

        public int TasksCount()
        {
            return scheduledTasks.Count;
        }

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

        private double GetNextId()
        {
            return ++idCounter;
        }

        private DateTime GetRenewDate(DateTime actualTriggerDate, string repeat)
        { 
            if (repeat == "weekly")
            {
                return GetNextWeekday(actualTriggerDate, actualTriggerDate.DayOfWeek);
            }
            else if (repeat == "daily")
            {
                return actualTriggerDate.AddDays(1);
            }
            else if (repeat == "hourly")
            {
                return actualTriggerDate.AddHours(1);
            }
            else if (repeat == "hafhourly")
            {
                return actualTriggerDate.AddMinutes(30);
            }
            else
            {
                throw new Exception("Renew mode '"+ repeat +"' not found.");
            }
        }
        
        private void SchedulerThread()
        {
            while (true)
            {
                ScheduledTask temp;
                double tempId;
                int tasksCount = scheduledTasks.Count;
                int toDeleteCount = deletTasks.Count;

                if (toDeleteCount > 0)//case there are itens to delete
                {
                    for (int i = 0; i < toDeleteCount; i++)
                    {
                        if (deletTasks.TryTake(out tempId))
                        {
                            for (int j = 0; j < tasksCount; j++)
                            {

                                if (scheduledTasks.TryTake(out temp))
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
                                        scheduledTasks.Add(temp);
                                    }
                                }
                            }
                        }
                    }
                }
                else if (!cancelAll)//case the flag cancelAll is false
                {
                    for (int i = 0; i < tasksCount; i++)
                    {
                        if(scheduledTasks.TryTake(out temp))
                        {
                            if (temp.Scheduler.Enabled == false)//if times is stopped
                            {
                                if (temp.Repeat != "no")//and is set to repeat
                                {
                                    temp.renew(GetRenewDate(temp.TriggerDate, temp.Repeat));//updates trigger time

                                    scheduledTasks.Add(temp);//add it back to the list
                                }
                                else
                                {
                                    temp.Scheduler.Dispose();//dispose the timer and thus the ScheduledTask
                                }
                            }
                            else
                            {
                                scheduledTasks.Add(temp);//add it back to the list
                            }
                        }
                    }
                }
                else//case cancelAll is true
                {
                    for (int i = 0; i < tasksCount; i++)
                    {
                        if (scheduledTasks.TryTake(out temp))
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

                    cancelAll = false;
                }

                Thread.Sleep(3000);
            }
        }
    }

    class ScheduledTask
    {
        public ScheduledTask(DateTime triggerDate, Func<Task> task, string repeat, double taskId)
        {
            TriggerDate = triggerDate;

            Repeat = repeat.ToLower();

            TaskId = taskId;

            double temp = (triggerDate - DateTime.Now).TotalMilliseconds;

            Scheduler = new System.Timers.Timer(temp);

            Scheduler.Elapsed += async (sender, e) => await task();

            Scheduler.Start();
        }

        public double TaskId { get; private set; }

        public string Repeat { get; private set; }

        public DateTime TriggerDate { get; set; }

        public System.Timers.Timer Scheduler { get; private set; }

        public bool renew(DateTime renewTo)
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
