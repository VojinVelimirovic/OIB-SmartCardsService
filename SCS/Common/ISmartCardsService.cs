using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    [ServiceContract]
    public interface ISmartCardsService
    {
        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        void TestCommunication(string message);

        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        void CreateSmartCard(string username, int pin);

        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        bool ValidateSmartCard(string username, int pin);

        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        void UpdatePin(string username, int oldPin, int newPin);

        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        bool AddBalance(string username, int pin, float sum);

        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        bool RemoveBalance(string username, int pin, float sum);

        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        List<string> GetActiveUserAccounts();
    }
}
