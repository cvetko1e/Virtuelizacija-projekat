using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel;
using Common;

namespace Client
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string logFile = Path.Combine(logDir, "client_" + timestamp + ".log");
            string csvIssuesLogFile = Path.Combine(logDir, "csv_issues_" + timestamp + ".csv");

            using (Logger logger = new Logger("Client", logFile))
            {
                ChannelFactory<IWeatherService> factory = null;
                IWeatherService proxy = null;
                IClientChannel channel = null;

                try
                {
                    factory = new ChannelFactory<IWeatherService>("WeatherServiceEndpoint");
                    proxy = factory.CreateChannel();
                    channel = (IClientChannel)proxy;

                    string csvPath = args.Length > 0 ? args[0] : "dataset.csv";
                    logger.Info(string.Format("CSV putanja: {0}", csvPath));

                    List<WeatherSample> samples;
                    using (CsvWeatherReader reader = new CsvWeatherReader(logger))
                    {
                        samples = reader.ReadFirstSamples(csvPath, 113, csvIssuesLogFile);
                    }

                    logger.Info(string.Format("Ucitano {0} uzoraka. Pokretanje sesije...", samples.Count));

                    SessionMeta meta = new SessionMeta
                    {
                        SessionId = Guid.NewGuid().ToString("N"),
                        StartedAt = DateTime.Now,
                        SourceFile = csvPath,
                        ExpectedSamples = samples.Count,
                        HeaderFields = new[] { "T", "Tpot", "Tdew", "Sh", "Rh", "Date" }
                    };

                    TransferResponse startResponse = proxy.StartSession(meta);
                    logger.Info(string.Format("StartSession: {0} | {1} | {2}",
                        startResponse.Success ? "ACK" : "NACK", startResponse.Status, startResponse.Message));

                    int ackCount = 0;
                    int nackCount = 0;

                    for (int i = 0; i < samples.Count; i++)
                    {
                        try
                        {
                            TransferResponse pushResponse = proxy.PushSample(samples[i]);

                            if (pushResponse.Success)
                            {
                                ackCount++;
                            }
                            else
                            {
                                nackCount++;
                                logger.Warning(string.Format("PushSample #{0}: NACK | {1}", i + 1, pushResponse.Message));
                            }
                        }
                        catch (FaultException<DataFormatFault> ex)
                        {
                            nackCount++;
                            logger.Warning(string.Format("PushSample #{0}: NACK/DataFormatFault [{1}] polje={2}: {3}",
                                i + 1, ex.Detail.Code ?? "?", ex.Detail.Field ?? "?", ex.Detail.Message));
                        }
                        catch (FaultException<ValidationFault> ex)
                        {
                            nackCount++;
                            logger.Warning(string.Format("PushSample #{0}: NACK/ValidationFault [{1}] polje={2}: {3}",
                                i + 1, ex.Detail.Code ?? "?", ex.Detail.Field ?? "?", ex.Detail.Message));
                        }

                        // Ispis progresa svakih 10 uzoraka.
                        if ((i + 1) % 10 == 0 || i == samples.Count - 1)
                        {
                            logger.Info(string.Format("Progres: {0}/{1} poslato (ACK={2}, NACK={3})",
                                i + 1, samples.Count, ackCount, nackCount));
                        }
                    }

                    TransferResponse endResponse = proxy.EndSession();
                    logger.Info(string.Format("EndSession: {0} | {1} | {2}",
                        endResponse.Success ? "ACK" : "NACK", endResponse.Status, endResponse.Message));

                    // Sumarni izvestaj klijentske strane.
                    logger.Info("--- Klijentski izvestaj ---");
                    logger.Info(string.Format("  Ukupno poslato:  {0}", samples.Count));
                    logger.Info(string.Format("  ACK:             {0}", ackCount));
                    logger.Info(string.Format("  NACK:            {0}", nackCount));
                    logger.Info("--------------------------");

                    channel.Close();
                    factory.Close();
                }
                catch (FaultException<DataFormatFault> ex)
                {
                    logger.Error(string.Format("DataFormatFault [{0}] polje={1}: {2}",
                        ex.Detail.Code ?? "?", ex.Detail.Field ?? "?", ex.Detail.Message));
                    Abort(channel, factory);
                }
                catch (FaultException<ValidationFault> ex)
                {
                    logger.Error(string.Format("ValidationFault [{0}] polje={1}: {2}",
                        ex.Detail.Code ?? "?", ex.Detail.Field ?? "?", ex.Detail.Message));
                    Abort(channel, factory);
                }
                catch (Exception ex)
                {
                    logger.Error(string.Format("Neocekivana greska: {0}", ex.Message));
                    Abort(channel, factory);
                }

                Console.WriteLine("Kraj rada klijenta. ENTER za izlaz...");
                Console.ReadLine();
            }
        }

        private static void Abort(IClientChannel channel, ChannelFactory<IWeatherService> factory)
        {
            if (channel != null)
            {
                channel.Abort();
            }

            if (factory != null)
            {
                factory.Abort();
            }
        }
    }
}
