using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MpcGrabber
{
    internal class ProcResult
    {
        public int Code;
        public string Out = "";
        public string Err = "";
        public string All
        {
            get { return (Out + "\n" + Err).Trim(); }
        }
    }

    internal static class YtDlp
    {
        public static string Dir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static string ExePath
        {
            get
            {
                string p = Path.Combine(Dir, "yt-dlp.exe");
                return File.Exists(p) ? p : "yt-dlp.exe";
            }
        }

        public static bool HasFfmpeg
        {
            get { return File.Exists(Path.Combine(Dir, "ffmpeg.exe")); }
        }

        public static string FindCookies()
        {
            try
            {
                string exact = Path.Combine(Dir, "cookies.txt");
                if (File.Exists(exact)) return exact;
                string[] files = Directory.GetFiles(Dir, "cookies*.txt");
                if (files.Length > 0)
                {
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    return files[0];
                }
            }
            catch { }
            return null;
        }

        public static string Quote(string s)
        {
            if (s == null) s = "";
            if (s.Length == 0) return "\"\"";
            bool needsQuotes = s.IndexOf(' ') >= 0 || s.IndexOf('\t') >= 0 || s.IndexOf('"') >= 0 || s.EndsWith("\\");
            if (!needsQuotes) return s;
            StringBuilder sb = new StringBuilder("\"");
            int backslashes = 0;
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\') { backslashes++; continue; }
                if (c == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                }
                else
                {
                    sb.Append('\\', backslashes);
                    sb.Append(c);
                }
                backslashes = 0;
            }
            if (backslashes > 0) sb.Append('\\', backslashes * 2);
            sb.Append('"');
            return sb.ToString();
        }

        public static string CommonArgs()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("--no-warnings ");
            string cookies = FindCookies();
            if (cookies != null) sb.Append("--cookies ").Append(Quote(cookies)).Append(' ');
            if (HasFfmpeg) sb.Append("--ffmpeg-location ").Append(Quote(Dir)).Append(' ');
            return sb.ToString();
        }

        public static Task<ProcResult> RunAsync(string args, Action<string> onLine)
        {
            TaskCompletionSource<ProcResult> tcs = new TaskCompletionSource<ProcResult>();
            Process p = new Process();
            p.StartInfo = new ProcessStartInfo(ExePath, args);
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            p.StartInfo.WorkingDirectory = Dir;

            StringBuilder sbOut = new StringBuilder();
            StringBuilder sbErr = new StringBuilder();
            object gate = new object();

            p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (gate) sbOut.AppendLine(e.Data);
                if (onLine != null) { try { onLine(e.Data); } catch { } }
            };
            p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (gate) sbErr.AppendLine(e.Data);
            };
            p.EnableRaisingEvents = true;
            p.Exited += delegate
            {
                try { p.WaitForExit(); } catch { }
                ProcResult r = new ProcResult();
                lock (gate) { r.Out = sbOut.ToString(); r.Err = sbErr.ToString(); }
                try { r.Code = p.ExitCode; } catch { r.Code = -1; }
                tcs.TrySetResult(r);
                try { p.Dispose(); } catch { }
            };

            try
            {
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            return tcs.Task;
        }

        public static async Task<string> VersionAsync()
        {
            try
            {
                ProcResult r = await RunAsync("--version", null);
                return r.Out.Trim();
            }
            catch { return ""; }
        }

        public static List<FormatOption> GenericFormats()
        {
            List<FormatOption> list = new List<FormatOption>();
            list.Add(new FormatOption { Title = "Best", Sub = "highest available", VideoFmt = "bv*+ba/b", Badge = "BEST" });
            int[] hs = { 2160, 1440, 1080, 720, 480, 360 };
            for (int i = 0; i < hs.Length; i++)
            {
                int h = hs[i];
                list.Add(new FormatOption
                {
                    Title = h + "p",
                    Sub = h >= 2160 ? "up to 4K" : "up to " + h + "p",
                    VideoFmt = "bv*[height<=" + h + "]+ba/b[height<=" + h + "]/b"
                });
            }
            AddAudio(list);
            return list;
        }

        public static void AddAudio(List<FormatOption> list)
        {
            int[] rates = { 320, 192, 128 };
            for (int i = 0; i < rates.Length; i++)
            {
                int r = rates[i];
                list.Add(new FormatOption { Title = "MP3 " + r, Sub = "audio only", AudioBitrate = r });
            }
        }

        public static void Log(string message)
        {
            try
            {
                File.AppendAllText(Path.Combine(Dir, "mpcgrabber.log"),
                    DateTime.Now.ToString("s") + "  " + message + Environment.NewLine);
            }
            catch { }
        }

        public static async Task<MediaInfo> FetchAsync(string url)
        {
            string args = "-J --flat-playlist --skip-download " + CommonArgs() + " " + Quote(url);
            ProcResult r = await RunAsync(args, null);
            if (r.Code != 0 || string.IsNullOrWhiteSpace(r.Out))
            {
                string so = r.Out.Length > 600 ? r.Out.Substring(0, 600) : r.Out;
                string se = r.Err.Length > 600 ? r.Err.Substring(0, 600) : r.Err;
                Log("FETCH FAILED exit=" + r.Code + "\r\nARGS: " + args + "\r\nOUT: " + so + "\r\nERR: " + se);
                string err = r.Err.Trim();
                if (err.Length == 0) err = r.Out.Trim();
                if (err.Length > 400) err = err.Substring(err.Length - 400);
                throw new Exception(err.Length > 0 ? err : "yt-dlp could not read that URL.");
            }

            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = int.MaxValue;
            js.RecursionLimit = 200;
            Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(r.Out);
            if (d == null) throw new Exception("Unexpected response from yt-dlp.");

            MediaInfo info = new MediaInfo();
            info.Title = J.Str(d, "title");
            info.Uploader = J.Str(d, "uploader");
            if (info.Uploader.Length == 0) info.Uploader = J.Str(d, "channel");
            info.Duration = J.Num(d, "duration");
            info.ViewCount = (long)J.Num(d, "view_count");
            info.Url = url;
            info.ThumbnailUrl = J.Str(d, "thumbnail");

            string type = J.Str(d, "_type");
            if (type == "playlist" || type == "multi_video")
            {
                info.IsPlaylist = true;
                List<object> entries = J.Arr(d, "entries");
                foreach (object o in entries)
                {
                    Dictionary<string, object> ed = J.AsDict(o);
                    if (ed == null) continue;
                    PlaylistEntry pe = new PlaylistEntry();
                    pe.Title = J.Str(ed, "title");
                    pe.Duration = J.Num(ed, "duration");
                    pe.Url = J.Str(ed, "url");
                    if (pe.Url.Length == 0) pe.Url = J.Str(ed, "webpage_url");
                    string id = J.Str(ed, "id");
                    if (pe.Url.Length == 0 && id.Length > 0) pe.Url = "https://www.youtube.com/watch?v=" + id;
                    if (pe.Title.Length == 0) pe.Title = pe.Url;
                    if (pe.Url.Length > 0) info.Entries.Add(pe);
                }
                if (info.Entries.Count == 0) throw new Exception("That playlist appears to be empty.");
                info.Formats = GenericFormats();
            }
            else
            {
                info.Formats = BuildFormats(d);
            }
            return info;
        }

        private static List<FormatOption> BuildFormats(Dictionary<string, object> d)
        {
            List<FormatOption> list = new List<FormatOption>();
            list.Add(new FormatOption { Title = "Best", Sub = "highest available", VideoFmt = "bv*+ba/b", Badge = "BEST" });

            List<object> formats = J.Arr(d, "formats");
            Dictionary<int, double> videoSize = new Dictionary<int, double>();
            Dictionary<int, double> fps = new Dictionary<int, double>();
            double audioSize = 0;

            foreach (object o in formats)
            {
                Dictionary<string, object> f = J.AsDict(o);
                if (f == null) continue;
                string vcodec = J.Str(f, "vcodec");
                string acodec = J.Str(f, "acodec");
                double size = J.Num(f, "filesize");
                if (size <= 0) size = J.Num(f, "filesize_approx");

                if (vcodec == "none")
                {
                    if (acodec != "none" && size > audioSize) audioSize = size;
                    continue;
                }
                int h = (int)J.Num(f, "height");
                if (h <= 0) continue;
                double cur;
                if (!videoSize.TryGetValue(h, out cur) || size > cur) videoSize[h] = size;
                double fr = J.Num(f, "fps");
                if (fr > 0) fps[h] = fr;
            }

            if (videoSize.Count == 0) { AddAudio(list); return list; }

            List<int> heights = new List<int>(videoSize.Keys);
            heights.Sort();
            heights.Reverse();
            int max = Math.Min(6, heights.Count);

            for (int i = 0; i < max; i++)
            {
                int h = heights[i];
                FormatOption fo = new FormatOption();
                fo.Title = h + "p";
                double fr;
                double total = videoSize[h] + audioSize;
                string sizeText = total > 0 ? "~" + T.FormatBytes(total) : (h >= 2160 ? "up to 4K" : "mp4");
                if (fps.TryGetValue(h, out fr) && fr >= 50) sizeText += " \u00B7 60fps";
                fo.Sub = sizeText;
                fo.VideoFmt = "bv*[height=" + h + "]+ba/b[height=" + h + "]/b";
                list.Add(fo);
            }
            AddAudio(list);
            return list;
        }
    }
}
