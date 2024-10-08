using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OpenSteamworks.KeyValue.ObjectGraph;

namespace OpenSteamworks.KeyValue.Serializers;

public class KVTextSerializer {
    private readonly KVObject rootObject;
    private readonly StringBuilder builder;
    private int indentation = 0;
    private readonly List<string> stack = new();

    private KVTextSerializer(KVObject rootObject) {
        this.rootObject = rootObject;
        this.builder = new();
    }

    public static string Serialize(KVObject rootObject) {
        var serializer = new KVTextSerializer(rootObject);
        return serializer.Serialize();
    }

    private string Serialize() {
        WriteObject(rootObject);
        return builder.ToString();
    }

    private void WriteObject(KVObject? obj) { 
        if (obj == null) {
            return;
        }

        stack.Add(obj.Name);


        if (obj.HasChildren) {
            WriteStartObject(obj.Name);
            foreach (var item in obj.Children)
            {
                WriteObject(item);
            }
            WriteEndObject();
        } else {
            if (obj.Value is not IConvertible convertible) 
            {
                throw new ArgumentException("KV value is not IConvertible", nameof(obj));
            } 

            WriteKeyValuePair(obj.Name, convertible);
        }

        stack.Remove(obj.Name);
    }

    private void WriteStartObject(string name)
    {
        WriteIndentation();
        WriteText(name);
        WriteLine();
        WriteIndentation();
        builder.Append('{');
        indentation++;
        WriteLine();
    }

    private void WriteEndObject()
    {
        indentation--;
        WriteIndentation();
        builder.Append('}');
        builder.AppendLine();
    }

    private void WriteKeyValuePair(string name, IConvertible value)
    {
        WriteIndentation();
        WriteText(name);
        builder.Append('\t');
        WriteText(value.ToString(CultureInfo.InvariantCulture));
        WriteLine();
    }

    private void WriteIndentation()
    {
        if (indentation == 0)
        {
            return;
        }

        var text = new string('\t', indentation);
        builder.Append(text);
    }

    private void WriteText(string text)
    {
        builder.Append('"');

        for (int i = 0; i < text.Length; i++)
        {
            var @char = text[i];
            switch (@char)
            {
                case '"':
                    builder.Append("\\\"");
                    break;

                case '\\':
                    if (text.Length == i + 1) {
                        // Don't add the other slash if it's the end of the string
                        builder.Append('\\');
                        break;
                    }

                    builder.Append("\\\\");
                    break;

                default:
                    builder.Append(@char);
                    break;
            }
        }

        builder.Append('"');
    }

    private void WriteLine()
    {
        builder.AppendLine();
    }
}