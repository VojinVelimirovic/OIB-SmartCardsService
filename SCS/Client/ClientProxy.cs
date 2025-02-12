using Common;
using System;
using System.ServiceModel;

namespace Client
{
    internal class ClientProxy : ChannelFactory<IATMService>, IATMService, IDisposable
    {
        IATMService factory;

        public ClientProxy(NetTcpBinding binding, string address) : base(binding, address)
        {
            factory = this.CreateChannel();
        }

        public bool AuthenticateUser(string username, int pin)
        {
            return factory.AuthenticateUser(username, pin);
        }

        public void Deposit(string username, double amount)
        {
            factory.Deposit(username, amount);
        }

        public bool Withdraw(string username, double amount)
        {
            return factory.Withdraw(username, amount);
        }

        public string[] GetActiveUserAccounts()
        {
            return factory.GetActiveUserAccounts();
        }

    }
}
