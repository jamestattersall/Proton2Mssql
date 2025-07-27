using System.Buffers.Binary;

namespace ProtonConsole2.Proton
{

    public class BigEndianIntegerPrimitive : ProtonBinaryReaders.IPrimativeIntConverter
    {
        public bool IsBigEndian => true;

        public short Int16Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadInt16BigEndian(memory.Span);

        public Int32 Int32Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadInt32BigEndian(memory.Span);

        public ushort UInt16Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadUInt16BigEndian(memory.Span);

        public UInt32 UInt32Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadUInt32BigEndian(memory.Span);
    }

    public class LittleEndianIntegerPrimitive : ProtonBinaryReaders.IPrimativeIntConverter
    {
        public bool IsBigEndian => false;

        public short Int16Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadInt16LittleEndian(memory.Span);

        public Int32 Int32Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadInt32LittleEndian(memory.Span);

        public ushort UInt16Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadUInt16LittleEndian(memory.Span);

        public UInt32 UInt32Converter(ReadOnlyMemory<byte> memory) => BinaryPrimitives.ReadUInt32LittleEndian(memory.Span);
    }

    public class BigEndianFloatPrimative : ProtonBinaryReaders.IPrimativeFloatConverter
    {
        bool ProtonBinaryReaders.IPrimativeFloatConverter.IsBigEndian => true;

        float ProtonBinaryReaders.IPrimativeFloatConverter.DoubleConverter(ReadOnlyMemory<byte> memory) {
            float res = (float)BinaryPrimitives.ReadDoubleBigEndian(memory.Span);
            return float.IsRealNumber(res) ? res : 0;
        }

        float ProtonBinaryReaders.IPrimativeFloatConverter.SingleConverter(ReadOnlyMemory<byte> memory)
        {
            float res = BinaryPrimitives.ReadSingleBigEndian(memory.Span);
            return float.IsRealNumber(res) ? res : 0;
        }
    }

    public class LittleEndianFloatPrimative : ProtonBinaryReaders.IPrimativeFloatConverter
    {
        bool ProtonBinaryReaders.IPrimativeFloatConverter.IsBigEndian => true;

        float ProtonBinaryReaders.IPrimativeFloatConverter.DoubleConverter(ReadOnlyMemory<byte> memory)
        {
            float res = (float)BinaryPrimitives.ReadDoubleLittleEndian(memory.Span);
            return float.IsRealNumber(res) ? res : 0;
         
        }

        float ProtonBinaryReaders.IPrimativeFloatConverter.SingleConverter(ReadOnlyMemory<byte> memory)
        {
            float res = BinaryPrimitives.ReadSingleLittleEndian(memory.Span);
            return float.IsRealNumber(res) ? res : 0 ;
        }
    }
}
