using System;
using System.Configuration;
using System.ServiceModel;
using Common;

namespace Service
{
    [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
    public class WeatherService : IWeatherService
    {
        private static readonly string[] RequiredHeaderFields = { "T", "Tpot", "Tdew", "Sh", "Rh", "Date" };

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

            ValidateHeaderFields(meta.HeaderFields);

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
            try
            {
                ValidateSampleFormat(sample);
                ValidateSampleRange(sample);
            }
            catch (FaultException<DataFormatFault> ex)
            {
                storage.WriteReject(sample, ex.Detail.Message);
                throw;
            }
            catch (FaultException<ValidationFault> ex)
            {
                storage.WriteReject(sample, ex.Detail.Message);
                throw;
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

        private static void ValidateHeaderFields(string[] headerFields)
        {
            if (headerFields.Length != RequiredHeaderFields.Length)
            {
                ThrowValidationFault(
                    "HeaderFields mora tacno odgovarati meta-zaglavlju {T,Tpot,Tdew,Sh,Rh,Date}.",
                    "HeaderFields",
                    "INVALID_HEADER");
            }

            for (int i = 0; i < RequiredHeaderFields.Length; i++)
            {
                if (!string.Equals(headerFields[i], RequiredHeaderFields[i], StringComparison.OrdinalIgnoreCase))
                {
                    ThrowValidationFault(
                        string.Format("HeaderFields[{0}] mora biti {1}.", i, RequiredHeaderFields[i]),
                        "HeaderFields",
                        "INVALID_HEADER");
                }
            }
        }

        private static void ValidateSampleFormat(WeatherSample sample)
        {
            if (sample == null)
            {
                ThrowDataFormatFault("Sample objekat je null.", "sample", "NULL_OBJECT");
            }

            EnsureFinite(sample.T, "T");
            EnsureFinite(sample.Tpot, "Tpot");
            EnsureFinite(sample.Tdew, "Tdew");
            EnsureFinite(sample.Sh, "Sh");
            EnsureFinite(sample.Rh, "Rh");

            if (sample.Date == DateTime.MinValue)
            {
                ThrowDataFormatFault("Date nije parsiran ili je prazan.", "Date", "INVALID_FORMAT");
            }
        }

        private static void EnsureFinite(double value, string field)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                ThrowDataFormatFault(
                    string.Format("Polje {0} mora biti numericka konacna vrednost.", field),
                    field,
                    "INVALID_NUMBER");
            }
        }

        private static void ValidateSampleRange(WeatherSample sample)
        {
            if (sample.Sh <= 0)
            {
                ThrowValidationFault("Sh mora biti > 0.", "Sh", "OUT_OF_RANGE");
            }

            if (sample.Rh < 0 || sample.Rh > 100)
            {
                ThrowValidationFault("Rh mora biti u opsegu 0-100 %.", "Rh", "OUT_OF_RANGE");
            }

            if (sample.T < -90 || sample.T > 60)
            {
                ThrowValidationFault("T mora biti u opsegu [-90, 60] C.", "T", "OUT_OF_RANGE");
            }

            if (sample.Tpot < 0 || sample.Tpot > 400)
            {
                ThrowValidationFault("Tpot mora biti u razumnom opsegu za Kelvine (npr. [0, 400] K).", "Tpot", "OUT_OF_RANGE");
            }

            if (sample.Tdew < -90 || sample.Tdew > 60)
            {
                ThrowValidationFault("Tdew mora biti u opsegu [-90, 60] C.", "Tdew", "OUT_OF_RANGE");
            }

            if (sample.Tdew > sample.T)
            {
                ThrowValidationFault("Tdew ne moze biti veci od T (temperatura rosne tacke <= temperatura vazduha).", "Tdew", "OUT_OF_RANGE");
            }
        }

        private static void ThrowDataFormatFault(string message, string field, string code)
        {
            throw new FaultException<DataFormatFault>(new DataFormatFault
            {
                Message = message,
                Field = field,
                Code = code
            });
        }

        private static void ThrowValidationFault(string message, string field, string code)
        {
            throw new FaultException<ValidationFault>(new ValidationFault
            {
                Message = message,
                Field = field,
                Code = code
            });
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
