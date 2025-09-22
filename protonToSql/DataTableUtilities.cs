using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Identity.Client;
using ProtonConsole2.DataContext;
using Serilog;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.protonToSql
{
    interface IValueTableUtilities : IDisposable
    {
        void BulkSync();
        void BulkInsert(bool forSync = false);
        DataRowCollection DataRows { get;  }
    }

    internal abstract class TableUtilities<T> : IDisposable, IValueTableUtilities
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
        protected static DataTable emptyDataTable = new();
        public SqlConnection cnn;
        public SqlBulkCopy sqlBulkCopy;
        protected SqlCommand cdCreateStagingTable;
        protected SqlCommand cdSyncFromStaging;
        protected DataTable DataTable = new();
        protected bool NeedsStagingTable = true;


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
            propertyInfos = [.. t.GetProperties().Where(p => !p.PropertyType.IsClass || p.PropertyType == typeof(string)).OrderBy(o => o.DeclaringType == t)];

            System.Attribute[] attrs = System.Attribute.GetCustomAttributes(t);

            foreach (System.Attribute attr in attrs)
            {
                if (attr is PrimaryKeyAttribute a)
                {
                    keyColumnNames = [.. a.PropertyNames];
                }
            }

            if (keyColumnNames.Count == 0)
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

            
            for (int i = 0; i < propertyInfos.Length; i++)
            {
                var pi = propertyInfos[i];
                emptyDataTable.Columns.Add(new DataColumn()
                {
                    ColumnName = pi.Name,
                    DataType = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType,
                });
            }

            sqlCreateStagingTable = Sql.sqlCreateStagingTable(tableName);
            sqlCreatePk = Sql.sqlCreateStagingPrimaryKey(tableName, [.. keyColumnNames]);
            sqlUpsert = Sql.sqlUpsert(tableName, [.. keyColumnNames], [.. nonKeyColumnNames]);
            sqlDeleteStagingTable = Sql.sqlDeleteStagingTable(tableName);
        }

        public TableUtilities()
        {
            cnn = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString());
            sqlBulkCopy = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString());
            if (cnn.State != ConnectionState.Open) cnn.Open();

            DataTable = emptyDataTable.Clone();
            NeedsStagingTable = true;
            cdCreateStagingTable = new(Sql.sqlCreateStagingTable(tableName), cnn);
            cdSyncFromStaging = new(sqlSyncFromStaging, cnn);
        }

                       

        private void CreateDataTable(List<T> values)
        {
            DataTable=emptyDataTable.Clone();
            foreach (T val in values)
            {
                var vals = new object[propertyInfos.Length];
                for (int i = 0; i < propertyInfos.Length; i++)
                {
                    vals[i] = propertyInfos[i].GetValue(val)!;
                }
                DataTable.Rows.Add(vals);
            }
        }

        public void BulkSync(List<T> values)
        {
            CreateDataTable(values);
            BulkInsert(true);
            BulkSync();
        }

        public void BulkInsert(List<T> values)
        {
            CreateDataTable(values);
            BulkInsert(false);
        }

        public DataRowCollection DataRows => DataTable.Rows;
  
        public void BulkInsert(bool toStaging = false)
        {
            if (toStaging && NeedsStagingTable) CreateStagingTable();

            try
            {
                sqlBulkCopy.DestinationTableName = toStaging ? stagingTableName : tableName;
                sqlBulkCopy.WriteToServer(DataTable);
                DataTable = emptyDataTable.Clone();
                NeedsStagingTable = toStaging;
            }
            catch (Exception ex)
            {
                Log.Error(ex, $"Unable to write {sqlBulkCopy.DestinationTableName} to server.");
            }
        }

        private void CreateStagingTable()
        {
            try
            {
                if (cnn.State != ConnectionState.Open)  cnn.Open();

                cdCreateStagingTable.ExecuteNonQuery();
                NeedsStagingTable = false;
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, @$"Unable to create {stagingTableName}");
            }
        }

        public void BulkSync()
        {


            if (cnn.State != ConnectionState.Open)  cnn.Open();

            BulkInsert(true);

            try
            {
                cdSyncFromStaging.ExecuteNonQuery();
                NeedsStagingTable = true;
                DataTable = emptyDataTable.Clone();
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
            string sqlDeleteFromStaging = Sql.sqlValuesDeleteFromStaging(tableName, [.. keyColumnNames]); ;

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
            string sqlDeleteFromStaging = Sql.sqlMetadataDeleteFromStaging(tableName, [.. keyColumnNames]);

            StringBuilder sb = new();
            sb.AppendLine(sqlCreatePk);
            sb.AppendLine(sqlDeleteFromStaging);
            sb.AppendLine(sqlUpsert);
            sb.AppendLine(sqlDeleteStagingTable);
            sqlSyncFromStaging = sb.ToString();

        }
    }
}
