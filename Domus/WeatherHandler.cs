using System;
using System.Collections.Generic;
using System.Text;
using System.Net;
using System.Xml;

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
        public WeatherHandler(string City, string Country)
        {
            this.City = City;
            this.Country = Country;
        }

        public string City { get; private set; }

        public string Country { get; private set; }

        public float Temperature { get; private set; }

        public float TemperatureMax { get; private set; }

        public float TemperatureMin { get; private set; }

        public void CheckWeather()
        {
            WeatherAPI DataAPI = new WeatherAPI(City + "," + Country);
            DataAPI.GetForecast();
        }
    }

    class WeatherAPI
    {
        private const string APIKEY = "8e7b8c6d4be9320c424740f01c772211";
        private string CurrentURL;
        private XmlDocument xmlDocument;

        public WeatherAPI(string location)
        {
            SetCurrentURL(location);
            xmlDocument = GetXML(CurrentURL);
        }

        // trocar para : http://api.openweathermap.org/data/2.5/forecast?q=Piracicaba,br&mode=xml&lang=pt&units=metric&APPID=8e7b8c6d4be9320c424740f01c772211
        private void SetCurrentURL(string location)
        {
            /*CurrentURL = "http://api.openweathermap.org/data/2.5/weather?q="
                         + location + "&mode=xml&units=metric&APPID=" + APIKEY;*/

            CurrentURL = "http://api.openweathermap.org/data/2.5/forecast?q="
                         + location + "&mode=xml&lang=pt&units=metric&APPID=" + APIKEY;

        }

        private XmlDocument GetXML(string CurrentURL)
        {
            using (WebClient client = new WebClient())
            {
                string xmlContent = client.DownloadString(CurrentURL);
                XmlDocument xmlDocument = new XmlDocument();
                xmlDocument.LoadXml(xmlContent);
                return xmlDocument;
            }
        }

        public Forecast GetForecast()
        {
            Forecast temp_forecast = new Forecast();

            List<string> locationData = getLocationData();


            return temp_forecast;
        }

        private List<string> getLocationData()
        {
            List<string> temp = new List<string>();

            temp.Add(xmlDocument.SelectSingleNode("//location//name").FirstChild.Value);//resgata o nome da cidade
            temp.Add(xmlDocument.SelectSingleNode("//location//country").FirstChild.Value);//resgata o nome do país

            temp.Add(xmlDocument.SelectSingleNode("//location//location").Attributes["latitude"].Value);//resgata a latitude da localização
            temp.Add(xmlDocument.SelectSingleNode("//location//location").Attributes["longitude"].Value);//resgata a longitude da localização

            return temp;
        }

        
    }
}
