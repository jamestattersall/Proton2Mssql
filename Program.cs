
using ProtonConsole2.protonToSql;
using ProtonConsole2.Utilities;
using Serilog;

if (args.Length > 0)
{
    Console.WriteLine(args[0]);
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File(Path.Combine(ConfigurationManager.AppSettings.PathToLogs ,"AppLog.txt"), rollingInterval: RollingInterval.Day)
        .CreateLogger();

    using ValuesLoader dsl = new();
    dsl.LoadValues(1000);
    EntityLoader.LoadIndexes(1000);
    EntityLoader.LoadEntities(1000);
    EntityLoader.UpdateEntityNames();
}
else Questioner.EditSettings();
return;





