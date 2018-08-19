using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using DomusSharedClasses;
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
                "PASSWORD=" + databasePassword + ";" +
                "SslMode = none" + ";" +
                "CharSet=utf8";

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
                catch(MySqlException e)
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
                        cmd.CommandText = "SELECT * FROM users WHERE username = '" + username + "' AND active = True";

                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            dataReader.Read();
                            user = Maper.MapUser(dataReader);
                        }

                        return user;
                    }
                    catch (MySqlException e)
                    {
                        throw e;
                    }

                }
            }
        }

        /// <summary>
        /// Atualiza o timestamp do usuário com a ultima data de login
        /// </summary>
        public static void UpdateUserLastLogin(string connectionString, int userId)
        {
            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandText = "UPDATE users SET last_login = '" + 
                                      DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + 
                                      "' WHERE user_id=" + userId;

                    cmd.ExecuteNonQuery();
                }
                catch (MySqlException e)
                {
                    throw e;
                }
            }
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
                catch (MySqlException e)
                {
                    throw e;
                }

            }
        }

        /// <summary>
        /// Atualiza um usuário no banco
        /// </summary>
        public static void UpdateUser(string connectionString, User user)
        {
            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandText = "UPDATE users SET username = '" +
                                      user.username +
                                      "', email = '"+ user.email +
                                      "', active = " + user.isActive +
                                      ", name = '"+ user.name + 
                                      "', last_name = '"+ user.lastName + 
                                      "', isAdmin = " + user.isAdmin +
                                      " WHERE user_id=" + user.userId;

                    cmd.ExecuteNonQuery();
                }
                catch (MySqlException e)
                {
                    throw e;
                }
            }
        }

        /// <summary>
        /// Altera a senha de um usuário no banco
        /// </summary>
        public static void ChangeUserPasswd(string connectionString, int userId, string passwd)
        {
            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandText = "UPDATE users SET password = '" +
                                      passwd +
                                      "' WHERE user_id=" + userId;

                    cmd.ExecuteNonQuery();
                }
                catch (MySqlException e)
                {
                    throw e;
                }
            }
        }

        /// <summary>
        /// Retorna um usuário que corresponde ao id informado
        /// </summary>
        public static User GetUserById(string connectionString, int userId)
        {
            User user = null;

            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT * FROM users WHERE user_id = " + userId;

                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            dataReader.Read();
                            user = Maper.MapUser(dataReader);
                        }

                        return user;
                    }
                    catch (MySqlException e)
                    {
                        throw e;
                    }

                }
            }
        }

        /// <summary>
        /// Deleta um usuário no banco
        /// </summary>
        public static void DeleteUser(string connectionString, int userId)
        {
            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandText = "DELETE FROM users WHERE user_id=" + userId;

                    cmd.ExecuteNonQuery();
                }
                catch (MySqlException e)
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
                    cmd.CommandText = "INSERT INTO devices (deviceName,device_id,deviceType,created_at,last_activity, data1_name, data2_name, data3_name, data4_name, data1_active, data2_active, data3_active, data4_active) values('" + device.deviceName +
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
                catch (MySqlException e)
                {
                    throw e;
                }

            }
        }

        /// <summary>
        /// Atualiza um dispositivo no banco
        /// </summary>
        public static void UpdateDevice(string connectionString, Device device)
        {
            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandText = "UPDATE devices SET deviceName = '" +
                                      device.deviceName +
                                      "', deviceType = " + device.deviceType +
                                      ", data1_name = '" + device.data1_name +
                                      "', data2_name = '" + device.data2_name +
                                      "', data3_name = '" + device.data3_name +
                                      "', data4_name = '" + device.data4_name +
                                      "', data1_active = " + device.data1_active +
                                      ", data2_active = " + device.data2_active +
                                      ", data3_active = " + device.data3_active +
                                      ", data4_active = " + device.data4_active +
                                      " WHERE device_id='" + device.deviceId + "'";

                    cmd.ExecuteNonQuery();
                }
                catch (MySqlException e)
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
                    cmd.CommandText = "INSERT INTO data (device_id,created_at,data1,data2,data3,data4) values('" + data.device_id +
                                      "','" + data.createdAt +
                                      "','" + data.data1 +
                                      "','" + data.data2 +
                                      "','" + data.data3 +
                                      "','" + data.data4 + "')";

                    cmd.ExecuteNonQuery();
                }
                catch (MySqlException e)
                {
                    throw e;
                }

            }
        }

        /// <summary>
        /// Verifica de o dispositivo está cadastrado no banco de dados
        /// </summary>
        public static bool IsAuthenticDevice(string connectionString, string UID)
        {
            int count = 0;
 
            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT count(*) FROM devices WHERE device_id = '" + UID +"'";

                        count = int.Parse(cmd.ExecuteScalar().ToString());
                    }
                    catch (MySqlException e)
                    {
                        throw e;
                    }

                }
            }

            if (count == 0)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// Retorna um dispositivo que corresponde o UID informado
        /// </summary>
        public static Device GetDeviceByUid(string connectionString, string UID)
        {
            Device device;

            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT * FROM devices WHERE device_id = '" + UID + "'";
                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            dataReader.Read();

                            device = Maper.MapDevice(dataReader);
                        }

                        return device;
                    }
                    catch (MySqlException e)
                    {
                        throw e;
                    }

                }
            }
        }

        /// <summary>
        /// Deleta um dispositivo no banco
        /// </summary>
        public static void DeleteDevice(string connectionString, string deviceId)
        {
            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandText = "DELETE FROM devices WHERE device_id='" + deviceId + "'";

                    cmd.ExecuteNonQuery();
                }
                catch (MySqlException e)
                {
                    throw e;
                }
            }
        }

        /// <summary>
        /// Retorna uma lista contendo todos os dispositivos cadastrados
        /// </summary>
        public static List<Device> GetAllDevices(string connectionString)
        {
            List<Device> devices = new List<Device>();
            Device temp;

            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT * FROM devices";

                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                temp = Maper.MapDevice(dataReader);

                                devices.Add(temp);
                            }
                        }

                        return devices;
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }

                }
            }
        }

        /// <summary>
        /// Retorna uma lista contendo todos os usuários cadastrados
        /// </summary>
        public static List<User> GetAllUsers(string connectionString)
        {
            List<User> users = new List<User>();
            User temp;

            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT * FROM users";

                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                temp = Maper.MapUser(dataReader);

                                temp.password = null;

                                users.Add(temp);
                            }
                        }

                        return users;
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }

                }
            }
        }

        /// <summary>
        /// Retorna uma lista contendo todos os agendamentos de irrigação cadastrados
        /// </summary>
        public static List<IrrigationSchedule> GetAllIrrigationSchedules(string connectionString)
        {
            List<IrrigationSchedule> schedules = new List<IrrigationSchedule>();
            IrrigationSchedule temp;

            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT * FROM irrigation_schedule";

                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                temp = Maper.MapIrrigationSchedule(dataReader);

                                schedules.Add(temp);
                            }
                        }

                        return schedules;
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }

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
