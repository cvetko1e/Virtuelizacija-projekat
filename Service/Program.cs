using System;
using System.IO;
using System.ServiceModel;
using Common;

namespace Service
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            string logFile = Path.Combine(logDir, "service_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".log");

            using (Logger logger = new Logger("Service", logFile))
            {
                if (HasArgument(args, "--simulate-dispose"))
                {
                    string storagePath = System.Configuration.ConfigurationManager.AppSettings["storagePath"] ?? "Measurements";
                    bool success = ResourceDisposalSimulation.Run(storagePath, logger);
                    logger.Info(success
                        ? "Simulacija Dispose pattern-a je uspesna."
                        : "Simulacija Dispose pattern-a nije uspela.");

                    if (!success)
                    {
                        Environment.ExitCode = 1;
                    }

                    return;
                }

                logger.Info("Inicijalizacija WeatherService...");

                // Kreiramo singleton instancu servisa rucno
                // kako bismo mogli da se pretplatimo na dogadjaje PRE otvaranja host-a.
                WeatherService serviceInstance = new WeatherService();

                using (WeatherEventHandler eventHandler = new WeatherEventHandler(logger))
                {
                    // Pretplata na sve dogadjaje servisa.
                    eventHandler.Subscribe(serviceInstance);
                    logger.Info("Event handler registrovan na servis.");

                    using (ServiceHost host = new ServiceHost(serviceInstance))
                    {
                        try
                        {
                            host.Open();

                            string baseAddress = "net.tcp://localhost:4000/WeatherService";
                            logger.Info(string.Format("WeatherService je pokrenut na {0}.", baseAddress));
                            logger.Info("Konfigurisani pragovi:");
                            logger.Info(string.Format("  SH_threshold:       {0}",
                                System.Configuration.ConfigurationManager.AppSettings["SH_threshold"] ?? "0.002"));
                            logger.Info(string.Format("  HI_max_threshold:   {0}",
                                System.Configuration.ConfigurationManager.AppSettings["HI_max_threshold"] ?? "5"));
                            logger.Info(string.Format("  OUT_OF_BAND_PERCENT: {0}",
                                System.Configuration.ConfigurationManager.AppSettings["OUT_OF_BAND_PERCENT"] ?? "25"));

                            Console.WriteLine();
                            Console.WriteLine("Pritisnite ENTER za gasenje servisa...");
                            Console.ReadLine();

                            host.Close();
                            logger.Info("Servis zatvoren.");
                        }
                        catch (Exception ex)
                        {
                            logger.Error(string.Format("Greska pri pokretanju servisa: {0}", ex.Message));
                            host.Abort();
                        }
                    }

                    // Odjava sa dogadjaja.
                    eventHandler.Unsubscribe(serviceInstance);
                    logger.Info("Event handler odjavljen. Kraj rada.");
                }
            }
        }

        private static bool HasArgument(string[] args, string expected)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], expected, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
