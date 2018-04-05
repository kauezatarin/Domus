using System.Threading;

namespace Domus
{
    class ConnectionCommandStore
    {
        public ConnectionCommandStore(Thread client)
        {
            conexao = client;
            command = null;
            clientIP = null;
            deviceType = null;
            deviceName = null;
            dataDelay = 30;
        }

        public ConnectionCommandStore()
        {
            conexao = null;
            command = null;
            clientIP = null;
            deviceType = null;
            deviceName = null;
            dataDelay = 30;
        }

        public Thread conexao { get; set; }

        public string command { get; set; }

        public string clientIP { get; set; }

        public string deviceName { get; set; }

        public string deviceType { get; set; }

        public int dataDelay { get; set; }

        public string deviceUniqueID { get; set; }
    }
}
