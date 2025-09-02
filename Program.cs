// See https://aka.ms/new-console-template for more information
using EFCore.BulkExtensions;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using ProtonConsole2.ProtonToSql;
using ProtonConsole2.Utilities;
using System.Text.Json;


Questioner.HomeEdit();
return;

var ctx = new Proton2Context();
//code below for debugging;
var ited = MetaDataFunctions.GetViews();
var options = new JsonSerializerOptions { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
var bulkConfig2 = new BulkConfig { SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity, IncludeGraph = true };

ctx.BulkInsertOrUpdate(ited, bulkConfig2);

Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(ited, options));
return;



//SqlLoader.LoadMetadata();
//SqlLoader.LoadEntityInstances(300);

SqlLoader.LoadMetadata();


var ite = MetaDataFunctions.GetTables();
//ctx.BulkInsertOrUpdateOrDelete(ite);


Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(ite, options));
return;





