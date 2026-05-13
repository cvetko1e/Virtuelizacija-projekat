using System;
using System.Collections.Generic;
using System.ServiceModel;
using Common;

namespace Client
{
    internal class Program
    {
        private static void Main(string[] args)
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
                List<WeatherSample> samples;

                using (CsvWeatherReader reader = new CsvWeatherReader())
                {
                    samples = reader.ReadFirstSamples(csvPath, 113);
                }

                SessionMeta meta = new SessionMeta
                {
                    SessionId = Guid.NewGuid().ToString("N"),
                    StartedAt = DateTime.Now,
                    SourceFile = csvPath,
                    ExpectedSamples = samples.Count,
                    HeaderFields = new[] { "T", "Tpot", "Tdew", "Sh", "Rh", "Date" }
                };

                TransferResponse startResponse = proxy.StartSession(meta);
                Console.WriteLine("StartSession: {0} | {1}", startResponse.Success, startResponse.Status);

                for (int i = 0; i < samples.Count; i++)
                {
                    TransferResponse pushResponse = proxy.PushSample(samples[i]);
                    Console.WriteLine("PushSample #{0}: {1} | {2} | {3}", i + 1, pushResponse.Success ? "ACK" : "NACK", pushResponse.Status, pushResponse.Message);
                }

                TransferResponse endResponse = proxy.EndSession();
                Console.WriteLine("EndSession: {0} | {1}", endResponse.Success, endResponse.Status);

                channel.Close();
                factory.Close();
            }
            catch (FaultException<DataFormatFault> ex)
            {
                Console.WriteLine("DataFormatFault: " + ex.Detail.Message);
                Abort(channel, factory);
            }
            catch (FaultException<ValidationFault> ex)
            {
                Console.WriteLine("ValidationFault: " + ex.Detail.Message);
                Abort(channel, factory);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Greska: " + ex.Message);
                Abort(channel, factory);
            }

            Console.WriteLine("Kraj rada klijenta. ENTER za izlaz...");
            Console.ReadLine();
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
