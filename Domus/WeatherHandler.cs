using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Xml;
using DomusSharedClasses;

namespace Domus
{
    /* USO:
       WeatherHandler Weather = new WeatherHandler("Piracicaba", "br");
       Weather.CheckWeather();
       System.Console.WriteLine(Weather.Temperature);

        https://openweathermap.org/weather-data
     
     */

    class WeatherHandler
    {
        public WeatherHandler(string city, string country, string apiKey)
        {
            this.City = city;
            this.Country = country;
            this.ApiKey = apiKey;
        }

        public string City { get; private set; }

        public string Country { get; private set; }

        private string ApiKey { get; set; }

        public Forecast CheckForecast()
        {
            Forecast forecast = null;

            try
            {
                WeatherApi dataApi = new WeatherApi(City + "," + Country, ApiKey);

                forecast = dataApi.GetForecast();
            }
            catch (Exception e)
            {
                throw e;
            }

            return forecast;
        }

        public WeatherData CheckWeather()
        {
            WeatherData weather = null;

            try
            {
                WeatherApi dataApi = new WeatherApi(City + "," + Country, ApiKey, true);//gets current weather instead of 5 days forecast

                weather = dataApi.GetWeather();
            }
            catch (Exception e)
            {
                throw e;
            }

            return weather;
        }
    }

    class WeatherApi
    {
        private static string _apikey;
        private string _currentForecastUrl;
        private string _currentWeatherUrl;
        private XmlDocument _xmlDocument;

        public WeatherApi(string location, string apiKey, bool getWeather = false)
        {
            _apikey = apiKey;
            SetCurrentUrl(location);
            _xmlDocument = getWeather ? GetXml(_currentWeatherUrl) : GetXml(_currentForecastUrl);
        }

        private void SetCurrentUrl(string location)
        {
            _currentForecastUrl = "http://api.openweathermap.org/data/2.5/forecast?q="
                         + location + "&mode=xml&lang=pt&units=metric&APPID=" + _apikey;

            _currentWeatherUrl = "http://api.openweathermap.org/data/2.5/weather?q="
                                + location + "&mode=xml&lang=pt&units=metric&APPID=" + _apikey;

        }

        private XmlDocument GetXml(string currentUrl)
        {
            using (WebClient client = new WebClient())
            {
                string xmlContent = client.DownloadString(currentUrl);
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(xmlContent);
                return xmlDocument;
            }
        }

        #region ForecastMethods

        //return the 5 days forecast
        public Forecast GetForecast()
        {
            List<string> locationData = GetForecastLocationData();
            List<DateTime> sunData = GetForecastSunData();
            List<ForecastData> forecastDatas = GetForecastDatas();

            return new Forecast(locationData, sunData, forecastDatas);
        }

        private List<string> GetForecastLocationData()
        {
            List<string> data = new List<string>();

            data.Add(_xmlDocument.SelectSingleNode("//location//name").FirstChild.Value);//resgata o nome da cidade
            data.Add(_xmlDocument.SelectSingleNode("//location//country").FirstChild.Value);//resgata o nome do país

            data.Add(_xmlDocument.SelectSingleNode("//location//location").Attributes["latitude"].Value);//resgata a latitude da localização
            data.Add(_xmlDocument.SelectSingleNode("//location//location").Attributes["longitude"].Value);//resgata a longitude da localização

            return data;
        }

        private List<DateTime> GetForecastSunData()
        {
            List<DateTime> data = new List<DateTime>();

            data.Add(GenerateDatetime(_xmlDocument.SelectSingleNode("//sun").Attributes["rise"].Value));//resgata a latitude da localização
            data.Add(GenerateDatetime(_xmlDocument.SelectSingleNode("//sun").Attributes["set"].Value));//resgata a longitude da localização

            return data;
        }

        private List<ForecastData> GetForecastDatas()
        {
            List<ForecastData> data = new List<ForecastData>();
            XmlNodeList nodes = _xmlDocument.SelectSingleNode("//forecast").ChildNodes;

            string fromData;
            string toData;
            string value;
            string type;

            foreach (XmlNode node in nodes)
            {
                fromData = node.Attributes["from"].Value;
                toData = node.Attributes["to"].Value;

                XmlNodeList tempnodes = node.ChildNodes;

                if (tempnodes[1].Attributes.Count > 0)
                {
                    value = tempnodes[1].Attributes["value"].Value.Replace(".", ",");
                    type = tempnodes[1].Attributes["type"].Value;
                }
                else
                {
                    value = "-1";
                    type = "none";
                }

                data.Add(new ForecastData(GenerateDatetime(fromData), GenerateDatetime(toData), Convert.ToSingle(value), type));
            }


            return data;
        }

        #endregion

        #region WeatherMethods

        //todays weather
        public WeatherData GetWeather()
        {
            WeatherData forecastDatas = GetWeatherData();

            return forecastDatas;
        }

        private WeatherData GetWeatherData()
        {
            WeatherData data = new WeatherData();

            data.LocationCity = _xmlDocument.SelectSingleNode("//city").Attributes["name"].Value;//resgata o nome da cidade
            data.LocationCountry = _xmlDocument.SelectSingleNode("//city//country").FirstChild.Value;//resgata o nome do país

            data.Temperature = Convert.ToSingle(_xmlDocument.SelectSingleNode("//temperature").Attributes["value"].Value);
            data.MaxTemperature = Convert.ToSingle(_xmlDocument.SelectSingleNode("//temperature").Attributes["max"].Value);
            data.MinTemperature = Convert.ToSingle(_xmlDocument.SelectSingleNode("//temperature").Attributes["min"].Value);
            data.TemperatureUnit = _xmlDocument.SelectSingleNode("//temperature").Attributes["unit"].Value;

            data.Humidity = Convert.ToSingle(_xmlDocument.SelectSingleNode("//humidity ").Attributes["value"].Value);

            data.PrecipitationMode = _xmlDocument.SelectSingleNode("//precipitation").Attributes["mode"].Value;

            if(data.PrecipitationMode != "no")
                data.PrecipitationValue = Convert.ToInt32(_xmlDocument.SelectSingleNode("//precipitation").Attributes["value"].Value);

            data.Pressure = Convert.ToSingle(_xmlDocument.SelectSingleNode("//pressure").Attributes["value"].Value);
            data.PressureUnit = _xmlDocument.SelectSingleNode("//pressure").Attributes["unit"].Value;

            data.IconDescription = _xmlDocument.SelectSingleNode("//weather").Attributes["value"].Value;
            data.IconValue = _xmlDocument.SelectSingleNode("//weather").Attributes["icon"].Value;

            return data;
        }

        #endregion

        private DateTime GenerateDatetime(string dataString, bool returnGmt = false)
        {
            string[] dateTime = dataString.Split("T");
            string[] date = dateTime[0].Split("-");
            string[] time = dateTime[1].Split(":");

            DateTime tempDateTime = new DateTime(Convert.ToInt32(date[0]), Convert.ToInt32(date[1]), Convert.ToInt32(date[2]), Convert.ToInt32(time[0]), Convert.ToInt32(time[1]), Convert.ToInt32(date[2]));

            if (!returnGmt)//retorna o horário convertido para 
            {
                tempDateTime = TimeZoneInfo.ConvertTimeFromUtc(tempDateTime,TimeZoneInfo.Local);
            }

            return tempDateTime;
        }
    }
}
