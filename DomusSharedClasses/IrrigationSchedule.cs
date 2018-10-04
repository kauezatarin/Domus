using System;
using System.Collections.Generic;
using System.Text;

namespace DomusSharedClasses
{
    [Serializable]

    public class IrrigationSchedule
    {
        public IrrigationSchedule(int scheduleId, string scheduleName, DateTime scheduleTime, int runFor,bool sunday, bool monday, bool tuesday, bool wednesday, bool thursday, bool friday, bool saturday, bool active)
        {
            this.ScheduleId = scheduleId;
            this.ScheduleName = scheduleName;
            this.ScheduleTime = scheduleTime;
            this.Sunday = sunday;
            this.Monday = monday;
            this.Tuesday = tuesday;
            this.Wednesday = wednesday;
            this.Thursday = thursday;
            this.Friday = friday;
            this.Saturday = saturday;
            this.Active = active;
            this.RunFor = runFor;
        }

        public int ScheduleId { get; set; }

        public string ScheduleName { get; set; }

        public DateTime ScheduleTime { get; set; }

        public int RunFor { get; set; }

        public bool Sunday { get; set; }

        public bool Monday { get; set; }

        public bool Tuesday { get; set; }

        public bool Wednesday { get; set; }

        public bool Thursday { get; set; }

        public bool Friday { get; set; }

        public bool Saturday { get; set; }

        public bool Active { get; set; }
    }
}
