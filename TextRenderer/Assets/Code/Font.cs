using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class Font
{
    private Dictionary<string, Table> tables;
    private int glyphCount;
    private uint[] loca;
    public Glyph[] glyphs;

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

                ReadGlyphTable(tables["glyf"], reader);
            }
        }
    }

    //reads all the glyphs from the 'glyf' table into the glyphs array
    private void ReadGlyphTable(Table glyphTable, BinaryReader reader)
    {
        glyphs = new Glyph[glyphCount];

        for(int i = 0; i < glyphCount; i++)
        {
            glyphs[i] = ReadGlyph(glyphTable.offset, i, reader);
        }
    }
    //reads one glyp from the 'glyf' table
    private Glyph ReadGlyph(uint glyphTableOffset, int glyphIndex, BinaryReader reader)
    {
        reader.BaseStream.Position = glyphTableOffset + loca[glyphIndex];

        int numberOfContours = reader.ReadBEShort();

        int xMin = reader.ReadBEShort();
        int yMin = reader.ReadBEShort();
        int xMax = reader.ReadBEShort();
        int yMax = reader.ReadBEShort();

        Vector2 min = new Vector2(xMin, yMin);
        Vector2 max = new Vector2(xMax, yMax);

        List<Vector2> points;

        if (numberOfContours > 0)
        {
            //regular glyph
            int[] contourEndPoints = new int[numberOfContours];

            for (int i = 0; i < numberOfContours; i++)
            {
                contourEndPoints[i] = reader.ReadBEUShort();
            }
            int totalGlyfPoints = contourEndPoints[contourEndPoints.Length - 1] + 1;

            //skip instructions
            int instructionLength = reader.ReadBEUShort();
            reader.BaseStream.SkipBytes(instructionLength);

            byte[] flags = new byte[totalGlyfPoints];

            for (int i = 0; i < totalGlyfPoints; i++)
            {
                byte flag = reader.ReadByte();
                flags[i] = flag;
                //repeat flag
                if (FileReadUtilities.GetBit(flag, 3) == 1)
                {
                    byte repeatCount = reader.ReadByte();

                    for (int j = 0; j < repeatCount; j++)
                    {
                        flags[++i] = flag;
                    }
                }
            }

            int[] xCoords = new int[totalGlyfPoints];
            int[] yCoords = new int[totalGlyfPoints];

            ReadCoordArray(reader, flags, true, xCoords);
            ReadCoordArray(reader, flags, false, yCoords);

            bool[] onCurve = new bool[totalGlyfPoints];
            Vector2[] coords = new Vector2[totalGlyfPoints];
            for (int i = 0; i < totalGlyfPoints; i++)
            {
                coords[i] = new Vector2(xCoords[i], yCoords[i]);
                onCurve[i] = FileReadUtilities.IsBitActive(flags[i], 0);
            }

            points = new List<Vector2>(xCoords.Length);
            GenerateSplineData(coords, contourEndPoints, onCurve, points);
        }
        else
        {
            //combound glyph (or empty)
            points = new List<Vector2>(); //remove later
        }

        return new Glyph(points.ToArray(), min, max);
    }

    //generates the spline data so that every 3 elements in the pointsOut array makes one bezier segment (start, control, end)
    //the sentence above implies that the end of a segment is start of the next one meaning there's dublicated data
    //coords = the read coordinates. onCurve = which of the coords points are on and off curve points
    //contourEndPoints = last indexes of each contour (loop of bezier curves)
    private void GenerateSplineData(Vector2[] coords, int[] contourEndPoints, bool[] onCurve, List<Vector2> pointsOut)
    {
        int contourStartIndex = 0;
        //originalIndex = index going through flags/xCoords/yCoords/onCurve array
        for (int contourIndex = 0, originalIndex = 0; contourIndex < contourEndPoints.Length; contourIndex++)
        {
            bool firstOnCurve = onCurve[contourStartIndex];
            int secondIndex = contourStartIndex + 1;
            bool secondOnCurve = onCurve[secondIndex];

            //add two first points and possibly additional inbetween
            if (firstOnCurve)
            {
                pointsOut.Add(coords[contourStartIndex]);
            }
            if (firstOnCurve == secondOnCurve)
            {
                AddPointBetween(contourStartIndex, secondIndex, coords, pointsOut);
            }
            if (firstOnCurve || !secondOnCurve)
            {
                //not added when the first is off curve and second is on (to avoid doubled first point)
                pointsOut.Add(coords[secondIndex]);
            }

            originalIndex++; //first two already done

            //add rest of the points and additionals inbetween
            for (; originalIndex <= contourEndPoints[contourIndex]; originalIndex++)
            {
                int sIndex = originalIndex + 1;
                //wrap the second index around the contour
                if (sIndex > contourEndPoints[contourIndex])
                {
                    sIndex = contourStartIndex;
                }
                bool fOnCurve = onCurve[originalIndex];
                bool sOnCurve = onCurve[sIndex];

                if (fOnCurve)
                {
                    //add only if on curve (to avoid dublicates)
                    pointsOut.Add(coords[originalIndex]);
                }
                if (fOnCurve == sOnCurve)
                {
                    //add point between two subsequent off or on curve points
                    //if the added point is on curve, add two (to start another curve)
                    AddPointBetween(originalIndex, sIndex, coords, pointsOut, !onCurve[originalIndex]);
                }
                //last point always added
                pointsOut.Add(coords[sIndex]);
            }

            //wrap around logic when contours first is off curve
            if (!firstOnCurve)
            {
                Debug.Log("Apparently contours first can be off bounds");
                if (secondOnCurve)
                {
                    pointsOut.Add(coords[secondIndex]);
                }
                else
                {
                    AddPointBetween(contourStartIndex, secondIndex, coords, pointsOut);
                }
            }
            contourStartIndex = contourEndPoints[contourIndex] + 1;
        }
    }

    //adds point (or two) at the end of pnts array between firstIdx and secondIdx of the coordinate arrays
    //xCs and yCs are the x and y coordinates of the points
    //doubledPoints will add the same point twice
    private void AddPointBetween(int firstIdx, int secondIdx, Vector2[] coords, List<Vector2> pnts, bool doubledPoint = false)
    {
        Vector2 point = (coords[firstIdx] + coords[secondIdx]) * 0.5f;
        pnts.Add(point);

        if (doubledPoint)
        {
            pnts.Add(point);
        }
    }


    //reads one coordinate axis array from file
    //flags are used to figure out the format
    //resulting array stored in coordOut
    //isX used to figure where on the flag masks to look for
    private void ReadCoordArray(BinaryReader reader, byte[] flags, bool isX, int[] coordOut)
    {
        int lastPosition = 0;


        for (int i = 0; i < flags.Length; i++)
        {
            //x/y-Short Vector
            if (FileReadUtilities.IsBitActive(flags[i], isX ? 1 : 2))
            {
                //1 byte long coord
                lastPosition += FileReadUtilities.IsBitActive(flags[i], isX ? 4 : 5) ? reader.ReadByte() : (short)(-reader.ReadByte());
                coordOut[i] = lastPosition;
            }
            else
            {
                //2 byte long coord (signed)
                if (FileReadUtilities.IsBitActive(flags[i], isX ? 4 : 5)) //same as last bit
                {
                    coordOut[i] = lastPosition;
                }
                else
                {
                    lastPosition += reader.ReadBEShort();
                    coordOut[i] = lastPosition;
                }
            }
        }
    }
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
