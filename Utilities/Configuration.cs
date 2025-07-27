using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.Utilities
{
    public static class ConfigurationManager
    {
        static ConfigurationManager()
        {
            path = Directory.GetCurrentDirectory() + "\\appsettings.json";
            AppSettings = new();
            LoadSettings();
            if(AppSettings.UserPassword.IsNullOrEmpty())
            {
                SetUserPassword();
            }
            if (AppSettings.PathToProtonFolder.IsNullOrEmpty())
            {
                SetPathToProtonDbs();
            }
            if (AppSettings.Server.IsNullOrEmpty())
            {
               SetDbServer();
            }
            if (AppSettings.DBname.IsNullOrEmpty())
            {
                SetDBname();
            }

            if (!AppSettings.DBIsIntegrated! && AppSettings.DBPassword.IsNullOrEmpty())
            {
               SetDbIntegretedSecurity();
                if (!AppSettings.DBIsIntegrated)
                {
                    SetDbPassword();
                }
            }
        }

        private static string path;

        public static AppSettings AppSettings { get; set; }

        public static void SaveSettings(bool withFeedback = true)
        {
            string json = System.Text.Json.JsonSerializer.Serialize<AppSettings>(AppSettings);
            System.IO.File.WriteAllText(path , Protection.Crypt(json));
            if (withFeedback)
            {
                Console.WriteLine("settings saved successfully");
                Console.WriteLine("Press enter to continue");

            }
        }

        public static void LoadSettings()
        {
            if (System.IO.File.Exists(path))
            {
                string enc = System.IO.File.ReadAllText(path );
                string json = Protection.Decrypt(enc);
                AppSettings? asst  = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json);
                if (asst != null ) {AppSettings = asst;}

            } else
            {
                SaveSettings();
            }
        }

        public static void SetUserPassword()
        {
            string? pwd = Questioner.GetStringResponse("Enter password");
            if (!pwd.IsNullOrEmpty())
            {
                AppSettings.UserPassword = pwd!;
                SaveSettings();
            }
        }

        public static void SetDBname()
        {
            string? dbn = Questioner.GetStringResponse("Enter DB name", AppSettings.DBname);
            if (!dbn.IsNullOrEmpty())
            {
                AppSettings.DBname = dbn!;
                SaveSettings();
            }

        }

        public static void SetPathToProtonDbs()
        {
            int trycount = 0; ;
            while (trycount<4) { 
                string? path = Questioner.GetStringResponse("Enter Proton dbs files directory path:", AppSettings.PathToProtonFolder);
           
                if (System.IO.Directory.Exists(path)) 
                {
                    var filename = path.EndsWith("/")? "BASE.DBS" : "/BASE.DBS";
                    if (System.IO.File.Exists(path + filename))
                    {
                        AppSettings.PathToProtonFolder = path!;
                        SaveSettings();
                        trycount = 10; ;

                    }
                    Console.WriteLine("Directory does not contain base.dbs file.");
                }
                else
                {
                    Console.WriteLine("Directory not found");
                }
                trycount++;
            }
            Questioner.EditSettings();
        }

        public static void SetDbServer()
        {

            int trycount = 0; ;
            while (trycount < 4) { 
                var sa = Questioner.GetStringResponse("Enter SQL server address", AppSettings.Server);
                if (!sa.IsNullOrEmpty())
                {
                    var pi = new Ping();
                    try
                    {
                        PingReply reply = pi.Send(sa);
                        if (reply.Status == IPStatus.Success)
                        {
                            AppSettings.Server = sa!;
                            SaveSettings();
                            trycount = 10;

                        }
                        else
                        {
                            Console.WriteLine("Server not contactable");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                    trycount++;
                }
            }

            Questioner.EditSettings();

        }

        public static void SetDbPassword()
        {
            int trycount = 0;
            while (trycount < 4)
            {
                string? pwd = Questioner.GetStringResponse("Enter DB password", AppSettings.DBPassword);
                if (!pwd.IsNullOrEmpty())
                {
                    AppSettings.DBPassword = pwd!;
                    if (AppSettings.TestConnection())
                    {
                        SaveSettings();
                        trycount = 10;
                    }
                    Console.WriteLine("Unable to login with: " + AppSettings.SQLConnectionString());
                }
                trycount++;
            }

            Questioner.EditSettings();
        }


        public static void SetDbIntegretedSecurity()
        {
            bool? igp = Questioner.GetBoolResponse("Db Integrated security", AppSettings.DBIsIntegrated);
            if (igp != null)
            {
                AppSettings.DBIsIntegrated = (bool)igp!;
                SaveSettings();
            }
        }
    }

    public class AppSettings
    {

        public  string UserPassword { get; set; } = string.Empty;
        public  string Server { get; set; } = string.Empty;
        public  string DBname { get; set; } = string.Empty;
        public  string DBPassword { get; set; } = string.Empty;
        public  bool DBIsIntegrated { get; set; } = false;
        public string PathToProtonFolder { get; set; } = string.Empty;
        public  DateTime LastUpdate { get; set; } = DateTime.MinValue;

        public string SQLConnectionString()
        {
            var sb = new SqlConnectionStringBuilder();
            sb.DataSource = Server;
            sb.InitialCatalog = DBname;
            sb.IntegratedSecurity = DBIsIntegrated;
            if (!DBIsIntegrated )  sb.Password = DBPassword;
            sb.CommandTimeout = 100000;
            sb.TrustServerCertificate = true;
            return sb.ToString();
        }

        public bool TestConnection()
        {
            using (SqlConnection connection = new SqlConnection(SQLConnectionString()))
            {
                try
                {
                    connection.Open();
                    return true;
                }
                catch (SqlException)
                {
                    return false;
                }
            }
        }
    }

    public static class Protection
    {
        public static string Crypt(string text)
        {
            return Convert.ToBase64String(
                ProtectedData.Protect(
                    Encoding.Unicode.GetBytes(text), null, DataProtectionScope.LocalMachine));
        }

        public static string Decrypt( string text)
        {
            return Encoding.Unicode.GetString(
                ProtectedData.Unprotect(
                     Convert.FromBase64String(text), null, DataProtectionScope.LocalMachine));
        }
    }
}