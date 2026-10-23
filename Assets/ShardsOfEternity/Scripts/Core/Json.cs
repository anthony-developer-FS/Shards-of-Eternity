using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ShardsOfEternity.Core
{
    /// <summary>
    /// A small JSON reader and writer for the save file, so Core stays free of engine and package references.
    /// Objects read as Dictionary&lt;string, object&gt;, arrays as List&lt;object&gt;, numbers as double.
    /// </summary>
    public static class Json
    {
        public static object Parse(string text)
        {
            var r = new Reader(text ?? "");
            r.SkipSpace();
            var value = r.Value();
            r.SkipSpace();
            if (!r.AtEnd) throw r.Error("unexpected text after the value");
            return value;
        }

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0);
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object value, int indent)
        {
            switch (value)
            {
                case null: sb.Append("null"); break;
                case bool b: sb.Append(b ? "true" : "false"); break;
                case string s: WriteString(sb, s); break;
                case int i: sb.Append(i.ToString(CultureInfo.InvariantCulture)); break;
                case double d: sb.Append(d.ToString("R", CultureInfo.InvariantCulture)); break;
                case IDictionary<string, object> obj:
                    if (obj.Count == 0) { sb.Append("{}"); break; }
                    sb.Append('{');
                    bool first = true;
                    foreach (var kv in obj)
                    {
                        if (!first) sb.Append(',');
                        first = false;
                        NewLine(sb, indent + 1);
                        WriteString(sb, kv.Key);
                        sb.Append(": ");
                        WriteValue(sb, kv.Value, indent + 1);
                    }
                    NewLine(sb, indent);
                    sb.Append('}');
                    break;
                case System.Collections.IEnumerable list:
                    sb.Append('[');
                    bool any = false;
                    foreach (var item in list)
                    {
                        if (any) sb.Append(',');
                        any = true;
                        NewLine(sb, indent + 1);
                        WriteValue(sb, item, indent + 1);
                    }
                    if (any) NewLine(sb, indent);
                    sb.Append(']');
                    break;
                default: throw new ArgumentException($"Can't write {value.GetType().Name} as JSON.");
            }
        }

        static void NewLine(StringBuilder sb, int indent)
        {
            sb.Append('\n');
            sb.Append(' ', indent * 2);
        }

        static void WriteString(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        sealed class Reader
        {
            readonly string s;
            int i;

            public Reader(string s) => this.s = s;

            public bool AtEnd => i >= s.Length;

            public FormatException Error(string what) => new FormatException($"Bad JSON at {i}: {what}.");

            public void SkipSpace()
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            char Peek => i < s.Length ? s[i] : '\0';

            void Expect(char c)
            {
                if (Peek != c) throw Error($"expected '{c}'");
                i++;
            }

            public object Value()
            {
                switch (Peek)
                {
                    case '{': return Object();
                    case '[': return Array();
                    case '"': return String();
                    case 't': Word("true"); return true;
                    case 'f': Word("false"); return false;
                    case 'n': Word("null"); return null;
                    default:
                        if (Peek == '-' || char.IsDigit(Peek)) return Number();
                        throw Error("expected a value");
                }
            }

            void Word(string w)
            {
                if (string.CompareOrdinal(s, i, w, 0, w.Length) != 0) throw Error($"expected {w}");
                i += w.Length;
            }

            Dictionary<string, object> Object()
            {
                var obj = new Dictionary<string, object>();
                Expect('{');
                SkipSpace();
                if (Peek == '}') { i++; return obj; }
                while (true)
                {
                    SkipSpace();
                    string key = String();
                    SkipSpace();
                    Expect(':');
                    SkipSpace();
                    obj[key] = Value();
                    SkipSpace();
                    if (Peek == ',') { i++; continue; }
                    Expect('}');
                    return obj;
                }
            }

            List<object> Array()
            {
                var list = new List<object>();
                Expect('[');
                SkipSpace();
                if (Peek == ']') { i++; return list; }
                while (true)
                {
                    SkipSpace();
                    list.Add(Value());
                    SkipSpace();
                    if (Peek == ',') { i++; continue; }
                    Expect(']');
                    return list;
                }
            }

            string String()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd) throw Error("unterminated string");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (AtEnd) throw Error("unterminated string");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw Error("short \\u escape");
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw Error($"unknown escape \\{e}");
                    }
                }
            }

            double Number()
            {
                int start = i;
                if (Peek == '-') i++;
                while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.' || s[i] == 'e' || s[i] == 'E' || s[i] == '+' || s[i] == '-')) i++;
                if (!double.TryParse(s.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                    throw Error("bad number");
                return d;
            }
        }
    }
}
