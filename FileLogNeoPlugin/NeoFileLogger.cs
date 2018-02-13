using Neo.Plugins;
using System;
using System.IO;
using System.Runtime.ExceptionServices;

namespace FileLogNeoPlugin
{
    public class NeoFileLogger : NeoLogPlugin
    {
        /// <summary>
        /// File path
        /// </summary>
        public string LogFilePath { get; set; }

        public override bool Load()
        {
            LogFilePath = Path.Combine(".", "log.txt");

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

            Log(error.ToString());
        }

        public override void Log(string message)
        {
            try
            {
                lock (this)
                {
                    File.AppendAllText(LogFilePath, DateTime.Now.ToString() + " - " + message + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}