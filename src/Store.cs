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
        public static List<SavedChannel> Channels = new List<SavedChannel>();

        public static string SettingsPath { get { return Path.Combine(Dir, "mpcgrabber.settings.json"); } }
        public static string HistoryPath { get { return Path.Combine(Dir, "mpcgrabber.history.json"); } }
        public static string ChannelsPath { get { return Path.Combine(Dir, "mpcgrabber.channels.json"); } }

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
            if (Current.ChannelDays != 1 && Current.ChannelDays != 7 && Current.ChannelDays != 30)
                Current.ChannelDays = 30;

            try
            {
                if (File.Exists(HistoryPath))
                {
                    List<HistoryEntry> h = J.FromJson<List<HistoryEntry>>(File.ReadAllText(HistoryPath, Encoding.UTF8));
                    if (h != null) History = h;
                }
            }
            catch { History = new List<HistoryEntry>(); }

            try
            {
                if (File.Exists(ChannelsPath))
                {
                    List<SavedChannel> ch = J.FromJson<List<SavedChannel>>(File.ReadAllText(ChannelsPath, Encoding.UTF8));
                    if (ch != null) Channels = ch;
                }
            }
            catch { Channels = new List<SavedChannel>(); }
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

        public static void SaveChannels()
        {
            try { File.WriteAllText(ChannelsPath, J.ToJson(Channels), Encoding.UTF8); } catch { }
        }

        public static void UpsertChannel(string id, string name, string url)
        {
            if (string.IsNullOrEmpty(id)) return;
            SavedChannel hit = null;
            for (int i = 0; i < Channels.Count; i++)
                if (string.Equals(Channels[i].Id, id, StringComparison.OrdinalIgnoreCase)) { hit = Channels[i]; break; }
            if (hit == null)
            {
                hit = new SavedChannel();
                Channels.Insert(0, hit);
            }
            hit.Id = id;
            if (!string.IsNullOrEmpty(name)) hit.Name = name;
            if (!string.IsNullOrEmpty(url)) hit.Url = url;
            hit.LastChecked = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            SaveChannels();
        }

        public static int CountDownloaded(string channelId)
        {
            int n = 0;
            for (int i = 0; i < History.Count; i++)
                if (string.Equals(History[i].ChannelId, channelId, StringComparison.OrdinalIgnoreCase)) n++;
            return n;
        }

        public static void AddHistory(HistoryEntry e)
        {
            History.Insert(0, e);
            SaveHistory();
        }
    }
}
