using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
//Add MySql Library
using MySql.Data.MySqlClient;

namespace Domus
{
    static class DatabaseHandler
    {
        /// <summary>
        /// Gera a string de conexão.
        /// </summary>
        public static string CreateConnectionString(string databaseIP, int databasePort, string databaseName, string databaseUser, string databasePassword = "")
        {
            string connectionString = "SERVER=" + databaseIP + ";" +
                "PORT=" + databasePort + ";" +
                "DATABASE=" + databaseName + ";" +
                "UID=" + databaseUser + ";" +
                "PASSWORD=" + databasePassword + ";";

            return connectionString;
        }

        /// <summary>
        /// Testa a conexão do banco
        /// </summary>
        public static void TestConnection(string connectionString)
        {

            using (var conn = new MySqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (MySqlException e)
                {
                    throw e;
                }
            }
        }

        /// <summary>
        /// Busca na base as informações do usuario para login
        /// </summary>
        public static User LoginRequest(string connectionString, string username)
        {
            User user = null;

            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT * FROM Users WHERE username = '" + username + "'";

                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            dataReader.Read();
                            user = MapUser(dataReader);
                        }

                        return user;
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }

                }
            }
        }

        private static User MapUser(MySqlDataReader dataReader)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Insere um usuário no banco
        /// </summary>
        public static void InsertUser(string connectionString, User user)
        {

            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandText = "INSERT INTO users (username,password,email,name,last_name,isAdmin,created_at,last_login) values('" + user.username +
                                      "','" + user.password +
                                      "','" + user.email +
                                      "','" + user.name +
                                      "','" + user.lastName +
                                      "'," + user.isAdmin +
                                      ",'" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                                      "','" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "')";

                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    throw e;
                }

            }
        }

        /// <summary>
        /// Insere um dispositivo no banco
        /// </summary>
        public static void InsertDevice(string connectionString, Device device)
        {
            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandText = "INSERT INTO devices (deviceName,deviceId,deviceType,user_id,created_at,last_activity, data1_name, data2_name, data3_name, data4_name, data1_active, data2_active, data3_active, data4_active) values('" + device.deviceName +
                                      "','" + device.deviceId +
                                      "','" + device.deviceType +
                                      "','" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                                      "','" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") +
                                      "','" + device.data1_name +
                                      "','" + device.data2_name +
                                      "','" + device.data3_name +
                                      "','" + device.data4_name +
                                      "'," + device.data1_active +
                                      "," + device.data2_active +
                                      "," + device.data3_active +
                                      "," + device.data4_active + ")";

                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    throw e;
                }

            }
        }

        /// <summary>
        /// Insere um registro de dispositivo no banco de dados
        /// </summary>
        public static void InsertData(string connectionString, Data data)
        {
            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandText = "INSERT INTO data (device_id,created_at,data1,data2,data3,data4) values('" + data.deviceId +
                                      "','" + data.createdAt +
                                      "','" + data.data1 +
                                      "','" + data.data2 +
                                      "','" + data.data3 +
                                      "','" + data.data4 + "')";

                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    throw e;
                }

            }
        }

        /*//Count statement
        public int Count()
        {
            string query = "SELECT Count(*) FROM tableinfo";
            int Count = -1;

            //Open Connection
            if (this.OpenConnection() == true)
            {
                //Create Mysql Command
                MySqlCommand cmd = new MySqlCommand(query, connection);

                //ExecuteScalar will return one value
                Count = int.Parse(cmd.ExecuteScalar() + "");

                //close Connection
                this.CloseConnection();

                return Count;
            }
            else
            {
                return Count;
            }
         }*/

    }
}
