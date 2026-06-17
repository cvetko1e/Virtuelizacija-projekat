using System;
using System.Collections.Generic;
using Common;

namespace Service
{
    /// <summary>
    /// Pretplatnik na dogadjaje WeatherService-a.
    /// Loguje sve dogadjaje (transfer, sample, warning) i vrsi
    /// dodatnu analizu/obradu warning-a po tipu i ozbiljnosti.
    /// Na kraju sesije ispisuje sumarni izvestaj.
    /// </summary>
    public class WeatherEventHandler : IDisposable
    {
        private bool disposed;
        private readonly Logger logger;

        private int samplesReceived;
        private int totalWarnings;
        private readonly Dictionary<string, int> warningsByType;
        private readonly List<string> criticalAlerts;
        private DateTime? sessionStartTime;

        public WeatherEventHandler(Logger logger)
        {
            this.logger = logger;
            warningsByType = new Dictionary<string, int>();
            criticalAlerts = new List<string>();
        }

        /// <summary>
        /// Registruje sve event handler-e na dati servis.
        /// </summary>
        public void Subscribe(WeatherService service)
        {
            service.OnTransferStarted += HandleTransferStarted;
            service.OnSampleReceived += HandleSampleReceived;
            service.OnTransferCompleted += HandleTransferCompleted;
            service.OnWarningRaised += HandleWarningRaised;
        }

        /// <summary>
        /// Uklanja registraciju sa datog servisa.
        /// </summary>
        public void Unsubscribe(WeatherService service)
        {
            service.OnTransferStarted -= HandleTransferStarted;
            service.OnSampleReceived -= HandleSampleReceived;
            service.OnTransferCompleted -= HandleTransferCompleted;
            service.OnWarningRaised -= HandleWarningRaised;
        }

        public void HandleTransferStarted(object sender, TransferEventArgs e)
        {
            samplesReceived = 0;
            totalWarnings = 0;
            warningsByType.Clear();
            criticalAlerts.Clear();
            sessionStartTime = DateTime.Now;

            logger.Info(string.Format("=== SESIJA POKRENUTA: {0} ===", e.SessionId));
            logger.Info(e.Message);
            logger.Info("Prenos u toku...");
        }

        public void HandleSampleReceived(object sender, SampleEventArgs e)
        {
            samplesReceived++;
            logger.Info(string.Format(
                "Prenos u toku... primljen uzorak #{0} za sesiju {1}.",
                samplesReceived,
                e.SessionId));

            // Progres ispis svakih 10 uzoraka.
            if (samplesReceived % 10 == 0)
            {
                logger.Info(string.Format("Primljeno {0} uzoraka u sesiji {1}.", samplesReceived, e.SessionId));
            }
        }

        public void HandleTransferCompleted(object sender, TransferEventArgs e)
        {
            logger.Info(string.Format("=== SESIJA ZAVRSENA: {0} ===", e.SessionId));
            logger.Info(e.Message);
            logger.Info("Zavrsen prenos.");

            PrintSessionSummary(e.SessionId);
        }

        public void HandleWarningRaised(object sender, WarningEventArgs e)
        {
            totalWarnings++;

            // Brojanje po tipu.
            if (warningsByType.ContainsKey(e.WarningType))
            {
                warningsByType[e.WarningType]++;
            }
            else
            {
                warningsByType[e.WarningType] = 1;
            }

            // Klasifikacija ozbiljnosti i obrada.
            string severity = ClassifySeverity(e.WarningType);

            if (severity == "CRITICAL")
            {
                string alert = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}: {2} (vrednost={3:F4}, ocekivano={4:F4}, smer={5})",
                    e.Date, e.WarningType, e.Message, e.CurrentValue, e.ExpectedValue, e.Direction ?? "-");
                criticalAlerts.Add(alert);
                logger.Error(string.Format("KRITICNO UPOZORENJE [{0}]: {1} | Vrednost={2:F4} | Ocekivano={3:F4} | Smer={4} | Datum={5:O}",
                    e.WarningType, e.Message, e.CurrentValue, e.ExpectedValue, e.Direction ?? "-", e.Date));
            }
            else
            {
                logger.Warning(string.Format("Upozorenje [{0}]: {1} | Vrednost={2:F4} | Ocekivano={3:F4} | Smer={4} | Datum={5:O}",
                    e.WarningType, e.Message, e.CurrentValue, e.ExpectedValue, e.Direction ?? "-", e.Date));
            }
        }

        /// <summary>
        /// Klasifikuje ozbiljnost upozorenja na osnovu tipa.
        /// HISpike je CRITICAL jer nagla promena indeksa toplote utice na zdravlje ljudi.
        /// SHSpike je WARNING jer ukazuje na naglu promenu.
        /// OutOfBandWarning je INFO jer je statisticko odstupanje.
        /// </summary>
        private string ClassifySeverity(string warningType)
        {
            switch (warningType)
            {
                case "HISpike":
                    return "CRITICAL";
                case "SHSpike":
                    return "WARNING";
                case "OutOfBandWarning":
                    return "INFO";
                default:
                    return "WARNING";
            }
        }

        /// <summary>
        /// Ispisuje sumarni izvestaj na kraju sesije.
        /// </summary>
        private void PrintSessionSummary(string sessionId)
        {
            logger.Info("------------------------------------------------------------");
            logger.Info(string.Format("SUMARNI IZVESTAJ ZA SESIJU: {0}", sessionId));
            logger.Info(string.Format("  Ukupno primljenih uzoraka: {0}", samplesReceived));
            logger.Info(string.Format("  Ukupno upozorenja:         {0}", totalWarnings));

            if (warningsByType.Count > 0)
            {
                logger.Info("  Upozorenja po tipu:");
                foreach (var pair in warningsByType)
                {
                    string sev = ClassifySeverity(pair.Key);
                    logger.Info(string.Format("    - {0}: {1} (ozbiljnost: {2})", pair.Key, pair.Value, sev));
                }
            }

            if (criticalAlerts.Count > 0)
            {
                logger.Warning(string.Format("  KRITICNI ALARMI ({0}):", criticalAlerts.Count));
                for (int i = 0; i < criticalAlerts.Count; i++)
                {
                    logger.Warning(string.Format("    [{0}] {1}", i + 1, criticalAlerts[i]));
                }
            }

            if (sessionStartTime.HasValue)
            {
                TimeSpan duration = DateTime.Now - sessionStartTime.Value;
                logger.Info(string.Format("  Trajanje sesije: {0:F1} sekundi", duration.TotalSeconds));
            }

            logger.Info("------------------------------------------------------------");
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
                    // Logger se ne dispose-uje ovde jer je dodeljen spolja.
                }

                disposed = true;
            }
        }

        ~WeatherEventHandler()
        {
            Dispose(false);
        }
    }
}
