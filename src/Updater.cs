using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace MpcGrabber
{
    internal class ReleaseInfo
    {
        public Version Version;
        public string Tag = "";
        public string HtmlUrl = "";
        public string AssetUrl = "";
    }

    internal static class Updater
    {
        private const string Repo = "jlewsi/mpc-grabber";
        private const string Api = "https://api.github.com/repos/" + Repo;

        public static Version Current
        {
            get
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                if (v == null) return new Version(0, 0, 0);
                return v;
            }
        }

        public static string CurrentShort
        {
            get
            {
                Version v = Current;
                return v.Major + "." + v.Minor + "." + Math.Max(v.Build, 0);
            }
        }

        public static Version ParseVersion(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return new Version(0, 0, 0);
            string s = tag.Trim().TrimStart('v', 'V');
            int dash = s.IndexOf('-');
            if (dash > 0) s = s.Substring(0, dash);
            string[] parts = s.Split('.');
            int major = 0, minor = 0, build = 0;
            if (parts.Length > 0) int.TryParse(parts[0], out major);
            if (parts.Length > 1) int.TryParse(parts[1], out minor);
            if (parts.Length > 2) int.TryParse(parts[2], out build);
            return new Version(major, minor, build);
        }

        public static async Task<ReleaseInfo> CheckAsync()
        {
            string json = await Task.Run(delegate
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "MPCGrabber/" + CurrentShort;
                    wc.Headers["Accept"] = "application/vnd.github+json";
                    return wc.DownloadString(Api + "/releases/latest");
                }
            });

            JavaScriptSerializer js = new JavaScriptSerializer();
            js.MaxJsonLength = int.MaxValue;
            Dictionary<string, object> d = js.Deserialize<Dictionary<string, object>>(json);
            if (d == null) return null;

            ReleaseInfo r = new ReleaseInfo();
            r.Tag = J.Str(d, "tag_name");
            r.HtmlUrl = J.Str(d, "html_url");
            r.Version = ParseVersion(r.Tag);

            List<object> assets = J.Arr(d, "assets");
            for (int i = 0; i < assets.Count; i++)
            {
                Dictionary<string, object> a = J.AsDict(assets[i]);
                if (a == null) continue;
                if (string.Equals(J.Str(a, "name"), "MPCGrabber.exe", StringComparison.OrdinalIgnoreCase))
                {
                    r.AssetUrl = J.Str(a, "browser_download_url");
                    break;
                }
            }
            return r;
        }

        public static bool IsNewer(ReleaseInfo r)
        {
            return r != null && r.Version != null && r.Version > Current;
        }

        public static void DownloadAndRestart(ReleaseInfo r, out string error)
        {
            error = "";
            try
            {
                if (r == null || string.IsNullOrEmpty(r.AssetUrl))
                    throw new Exception("The release has no MPCGrabber.exe asset.");

                string dir = AppDomain.CurrentDomain.BaseDirectory;
                string staged = Path.Combine(dir, "MPCGrabber.update.exe");

                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                using (WebClient wc = new WebClient())
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "MPCGrabber/" + CurrentShort;
                    wc.DownloadFile(r.AssetUrl, staged);
                }

                string bat = Path.Combine(dir, "mpcgrabber-update.cmd");
                string script =
                    "@echo off\r\n" +
                    "set n=0\r\n" +
                    "ping -n 3 127.0.0.1 >nul\r\n" +
                    ":loop\r\n" +
                    "move /y \"%~dp0MPCGrabber.update.exe\" \"%~dp0MPCGrabber.exe\" >nul 2>&1\r\n" +
                    "if not errorlevel 1 goto done\r\n" +
                    "set /a n+=1\r\n" +
                    "if %n% geq 90 goto fail\r\n" +
                    "ping -n 2 127.0.0.1 >nul\r\n" +
                    "goto loop\r\n" +
                    ":done\r\n" +
                    "start \"\" \"%~dp0MPCGrabber.exe\"\r\n" +
                    "del \"%~dp0mpcgrabber-update.cmd\"\r\n" +
                    "goto :eof\r\n" +
                    ":fail\r\n" +
                    "del \"%~dp0MPCGrabber.update.exe\"\r\n" +
                    "del \"%~dp0mpcgrabber-update.cmd\"\r\n";
                File.WriteAllText(bat, script);

                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c \"" + bat + "\"");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                error = ex.Message;
            }
        }
    }
}
