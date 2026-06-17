using System;

namespace Common
{
    public class WarningEventArgs : EventArgs
    {
        public string WarningType { get; set; }
        public string Message { get; set; }
        public double CurrentValue { get; set; }
        public double ExpectedValue { get; set; }
        public DateTime Date { get; set; }
        public string Direction { get; set; }
    }
}
