using System;
using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class SessionMeta
    {
        [DataMember]
        public string SessionId { get; set; }

        [DataMember]
        public DateTime StartedAt { get; set; }

        [DataMember]
        public string SourceFile { get; set; }

        [DataMember]
        public int ExpectedSamples { get; set; }

        [DataMember]
        public string[] HeaderFields { get; set; }
    }
}
