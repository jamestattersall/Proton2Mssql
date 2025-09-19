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
    interface ITableUtilities : IDisposable
    {
        DataTable GetTable();
        void BulkInsert(DataTable dt, bool toStaging = false);
        void BulkSync(DataTable dt);
        void SyncFromStaging();
    }

    internal abstract class TableUtilities<T> : IDisposable, ITableUtilities
    {
        protected static readonly string tableName;
        protected static readonly string sqlCreateStagingTable;
        protected static readonly string sqlDeleteStagingTable;
        protected static readonly string sqlCreatePk;
        protected static readonly string sqlUpsert;
        protected static string sqlSyncFromStaging;
        protected static PropertyInfo[] propertyInfos;
        protected static readonly Proton2Context ctx = new();
        protected static List<string> nonKeyColumnNames = [];
        protected static List<string> keyColumnNames = [];
        protected static string stagingTableName;
        public SqlConnection cnn;
        public SqlBulkCopy sqlBulkCopy;
        protected SqlCommand cdCreateStagingTable;
        protected SqlCommand cdSyncFromStaging;

        static TableUtilities()
        {
            var t = typeof(T);
            var entityType = ctx.Model.FindEntityType(t);
            string? tn = null;
            if (entityType != null)
            {
                tn = Microsoft.EntityFrameworkCore.RelationalEntityTypeExtensions.GetTableName(entityType);
            }
            if (tn == null) throw new Exception($"Type {t.FullName} not in datasets");
            tableName = tn;
            stagingTableName=Sql.StagingTableName(tableName);

            //exclude ref properties apart from strings properties, order by derived properties first.
            propertyInfos = t.GetProperties().Where(p => !p.PropertyType.IsClass || p.PropertyType == typeof(string)).OrderBy(o => o.DeclaringType == t).ToArray();

            System.Attribute[] attrs = System.Attribute.GetCustomAttributes(t);

            foreach (System.Attribute attr in attrs)
            {
                if (attr is PrimaryKeyAttribute a)
                {
                    keyColumnNames = a.PropertyNames.ToList();
                }
            }

            if (keyColumnNames.Count() == 0)
                foreach (PropertyInfo p in propertyInfos)
                {
                    foreach (System.Attribute attr in p.GetCustomAttributes())
                    {
                        if (attr is KeyAttribute a)
                        {
                            keyColumnNames.Add($"[{p.Name}]");
                        }
                    }
                }

            foreach (PropertyInfo p in propertyInfos)
            {
                var sname = $"[{p.Name}]";
                if (!keyColumnNames.Contains(sname))
                {
                    nonKeyColumnNames.Add(sname);
                }
            }

            sqlCreateStagingTable = Sql.sqlCreateStagingTable(tableName);
            sqlCreatePk = Sql.sqlCreateStagingPrimaryKey(tableName, keyColumnNames.ToArray());
            sqlUpsert = Sql.sqlUpsert(tableName, keyColumnNames.ToArray(), nonKeyColumnNames.ToArray());
            sqlDeleteStagingTable = Sql.sqlDeleteStagingTable(tableName);
        }

        public TableUtilities()
        {
            cnn = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString());
            sqlBulkCopy = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString());
            if (cnn.State != ConnectionState.Open) cnn.Open();

            cdCreateStagingTable = new(Sql.sqlCreateStagingTable(tableName), cnn);
            cdSyncFromStaging = new(sqlSyncFromStaging, cnn);
        }

        public DataTable GetTable()
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
                          
        public DataTable GetTable(List<T> values)
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
            string sqlDeleteFromStaging = Sql.sqlValuesDeleteFromStaging(tableName, keyColumnNames.ToArray()); ;

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
            string sqlDeleteFromStaging = Sql.sqlMetadataDeleteFromStaging(tableName, keyColumnNames.ToArray());

            StringBuilder sb = new();
            sb.AppendLine(sqlCreatePk);
            sb.AppendLine(sqlDeleteFromStaging);
            sb.AppendLine(sqlUpsert);
            sb.AppendLine(sqlDeleteStagingTable);
            sqlSyncFromStaging = sb.ToString();

        }
    }
}
