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
        private StreamWriter invalidLogWriter;

        public List<WeatherSample> ReadFirstSamples(string path, int count)
        {
            List<WeatherSample> samples = new List<WeatherSample>();

            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "invalid_rows.log");
            invalidLogWriter = new StreamWriter(logPath, true);

            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                bool firstLine = true;

                while ((line = reader.ReadLine()) != null && samples.Count < count)
                {
                    if (firstLine)
                    {
                        firstLine = false;
                        if (line.Contains("T") && line.Contains("Date"))
                        {
                            continue;
                        }
                    }

                    WeatherSample sample;
                    try
                    {
                        sample = ParseLine(line);
                    }
                    catch (Exception ex)
                    {
                        invalidLogWriter.WriteLine(DateTime.Now.ToString("s") + " | " + line + " | " + ex.Message);
                        invalidLogWriter.Flush();
                        continue;
                    }

                    if (sample != null)
                    {
                        samples.Add(sample);
                    }
                }
            }

            return samples;
        }

        public WeatherSample ParseLine(string line)
        {
            string[] parts = line.Split(',');
            if (parts.Length < 6)
            {
                throw new FormatException("Nedovoljan broj kolona.");
            }

            return new WeatherSample(
                double.Parse(parts[0], CultureInfo.InvariantCulture),
                double.Parse(parts[1], CultureInfo.InvariantCulture),
                double.Parse(parts[2], CultureInfo.InvariantCulture),
                double.Parse(parts[3], CultureInfo.InvariantCulture),
                double.Parse(parts[4], CultureInfo.InvariantCulture),
                DateTime.Parse(parts[5], CultureInfo.InvariantCulture));
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
                    if (invalidLogWriter != null)
                    {
                        invalidLogWriter.Dispose();
                        invalidLogWriter = null;
                    }
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
