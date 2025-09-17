using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using ProtonConsole2.ProtonToSql;
using Serilog;
using System.Data;
using System.Diagnostics;

namespace ProtonConsole2.protonToSql
{

    internal class ValuesLoader : IDisposable
    {
        public DataSet ValuesDs { get; init; }
        private DataSet EntityDs;
        static readonly Dictionary<int, AttributeFn?> AttributeFns = [];

        private Vrx vrx = new();
        private Patsts patsts = new();
        private Data data = new();
        private FrText frText = new();

        public struct ValueIndex
        {
            public int EntityId { get; set; }
            public short AttributeId { get; set; }
            public short Seq { get; set; }
        }

        public long NEntities { get; private set; }
        public long NDataPages { get; private set; }
        public int DataPageCount => data.PageCounter;


        public ValuesLoader()
        {
            ValuesDs = CreateDataset();
            EntityDs = CreateDataset();
            NEntities = vrx.NPages;
            NDataPages = data.NPages;
            AttributeFns.Clear();

            using Proton.Item item = new();
            using Proton.Valid valid = new();
            for (int i = 1; i <= item.NPages; i++)
            {
                if (item.MoveToPage(i) &&
                    item.IsInstalled &&
                    !item.IsCalculated &&
                    item.DataType > 0 &&
                    !Utilities.ConfigurationManager.AppSettings.ExcludeItems.Contains(i)
                    )
                {
                    var qual = false;
                    var mod = false;

                    if (valid.MoveToPage(i))
                    {
                        qual = valid.QualifierCodeType > 0;
                        mod = valid.ModifierCodeType > 0;
                    }

                    Action<ValueIndex, ReadOnlyMemory<byte>, short>? act = null;
                    switch (item.DataType)
                    {
                        case 1:
                            act = ProcessString;
                            break;
                        case 2:
                            act = (item.SubType == 0) ? ProcessUint8 : ProcessInt8;
                            break;
                        case 3:
                            act = (item.SubType == 0) ? ProcessUint16 : ProcessInt16;
                            break;
                        case 4:
                            act = (item.SubType == 0) ? ProcessUint32 : ProcessInt32;
                            break;
                        case 5:
                            act = (qual || mod) ? ProcessCompositeSingle : ProcessSingle;
                            break;
                        case 6:
                            act = (qual || mod) ? ProcessCompositeDouble : ProcessDouble; ;
                            break;
                        case 7:
                            act = ProcessDict;
                            break;
                        case 8:
                            act = ProcessDate;
                            break;
                        case 9:
                            act = ProcessCompositeTime;
                            break;
                        case 10:
                            act = ProcessFreeText;
                            break;
                        case 11:
                            act = ProcessEntityPtr;
                            break;
                        case 12:
                            act = ProcessCode;
                            break;

                        default:
                            throw new Exception("unknown datatype " + item.DataType.ToString());

                    }
                    AttributeFns.Add(i, new() { Action = act, SubtypeId = item.SubType });
                }
            }

        }
        private DataSet CreateDataset()
        {
            var ds = new DataSet();
            foreach (var tbl in Enum.GetValues(typeof(ValueTable)))
            {
                var dt = new DataTable(tbl.ToString());
                dt.Columns.Add(new DataColumn() { ColumnName = "EntityId", DataType = typeof(int) });
                dt.Columns.Add(new DataColumn() { ColumnName = "AttributeId", DataType = typeof(short) });
                dt.Columns.Add(new DataColumn() { ColumnName = "Seq", DataType = typeof(int) });

                ds.Tables.Add(dt);
            }


            ds.Tables[(int)ValueTable.ValueTexts].Columns.Add(new DataColumn() { ColumnName = "Value", DataType = typeof(string) });
            ds.Tables[(int)ValueTable.ValueLongTexts].Columns.Add(new DataColumn() { ColumnName = "Value", DataType = typeof(string) });
            ds.Tables[(int)ValueTable.ValueNumbers].Columns.Add(new DataColumn() { ColumnName = "Value", DataType = typeof(float) });
            ds.Tables[(int)ValueTable.ValueDates].Columns.Add(new DataColumn() { ColumnName = "Value", DataType = typeof(DateOnly) });
            ds.Tables[(int)ValueTable.ValueTimes].Columns.Add(new DataColumn() { ColumnName = "Value", DataType = typeof(TimeOnly) });
            ds.Tables[(int)ValueTable.ValueLookups].Columns.Add(new DataColumn() { ColumnName = "LookupId", DataType = typeof(int) });
            ds.Tables[(int)ValueTable.ValueEntities].Columns.Add(new DataColumn() { ColumnName = "LinkedEntityId", DataType = typeof(int) });
            return ds;
        }

        enum ValueTable
        {
            ValueTexts = 0,
            ValueLongTexts = 1,
            ValueNumbers = 2,
            ValueDates = 3,
            ValueTimes = 4,
            ValueLookups = 5,
            ValueEntities = 6,
        }


        /// <summary>
        /// Numeric data in Proton DATA.dbs may be trimmed by removing trailing zeros
        /// This function adds the trailing zero bytes back so it can be converted normally
        /// </summary>
        private static ReadOnlyMemory<byte> GetMemory(ReadOnlyMemory<byte> mem, int length)
        {
            if (mem.Length == length) return mem;

            if (mem.Length > length) return mem[..length];

            //add trailing zeros
            var m = new Memory<byte>(new byte[length]);

            mem.CopyTo(m);

            return m;
        }


        public bool LoadDataset(int entityId, DateTime? ifUpdatedSince = null)
        {
            EntityDs.Clear();
            if (vrx.MoveToPage(entityId) &&
                patsts.MoveToPage(entityId) &&
                (ifUpdatedSince == null || ifUpdatedSince < patsts.Updated))
            {
                ValueIndex valueIndex = new() { EntityId = entityId };

                var start = vrx.FirstDataPageId;
                if (data.MoveToPage(start))
                {
                    valueIndex.AttributeId = 0;
                    AttributeFn? fn = null;
                    bool blockIsValid = true;
                    short skippedAttributeId = 0;
                    while (blockIsValid && data.ItemId > 0)
                    {
                        var newAttributeId = data.ItemId;
                        valueIndex.Seq = data.Seq;
                        if (newAttributeId > valueIndex.AttributeId)
                        {
                            valueIndex.AttributeId = newAttributeId;
                            fn = AttributeFns.GetValueOrDefault(valueIndex.AttributeId);
                        }
                        else if (newAttributeId < valueIndex.AttributeId)
                        {
                            fn = null;
                            if (skippedAttributeId == 0)
                            {
                                Log.Warning($"Items not in ascending order. Entity:{entityId}, any values for item {valueIndex.AttributeId} with rows higher than {valueIndex.Seq} will not be imported. ");

                            }
                            if (skippedAttributeId != newAttributeId)
                            {
                                skippedAttributeId = newAttributeId;
                                Log.Warning($"Skipping item {newAttributeId}.");
                            }
                        }

                        if (fn != null && !data.RawValue.IsEmpty)
                        {
                            fn.Action!(valueIndex, data.RawValue, fn.SubtypeId);
                        }
                        blockIsValid = data.MoveToNextBlock();
                    }
                    return true;
                }
            }
            return false;
        }

  


        private class AttributeFn
        {
            public short SubtypeId { get; set; }
            public Action<ValueIndex, ReadOnlyMemory<byte>, short>? Action { get; set; }
        }

        private void ProcessString(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            EntityDs.Tables[(int)ValueTable.ValueTexts].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetString(data));
        }

        private void ProcessUint8(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            EntityDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, data.Span[0]);
        }

        private void ProcessInt8(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            EntityDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetUInt8(data.Slice(0, 1)));
        }

        private void ProcessUint16(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            EntityDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetUInt16(GetMemory(data, 2)));
        }

        private void ProcessInt16(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            EntityDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetInt16(GetMemory(data, 2)));
        }

        private void ProcessUint32(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            EntityDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetUInt32(GetMemory(data, 4)));
        }

        private void ProcessInt32(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            EntityDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetInt32(GetMemory(data, 4)));
        }

        private bool CheckFloat(float res, ValueIndex valueIndex)
        {
            if (!float.IsFinite(res))
            {
                Log.Warning($"Abnormal number {res}, entity:{valueIndex.EntityId}, attribute:{valueIndex.AttributeId}, Seq:{valueIndex.Seq}");
                return false;
            }
            return true;
        }

        private void ProcessSingle(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {

            var res = ProtonDbFileReader.GetSingle(GetMemory(data, 4));
            if (CheckFloat(res, valueIndex))
            {
                EntityDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, res);
            }
        }

        private void ProcessDouble(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            var res = ProtonDbFileReader.GetDouble(GetMemory(data, 8));
            if (CheckFloat(res, valueIndex))
            {
                EntityDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, res);
            }
        }

        private void ProcessCompositeSingle(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            var com = GetCompositeSingle(data);
            if (com.Number != null && CheckFloat((float)com.Number!, valueIndex))
            {
                EntityDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, (float)Math.Round((double)com.Number, subTypeId));
            }
            if (com.QualifierCodeId > 0)
            {
                EntityDs.Tables[(int)ValueTable.ValueLookups].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.QualifierCodeId);
            }
        }

        private void ProcessCompositeDouble(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            var com = GetCompositeDouble(data);

            if (com.Number != null && CheckFloat((float)com.Number!, valueIndex))
            {
                EntityDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, (float)Math.Round((double)com.Number, subTypeId));
            }
            if (com.QualifierCodeId > 0)
            {
                EntityDs.Tables[(int)ValueTable.ValueLookups].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.QualifierCodeId);
            }
        }

        private void ProcessDict(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var id = ProtonDbFileReader.GetUInt16(GetMemory(data, 2));
            if (id > 0)
            {
                EntityDs.Tables[(int)ValueTable.ValueLookups].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, -id);
            }
        }

        private void ProcessCode(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var id = ProtonDbFileReader.GetInt32(GetMemory(data, 4));
            if (id > 0)
            {
                EntityDs.Tables[(int)ValueTable.ValueLookups].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, id);
            }
        }

        private void ProcessDate(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var dt = ProtonDbFileReader.GetUInt16(GetMemory(data, 2));
            if (dt > 0)
            {
                EntityDs.Tables[(int)ValueTable.ValueDates].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.ProtonBaseDate.AddDays(dt));
            }
        }

        private void ProcessCompositeTime(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var com = GetCompositeTime(data);
            if (com.ReplacementText == "")
            {
                EntityDs.Tables[(int)ValueTable.ValueTimes].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.Time);
            }
            else
            {
                EntityDs.Tables[(int)ValueTable.ValueTexts].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.ReplacementText);
            }
        }

        private void ProcessFreeText(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var id = ProtonDbFileReader.GetInt32(GetMemory(data, 4));
            if (id > 0 && frText.MoveToPage(id))
            {
                if (frText.EntityId == valueIndex.EntityId)
                {
                    EntityDs.Tables[(int)ValueTable.ValueLongTexts].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, frText.Text(id));
                }
            }
        }

        private void ProcessEntityPtr(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            var key = ProtonDbFileReader.GetString(data);
            using IndexReader ih = new();
            if (!key.IsNullOrEmpty())
            {
                EntityDs.Tables[(int)ValueTable.ValueTexts].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, key);

                var eId = ih.GetEntityId(subTypeId, key);

                if (eId > 0)
                {
                    EntityDs.Tables[(int)ValueTable.ValueEntities].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, eId);
                }
            }
        }

        private static CompositeFloat GetCompositeSingle(ReadOnlyMemory<byte> data)
        {
            CompositeFloat cf = new();
            if (data.Length > 8)
            {
                cf.QualifierCodeId = ProtonDbFileReader.GetInt32(GetMemory(data.Slice(8), 4));

                if (cf.QualifierCodeId > 0)
                {
                    int qr = ProtonDbFileReader.GetInt16(data.Slice(4, 2));
                    if (qr == 1)
                    {
                        return cf;
                    }
                }
            }
            cf.Number = ProtonDbFileReader.GetSingle(GetMemory(data, 4));
            return cf;
        }

        private static CompositeFloat GetCompositeDouble(ReadOnlyMemory<byte> data)
        {
            CompositeFloat cf = new();
            if (data.Length > 12)
            {
                cf.QualifierCodeId = ProtonDbFileReader.GetInt32(GetMemory(data.Slice(12), 4));
                if (cf.QualifierCodeId > 0)
                {
                    int qr = ProtonDbFileReader.GetInt16(data.Slice(8, 2));
                    if (qr == 1)
                    {
                        return cf;
                    }
                }
            }
            cf.Number = (float)ProtonDbFileReader.GetDouble(GetMemory(data, 8));
            return cf;
        }

        private static CompositeTime GetCompositeTime(ReadOnlyMemory<byte> data)
        {
            var timeOffset = ProtonDbFileReader.GetUInt32(GetMemory(data, 4));

            switch (timeOffset)
            {
                case 0x20000000: return new CompositeTime() { ReplacementText = "PRE" };
                case 0x40000000: return new CompositeTime() { ReplacementText = "POST" };
                case 0x80000000: return new CompositeTime() { Time = TimeOnly.MinValue, ReplacementText = "" };
                default: return new CompositeTime() { Time = TimeOnly.MinValue.AddMinutes(timeOffset / 60000), ReplacementText = "" };
            }
        }


        public void LoadValues(int nRows)
        {
            var st = Stopwatch.StartNew();
            ValuesDs.Clear();

            int counter = 0;

            using Proton2Context ctx = new();
            ctx.Database.ExecuteSql($"EXEC sp_msforeachtable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'");


            Console.WriteLine($"Loading values for {vrx.NPages} entities..");
            var prog = new Utilities.Progress(20);
            prog.WriteProgressBar(0);


            using SqlBulkCopy bkc = new(Utilities.ConfigurationManager.AppSettings.SQLConnectionString(),
                SqlBulkCopyOptions.TableLock & SqlBulkCopyOptions.KeepIdentity)
            {
                BatchSize = nRows
            };

            Func<DataTable, bool> loadFunction = Utilities.ConfigurationManager.AppSettings.NoLoad ? Noload : writeToServer;

            long nEntities = 0;

            if (Utilities.ConfigurationManager.AppSettings.OnlyTheseEntities.Count == 0)
            {
                nEntities = vrx.NPages;
                for (int i = 1; i <= nEntities; i++)
                {
                    Process(i);
                }
            }
            else
            {
                nEntities = Utilities.ConfigurationManager.AppSettings.OnlyTheseEntities.Count;
                foreach (int i in Utilities.ConfigurationManager.AppSettings.OnlyTheseEntities)
                {
                    Process(i);
                }
            }

            foreach (DataTable tbl in ValuesDs.Tables)
            {
                loadFunction(tbl);
            }

            prog.WriteProgressBar(1);
            st.Stop();
            string str = Utilities.ConfigurationManager.AppSettings.NoLoad ? "Scanned " : "Loaded ";
            Console.WriteLine($"{str} in {st.Elapsed:hh\\:mm\\:ss}");

            bool Noload(DataTable tbl)
            {
                tbl.Rows.Clear();
                return true;
            }

            bool writeToServer(DataTable tbl)
            {
                bkc.DestinationTableName = tbl.TableName;
                try
                {
                    bkc.WriteToServer(tbl);
                    return true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Failed to save to table {tbl.TableName}{Environment.NewLine} first row:{string.Join(',',tbl.Rows[0].ItemArray.ToList().ConvertAll(o => o.ToString()).ToArray())}, last row{tbl.Rows[tbl.Rows.Count - 1].ItemArray}");
                    return false;
                }
                finally
                {
                    tbl.Rows.Clear();
                }
            }

            void Process(int i)
            {
                counter++;
                if (vrx.MoveToPage(i))
                {
                    bool success = false;
                    try
                    {
                        success = LoadDataset(i);
                        ValuesDs.Merge(EntityDs);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Unable to load data for entity {i}");
                    }

                    if (success)
                    {
                        foreach (DataTable tbl in ValuesDs.Tables)
                        {
                            if (tbl.Rows.Count > nRows)
                            {
                                loadFunction(tbl);
                            }
                        }
                    }
                    prog.WriteProgressBar(counter / (float)nEntities);
                }
            }
        }


        private class CompositeTime
        {
            public TimeOnly Time { get; set; }
            public string ReplacementText { get; set; } = "";
            public string ToString(string format)
            {
                if (ReplacementText == "")
                {
                    return Time.ToString(format);
                }
                else return ReplacementText;
            }
        }

        private class CompositeFloat
        {
            public float? Number { get; set; }
            public int QualifierCodeId { get; set; }

        }
        public void Dispose()
        {
            vrx.Dispose();
            frText.Dispose();
            data.Dispose();
            patsts.Dispose();
            ValuesDs.Clear();
            ValuesDs.Dispose();
            EntityDs.Clear();
            EntityDs.Dispose();
        }
    }
}

