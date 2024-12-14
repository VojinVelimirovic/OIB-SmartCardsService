using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [DataContract]
    public class SecurityException
    {
        string message;

        [DataMember]
        public string Message { get => message; set => message = value; }

        public SecurityException(string message)
        {
            Message = message;
        }
    }
}
