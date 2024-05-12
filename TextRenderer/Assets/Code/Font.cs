using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using UnityEngine.Analytics;

public class Font : IDisposable
{
    private Dictionary<string, Table> tables;

    private int glyphCount;

    private uint[] loca; //location table
    public Glyph[] glyphs { get; private set; }
    public CharacterMapper CharacterMapper { get; private set; }

    public ComputeBuffer GlyphDataBuffer { get; private set; }
    public ComputeBuffer GlyphLocaBuffer { get; private set; }

    public int Ascent { get; private set; }
    public int Descent { get; private set; }
    public int LineGap { get; private set; }

    public Font(string path)
    {
        using (var stream = File.Open(path, FileMode.Open))
        {
            using (var reader = new BinaryReader(stream))
            {
                stream.SkipBytes(4);
                ushort numTables = reader.ReadBEUShort();
                //don't need binary search values
                stream.SkipBytes(6);

                tables = new Dictionary<string, Table>(numTables);

                for(int i = 0; i < numTables; i++)
                {
                    Table table;
                    table.tag = reader.ReadUInt32();
                    table.checkSum = reader.ReadBEUInt();
                    table.offset = reader.ReadBEUInt();
                    table.length = reader.ReadBEUInt();

                    tables.Add(table.getTagString(), table);
                }

                ReadLocaTable(reader);



                CharacterMapper = new CharacterMapper(tables["cmap"].offset, reader);

                ReadGlyphTable(reader);

                InitializeBuffers();

                ReadMetrics(reader);
            }
        }
    }

    //reads all the glyphs from the 'glyf' table into the glyphs array
    private void ReadGlyphTable(BinaryReader reader)
    {
        Table glyphTable = tables["glyf"];
        glyphs = new Glyph[glyphCount];
        GlyphReader glyphReader = new GlyphReader(glyphTable.offset, reader, loca, glyphs); //reads directly to the glyphs array
    }
    
    //reads the location table
    //reads the glyph count and stores in glyphCount attribute
    private void ReadLocaTable(BinaryReader reader)
    {
        //header for shortLocaOffset
        tables["head"].MoveStreamToTable(reader.BaseStream, 50); //skip first 50 bytes
        bool shortLocaOffset = reader.ReadBEShort() == 0;

        //maximum profiles table for glyph count
        tables["maxp"].MoveStreamToTable(reader.BaseStream, 4); //skip first 4 bytes
        glyphCount = reader.ReadBEUShort();
        tables["loca"].MoveStreamToTable(reader.BaseStream);

        loca = new uint[glyphCount + 1];

        if (shortLocaOffset)
        {
            for (int i = 0; i < loca.Length; i++)
            {
                loca[i] = reader.ReadBEUShort() * 2u;
            }
        }
        else
        {
            for (int i = 0; i < loca.Length; i++)
            {
                loca[i] = reader.ReadBEUInt();
            }
        }
    }

    private void ReadMetrics(BinaryReader reader)
    {
        Table horizontalHeader = tables["hhea"];
        horizontalHeader.MoveStreamToTable(reader.BaseStream, 4); //skip version

        Ascent = reader.ReadBEShort();
        Descent = reader.ReadBEShort();
        LineGap = reader.ReadBEShort();

        reader.BaseStream.SkipBytes(12 * 2); //skip advanceWidthMax, minLeftSideBearing ...

        int numOfLongHorMetrics = reader.ReadBEUShort();

        Table horizontalMetrics = tables["hmtx"];
        horizontalMetrics.MoveStreamToTable(reader.BaseStream);

        int i;
        for (i = 0; i < numOfLongHorMetrics; i++)
        {
            glyphs[i].AdvanceWidth = reader.ReadBEUShort();
            glyphs[i].LeftSideBearing = reader.ReadBEShort();
        }

        int lastAdvanceWidth = glyphs[i - 1].AdvanceWidth;

        for (; i < glyphCount; i++)
        {
            glyphs[i].AdvanceWidth = lastAdvanceWidth;
            glyphs[i].LeftSideBearing = reader.ReadBEShort();
        }
    }

    private void InitializeBuffers()
    {
        List<Vector2> normalizedData = new List<Vector2>();
        uint[] locations = new uint[glyphCount + 1];

        uint location = 0;

        for (int glyphIndex = 0; glyphIndex < glyphCount; glyphIndex++)
        {
            locations[glyphIndex] = location;

            Glyph glyph = glyphs[glyphIndex];

            Vector2 min = glyph.Min;
            Vector2 max = glyph.Max;
            Vector2 size = max - min;

            var splineData = glyph.SplineData;

            for (int i = 0; i < splineData.Length; i++)
            {
                normalizedData.Add((splineData[i] - min) / size); //from 0 to 1 values
            }

            location += (uint)splineData.Length;
        }

        locations[glyphCount] = location; //extra location to calculate the length of the glyph data

        GlyphDataBuffer = new ComputeBuffer(normalizedData.Count, sizeof(float) * 2, ComputeBufferType.Structured);
        GlyphDataBuffer.SetData(normalizedData);

        GlyphLocaBuffer = new ComputeBuffer(locations.Length, sizeof(uint));
        GlyphLocaBuffer.SetData(locations);
    }

    public void Dispose()
    {
        GlyphDataBuffer.Release();
        GlyphLocaBuffer.Release();
    }
}

internal struct Table
{
    public uint tag; // big endian
    public uint offset;
    public uint length;
    public uint checkSum;

    public Table(uint tag, uint offset, uint length, uint checkSum)
    {
        this.tag = tag;
        this.offset = offset; 
        this.length = length;
        this.checkSum = checkSum;
    }
    public void MoveStreamToTable(Stream stream, long offset = 0)
    {
        stream.Position = this.offset + offset;
    }
    public string getTagString()
    {
        return TagToString(tag);
    }

    public override int GetHashCode()
    {
        return (int)tag;
    }
    public override string ToString()
    {
        return $"t: {tag}, o: {offset}, l: {length}, c: {checkSum}";
    }

    public static string TagToString(uint tag)
    {
        return System.Text.Encoding.ASCII.GetString(BitConverter.GetBytes(tag));
    }

    public static uint TagFromString(string tag)
    {
        if (tag == null | tag.Length != 4)
        {
            return 0;
        }

        return Convert.ToUInt32(System.Text.Encoding.ASCII.GetBytes(tag));
    }
}
