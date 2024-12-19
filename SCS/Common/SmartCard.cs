using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [DataContract]
    public class SmartCard
    {
        private string subjetName;
        private string pin;

        public SmartCard(string username, string pinHashed)
        {
            this.subjetName = username;
            this.pin = pinHashed;
        }

        public SmartCard() { }

        [DataMember]
        public string SubjectName
        {
            get { return subjetName; }
            set { subjetName = value; }
        }
        [DataMember]
        public string PIN
        {
            get { return pin; }
            set { pin = value; }
        } 
    }
}
