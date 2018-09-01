using log4net;
using System;

namespace Domus
{
    class TaskSchedulerHandler:TaskScheduler
    {
        private ILog log;

        public TaskSchedulerHandler(ILog log)
        {
            this.log = log;
        }

        protected override void Log(string message, Exception e = null)
        {
            if (e == null)
            {
                log.Info(message);
            }
            else
            {
                log.Error(message + " - " + e.Message, e);
            }
        }
    }
}
