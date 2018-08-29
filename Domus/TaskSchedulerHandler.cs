using log4net;
using System;

namespace Domus
{
    class TaskSchedulerHandler:TaskScheduler
    {
        private ILog _log;

        public TaskSchedulerHandler(ILog log)
        {
            this._log = log;
        }

        protected override void Log(string message, Exception e = null)
        {
            if (e == null)
            {
                _log.Info(message);
            }
            else
            {
                _log.Error(message + " - " + e.Message, e);
            }
        }
    }
}
