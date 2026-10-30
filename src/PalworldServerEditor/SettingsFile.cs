using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace PalworldServerEditor
{
    /// <summary>
    /// PalWorldSettings.ini in memory. Only the values inside OptionSettings=(...) are touched:
    /// comments, other lines, key order, unknown keys and the original formatting of
    /// untouched values are written back exactly as they were read.
    /// </summary>
    sealed class SettingsFile
    {
        static readonly Regex OptionSettingsStart = new Regex(
            @"^[ \t]*OptionSettings[ \t]*=[ \t]*\(", RegexOptions.Multiline | RegexOptions.IgnoreCase);

        sealed class Entry
        {
            public string Raw;    // item text exactly as read, e.g. "ExpRate=1.000000"
            public string Lead;   // whitespace before the key
            public string Key;    // null when the item has no '='
            public string Value;
            public bool Dirty;
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly Encoding encoding;
        int innerStart, innerEnd;

        public string FilePath { get; }
        public string Text { get; }

        SettingsFile(string path, string text, Encoding encoding)
        {
            FilePath = path;
            Text = text;
            this.encoding = encoding;
            Parse();
        }

        public static SettingsFile Load(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string text = Decode(bytes, out Encoding encoding);
            return new SettingsFile(path, text, encoding);
        }

        public static SettingsFile FromText(string path, string text) => new SettingsFile(path, text, new UTF8Encoding(false));

        /// <summary>True when the file still carries the header of DefaultPalWorldSettings.ini.</summary>
        public bool LooksLikeSample =>
            Text.IndexOf("Changes to this file will NOT be reflected", StringComparison.OrdinalIgnoreCase) >= 0;

        public IEnumerable<KeyValuePair<string, string>> Entries =>
            entries.Where(e => e.Key != null).Select(e => new KeyValuePair<string, string>(e.Key, e.Value));

        public bool TryGet(string key, out string value)
        {
            Entry entry = Find(key);
            value = entry?.Value;
            return entry != null;
        }

        public bool Set(string key, string value)
        {
            Entry entry = Find(key);
            if (entry == null)
                return false;
            if (!IniValue.IsWellFormed(value))
                throw new ArgumentException("Valore non valido per " + key + ": " + value);
            entry.Value = value;
            entry.Dirty = true;
            return true;
        }

        public string Serialize()
        {
            var sb = new StringBuilder(Text.Length + 64);
            sb.Append(Text, 0, innerStart);
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0)
                    sb.Append(',');
                Entry e = entries[i];
                sb.Append(e.Dirty ? e.Lead + e.Key + "=" + e.Value : e.Raw);
            }
            sb.Append(Text, innerEnd, Text.Length - innerEnd);
            return sb.ToString();
        }

        /// <summary>Writes the file with its original encoding and returns the freshly parsed result.</summary>
        public SettingsFile Save()
        {
            string text = Serialize();
            byte[] preamble = encoding.GetPreamble();
            byte[] body = encoding.GetBytes(text);
            var bytes = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);
            File.WriteAllBytes(FilePath, bytes);
            return new SettingsFile(FilePath, text, encoding);
        }

        Entry Find(string key) =>
            entries.FirstOrDefault(e => e.Key != null && string.Equals(e.Key, key, StringComparison.OrdinalIgnoreCase));

        void Parse()
        {
            Match m = OptionSettingsStart.Match(Text);
            if (!m.Success)
                throw new InvalidDataException(
                    "Nel file non c'è la riga OptionSettings=(...).\n\n" +
                    "Se è il PalWorldSettings.ini appena creato dal server (vuoto), copia dentro il contenuto di " +
                    "DefaultPalWorldSettings.ini e riprova.");

            innerStart = m.Index + m.Length;
            int depth = 0, itemStart = innerStart, i = innerStart;
            bool quoted = false;
            var spans = new List<KeyValuePair<int, int>>();
            for (; i < Text.Length; i++)
            {
                char c = Text[i];
                if (c == '\r' || c == '\n')
                    throw new InvalidDataException("La riga OptionSettings non è chiusa correttamente (manca una parentesi o una virgoletta).");
                if (quoted)
                {
                    if (c == '\\' && i + 1 < Text.Length && Text[i + 1] != '\r' && Text[i + 1] != '\n')
                        i++;
                    else if (c == '"')
                        quoted = false;
                    continue;
                }
                if (c == '"')
                    quoted = true;
                else if (c == '(')
                    depth++;
                else if (c == ')')
                {
                    if (depth == 0)
                        break;
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    spans.Add(new KeyValuePair<int, int>(itemStart, i));
                    itemStart = i + 1;
                }
            }
            if (i >= Text.Length)
                throw new InvalidDataException("La riga OptionSettings non è chiusa correttamente (manca la parentesi finale).");
            innerEnd = i;
            spans.Add(new KeyValuePair<int, int>(itemStart, i));

            foreach (var span in spans)
            {
                string raw = Text.Substring(span.Key, span.Value - span.Key);
                var entry = new Entry { Raw = raw };
                int eq = raw.IndexOf('=');
                if (eq > 0)
                {
                    string keyPart = raw.Substring(0, eq);
                    string key = keyPart.Trim();
                    if (key.Length > 0)
                    {
                        entry.Lead = keyPart.Substring(0, keyPart.Length - keyPart.TrimStart().Length);
                        entry.Key = key;
                        entry.Value = raw.Substring(eq + 1).Trim();
                    }
                }
                entries.Add(entry);
            }
        }

        static string Decode(byte[] b, out Encoding encoding)
        {
            if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF)
            {
                encoding = new UTF8Encoding(true);
                return encoding.GetString(b, 3, b.Length - 3);
            }
            if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE)
            {
                encoding = new UnicodeEncoding(false, true);
                return encoding.GetString(b, 2, b.Length - 2);
            }
            if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF)
            {
                encoding = new UnicodeEncoding(true, true);
                return encoding.GetString(b, 2, b.Length - 2);
            }
            try
            {
                string text = new UTF8Encoding(false, true).GetString(b);
                encoding = new UTF8Encoding(false);
                return text;
            }
            catch (DecoderFallbackException)
            {
                // Not UTF-8: keep the Windows ANSI code page so accented letters survive the round trip.
                encoding = Encoding.Default;
                return encoding.GetString(b);
            }
        }
    }

    /// <summary>Helpers for the value syntax used inside OptionSettings (Unreal Engine struct text).</summary>
    static class IniValue
    {
        public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Regex IntPattern = new Regex(@"^[+-]?\d+$");
        static readonly Regex FloatPattern = new Regex(@"^[+-]?\d*\.\d+$");

        public static bool IsQuoted(string raw)
        {
            string t = raw.Trim();
            return t.Length >= 2 && t[0] == '"' && t[t.Length - 1] == '"';
        }

        public static string Unquote(string raw)
        {
            string t = raw.Trim();
            if (!IsQuoted(t))
                return t;
            var sb = new StringBuilder(t.Length);
            for (int i = 1; i < t.Length - 1; i++)
            {
                char c = t[i];
                if (c == '\\' && i + 1 < t.Length - 1 && (t[i + 1] == '"' || t[i + 1] == '\\'))
                    c = t[++i];
                sb.Append(c);
            }
            return sb.ToString();
        }

        public static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

        public static bool IsInt(string raw) => IntPattern.IsMatch(raw.Trim());

        public static bool IsFloat(string raw) => FloatPattern.IsMatch(raw.Trim());

        public static bool TryParseNumber(string raw, out decimal value) =>
            decimal.TryParse(raw.Trim(), NumberStyles.Float, Inv, out value);

        public static bool IsBool(string raw)
        {
            string t = raw.Trim();
            return t.Equals("True", StringComparison.OrdinalIgnoreCase) || t.Equals("False", StringComparison.OrdinalIgnoreCase);
        }

        public static string FormatFloat(decimal d) => d.ToString("0.000000", Inv);

        public static string FormatInt(decimal d) => decimal.Truncate(d).ToString(Inv);

        /// <summary>Number of decimals needed to show the value without losing digits.</summary>
        public static int SignificantDecimals(decimal d)
        {
            string s = d.ToString(Inv);
            int dot = s.IndexOf('.');
            return dot < 0 ? 0 : s.TrimEnd('0').Length - dot - 1;
        }

        /// <summary>
        /// A value can be stored only if it stays a single item: quotes closed, parentheses balanced,
        /// no commas outside parentheses and no line breaks.
        /// </summary>
        public static bool IsWellFormed(string raw)
        {
            int depth = 0;
            bool quoted = false;
            for (int i = 0; i < raw.Length; i++)
            {
                char c = raw[i];
                if (c == '\r' || c == '\n')
                    return false;
                if (quoted)
                {
                    if (c == '\\' && i + 1 < raw.Length)
                        i++;
                    else if (c == '"')
                        quoted = false;
                    continue;
                }
                if (c == '"')
                    quoted = true;
                else if (c == '(')
                    depth++;
                else if (c == ')' && --depth < 0)
                    return false;
                else if (c == ',' && depth == 0)
                    return false;
            }
            return !quoted && depth == 0;
        }

        /// <summary>Items of a parenthesised list such as (Steam,Xbox,PS5,Mac).</summary>
        public static List<string> ParseList(string raw)
        {
            string t = raw.Trim();
            if (t.StartsWith("(") && t.EndsWith(")"))
                t = t.Substring(1, t.Length - 2);
            return t.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }
    }
}
