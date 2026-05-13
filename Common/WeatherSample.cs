using System;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class WeatherSample
    {
        [DataMember]
        public double T { get; set; }

        [DataMember]
        public double Tpot { get; set; }

        [DataMember]
        public double Tdew { get; set; }

        [DataMember]
        public double Sh { get; set; }

        [DataMember]
        public double Rh { get; set; }

        [DataMember]
        public DateTime Date { get; set; }

        public WeatherSample()
        {
        }

        public WeatherSample(double t, double tpot, double tdew, double sh, double rh, DateTime date)
        {
            T = t;
            Tpot = tpot;
            Tdew = tdew;
            Sh = sh;
            Rh = rh;
            Date = date;
        }
    }
}
