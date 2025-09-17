using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.protonToSql
{
    internal static class EntityLoader
    {
        public static void LoadLookups(int nRows)
        {
            List<DataContext.Lookup> list = new() { Capacity = nRows };

            using MetaTableUtilities<Lookup> bx = new ();
            bx.CreateStagingTable();
            using Proton.Dict dict = new();
            using Proton.RCode code = new();
            using Proton2Context ctx = new();

            bool exists = ctx.Lookups.Any();
            Action fn;  
            int c = 0;
            var prog = new Utilities.Progress(20);
            if (exists)
            {
                fn = dbUpdateFunction;
                Console.WriteLine("Updating Lookups..");
            }
            else
            {
                fn = dbInsertFunction;
                Console.WriteLine("Loading Lookups..");
            }
            prog.WriteProgressBar(0);
            var nDicts = dict.NPages;
            var tPages = nDicts + code.NPages;
            for (int i = 1; i <= dict.NPages; i++)
            {
                if (dict.MoveToPage(i))
                {
                    list.Add(new()
                    {
                        Id = -i,
                        Name = dict.Name,
                        LookupTypeId = 0

                    });
                }
                c++;
                if (c > nRows)
                {
                     fn();
                    c = 0;
                    list.Clear();

                    prog.WriteProgressBar(i / (float)tPages);
                }
            }
            for (int i = 1; i <= code.NPages; i++)
            {
                if (code.MoveToPage(i))
                {
                    list.Add(new()
                    {
                        Id = i,
                        Name = code.Name,
                        Code = code.ReadCode,
                        LookupTypeId = code.CodeTypeID
                    });
                    c++;
                    if (c > nRows)
                    {
                        fn();
                        c = 0;
                        list.Clear();

                        prog.WriteProgressBar((i + nDicts) / (float)tPages);
                    }
                }
            }
            fn();
            if (exists) bx.SyncFromStaging();
           
            prog.WriteProgressBar(1);

            void dbUpdateFunction()
            {
                 bx.BulkInsert(list, true);
                //ctx.BulkInsertOrUpdate<DataContext.Lookup>(list);
            }
            void dbInsertFunction()
            {
                 bx.BulkInsert(list);
            }
        }


        public static void LoadEntities(int nRows)
        {
            using MetaTableUtilities<DataContext.Entity> bx = new();
            bx.CreateStagingTable();
            List<DataContext.Entity> list = new() { Capacity = nRows };
            using Proton.Patsts patsts = new();
            using Proton.Vrx vrx = new();
            using Proton2Context ctx = new();
            bool exists = ctx.Entities.Any();
            Action fn =
                exists ? dbUpdateFunction : dbInsertFunction;

            int c = 0;
            int maxId = exists ? ctx.Entities.Max(e => e.Id) : 0;
            var prog = new Utilities.Progress(20);
            if (exists)
            {
                Console.WriteLine("Updating Entities..");
            }
            else Console.WriteLine("Loading Entities..");
            prog.WriteProgressBar(0);
            var tPages = vrx.NPages;
            for (int i = 1; i <= tPages; i++)
            {
                if (patsts.MoveToPage(i) && vrx.MoveToPage(i))
                {
                    list.Add(new()
                    {
                        Id = i,
                        LastUpdated = patsts.Updated

                    });
                    if (exists && i > maxId)
                    {
                        fn = dbUpdateFunction;
                        exists = false;
                    }
                    c++;
                    if (c > nRows)
                    {
                        fn();
                        c = 0;
                        list.Clear();

                        prog.WriteProgressBar(i / (float)tPages);
                    }
                }
            }
            fn();
            if(exists) bx.SyncFromStaging();

            prog.WriteProgressBar(1);

            void dbUpdateFunction()
            {
                //ctx.Entities.UpdateRange(list);
                // ctx.SaveChanges();
                bx.BulkInsert(list, true);
            }
            void dbInsertFunction()
            {
                //ctx.Entities.AddRange(list);
                //ctx.SaveChanges();
                bx.BulkInsert(list);
            }
     
        }


        public static void LoadIndexes(int nRows)
        {
            using DataTable indexDt = MetaTableUtilities<DataContext.Index>.GetTable();
          

            using Proton.Index index = new();
            using Proton.KeyDef keydef = new();
            using Proton.IndexDef indexdef = new();
            using Proton2Context ctx = new();
            using SqlBulkCopy bkc = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString())
            {
                DestinationTableName="Indexes",
                BatchSize=nRows,
                BulkCopyTimeout=1000,
            };

            var prog = new Utilities.Progress(20);
            if (ctx.Indexes.Any())
            {
                Console.WriteLine("Deleting old indexes..");
                ctx.Database.ExecuteSqlRaw("DELETE Indexes");
            }
            int c = 0;
            int tc = 0;
            int cc = 0;

            Console.WriteLine("Counting new indexes..");
            for (int i = 1; i <= index.NPages; i++)
            {
                if (index.MoveToPage(i))
                {
                    tc += index.BlocksOnPage;
                }
            }
            Console.WriteLine("Loading indexes..");
            prog.WriteProgressBar(0);
            for (short i = 1; i <= indexdef.NPages; i++)
            {
                if (indexdef.MoveToPage(i) && index.MoveToPage(indexdef.IndexIdStart))
                {
                    var keyLength = indexdef.KeyLength;

                    index.SetBlockLength(keyLength);

                    int oldEntityId = 0;
                    byte[] bytes = new byte[keyLength];
                    Memory<byte> oldKeyText = new(bytes);
                    while (index.MoveToNextBlock())
                    {
                        var keyText = index.KeyTextRaw;
                        var entityId = index.EntityId;
                        if(
                           // !keyText.IsNullOrEmpty() && 
                            !keyText.Span.SequenceEqual(oldKeyText.Span) && 
                            !(entityId == oldEntityId)
                            )
                        {
                            keyText.CopyTo(oldKeyText);
                            oldEntityId = entityId;
                            indexDt.Rows.Add( i, ProtonDbFileReader.GetString(keyText), entityId);
                            c++;
                            cc++;
                            if (c > nRows)
                            {
                                bkc.WriteToServer(indexDt);
                                c = 0;
                                indexDt.Rows.Clear();
                                prog.WriteProgressBar((float)cc / (float)tc);
                            }
                        }
                    }
                }
            }
            bkc.WriteToServer(indexDt);
            prog.WriteProgressBar(1);
            Console.WriteLine();

        }

        public static void UpdateEntityNames()
        {
            string sql = @"
UPDATE Entities
SET Name=i.Term, EntityTypeId=a.EntityTypeId
FROM Attributes a
INNER JOIN EntityTypes et on et.IdAttributeId=a.Id
INNER JOIN Indexes i ON i.IndexTypeId=et.DefaultIndexTypeId 
INNER JOIN Entities e ON e.Id=i.EntityId";
            Console.WriteLine("Updating entities..");
            using Proton2Context ctx = new();
            ctx.Database.ExecuteSqlRaw(sql);
        }
    }
}
