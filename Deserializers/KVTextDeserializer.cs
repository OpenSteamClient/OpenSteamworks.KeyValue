using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using OpenSteamworks.KeyValue.ObjectGraph;

namespace OpenSteamworks.KeyValue.Deserializers;

/// <summary>
/// Deserialized KV1 text data. Does not care about formatting (white spaces, tabs and newlines)
/// </summary>
public class KVTextDeserializer {
    private int index;
    private readonly string Text;
    private ReadOnlySpan<char> AllChars => Text.AsSpan();
    private ReadOnlySpan<char> CurrentChars => Text.AsSpan(index);
    private bool HasReachedEnd => index >= Text.Length;

    private KVTextDeserializer(string text) {
        this.Text = text;
    }

    public static KVObject Deserialize(string text) {
        var deserializer = new KVTextDeserializer(text);
        return deserializer.DeserializeInternal();
    }

    private bool placeholderName = true;
    private KVObject DeserializeInternal() {
        KVObject parent = new("", new List<KVObject>());
        while (true)
        {
            bool setPlaceholderName = false;
            KVObject? deserialized;
                
            if (GetNextNonWhitespaceChar() == '}') {
                index++;
                break;
            }

            string name = ReadNextQuotedString();
            object value;

            if (placeholderName) {
                placeholderName = false;
                setPlaceholderName = true;
                parent.Name = name;
            }

            var c = GetNextNonWhitespaceChar();
            switch (c) {
                case '{':
                    value = DeserializeInternal();
                    break;
                
                case '\"':
                    value = ReadNextQuotedString();
                    break;
                
                case '}':
                    goto BreakLoop;

                default:
                    Console.WriteLine("Full text: '" + Text + "'");
                    Console.WriteLine("Last 5 chars: '" + AllChars[(index-5)..(index)].ToString() + "'");
                    Console.WriteLine("Next 5 chars: '" + CurrentChars[0..5].ToString() + "'");
                    throw new Exception($"Unhandled char in KV text: '" + c + "' at index " + index + " while deserializing named object: '" + parent.Name + "'");
		    }

            if (value is KVObject asKV) {
                deserialized = KVObject.Create(name, asKV.Value);
            } else {
                deserialized = KVObject.Create(name, value);
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

        BreakLoop:
        return parent;
    }

    private char GetNextNonWhitespaceChar() {
        SkipWhiteSpace();

        if (HasReachedEnd) {
            return '}';
        }
        
        return CurrentChars[0];
    }

    private void SkipWhiteSpace() {
        while (true)
        {
            if (HasReachedEnd) {
                break;
            }

            if (char.IsWhiteSpace(CurrentChars[0])) {
                index++;
            } else {
                break;
            }
        }
    }

    private bool TryPeekNextNonWhitespaceChar(out char c, out int i, int extra = 0) {
        c = char.MinValue;
        i = 0;

        int localIndex = index + extra;
        bool HasReachedEndLocal() {
            return localIndex >= Text.Length;
        }

        ReadOnlySpan<char> CurrentCharsLocal() {
            return Text.AsSpan(localIndex);
        }

        void SkipWhiteSpaceLocal() {
            while (true)
            {
                if (HasReachedEndLocal()) {
                    break;
                }

                if (char.IsWhiteSpace(CurrentCharsLocal()[0])) {
                    localIndex++;
                } else {
                    break;
                }
            }
        }

        SkipWhiteSpaceLocal();

        if (HasReachedEndLocal()) {
            return false;
        }

        c = CurrentCharsLocal()[0];
        return true;
    }

    private readonly List<char> escapeChars = ['\\'];
    private string ReadNextQuotedString() {
        StringBuilder sb = new();

        SkipWhiteSpace();

        bool foundStart = false;
        while (true) {
            if (this.HasReachedEnd) {
                break;
            }

            char currentChar = Next();

            // Start of string, " literal
            if (currentChar == '\"') {
                if (foundStart) {
                    break;
                } else {
                    foundStart = true;
                    continue;
                }
            }

            if (!foundStart) {
                continue;
            }

            if (escapeChars.Contains(currentChar)) {
                var next = Peek();
                
                // Console.WriteLine("prev: " + AllChars[index - 2]);
                // Console.WriteLine("current: " + currentChar);
                // Console.WriteLine("next: " + next);

                // Add allowed escapable chars here
                if (next == '"')
                {
                    if (TryPeekNextNonWhitespaceChar(out char peeked, out _, 1)) {
                        Console.WriteLine("Got peeked char " + peeked);
                        if (peeked == '}') {
                            Console.WriteLine("Detected abrupt end of string with escape '}' at the end of the string");
                            sb.Append(currentChar);
                        } else if (peeked == '"') {
                            sb.Append(Next());
                        } else {
                            sb.Append(Next());
                            //sb.Append(peeked);
                        }
                    } else {
                        // Console.WriteLine("Failed peek");
                        sb.Append(Next());
                    }
                } else if (next == '\\') {
                    sb.Append(Next());
                } else {
                    // Console.WriteLine("Not escapable char: " + next);
                    sb.Append(currentChar);
                }
               
                continue;
            } else {
                sb.Append(currentChar);
            }
        }

        // Console.WriteLine("Final string:");
        // Console.WriteLine(sb.ToString());
        return sb.ToString();
    }

    private char Next() {
        if (WillEnd()) {
            throw new EndOfStreamException();
        }

        var c = CurrentChars[0];
        index++;
        return c;
    }

    private char Peek(int extra = 0) {
        if (WillEnd(extra)) {
            return char.MinValue;
        }

        return CurrentChars[0 + extra];
    }

    private bool WillEnd(int extra = 0) {
        return index + extra >= Text.Length;
    }
}