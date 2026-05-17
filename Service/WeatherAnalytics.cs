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

        /// <summary>
        /// Racuna Heat Index prema Rothfusz regresionoj jednacini (Celsius / %).
        /// Formula:
        ///   HI = c1 + c2*T + c3*RH + c4*T*RH + c5*T^2 + c6*RH^2
        ///        + c7*T^2*RH + c8*T*RH^2 + c9*T^2*RH^2
        ///
        /// Koeficijenti su preuzeti iz NWS/Rothfusz specifikacije konvertovane u Celsius.
        /// Napomena: formula je validna za T >= 27 °C i RH >= 40 %. Za nize vrednosti
        /// vraca se jednostavnija aproksimacija.
        /// </summary>
        public double CalculateHeatIndex(double temperature, double relativeHumidity)
        {
            double T = temperature;
            double RH = relativeHumidity;

            // Za temperature ispod 27 °C koristimo Steadman-ovu pojednostavljenu aproksimaciju.
            if (T < 27.0)
            {
                return 0.5 * (T + 61.0 + ((T - 68.0) * 1.2) + (RH * 0.094));
            }

            // Rothfusz regresiona jednacina (Celsius, RH u procentima).
            double c1 = -8.78469475556;
            double c2 = 1.61139411;
            double c3 = 2.33854883889;
            double c4 = -0.14611605;
            double c5 = -0.012308094;
            double c6 = -0.0164248277778;
            double c7 = 0.002211732;
            double c8 = 0.00072546;
            double c9 = -0.000003582;

            double HI = c1
                       + c2 * T
                       + c3 * RH
                       + c4 * T * RH
                       + c5 * T * T
                       + c6 * RH * RH
                       + c7 * T * T * RH
                       + c8 * T * RH * RH
                       + c9 * T * T * RH * RH;

            // Korekcija za nisku vlaznost pri visokim temperaturama.
            if (RH < 13.0 && T > 33.0 && T < 44.5)
            {
                double adj = ((13.0 - RH) / 4.0) * Math.Sqrt((17.0 - Math.Abs(T - 35.0)) / 17.0);
                HI -= adj;
            }

            // Korekcija za visoku vlaznost pri umereno visokim temperaturama.
            if (RH > 85.0 && T > 27.0 && T < 30.0)
            {
                double adj = ((RH - 85.0) / 10.0) * ((30.0 - T) / 5.0);
                HI += adj;
            }

            return HI;
        }
    }
}
