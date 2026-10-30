using System;
using System.IO;
using System.Text;

namespace QingShang
{
    /// <summary>极简运行日志，出问题时用来定位崩溃发生在哪一步。</summary>
    internal static class Diag
    {
        private static readonly object Gate = new object();
        private static readonly string Path =
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "qingshang.log");

        public static bool Enabled { get; set; } = true;

        public static void Write(string message)
        {
            if (!Enabled) return;
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(Path,
                        DateTime.Now.ToString("HH:mm:ss.fff") + "  " + message + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // 忽略
            }
        }

        public static void Error(string tag, Exception ex)
        {
            try
            {
                lock (Gate)
                {
                    File.AppendAllText(Path,
                        DateTime.Now.ToString("HH:mm:ss.fff") + "  !! " + tag + ": " + (ex == null ? "(null)" : ex.ToString()) + Environment.NewLine,
                        Encoding.UTF8);
                }
            }
            catch
            {
                // 忽略
            }
        }

        public static void Reset()
        {
            try
            {
                if (File.Exists(Path)) File.Delete(Path);
            }
            catch
            {
                // 忽略
            }
        }
    }
}
