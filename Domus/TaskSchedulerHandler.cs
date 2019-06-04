using log4net;
using System;

namespace Domus
{
    /// <summary>
    /// Class that implements <see cref="TaskScheduler"/>
    /// </summary>
    class TaskSchedulerHandler:TaskScheduler
    {
        private ILog _log;

        /// <summary>
        /// Class contructor
        /// </summary>
        /// <param name="log">Logging interface</param>
        public TaskSchedulerHandler(ILog log)
        {
            this._log = log;
        }

        /// <summary>
        /// Method that inserts a new log registry
        /// </summary>
        /// <param name="message">Message to be logged</param>
        /// <param name="e">Exception object to be inserted in log, usually to show the stacktrace</param>
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
