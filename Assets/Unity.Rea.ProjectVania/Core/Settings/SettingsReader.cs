using System;
using System.Collections.Generic;
using System.Globalization;

namespace Unity.Rea.ProjectVania.Core.Settings
{
    /// <summary>A two-number figure from a data file: a position, a size or a rectangle's corner.</summary>
    public readonly struct Pair
    {
        public Pair(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public int XInt => (int)X;
        public int YInt => (int)Y;
    }

    /// <summary>
    /// Reads one JSON object of a data file and validates it as it goes. Every key must be present, of the right
    /// type and in range; a key nobody asked for is an error too (a typo would otherwise be silently ignored).
    /// Keys that start with an underscore are notes for people ("_source") and are skipped. Problems are collected,
    /// not thrown, so one load reports all of them, each as "file: 'key' ... (line n)". A value that fails gives back
    /// a fallback so reading can go on.
    /// </summary>
    public sealed class SettingsReader
    {
        private readonly JsonValue _object;
        private readonly string _path;
        private readonly List<string> _errors;
        private readonly HashSet<string> _read = new HashSet<string>();

        internal SettingsReader(JsonValue value, string path, List<string> errors)
        {
            _object = value;
            _path = path;
            _errors = errors;
        }

        /// <summary>The nested object <paramref name="key"/>, read by <paramref name="read"/> and checked for unknown keys.</summary>
        public T Section<T>(string key, Func<SettingsReader, T> read)
        {
            var value = Take(key, JsonKind.Object, "an object");
            var reader = new SettingsReader(value, $"{_path}.{key}", _errors);
            var data = read(reader);
            reader.Done();
            return data;
        }

        /// <summary>Every object of the array <paramref name="key"/>, each read by <paramref name="read"/> and checked for unknown keys.</summary>
        public List<T> Each<T>(string key, Func<SettingsReader, T> read, bool allowEmpty = false)
        {
            var list = new List<T>();
            var value = Take(key, JsonKind.Array, "an array of objects");
            if (value == null)
            {
                return list;
            }

            if (value.Items.Count == 0 && !allowEmpty)
            {
                Fail(key, value, "must not be empty");
            }

            for (var i = 0; i < value.Items.Count; i++)
            {
                var item = value.Items[i];
                if (item.Kind != JsonKind.Object)
                {
                    Fail(key, item, $"item {i} must be an object (is {Describe(item)})");
                    continue;
                }

                var reader = new SettingsReader(item, $"{_path}.{key}[{i}]", _errors);
                list.Add(read(reader));
                reader.Done();
            }

            return list;
        }

        /// <summary>A finite number in [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public float Number(string key, float min = float.MinValue, float max = float.MaxValue)
        {
            var value = Take(key, JsonKind.Number, "a number");
            return value == null ? 0f : CheckNumber(key, value, min, max);
        }

        /// <summary>A number strictly greater than <paramref name="above"/>.</summary>
        public float Positive(string key, float above = 0f)
        {
            var value = Take(key, JsonKind.Number, "a number");
            if (value == null)
            {
                return 0f;
            }

            var number = CheckNumber(key, value, float.MinValue, float.MaxValue);
            if (!(number > above))
            {
                Fail(key, value, $"must be greater than {Show(above)} (is {Show(number)})");
            }

            return number;
        }

        /// <summary>A whole number in [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public int Integer(string key, int min = int.MinValue, int max = int.MaxValue)
        {
            var value = Take(key, JsonKind.Number, "a whole number");
            return value == null ? Fallback(min, max) : CheckInteger(key, value, min, max);
        }

        public bool Flag(string key)
        {
            var value = Take(key, JsonKind.Bool, "true or false");
            return value != null && value.Bool;
        }

        /// <summary>A string; empty only when <paramref name="allowEmpty"/>.</summary>
        public string Text(string key, bool allowEmpty = false)
        {
            var value = Take(key, JsonKind.String, "text");
            if (value == null)
            {
                return string.Empty;
            }

            if (!allowEmpty && value.Text.Trim().Length == 0)
            {
                Fail(key, value, "must not be empty");
            }

            return value.Text;
        }

        /// <summary>
        /// An array of strings, not empty unless <paramref name="allowEmpty"/>; each entry must hold more than spaces
        /// unless <paramref name="blankEntries"/>.
        /// </summary>
        public List<string> Texts(string key, bool allowEmpty = false, bool blankEntries = false)
        {
            var list = new List<string>();
            var value = Take(key, JsonKind.Array, "an array of text");
            if (value == null)
            {
                return list;
            }

            if (value.Items.Count == 0 && !allowEmpty)
            {
                Fail(key, value, "must not be empty");
            }

            foreach (var item in value.Items)
            {
                if (item.Kind != JsonKind.String)
                {
                    Fail(key, item, $"must hold only text (has {Describe(item)})");
                }
                else if (!blankEntries && item.Text.Trim().Length == 0)
                {
                    Fail(key, item, "must not hold an empty entry");
                }
                else
                {
                    list.Add(item.Text);
                }
            }

            return list;
        }

        /// <summary>An array of whole numbers in [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public List<int> Integers(string key, int min = int.MinValue, int max = int.MaxValue)
        {
            var list = new List<int>();
            var value = Take(key, JsonKind.Array, "an array of whole numbers");
            if (value == null)
            {
                return list;
            }

            foreach (var item in value.Items)
            {
                if (item.Kind != JsonKind.Number)
                {
                    Fail(key, item, $"must hold only whole numbers (has {Describe(item)})");
                    continue;
                }

                list.Add(CheckInteger(key, item, min, max));
            }

            return list;
        }

        /// <summary>A two-number array [x, y], each in [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public Pair Pair(string key, float min = float.MinValue, float max = float.MaxValue)
        {
            var value = Take(key, JsonKind.Array, "an [x, y] pair");
            return value == null ? default : ReadPair(key, value, min, max);
        }

        /// <summary>An array of [x, y] pairs.</summary>
        public List<Pair> Pairs(string key, float min = float.MinValue, float max = float.MaxValue)
        {
            var list = new List<Pair>();
            var value = Take(key, JsonKind.Array, "an array of [x, y] pairs");
            if (value == null)
            {
                return list;
            }

            foreach (var item in value.Items)
            {
                list.Add(ReadPair(key, item, min, max));
            }

            return list;
        }

        /// <summary>A four-number array (a rectangle or corners), each in [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public float[] Quad(string key, float min = float.MinValue, float max = float.MaxValue)
        {
            var result = new float[4];
            var value = Take(key, JsonKind.Array, "an array of four numbers");
            if (value == null)
            {
                return result;
            }

            if (value.Items.Count != 4)
            {
                Fail(key, value, "must hold exactly four numbers");
                return result;
            }

            for (var i = 0; i < 4; i++)
            {
                if (value.Items[i].Kind != JsonKind.Number)
                {
                    Fail(key, value.Items[i], "must hold only numbers");
                    return result;
                }

                result[i] = CheckNumber(key, value.Items[i], min, max);
            }

            return result;
        }

        /// <summary>One of <typeparamref name="T"/>'s names, written as in the code ("LoosePlayer").</summary>
        public T Choice<T>(string key) where T : struct
        {
            var value = Take(key, JsonKind.String, "a name");
            return value == null ? default : ParseChoice<T>(key, value);
        }

        /// <summary>An array of <typeparamref name="T"/>'s names.</summary>
        public List<T> Choices<T>(string key) where T : struct
        {
            var list = new List<T>();
            var value = Take(key, JsonKind.Array, "an array of names");
            if (value == null)
            {
                return list;
            }

            foreach (var item in value.Items)
            {
                if (item.Kind != JsonKind.String)
                {
                    Fail(key, item, $"must hold only names (has {Describe(item)})");
                    continue;
                }

                list.Add(ParseChoice<T>(key, item));
            }

            return list;
        }

        /// <summary>Whether this object has <paramref name="key"/> at all, for the few keys that pick between two forms of an entry.</summary>
        public bool Has(string key) => _object?.Get(key) != null;

        /// <summary>A cross-field check: records <paramref name="message"/> against <paramref name="key"/> when <paramref name="ok"/> is false.</summary>
        public void Check(bool ok, string key, string message)
        {
            if (!ok)
            {
                Fail(key, _object?.Get(key), message);
            }
        }

        /// <summary>Reports every key of this object that nothing read. Called once its keys have all been read.</summary>
        internal void Done()
        {
            if (_object == null)
            {
                return;
            }

            foreach (var member in _object.Members)
            {
                if (!_read.Contains(member.Key) && !member.Key.StartsWith("_", StringComparison.Ordinal))
                {
                    _errors.Add($"{_path}: unknown key '{member.Key}' (line {member.Value.Line}); check its spelling");
                }
            }
        }

        private float CheckNumber(string key, JsonValue value, float min, float max)
        {
            var number = value.Number;
            if (double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number) > float.MaxValue)
            {
                Fail(key, value, $"must be a finite number (is {Show(number)})");
                return 0f;
            }

            if (number < min || number > max)
            {
                Fail(key, value, $"must be {Range(min, max)} (is {Show(number)})");
                return (float)Math.Min(Math.Max(number, min), max);
            }

            return (float)number;
        }

        private int CheckInteger(string key, JsonValue value, int min, int max)
        {
            var number = value.Number;
            if (Math.Floor(number) != number || number < int.MinValue || number > int.MaxValue)
            {
                Fail(key, value, $"must be a whole number (is {Show(number)})");
                return Fallback(min, max);
            }

            if (number < min || number > max)
            {
                Fail(key, value, $"must be {Range(min, max)} (is {Show(number)})");
                return (int)Math.Min(Math.Max(number, min), max);
            }

            return (int)number;
        }

        private Pair ReadPair(string key, JsonValue value, float min, float max)
        {
            if (value.Kind != JsonKind.Array || value.Items.Count != 2
                || value.Items[0].Kind != JsonKind.Number || value.Items[1].Kind != JsonKind.Number)
            {
                Fail(key, value, "must be an [x, y] pair of two numbers");
                return default;
            }

            var x = CheckNumber(key, value.Items[0], min, max);
            var y = CheckNumber(key, value.Items[1], min, max);
            return new Pair(x, y);
        }

        private T ParseChoice<T>(string key, JsonValue value) where T : struct
        {
            if (Enum.TryParse<T>(value.Text, false, out var parsed) && Enum.IsDefined(typeof(T), parsed))
            {
                return parsed;
            }

            Fail(key, value, $"must be one of {string.Join(", ", Enum.GetNames(typeof(T)))} (is \"{value.Text}\")");
            return default;
        }

        private JsonValue Take(string key, JsonKind kind, string what)
        {
            _read.Add(key);
            if (_object == null)
            {
                return null; // the enclosing object is missing, already reported
            }

            var value = _object.Get(key);
            if (value == null)
            {
                _errors.Add($"{_path}: '{key}' is missing (expected {what})");
                return null;
            }

            if (value.Kind != kind)
            {
                Fail(key, value, $"must be {what} (is {Describe(value)})");
                return null;
            }

            return value;
        }

        private void Fail(string key, JsonValue value, string message)
        {
            var where = value != null ? $" (line {value.Line})" : string.Empty;
            _errors.Add($"{_path}: '{key}' {message}{where}");
        }

        private static int Fallback(int min, int max) => Math.Min(Math.Max(0, min), max);

        private static string Describe(JsonValue value)
        {
            switch (value.Kind)
            {
                case JsonKind.Null: return "null";
                case JsonKind.Bool: return value.Bool ? "true" : "false";
                case JsonKind.Number: return Show(value.Number);
                case JsonKind.String: return $"\"{value.Text}\"";
                case JsonKind.Array: return "an array";
                default: return "an object";
            }
        }

        private static string Range(double min, double max)
        {
            var hasMin = min > float.MinValue && min > int.MinValue;
            var hasMax = max < float.MaxValue && max < int.MaxValue;
            if (hasMin && hasMax)
            {
                return $"between {Show(min)} and {Show(max)}";
            }

            return hasMin ? $"at least {Show(min)}" : $"at most {Show(max)}";
        }

        private static string Show(double number) => number.ToString("G6", CultureInfo.InvariantCulture);
    }
}
