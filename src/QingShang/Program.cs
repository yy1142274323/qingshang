using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using QingShang.Core;
using QingShang.UI;

namespace QingShang
{
    internal static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern uint SetErrorMode(uint mode);

        [STAThread]
        private static void Main(string[] args)
        {
            // 进程内绝不弹系统崩溃框，出问题只写日志
            SetErrorMode(0x0001 | 0x0002 | 0x8000);

            Diag.Reset();
            Diag.Write("start, dpi=" + GetDpiString());

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (s, e) =>
            {
                Diag.Error("Thread", e.Exception);
            };

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                Diag.Error("AppDomain", e.ExceptionObject as Exception);
            };

            try
            {
                using (var form = new MainForm(args))
                {
                    Application.Run(form);
                }
                Diag.Write("exit normally");
            }
            catch (Exception ex)
            {
                Diag.Error("Main", ex);
            }
        }

        private static string GetDpiString()
        {
            try
            {
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                    return g.DpiX.ToString("0.#");
            }
            catch
            {
                return "?";
            }
        }
    }
}
