using System;
using System.IO;
using Common;

namespace Service
{
    public static class ResourceDisposalSimulation
    {
        public static bool Run(string basePath, Logger logger)
        {
            string sessionId = "dispose_simulation_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string sessionPath = Path.Combine(basePath, sessionId);
            string measurementsPath = Path.Combine(sessionPath, "measurements_session.csv");
            string rejectsPath = Path.Combine(sessionPath, "rejects.csv");

            logger.Info("Pokretanje simulacije prekida veze usred prenosa.");

            try
            {
                using (WeatherStorage storage = new WeatherStorage(basePath))
                {
                    storage.StartSessionFiles(sessionId);
                    storage.WriteMeasurement(new WeatherSample(
                        25.0,
                        298.15,
                        16.0,
                        0.012,
                        55.0,
                        DateTime.Now));

                    throw new InvalidOperationException("Simulirani prekid veze usred prenosa.");
                }
            }
            catch (InvalidOperationException ex)
            {
                logger.Warning(ex.Message);
            }

            bool measurementsClosed = CanOpenExclusive(measurementsPath);
            bool rejectsClosed = CanOpenExclusive(rejectsPath);

            logger.Info(string.Format("measurements_session.csv zatvoren: {0}", measurementsClosed));
            logger.Info(string.Format("rejects.csv zatvoren: {0}", rejectsClosed));

            return measurementsClosed && rejectsClosed;
        }

        private static bool CanOpenExclusive(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                return stream.CanRead && stream.CanWrite;
            }
        }
    }
}
