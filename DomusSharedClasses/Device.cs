using System;
using System.Collections.Generic;
using System.Text;

namespace DomusSharedClasses
{
    [Serializable]
    public class Device
    {
        public Device(string deviceName, int deviceType, string createdAt, string lastActivity, string deviceId = null, bool data1Active = false, bool data2Active = false, bool data3Active = false, bool data4Active = false, string data1Name = " ", string data2Name = " ", string data3Name = " ", string data4Name = " ")
        {
            this.DeviceId = deviceId;
            this.DeviceName = deviceName;
            this.DeviceType = deviceType;
            this.CreatedAt = createdAt;
            this.LastActivity = lastActivity;
            this.Data1Active = data1Active;
            this.Data2Active = data2Active;
            this.Data3Active = data3Active;
            this.Data4Active = data4Active;
            this.Data1Name = data1Name;
            this.Data2Name = data2Name;
            this.Data3Name = data3Name;
            this.Data4Name = data4Name;
        }

        public string DeviceId { get; set; }

        public string DeviceName { get; set; }

        public int DeviceType { get; set; }

        public string CreatedAt { get; set; }

        public string LastActivity { get; set; }

        public string Data1Name { get; set; }

        public string Data2Name { get; set; }

        public string Data3Name { get; set; }

        public string Data4Name { get; set; }

        public bool Data1Active { get; set; }

        public bool Data2Active { get; set; }

        public bool Data3Active { get; set; }

        public bool Data4Active { get; set; }
    }
}
