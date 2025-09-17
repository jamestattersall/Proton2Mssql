using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Identity.Client;
using ProtonConsole2.DataContext;
using Serilog;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.protonToSql
{
    internal abstract class TableUtilities<T> : IDisposable
    {
        protected static readonly string tableName;
        protected static readonly string stagingTableName;
        protected static readonly string sqlCreateStagingTable;
        protected static readonly string sqlDeleteStagingTable;
        protected static readonly string sqlCreatePk;
        protected static readonly string sqlUpsert;
        protected static string sqlSyncFromStaging;
        protected static PropertyInfo[] propertyInfos;
        protected static readonly Proton2Context ctx = new();
        protected static List<string> joinSet = [];
        protected static List<string> valSet = [];
        protected static List<string> keyColumns = null;

        public SqlConnection cnn;
        public SqlBulkCopy sqlBulkCopy;
        protected SqlCommand cdCreateStagingTable;
        protected SqlCommand cdSyncFromStaging;

        static TableUtilities()
        {
            List<string> nonKeyColumns = [];
            var t = typeof(T);
            var entityType = ctx.Model.FindEntityType(t);
            string? tn = null;
            if (entityType != null)
            {
                tn = Microsoft.EntityFrameworkCore.RelationalEntityTypeExtensions.GetTableName(entityType);
            }
            if (tn == null) throw new Exception($"Type {t.FullName} not in datasets");
            tableName = tn;
            stagingTableName = $"staging_{tableName}";

            propertyInfos = t.GetProperties().Where(p => !p.PropertyType.IsClass || p.PropertyType == typeof(string)).ToArray();
            System.Attribute[] attrs = System.Attribute.GetCustomAttributes(t);  

            foreach (System.Attribute attr in attrs)
            {
                if (attr is PrimaryKeyAttribute a)
                {
                    keyColumns = (List<string>)a.PropertyNames;
                }
            }

            if (keyColumns == null)
            {
                keyColumns = new List<string>();
                foreach (PropertyInfo p in propertyInfos)
                {
                    foreach (System.Attribute attr in p.GetCustomAttributes())
                    {
                        if (attr is KeyAttribute a)
                        {
                            keyColumns.Add($"[{p.Name}]");
                        }
                    }
                }
            }

            foreach (PropertyInfo p in propertyInfos)
            {
                var sname = $"[{p.Name}]";
                if (!keyColumns.Contains(sname))
                {
                    nonKeyColumns.Add(sname);
                }
            }

            foreach (string val in nonKeyColumns)
            {
                valSet.Add($"{val} = s.{val}");
            }


            foreach (string val in keyColumns)
            {
                joinSet.Add($"s.{val} = t.{val}");
            }

            List<string> whereNotSet = [];
            foreach (string val in nonKeyColumns)
            {
                whereNotSet.Add($"(t.{val} <> s.{val})");
                whereNotSet.Add($"(t.{val} IS NULL AND s.{val} IS NOT NULL)");
                whereNotSet.Add($"(t.{val} IS NOT NULL AND s.{val} IS NULL)");
            }

            sqlCreateStagingTable = $@"
IF OBJECT_ID('{stagingTableName}') IS NOT NULL  
DROP TABLE [{stagingTableName}]

SELECT TOP 0 * 
INTO [{stagingTableName}]
FROM [{tableName}]";

            sqlCreatePk = $@"
ALTER TABLE [{stagingTableName}]
   ADD CONSTRAINT PK_staging_{tableName} PRIMARY KEY CLUSTERED ({string.Join(',', keyColumns.ToArray())})";

            sqlUpsert = $@"
UPDATE [{tableName}]
SET {string.Join(@",
", valSet.ToArray())}
FROM [{stagingTableName}] s
INNER JOIN [{tableName}] t ON {string.Join(@"
  AND ", joinSet.ToArray())}
WHERE ({string.Join(@"
  OR ", whereNotSet.ToArray())})

INSERT INTO [{tableName}]
SELECT s.* 
FROM [{stagingTableName}] s
LEFT JOIN [{tableName}] t ON {string.Join(@"
  AND ", joinSet.ToArray())}
WHERE t.{keyColumns[0]} is null";

            sqlDeleteStagingTable = $@"
DROP TABLE [{stagingTableName}]
";

        }

        public TableUtilities()
        {

            cnn = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString());
            sqlBulkCopy = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString());
            if (cnn.State != ConnectionState.Open) cnn.Open();

            cdCreateStagingTable = new(sqlCreateStagingTable, cnn);
            cdSyncFromStaging = new(sqlSyncFromStaging, cnn);

        }

        public static DataTable GetTable()
        {
            DataTable dt = new(tableName);
            for (int i = 0; i < propertyInfos.Length; i++)
            {
                var pi = propertyInfos[i];
                dt.Columns.Add(new DataColumn()
                {
                    ColumnName = pi.Name,
                    DataType = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType,
                    
                });
            }
            return dt;
        }
                          
        public static DataTable GetTable(List<T> values)
        {
            DataTable dt = GetTable();
            foreach (T val in values)
            {
                var vals = new object[propertyInfos.Length];
                for (int i = 0; i < propertyInfos.Length; i++)
                {
                    vals[i] = propertyInfos[i].GetValue(val);
                }
                dt.Rows.Add(vals);
            }
            return dt;
        }

        public void BulkInsert(List<T> values, bool toStaging = false)
        {
            var dt = GetTable(values);
             BulkInsert(dt, toStaging);
        }

        public void BulkInsert(DataTable dt, bool toStaging = false)
        {
            try
            {
                sqlBulkCopy.DestinationTableName = toStaging ? stagingTableName : tableName;
                 sqlBulkCopy.WriteToServer(dt);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unable to write {sqlBulkCopy.DestinationTableName} to server.");
            }
        }

        public void BulkSync(List<T> values)
        {
            var dt = GetTable(values);
             BulkSync(dt);
        }

        public void CreateStagingTable()
        {
            try
            {
                if (cnn.State != ConnectionState.Open)  cnn.Open();

                cdCreateStagingTable.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, @$"Unable to create {stagingTableName}");
            }
        }

        public void BulkSync(DataTable dt)
        {
           

            try
            {

                if (cnn.State != ConnectionState.Open)  cnn.Open();
                 cdCreateStagingTable.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unable to create table {stagingTableName} ");
            }

            try
            {

                sqlBulkCopy.DestinationTableName = stagingTableName;
                 sqlBulkCopy.WriteToServer(dt);
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unable to write {stagingTableName} to server.");
            }

            SyncFromStaging();
        }

        public void SyncFromStaging()
        {

            if (cnn.State != ConnectionState.Open)  cnn.Open();
            try
            {
                 cdSyncFromStaging.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Log.Error(ex, @$"Unable to sync {stagingTableName}
{sqlSyncFromStaging}
");

            }
        }

        public void Dispose()
        {
            cnn.Dispose();
            sqlBulkCopy.Close();
            cdCreateStagingTable.Dispose();
            cdSyncFromStaging.Dispose();

        }
    }


    internal class ValueTableUtilities<T> : TableUtilities<T> where T : class
    {


        static ValueTableUtilities()
        {
            string sqlDeleteFromStaging = $@"
DELETE {tableName}
FROM {tableName} t
LEFT JOIN {stagingTableName} s ON {string.Join(" AND ", joinSet.ToArray())}
WHERE s.EntityId is null
AND t.EntityId IN (SELECT EntityId FROM {stagingTableName} GROUP BY EntityId)";

            StringBuilder sb = new();
            sb.AppendLine(sqlCreatePk);
            sb.AppendLine(sqlDeleteFromStaging);
            sb.AppendLine(sqlUpsert);
            sb.AppendLine(sqlDeleteStagingTable);
            sqlSyncFromStaging = sb.ToString();
        }
    }

    internal class MetaTableUtilities<T> : TableUtilities<T> where T : class
    {

        static MetaTableUtilities()
        {
            
            string sqlDeleteFromStaging = $@"
DELETE {tableName}
FROM {tableName} t
LEFT JOIN {stagingTableName} s ON {string.Join(" AND ", joinSet.ToArray())}
WHERE s.{keyColumns[0]} is null";

            StringBuilder sb = new();
            sb.AppendLine(sqlCreatePk);
            sb.AppendLine(sqlDeleteFromStaging);
            sb.AppendLine(sqlUpsert);
            sb.AppendLine(sqlDeleteStagingTable);
            sqlSyncFromStaging = sb.ToString();

        }
    }

}
