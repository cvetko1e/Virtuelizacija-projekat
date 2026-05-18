using System;
using System.Configuration;
using System.ServiceModel;
using Common;

namespace Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class WeatherService : IWeatherService
    {
        private string currentSessionId;
        private bool sessionStarted;
        private double? previousSh;
        private double shSum;
        private int sampleCount;
        private readonly WeatherStorage storage;
        private readonly WeatherAnalytics analytics;

        private readonly double shThreshold;
        private readonly double hiMaxThreshold;
        private readonly double outOfBandPercent;

        public delegate void TransferStartedHandler(object sender, TransferEventArgs e);
        public delegate void SampleReceivedHandler(object sender, SampleEventArgs e);
        public delegate void TransferCompletedHandler(object sender, TransferEventArgs e);
        public delegate void WarningRaisedHandler(object sender, WarningEventArgs e);

        public event TransferStartedHandler OnTransferStarted;
        public event SampleReceivedHandler OnSampleReceived;
        public event TransferCompletedHandler OnTransferCompleted;
        public event WarningRaisedHandler OnWarningRaised;

        public WeatherService()
        {
            string storagePath = ConfigurationManager.AppSettings["storagePath"] ?? "Measurements";
            storage = new WeatherStorage(storagePath);
            analytics = new WeatherAnalytics();

            shThreshold = ParseSetting("SH_threshold", 0.002);
            hiMaxThreshold = ParseSetting("HI_max_threshold", 5);
            outOfBandPercent = ParseSetting("OUT_OF_BAND_PERCENT", 25);
        }

        public TransferResponse StartSession(SessionMeta meta)
        {
            if (meta == null)
            {
                throw new FaultException<ValidationFault>(new ValidationFault
                {
                    Message = "Meta podaci ne postoje.",
                    Field = "meta",
                    Code = "MISSING_FIELD"
                });
            }

            if (string.IsNullOrWhiteSpace(meta.SessionId))
            {
                throw new FaultException<ValidationFault>(new ValidationFault
                {
                    Message = "SessionId je obavezan.",
                    Field = "SessionId",
                    Code = "MISSING_FIELD"
                });
            }

            if (meta.ExpectedSamples <= 0)
            {
                throw new FaultException<ValidationFault>(new ValidationFault
                {
                    Message = "ExpectedSamples mora biti > 0.",
                    Field = "ExpectedSamples",
                    Code = "OUT_OF_RANGE"
                });
            }

            if (meta.HeaderFields == null || meta.HeaderFields.Length == 0)
            {
                throw new FaultException<ValidationFault>(new ValidationFault
                {
                    Message = "HeaderFields mora sadrzati barem jedno polje.",
                    Field = "HeaderFields",
                    Code = "MISSING_FIELD"
                });
            }

            if (string.IsNullOrWhiteSpace(meta.SourceFile))
            {
                throw new FaultException<ValidationFault>(new ValidationFault
                {
                    Message = "SourceFile je obavezan.",
                    Field = "SourceFile",
                    Code = "MISSING_FIELD"
                });
            }

            currentSessionId = meta.SessionId;
            sessionStarted = true;
            previousSh = null;
            shSum = 0;
            sampleCount = 0;

            storage.StartSessionFiles(currentSessionId);

            RaiseTransferStarted("Sesija je uspesno pokrenuta.");

            return new TransferResponse
            {
                Success = true,
                Message = "Session started.",
                Status = TransferStatus.IN_PROGRESS
            };
        }

        public TransferResponse PushSample(WeatherSample sample)
        {
            if (!sessionStarted)
            {
                throw new FaultException<ValidationFault>(new ValidationFault
                {
                    Message = "Sesija nije pokrenuta.",
                    Field = "session",
                    Code = "INVALID_STATE"
                });
            }

            // Provera formata podataka — da li je sample uopste validan objekat.
            if (sample == null)
            {
                throw new FaultException<DataFormatFault>(new DataFormatFault
                {
                    Message = "Sample objekat je null.",
                    Field = "sample",
                    Code = "NULL_OBJECT"
                });
            }

            // Validacija formata datuma.
            if (sample.Date == DateTime.MinValue)
            {
                throw new FaultException<DataFormatFault>(new DataFormatFault
                {
                    Message = "Date nije parsiran ili je prazan.",
                    Field = "Date",
                    Code = "INVALID_FORMAT"
                });
            }

            // Validacija opsega (business rules) — vraca NACK umesto izuzetka.
            string validationMessage;
            string validationField;
            if (!ValidateSampleRange(sample, out validationMessage, out validationField))
            {
                storage.WriteReject(sample, validationMessage);
                return new TransferResponse
                {
                    Success = false,
                    Message = "NACK: " + validationMessage,
                    Status = TransferStatus.IN_PROGRESS
                };
            }

            storage.WriteMeasurement(sample);
            sampleCount++;
            shSum += sample.Sh;
            RaiseSampleReceived(sample);

            // --- Provera nagle promene specificne vlage (Δsh) ---
            // Formula: Δsh = sh(t) - sh(t - Δt)
            // Ako je |Δsh| > SH_threshold, podici dogadjaj.
            if (previousSh.HasValue)
            {
                double deltaSh = analytics.CalculateDelta(sample.Sh, previousSh.Value);
                if (Math.Abs(deltaSh) > shThreshold)
                {
                    RaiseWarning(
                        "SHSpike",
                        string.Format("Nagla promena specificne vlage: |Δsh| = {0:F6} > prag {1:F6}.",
                            Math.Abs(deltaSh), shThreshold),
                        sample.Sh,
                        previousSh.Value,
                        sample.Date);
                }
            }

            // --- Provera odstupanja SH od running-mean proseka (±25%) ---
            double meanSh = analytics.CalculateRunningMean(shSum, sampleCount);
            double lowerBand = meanSh * (1.0 - outOfBandPercent / 100.0);
            double upperBand = meanSh * (1.0 + outOfBandPercent / 100.0);
            if (sample.Sh < lowerBand || sample.Sh > upperBand)
            {
                RaiseWarning(
                    "OutOfBandWarning",
                    string.Format("SH ({0:F4}) je van opsega [{1:F4}, {2:F4}] u odnosu na running mean ({3:F4}).",
                        sample.Sh, lowerBand, upperBand, meanSh),
                    sample.Sh,
                    meanSh,
                    sample.Date);
            }

            // --- Racunanje Heat Index-a i provera praga ---
            // Ako je HI > HI_max_threshold, podici dogadjaj.
            double currentHi = analytics.CalculateHeatIndex(sample.T, sample.Rh);
            if (currentHi > hiMaxThreshold)
            {
                RaiseWarning(
                    "HIExceeded",
                    string.Format("Heat Index ({0:F2}) premašuje dozvoljeni prag ({1:F2}).",
                        currentHi, hiMaxThreshold),
                    currentHi,
                    hiMaxThreshold,
                    sample.Date);
            }

            previousSh = sample.Sh;

            return new TransferResponse
            {
                Success = true,
                Message = "ACK",
                Status = TransferStatus.IN_PROGRESS
            };
        }

        public TransferResponse EndSession()
        {
            if (!sessionStarted)
            {
                throw new FaultException<ValidationFault>(new ValidationFault
                {
                    Message = "Sesija nije aktivna.",
                    Field = "session",
                    Code = "INVALID_STATE"
                });
            }

            storage.CloseFiles();
            sessionStarted = false;

            RaiseTransferCompleted("Sesija zavrsena.");

            return new TransferResponse
            {
                Success = true,
                Message = "Session completed.",
                Status = TransferStatus.COMPLETED
            };
        }

        /// <summary>
        /// Validacija dozvoljenih opsega za svako polje uzorka.
        /// Vraca false i postavlja poruku/polje ako validacija ne prolazi.
        /// </summary>
        private bool ValidateSampleRange(WeatherSample sample, out string message, out string field)
        {
            // Sh — specificna vlaznost mora biti > 0 (g/kg).
            if (sample.Sh <= 0)
            {
                message = "Sh mora biti > 0.";
                field = "Sh";
                return false;
            }

            // Rh — relativna vlaznost mora biti u opsegu [0, 100] %.
            if (sample.Rh < 0 || sample.Rh > 100)
            {
                message = "Rh mora biti u opsegu 0-100.";
                field = "Rh";
                return false;
            }

            // T — temperatura u Celzijusima, fizicki razuman opseg.
            if (sample.T < -90 || sample.T > 60)
            {
                message = "T mora biti u opsegu [-90, 60] °C.";
                field = "T";
                return false;
            }

            // Tpot — potencijalna temperatura, prema Kaggle opisu je u Kelvinima (K).
            if (sample.Tpot < 0 || sample.Tpot > 400)
            {
                message = "Tpot mora biti u razumnom opsegu za Kelvine (npr. [0, 400] K).";
                field = "Tpot";
                return false;
            }

            // Tdew — temperatura rosne tacke, ne sme premašiti T.
            if (sample.Tdew < -90 || sample.Tdew > 60)
            {
                message = "Tdew mora biti u opsegu [-90, 60] °C.";
                field = "Tdew";
                return false;
            }

            if (sample.Tdew > sample.T)
            {
                message = "Tdew ne moze biti veci od T (temperatura rosne tacke <= temperatura vazduha).";
                field = "Tdew";
                return false;
            }

            // Datum — ne sme biti podrazumevana (prazna) vrednost.
            if (sample.Date == DateTime.MinValue)
            {
                message = "Date nije validan.";
                field = "Date";
                return false;
            }

            message = string.Empty;
            field = string.Empty;
            return true;
        }

        private static double ParseSetting(string key, double fallback)
        {
            double value;
            string input = ConfigurationManager.AppSettings[key];
            if (!double.TryParse(input, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out value))
            {
                return fallback;
            }

            return value;
        }

        private void RaiseTransferStarted(string message)
        {
            if (OnTransferStarted != null)
            {
                OnTransferStarted(this, new TransferEventArgs { SessionId = currentSessionId, Message = message });
            }
        }

        private void RaiseSampleReceived(WeatherSample sample)
        {
            if (OnSampleReceived != null)
            {
                OnSampleReceived(this, new SampleEventArgs { SessionId = currentSessionId, Sample = sample });
            }
        }

        private void RaiseTransferCompleted(string message)
        {
            if (OnTransferCompleted != null)
            {
                OnTransferCompleted(this, new TransferEventArgs { SessionId = currentSessionId, Message = message });
            }
        }

        private void RaiseWarning(string warningType, string message, double currentValue, double expectedValue, DateTime date)
        {
            if (OnWarningRaised != null)
            {
                OnWarningRaised(this, new WarningEventArgs
                {
                    WarningType = warningType,
                    Message = message,
                    CurrentValue = currentValue,
                    ExpectedValue = expectedValue,
                    Date = date
                });
            }
        }
    }
}
