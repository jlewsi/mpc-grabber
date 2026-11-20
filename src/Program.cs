using System;
using System.Windows.Forms;

namespace MpcGrabber
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.ThreadException += delegate(object s, System.Threading.ThreadExceptionEventArgs e)
                {
                    Log(e.Exception);
                    MessageBox.Show(e.Exception.Message, "MPC Grabber \u2014 unexpected error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                };
                Application.Run(new MainForm(Environment.GetCommandLineArgs()));
            }
            catch (Exception ex)
            {
                Log(ex);
                throw;
            }
        }

        private static void Log(Exception ex)
        {
            try
            {
                string p = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crash.log");
                System.IO.File.AppendAllText(p, DateTime.Now.ToString("s") + Environment.NewLine + ex.ToString() + Environment.NewLine + Environment.NewLine);
            }
            catch { }
        }
    }
}
