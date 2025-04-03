using Common;
using System;
using System.ServiceModel;

namespace Client
{
    internal class ClientProxyATM : ChannelFactory<IATMService>, IATMService, IDisposable
    {
        IATMService _atmService;

        public ClientProxyATM(NetTcpBinding binding, string address) : base(binding, address)
        {
            _atmService = this.CreateChannel();
        }
        public bool TestConnection()
        {
            try
            {
                return this.Ping(); // Call the Ping method on the server
            }
            catch
            {
                return false; // If the call fails, the server is unreachable
            }
        }

        public bool Ping()
        {
            return _atmService.Ping();
        }

        public bool AuthenticateUser(string username, int pin)
        {
            return _atmService.AuthenticateUser(username, pin);
        }
        public double? GetBalance(string username)
        {
            return _atmService.GetBalance(username);
        }
        public bool Deposit(string username, double amount)
        {
            return _atmService.Deposit(username, amount);
        }

        public bool Withdraw(string username, double amount)
        {
            return _atmService.Withdraw(username, amount);
        }

        public string[] GetActiveUserAccounts()
        {
            return _atmService.GetActiveUserAccounts();
        }
        protected override void OnClosed()
        {
            base.OnClosed();
            if (_atmService is IClientChannel clientChannel)
            {
                if (clientChannel.State == CommunicationState.Faulted)
                    clientChannel.Abort();
                else
                    clientChannel.Close();
            }
        }
    }
}
