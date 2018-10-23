using System;
using System.Collections.Generic;
using System.Text;

namespace DomusSharedClasses
{
    [Serializable]
    public class WaterConsumeData
    {
        public WaterConsumeData(double value, int month, int year)
        {
            Value = value;
            Month = month;
            Year = year;
        }

        public double Value { get; set; }

        public int Month { get; set; }

        public int Year { get; set; }
    }
}
