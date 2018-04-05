using System;
using System.Collections.Generic;
using System.Text;

namespace Domus
{
    class Device
    {
        public Device(string deviceName, string deviceType, int userId, string createdAt, string lastActivity, string deviceId = null, bool data1_active = false, bool data2_active = false, bool data3_active = false, bool data4_active = false, string data1_name = " ", string data2_name = " ", string data3_name = " ", string data4_name = " ")
        {
            this.deviceId = deviceId;
            this.deviceName = deviceName;
            this.deviceType = deviceType;
            this.userId = userId;
            this.createdAt = createdAt;
            this.lastActivity = lastActivity;
            this.data1_active = data1_active;
            this.data2_active = data2_active;
            this.data3_active = data3_active;
            this.data4_active = data4_active;
            this.data1_name = data1_name;
            this.data2_name = data2_name;
            this.data3_name = data3_name;
            this.data4_name = data4_name;
        }

        public string deviceId { get; set; }

        public string deviceName { get; set; }

        public string deviceType { get; set; }

        public int userId { get; set; }

        public string createdAt { get; set; }

        public string lastActivity { get; set; }

        public string data1_name { get; set; }

        public string data2_name { get; set; }

        public string data3_name { get; set; }

        public string data4_name { get; set; }

        public bool data1_active { get; set; }

        public bool data2_active { get; set; }

        public bool data3_active { get; set; }

        public bool data4_active { get; set; }
    }
}
