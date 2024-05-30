using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static UnityEngine.Rendering.DebugUI;
using UnityEngine.SocialPlatforms;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using System;

public static class FontReader
{
    private static Dictionary<string, Table> tables;

    private static Font font;
    private static int numTables;
    private static int glyphCount;
    private static uint[] loca;

    public static void LoadFontAsset(Font font)
    {
        FontReader.font = font;
        string path = AssetDatabase.GetAssetPath(font.FontAsset);

        using (var stream = File.Open(path, FileMode.Open))
        {
            using (var reader = new BinaryReader(stream))
            {
                stream.SkipBytes(4);
                numTables = reader.ReadBEUShort();
                //don't need binary search values
                stream.SkipBytes(6);

                tables = new Dictionary<string, Table>(numTables);

                for (int i = 0; i < numTables; i++)
                {
                    Table table;
                    table.tag = reader.ReadUInt32();
                    table.checkSum = reader.ReadBEUInt();
                    table.offset = reader.ReadBEUInt();
                    table.length = reader.ReadBEUInt();

                    tables.Add(table.getTagString(), table);
                }

                ReadLocaTable(reader);

                font.CharacterMapper = new CharacterMapper(tables["cmap"].offset, reader);

                ReadGlyphTable(reader);

                ReadMetrics(reader);
            }
        }
    }

    //reads all the glyphs from the 'glyf' table into the glyphs array
    private static void ReadGlyphTable(BinaryReader reader)
    {
        Table glyphTable = tables["glyf"];
        font.glyphs = new GlyphMetrics[glyphCount];
        List<Vector2>[] glyphData = GlyphReader.ReadGlyphs(glyphTable.offset, reader, loca, font.glyphs); //reads directly to the glyphs array

        //fill in the glyphBezierData and glyphLocaData arrays to be used for buffers

        int bezierCount = 0; //total count of bezier parts
        for(int i = 0; i < glyphCount; i++)
        {
            var points = glyphData[i];
            bezierCount += points != null ? points.Count : 0;
        }
        bezierCount /= 3;

        font.glyphBezierData = new Bezier[bezierCount];
        font.glyphLocaData = new uint[font.glyphs.Length + 1]; //extra location at the end to calculate the count of beziers in glyph

        //first location not set because it's 0 by default anyway
        uint bezierIdx = 0;
        for (int glyphIndex = 0; glyphIndex < font.glyphs.Length; glyphIndex++)
        {
            GlyphMetrics glyphMetrics = font.glyphs[glyphIndex];

            Vector2 min = glyphMetrics.Min;
            Vector2 size = glyphMetrics.Max - min;

            font.glyphLocaData[glyphIndex] = bezierIdx;

            var splineData = glyphData[glyphIndex];

            if (splineData == null)
            {
                continue;
            }

            for (int i = 0; i < splineData.Count; i += 3, bezierIdx++)
            {
                Bezier bezier;
                bezier.start = (splineData[i] - min) / size; //from 0 to 1 values
                bezier.middle = (splineData[i + 1] - min) / size;
                bezier.end = (splineData[i + 2] - min) / size;

                font.glyphBezierData[bezierIdx] = bezier;
            }
        }
        font.glyphLocaData[font.glyphLocaData.Length - 1] = bezierIdx;
    }

    //reads the location table
    //reads the glyph count and stores in glyphCount attribute
    private static void ReadLocaTable(BinaryReader reader)
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

    private static void ReadMetrics(BinaryReader reader)
    {
        Table horizontalHeader = tables["hhea"];
        horizontalHeader.MoveStreamToTable(reader.BaseStream, 4); //skip version

        font.Ascent = reader.ReadBEShort();
        font.Descent = reader.ReadBEShort();
        font.LineGap = reader.ReadBEShort();

        reader.BaseStream.SkipBytes(12 * 2); //skip advanceWidthMax, minLeftSideBearing ...

        int numOfLongHorMetrics = reader.ReadBEUShort();

        Table horizontalMetrics = tables["hmtx"];
        horizontalMetrics.MoveStreamToTable(reader.BaseStream);

        int i;
        for (i = 0; i < numOfLongHorMetrics; i++)
        {
            font.glyphs[i].AdvanceWidth = reader.ReadBEUShort();
            font.glyphs[i].LeftSideBearing = reader.ReadBEShort();
        }

        int lastAdvanceWidth = font.glyphs[i - 1].AdvanceWidth;

        for (; i < glyphCount; i++)
        {
            font.glyphs[i].AdvanceWidth = lastAdvanceWidth;
            font.glyphs[i].LeftSideBearing = reader.ReadBEShort();
        }
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