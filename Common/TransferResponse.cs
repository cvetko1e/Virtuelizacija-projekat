using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class TransferResponse
    {
        [DataMember]
        public bool Success { get; set; }

        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public TransferStatus Status { get; set; }
    }
}
