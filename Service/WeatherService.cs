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
        private double? previousHI;
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
                throw new FaultException<ValidationFault>(new ValidationFault { Message = "Meta podaci ne postoje." });
            }

            if (string.IsNullOrWhiteSpace(meta.SessionId))
            {
                throw new FaultException<ValidationFault>(new ValidationFault { Message = "SessionId je obavezan." });
            }

            if (meta.ExpectedSamples <= 0)
            {
                throw new FaultException<ValidationFault>(new ValidationFault { Message = "ExpectedSamples mora biti > 0." });
            }

            currentSessionId = meta.SessionId;
            sessionStarted = true;
            previousSh = null;
            previousHI = null;
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
                throw new FaultException<ValidationFault>(new ValidationFault { Message = "Sesija nije pokrenuta." });
            }

            string validationMessage;
            if (!ValidateSample(sample, out validationMessage))
            {
                storage.WriteReject(sample, validationMessage);
                return new TransferResponse
                {
                    Success = false,
                    Message = "NACK: " + validationMessage,
                    Status = TransferStatus.FAILED
                };
            }

            storage.WriteMeasurement(sample);
            sampleCount++;
            shSum += sample.Sh;
            RaiseSampleReceived(sample);

            if (previousSh.HasValue)
            {
                double deltaSh = analytics.CalculateDelta(sample.Sh, previousSh.Value);
                if (Math.Abs(deltaSh) > shThreshold)
                {
                    RaiseWarning("SHSpike", deltaSh >= 0 ? "SH iznad ocekivanog." : "SH ispod ocekivanog.", sample.Sh, previousSh.Value, sample.Date);
                }
            }

            double meanSh = analytics.CalculateRunningMean(shSum, sampleCount);
            double lowerBand = meanSh * (1.0 - outOfBandPercent / 100.0);
            double upperBand = meanSh * (1.0 + outOfBandPercent / 100.0);
            if (sample.Sh < lowerBand || sample.Sh > upperBand)
            {
                RaiseWarning("OutOfBandWarning", "SH je van opsega u odnosu na running mean.", sample.Sh, meanSh, sample.Date);
            }

            double currentHi = analytics.CalculateHeatIndex(sample.T, sample.Rh);
            if (previousHI.HasValue)
            {
                double deltaHi = analytics.CalculateDelta(currentHi, previousHI.Value);
                if (Math.Abs(deltaHi) > hiMaxThreshold)
                {
                    RaiseWarning("HISpike", "Heat Index skok je veci od dozvoljenog.", currentHi, previousHI.Value, sample.Date);
                }
            }

            previousSh = sample.Sh;
            previousHI = currentHi;

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
                throw new FaultException<ValidationFault>(new ValidationFault { Message = "Sesija nije aktivna." });
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

        private bool ValidateSample(WeatherSample sample, out string message)
        {
            if (sample == null)
            {
                message = "Sample je null.";
                return false;
            }

            if (sample.Sh <= 0)
            {
                message = "Sh mora biti > 0.";
                return false;
            }

            if (sample.Rh < 0 || sample.Rh > 100)
            {
                message = "Rh mora biti u opsegu 0-100.";
                return false;
            }

            if (sample.Date == DateTime.MinValue)
            {
                message = "Date nije validan.";
                return false;
            }

            message = string.Empty;
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
