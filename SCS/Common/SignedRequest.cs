using System.Runtime.Serialization;

namespace Common
{
    [DataContract]
    public class SignedRequest
    {
        [DataMember]
        public string Message { get; set; }

        [DataMember]
        public byte[] Signature { get; set; }

        [DataMember]
        public string SenderName { get; set; } // for certificate lookup on intermediary
    }
}
