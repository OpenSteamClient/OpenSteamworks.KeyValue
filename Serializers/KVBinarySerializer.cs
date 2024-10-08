using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenSteamworks.KeyValue.Enums;
using OpenSteamworks.KeyValue.ObjectGraph;
using OpenSteamworks.Utils;
using OpenSteamworks.Utils.Enum;

namespace OpenSteamworks.KeyValue.Serializers;

public sealed class KVBinarySerializer : IDisposable {
    private readonly Stream stream;
    private readonly EndianAwareBinaryWriter writer;
    private readonly bool enableStringTable = false;
    private readonly List<string> stringTable = new();

    private KVBinarySerializer(Stream stream, bool enableStringTable = false) {
        this.enableStringTable = enableStringTable;
        this.stream = stream;
        this.writer = new EndianAwareBinaryWriter(stream, Encoding.Default, true, Endianness.Little);
    }
    
    public static byte[] SerializeToArray(KVObject toSerialize) {
        using (var serializer = new KVBinarySerializer(new MemoryStream()))
        {
            serializer.SerializeRootObject(toSerialize);
            return (serializer.stream as MemoryStream)!.ToArray();
        }
    }

    public static void Serialize(Stream stream, KVObject toSerialize) {
        using (var serializer = new KVBinarySerializer(stream))
        {
            serializer.SerializeRootObject(toSerialize);
        }
    }

    public static void SerializeWithKeyTable(Stream stream, out List<string> keyTable, KVObject toSerialize) {
        using (var serializer = new KVBinarySerializer(stream, true))
        {
            serializer.SerializeRootObject(toSerialize);
            keyTable = serializer.stringTable;
        }
    }

    private void SerializeRootObject(KVObject obj) {
        SerializeInternal(obj);
        stream.WriteByte((byte)BType.End);
    }

    private void SerializeInternal(KVObject toSerialize) {
        var type = WriteTypeAndName(toSerialize);
        if (toSerialize.HasChildren) {
            foreach (var item in toSerialize.Children)
            {
                SerializeInternal(item);
            }

            stream.WriteByte((byte)BType.End);
            return;
        }

        switch (type) {            
            case BType.String:
                stream.Write(Encoding.UTF8.GetBytes(toSerialize.Value + "\0"));
                break;
            
            case BType.Int32:
            case BType.Color:
            case BType.Pointer:
                writer.WriteInt32((int)toSerialize.Value);
                break;
            
            case BType.UInt64:
                writer.WriteUInt64((ulong)toSerialize.Value);
                break;
            
            case BType.Int64:
                writer.WriteInt64((long)toSerialize.Value);
                break;
            
            case BType.Float32:
                writer.Write((float)toSerialize.Value);
                break;
            
            default:
                throw new Exception($"Unknown/unhandled KV type {type}");
        }
    }

    private BType WriteTypeAndName(KVObject obj) {
        BType type = GetBTypeFromType(obj.Value.GetType());
        stream.WriteByte((byte)type);

        if (enableStringTable)
        {
            var idx = stringTable.IndexOf(obj.Name);
            if (idx == -1)
            {
                stringTable.Add(obj.Name);
                idx = stringTable.Count - 1;
            }

            // This doesn't need null termination, parser will handle it
            writer.WriteInt32(idx);
        } else {
            stream.Write(Encoding.UTF8.GetBytes(obj.Name + "\0"));
        }

        return type;
    }

    private static BType GetBTypeFromType(Type type) {
        if (type == typeof(string)) {
            return BType.String;
        } else if (type == typeof(List<KVObject>)) {
            return BType.ChildObject;
        } else if (type == typeof(int)) {
            return BType.Int32;
        } else if (type == typeof(ulong)) {
            return BType.UInt64;
        } else if (type == typeof(long)) {
            return BType.Int64;
        } else if (type == typeof(float)) {
            return BType.Float32;
        }

        throw new InvalidOperationException("Type " + type + " has no corresponding BType");
    }

    public void Dispose()
    {
        ((IDisposable)writer).Dispose();
    }
}