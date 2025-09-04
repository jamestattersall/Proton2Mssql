using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using ProtonConsole2.Utilities;
using System.Data.Common;
using System.Diagnostics;
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

    internal class SqlLoader()
    {

        public static void LoadEntityInstances(int batchNPages, bool incremental=true)
        {
            if (!ConfigurationManager.AppSettings.TestConnection())
            {
                return;
            }

            int maxId = DataFunctions.NEntities;
            long totalPages = DataFunctions.NDataPages;
            var conu = new Progress(20);
            Console.WriteLine("");

            Console.WriteLine("Loading data for {0} entities", maxId.ToString());
            List<Entity> entities = new(); ;
            using Proton2Context ctx = new();
            DbSet<Entity> entityDbSet = ctx.Entities;
            int currentpageCount = 0;
            int dataPageCount = 0;
            var sw = new Stopwatch();
            sw.Start();
            int entityId;
            var hasData = ctx.Entities.Any();
            
            Action<List<Entity>, Proton2Context> copyChangesToSqlDb = hasData ? DbSyncFunction : DbInsertFunction; ;
        
            DateTime lastUpdateTime = DateTime.MinValue;
            int maxEntityId = 0;
            using Patsts patsts = new();
            if (hasData && incremental)
            {
                lastUpdateTime = ctx.Entities.Max(e => e.LastUpdated);
                maxEntityId = ctx.Entities.Max(e => e.Id);
            }

            for (entityId = 1; entityId <= maxId; entityId++)
            {
                Entity? entity = null;
                if (patsts.MoveToPage(entityId) && (patsts.Updated > lastUpdateTime || entityId > maxEntityId ))
                {
                   entity = DataFunctions.GetEntityInstance(entityId);
                }

                if (entity != null)
                {
                    entity.LastUpdated = patsts.Updated;
                    entities.Add(entity!);
                    dataPageCount = DataFunctions.DataPageCount;
                    if (dataPageCount - currentpageCount > batchNPages)
                    {
                        currentpageCount = dataPageCount;
                        copyChangesToSqlDb(entities, ctx);
                        //entities = [];
                        entities.Clear();

                        conu.WriteProgressBar(currentpageCount, totalPages);
                        if (entityId > maxEntityId) { 
                            //switch to fast inserts as there is no existing data to merge
                            copyChangesToSqlDb = DbInsertFunction; 
                        };
                    }
                }
                else
                {
                    dataPageCount += DataFunctions.NumbPages(entityId);
                    if (dataPageCount - currentpageCount > batchNPages)
                    {
                        currentpageCount = dataPageCount;
                        conu.WriteProgressBar(currentpageCount, totalPages);
                    }
                }
            }

            copyChangesToSqlDb(entities, ctx);
            conu.WriteProgressBar((float)1);
            

            UpdateEntityNames();
            sw.Stop();
            Console.WriteLine(((double)(entityId * 60 / sw.Elapsed.TotalSeconds)).ToString("#0.0") + " entity instances/min");
        }
        public static void DbInsertFunction(List<Entity> entities, Proton2Context ctx)
        {
            ctx.BulkInsert(entities, o =>
            {
                o.IncludeGraph = true;
                o.SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity;
            });

        }
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
        
        public static void LoadIndexes(int nItemsBuffer=100)
        {
            if (!ConfigurationManager.AppSettings.TestConnection())
            {
                return;
            }

            Console.WriteLine("");
            Console.WriteLine("Loading indexes");

            Progress.WriteProgress(0);
            using Proton2Context ctx = new();
            var bulkConfig = new BulkConfig { PreserveInsertOrder = true, SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity, ReplaceReadEntities = true };
            var bulkConfig2 = new BulkConfig { SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity & SqlBulkCopyOptions.CheckConstraints, IncludeGraph = true };

            List<DataContext.Index> list = [];
            using Proton.Index index = new();
            using Proton.IndexDef indexDef = new();
            int totalPages = index.NPages;
            int bufferCount=0;
            int currentPageId = 0;
            Progress progress = new(20);
            index.PageCounterReset();
            var dataExists = ctx.Indexes.Any();
            //ctx.Database.ExecuteSql($"ALTER TABLE Indexes NOCHECK CONSTRAINT FK_Indexes_Entities_EntityId");
            var sw = new Stopwatch();
            sw.Start();
            for (short indexTypeId = 1; indexTypeId <= indexDef.NPages; indexTypeId++)
            {
                if (indexDef.MoveToPage(indexTypeId))
                {
                    var start = indexDef.IndexIdStart;
                    if (index.MoveToPage(start))
                    {
                        index.SetBlockLength(indexDef.KeyLength);
                        while (index.MoveToNextBlock())
                        {
                            string key = index.KeyText.Trim();
                            if (!key.IsNullOrEmpty())
                            {
                                list.Add( new()
                                {
                                    Term = key.Trim(),
                                    IndexTypeId =  index.IndexDefId,
                                    EntityId = index.EntityId
                                }); 
                               
                                if(index.IndexId != currentPageId)
                                {
                                    currentPageId = index.IndexId;
                                    bufferCount++;
                                }
                            } 
                            if (bufferCount > nItemsBuffer)
                            {
                                //fails if there are duplicates in the list

                                ctx.BulkInsertOrUpdate(list.Distinct(new IndexComparer()), bulkConfig );

                                progress.WriteProgressBar( index.PageCounter, totalPages);
                                list = [];
                                bufferCount = 0;
                            }
                        }
                    }
                }
             
            }

            ctx.BulkInsertOrUpdate(list.Distinct(), bulkConfig);

            progress.WriteProgressBar((float)1);
            Console.WriteLine();
            sw.Stop();
            Console.WriteLine((sw.ElapsedMilliseconds / 1000).ToString() + "sec.");
        }

        public static void UpdateEntityNames()
        {
            string sql = @"
UPDATE Entities
SET Name=i.Term, EntityTypeId=a.EntityTypeId
FROM Attributes a
INNER JOIN EntityTypes et on et.IdAttributeId=a.AttributeId
INNER JOIN Indexes i ON i.Id=et.DefaultIndexTypeId 
INNER JOIN Entities e ON e.Id=i.EntityId";

            using Proton2Context ctx = new();
            ctx.Database.ExecuteSqlRaw(sql);

        }

        public static void LoadMetadata()
        {
            if (!ConfigurationManager.AppSettings.TestConnection())
            {
               return;
            }
            float nTables = 12;

            using Proton2Context ctx = new();
            var progress = new Progress(10);
            float  c = 1;
            var bulkConfig = new BulkConfig { SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity, UseTempDB = false };
            var bulkConfig2 = new BulkConfig { SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity, IncludeGraph=true , UseTempDB=false};
            Console.WriteLine("Loading /updating metadata");

            if (ctx.DataTypes.Any())
            {
                ctx.DataTypes.UpdateRange(MetaDataFunctions.GetDataTypes());
                //ctx.BulkInsertOrUpdate(MetaDataFunctions.GetDataTypes(), bulkConfig);
            } else
            {
                ctx.DataTypes.AddRange(MetaDataFunctions.GetDataTypes());
                //ctx.BulkInsert(MetaDataFunctions.GetDataTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.Tables.Any())
            {
                ctx.Tables.UpdateRange(MetaDataFunctions.GetTables());
                //ctx.BulkInsertOrUpdate(MetaDataFunctions.GetTables(), bulkConfig);
            }
            else
            {
                ctx.Tables.AddRange(MetaDataFunctions.GetTables());
                //ctx.BulkInsert(MetaDataFunctions.GetTables(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
       


            if (ctx.Views.Any())
            {
                ctx.Views.AddRange(MetaDataFunctions.GetViews());
                // ctx.BulkInsertOrUpdate(MetaDataFunctions.GetViews(), bulkConfig);
            }
            else
            {
                ctx.Views.AddRange(MetaDataFunctions.GetViews());
                //ctx.BulkInsert(MetaDataFunctions.GetViews(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
           

            if (ctx.EntityTypes.Any())
            {
                ctx.EntityTypes.UpdateRange(MetaDataFunctions.GetEntityTypes());
                //ctx.BulkInsertOrUpdate(MetaDataFunctions.GetEntityTypes(), bulkConfig);
            }
            else
            {

                ctx.EntityTypes.AddRange(MetaDataFunctions.GetEntityTypes());
                //ctx.BulkInsert(MetaDataFunctions.GetEntityTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c/ nTables); c++;
            ctx.SaveChanges();

            if (ctx.IndexTypes.Any())
            {
                ctx.IndexTypes.UpdateRange(IndexTypeReader.GetIndexTypes());
               // ctx.BulkInsertOrUpdate(IndexTypeReader.GetIndexTypes(), bulkConfig);
            }
            else
            {

                ctx.IndexTypes.AddRange(IndexTypeReader.GetIndexTypes());
                //ctx.BulkInsert(IndexTypeReader.GetIndexTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

           
            if (ctx.Attributes.Any())
            {
                ctx.Attributes.UpdateRange(MetaDataFunctions.GetAttributes());
                //ctx.BulkInsertOrUpdate(MetaDataFunctions.GetAttributes(), bulkConfig);
            }
            else
            {
                ctx.Attributes.AddRange(MetaDataFunctions.GetAttributes());
                //ctx.BulkInsert(MetaDataFunctions.GetAttributes(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            

            if (ctx.ViewAttributes.Any())
            {
                ctx.ViewAttributes.UpdateRange(MetaDataFunctions.GetViewAttributes());
                // ctx.BulkInsertOrUpdate(MetaDataFunctions.GetViews(), bulkConfig);
            }
            else
            {
                ctx.ViewAttributes.AddRange(MetaDataFunctions.GetViewAttributes());
                //ctx.BulkInsert(MetaDataFunctions.GetViews(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.ViewCaptions.Any())
            {
                ctx.ViewCaptions.UpdateRange(MetaDataFunctions.GetViewCaptions());
                // ctx.BulkInsertOrUpdate(MetaDataFunctions.GetViews(), bulkConfig);
            }
            else
            {
                ctx.ViewCaptions.AddRange(MetaDataFunctions.GetViewCaptions());
                //ctx.BulkInsert(MetaDataFunctions.GetViews(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();


            if (ctx.LookupTypes.Any())
            {
                ctx.LookupTypes.UpdateRange(MetaDataFunctions.GetLookupTypes());
                //ctx.BulkInsertOrUpdate(MetaDataFunctions.GetLookupTypes(), bulkConfig);
            }
            else
            {
                ctx.LookupTypes.AddRange(MetaDataFunctions.GetLookupTypes());
               // ctx.BulkInsert(MetaDataFunctions.GetLookupTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.Lookups.Any())
            {
                ctx.Lookups.UpdateRange(MetaDataFunctions.GetLookups());
                //ctx.BulkInsertOrUpdate(MetaDataFunctions.GetLookups(), bulkConfig);
            }
            else
            {
                ctx.Lookups.AddRange(MetaDataFunctions.GetLookups());
                //ctx.BulkInsert(MetaDataFunctions.GetLookups(), bulkConfig);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();



            if (ctx.Menus.Any())
            {
                ctx.Menus.UpdateRange(MetaDataFunctions.GetMenus());
                //ctx.BulkInsertOrUpdate(MetaDataFunctions.GetMenus(), bulkConfig2);
            }
            else
            {
                ctx.Menus.AddRange(MetaDataFunctions.GetMenus());
                //ctx.BulkInsert(MetaDataFunctions.GetMenus(), bulkConfig2);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.MenuItems.Any())
            {
                ctx.MenuItems.UpdateRange(MetaDataFunctions.GetMenuItems());
                //ctx.BulkInsertOrUpdate(MetaDataFunctions.GetMenus(), bulkConfig2);
            }
            else
            {
                ctx.MenuItems.AddRange(MetaDataFunctions.GetMenuItems());
                //ctx.BulkInsert(MetaDataFunctions.GetMenus(), bulkConfig2);
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.UserStarters.Any())
            {

                ctx.UserStarters.UpdateRange(MetaDataFunctions.GetUserStarters());
                //ctx.BulkInsertOrUpdate(MetaDataFunctions.GetUserStarters(), bulkConfig);
            }
            else
            {
                ctx.UserStarters.AddRange(MetaDataFunctions.GetUserStarters());
                //ctx.BulkInsert(MetaDataFunctions.GetUserStarters(), bulkConfig);
            }
            progress.WriteProgressBar(1);
            ctx.SaveChanges();


        }
    }
}
