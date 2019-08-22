// FileLogNeoPlugin by shargon
// Log all neo-cli exceptions to a file

using System;
using System.IO;
using System.Runtime.ExceptionServices;

namespace Neo.Plugins
{
    public class FileLogPlugin : Plugin, ILogPlugin
    {
        object Dummy = new object();

        public FileLogPlugin()
        {
            // Create directory for logs

            string path = Path.GetDirectoryName(GetLogFilePath());
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        }

        ~FileLogPlugin()
        {
            AppDomain.CurrentDomain.FirstChanceException -= CurrentDomain_FirstChanceException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
        }

        public override void Configure()
        {
        }

        public string GetLogFilePath()
        {
            return Path.Combine(".", "Logs", "logPlugin_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            MyLog((Exception)e.ExceptionObject);
        }

        void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            MyLog(e.Exception);
        }

        public void MyLog(Exception error)
        {
            if (error == null) return;
            Log("FileLogPlugin", LogLevel.Error, error.ToString());
        }
        
        void ILogPlugin.Log(string source, LogLevel level, string message)
        {
            try
            {
                message = message.Replace("\n", "\t");
                message = message.Replace("\r", "");

                lock (Dummy)
                {
                    string m = $"[{DateTime.Now.ToString()}] {source}: [{Enum.GetName(typeof(LogLevel), level)}] - {message}{Environment.NewLine}";
                    File.AppendAllText(GetLogFilePath(), m);
                }
            }
            catch { }
        }
    }
}
