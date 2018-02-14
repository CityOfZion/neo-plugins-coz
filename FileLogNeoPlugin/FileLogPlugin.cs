using Neo.Plugins;
using System;
using System.IO;
using System.Runtime.ExceptionServices;

namespace FileLogNeoPlugin
{
    public class FileLogPlugin : NeoLogPlugin
    {
        object Dummy = new object();

        /// <summary>
        /// File path
        /// </summary>
        public string GetLogFilePath()
        {
            return Path.Combine(".", "log_" + DateTime.Now.ToString("yyyy-MM-dd") + ".txt");
        }

        public override bool Load()
        {
            AppDomain.CurrentDomain.FirstChanceException += CurrentDomain_FirstChanceException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            return base.Load();
        }
        public override void Unload()
        {
            AppDomain.CurrentDomain.FirstChanceException -= CurrentDomain_FirstChanceException;
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;

            base.Unload();
        }

        void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Log((Exception)e.ExceptionObject);
        }

        void CurrentDomain_FirstChanceException(object sender, FirstChanceExceptionEventArgs e)
        {
            Log(e.Exception);
        }

        public override void Log(Exception error)
        {
            if (error == null) return;

            Log("[ERROR] " + error.ToString());
        }

        public override void Log(string message)
        {
            try
            {
                message = message.Replace("\n", "\t");
                message = message.Replace("\r", "");

                lock (Dummy)
                {
                    File.AppendAllText(GetLogFilePath(), "[" + DateTime.Now.ToString() + "] - " + message + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}