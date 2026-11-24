using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MpcGrabber
{
    internal static class TranscriptText
    {
        private static readonly Regex CueIndex = new Regex(@"^\s*\d+\s*$", RegexOptions.Compiled);
        private static readonly Regex CueTime = new Regex(@"^\s*\d{1,2}:\d{2}:\d{2}[,.]\d{1,3}\s*-->", RegexOptions.Compiled);
        private static readonly Regex Tag = new Regex(@"<[^>]*>", RegexOptions.Compiled);
        private static readonly Regex NonWord = new Regex(@"[^\p{L}\p{Nd}]+", RegexOptions.Compiled);
        private static readonly Regex LangSuffix = new Regex(@"\.[A-Za-z]{2,3}(-[A-Za-z0-9]+)?$", RegexOptions.Compiled);

        public static string BaseName(string path)
        {
            string dir = Path.GetDirectoryName(path) ?? "";
            string name = Path.GetFileNameWithoutExtension(path);
            return Path.Combine(dir, LangSuffix.Replace(name, ""));
        }

        public static string ReadPrefix()
        {
            try
            {
                string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "transcript-add.txt");
                if (!File.Exists(p)) return "";
                return File.ReadAllText(p, Encoding.UTF8).Trim();
            }
            catch { return ""; }
        }

        // SRT -> one line of bare words, prefixed with transcript-add.txt when present
        public static string Convert(string srtPath)
        {
            if (string.IsNullOrEmpty(srtPath) || !File.Exists(srtPath)) return "";

            string[] lines = File.ReadAllLines(srtPath, Encoding.UTF8);
            List<string> kept = new List<string>();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Trim().Length == 0) continue;
                if (CueIndex.IsMatch(line)) continue;
                if (CueTime.IsMatch(line)) continue;

                string text = Tag.Replace(line, " ").Trim();
                if (text.Length == 0) continue;
                if (kept.Count > 0 && string.Equals(kept[kept.Count - 1], text, StringComparison.Ordinal)) continue;
                kept.Add(text);
            }

            string words = NonWord.Replace(string.Join(" ", kept.ToArray()), " ").Trim();
            if (words.Length == 0) return "";

            StringBuilder sb = new StringBuilder();
            string prefix = ReadPrefix();
            if (prefix.Length > 0) sb.Append(prefix).Append(Environment.NewLine);
            sb.Append(words);

            string outPath = BaseName(srtPath) + ".txt";
            File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false));
            return outPath;
        }

        public static bool HasSidecar(string basePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(basePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;
                string name = Path.GetFileName(basePath);
                foreach (string f in Directory.GetFiles(dir, name + ".*"))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".srt" || ext == ".vtt" || ext == ".ttml") return true;
                }
            }
            catch { }
            return false;
        }

        public static void DeleteSidecars(string basePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(basePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
                string name = Path.GetFileName(basePath);
                foreach (string f in Directory.GetFiles(dir, name + ".*"))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".srt" || ext == ".vtt" || ext == ".ttml")
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
            catch { }
        }

        public static string NewestSubtitle(string folder, DateTime since)
        {
            string srt = "";
            string other = "";
            try
            {
                if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return "";
                DateTime cutoff = since.AddSeconds(-3);
                foreach (string f in Directory.GetFiles(folder))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext != ".srt" && ext != ".vtt" && ext != ".ttml") continue;
                    if (File.GetLastWriteTime(f) < cutoff) continue;
                    if (ext == ".srt" && srt.Length == 0) srt = f;
                    else if (other.Length == 0) other = f;
                }
            }
            catch { }
            return srt.Length > 0 ? srt : other;
        }
        public static string FindSubtitle(string basePath)
        {
            try
            {
                string dir = Path.GetDirectoryName(basePath);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return "";
                string name = Path.GetFileName(basePath);
                string[] hits = Directory.GetFiles(dir, name + ".*.srt");
                if (hits.Length > 0)
                {
                    Array.Sort(hits, StringComparer.OrdinalIgnoreCase);
                    return hits[0];
                }
            }
            catch { }
            return "";
        }
    }
}
