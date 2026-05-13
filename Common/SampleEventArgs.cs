using System;

namespace Common
{
    public class SampleEventArgs : EventArgs
    {
        public string SessionId { get; set; }
        public WeatherSample Sample { get; set; }
    }
}
