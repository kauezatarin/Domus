using System;
using System.Collections.Generic;
using System.Security.Authentication.ExtendedProtection;
using System.Text;

namespace DomusSharedClasses
{
    [Serializable]
    public class Service
    {
        public Service(int serviceId, string serviceName, bool isSensor, string deviceId, int devicePortNumber)
        {
            ServiceId = serviceId;
            ServiceName = serviceName;
            IsSensor = isSensor;
            DeviceId = deviceId;
            DevicePortNumber = devicePortNumber;
        }

        public int ServiceId { get; set; }

        public string ServiceName { get; set; }

        public bool IsSensor { get; set; }

        public string DeviceId { get; set; }

        public int DevicePortNumber { get; set; }

    }
}
