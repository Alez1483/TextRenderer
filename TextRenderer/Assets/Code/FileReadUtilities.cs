using System.IO;
using System;

public static class FileReadUtilities
{
    public static void SkipBytes(this Stream stream, long count)
    {
        stream.Seek(count, SeekOrigin.Current);
    }
    public static uint ReadBEUInt(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return FlipEndianess(reader.ReadUInt32());
        }
        return reader.ReadUInt32();
    }
    public static int ReadBEInt(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return FlipEndianess(reader.ReadInt32());
        }
        return reader.ReadInt32();
    }
    public static ushort ReadBEUShort(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return FlipEndianess(reader.ReadUInt16());
        }
        return reader.ReadUInt16();
    }
    public static short ReadBEShort(this BinaryReader reader)
    {
        if (BitConverter.IsLittleEndian)
        {
            return FlipEndianess(reader.ReadInt16());
        }
        return reader.ReadInt16();
    }
    public static ushort FlipEndianess(ushort v)
    {
        return (ushort)(v << 8 | v >> 8);
    }
    public static short FlipEndianess(short v)
    {
        return (short)FlipEndianess((ushort)v);
    }
    public static uint FlipEndianess(uint v)
    {
        v = v << 16 | v >> 16;
        return (v << 8 & 0xFF00FF00) | (v >> 8 & 0x00FF00FF);
    }
    public static int FlipEndianess(int v)
    {
        return (int)FlipEndianess((uint)v);
    }
    public static int GetBit(int i, int indexLSB)
    {
        return i >> indexLSB & 1;
    }
    public static bool IsBitActive(int i, int indexLSB)
    {
        return GetBit(i, indexLSB) == 1;
    }
}
