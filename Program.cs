// See https://aka.ms/new-console-template for more information
using EFCore.BulkExtensions;
using Newtonsoft;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using ProtonConsole2.protonToSql;
using ProtonConsole2.ProtonToSql;
using ProtonConsole2.Utilities;
using Serilog;
using System.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;

Questioner.EditSettings();
return;
//var ctx = new Proton2Context();
var e = new Entity();
var l=new List<Entity>();
l.Add(e);
string jsonString = JsonSerializer.Serialize(l);

var dataTable = Newtonsoft.Json.JsonConvert.DeserializeObject< DataTable>(jsonString);


//code below for debugging;

var ited = MetaDataFunctions.GetViews();
var options = new JsonSerializerOptions { WriteIndented = true, NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals };
var bulkConfig2 = new BulkConfig { SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity, IncludeGraph = true };

//ctx.BulkInsertOrUpdate(ited, bulkConfig2);

Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(ited, options));
return;



//SqlLoader.LoadMetadata();
//SqlLoader.LoadEntityInstances(300);

MetadataLoader.LoadMetadata();


var ite = MetaDataFunctions.GetTables();
//ctx.BulkInsertOrUpdateOrDelete(ite);


Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(ite, options));
return;





