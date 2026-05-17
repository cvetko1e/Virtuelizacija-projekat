using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Common;

namespace Client
{
    public class CsvWeatherReader : IDisposable
    {
        private bool disposed;
        private readonly Logger logger;

        public CsvWeatherReader(Logger logger)
        {
            this.logger = logger;
        }

        /// <summary>
        /// Ucitava prvih <paramref name="count"/> validnih redova iz CSV fajla.
        /// Nevalidne redove i redove viska (posle count validnih) prijavljuje u log.
        /// </summary>
        /// <param name="path">Putanja do CSV fajla.</param>
        /// <param name="count">Maksimalan broj validnih uzoraka za ucitavanje (113).</param>
        /// <returns>Lista parsiranih WeatherSample objekata.</returns>
        public List<WeatherSample> ReadFirstSamples(string path, int count)
        {
            List<WeatherSample> samples = new List<WeatherSample>();
            int totalLines = 0;
            int invalidLines = 0;
            int excessLines = 0;

            logger.Info(string.Format("Pocetak ucitavanja CSV fajla: {0}", path));
            logger.Info(string.Format("Maksimalan broj uzoraka: {0}", count));

            if (!File.Exists(path))
            {
                logger.Error(string.Format("CSV fajl ne postoji: {0}", path));
                throw new FileNotFoundException("CSV fajl nije pronadjen.", path);
            }

            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                bool firstLine = true;

                while ((line = reader.ReadLine()) != null)
                {
                    if (firstLine)
                    {
                        firstLine = false;
                        if (line.Contains("T") && line.Contains("Date"))
                        {
                            logger.Info(string.Format("Zaglavlje preskoceno: {0}", line));
                            continue;
                        }
                    }

                    totalLines++;

                    // Parsiranje reda.
                    WeatherSample sample;
                    try
                    {
                        sample = ParseLine(line);
                    }
                    catch (Exception ex)
                    {
                        invalidLines++;
                        logger.Warning(string.Format(
                            "Nevalidan red #{0}: \"{1}\" | Razlog: {2}",
                            totalLines, TruncateLine(line, 80), ex.Message));
                        continue;
                    }

                    // Proveriti da li smo dostigli limit validnih uzoraka.
                    if (samples.Count >= count)
                    {
                        excessLines++;
                        if (excessLines <= 5)
                        {
                            logger.Info(string.Format(
                                "Red viska #{0} (posle {1} validnih): T={2:F2}, Date={3:O}",
                                totalLines, count, sample.T, sample.Date));
                        }
                        continue;
                    }

                    if (sample != null)
                    {
                        samples.Add(sample);
                    }
                }
            }

            // Sumarni izvestaj ucitavanja.
            logger.Info("--- Izvestaj ucitavanja CSV-a ---");
            logger.Info(string.Format("  Ukupno redova u fajlu: {0}", totalLines));
            logger.Info(string.Format("  Validnih ucitanih:     {0}", samples.Count));
            logger.Info(string.Format("  Nevalidnih redova:     {0}", invalidLines));
            logger.Info(string.Format("  Redova viska:          {0}", excessLines));
            if (excessLines > 5)
            {
                logger.Info(string.Format("  (prikazano prvih 5 redova viska, preostalih {0} izostavljeno)", excessLines - 5));
            }
            logger.Info("---------------------------------");

            return samples;
        }

        /// <summary>
        /// Parsira jedan CSV red u WeatherSample.
        /// Ocekivani format: T,Tpot,Tdew,Sh,Rh,Date
        /// Decimalni separator: tacka (InvariantCulture).
        /// </summary>
        public WeatherSample ParseLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                throw new FormatException("Prazan red.");
            }

            string[] parts = line.Split(',');
            if (parts.Length < 6)
            {
                throw new FormatException(string.Format(
                    "Nedovoljan broj kolona: ocekivano 6, dobijeno {0}.", parts.Length));
            }

            double t, tpot, tdew, sh, rh;
            DateTime date;

            if (!double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out t))
            {
                throw new FormatException(string.Format("Nevalidan format za T: \"{0}\".", parts[0].Trim()));
            }

            if (!double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out tpot))
            {
                throw new FormatException(string.Format("Nevalidan format za Tpot: \"{0}\".", parts[1].Trim()));
            }

            if (!double.TryParse(parts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out tdew))
            {
                throw new FormatException(string.Format("Nevalidan format za Tdew: \"{0}\".", parts[2].Trim()));
            }

            if (!double.TryParse(parts[3].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out sh))
            {
                throw new FormatException(string.Format("Nevalidan format za Sh: \"{0}\".", parts[3].Trim()));
            }

            if (!double.TryParse(parts[4].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out rh))
            {
                throw new FormatException(string.Format("Nevalidan format za Rh: \"{0}\".", parts[4].Trim()));
            }

            if (!DateTime.TryParse(parts[5].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
            {
                throw new FormatException(string.Format("Nevalidan format za Date: \"{0}\".", parts[5].Trim()));
            }

            return new WeatherSample(t, tpot, tdew, sh, rh, date);
        }

        /// <summary>
        /// Skracuje red na maxLen karaktera za citljiviji log ispis.
        /// </summary>
        private string TruncateLine(string line, int maxLen)
        {
            if (line.Length <= maxLen)
            {
                return line;
            }

            return line.Substring(0, maxLen) + "...";
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

        ~CsvWeatherReader()
        {
            Dispose(false);
        }
    }
}
