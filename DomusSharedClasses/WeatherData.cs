using System;
using System.Collections.Generic;
using System.Text;

namespace DomusSharedClasses
{
    [Serializable]
    public class WeatherData
    {
        public WeatherData(float temperature, float maxTemperature, float minTemperature, string temperatureUnit, float humidity, float pressure, string pressureUnit, string precipitationMode, int precipitationValue, string iconValue)
        {
            MaxTemperature = maxTemperature;
            MinTemperature = minTemperature;
            TemperatureUnit = temperatureUnit;
            Humidity = humidity;
            Pressure = pressure;
            PressureUnit = pressureUnit;
            PrecipitationMode = precipitationMode;
            PrecipitationValue = precipitationValue;
            IconValue = iconValue;
            Temperature = temperature;
        }

        public WeatherData()
        {

        }

        public float Temperature { get; set; }

        public float MaxTemperature { get; set; }

        public float MinTemperature { get; set; }

        public string TemperatureUnit { get; set; }

        public float Humidity { get; set; }

        public float Pressure { get; set; }

        public string PressureUnit { get; set; }

        public string PrecipitationMode { get; set; } //no/rain/snow

        public int PrecipitationValue { get; set; } // vaule in mm

        public string IconValue { get; set; } //http://openweathermap.org/img/w/ + IconValue

    }
}
