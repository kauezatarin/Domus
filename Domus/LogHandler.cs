using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Domus
{
    class LogHandler
    {
        private Thread logWorker;
        protected static ConcurrentQueue<string> logEntries = new ConcurrentQueue<string>();
        private ConfigHandler config;
        private string logpath;

        public bool stopWorkers { get; set; } = false;

        public LogHandler(ConfigHandler config)
        {
            this.config = config;

            logpath = config.GetLogPath();

            if (config.logEnabled)
            {
                if (!File.Exists(logpath))
                {
                    using (StreamWriter logWrite = File.CreateText(logpath))
                    {
                        AddLog("Log initiated!");
                    }
                }
                else
                {
                    AddLog("Log initiated!");
                }

                logWorker = new Thread(() => LogWorker());
                logWorker.Name = "Log Worker";
                logWorker.Start();

            }
        }

        public double GetLogSize(bool inKbytes = false)
        {
            double fileSizeInBytes = new FileInfo(logpath).Length;

            if (!inKbytes)//retorna o tamanho em MB
            {
                return fileSizeInBytes / 1024 / 1024;
            }
            else
            {
                return fileSizeInBytes / 1024;//retorna em bytes
            }

        }

        public void AddLog(string registry, params object[] args)
        {
            if (config.logEnabled || config.forceLog)
            {
                if (config.maxLogSize != -1 && GetLogSize() >= config.maxLogSize)
                {
                    registry = "Log is full.";
                    DisableLog();

                    registry = DateTime.Now.ToString(new CultureInfo("pt-BR")) + " - " + registry;
                    logEntries.Enqueue(registry);
                }
                else
                {
                    for (int i = 0; i < args.Length; i++)
                    {
                        try
                        {
                            registry = registry.Replace("{" + i + "}", args[i].ToString());
                        }
                        catch (Exception)
                        {
                            registry = registry.Replace("{" + i + "}", "NULL");
                        }
                    }

                    registry = DateTime.Now.ToString(new CultureInfo("pt-BR")) + " - " + registry;
                    logEntries.Enqueue(registry);
                }
            }
        }

        public void LogWorker()
        {
            string temp;

            //thread that writes on the log
            while (!stopWorkers || !logEntries.IsEmpty)
            {
                if (!logEntries.IsEmpty)
                {
                    using (StreamWriter logWrite = File.AppendText(logpath))
                    {
                        try
                        {
                            logEntries.TryDequeue(out temp);//gets the first element of the queue.

                            logWrite.WriteLine(temp);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine("LOGWORKER EXCEPTION: " + e.Message);
                        }
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        public void DisableLog()
        {
            config.DisableLog();
        }

    }
}
