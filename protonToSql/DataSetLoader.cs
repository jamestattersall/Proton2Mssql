using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.IdentityModel.Tokens;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using ProtonConsole2.ProtonToSql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtonConsole2.protonToSql
{

    internal class DataSetLoader : IDisposable
    {
        public DataSet ValuesDs { get; init; }
        static readonly Dictionary<int, AttributeFn?> AttributeFns = [];

        private  Vrx vrx = new();
        private Patsts patsts = new();
        private  Data data = new();
        private  FrText frText = new();

        public struct ValueIndex
        {
            public int EntityId { get; set; }
            public short AttributeId { get; set; }
            public short Seq { get; set; }
        }

        public int NEntities { get; private set; }
        public int NDataPages { get; private set; }
        public int DataPageCount => data.PageCounter;


        public DataSetLoader()
        {
            ValuesDs = new DataSet();
            foreach(var tbl in Enum.GetValues(typeof(ValueTable)))
            {
                var dt = new DataTable(tbl.ToString());
                dt.Columns.Add(new DataColumn() { ColumnName = "EntityId", DataType = typeof(int) });
                dt.Columns.Add(new DataColumn() { ColumnName = "AttributeId", DataType = typeof(short) });
                dt.Columns.Add(new DataColumn() { ColumnName = "Seq", DataType = typeof(int) });

                ValuesDs.Tables.Add(dt);
            }


            ValuesDs.Tables[(int)ValueTable.ValueTexts].Columns.Add(new DataColumn() { ColumnName = "Value", DataType = typeof(string) });
            ValuesDs.Tables[(int)ValueTable.ValueLongTexts].Columns.Add(new DataColumn() { ColumnName = "Value", DataType = typeof(string) });
            ValuesDs.Tables[(int)ValueTable.ValueNumbers].Columns.Add(new DataColumn() { ColumnName = "Value", DataType = typeof(float) });
            ValuesDs.Tables[(int)ValueTable.ValueDates].Columns.Add(new DataColumn() { ColumnName = "Value", DataType = typeof(DateOnly) });
            ValuesDs.Tables[(int)ValueTable.ValueTimes].Columns.Add(new DataColumn() { ColumnName = "Value", DataType = typeof(TimeOnly) });
            ValuesDs.Tables[(int)ValueTable.ValueLookups].Columns.Add(new DataColumn() { ColumnName = "LookupId", DataType = typeof(int) });
            ValuesDs.Tables[(int)ValueTable.ValueEntities].Columns.Add(new DataColumn() { ColumnName = "LinkedEntityId", DataType = typeof(int) });

            NEntities = vrx.NPages;
            NDataPages = data.NPages;
            AttributeFns.Clear();

            using Proton.Item item = new();
            using Proton.Valid valid = new();
            for (int i = 1; i <= item.NPages; i++)
            {
                if (item.MoveToPage(i) && item.IsInstalled && !item.IsCalculated && item.DataType > 0)
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

        enum ValueTable
        {
            ValueTexts = 0,
            ValueLongTexts = 1,
            ValueNumbers = 2,
            ValueDates = 3,
            ValueTimes = 4,
            ValueEntities = 5,
            ValueLookups = 6
        }


        public  int NumbPages(int entityId)
        {
            if (vrx.MoveToPage(entityId))
            {
                int c = 0;
                while (vrx.MoveToNextBlock())
                {
                    c += vrx.DataPageCount;
                }
                return c;
            }
            return 0;
        }

        public void ClearRows()
        {
            foreach (DataTable dt in ValuesDs.Tables) 
            {
                dt.Rows.Clear();
            }

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
            if (vrx.MoveToPage(entityId) && 
                patsts.MoveToPage(entityId) && 
                (ifUpdatedSince == null || ifUpdatedSince<patsts.Updated))
            {
                
                ValueIndex valueIndex = new() { EntityId = entityId };

                var start = vrx.FirstDataPageId;
                if (data.MoveToPage(start))
                {
                    valueIndex.AttributeId = 0;
                    AttributeFn? fn = null;
                    bool blockIsValid = true;
                    while (blockIsValid)
                    {
                        var newAttributeId = data.ItemId;
                        valueIndex.Seq = data.Seq;
                        if (valueIndex.AttributeId != newAttributeId)
                        {
                            valueIndex.AttributeId = newAttributeId;
                            fn = AttributeFns.GetValueOrDefault(valueIndex.AttributeId);
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

        private void ProcessString( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ValuesDs.Tables[(int)ValueTable.ValueTexts].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetString(data));
        }

        private  void ProcessUint8( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ValuesDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, data.Span[0]);
        }

        private void ProcessInt8( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ValuesDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetUInt8(data.Slice(0,1)));
        }

        private void ProcessUint16( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ValuesDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetUInt16(GetMemory(data, 2)));
        }

        private void ProcessInt16( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ValuesDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetInt16(GetMemory(data, 2)));
        }

        private void ProcessUint32( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ValuesDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetUInt32(GetMemory(data, 4)));
        }

        private void ProcessInt32( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ValuesDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetInt32(GetMemory(data, 4)));
        }

        private void ProcessSingle( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            ValuesDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetSingle(GetMemory(data, 4)));
        }

        private void ProcessDouble( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            ValuesDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.GetDouble(GetMemory(data, 8)));
        }

        private void ProcessCompositeSingle(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            var com = GetCompositeSingle(data);
            if (com.Number != null)
            {
                ValuesDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, (float)Math.Round((double)com.Number, subTypeId));
            }
            if (com.QualifierCodeId > 0)
            {
                ValuesDs.Tables[(int)ValueTable.ValueLookups].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.QualifierCodeId);
            }
        }

        private void ProcessCompositeDouble( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            var com = GetCompositeDouble(data);

            if (com.Number != null)
            {
                ValuesDs.Tables[(int)ValueTable.ValueNumbers].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, (float)Math.Round((double)com.Number, subTypeId));
            }
            if (com.QualifierCodeId > 0)
            {
                ValuesDs.Tables[(int)ValueTable.ValueLookups].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.QualifierCodeId);
            }
        }

        private void ProcessDict( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var id = ProtonDbFileReader.GetUInt16(GetMemory(data, 2));
            if (id > 0)
            {
                ValuesDs.Tables[(int)ValueTable.ValueLookups].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, -id);
            }
        }

        private void ProcessCode(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var id = ProtonDbFileReader.GetInt32(GetMemory(data, 4));
            if (id > 0)
            {
                ValuesDs.Tables[(int)ValueTable.ValueLookups].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, id);
            }
        }

        private void ProcessDate( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var dt = ProtonDbFileReader.GetUInt16(GetMemory(data, 2));
            if (dt > 0)
            {
                ValuesDs.Tables[(int)ValueTable.ValueDates].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, ProtonDbFileReader.ProtonBaseDate.AddDays(dt));
            }
        }

        private void ProcessCompositeTime(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var com = GetCompositeTime(data);
            if (com.ReplacementText == "")
            {
                ValuesDs.Tables[(int)ValueTable.ValueTimes].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.Time);
            }
            else
            {
                ValuesDs.Tables[(int)ValueTable.ValueTexts].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, com.ReplacementText);
            }
        }

        private  void ProcessFreeText(ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var id = ProtonDbFileReader.GetInt32(GetMemory(data, 4));
            if (id > 0 && frText.MoveToPage(id))
            {
                if (frText.EntityId == valueIndex.EntityId)
                {
                    ValuesDs.Tables[(int)ValueTable.ValueLongTexts].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, frText.Text(id));
                }
            }
        }

        private void ProcessEntityPtr( ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            var key = ProtonDbFileReader.GetString(data);
            using IndexReader ih = new();
            if (!key.IsNullOrEmpty())
            {
                ValuesDs.Tables[(int)ValueTable.ValueTexts].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, key);

                var eId = ih.GetEntityId(subTypeId, key);

                if (eId > 0)
                {
                    ValuesDs.Tables[(int)ValueTable.ValueEntities].Rows.Add(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq, eId);
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

        public void Dispose()
        {
            vrx.Dispose();
            frText.Dispose();
            data.Dispose();
            patsts.Dispose();
            ValuesDs.Clear();
            ValuesDs.Dispose();
        }
    }
}

