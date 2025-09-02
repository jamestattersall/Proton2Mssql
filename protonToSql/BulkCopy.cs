using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EFCore.BulkExtensions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;

namespace ProtonConsole2.protonToSql
{
    internal class BulkCopy
    {
        private SqlBulkCopy bkc;
        private DataSetLoader dsl;
        private int EntityCounter = 0;

        public BulkCopy(SqlConnection cn)
        {
            bkc = new(cn);
            dsl = new();
        }

        public void DeleteEntity(int entityId, Proton2Context ctx)
        {
            ctx.Database.ExecuteSqlRaw($@"
DELETE ValueTexts WHERE EntityId={0}
DELETE ValueLongTexts WHERE EntityId={0}
DELETE ValueNumbers WHERE EntityId={0}
DELETE ValueDates WHERE EntityId={0}
DELETE ValueTimes WHERE EntityId={0}
DELETE ValueLookups WHERE EntityId={0}
DELETE ValueEntities WHERE EntityId={0}" , entityId);
        }

        public void CopyEntity(int entityId)
        {
            dsl.ClearRows();
            dsl.LoadDataset(entityId);
            UpdateDatabase(0);
        }

        private void UpdateDatabase(int rows)
        {
            foreach(DataTable dt in dsl.ValuesDs.Tables)
            {
                if(dt.Rows.Count > rows)
                {
                    bkc.DestinationTableName = dt.TableName;
                    bkc.WriteToServer(dt);
                    
                    dt.Rows.Clear();
                }
            }
        }

        public void LoadAllData()
        {
            dsl.ClearRows();
            for (int i = 1; i<= dsl.NEntities; i++)
            {
                if (dsl.LoadDataset(i))
                {
                    UpdateDatabase(1000);
                }
                EntityCounter++ ;
            }
            UpdateDatabase(0);
        }

    }
}
