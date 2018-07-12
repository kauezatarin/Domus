using System;
using System.Collections.Concurrent;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Threading;

namespace Domus
{
    class ConfigHandler
    {
        public BlockingCollection<string> bannedIPs = new BlockingCollection<string>();

        public int maxClientsConnections { get; private set; } = -1;

        public int maxDevicesConnections { get; private set; } = -1;

        public int clientListeningPort { get; private set; } = 9090;

        public int deviceListeningPort { get; private set; } = 9595;

        public int minDataDelay { get; private set; } = 30;

        public bool logEnabled { get; private set; } = true;

        public bool forceLog { get; private set; } = false;

        public int maxLogSize { get; private set; } = 200;

        public int RSAlength { get; private set; } = 1024;

        public int RSAHashType { get; private set; } = 0;

        public string databaseIP { get; private set; } = "localhost";

        public int databasePort { get; private set; } = 3306;

        public string databaseUser { get; private set; } = "root";

        public string databasePassword { get; private set; } = "";

        public string databaseName { get; private set; } = "domus";

        private string banpath = @"bannedip";

        private string configpath = @"serverConf";

        private string logpath = @"serverLog";

        public ConfigHandler()
        {
            if (!File.Exists(banpath))
            {
                // Create a file to write to.
                using (StreamWriter bannedIpWrite = File.CreateText(banpath))
                {
                    bannedIpWrite.WriteLine("#Write one ip per line like the exemple above:");
                    bannedIpWrite.WriteLine("#127.0.0.1");
                    bannedIpWrite.WriteLine("#127.0.0.2");
                }

            }
            if (!File.Exists(configpath))
            {
                // Create a file to write to.
                using (StreamWriter configWrite = File.CreateText(configpath))
                {
                    configWrite.WriteLine("#Server Configuration file" + configWrite.NewLine);
                    configWrite.WriteLine("#Max Connections per type. Set -1 to unlimited.");
                    configWrite.WriteLine("maxClientsConnections: -1");
                    configWrite.WriteLine("maxDevicesConnections: -1");
                    configWrite.WriteLine(configWrite.NewLine + "#Connection Settings. minDataDelay is in seconds:");
                    configWrite.WriteLine("clientListeningPort: 9090");
                    configWrite.WriteLine("deviceListeningPort: 9595");
                    configWrite.WriteLine("minDataDelay: 30");
                    configWrite.WriteLine(configWrite.NewLine + "#RSA criptography:");
                    configWrite.WriteLine("#RSA Types: 0- SHA1  1- SHA256  2- SHA512");
                    configWrite.WriteLine("RSAlength: 1024");
                    configWrite.WriteLine("RSAHashType: 0");
                    configWrite.WriteLine(configWrite.NewLine + "#Log Settings. Set maxLogSize in MB, -1 to unlimited:");
                    configWrite.WriteLine("#The 'forceLog' option is used to force the server to write everything to the Log file.");
                    configWrite.WriteLine("#WARNING: the 'forceLog' option can cause performance issues and memory leak in some cases, use only for debug.");
                    configWrite.WriteLine("logEnabled: true");
                    configWrite.WriteLine("forceLog: false");
                    configWrite.WriteLine("maxLogSize: 200");
                    configWrite.WriteLine(configWrite.NewLine + "#Database settings:");
                    configWrite.WriteLine("databaseIP: localhost");
                    configWrite.WriteLine("databasePort: 3306");
                    configWrite.WriteLine("databaseUser: root");
                    configWrite.WriteLine("databasePassword: root");
                    configWrite.WriteLine("databaseName: domus");
                }

            }

            LoadConfigs();//must be before log creation

        }

        private void LoadConfigs()
        {
            // Open the file and add banned ips to the list
            using (StreamReader bannedIpRead = File.OpenText(banpath))
            {
                string line = "";

                while ((line = bannedIpRead.ReadLine()) != null)
                {
                    if (line.Contains('#') != true && line.Trim(' ') != "")
                    {
                        bannedIPs.Add(line.Trim(' '));
                    }
                }
            }

            // Open the file and set the config to the properties
            using (StreamReader configRead = File.OpenText(configpath))
            {
                string line = "";

                while ((line = configRead.ReadLine()) != null)
                {
                    if (line.Contains('#'))
                    {
                        /*do nothing*/
                    }
                    else if (line.Contains("maxClientsConnections"))
                    {
                        maxClientsConnections = Convert.ToInt32(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("maxDevicesConnections"))
                    {
                        maxDevicesConnections = Convert.ToInt32(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("clientListeningPort"))
                    {
                        clientListeningPort = Convert.ToInt32(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("deviceListeningPort"))
                    {
                        deviceListeningPort = Convert.ToInt32(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("minDataDelay"))
                    {
                        minDataDelay = (Convert.ToInt32(line.Split(':')[1].Trim(' ')) * 10) + 100;
                    }
                    else if (line.Contains("RSAlength"))
                    {
                        RSAlength = Convert.ToInt32(line.Split(':')[1].Trim(' '));

                        if (RSAlength < 1024 || RSAlength > 16384)
                        {
                            Console.WriteLine("RSA lenght can't be less than 1024 or higher than 16384 (2048 recommended). Using recommended value.");

                            RSAlength = 2048;
                        }
                    }
                    else if (line.Contains("RSAHashType"))
                    {
                        RSAHashType = Convert.ToInt32(line.Split(':')[1].Trim(' '));

                        if (RSAHashType > 2 || RSAHashType < 0)
                        {
                            Console.WriteLine("The log type " + RSAHashType + " does not exists. Has type set to 0 (SHA1).");

                            RSAHashType = 0;
                        }
                    }
                    else if (line.Contains("logEnabled"))
                    {
                        logEnabled = Convert.ToBoolean(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("forceLog"))
                    {
                        forceLog = Convert.ToBoolean(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("maxLogSize"))
                    {
                        maxLogSize = Convert.ToInt32(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("databaseIP"))
                    {
                        databaseIP = line.Split(':')[1].Trim(' ');
                    }
                    else if (line.Contains("databasePort"))
                    {
                        databasePort = Convert.ToInt32(line.Split(':')[1].Trim(' '));
                    }
                    else if (line.Contains("databaseUser"))
                    {
                        databaseUser = line.Split(':')[1].Trim(' ');
                    }
                    else if (line.Contains("databasePassword"))
                    {
                        databasePassword = line.Split(':')[1].Trim(' ');
                    }
                    else if (line.Contains("databaseName"))
                    {
                        databaseName = line.Split(':')[1].Trim(' ');
                    }
                }
            }
        }

        public void AddBannedIp(string ip)
        {
            if (!bannedIPs.Contains(ip))
                bannedIPs.Add(ip);
        }

        public void SaveBannedIPs()
        {
            using (StreamWriter bannedIpWrite = File.CreateText(banpath))
            {
                bannedIpWrite.WriteLine("#Write one ip per line like the exemple above:");
                bannedIpWrite.WriteLine("#127.0.0.1");
                bannedIpWrite.WriteLine("#127.0.0.2");

                foreach (var bannedip in bannedIPs)
                {
                    bannedIpWrite.WriteLine(bannedip);
                }
            }
        }

        public void RemoveBannedIp(string ip)
        {
            int banIpsCount = bannedIPs.Count;
            string temp;

            for (int i = 0; i < banIpsCount; i++)
            {
                temp = bannedIPs.Take();

                if(temp != ip)
                    bannedIPs.Add(temp);
                else
                    break;
                
            }

            SaveBannedIPs();
        }

        public void SaveConfigs()
        {
            SaveBannedIPs();
        }

        public string HashTypeName()
        {
            string[] name = new[] { "SHA1", "SHA256", "SHA512" };

            return name[RSAHashType];
        }

        public string GetLogPath()
        {
            return logpath;
        }

        public void DisableLog()
        {
            logEnabled = false;
        }
    }
}
