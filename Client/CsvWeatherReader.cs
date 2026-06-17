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

        public List<WeatherSample> ReadFirstSamples(string path, int count)
        {
            return ReadFirstSamples(path, count, "csv_issues.log");
        }

        /// <summary>
        /// Ucitava ceo CSV dataset, izdvaja prvih <paramref name="count"/> validnih redova
        /// i sve nevalidne/redove viska upisuje u poseban log.
        /// </summary>
        /// <param name="path">Putanja do CSV fajla.</param>
        /// <param name="count">Maksimalan broj validnih uzoraka za slanje (113).</param>
        /// <param name="issuesLogPath">Poseban log za nevalidne i visak redova.</param>
        /// <returns>Lista prvih validnih WeatherSample objekata.</returns>
        public List<WeatherSample> ReadFirstSamples(string path, int count, string issuesLogPath)
        {
            List<WeatherSample> allValidSamples = new List<WeatherSample>();
            List<int> allValidLineNumbers = new List<int>();
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

            string issuesDirectory = Path.GetDirectoryName(issuesLogPath);
            if (!string.IsNullOrEmpty(issuesDirectory))
            {
                Directory.CreateDirectory(issuesDirectory);
            }

            using (StreamReader reader = new StreamReader(path))
            using (StreamWriter issuesWriter = new StreamWriter(issuesLogPath, false))
            {
                issuesWriter.WriteLine("LineNumber,Issue,RawLine,Reason");

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
                        WriteIssue(issuesWriter, totalLines, "INVALID", line, ex.Message);
                        continue;
                    }

                    allValidSamples.Add(sample);
                    allValidLineNumbers.Add(totalLines);
                }

                for (int i = count; i < allValidSamples.Count; i++)
                {
                    excessLines++;
                    WeatherSample sample = allValidSamples[i];
                    string formattedSample = FormatSample(sample);
                    WriteIssue(issuesWriter, allValidLineNumbers[i], "EXCESS", formattedSample, "Red posle prvih " + count + " validnih uzoraka.");

                    if (excessLines <= 5)
                    {
                        logger.Info(string.Format(
                            "Red viska #{0} (posle {1} validnih): T={2:F2}, Date={3:O}",
                            i + 1, count, sample.T, sample.Date));
                    }
                }
            }

            int returnedCount = Math.Min(count, allValidSamples.Count);
            List<WeatherSample> samples = allValidSamples.GetRange(0, returnedCount);

            // Sumarni izvestaj ucitavanja.
            logger.Info("--- Izvestaj ucitavanja CSV-a ---");
            logger.Info(string.Format("  Ukupno redova u fajlu: {0}", totalLines));
            logger.Info(string.Format("  Validnih ucitanih:     {0}", samples.Count));
            logger.Info(string.Format("  Validnih u datasetu:   {0}", allValidSamples.Count));
            logger.Info(string.Format("  Nevalidnih redova:     {0}", invalidLines));
            logger.Info(string.Format("  Redova viska:          {0}", excessLines));
            logger.Info(string.Format("  Izdvojeni CSV log:     {0}", issuesLogPath));
            if (excessLines > 5)
            {
                logger.Info(string.Format("  (prikazano prvih 5 redova viska, preostalih {0} izostavljeno)", excessLines - 5));
            }
            logger.Info("---------------------------------");

            return samples;
        }

        private void WriteIssue(StreamWriter writer, int lineNumber, string issue, string rawLine, string reason)
        {
            writer.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3}",
                lineNumber,
                EscapeCsv(issue),
                EscapeCsv(rawLine),
                EscapeCsv(reason)));
        }

        private string FormatSample(WeatherSample sample)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0},{1},{2},{3},{4},{5:O}",
                sample.T,
                sample.Tpot,
                sample.Tdew,
                sample.Sh,
                sample.Rh,
                sample.Date);
        }

        private string EscapeCsv(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
            {
                return value;
            }

            return "\"" + value.Replace("\"", "\"\"") + "\"";
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

            if (sh <= 0)
            {
                throw new FormatException(string.Format("Sh mora biti > 0, dobijeno: {0}.", sh));
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
