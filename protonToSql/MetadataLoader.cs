using EFCore.BulkExtensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using ProtonConsole2.Utilities;
using Newtonsoft.Json;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using SqlBulkCopyOptions = EFCore.BulkExtensions.SqlBulkCopyOptions;

namespace ProtonConsole2.ProtonToSql
{
    class IndexComparer : IEqualityComparer<DataContext.Index>
    {
        public bool Equals(DataContext.Index x, DataContext.Index y)
        {

            if (Object.ReferenceEquals(x, y)) return true;

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
                return false;

            return x.IndexTypeId == y.IndexTypeId && x.Term == y.Term && x.EntityId==y.EntityId;
        }

        // If Equals() returns true for a pair of objects
        // then GetHashCode() must return the same value for these objects.

        public int GetHashCode(DataContext.Index index)
        {
            //Check whether the object is null
            if (Object.ReferenceEquals(index, null)) return 0;

            //Get hash code for the Name field if it is not null.
            int hashTerm = index.Term == null ? 0 : index.Term.GetHashCode();

            //Get hash code for the Code field.
            int hashIndexType = index.IndexTypeId.GetHashCode();


            //Get hash code for the Code field.
            int hashEntityId = index.EntityId.GetHashCode();

            //Calculate the hash code for the product.
            return hashTerm ^ hashIndexType ^ hashEntityId;
        }
    }

    public class MetadataLoader()
    {

        public static void DbSyncFunction(List<Entity> entities, Proton2Context ctx)
        {
            var es = (from e in entities
                      select e.Id).ToArray();
            ctx.ValueTexts.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueNumbers.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueLookups.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueLongTexts.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueDates.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueTimes.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueEntities.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.Entities.Where(e => es.Contains(e.Id)).ExecuteDelete();

            ctx.BulkInsert(entities, o =>
            {
                o.IncludeGraph = true;
                o.SqlBulkCopyOptions=SqlBulkCopyOptions.KeepIdentity;
            });

        }
        
        public static void LoadMetadata()
        {
            if (!ConfigurationManager.AppSettings.TestConnection())
            {
               return;
            }
            float nTables = 11;

            using Proton2Context ctx = new();

            ctx.Database.ExecuteSql($"EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");
            var progress = new Progress(10);
            float  c = 1;
            var bulkConfig = new BulkConfig { SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity, UseTempDB = false };
            Console.WriteLine("Loading /updating metadata");

            if (ctx.DataTypes.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetDataTypes(), bulkConfig);
            } else
            {
               ctx.BulkInsert(MetaDataFunctions.GetDataTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.Tables.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetTables(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetTables(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
       


            if (ctx.Views.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetViews(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetViews(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
           

            if (ctx.EntityTypes.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetEntityTypes(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetEntityTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c/ nTables); c++;
            ctx.SaveChanges();

            if (ctx.IndexTypes.Any())
            {
               ctx.BulkInsertOrUpdate(MetaDataFunctions.GetIndexTypes(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetIndexTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

           
            if (ctx.Attributes.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetAttributes(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetAttributes(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            

            if (ctx.ViewAttributes.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetViewAttributes(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetViewAttributes(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.ViewCaptions.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetViewCaptions(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetViewCaptions(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();


            if (ctx.LookupTypes.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetLookupTypes(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetLookupTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.Menus.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetMenus(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetMenus(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.MenuItems.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetMenuItems(), bulkConfig);
            }
            else
            {
                ctx.MenuItems.AddRange(MetaDataFunctions.GetMenuItems());
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.UserStarters.Any())
            {

                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetUserStarters(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetUserStarters(), bulkConfig);
            }
            progress.WriteProgressBar(1);
            ctx.SaveChanges();


        }
    }
}
