using System;
using System.ServiceModel;

namespace Common
{
    [ServiceContract]
    public interface IATMService
    {
        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        void TestCommunication(); // for testing purposes

        [OperationContract]
        void SignedMessage(SignedRequest request); // for testing purposes

        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        bool AuthenticateUser(string username, int pin);

        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        bool Deposit(string username, double amount);

        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        bool Withdraw(string username, double amount, out string message);
        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        double? GetBalance(string username);

        [OperationContract]
        [FaultContract(typeof(SecurityException))]
        string[] GetActiveUserAccounts(byte[] clientCert); // Only for Managers

    }
}
