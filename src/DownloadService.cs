using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace MpcGrabber
{
    internal class DownloadService
    {
        public readonly List<DownloadJob> Jobs = new List<DownloadJob>();
        public event EventHandler Changed;
        public event Action<HistoryEntry> JobCompleted;

        private Process _proc;
        private DownloadJob _current;
        private Task _worker;
        private volatile bool _cancel;
        private int _lastRaise;

        public bool Busy
        {
            get { return _worker != null && !_worker.IsCompleted; }
        }

        public void Enqueue(string url, string title, FormatOption fmt, string folder)
        {
            DownloadJob job = new DownloadJob();
            job.Url = url;
            job.Title = string.IsNullOrWhiteSpace(title) ? url : title;
            job.VideoFmt = fmt.VideoFmt;
            job.AudioBitrate = fmt.AudioBitrate;
            job.QualityLabel = fmt.Title;
            lock (Jobs) Jobs.Add(job);
            Raise(true);
            EnsureWorker();
        }

        private void EnsureWorker()
        {
            if (_worker != null && !_worker.IsCompleted) return;
            _worker = Worker();
        }

        private async Task Worker()
        {
            while (true)
            {
                DownloadJob job = null;
                lock (Jobs)
                {
                    for (int i = 0; i < Jobs.Count; i++)
                        if (Jobs[i].State == JobState.Queued) { job = Jobs[i]; break; }
                }
                if (job == null) break;
                await RunJob(job);
            }
        }

        private async Task RunJob(DownloadJob job)
        {
            job.State = JobState.Downloading;
            job.Status = "Starting";
            Raise(true);

            string folder = Store.Current.DownloadFolder;
            try { Directory.CreateDirectory(folder); }
            catch
            {
                job.State = JobState.Failed;
                job.Error = "Cannot create folder: " + folder;
                job.Status = "Failed";
                Raise(true);
                return;
            }

            string temp = Path.Combine(Path.GetTempPath(), "mpcgrabber_" + Guid.NewGuid().ToString("N") + ".txt");
            StringBuilder args = new StringBuilder();
            args.Append(YtDlp.CommonArgs());
            args.Append("--newline --no-playlist --windows-filenames ");
            args.Append("-o ").Append(YtDlp.Quote(Path.Combine(folder, "%(title)s.%(ext)s"))).Append(' ');
            args.Append("--print-to-file after_move:filepath ").Append(YtDlp.Quote(temp)).Append(' ');
            if (job.AudioBitrate > 0)
                args.Append("-x --audio-format mp3 --audio-quality ").Append(job.AudioBitrate).Append("K ");
            else
                args.Append("--merge-output-format mp4 ");
            args.Append("-f ").Append(YtDlp.Quote(job.VideoFmt)).Append(' ');
            args.Append(YtDlp.Quote(job.Url));

            Process p = new Process();
            p.StartInfo = new ProcessStartInfo(YtDlp.ExePath, args.ToString());
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            p.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            p.StartInfo.WorkingDirectory = YtDlp.Dir;

            StringBuilder errText = new StringBuilder();
            object gate = new object();
            _proc = p;
            _current = job;
            _cancel = false;

            p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                ParseLine(job, e.Data);
                Raise(false);
            };
            p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (gate) errText.AppendLine(e.Data);
            };

            TaskCompletionSource<bool> done = new TaskCompletionSource<bool>();
            p.EnableRaisingEvents = true;
            p.Exited += delegate
            {
                try { p.WaitForExit(); } catch { }
                done.TrySetResult(true);
            };

            try
            {
                p.Start();
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();
                await done.Task;
            }
            catch (Exception ex)
            {
                job.State = JobState.Failed;
                job.Error = ex.Message;
                job.Status = "Failed";
                _proc = null;
                Raise(true);
                return;
            }

            int code = 0;
            try { code = p.ExitCode; } catch { }
            try { p.Dispose(); } catch { }
            _proc = null;

            string outPath = "";
            try
            {
                if (File.Exists(temp))
                {
                    string[] lines = File.ReadAllLines(temp);
                    if (lines.Length > 0) outPath = lines[lines.Length - 1].Trim();
                    File.Delete(temp);
                }
            }
            catch { }

            if (_cancel)
            {
                job.State = JobState.Cancelled;
                job.Status = "Cancelled";
                job.Error = "";
                Raise(true);
                return;
            }

            if (code != 0 || outPath.Length == 0 || !File.Exists(outPath))
            {
                job.State = JobState.Failed;
                job.Status = "Failed";
                string err;
                lock (gate) err = errText.ToString().Trim();
                if (err.Length == 0) err = "yt-dlp exited with code " + code + ".";
                string[] el = err.Split('\n');
                StringBuilder last = new StringBuilder();
                for (int i = Math.Max(0, el.Length - 3); i < el.Length; i++)
                {
                    if (el[i].Trim().Length > 0) last.AppendLine(el[i].Trim());
                }
                job.Error = last.ToString().Trim();
                Raise(true);
                return;
            }

            job.OutputPath = outPath;
            job.State = JobState.Done;
            job.Status = "Done";
            job.Progress = 1;
            job.Speed = "";
            job.Eta = "";

            HistoryEntry h = new HistoryEntry();
            h.Title = job.Title;
            h.Url = job.Url;
            h.FilePath = outPath;
            h.Kind = job.AudioBitrate > 0 ? "MP3" : "MP4";
            h.Quality = job.QualityLabel;
            h.FinishedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            try { h.SizeBytes = new FileInfo(outPath).Length; } catch { }
            if (JobCompleted != null) JobCompleted(h);
            Raise(true);
        }

        private static readonly Regex ProgressRx = new Regex(
            @"\[download\]\s+(\d+(?:\.\d+)?)%\s+of\s+~?\s*([\d.]+)\s*([KMG]iB)(?:\s+at\s+([\d.]+)\s*([KMG]?iB)/s)?(?:\s+ETA\s+([\d:]+))?",
            RegexOptions.Compiled);

        private void ParseLine(DownloadJob job, string line)
        {
            Match m = ProgressRx.Match(line);
            if (m.Success)
            {
                double pct;
                if (double.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out pct))
                    job.Progress = Math.Min(0.98, pct / 100.0);
                job.Status = "Downloading";
                job.Speed = m.Groups[4].Success ? m.Groups[4].Value + m.Groups[5].Value + "/s" : "";
                job.Eta = m.Groups[6].Success ? m.Groups[6].Value : "";
                return;
            }
            if (line.IndexOf("[Merger]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("[ExtractAudio]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("[VideoConvertor]", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                job.State = JobState.Converting;
                job.Status = "Processing";
                job.Progress = 0.99;
            }
        }

        private void Raise(bool force)
        {
            int now = Environment.TickCount;
            if (!force && now - _lastRaise < 90) return;
            _lastRaise = now;
            if (Changed != null)
            {
                try { Changed(this, EventArgs.Empty); } catch { }
            }
        }

        public void CancelCurrent()
        {
            _cancel = true;
            Process p = _proc;
            if (p == null) return;
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo("taskkill", "/PID " + p.Id + " /T /F");
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                Process.Start(psi);
            }
            catch { }
        }

        public void Shutdown()
        {
            try
            {
                if (_proc != null && !_proc.HasExited) _proc.Kill();
            }
            catch { }
        }
    }
}
