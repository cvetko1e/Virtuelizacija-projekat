using System;
using System.Globalization;
using System.IO;
using Common;

namespace Service
{
    public class WeatherStorage : IDisposable
    {
        private bool disposed;
        private readonly string basePath;
        private StreamWriter measurementsWriter;
        private StreamWriter rejectsWriter;

        public WeatherStorage(string basePath)
        {
            this.basePath = basePath;
        }

        public void StartSessionFiles(string sessionId)
        {
            string sessionPath = Path.Combine(basePath, sessionId);
            Directory.CreateDirectory(sessionPath);

            measurementsWriter = new StreamWriter(Path.Combine(sessionPath, "measurements_session.csv"), false);
            rejectsWriter = new StreamWriter(Path.Combine(sessionPath, "rejects.csv"), false);

            measurementsWriter.WriteLine("T,Tpot,Tdew,Sh,Rh,Date");
            rejectsWriter.WriteLine("T,Tpot,Tdew,Sh,Rh,Date,Reason");
            measurementsWriter.Flush();
            rejectsWriter.Flush();
        }

        public void WriteMeasurement(WeatherSample sample)
        {
            if (measurementsWriter == null)
            {
                return;
            }

            measurementsWriter.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5:O}",
                sample.T,
                sample.Tpot,
                sample.Tdew,
                sample.Sh,
                sample.Rh,
                sample.Date));
            measurementsWriter.Flush();
        }

        public void WriteReject(WeatherSample sample, string reason)
        {
            if (rejectsWriter == null)
            {
                return;
            }

            if (sample == null)
            {
                rejectsWriter.WriteLine(",,,,,," + reason);
            }
            else
            {
                rejectsWriter.WriteLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "{0},{1},{2},{3},{4},{5:O},{6}",
                    sample.T,
                    sample.Tpot,
                    sample.Tdew,
                    sample.Sh,
                    sample.Rh,
                    sample.Date,
                    reason));
            }

            rejectsWriter.Flush();
        }

        public void CloseFiles()
        {
            if (measurementsWriter != null)
            {
                measurementsWriter.Dispose();
                measurementsWriter = null;
            }

            if (rejectsWriter != null)
            {
                rejectsWriter.Dispose();
                rejectsWriter = null;
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
                    CloseFiles();
                }

                disposed = true;
            }
        }

        ~WeatherStorage()
        {
            Dispose(false);
        }
    }
}
