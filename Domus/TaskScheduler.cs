using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

namespace Domus
{
    class TaskScheduler
    {
        List<ScheduledTask> tasksTimers = new List<ScheduledTask>();
        BlockingCollection<SchedulerRequest> schedulerReuestsQueue = new BlockingCollection<SchedulerRequest>(new ConcurrentQueue<SchedulerRequest>());
        private Thread schedulerWorker;

        public TaskScheduler()
        {
            schedulerWorker = new Thread(SchedulerThread);
            schedulerWorker.IsBackground = true;
            schedulerWorker.Start();
        }

        public int scheduleTask(DateTime runDateTime, Func<Task> taskFunc)
        {
            int taskId;

            try
            {
                ScheduledTask temp = new ScheduledTask(runDateTime, taskFunc);

                tasksTimers.Add(temp);

                taskId = tasksTimers.IndexOf(temp);
            }
            catch (Exception e)
            {
                taskId = -1;
            }

            return taskId;
        }

        public void renewTask(int taskIndex, DateTime renewTo)
        {
            schedulerReuestsQueue.Add(new SchedulerRequest(taskIndex, renewTo));
        }

        public bool renewTask(int taskIndex, string renewTo)
        {
            if (renewTo == "nextWeek")
            {
                return tasksTimers[taskIndex].renew(GetNextWeekday(tasksTimers[taskIndex].TriggerDate, tasksTimers[taskIndex].TriggerDate.DayOfWeek));
            }
            else if (renewTo == "nextDay")
            {
                return tasksTimers[taskIndex].renew(tasksTimers[taskIndex].TriggerDate + new TimeSpan(1,0,0,0));
            }
            else if (renewTo == "nextHour")
            {
                return tasksTimers[taskIndex].renew(tasksTimers[taskIndex].TriggerDate + new TimeSpan(0, 1, 0, 0));
            }
            else if (renewTo == "nextHafHour")
            {
                return tasksTimers[taskIndex].renew(tasksTimers[taskIndex].TriggerDate + new TimeSpan(0, 0, 30, 0));
            }
            else
            {
                return false;
            }
        }

        private DateTime GetNextWeekday(DateTime start, DayOfWeek day)
        {
            // The (... + 7) % 7 ensures we end up with a value in the range [0, 6]
            int daysToAdd = ((int)day - (int)start.DayOfWeek + 7) % 7;
            return start.AddDays(daysToAdd);
        }

        private void SchedulerThread()
        {
            while (true)
            {
                SchedulerRequest temp;

                while (schedulerReuestsQueue.Count > 0)
                {
                    schedulerReuestsQueue.TryTake(out temp);

                    if (temp.Args[1].GetType() == typeof(string))
                    {
                        renewTask((int) temp.Args[0], (string) temp.Args[1]);
                    }
                    else if (temp.Args[1].GetType() == typeof(DateTime))
                    {
                        renewTask((int)temp.Args[0], (DateTime)temp.Args[1]);
                    }
                }

                Thread.Sleep(1000);
            }
        }
    }

    class ScheduledTask
    {
        public ScheduledTask(DateTime triggerDate, Func<Task> task)
        {
            TriggerDate = triggerDate;

            Scheduler = new Timer((triggerDate - DateTime.Now).Milliseconds);

            Scheduler.Elapsed += async (sender, e) => await task();

            Scheduler.Start();
        }

        public DateTime TriggerDate { get; set; }

        private Timer Scheduler { get; set; }

        public bool renew(DateTime renewTo)
        {
            try
            {
                Scheduler.Interval = (DateTime.Now - renewTo).Milliseconds;

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

    class SchedulerRequest
    {
        public SchedulerRequest(int taskIndex, params object[] args)
        {
            Args = args;
            TaskIndex = taskIndex;
        }

        public SchedulerRequest( params object[] args)
        {
            Args = args;
            TaskIndex = -1;
        }

        public int TaskIndex { get; set; }

        public object[] Args { get; set; }
    }
}
