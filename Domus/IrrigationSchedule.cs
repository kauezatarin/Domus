using System;
using System.Collections.Generic;
using System.Text;

namespace Domus
{
    public class IrrigationSchedule
    {
        public IrrigationSchedule(int scheduleId, string scheduleName, DateTime scheduleTime, int runFor,bool sunday, bool moonday, bool tuesday, bool wednesday, bool thursday, bool friday, bool saturday, bool active)
        {
            this.scheduleId = scheduleId;
            this.scheduleName = scheduleName;
            this.scheduleTime = scheduleTime;
            this.sunday = sunday;
            this.moonday = moonday;
            this.tuesday = tuesday;
            this.wednesday = wednesday;
            this.thursday = thursday;
            this.friday = friday;
            this.saturday = saturday;
            this.active = active;
            this.runFor = runFor;
        }

        public int scheduleId { get; set; }

        public string scheduleName { get; set; }

        public DateTime scheduleTime { get; set; }

        public int runFor { get; set; }

        public bool sunday { get; set; }

        public bool moonday { get; set; }

        public bool tuesday { get; set; }

        public bool wednesday { get; set; }

        public bool thursday { get; set; }

        public bool friday { get; set; }

        public bool saturday { get; set; }

        public bool active { get; set; }
    }
}
