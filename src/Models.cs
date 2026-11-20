using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Web.Script.Serialization;

namespace MpcGrabber
{
    internal class FormatOption
    {
        public string Title = "";
        public string Sub = "";
        public string VideoFmt = "bv*+ba/b";
        public int AudioBitrate = 0;      // 0 = video
        public bool IsAudio { get { return AudioBitrate > 0; } }
        public string Badge = null;
    }

    internal class PlaylistEntry
    {
        public string Title = "";
        public string Url = "";
        public double Duration = 0;
        public bool Checked = true;
    }

    internal class MediaInfo
    {
        public bool IsPlaylist = false;
        public string Title = "";
        public string Uploader = "";
        public string Url = "";
        public string ThumbnailUrl = "";
        public double Duration = 0;
        public long ViewCount = 0;
        public List<PlaylistEntry> Entries = new List<PlaylistEntry>();
        public List<FormatOption> Formats = new List<FormatOption>();
    }

    public enum JobState { Queued, Fetching, Downloading, Converting, Done, Failed, Cancelled }

    internal class DownloadJob
    {
        public string Title = "";
        public string Url = "";
        public string QualityLabel = "";
        public string VideoFmt = "bv*+ba/b";
        public int AudioBitrate = 0;
        public JobState State = JobState.Queued;
        public double Progress = 0;
        public string Speed = "";
        public string Eta = "";
        public string Status = "Queued";
        public string OutputPath = "";
        public string Error = "";
        public DateTime StartedAt = DateTime.Now;
    }

    internal class HistoryEntry
    {
        public string Title { get; set; }
        public string Url { get; set; }
        public string FilePath { get; set; }
        public string Kind { get; set; }        // "MP4" or "MP3"
        public string Quality { get; set; }
        public string FinishedAt { get; set; }  // ISO-ish local string
        public long SizeBytes { get; set; }
    }

    internal class Settings
    {
        public string DownloadFolder { get; set; }
        public int AudioBitrate { get; set; }
        public Settings() { AudioBitrate = 320; }
    }

    internal static class J
    {
        public static Dictionary<string, object> AsDict(object o)
        {
            return o as Dictionary<string, object>;
        }

        public static object Get(Dictionary<string, object> d, string key)
        {
            if (d == null) return null;
            object v;
            return d.TryGetValue(key, out v) ? v : null;
        }

        public static string Str(Dictionary<string, object> d, string key)
        {
            object v = Get(d, key);
            return v == null ? "" : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static double Num(Dictionary<string, object> d, string key)
        {
            object v = Get(d, key);
            if (v == null) return 0;
            double r;
            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out r)) return r;
            return 0;
        }

        public static List<object> Arr(Dictionary<string, object> d, string key)
        {
            object v = Get(d, key);
            List<object> list = v as List<object>;
            if (list != null) return list;
            ArrayList al = v as ArrayList;
            if (al != null)
            {
                List<object> r = new List<object>();
                foreach (object x in al) r.Add(x);
                return r;
            }
            return new List<object>();
        }

        public static string ToJson(object o)
        {
            return new JavaScriptSerializer().Serialize(o);
        }

        public static T FromJson<T>(string s)
        {
            return new JavaScriptSerializer().Deserialize<T>(s);
        }
    }
}
