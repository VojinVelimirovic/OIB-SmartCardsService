using Common;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.ServiceModel;
using System.ServiceModel.Security;

namespace Client
{
    internal class ClientProxyATM : ChannelFactory<IATMService>, IATMService, IDisposable
    {
        IATMService factory;

        public ClientProxyATM(NetTcpBinding binding, EndpointAddress address) 
            : base(binding, address)
        {
            this.Credentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.ChainTrust;
            this.Credentials.ServiceCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;

            string clientName = CertManager.ParseName(WindowsIdentity.GetCurrent().Name);
            string certCN = clientName + "_sign";

            this.Credentials.ClientCertificate.SetCertificate(
                StoreLocation.LocalMachine,
                StoreName.My,
                X509FindType.FindBySubjectName,
                certCN
            );
            factory = this.CreateChannel();
        }

        public void TestCommunication()
        {
            try
            {
                factory.TestCommunication();
                Console.WriteLine("[ATM PROXY] Request successfully forwarded");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ATM PROXY] ATM reported failure: {ex.Message}");
                throw; // Re-throw to maintain exception propagation
            }
        }
        public void SignedMessage(SignedRequest request)
        {
            factory.SignedMessage(request);
        }

        public bool AuthenticateUser(string username, int pin)
        {
            return factory.AuthenticateUser(username, pin);
        }
        public double? GetBalance(string username)
        {
            return factory.GetBalance(username);
        }
        public bool Deposit(string username, double amount)
        {
            return factory.Deposit(username, amount);
        }
        public bool Withdraw(string username, double amount)
        {
            return factory.Withdraw(username, amount);
        }
        public string[] GetActiveUserAccounts()
        {
            return factory.GetActiveUserAccounts();
        }
        protected override void OnClosed()
        {
            base.OnClosed();
            if (factory is IClientChannel clientChannel)
            {
                if (clientChannel.State == CommunicationState.Faulted)
                    clientChannel.Abort();
                else
                    clientChannel.Close();
            }
        }
        public void Dispose()
        {
            if (factory != null)
            {
                ((IClientChannel)factory).Close();
            }
            this.Close();
        }
    }
}
