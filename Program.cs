
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.IdentityModel.Tokens;
using ProtonConsole2.protonToSql;
using ProtonConsole2.Utilities;
using Serilog;

if (args.Length == 0 && ConfigurationManager.AppSettings.IsValid())
{
    Log.Logger = new LoggerConfiguration()
        .MinimumLevel.Debug()
        .WriteTo.Console()
        .WriteTo.File(Path.Combine(ConfigurationManager.AppSettings.PathToLogs ,"AppLog.txt"), rollingInterval: RollingInterval.Day)
        .CreateLogger();

    using ValuesLoader dsl = new();
    Log.Information($"Main task started: {0}", DateTime.Now.ToString());
    dsl.LoadValues(1000);
    if(dsl.EntitiesLoaded>0 || dsl.EntitiesUpdated > 0)
    {
        EntityLoader.LoadIndexes(1000);
        EntityLoader.LoadEntities(1000);
        EntityLoader.UpdateEntityNames();
    }
}
else Questioner.EditSettings();
return;





