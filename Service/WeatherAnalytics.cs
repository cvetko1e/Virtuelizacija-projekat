using System;

namespace Service
{
    public class WeatherAnalytics
    {
        public double CalculateDelta(double current, double previous)
        {
            return current - previous;
        }

        public double CalculateRunningMean(double sum, int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            return sum / count;
        }

        public double CalculateHeatIndex(double temperature, double relativeHumidity)
        {
            // Jednostavna pocetna formula za template.
            return temperature + (0.1 * relativeHumidity);
        }
    }
}
