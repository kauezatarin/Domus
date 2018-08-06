using System;
using System.Collections.Generic;
using System.Text;

namespace DomusSharedClasses
{
    public class WeatherData
    {
        public float MaxTemperature { get; private set; }

        public float MinTemperature { get; private set; }

        public string TemperatureUnit { get; private set; }

        public float Humidity { get; private set; }

        public float Pressure { get; private set; }

        public string PressureUnit { get; private set; }

        public bool PrecipitationMode { get; private set; } //no/rain/snow

        public int PrecipitationValue { get; private set; } // vaule in mm

        public string IconValue { get; private set; } //http://openweathermap.org/img/w/ + IconValue

        public WeatherData()
        {

        }

    }
}
