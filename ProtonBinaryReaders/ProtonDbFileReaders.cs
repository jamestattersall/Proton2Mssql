﻿
using System.Buffers.Binary;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography.Pkcs;
using System.Text;
namespace ProtonConsole2.Proton
{

    /// <summary>
    /// DICT.DBS one page for each DICT code (an ad-hoc list of lookup codes, created by database admin) 
    /// </summary>
    public class Dict() : ProtonDbFileReader("DICT.DBS")
    {
        public int CodeId => -PagePtr;

        public string Name => GetString(0, 64);

    }

    /// <summary>
    /// CODES.DBS. The "New" hierarchical (AKA "Read") codes, one page for each code
    /// intended to handle READ codes V2 (5-byte e.g. "abcd.", hierarchy explicit in code)
    /// but could be populated by ad-hic codes entered by system administrator
    /// </summary>
    public class RCode() : ProtonDbFileReader("CODES.DBS")
    {
        public int CodeId => PagePtr;

        public string Name => GetString(0, 80);

        public short CodeTypeID => GetInt16(80);

        /// <summary>
        /// the 5 byte "read" code e.g. "abcd."
        /// </summary>
        public string ReadCode => GetString(84, 5);

    }

    public class CodeDef() : ProtonDbFileReader("CODEDEF.DBS")
    {
        // the New (AKA READ) code types
        public int CodeDefId => PagePtr;

        public string Name => GetString(64, 80);
    }


    public class EntityDef() : ProtonDbFileReader("ENTITY.DBS")
    {
        // the Proton Entities are classes (type of object) (e.g. DataAccess, GP, Equipment etc.)
        // i.e. EAV Entity Type
        public int EntityDefId => PagePtr;

        public string Name => GetString(0, 16);

        public short IdGroup => GetInt16(16);

        public short IdItemId => GetInt16(18);

        public short KeyIndexDefId => GetInt16(20);

        public short DefaultIndexDefId => GetInt16(22);

    }

    public class Item() : ProtonDbFileReader("ITEM.DBS")
    {

        public int ItemId => PagePtr;

        public bool IsKeyDateItem => DateItemId == ItemId;

        public string Name => GetString(0, 2) + "." + GetString(2, 4);

        public short DataType => GetInt16(6);

        public bool IsIndexed => ExamineBit(Flag2Index, 1);

        public bool IsMandatory => ExamineBit(Flag2Index, 2);

        public bool CanDuplicateIndex => ExamineBit(Flag2Index, 3);

        public bool IsInstalled => ExamineBit(Flag1Index, 8);

        public bool IsCalculated => ExamineBit(Flag1Index, 7);

        public short SubType => GetInt16(8);

        public short DisplayLength => GetInt16(10);

        private int Flag1Index => (IntConverter.IsBigEndian) ? 12 : 13;

        private int Flag2Index => (IntConverter.IsBigEndian) ? 13 : 12;

        public short GroupId => GetInt16(14);

        public short DateItemId => GetInt16(16);

        public short EntityTypeId =>  GetInt16(18);

        public string Comment => GetString(20, 27);

        public string floatFormat => SubType > 0 ? "#." + new string('#', SubType) : "#";

    }

    public class Valid() : ProtonDbFileReader("VALID.DBS")
    {
        // one VALID.DBS page per item
        // VALID.DBS page numbers the same as ITEM.DBS

        public int ItemId => PagePtr;

        public double Max(short DataType, byte SubType)
        {
            switch (DataType)
            {
                case 2: return SubType > 0 ? GetInt8(40) : GetUInt8(40);
                case 3: return SubType > 0 ? GetInt16(40) : GetUInt16(40);
                case 4: return SubType > 0 ? GetInt32(40) : GetUInt32(40);
                case 5: return GetSingle(40);
                case 6: return GetDouble(40);
            }
            return 0;

        }
        public double Min(int DataType, int SubType)
        {
            switch (DataType)
            {
                case 2: return SubType > 0 ? GetInt8(32) : GetUInt8(32);
                case 3: return SubType > 0 ? GetInt16(32) : GetUInt16(32);
                case 4: return SubType > 0 ? GetInt32(32) : GetUInt32(32);
                case 5: return GetSingle(32);
                case 6: return GetDouble(32);
            }
            return 0;
        }

        public short CalcQuark => GetInt16(14);

        public short CodeTypeId => GetInt16(32);

        public short QualifierCodeType => GetInt16(12);

        public short ModifierCodeType => GetInt16(16);

    }

    public class Menu() : ProtonDbFileReaderExt("MENU.DBS", 20, 32)
    {
        public override bool MoveToNextBlock()
        {
            if (base.MoveToNextBlock())
            {
                return PageMemory.Span[ Ptr] != 0x0;
            }
            return false;
        }

        public int MenuId => PagePtr;

        public string Name => GetString(0, 20);

        public string ItemName => GetString(Ptr, 12);

        public byte Flags => GetUInt8(Ptr + 13);

        public byte Priv1 => GetUInt8(Ptr + 14);

        public byte Priv2 => GetUInt8( (Ptr + 15));

        public string Function => GetString( (Ptr + 16), 4);

        public short Parameter1 => GetInt16( (Ptr + 20));

        public short Parameter2 => GetInt16( (Ptr + 22));

        public short Parameter3 => GetInt16( (Ptr + 24));

        public short Parameter4 => GetInt16( (Ptr + 26));

        public short StartMenuId => GetInt16( (Ptr + 28));

        public short NextMenuId => GetInt16( (Ptr + 30));

    }
    public class Passwd() : ProtonDbFileReader("PASSWD.DBS")
    {
        public string Password
        {
            // secret algorithm for encrypted passwords in Proton
            get
            {
                Span<byte> chars = Encoding.ASCII.GetBytes(EncryptedPassword);

                for (int i = 0; i < chars.Length; i++)
                    //decrypt using XOR 31 (c# ^ 31) 
                    chars[i] ^= 31;
                return Encoding.ASCII.GetString(chars);
            }
        }
        public string EncryptedPassword => GetString(0, 12);

        public short FunctionId => GetInt16(12);

        public short FunctionParameter => GetInt16(14);

        public byte Priviledge => GetUInt8(16);

        public short EntityTypeId => GetInt16(18);

        public short IndexDefId => GetInt16(20);

        public short IdLineScreenId => GetInt16(22);

        public string username => GetString(24, 4);

    }

    public class Base() : ProtonDbFileReader("BASE.DBS", 64)
    {
        public int BaseId => PagePtr;

        public string Name => GetString(0, 16);

        public short DbPageLength => GetInt16(24);

        public long ValidMaxPage => GetInt32(28);

        public bool CanValidate => GetInt8(27) == 1;
    }


    public class IndexDef() : ProtonDbFileReader("IDXDEF.DBS")
    {
        public int IndexDefId => PagePtr;

        public byte KeyLength => (byte)(4 * ((GetUInt16(0) + 11) / 4));

        public bool AllowPartialMatch => ExamineBit(2, 1);

        public int IndexIdStart => GetInt32(4);

        public int IndexIdMiddle => GetInt32(8);

        public short IdlineScreenId => GetInt16(16);
    }


    public class Index() : ProtonDbFileReaderExt("INDEX.DBS", 20)
    {

        private bool _MoveToNextBlock()
        {
            if (base.MoveToNextBlock())
            {
                return EntityId > 0;
            }
            return false;
        }

        public void SetBlockLength(short value)
        {
            BlockLength = value;
        }

        public override bool MoveToNextBlock()
        {

            if (_MoveToNextBlock())
            {
                return true;
            }
            if (MoveToNextPage())
            {
                return _MoveToNextBlock();
            }
            return false;
        }

        public bool MoveToPreviousBlock()
        {
            if (Ptr == HeaderLength)
            {
                if (MoveToPreviousPage())
                {
                    Ptr =   (HeaderLength + BlockLength * (BlocksOnPage - 1));
                    return true;
                }
            }
            else
            {
                Ptr -= BlockLength;
                return true;
            }
            return false;
        }

        public bool MoveToNextPage()
        {
            var np = NextPageId;
            return MoveToPage(np);
        }

        public bool MoveToPreviousPage()
        {
            var pp = PreviousPageId;
            return MoveToPage(pp);
        }

        public int IndexId => PagePtr;

        private int PreviousPageId =>  GetInt32(0);

        private int NextPageId =>  GetInt32(4);

        public short BlocksOnPage => GetInt16(12);

        public short IndexDefId => GetInt16(14);

        public ReadOnlyMemory<byte> KeyTextRaw => PageMemory.Slice(Ptr + 4, BlockLength - 4);

        public string KeyText => GetString( (Ptr + 4),  (BlockLength - 4));

        public int EntityId => GetInt32(Ptr);

        public int Pointer 
        {
          get { return Ptr; }
          set { Ptr = value; }
        }
    }

    public class Screen() : ProtonDbFileReaderExt("SCREEN.DBS", 48, 10)
    {
        // All Proton Screens, one screen per page.
        public int ScreenId => PagePtr;

        public string Name => GetString(0, 24);

        public short ItemCount => GetInt16(28);

        public short RowCount => GetInt16(26);

        public short ItemId => GetInt16(Ptr);

        public byte X => GetUInt8( (Ptr + 3));
        public byte Y => GetUInt8( (Ptr + 5));

        public override bool MoveToNextBlock()
        {
            if (base.MoveToNextBlock())
            {
                return PageMemory.Span[Ptr + 1] > 0;
            }
            else
            {
                return false;
            }
        }
    }

    public class ScrTxt() : ProtonDbFileReaderExt("SCRTXT.DBS", 0, 0)
    {
        // the text (captions) displayed on screen
        // all text for screen stored in same page
        public int ScrTxtId => PagePtr;

        private const byte EndOfBlockCode = 5;
        private int _validPageLength;

        // --Proton screen text consists of a binary series to be interpreted as (mostly) ascii characters
        // --The series is delimited by ascii character 5 but beware, as not every character 5 is a delimieter!
        // --The delimiting character 5 is followed by the screen co-ordinates (x and y, 1-byte each), then the actual text, terminated by character 5
        // --The x or y co-ordinate could be any number including 5


        public override bool MoveToPage(short pagePtr)
        {
            if (base.MoveToPage(pagePtr))
            {
                _validPageLength = PageMemory.Span.LastIndexOfAnyExcept(NULL) + 1;
                
                SetBlockData();
                return BlockLength > 4;
            }
            return false;
        }
        public override bool MoveToPage(int pagePtr)
        {
            if (base.MoveToPage(pagePtr))
            {
                _validPageLength = PageMemory.Span.LastIndexOfAnyExcept(NULL) + 1;

                SetBlockData();
                return BlockLength > 4;
            }
            return false;
        }

        public byte X => PageMemory.Span[Ptr + 1];
        public byte Y => PageMemory.Span[Ptr + 2];
        public string Text => GetString(PageMemory.Slice(Ptr + 3, BlockLength - 3));

        public override bool MoveToNextBlock()
        {
            if (base.MoveToNextBlock())
            {
                SetBlockData();
                if (BlockLength > 2)
                    return true;
            }
            return false;
        }


        private void SetBlockData()
        {
            if (Ptr + 4 > _validPageLength)
            {
                BlockLength = 0;
                return;
            }
            var _span = PageMemory.Slice(Ptr + 3, _validPageLength - Ptr - 3);

            BlockLength =  (_span.Span.IndexOf(EndOfBlockCode) + 3);
            if (BlockLength < 3)
            {
                BlockLength =  (_span.Length + 3);
            }
        }
    }

    public class TrGroup() : ProtonDbFileReaderExt("TRGROUP.DBS", 64, 2)
    {
        public int TrGroupId => PagePtr;

        public string Name => GetString(0, 30);

        public short DateItemId => GetInt16(34);

        public short TimeItemId => GetInt16(36);

        public short ItemId => GetInt16(Ptr);

        public override bool MoveToNextBlock()
        {
            if (base.MoveToNextBlock())
            {
                return ItemId > 0;
            }
            else
            {
                return false;
            }
        }
    }

    public class Patsts() : ProtonDbFileReader("PATSTS.DBS")
    {
        public int EntityId => PagePtr;
       
        public DateTime Updated => GetDateTime(36).AddMilliseconds(GetUInt32(32));

        public uint Status => GetUInt8(1);  

    }

    public class KeyDef() : ProtonDbFileReader("KEYDEF.DBS")
    {
        public int KeyDefId => PagePtr;

        public string Name => GetString(16,   (PageLength - 16));

        public short IndexDefId => GetInt16(0);

        public short EntityTypeId => GetInt16(14);

        public short KeyMatchItemId1 => GetInt16(2);

        public short KeyMatchItemId2 => GetInt16(4);

        public short Priv => GetInt16(6);

        public byte KeyLength => GetUInt8(9);

        public SByte Typ1 => GetInt8(10);

        public SByte Typr => GetInt8(11);

        public string Prefix => GetString(12, 1);

    }

    public class FrText() : ProtonDbFileReader("FRTEXT.DBS")
    {
        public int FrTextId => PagePtr;

        public int NextPageId => GetInt32(0);

        public short LineCount => GetUInt8(6);

        public int EntityId => GetInt32(8);

        public short PageSequence => GetInt16(16);

        public bool MoveToNextPage()
        {
            var np = NextPageId;
            return MoveToPage(np);
        }


        //character codes
        private const byte LF = 0x0A; // Line feed
        private const byte DEL = 127; // delete

        private string lineFeeds()
        {
            //aways get at least 1 linefeed at end of page.
            return new string(Convert.ToChar(LF), LineCount + 1);
        }

        private string PageText
        {
            get
            {
                ReadOnlySpan<byte> rawText = PageMemory.Slice(32).Span;
                rawText = rawText.Slice(0, rawText.LastIndexOfAnyExcept(NULL) + 1); //rmove all trailing [NUL]
                if (rawText.Length > 0)
                {
                    if (rawText[rawText.Length - 1] == DEL)
                    {
                        //terminating [DEL]
                        rawText = rawText.Slice(0, rawText.Length - 1); //remove
                    }

                    // need a writeable span ;
                    Span<byte> writeable = Unsafe.As<ReadOnlySpan<byte>, Span<byte>>(ref rawText);
                    // slow safe version: var writeable = rawText.ToArray().AsSpan(); 

                    // [NUL] and [DEL] interpreted as [LF]
                    writeable.Replace(DEL, LF);
                    writeable.Replace(NULL, LF);

                    return Encoding.ASCII.GetString(writeable);
                }
                return string.Empty;
            }
        }

        public string Text(int FreeTextpageId)
        {
            var sb = new StringBuilder();
            
            if (MoveToPage( FreeTextpageId))
            {
                sb.Append(PageText);
                sb.Append(lineFeeds());
                while (MoveToNextPage())
                {
                    sb.Append(PageText);
                    sb.Append(lineFeeds());
                }
            }
           return sb.ToString();
        }

   
    }

    public class Vrx() : ProtonDbFileReaderExt("VRX.DBS", 0, 8)
    {
        public int EntityId => PagePtr;

        public override bool MoveToNextBlock()
        {
            if (base.MoveToNextBlock())
            {
                return MaxItemId > 0;
            }
            return false;
        }

        public int FirstDataPageId => GetInt32(4);

        public short MaxItemId => GetInt16(Ptr);

        public short DataPageCount => GetInt16(  (2 + Ptr));

        public int DataPageId => GetInt32( (4 + Ptr));

    }

    public class Data() : ProtonDbFileReaderExt("DATA.DBS", 16)
    {
        private ReadOnlyMemory<byte> _value;
        private const byte REPEATMASK = 0x80;
        private const byte BLOCKLENGTHMASK = 0x7F;
        private byte _counter = 0;
        private short _currentItemId = 0;

        public int DataPageId => PagePtr;

        public int NextPageId => GetInt32(0);

        public short FreeByteCount => GetInt16(6);

        private int GetThisPageLength => (PageLength - FreeByteCount);

        public int EntityId => GetInt32(8);

        public short HighItemId => GetInt16(12);

        public short LowItemId => GetInt16(16);

        public short ItemId => GetInt16(Ptr);

        public short Seq { get; private set; }

        public int ThisPageLength { get; private set; }

        public ReadOnlyMemory<byte> RawValue => _value;


        private bool GetToRepeat(byte ctl) => (ctl & REPEATMASK) == REPEATMASK;

        private byte GetRepeatCount => (byte)(PageMemory.Slice(Ptr + BlockLength - 1, 1).Span[0] -1);

        private byte GetCtl => PageMemory.Slice(Ptr + 2, 1).Span[0];

        private byte GetBlockLength(byte ctl) => (byte)(ctl & BLOCKLENGTHMASK);

        private byte GetDataLength(bool toRepeat) => (byte)(toRepeat ? BlockLength - 4 : BlockLength - 3);

        private ReadOnlyMemory<byte> GetRawValue(byte dataLength) => (dataLength == 0)?
            ReadOnlyMemory<byte>.Empty :
           PageMemory.Slice(Ptr + 3, dataLength);
        
       
        /// <summary>
        /// Moves pointers to start of next block. 
        /// Advances to next page if no more blocks on that page.
        /// </summary>
        /// <returns>true if valid block</returns>
        public override bool MoveToNextBlock()
        {
            if (_counter == 0)
            {
                if (Ptr + BlockLength + 3 <= ThisPageLength)
                {
                    Ptr += BlockLength;
                    if (SetBlockData()) return true;
                }
                var oldInstanceId = EntityId;
                if (MoveToNextPage())
                {
                    if (oldInstanceId != EntityId)
                    {
                        throw new Exception("data chain error");
                    }
                    return true;
                }
                return false;
                
            } 
            Seq++;
            _counter--;

            return true;
        }

        public override bool MoveToPage(int pagePtr)
        {
            if (base.MoveToPage(pagePtr))
            {
                ThisPageLength = GetThisPageLength;
                                
                return SetBlockData();
            }
            return false;
        }

        private bool MoveToNextPage()
        {
            var np = NextPageId;
            if (np != 0) return MoveToPage(np);
            else return false;
        }

        private bool SetBlockData()
        {
            byte ctl = GetCtl;
            if (ctl > 0) {
                bool toRepeat = GetToRepeat(ctl);
                BlockLength = GetBlockLength(ctl);
                byte dataLength = GetDataLength(toRepeat);

                _value = GetRawValue(dataLength);
                short NewItemId = ItemId;
                if (NewItemId != _currentItemId)
                {
                    _currentItemId = NewItemId;
                    Seq = 0;
                }

                if (toRepeat)
                {
                    var repeatCount = GetRepeatCount ;
                    if (_value.IsEmpty)
                    {
                        Seq += repeatCount;
                    }
                    else
                    {
                        _counter = repeatCount;
                    }
                }

                Seq++;

                return true;
            }
            return false;
        }
    }
}