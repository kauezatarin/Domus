using System;
using System.Collections.Generic;
using System.Text;

namespace DomusSharedClasses
{
    [Serializable]
    public class WeatherData
    {
        public WeatherData(string locationCity, string locationCountry, float temperature, float maxTemperature, float minTemperature, string temperatureUnit, float humidity, float pressure, string pressureUnit, string precipitationMode, int precipitationValue, string iconDescription, string iconValue)
        {
            LocationCity = locationCity;
            LocationCountry = locationCountry;
            MaxTemperature = maxTemperature;
            MinTemperature = minTemperature;
            TemperatureUnit = temperatureUnit;
            Humidity = humidity;
            Pressure = pressure;
            PressureUnit = pressureUnit;
            PrecipitationMode = precipitationMode;
            PrecipitationValue = precipitationValue;
            IconValue = iconValue;
            IconDescription = iconDescription;
            Temperature = temperature;
        }

        public WeatherData()
        {

        }

        public string LocationCity { get; set; }

        public string LocationCountry { get; set; }

        public float Temperature { get; set; }

        public float MaxTemperature { get; set; }

        public float MinTemperature { get; set; }

        public string TemperatureUnit { get; set; }

        public float Humidity { get; set; }

        public float Pressure { get; set; }

        public string PressureUnit { get; set; }

        public string PrecipitationMode { get; set; } //no/rain/snow

        public int PrecipitationValue { get; set; } // vaule in mm

        public string IconDescription { get; set; }

        public string IconValue { get; set; } //http://openweathermap.org/img/w/ + IconValue

    }
}
