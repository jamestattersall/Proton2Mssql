using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Configuration;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;

namespace ProtonConsole2.Utilities
{
    internal class Questioner
    {
        public static bool GetBoolResponse(string caption, bool defaultValue) 
        {
            string? retn = GetStringResponse(caption + " Y/N?", defaultValue ? "Y":"N");
            if (!retn.IsNullOrEmpty())
            {
                if  (retn!.ToUpper().StartsWith("Y", StringComparison.CurrentCultureIgnoreCase)) return true;
                if (retn.ToUpper().StartsWith("N", StringComparison.CurrentCultureIgnoreCase)) return false;
            }
            return false;
        }
        public static int? GetIntResponse(string caption, int defaultValue = 0)
        {
            string? retn = GetStringResponse(caption, defaultValue.ToString());
            if (!retn.IsNullOrEmpty())
            {
                int op;
                if (int.TryParse(retn, out op))
                    {
                    return op;
                }

            }
            return null;
        }
        public static string? GetStringResponse(string caption, string defaultValue = "")
        {
            Console.WriteLine(caption);
            Console.WriteLine("Current value: " + defaultValue);
            return  Console.ReadLine();
        }

        public static void HomeEdit()
        {
            Console.Clear();
            Console.WriteLine("[1]  Edit settings,");
            Console.WriteLine("[2]  Load/update metadata,");
            Console.WriteLine("[3]  Load/update entity data,");
            Console.WriteLine("[0]  Exit");
            using Proton2Context ctx = new();
            int? optn = null;
            while (optn != 0)
            {

                optn = Questioner.GetIntResponse("Choose option 0-5:");
                switch (optn)
                {
                    case 1:
                        Questioner.EditSettings();
                        break;
                    case 2:

                        if (ConfigurationManager.AppSettings.TestConnection())
                        {
                            ProtonToSql.SqlLoader.LoadMetadata();
                            ProtonToSql.SqlLoader.LoadIndexes();
                        }
                        
                        break;
                    case 3:
                        ProtonToSql.SqlLoader.LoadEntityInstances(200); 
                        break;
                    case 0:
                        break;
                }
            }
            return;
        }
       public static void EditSettings() 
       {
            var apsettings = ConfigurationManager.AppSettings;
            string capt;
            Console.Clear();
            Console.WriteLine("[1] SQL Db Server: " + apsettings.Server);
            Console.WriteLine("[2] DB name: " + apsettings.DBname);
            Console.WriteLine("[3] DB integrated security: " + apsettings.DBIsIntegrated.ToString());
            Console.WriteLine("[4] DB Password: ");
            Console.WriteLine("[5] Proton.dbs files directory: " + apsettings.PathToProtonFolder);
            if(apsettings.IsValid())
            {
                using Proton2Context ctx = new();
                Console.WriteLine("[6] Load/update metadata...");
                Console.WriteLine("[7] Load/update entity data...");
                capt = "Enter number 0-7;";
            }
            else capt = "Enter number 0-5;";
            Console.WriteLine("[0] Exit");
            int? opt = null;
            while (opt != 0)
            {
                opt = GetIntResponse(capt);
                switch (opt)
                {
                    case 1:
                        ConfigurationManager.SetDbServer();
                        break;
                    case 2:
                        ConfigurationManager.SetDBname();
                        break;
                    case 3:
                        ConfigurationManager.SetDbIntegretedSecurity();
                        break;
                    case 4:
                        ConfigurationManager.SetDbPassword();
                        break;

                    case 5:
                        ConfigurationManager.SetPathToProtonDbs();
                        break;

                    case 6:

                        if (apsettings.IsValid())
                        {
                            ProtonToSql.SqlLoader.LoadMetadata();
                            ProtonToSql.SqlLoader.LoadIndexes();
                        }
                        break;
                    case 7:
                        if (apsettings.IsValid())
                        {
                            ProtonToSql.SqlLoader.LoadEntityInstances(200);
                        }
                        break;

                    default:
                        break;
                }
            }
           
       }
    }
}
