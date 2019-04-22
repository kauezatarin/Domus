using MySql.Data.MySqlClient;
using DomusSharedClasses;
using System;

namespace Domus
{
    /// <summary>
    /// Class that contains a mapping method for all database models
    /// </summary>
    public static class Maper
    {
        /// <summary>
        /// Maps the datareader to an <see cref="User"/> object
        /// </summary>
        public static User MapUser(MySqlDataReader dataReader)
        {
            User temp = new User(
                dataReader.GetString("username"),
                dataReader.GetString("email"),
                dataReader.GetString("name"),
                dataReader.GetString("last_name"),
                dataReader.GetBoolean("isAdmin"),
                dataReader.GetBoolean("active"),
                dataReader.GetString("created_at"),
                dataReader.GetString("last_login"),
                dataReader.GetString("password"),
                dataReader.GetInt32("user_id"));

            return temp;
        }

        /// <summary>
        /// Maps the datareader to a <see cref="Data"/> object
        /// </summary>
        public static Data MapData(MySqlDataReader dataReader)
        {
            Data temp = new Data(
                dataReader.GetInt32("data_id"),
                dataReader.GetString("device_id"),
                dataReader.GetDateTime("created_at"),
                dataReader.GetString("data1"),
                dataReader.GetString("data2"),
                dataReader.GetString("data3"),
                dataReader.GetString("data4"));

            return temp;
        }

        /// <summary>
        /// Maps the datareader to a <see cref="Device"/> object
        /// </summary>
        public static Device MapDevice(MySqlDataReader dataReader)
        {
            Device temp = new Device(
                dataReader.GetString("deviceName"),
                dataReader.GetInt32("deviceType"),
                dataReader.GetString("created_at"),
                dataReader.GetString("last_activity"),
                dataReader.GetString("device_id"),
                dataReader.GetBoolean("data1_active"),
                dataReader.GetBoolean("data2_active"),
                dataReader.GetBoolean("data3_active"),
                dataReader.GetBoolean("data4_active"),
                dataReader.GetString("data1_name"),
                dataReader.GetString("data2_name"),
                dataReader.GetString("data3_name"),
                dataReader.GetString("data4_name"));

            return temp;
        }

        /// <summary>
        /// Maps the datareader to an <see cref="IrrigationSchedule"/> object
        /// </summary>
        public static IrrigationSchedule MapIrrigationSchedule(MySqlDataReader dataReader)
        {
            IrrigationSchedule temp = new IrrigationSchedule(
                dataReader.GetInt32("schedule_id"),
                dataReader.GetString("schedule_name"),
                dataReader.GetDateTime("schedule_time"),
                dataReader.GetInt32("run_for"),
                dataReader.GetBoolean("sunday"),
                dataReader.GetBoolean("monday"),
                dataReader.GetBoolean("tuesday"),
                dataReader.GetBoolean("wednesday"),
                dataReader.GetBoolean("thursday"),
                dataReader.GetBoolean("friday"),
                dataReader.GetBoolean("saturday"),
                dataReader.GetBoolean("active")
                );

            return temp;
        }

        /// <summary>
        /// Maps the datareader to a <see cref="CisternConfig"/> object
        /// </summary>
        public static CisternConfig MapCisternConfig(MySqlDataReader dataReader)
        {
            CisternConfig config = new CisternConfig(
                dataReader.GetInt32("config_id"),
                dataReader.GetInt32("time_of_rain"),
                dataReader.GetInt32("min_water_level"),
                dataReader.GetInt32("min_level_action")
                );

            return config;
        }

        /// <summary>
        /// Maps the datareader to a <see cref="Service"/> object
        /// </summary>
        public static Service MapService(MySqlDataReader dataReader)
        {
            Service service = new Service(
                dataReader.GetInt32("service_id"),
                dataReader.GetString("service_name"),
                dataReader.GetBoolean("is_sensor"),
                dataReader.GetString("device_id"),
                dataReader.GetInt32("device_port_number")
            );

            return service;
        }

        /// <summary>
        /// Maps the datareader to an <see cref="IrrigationConfig"/> object
        /// </summary>
        public static IrrigationConfig MapIrrigationConfig(MySqlDataReader dataReader)
        {
            IrrigationConfig config = new IrrigationConfig(
                dataReader.GetInt32("config_id"),
                dataReader.GetInt32("max_soil_humidity"),
                dataReader.GetInt32("min_air_temperature"),
                dataReader.GetInt32("max_air_temperature"),
                dataReader.GetBoolean("use_forecast")
            );

            return config;
        }

        /// <summary>
        /// Maps the datareader to a <see cref="WaterConsumeData"/>
        /// </summary>
        public static WaterConsumeData MapWaterConsumeData(MySqlDataReader dataReader)
        {
            WaterConsumeData data = new WaterConsumeData(
                dataReader.GetDouble("consumo"),
                dataReader.GetInt32("mes"),
                dataReader.GetInt32("ano")
            );

            return data;
        }
    }
}
