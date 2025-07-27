using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.IdentityModel.Tokens;
using NetTopologySuite.Geometries;
using NetTopologySuite.Triangulate.Tri;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using ProtonConsole2.ProtonBinaryReaders;
using ProtonConsole2.ProtonToSql;
using ProtonConsole2.Utilities;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design.Serialization;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
namespace ProtonConsole2.ProtonToSql
{
    internal class SqlLoader()
    {

        public static void LoadEntityInstances(int batchNPages, bool incremental=true)
        {
            int maxId = DataFunctions.NEntities;
            long totalPages = DataFunctions.NDataPages;
            var conu = new Progress(20);
            Console.WriteLine("");

            Console.WriteLine("Loading data for {0} entities", maxId.ToString());
            List<Entity> entities = [];
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
                maxEntityId = ctx.Entities.Max(e => e.EntityId);
            }

            for (entityId = 1; entityId <= maxId; entityId++)
            {
                Entity? entity = null;
                if (patsts.MoveToPage(entityId) && (patsts.Updated > lastUpdateTime || entityId > maxEntityId || entityId < 300))
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
                        entities = [];

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
            ctx.BulkInsertOptimized(entities, o =>
            {
                o.IncludeGraph = true;
                o.InsertKeepIdentity = true;
                o.DisableValueGenerated = true;
            });

        }
        public static void DbSyncFunction(List<Entity> entities, Proton2Context ctx)
        {
            var es = (from e in entities
                     select e.EntityId).ToArray();
            ctx.ValueTexts.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueNumbers.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueLookups.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueLongTexts.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueDates.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueTimes.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.ValueEntities.Where(e => es.Contains(e.EntityId)).ExecuteDelete();
            ctx.Entities.Where(e => es.Contains(e.EntityId)).ExecuteDelete();

            ctx.BulkInsertOptimized(entities, o =>
            {
                o.IncludeGraph = true;
                o.InsertKeepIdentity = true;
                o.DisableValueGenerated = true;

            });


        }
        

        public static void LoadIndexes(int nItemsBuffer=100)
        {
            Console.WriteLine("");
            Console.WriteLine("Loading indexes");

            Progress.WriteProgress(0);
            using Proton2Context ctx = new();
            var bulkConfig = new BulkConfig { PreserveInsertOrder = true, SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity   };
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
            ctx.Database.ExecuteSql($"ALTER TABLE Indexes NOCHECK CONSTRAINT FK_Indexes_Entities_EntityId");

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
                                list.Add(new()
                                {
                                    Term = key,
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
                                if (dataExists)
                                {
                                    ctx.Indexes.BulkInsertOptimized(list, o => { o.SqlBulkCopyOptions= (int?)SqlBulkCopyOptions.KeepIdentity; o.InsertIfNotExists = true; o.InsertKeepIdentity = true; o.AllowDuplicateKeys = true; } );
                                }else {
                                    ctx.Indexes.BulkInsertOptimized(list, o => { o.SqlBulkCopyOptions = (int?)SqlBulkCopyOptions.KeepIdentity; o.InsertIfNotExists = true; o.InsertKeepIdentity = true; o.AllowDuplicateKeys = true; });
                                }

                                progress.WriteProgressBar( index.PageCounter, totalPages);
                                list = [];
                                bufferCount = 0;
                            }
                        }
                    }
                }
             
            }

            if (dataExists)
            {
                ctx.Indexes.BulkInsertOptimized(list, o => { o.SqlBulkCopyOptions = (int?)SqlBulkCopyOptions.KeepIdentity; o.InsertIfNotExists = true; o.InsertKeepIdentity = true; o.AllowDuplicateKeys = true; });
            }
            else
            {
                ctx.BulkInsertOrUpdate(list, bulkConfig);
            }
            progress.WriteProgressBar((float)1);
            Console.WriteLine();
        }

        public static void UpdateEntityNames()
        {
            string sql = @"
UPDATE Entities
SET Name=i.Term, EntityTypeId=a.EntityTypeId
FROM Attributes a
INNER JOIN EntityTypes et on et.IdAttributeId=a.AttributeId
INNER JOIN Indexes i ON i.IndexTypeId=et.DefaultIndexTypeId 
INNER JOIN Entities e ON e.EntityId=i.EntityId";

            using Proton2Context ctx = new();
            ctx.Database.ExecuteSqlRaw(sql);

        }

        public static void LoadMetadata()
        {
            using Proton2Context ctx = new();
            var progress = new Progress(10);
            float  c = 0;
            var bulkConfig = new BulkConfig { SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity,   };
            var bulkConfig2 = new BulkConfig { SqlBulkCopyOptions = SqlBulkCopyOptions.KeepIdentity, IncludeGraph=true };
            Console.WriteLine("Loading /updating metadata");

            if (ctx.DataTypes.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetDataTypes(), bulkConfig);
            } else
            {
                ctx.BulkInsert(MetaDataFunctions.GetDataTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c / (float)9); c++;


            if (ctx.EntityTypes.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetEntityTypes(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetEntityTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c/(float)9); c++;

            if (ctx.IndexTypes.Any())
            {
                ctx.BulkInsertOrUpdate(IndexTypeReader.GetIndexTypes(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(IndexTypeReader.GetIndexTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c / (float)9); c++;

            if (ctx.Tables.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetTables(), bulkConfig2);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetTables(), bulkConfig2);
            }
            progress.WriteProgressBar(c / (float)9); c++;

            if (ctx.Views.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetViews(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetViews(), bulkConfig);
            }
            progress.WriteProgressBar(c / (float)9); c++; 

            if (ctx.LookupTypes.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetLookupTypes(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetLookupTypes(), bulkConfig);
            }
            progress.WriteProgressBar(c / (float)9); c++;

            if (ctx.Lookups.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetLookups(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetLookups(), bulkConfig);
            }
            progress.WriteProgressBar(c / (float)9); c++;



            if (ctx.Menus.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetMenus(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetMenus(), bulkConfig);
            }
            progress.WriteProgressBar(c / (float)9); c++;


            ctx.BulkInsertOrUpdate(MetaDataFunctions.GetMenus(), bulkConfig2);
            progress.WriteProgressBar(c / (float)9); c++;


            if (ctx.UserStarters.Any())
            {
                ctx.BulkInsertOrUpdate(MetaDataFunctions.GetUserStarters(), bulkConfig);
            }
            else
            {
                ctx.BulkInsert(MetaDataFunctions.GetUserStarters(), bulkConfig);
            }
            progress.WriteProgressBar(c / (float)9); c++;


        }
    }
}
