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
using ProtonConsole2.protonToSql;
using Serilog;

namespace ProtonConsole2.ProtonToSql
{
    class IndexComparer : IEqualityComparer<DataContext.Index>
    {
        public bool Equals(DataContext.Index x, DataContext.Index y)
        {

            if (Object.ReferenceEquals(x, y)) return true;

            if (x == null || y == null)
                return false;

            return x.IndexTypeId == y.IndexTypeId && x.Term == y.Term && x.EntityId==y.EntityId;
        }

        // If Equals() returns true for a pair of objects
        // then GetHashCode() must return the same value for these objects.

        public int GetHashCode(DataContext.Index index)
        {
            //Check whether the object is null
            if (index is null) return 0;

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
            Log.Information("Loading /updating metadata");

            if (ctx.DataTypes.Any())
            {
                using MetaTableUtilities<DataType> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetDataTypes());
            } else
            {
                using MetaTableUtilities<DataType> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetDataTypes());
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.Tables.Any())
            {
                using MetaTableUtilities<Table> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetTables());
            }
            else
            {
                using MetaTableUtilities<Table> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetTables());
            }
            progress.WriteProgressBar(c / nTables); c++;
       


            if (ctx.Views.Any())
            {
                using MetaTableUtilities<View> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetViews());
            }
            else
            {
                using MetaTableUtilities<View> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetViews());
            }
            progress.WriteProgressBar(c / nTables); c++;
           

            if (ctx.EntityTypes.Any())
            {
                using MetaTableUtilities<EntityType> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetEntityTypes());
            }
            else
            {
                using MetaTableUtilities<EntityType> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetEntityTypes());
            }
            progress.WriteProgressBar(c/ nTables); c++;
            ctx.SaveChanges();

            if (ctx.IndexTypes.Any())
            {
                using MetaTableUtilities<IndexType> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetIndexTypes());
            }
            else
            {
                using MetaTableUtilities<IndexType> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetIndexTypes());
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

           
            if (ctx.Attributes.Any())
            {
                using MetaTableUtilities<DataContext.Attribute> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetAttributes());
            }
            else
            {
                using MetaTableUtilities<DataContext.Attribute> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetAttributes());
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            

            if (ctx.ViewAttributes.Any())
            {
                using MetaTableUtilities<DataContext.ViewAttribute> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetViewAttributes());
            }
            else
            {
                using MetaTableUtilities<DataContext.ViewAttribute> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetViewAttributes());
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.ViewCaptions.Any())
            {
                using MetaTableUtilities<DataContext.ViewCaption> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetViewCaptions());
            }
            else
            {
                using MetaTableUtilities<DataContext.ViewCaption> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetViewCaptions());
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();


            if (ctx.LookupTypes.Any())
            {
                using MetaTableUtilities<DataContext.LookupType> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetLookupTypes());
            }
            else
            {
                using MetaTableUtilities<DataContext.LookupType> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetLookupTypes());
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.Menus.Any())
            {
                using MetaTableUtilities<DataContext.Menu> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetMenus());
            }
            else
            {
                using MetaTableUtilities<DataContext.Menu> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetMenus());
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.MenuItems.Any())
            {
                using MetaTableUtilities<DataContext.ProtonUi.MenuItem> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetMenuItems());
            }
            else
            {
                using MetaTableUtilities<DataContext.ProtonUi.MenuItem> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetMenuItems());
            }
            progress.WriteProgressBar(c / nTables); c++;
            ctx.SaveChanges();

            if (ctx.UserStarters.Any())
            {
                using MetaTableUtilities<DataContext.UserStarter> mtu = new();
                mtu.BulkSync(MetaDataFunctions.GetUserStarters());
            }
            else
            {
                using MetaTableUtilities<DataContext.UserStarter> mtu = new();
                mtu.BulkInsert(MetaDataFunctions.GetUserStarters());
            }
            progress.WriteProgressBar(1);
            ctx.SaveChanges();

            ctx.Database.ExecuteSqlRaw(@"
UPDATE Menus
SET EntityTypeId = s.EntityTypeId
FROM MENUS m
inner join UserStarters s on s.MenuId=m.Id

UPDATE Menus
SET EntityTypeId = i.Parameter1
FROM Menus m
INNER JOIN MenuItems i on i.NextMenuId = m.Id and i.[function] = 'CHGE'

UPDATE Menus
SET EntityTypeId=t.EntityTypeId
FROM Menus m
INNER JOIN MenuItems i on i.MenuId=m.id and i.[function] = 'SCRN'
INNER JOIN Views v on v.Id=i.Parameter1
INNER JOIN Tables t on t.Id=v.TableId

declare @rc int = 1000
WHILE @rc <> 0
BEGIN
	UPDATE m
	SET EntityTypeId = m2.EntityTypeId
	FROM Menus m
	INNER JOIN MenuItems i on i.NextMenuId = m.Id and i.[function] <> 'CHGE'
	INNER JOIN Menus m2 on m2.id = i.MenuId
	where m.EntityTypeId = 0 and m2.EntityTypeId > 0

	set @rc = @@ROWCOUNT
END");
        }
    }
}
