//Add MySql Library

using MySql.Data.MySqlClient;
using DomusSharedClasses;

namespace Domus
{
    public static class Maper
    {
        /// <summary>
        /// Mapeia o datareader para um objeto User
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
        /// Mapeia o datareader para um objeto Data
        /// </summary>
        public static Data MapData(MySqlDataReader dataReader)
        {
            Data temp = new Data(
                dataReader.GetInt32("dataId"),
                dataReader.GetString("device_id"),
                dataReader.GetString("createdAt"),
                dataReader.GetString("data1"),
                dataReader.GetString("data2"),
                dataReader.GetString("data3"),
                dataReader.GetString("data4"));

            return temp;
        }

        /// <summary>
        /// Mapeia o datareader para um objeto Device
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
        /// Mapeia o datareader para um objeto Device
        /// </summary>
        public static IrrigationSchedule MapIrrigationSchedule(MySqlDataReader dataReader)
        {
            IrrigationSchedule temp = new IrrigationSchedule(
                dataReader.GetInt32("schedule_id"),
                dataReader.GetString("schedule_name"),
                dataReader.GetDateTime("schedule_time"),
                dataReader.GetInt32("run_for"),
                dataReader.GetBoolean("sunday"),
                dataReader.GetBoolean("moonday"),
                dataReader.GetBoolean("tuesday"),
                dataReader.GetBoolean("wednesday"),
                dataReader.GetBoolean("thursday"),
                dataReader.GetBoolean("friday"),
                dataReader.GetBoolean("saturday"),
                dataReader.GetBoolean("active")
                );

            return temp;
        }

    }
}
