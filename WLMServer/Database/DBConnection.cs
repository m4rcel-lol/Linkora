using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Data;
using System.Runtime.InteropServices;
using MySql.Data.MySqlClient;

namespace WLMServer.Database
{
    class DBConnection
    {
        public string connectionString = "";
        public MySqlConnection dbConnection;

        public DBConnection()
        {
            connectionString = String.Format("server={0};user id={1}; password={2}; database=msn; pooling=false; Charset=utf8;{3}",
                Config.Properties.DATABASE_HOST, Config.Properties.DATABASE_ID, Config.Properties.DATABASE_PASSWORD,
                KeepaliveOption);
            dbConnection = new MySqlConnection(connectionString);
            dbConnection.Open();
        }

        /// <summary>
        /// The connector implements Keepalive with a Windows only socket control code, so asking
        /// for it anywhere else throws before the connection is even attempted. The periodic
        /// "SELECT 1" in <see cref="CheckDatabaseAccess"/> keeps the connection alive regardless.
        /// </summary>
        private static string KeepaliveOption
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? " Keepalive=60;" : "";
            }
        }

        public void CheckDatabaseAccess()
        {
            if (dbConnection.State != ConnectionState.Open &
                !(dbConnection.State == ConnectionState.Executing || dbConnection.State == ConnectionState.Fetching))
            {
                dbConnection.Close();
                dbConnection.Open();
            }
            else
            {
                try
                {
                    MySqlCommand command = new MySqlCommand();
                    command.Connection = dbConnection;
                    command.CommandType = CommandType.Text;
                    command.CommandText = "SELECT 1;";
                    command.Prepare();

                    command.ExecuteNonQuery();
                }
                catch
                {
                    dbConnection.Close();
                    dbConnection.Open();
                }
            }
        }

        public void Write(string query, MySqlCommand command)
        {
            CheckDatabaseAccess();

            command.Connection = dbConnection;
            command.CommandType = CommandType.Text;
            command.CommandText = query;
            command.Prepare();

            command.ExecuteNonQuery();
        }

        public MySqlDataReader Read(string query, MySqlCommand command)
        {
            CheckDatabaseAccess();

            MySqlDataReader reader;

            command.Connection = dbConnection;
            command.CommandType = CommandType.Text;
            command.CommandText = query;
            command.Prepare();

            reader = command.ExecuteReader();

            return reader;
        }
    }
}
