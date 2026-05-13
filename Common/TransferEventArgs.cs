using System;

namespace Common
{
    public class TransferEventArgs : EventArgs
    {
        public string SessionId { get; set; }
        public string Message { get; set; }
    }
}
