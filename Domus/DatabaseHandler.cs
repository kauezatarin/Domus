using System;
using System.Collections.Generic;
using System.Text;
using MySql.Data.MySqlClient;

namespace Domus
{
    static class DatabaseHandler
    {
        /*
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
        /// Testa a conexão com o banco.
        /// </summary>
        public static void TestConnection(string connectionString)
        {
            using (var conn = new MySqlConnection(connectionString))
            {
                try
                {
                    conn.Open();
                }
                catch (Exception e)
                {
                    throw e;
                }
            }
        }

        /// <summary>
        /// Insere um novo usuario no banco
        /// </summary>
        public static void InsertUser(string connectionString, User user)
        {

            using (var conn = new MySqlConnection(connectionString))
            using (var cmd = conn.CreateCommand())
            {
                try
                {
                    conn.Open();
                    cmd.CommandText = "INSERT INTO Users (user_name,user_passwd,email,user_score,firstname,lastname,position) values('" + user.userName +
                                      "','" + user.passwd +
                                      "','" + user.email +
                                      "'," + user.score +
                                      ",'" + user.firstName +
                                      "','" + user.lastName +
                                      "','" + user.position + "')";

                    cmd.ExecuteNonQuery();
                }
                catch (Exception e)
                {
                    throw e;
                }

            }
        }

        /// <summary>
        /// Busca na base as informações do usuario para login
        /// </summary>
        public static List<User> LoginRequest(string connectionString, string username)
        {
            List<User> list = new List<User>();

            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT * FROM Users WHERE user_name = '" + username + "'";

                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                list.Add(MapUser(dataReader));
                            }
                        }

                        return list;
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }

                }
            }
        }

        /// <summary>
        /// Retorna o tutorial referente ao Id informado
        /// </summary>
        public static Tutorial getTutorial(string connectionString, int tutorialId)
        {
            Tutorial tutorial;

            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT * FROM Tutorials WHERE tutorial_id = " + tutorialId;

                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            dataReader.Read();

                            tutorial = MapTutorial(dataReader);
                        }

                        return tutorial;
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }

                }
            }
        }

        /// <summary>
        /// Retorna todos os tutoriais.
        /// </summary>
        public static List<Tutorial> getTutorials(string connectionString)
        {
            List<Tutorial> list = new List<Tutorial>();

            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT * FROM Tutorials";

                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                list.Add(MapTutorial(dataReader));
                            }
                        }

                        return list;
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }

                }
            }
        }

        /// <summary>
        /// Retorna o nivel referente ao Id informado
        /// </summary>
        public static Level getLevel(string connectionString, int levelId)
        {
            throw new Exception("Função não implementada");
        }

        /// <summary>
        /// Retorna todos os niveis.
        /// </summary>
        public static List<Level> getLevels(string connectionString)
        {
            List<Level> list = new List<Level>();

            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT * FROM Levels";

                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                list.Add(MapLevel(dataReader));
                            }
                        }

                        return list;
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }

                }
            }
        }

        /// <summary>
        /// Retorna todos os niveis concluidos pelo usuario.
        /// </summary>
        public static List<UserCompletedLevel> getCompletedLevels(string connectionString, int userId)
        {
            List<UserCompletedLevel> list = new List<UserCompletedLevel>();

            using (var conn = new MySqlConnection(connectionString))
            {
                using (var cmd = conn.CreateCommand())
                {
                    try
                    {
                        conn.Open();
                        cmd.CommandText = "SELECT * FROM UsersCompletedLevels WHERE user_id = " + userId;

                        using (MySqlDataReader dataReader = cmd.ExecuteReader())
                        {
                            while (dataReader.Read())
                            {
                                list.Add(MapCompletedLevel(dataReader));
                            }
                        }

                        return list;
                    }
                    catch (Exception e)
                    {
                        throw e;
                    }

                }
            }
        }

        /// <summary>
        /// Mapeia o datareader para um User
        /// </summary>
        public static User MapUser(MySqlDataReader dataReader)
        {
            User temp = new User(
                dataReader.GetInt32("user_id"),
                dataReader.GetString("user_name"),
                dataReader.GetString("user_passwd"),
                dataReader.GetInt32("user_score"),
                dataReader.GetString("firstname"),
                dataReader.GetString("lastname"),
                dataReader.GetString("position"),
                dataReader.GetString("email"));

            return temp;
        }

        /// <summary>
        /// Mapeia o datareader para um Tutorial
        /// </summary>
        public static Tutorial MapTutorial(MySqlDataReader dataReader)
        {
            Tutorial temp = new Tutorial(
                dataReader.GetInt32("tutorial_id"),
                dataReader.GetString("tutorial_name"),
                dataReader.GetString("tutorial_description"),
                dataReader.GetString("tutorial_text"),
                dataReader.GetInt32("prize_score"),
                dataReader.GetInt32("level_id"));

            return temp;
        }

        /// <summary>
        /// Mapeia o datareader para um Level
        /// </summary>
        public static Level MapLevel(MySqlDataReader dataReader)
        {
            Level temp = new Level(
                dataReader.GetInt32("level_id"),
                dataReader.GetString("level_name"),
                dataReader.GetString("level_description"),
                dataReader.GetString("level_question"),
                dataReader.GetString("level_answere"),
                dataReader.GetInt32("prize_score"),
                dataReader.GetInt32("score_needed"));

            return temp;
        }

        /// <summary>
        /// Mapeia o datareader para um UserCompletedLevel
        /// </summary>
        public static UserCompletedLevel MapCompletedLevel(MySqlDataReader dataReader)
        {
            UserCompletedLevel temp = new UserCompletedLevel(
                dataReader.GetInt32("user_id"),
                dataReader.GetBoolean("isLevel"),
                dataReader.GetInt32("level_id"),
                dataReader.GetString("answere"),
                dataReader.GetBoolean("completed"));

            return temp;
        }

           //Count statement
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
         */

    }
}
