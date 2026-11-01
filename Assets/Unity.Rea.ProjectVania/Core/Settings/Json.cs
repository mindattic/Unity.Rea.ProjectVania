using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Unity.Rea.ProjectVania.Core.Settings
{
    /// <summary>The kinds of value a JSON document holds.</summary>
    public enum JsonKind
    {
        Null,
        Bool,
        Number,
        String,
        Array,
        Object,
    }

    /// <summary>
    /// One value of a parsed JSON document, with the line it started on so a settings error can say where.
    /// Objects keep their keys in document order. Core has no JSON library it can rely on in Unity
    /// (no System.Text.Json under .NET Standard 2.1, and Core takes no packages), so it parses its own
    /// data files with <see cref="Json.Parse"/>.
    /// </summary>
    public sealed class JsonValue
    {
        private readonly List<KeyValuePair<string, JsonValue>> _members;
        private readonly List<JsonValue> _items;

        private JsonValue(JsonKind kind, int line, bool flag = false, double number = 0d, string text = null,
            List<JsonValue> items = null, List<KeyValuePair<string, JsonValue>> members = null)
        {
            Kind = kind;
            Line = line;
            Bool = flag;
            Number = number;
            Text = text;
            _items = items;
            _members = members;
        }

        public JsonKind Kind { get; }

        /// <summary>The 1-based line the value starts on.</summary>
        public int Line { get; }

        public bool Bool { get; }
        public double Number { get; }
        public string Text { get; }

        public IReadOnlyList<JsonValue> Items => _items;
        public IReadOnlyList<KeyValuePair<string, JsonValue>> Members => _members;

        /// <summary>The member called <paramref name="key"/>, or null.</summary>
        public JsonValue Get(string key)
        {
            if (_members == null)
            {
                return null;
            }

            foreach (var member in _members)
            {
                if (member.Key == key)
                {
                    return member.Value;
                }
            }

            return null;
        }

        internal static JsonValue Null(int line) => new JsonValue(JsonKind.Null, line);
        internal static JsonValue Of(bool value, int line) => new JsonValue(JsonKind.Bool, line, flag: value);
        internal static JsonValue Of(double value, int line) => new JsonValue(JsonKind.Number, line, number: value);
        internal static JsonValue Of(string value, int line) => new JsonValue(JsonKind.String, line, text: value);
        internal static JsonValue Array(List<JsonValue> items, int line) => new JsonValue(JsonKind.Array, line, items: items);

        internal static JsonValue Object(List<KeyValuePair<string, JsonValue>> members, int line) =>
            new JsonValue(JsonKind.Object, line, members: members);
    }

    /// <summary>Thrown by <see cref="Json.Parse"/> for text that is not JSON; the message gives the line and column.</summary>
    public sealed class JsonException : Exception
    {
        public JsonException(string message, int line, int column)
            : base($"line {line}, column {column}: {message}")
        {
            Line = line;
            Column = column;
        }

        public int Line { get; }
        public int Column { get; }
    }

    /// <summary>A strict RFC 8259 JSON parser (no comments, no trailing commas), enough for the settings files.</summary>
    public static class Json
    {
        public static JsonValue Parse(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            var parser = new Parser(text);
            parser.SkipSpace();
            var value = parser.ReadValue();
            parser.SkipSpace();
            if (!parser.AtEnd)
            {
                throw parser.Error("unexpected text after the document");
            }

            return value;
        }

        private sealed class Parser
        {
            private readonly string _text;
            private int _at;
            private int _line = 1;
            private int _lineStart;

            public Parser(string text)
            {
                _text = text;
                // A UTF-8 byte order mark read as text.
                if (_text.Length > 0 && _text[0] == '﻿')
                {
                    _at = 1;
                    _lineStart = 1;
                }
            }

            public bool AtEnd => _at >= _text.Length;

            public JsonException Error(string message) => new JsonException(message, _line, _at - _lineStart + 1);

            public void SkipSpace()
            {
                while (!AtEnd)
                {
                    var c = _text[_at];
                    if (c == '\n')
                    {
                        _at++;
                        _line++;
                        _lineStart = _at;
                    }
                    else if (c == ' ' || c == '\t' || c == '\r')
                    {
                        _at++;
                    }
                    else
                    {
                        return;
                    }
                }
            }

            public JsonValue ReadValue()
            {
                if (AtEnd)
                {
                    throw Error("unexpected end of the document");
                }

                var line = _line;
                var c = _text[_at];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return JsonValue.Of(ReadString(), line);
                    case 't': Expect("true"); return JsonValue.Of(true, line);
                    case 'f': Expect("false"); return JsonValue.Of(false, line);
                    case 'n': Expect("null"); return JsonValue.Null(line);
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                        {
                            return JsonValue.Of(ReadNumber(), line);
                        }

                        throw Error($"unexpected character '{c}'");
                }
            }

            private JsonValue ReadObject()
            {
                var line = _line;
                _at++; // {
                var members = new List<KeyValuePair<string, JsonValue>>();
                SkipSpace();
                if (Peek('}'))
                {
                    _at++;
                    return JsonValue.Object(members, line);
                }

                while (true)
                {
                    SkipSpace();
                    if (!Peek('"'))
                    {
                        throw Error("expected a key in double quotes");
                    }

                    var key = ReadString();
                    foreach (var member in members)
                    {
                        if (member.Key == key)
                        {
                            throw Error($"the key '{key}' appears twice");
                        }
                    }

                    SkipSpace();
                    Consume(':');
                    SkipSpace();
                    members.Add(new KeyValuePair<string, JsonValue>(key, ReadValue()));
                    SkipSpace();
                    if (Peek(','))
                    {
                        _at++;
                        continue;
                    }

                    Consume('}');
                    return JsonValue.Object(members, line);
                }
            }

            private JsonValue ReadArray()
            {
                var line = _line;
                _at++; // [
                var items = new List<JsonValue>();
                SkipSpace();
                if (Peek(']'))
                {
                    _at++;
                    return JsonValue.Array(items, line);
                }

                while (true)
                {
                    SkipSpace();
                    items.Add(ReadValue());
                    SkipSpace();
                    if (Peek(','))
                    {
                        _at++;
                        continue;
                    }

                    Consume(']');
                    return JsonValue.Array(items, line);
                }
            }

            private string ReadString()
            {
                _at++; // opening quote
                var builder = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                    {
                        throw Error("a string is not closed");
                    }

                    var c = _text[_at++];
                    if (c == '"')
                    {
                        return builder.ToString();
                    }

                    if (c == '\n' || c < ' ')
                    {
                        throw Error("a string runs past the end of its line");
                    }

                    if (c != '\\')
                    {
                        builder.Append(c);
                        continue;
                    }

                    if (AtEnd)
                    {
                        throw Error("a string is not closed");
                    }

                    var escape = _text[_at++];
                    switch (escape)
                    {
                        case '"': builder.Append('"'); break;
                        case '\\': builder.Append('\\'); break;
                        case '/': builder.Append('/'); break;
                        case 'b': builder.Append('\b'); break;
                        case 'f': builder.Append('\f'); break;
                        case 'n': builder.Append('\n'); break;
                        case 'r': builder.Append('\r'); break;
                        case 't': builder.Append('\t'); break;
                        case 'u':
                            if (_at + 4 > _text.Length ||
                                !int.TryParse(_text.Substring(_at, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var code))
                            {
                                throw Error("a \\u escape needs four hex digits");
                            }

                            builder.Append((char)code);
                            _at += 4;
                            break;
                        default:
                            throw Error($"unknown escape '\\{escape}'");
                    }
                }
            }

            private double ReadNumber()
            {
                var start = _at;
                if (Peek('-'))
                {
                    _at++;
                }

                if (!Digits())
                {
                    throw Error("a number needs digits");
                }

                if (Peek('.'))
                {
                    _at++;
                    if (!Digits())
                    {
                        throw Error("a number needs digits after its decimal point");
                    }
                }

                if (Peek('e') || Peek('E'))
                {
                    _at++;
                    if (Peek('+') || Peek('-'))
                    {
                        _at++;
                    }

                    if (!Digits())
                    {
                        throw Error("a number needs digits in its exponent");
                    }
                }

                return double.Parse(_text.Substring(start, _at - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            }

            private bool Digits()
            {
                var start = _at;
                while (!AtEnd && _text[_at] >= '0' && _text[_at] <= '9')
                {
                    _at++;
                }

                return _at > start;
            }

            private bool Peek(char c) => !AtEnd && _text[_at] == c;

            private void Consume(char c)
            {
                if (!Peek(c))
                {
                    throw AtEnd ? Error($"expected '{c}' but the document ended") : Error($"expected '{c}' but found '{_text[_at]}'");
                }

                _at++;
            }

            private void Expect(string word)
            {
                if (string.CompareOrdinal(_text, _at, word, 0, word.Length) != 0)
                {
                    throw Error($"unexpected text (expected '{word}')");
                }

                _at += word.Length;
            }
        }
    }
}
