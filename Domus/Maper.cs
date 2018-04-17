//Add MySql Library
using MySql.Data.MySqlClient;

namespace Domus
{
    public static class Maper
    {
        /// <summary>
        /// Mapeia o datareader para um User
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

    }
}
