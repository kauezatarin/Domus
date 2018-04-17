//Add MySql Library
using MySql.Data.MySqlClient;

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
                dataReader.GetString("lastname"),
                dataReader.GetBoolean("isAdmin"),
                dataReader.GetBoolean("isActive"),
                dataReader.GetString("createdAt"),
                dataReader.GetString("lastLogin"),
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
                dataReader.GetString("deviceId"),
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
                dataReader.GetString("deviceType"),
                dataReader.GetString("createdAt"),
                dataReader.GetString("lastActivity"),
                dataReader.GetString("deviceId"),
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

    }
}
