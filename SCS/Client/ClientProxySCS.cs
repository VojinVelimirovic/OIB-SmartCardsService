using Common;
using System;
using System.ServiceModel;

namespace Client
{
    internal class ClientProxySCS : ChannelFactory<ISmartCardsService>, ISmartCardsService, IDisposable
    {
        ISmartCardsService _smartCardService;
        public ClientProxySCS(NetTcpBinding binding, string address) : base(binding, address)
        {
            _smartCardService = this.CreateChannel();
        }
        public bool TestCommunication()
        {
            return _smartCardService.TestCommunication();
        }

        public void CreateSmartCard(string username, int pin)
        {
            _smartCardService.CreateSmartCard(username, pin);
        }

        public bool ValidateSmartCard(string username, int pin)
        {
            return _smartCardService.ValidateSmartCard(username, pin);
        }

        public void UpdatePin(string username, int oldPin, int newPin)
        {
            _smartCardService.UpdatePin(username, oldPin, newPin);
        }

    }
}
