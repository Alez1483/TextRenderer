using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using UnityEngine;

public static class GlyphReader
{
    private static uint[] loca; //location table
    private static GlyphMetrics[] glyphMetricsArray; //needed for compound glyphs
    
    public static List<Vector2>[] ReadGlyphs(uint glyphTableOffset, BinaryReader reader, uint[] loca, GlyphMetrics[] glyphMetricsOut)
    {
        GlyphReader.loca = loca;
        glyphMetricsArray = glyphMetricsOut;
        List<Vector2>[] glyphData = new List<Vector2>[glyphMetricsOut.Length];

        for (int i = 0;  i < glyphMetricsOut.Length; i++)
        {
            ReadGlyph(glyphTableOffset, i, reader, glyphData);
        }

        return glyphData;
    }

    //reads one glyp from the 'glyf' table and stores it in glyphArray and glyphDataOut as well as returns it (easier for compounds)
    private static void ReadGlyph(uint glyphTableOffset, int glyphIndex, BinaryReader reader, List<Vector2>[] glyphDataOut)
    {
        if (glyphMetricsArray[glyphIndex] != null) //compound glyphs can load the component glyphs in advance
        {
            return;
        }

        if (loca[glyphIndex + 1] - loca[glyphIndex] == 0) //empty glyph
        {
            GlyphMetrics metrics = new GlyphMetrics(Vector2.zero, Vector2.zero);
            glyphMetricsArray[glyphIndex] = metrics;
            //leave glyphDataOut null
            return;
        }

        reader.BaseStream.Position = glyphTableOffset + loca[glyphIndex]; //needed for compound glyphs

        int numberOfContours = reader.ReadBEShort();

        int xMin = reader.ReadBEShort();
        int yMin = reader.ReadBEShort();
        int xMax = reader.ReadBEShort();
        int yMax = reader.ReadBEShort();

        Vector2 min = new Vector2(xMin, yMin);
        Vector2 max = new Vector2(xMax, yMax);

        if (numberOfContours > 0) //simple glyph
        {
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

            glyphDataOut[glyphIndex] = new List<Vector2>(xCoords.Length);
            if (xCoords.Length > 2) //apparently control characters such as unicode 2000 can have only 1 point in it
            {
                GenerateSplineData(coords, contourEndPoints, onCurve, glyphDataOut[glyphIndex]);
            }
        }
        else if (numberOfContours < 0) // compound
        {
            glyphDataOut[glyphIndex] = new List<Vector2>();
            List<Vector2> points = glyphDataOut[glyphIndex];

            ushort flag;
            do
            {
                flag = reader.ReadBEUShort();
                ushort childIndex = reader.ReadBEUShort();
                bool argsAreWords = FileReadUtilities.IsBitActive(flag, 0);
                bool argsAreXYValues = FileReadUtilities.IsBitActive(flag, 1);

                int e;
                int f;

                if (argsAreXYValues)
                {
                    if (argsAreWords)
                    {
                        e = reader.ReadBEShort();
                        f = reader.ReadBEShort();
                    }
                    else
                    {
                        e = reader.ReadSByte();
                        f = reader.ReadSByte();
                    }
                }
                else
                {
                    throw new NotImplementedException("compound glyph aligned with points");
                    /*if (argsAreWords)
                    {

                        //1st short contains the index of matching point in compound being constructed
                        //2nd short contains index of matching point in component
                    }
                    else
                    {
                        //1st byte containing index of matching point in compound being constructed
                        //2nd byte containing index of matching point in component
                    }*/
                }
                float a = 1.0f;
                float b = 0.0f;
                float c = 0.0f;
                float d = 1.0f;

                if (FileReadUtilities.IsBitActive(flag, 3)) //WE_HAVE_A_SCALE flag
                {
                    a = reader.ReadBEShort() / 16384f; // divided by 2^14 for F2Dot14 -> float
                    d = a;
                }
                else if (FileReadUtilities.IsBitActive(flag, 6)) //WE_HAVE_AN_X_AND_Y_SCALE flag
                {
                    a = reader.ReadBEShort() / 16384f; // divided by 2^14 for F2Dot14 -> float
                    d = reader.ReadBEShort() / 16384f;
                }
                else if (FileReadUtilities.IsBitActive(flag, 7)) //WE_HAVE_A_TWO_BY_TWO flag
                {
                    a = reader.ReadBEShort() / 16384f; // divided by 2^14 for F2Dot14 -> float
                    b = reader.ReadBEShort() / 16384f;
                    c = reader.ReadBEShort() / 16384f;
                    d = reader.ReadBEShort() / 16384f;
                }
                float m0 = Mathf.Max(Mathf.Abs(a), Mathf.Abs(b));
                float n0 = Mathf.Max(Mathf.Abs(c), Mathf.Abs(d));
                float m = m0;
                float n = n0;

                if (Mathf.Abs(Mathf.Abs(a) - Mathf.Abs(c)) + (0.5f / 16384f) <= (33f / 16384f)) //(0.5f / 16384f) is used to fix floating point precision issues
                {
                    m *= 2f;
                }
                if (Mathf.Abs(Mathf.Abs(b) - Mathf.Abs(d)) + (0.5f / 16384f) <= (33f / 16384f)) //(0.5f / 16384f) is used to fix floating point precision issues
                {
                    n *= 2f;
                }

                long position = reader.BaseStream.Position;

                ReadGlyph(glyphTableOffset, childIndex, reader, glyphDataOut); //read the child glyph

                reader.BaseStream.Position = position; //back to where the file was left
                
                List<Vector2> childData = glyphDataOut[childIndex];
                for(int i = 0; i < childData.Count; i++)
                {
                    points.Add(transformGlyphPoint(childData[i], a, b, c ,d, e, f, m , n));
                }

            } while (FileReadUtilities.IsBitActive(flag, 5)); //MORE_COMPONENTS flag
        }
        else //0 contours
        {
            //leave glyphDataOut null
        }

        GlyphMetrics outGlyph = new GlyphMetrics(min, max);
        glyphMetricsArray[glyphIndex] = outGlyph;
    }
    //generates the spline data so that every 3 elements in the pointsOut array makes one bezier segment (start, control, end)
    //the sentence above implies that the end of a segment is start of the next one meaning there's dublicated data
    //coords = the read coordinates. onCurve = which of the coords points are on and off curve points
    //contourEndPoints = last indexes of each contour (loop of bezier curves)
    private static void GenerateSplineData(Vector2[] coords, int[] contourEndPoints, bool[] onCurve, List<Vector2> pointsOut)
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
    //indices of the points are given
    //doubledPoints will add the same point twice
    private static void AddPointBetween(int firstIdx, int secondIdx, Vector2[] coords, List<Vector2> pntsOut, bool doubledPoint = false)
    {
        Vector2 point = (coords[firstIdx] + coords[secondIdx]) * 0.5f;
        pntsOut.Add(point);

        if (doubledPoint)
        {
            pntsOut.Add(point);
        }
    }
    //reads one coordinate axis array from file
    //flags are used to figure out the format
    //resulting array stored in coordOut
    //isX used to figure where on the flag masks to look for
    private static void ReadCoordArray(BinaryReader reader, byte[] flags, bool isX, int[] coordOut)
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
    //affine transformation for the point
    private static Vector2 transformGlyphPoint(Vector2 point, float a, float b, float c, float d, float e, float f, float m, float n)
    {
        Vector2 o;
        o.x = m * (a / m * point.x + c / m * point.y + e);
        o.y = n * (b / n * point.x + d / n * point.y + f);
        return o;
    }
}
