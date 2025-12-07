using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.protonToSql
{
    internal static class Sql
    {
        const string stagingPrefix = "staging_";

        private static string equalSet(string[] keyColumnNames)
        {
            List<string> res = [];
            foreach (string val in keyColumnNames)
            {
                res.Add($"s.{val} = t.{val}");
            }
            return string.Join(" AND ", res);
        }

        private static string updateSet(string[] nonKeyColumnNames)
        {
            List<string> res = [];
            foreach (string val in nonKeyColumnNames)
            {
                res.Add($"{val} = s.{val}");
            }
            return string.Join(", ", res);
        }

        private static string nonEqualSet(string[] nonKeyColumnNames)
        {

            List<string> res = [];
            foreach (string val in nonKeyColumnNames)
            {
                res.Add($"(ISNULL(t.{val},'') <> s.{val})");
            }
            return string.Join(" OR ", res);
        }

        public static string StagingTableName(string tableName) => $@"{stagingPrefix}{tableName}";

        public static string sqlValuesDeleteFromStaging(string tableName, string[] keyColumnNames)
        {

            var stagingTableName = StagingTableName(tableName);
            var joinStr = equalSet(keyColumnNames);

            return $@"
DELETE {tableName}
FROM {tableName} t
LEFT JOIN {stagingTableName} s ON {joinStr}
WHERE s.EntityId is null
AND t.EntityId IN (SELECT EntityId FROM {stagingTableName} GROUP BY EntityId)";
        }

        public static string sqlMetadataDeleteFromStaging(string tableName, string[] keyColumnNames)
        {
            var stagingTableName = StagingTableName(tableName);

            var joinStr = equalSet(keyColumnNames);

            return $@"
DELETE {tableName}
FROM {tableName} t
LEFT JOIN {stagingTableName} s 
ON {joinStr}
WHERE s.{keyColumnNames[0]} is null";
        }


        public static string sqlCreateStagingTable(string tableName)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
IF OBJECT_ID('{stagingTableName}') IS NOT NULL  
TRUNCATE TABLE [{stagingTableName}]
ELSE
SELECT TOP 0 * 
INTO [{stagingTableName}]
FROM [{tableName}]";
        }

        public static string sqlCreateStagingPrimaryKey(string tableName, string[] keyColumnNames)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
IF OBJECT_ID(N'PK_staging_{tableName}')IS NULL
ALTER TABLE [{stagingTableName}]
   ADD CONSTRAINT PK_staging_{tableName} PRIMARY KEY CLUSTERED ({string.Join(", ", keyColumnNames)})";

        }

        public static string sqlUpsert(string tableName, string[] keyColumnNames, string[] nonKeyColumnNames)
        {
            var stagingTableName = StagingTableName(tableName);
            var joinStr = equalSet(keyColumnNames);
            var strNonEqual = nonEqualSet(nonKeyColumnNames);
            var updateStr = updateSet(nonKeyColumnNames);


            return $@"
UPDATE [{tableName}]
SET {updateStr}
FROM [{tableName}] t
INNER JOIN [{stagingTableName}] s ON {joinStr}
WHERE ({strNonEqual})

INSERT INTO [{tableName}]
SELECT s.* 
FROM [{stagingTableName}] s
LEFT JOIN [{tableName}] t ON {joinStr}
WHERE t.{keyColumnNames[0]} is null";

        }

        public static string sqlDeleteStagingTable(string tableName)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
IF OBJECT_ID('{stagingTableName}') IS NOT NULL  
DROP TABLE [{stagingTableName}]";
        }

        public static string sqlTruncateStagingTable(string tableName)
        {
            var stagingTableName = StagingTableName(tableName);

            return $@"
TRUNCATE TABLE [{stagingTableName}]
";
        }

        public static string sqlTruncateTable(string tablename)
        {
            return $@"
TRUNCATE TABLE [{tablename}]
";
        }

        public static string NoCheckConstraint()
        {
            return $"EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'";
        }

        public static string sqlMaxEntityId(string tablename)
        {
            return $"SELECT MAX(EntityId) FROM {tablename}";
        }

     
    }
}
