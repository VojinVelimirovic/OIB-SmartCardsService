using System.ServiceModel;

namespace Common
{
    [ServiceContract]
    public interface ISmartCardsService_ATM
    {
        [OperationContract]
        bool ValidateSmartCard_FromATM(string username, string hashedPin);
    }
}
