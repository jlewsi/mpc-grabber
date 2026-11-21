using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MpcGrabber
{
    internal static class Store
    {
        public static string Dir
        {
            get { return AppDomain.CurrentDomain.BaseDirectory; }
        }

        public static Settings Current = new Settings();
        public static List<HistoryEntry> History = new List<HistoryEntry>();

        public static string SettingsPath { get { return Path.Combine(Dir, "mpcgrabber.settings.json"); } }
        public static string HistoryPath { get { return Path.Combine(Dir, "mpcgrabber.history.json"); } }

        public static string DefaultDownloads
        {
            get { return Path.Combine(Dir, "Downloads"); }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    Settings s = J.FromJson<Settings>(File.ReadAllText(SettingsPath, Encoding.UTF8));
                    if (s != null) Current = s;
                }
            }
            catch { Current = new Settings(); }

            if (string.IsNullOrWhiteSpace(Current.DownloadFolder))
                Current.DownloadFolder = DefaultDownloads;
            if (Current.AudioBitrate != 320 && Current.AudioBitrate != 192 && Current.AudioBitrate != 128)
                Current.AudioBitrate = 320;
            if (!IsKnownVideoQuality(Current.VideoQuality))
                Current.VideoQuality = "best";

            try
            {
                if (File.Exists(HistoryPath))
                {
                    List<HistoryEntry> h = J.FromJson<List<HistoryEntry>>(File.ReadAllText(HistoryPath, Encoding.UTF8));
                    if (h != null) History = h;
                }
            }
            catch { History = new List<HistoryEntry>(); }
        }

        public static bool IsKnownVideoQuality(string q)
        {
            if (string.IsNullOrEmpty(q)) return false;
            return q == "best" || q == "2160" || q == "1440" || q == "1080" || q == "720" || q == "480" || q == "360";
        }

        public static void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                File.WriteAllText(SettingsPath, J.ToJson(Current), Encoding.UTF8);
            }
            catch { }
        }

        public static void SaveHistory()
        {
            try
            {
                Directory.CreateDirectory(Dir);
                while (History.Count > 500) History.RemoveAt(History.Count - 1);
                File.WriteAllText(HistoryPath, J.ToJson(History), Encoding.UTF8);
            }
            catch { }
        }

        public static void AddHistory(HistoryEntry e)
        {
            History.Insert(0, e);
            SaveHistory();
        }
    }
}
