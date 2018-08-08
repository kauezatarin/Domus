using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Domus
{
    class TaskScheduler
    {
        BlockingCollection<ScheduledTask> scheduledTasks = new BlockingCollection<ScheduledTask>(new ConcurrentQueue<ScheduledTask>());
        private Thread schedulerWorker;

        public TaskScheduler()
        {
            schedulerWorker = new Thread(SchedulerThread);
            schedulerWorker.IsBackground = true;
            schedulerWorker.Start();
        }

        public bool scheduleTask(DateTime runDateTime, Func<Task> taskFunc, string repeat = "no")
        {
            ScheduledTask temp = new ScheduledTask(runDateTime, taskFunc, repeat);

            temp.Scheduler.AutoReset = false;

            return scheduledTasks.TryAdd(temp);
        }

        private DateTime GetRenewDate(DateTime actualTriggerDate, string repeat)
        {
            if (repeat == "Weekly")
            {
                return GetNextWeekday(actualTriggerDate, actualTriggerDate.DayOfWeek);
            }
            else if (repeat == "Daily")
            {
                return actualTriggerDate.AddDays(1);
            }
            else if (repeat == "Hourly")
            {
                return actualTriggerDate.AddHours(1);
            }
            else if (repeat == "HafHourly")
            {
                return actualTriggerDate.AddMinutes(30);
            }
            else
            {
                throw new Exception("Renew mode '"+ repeat +"' not found.");
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
                ScheduledTask temp;
                int tasksCount = scheduledTasks.Count;

                for(int i=0; i < tasksCount; i++)
                {
                    scheduledTasks.TryTake(out temp);

                    if (temp.Scheduler.Enabled == false)//if times is stopped
                    {
                        if (temp.Repeat != "no")//and is set to repeat
                        {
                            temp.renew(GetRenewDate(temp.TriggerDate,temp.Repeat));//updates trigger time

                            scheduledTasks.Add(temp);//add it back to the list
                        }
                        else
                        {
                            temp.Scheduler.Dispose();//dispose the timer and thus the ScheduledTask
                        }
                    }
                }

                Thread.Sleep(3000);
            }
        }
    }

    class ScheduledTask
    {
        public ScheduledTask(DateTime triggerDate, Func<Task> task, string repeat)
        {
            TriggerDate = triggerDate;

            Repeat = repeat;

            Scheduler = new System.Timers.Timer((triggerDate - DateTime.Now).Milliseconds);

            Scheduler.Elapsed += async (sender, e) => await task();

            Scheduler.Start();
        }

        public string Repeat { get; set; }

        public DateTime TriggerDate { get; set; }

        public System.Timers.Timer Scheduler { get; set; }

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
