﻿namespace Lost_Ark_Packet_Capture
{
    public class BitReader : IDisposable
    {
        private byte[] Data;
        private Int32 BitOffset;
        private Int32 ByteOffset;
        public BitReader(Byte[] data, Int32 offset = 0)
        {
            Data = data;
            ByteOffset = offset;
        }
        public BitReader(BitReader data)
        {
            Data = data.Data;
            BitOffset = data.BitOffset;
            ByteOffset = data.ByteOffset;
        }
        public Int32 Position
        {
            get
            {
                return ByteOffset * 8 + BitOffset;
            }
            set
            {
                BitOffset = value % 8;
                ByteOffset = value / 8;
            }
        }
        public Int32 BitsLeft
        {
            get
            {
                return ((Data.Length - ByteOffset) - 1) * 8 + (8 - BitOffset);
            }
        }
        public Boolean ReadBit()
        {
            return ReadBits(1) == 1;
        }
        public UInt64 ReadDynamicValue(out Int32 size)
        {
            size = 8;
            if (ReadBit())
            {
                size *= 2;
                if (ReadBit()) size *= 2;
            }
            return ReadBits(size);
        }
        public UInt64 ReadBits(Int32 Count)
        {
            UInt64 Value = 0;
            Int32 ReadOffset = 0;
            while (Count > 0)
            {
                Int32 LeftInByte = 8 - BitOffset;
                Int32 ReadCount = Math.Min(Count, LeftInByte);
                if ((UInt64)Data.Length > (UInt64)ByteOffset)
                    Value |= (UInt64)((Data[ByteOffset] >> BitOffset) & ((1 << ReadCount) - 1)) << ReadOffset;
                ReadOffset += ReadCount;
                BitOffset += ReadCount;
                if (BitOffset == 8)
                {
                    BitOffset = 0;
                    ByteOffset++;
                }
                Count -= ReadCount;
            }

            return Value;
        }
        public UInt32 BitReverse(UInt32 value, Byte numBits)
        {
            UInt32 a = 0;
            for (Int32 i = 0; i < numBits; i++)
                if ((value & (UInt32)(1 << i)) != 0)
                    a |= (UInt32)(1 << (numBits - 1 - i));
            return a;
        }
        public Byte ReadByte()
        {
            return (Byte)ReadBits(8);
        }
        public UInt16 ReadUInt16()
        {
            return (UInt16)ReadBits(16);
        }
        public UInt32 ReadUInt32()
        {
            return (UInt32)ReadBits(32);
        }
        public UInt64 ReadUInt64()
        {
            return (UInt64)ReadBits(64);
        }
        public void SkipBits(Int32 Count)
        {
            BitOffset += Count % 8;
            ByteOffset += Count / 8;
        }
        public void Dispose()
        {
            Data = null;
        }
    }
}
