using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

[Serializable]
public class CharacterMapper : ISerializationCallbackReceiver
{
    [SerializeField]
    private Dictionary<uint, int> charToGlyphIndexMap = new();

    //only needed for serialization
    [SerializeField, HideInInspector]
    private List<uint> keys = new List<uint>();
    [SerializeField, HideInInspector]
    private List<int> values = new();

    public CharacterMapper(uint cmapTableOffset, BinaryReader reader)
    {
        reader.BaseStream.Position = cmapTableOffset + 2;
        int tableCount = reader.ReadBEUShort();
        ushort platform = reader.ReadBEUShort();
        reader.BaseStream.SkipBytes(2);
        uint offset = reader.ReadBEUInt();

        //read only the first table for now (unicode)
        if (platform > 0)
        {
            throw new NotImplementedException("Only unicode mapping implemented");
        }

        reader.BaseStream.Position = cmapTableOffset + offset;

        ushort format = reader.ReadBEUShort();

        switch (format)
        {
            case 4:
                ReadFormat4(reader);
                break;
            case 12:
                ReadFormat12(reader);
                break;
            case 13:
                ReadFormat13(reader);
                break;
            default:
                throw new NotImplementedException($"Character map format {format} not implemented");
        }
    }

    //reader position must be right after reading the format
    private void ReadFormat4(BinaryReader reader)
    {
        reader.BaseStream.SkipBytes(2 + 2); //skip length and language
        int segCount = reader.ReadBEUShort() / 2;
        reader.BaseStream.SkipBytes(3 * 2); //skip searchRange, entrySelector and rangeShift
        int[] endCodes = new int[segCount];
        int[] startCodes = new int[segCount];
        int[] idDelta = new int[segCount];
        int[] idRangeOffset = new int[segCount];

        for (int i = 0; i < segCount; i++)
        {
            endCodes[i] = reader.ReadBEUShort();
        }
        reader.BaseStream.SkipBytes(2); //skip reservedPad
        for (int i = 0; i < segCount; i++)
        {
            startCodes[i] = reader.ReadBEUShort();
        }
        for (int i = 0; i < segCount; ++i)
        {
            idDelta[i] = reader.ReadBEUShort();
        }
        for (int i = 0; i < segCount; i++)
        {
            idRangeOffset[i] = reader.ReadBEUShort();
        }

        int glyphIndexArrayStart = (int)reader.BaseStream.Position;

        for (int segmentIndex = 0; segmentIndex < segCount; segmentIndex++)
        {
            int glyphIndex;

            int rangeOffset = idRangeOffset[segmentIndex];
            int delta = idDelta[segmentIndex];
            int startCode = startCodes[segmentIndex];
            int endCode = endCodes[segmentIndex];

            int distanceToGlyphStart = (segCount - segmentIndex) * 2; //bytes

            for (int unicode = startCode, i = 0; unicode <= endCode; unicode++, i++)
            {
                if (rangeOffset == 0)
                {
                    glyphIndex = (unicode + delta) % 65536;
                }
                else
                {
                    //glyphIndexAddress = idRangeOffset[i] + 2 * (c - startCode[i]) + (Ptr) &idRangeOffset[i]
                    reader.BaseStream.Position = rangeOffset + (2 * i) + (glyphIndexArrayStart - distanceToGlyphStart);
                    glyphIndex = reader.ReadBEUShort();
                    if (glyphIndex == 0) //missing glyph? somehow?
                    {
                        continue;
                    }
                    glyphIndex = (glyphIndex + delta) % 65536;
                }

                charToGlyphIndexMap.Add((uint)unicode, glyphIndex);
            }
        }
    }

    //reader position must be right after reading the format
    private void ReadFormat12(BinaryReader reader)
    {
        reader.BaseStream.SkipBytes(2 + 4 + 4); //reserved, length and language
        uint numGroups = reader.ReadBEUInt();

        for(uint i = 0;  i < numGroups; i++)
        {
            uint startCharCode = reader.ReadBEUInt();
            uint endCharCode = reader.ReadBEUInt(); 
            uint startGlyphCode = reader.ReadBEUInt();

            for (uint unicode = startCharCode, glyphCode = startGlyphCode; unicode <= endCharCode; unicode++, glyphCode++)
            {
                charToGlyphIndexMap.Add(unicode, (int)glyphCode);
            }
        }
    }

    //reader position must be right after reading the format
    private void ReadFormat13(BinaryReader reader)
    {
        reader.BaseStream.SkipBytes(2 + 4 + 4); //reserved, length and language
        uint numGroups = reader.ReadBEUInt();

        for (uint i = 0; i < numGroups; i++)
        {
            uint startCharCode = reader.ReadBEUInt();
            uint endCharCode = reader.ReadBEUInt();
            uint glyphId = reader.ReadBEUInt();

            for (uint unicode = startCharCode; unicode <= endCharCode; unicode++)
            {
                charToGlyphIndexMap.Add(unicode, (int)glyphId);
            }
        }
    }

    public int CharToGlyphIndex(uint c)
    {
        try
        {
            return charToGlyphIndexMap[c];
        }
        catch (KeyNotFoundException)
        {
            return 0; //missing character glyph
        }
    }

    public void OnBeforeSerialize()
    {
        if (charToGlyphIndexMap == null)
        {
            return;
        }

        keys.Clear();
        values.Clear();

        foreach (var keyValuePair in charToGlyphIndexMap)
        {
            keys.Add(keyValuePair.Key);
            values.Add(keyValuePair.Value);
        }
    }
    public void OnAfterDeserialize()
    {
        charToGlyphIndexMap = new Dictionary<uint, int>(keys.Count);

        for (int i = 0; i < keys.Count; i++)
        {
            charToGlyphIndexMap.Add(keys[i], values[i]);
        }
    }
}
