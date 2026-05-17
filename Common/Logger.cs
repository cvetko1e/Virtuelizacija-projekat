using System;
using System.IO;

namespace Common
{
    /// <summary>
    /// Strukturirani logger koji ispisuje na konzolu i u fajl.
    /// Format: [YYYY-MM-DD HH:mm:ss] [NIVO] [IZVOR] Poruka
    /// Implementira IDisposable za zatvaranje StreamWriter-a.
    /// </summary>
    public class Logger : IDisposable
    {
        private bool disposed;
        private StreamWriter fileWriter;
        private readonly string source;
        private readonly object lockObj = new object();

        /// <summary>
        /// Kreira novi Logger sa datim izvorom i opcionalnom putanjom do log fajla.
        /// </summary>
        /// <param name="source">Naziv komponente (npr. "Service", "Client").</param>
        /// <param name="logFilePath">Putanja do log fajla. Ako je null, loguje se samo na konzolu.</param>
        public Logger(string source, string logFilePath = null)
        {
            this.source = source;

            if (!string.IsNullOrEmpty(logFilePath))
            {
                string directory = Path.GetDirectoryName(logFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                fileWriter = new StreamWriter(logFilePath, true) { AutoFlush = true };
            }
        }

        public void Info(string message)
        {
            Log("INFO", message);
        }

        public void Warning(string message)
        {
            Log("WARN", message);
        }

        public void Error(string message)
        {
            Log("ERROR", message);
        }

        public void Log(string level, string message)
        {
            string entry = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] [{2}] {3}",
                DateTime.Now, level, source, message);

            lock (lockObj)
            {
                ConsoleColor original = Console.ForegroundColor;
                try
                {
                    if (level == "ERROR")
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                    }
                    else if (level == "WARN")
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                    }

                    Console.WriteLine(entry);
                }
                finally
                {
                    Console.ForegroundColor = original;
                }

                if (fileWriter != null)
                {
                    fileWriter.WriteLine(entry);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    if (fileWriter != null)
                    {
                        fileWriter.Dispose();
                        fileWriter = null;
                    }
                }

                disposed = true;
            }
        }

        ~Logger()
        {
            Dispose(false);
        }
    }
}
