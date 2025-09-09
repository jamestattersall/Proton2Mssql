using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using ProtonConsole2.DataContext;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using Serilog;

namespace ProtonConsole2.protonToSql
{
    internal class DataTableUtilities<T>
    {
        private  PropertyInfo[] propertyInfos = [];
        private List<string> primaryKeys;
         
        private  string sqlCreateTempTable;
        private  string sqlMergeTempTable;

        public DataTableUtilities() {

            var t = typeof(T);

            propertyInfos = t.GetProperties();
            System.Attribute[] attrs = System.Attribute.GetCustomAttributes(t);  // Reflection.

            // Displaying output.
            foreach (System.Attribute attr in attrs)
            {
                if (attr is PrimaryKeyAttribute a)
                {
                    primaryKeys = (List<string>)a.PropertyNames;
                }
            }

            if(primaryKeys is null)
            {
                foreach (PropertyInfo p in propertyInfos)
                {
                    foreach (System.Attribute attr in p.GetCustomAttributes())
                    {
                        if (attr is KeyAttribute a)
                        {
                            primaryKeys = [a.ToString()];
                        }
                    }
                }
            }

           
            sqlCreateTempTable = @$"
IF EXISTS TABLE #{0} DROP TABLE #{0}
SELECT TOP 0 * FROM {0} INTO #{0}
";
            var pks=
            sqlMergeTempTable = $@"

";

        }



        public DataTable Create()
        {
            DataTable dt = new DataTable();
            for (int i = 0; i < propertyInfos.Length; i++)
            {
                dt.Columns.Add(new DataColumn()
                {
                    ColumnName = propertyInfos[i].Name,
                    DataType = propertyInfos[i].PropertyType
                });
            }
            return dt;
        }

        public  void Populate(List<T> list, DataTable dt)
        {
            foreach (T item in list) {
                var drb=dt.NewRow();

                for(int i = 0; i<propertyInfos.Length; i++)
                {
                    drb.ItemArray[i]= propertyInfos[i].GetValue(item);
                }
                dt.Rows.Add(drb);
            }
        }

        public  DataTable CreateAndPopulate(List<T> list) {
            var dt = Create();
            Populate(list, dt);
            return dt;
        }

        public  int BulkInsert(DataTable dataTable, SqlBulkCopy sqlBulkCopy, bool forUpdate = false)
        {
           
            sqlBulkCopy.DestinationTableName = forUpdate ? $"#{dataTable.TableName}" : dataTable.TableName;
           
            try
            {
                sqlBulkCopy.WriteToServer(dataTable);
                return sqlBulkCopy.RowsCopied;
            }
            catch (Exception ex) {
                Log.Error($"Unable to save table {dataTable.TableName} {Environment.NewLine} {ex.Message} ");
                return 0;
            }
        }

        public  bool CreateTempTable(DataTable dt, Proton2Context ctx)
        {
            try
            {
                ctx.Database.ExecuteSqlRaw(sqlCreateTempTable, dt.TableName);
                return true;
            }
            catch (Exception ex) {
                Log.Error($"Unable to create table #{dt.TableName} {Environment.NewLine} {ex.Message}");
                return false;
     
            }
        }

        public bool MergeTempTable(DataTable dt, Proton2Context ctx)
        {
            try
            {
                ctx.Database.ExecuteSqlRaw(sqlMergeTempTable, dt.TableName);
                return true;
            }
            catch (Exception ex)
            {
                Log.Error($"Unable to update table #{dt.TableName} {Environment.NewLine} {ex.Message}");
                return false;

            }
        }
    }
}
