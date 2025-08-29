using EFCore.BulkExtensions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.IdentityModel.Tokens;
using NetTopologySuite.Utilities;
using ProtonConsole2.DataContext;
using ProtonConsole2.Proton;
using ProtonConsole2.ProtonToSql;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.Serialization;

namespace ProtonConsole2.Proton
{

    internal class DataFunctions
    {
        static readonly Dictionary<int, AttributeFn?> AttributeFns = [];

        private static Vrx vrx = new();
        private static Data data = new();
        static FrText frText = new();

        public struct ValueIndex
        {
            public int EntityId { get; set; }
            public short AttributeId { get; set; }
            public short Seq { get; set; }
        }

        public static int NEntities { get; private set; }
        public static int NDataPages { get; private set; }
        public static int DataPageCount => data.PageCounter;

        static DataFunctions()
        {
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

                    Action<Entity, ValueIndex, ReadOnlyMemory<byte>, short>? act = null;
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
                            act = (qual || mod)? ProcessCompositeSingle: ProcessSingle;
                            break;
                        case 6:
                            act = (qual || mod)? ProcessCompositeDouble : ProcessDouble; ;
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
                    AttributeFns.Add(i, new() {Action = act, SubtypeId=item.SubType });
                }
            }
        }

        public static int NumbPages(int entityId)
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



        public static Entity? GetEntityInstance(int entityId) { 
            Entity? entity = null;

            if (vrx.MoveToPage(entityId))
            {
                ValueIndex valueIndex = new() { EntityId = entityId };
                entity = new()
                {
                    Id = entityId,
                };
                
                var start = vrx.FirstDataPageId;
                if (data.MoveToPage(start))
                {
                    valueIndex.AttributeId = 0;
                    AttributeFn? fn=null ;
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
                            fn.Action!(entity, valueIndex, data.RawValue, fn.SubtypeId);
                        }
                        blockIsValid = data.MoveToNextBlock();
                    }

                }
            }
            return entity;
        }

        private class AttributeFn
        {
            public short SubtypeId { get; set; }
            public Action<Entity, ValueIndex, ReadOnlyMemory<byte>, short>? Action { get; set; }
        }

        private static void ProcessString(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ent.ValueTexts.Add(new ValueText(valueIndex.EntityId,valueIndex.AttributeId, valueIndex.Seq)
            {
                Value= ProtonDbFileReader.GetString(data)
            } );
         
        }

        private static void ProcessUint8(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ent.ValueNumbers.Add(new ValueNumber(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
            {
                Value = data.Span[0]
            });
        }

        private static void ProcessInt8(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ent.ValueNumbers.Add(new(valueIndex.EntityId,valueIndex.AttributeId, valueIndex.Seq)
            {
                Value = (sbyte)data.Span[0]
            });
        }


        private static void ProcessUint16(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ent.ValueNumbers.Add(new(valueIndex.EntityId,valueIndex.AttributeId, valueIndex.Seq)
            {
                Value = ProtonDbFileReader.GetUInt16(GetMemory(data, 2))
            });

        }

        private static void ProcessInt16(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ent.ValueNumbers.Add(new(valueIndex.EntityId,valueIndex.AttributeId, valueIndex.Seq)
            {
                Value = ProtonDbFileReader.GetInt16(GetMemory(data, 2))
            });

        }

        private static void ProcessUint32(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            ent.ValueNumbers.Add(new(valueIndex.EntityId,valueIndex.AttributeId, valueIndex.Seq)
            {
                Value = ProtonDbFileReader.GetUInt32(GetMemory(data, 4))
            });
        }

        private static void ProcessInt32(Entity ent, ValueIndex valueIndex,ReadOnlyMemory<byte> data, short p = 0)
        {
            ent.ValueNumbers.Add(new(valueIndex.EntityId,valueIndex.AttributeId, valueIndex.Seq)
            {
                Value = ProtonDbFileReader.GetInt32(GetMemory(data, 4))
            });
        }

        private static void ProcessSingle(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            ent.ValueNumbers.Add(new(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
            {
                Value = float.Round(ProtonDbFileReader.GetSingle(GetMemory(data, 4)), subTypeId)
            });
        }

        private static void ProcessDouble(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            ent.ValueNumbers.Add(new(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
            {
                Value = float.Round((float)ProtonDbFileReader.GetDouble(GetMemory(data, 8)), subTypeId)
            });
        }

        private static void ProcessCompositeSingle(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            var com = GetCompositeSingle(data);
            if (com.Number != null)
            {
                ent.ValueNumbers.Add(new(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
                {
                    Value = (float)Math.Round((double)com.Number, subTypeId)
                });

            }
            if (com.QualifierCodeId > 0)
            {
                ent.ValueLookups.Add(new(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
                {
                    LookupId = com.QualifierCodeId
                });
            }
        }

        private static void ProcessCompositeDouble(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            var com = GetCompositeDouble(data);
            if (com.Number != null)
            {
                ent.ValueNumbers.Add(new(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
                {
                    Value = (float)Math.Round((double)com.Number, subTypeId)
                });
            }
            if (com.QualifierCodeId > 0)
            {
                ent.ValueLookups.Add(new(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
                {
                    LookupId = com.QualifierCodeId
                });
            }
        }

        private static void ProcessDict(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var id = ProtonDbFileReader.GetUInt16(GetMemory(data,2));
            if (id > 0)
            {
                ent.ValueLookups.Add(new(valueIndex.EntityId,valueIndex.AttributeId, valueIndex.Seq)
                {
                    LookupId = -id 
                });
            }
        }

        private static void ProcessCode(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var id = ProtonDbFileReader.GetInt32(GetMemory(data, 4));
            if (id > 0)
            {
                ent.ValueLookups.Add(new(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
                {
                    LookupId = id
                });
            }
        }

        private static void ProcessDate(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var dt = ProtonDbFileReader.GetUInt16(GetMemory(data, 2));
            if (dt > 0)
            {
                ent.ValueDates.Add(new(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
                {
                    Value = ProtonDbFileReader.ProtonBaseDate.AddDays(dt)
                });
            }
        }



        private static void ProcessCompositeTime(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var com = GetCompositeTime(data);
            if (com.ReplacementText == "")
            {
                ent.ValueTimes.Add(new(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
                {
                    Value = com.Time
                });
            }
            else
            {
                ent.ValueTexts.Add(new(valueIndex.EntityId,valueIndex.AttributeId, valueIndex.Seq)
                {
                    Value = com.ReplacementText 
                } );
                
            }
        }

        private static void ProcessFreeText(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short p = 0)
        {
            var id = ProtonDbFileReader.GetInt32(GetMemory(data, 4));
            if (id > 0 && frText.MoveToPage(id))
            {
                if (frText.EntityId == ent.Id)
                {
                    ent.ValueLongTexts.Add(new(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
                    {
                        Value=frText.Text(id)
                    });
                }
            }
        }

        private static void ProcessEntityPtr(Entity ent, ValueIndex valueIndex, ReadOnlyMemory<byte> data, short subTypeId)
        {
            var key = ProtonDbFileReader.GetString(data);
            using IndexReader ih = new();
            if (!key.IsNullOrEmpty())
            {
                ent.ValueTexts.Add(new(valueIndex.EntityId, valueIndex.AttributeId, valueIndex.Seq)
                { 
                    Value=key
                });
               
                var eId = ih.GetEntityId(subTypeId, key);

                if (eId > 0)
                {
                    ent.ValueEntities.Add(new(valueIndex.EntityId,valueIndex.AttributeId, valueIndex.Seq)
                    {
                        LinkedEntityId = eId
                    });
                }
            }
        }

        public static CompositeFloat GetCompositeSingle(ReadOnlyMemory<byte> data)
        {
            CompositeFloat cf = new();
            if (data.Length >8)
            {
                cf.QualifierCodeId = ProtonDbFileReader.GetInt32(GetMemory(data.Slice(8),4));

                if (cf.QualifierCodeId > 0)
                {
                    int qr = ProtonDbFileReader.GetInt16(data.Slice(4, 2));
                    if (qr == 1 )
                    {
                        return cf;
                    }
                }                 
            }
            cf.Number = ProtonDbFileReader.GetSingle(GetMemory(data,4));
             return cf;  
        }

        public static CompositeFloat GetCompositeDouble(ReadOnlyMemory<byte> data)
        {
            CompositeFloat cf = new();
            if (data.Length > 12)
            {
                cf.QualifierCodeId = ProtonDbFileReader.GetInt32(GetMemory(data.Slice(12), 4));
                if (cf.QualifierCodeId > 0)
                {
                    int qr = ProtonDbFileReader.GetInt16(data.Slice(8, 2));
                    if (qr ==1 )
                    {
                        return cf;
                    }
                }
            }
            cf.Number = (float)ProtonDbFileReader.GetDouble(GetMemory(data, 8));
            return cf;
        }

        public static CompositeTime GetCompositeTime(ReadOnlyMemory<byte> data)
        {
            var timeOffset = ProtonDbFileReader.GetUInt32(GetMemory(data,4));
           
            switch (timeOffset) 
            { 
                case 0x20000000: return new CompositeTime() { ReplacementText = "PRE" };
                case 0x40000000: return new CompositeTime() { ReplacementText = "POST" };
                case 0x80000000: return new CompositeTime() { Time=TimeOnly.MinValue, ReplacementText = "" };
                default: return new CompositeTime() { Time = TimeOnly.MinValue.AddMinutes(timeOffset / 60000), ReplacementText = "" };
            }                  
        }
    }


    public class CompositeTime
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

    public class CompositeFloat
    {
        public float? Number { get; set; }
        public int QualifierCodeId { get; set; }
        
    }    
}
