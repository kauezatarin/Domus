using System;
using System.Threading.Tasks;

namespace Domus
{
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