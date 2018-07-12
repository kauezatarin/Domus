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
       System.Console.WriteLine(Weather.Temp);
     
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

        public float Temp { get; private set; }

        public float TempMax { get; private set; }

        public float TempMin { get; private set; }

        public void CheckWeather()
        {
            WeatherAPI DataAPI = new WeatherAPI(City + "," + Country);
            Temp = DataAPI.GetTemp();
        }
    }

    class WeatherAPI
    {
        public WeatherAPI(string location)
        {
            SetCurrentURL(location);
            xmlDocument = GetXML(CurrentURL);
        }

        private const string APIKEY = "8e7b8c6d4be9320c424740f01c772211";
        private string CurrentURL;
        private XmlDocument xmlDocument;

        public float GetTemp()
        {
            XmlNode temp_node = xmlDocument.SelectSingleNode("//temperature");
            XmlAttribute temp_value = temp_node.Attributes["value"];
            string temp_string = temp_value.Value;
            return float.Parse(temp_string);
        }

        // trocar para : http://api.openweathermap.org/data/2.5/forecast?q=Piracicaba,br&mode=xml&lang=pt&units=metric&APPID=8e7b8c6d4be9320c424740f01c772211

        private void SetCurrentURL(string location)
        {
            CurrentURL = "http://api.openweathermap.org/data/2.5/weather?q="
                         + location + "&mode=xml&units=metric&APPID=" + APIKEY;
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
    }
}
