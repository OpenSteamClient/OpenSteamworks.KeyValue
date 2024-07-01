using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenSteamworks.Extensions;
using OpenSteamworks.KeyValue.Enums;
using OpenSteamworks.KeyValue.ObjectGraph;
using OpenSteamworks.Utils;
using OpenSteamworks.Utils.Enum;

namespace OpenSteamworks.KeyValue.Deserializers;

public sealed class KVBinaryDeserializer : IDisposable {
    private readonly Stream stream;
    private readonly EndianAwareBinaryReader reader;
    private readonly string[]? stringTable;

    private KVBinaryDeserializer(Stream stream, string[]? stringTable = null) {
        this.stringTable = stringTable;
        this.stream = stream;
        this.reader = new EndianAwareBinaryReader(stream, Encoding.Default, true, Endianness.Little);
    }

    private bool placeholderName = true;
    private KVObject Deserialize() {
        KVObject parent = new("", new List<KVObject>());
        while (true)
        {
            bool setPlaceholderName = false;

            KVObject? deserialized;
            var type = (BType)reader.ReadByte();
            if (type == BType.End) {
                break;
            }

            // Note: BType may lie here, since the new string table system stores ints but keeps the type as string
            string name;
            if (stringTable != null) {
                name = stringTable[reader.ReadInt32()];
            } else {
                name = reader.ReadNullTerminatedUTF8String();
            }

            dynamic value;

            if (placeholderName) {
                placeholderName = false;
                setPlaceholderName = true;
                parent.Name = name;
            }

            switch (type) {
                case BType.ChildObject:
                    value = Deserialize();
                    break;
                
                case BType.String:
                    value = reader.ReadNullTerminatedUTF8String();
                    break;
                
                case BType.Int32:
                case BType.Color:
                case BType.Pointer:
                    value = reader.ReadInt32();
                    break;
                
                case BType.UInt64:
                    value = reader.ReadUInt64();
                    break;
                
                case BType.Int64:
                    value = reader.ReadInt64();
                    break;
                
                case BType.Float32:
                    value = reader.ReadSingle();
                    break;
                
                default:
                    throw new Exception($"Unknown/unhandled KV type {type} encountered at position {stream.Position}");
		    }

            if (value is KVObject asKV) {
                deserialized = new KVObject(name, asKV.Value);
            } else {
                deserialized = new KVObject(name, value);
            }

            if (setPlaceholderName) {
                if (!deserialized.HasChildren) {
                    throw new InvalidOperationException("Root object is not List<>");
                }
                
                parent.Value = deserialized.Value;
            } else {
                parent.SetChild(deserialized);
            }
        }

        return parent;
    }

    public static KVObject Deserialize(byte[] bytes) {
        using (var serializer = new KVBinaryDeserializer(new MemoryStream(bytes)))
        {
            return serializer.Deserialize();
        }
    }

    /// <summary>
    /// If a string table is required for deserialization, provide it here, otherwise use another method
    /// </summary>
    public static KVObject DeserializeWithStringTable(byte[] bytes, string[] stringTable) {
        using var serializer = new KVBinaryDeserializer(new MemoryStream(bytes), stringTable);
        return serializer.Deserialize();
    }

    public static KVObject DeserializeWithStringTable(Stream stream, string[] stringTable) {
        using var serializer = new KVBinaryDeserializer(stream, stringTable);
        return serializer.Deserialize();
    }

    public static KVObject Deserialize(Stream stream) {
        using var serializer = new KVBinaryDeserializer(stream);
        return serializer.Deserialize();
    }

    public void Dispose()
    {
        ((IDisposable)reader).Dispose();
    }
}