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
        font.glyphs = new Glyph[glyphCount];
        GlyphReader.ReadGlyphs(glyphTable.offset, reader, loca, font.glyphs); //reads directly to the glyphs array
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