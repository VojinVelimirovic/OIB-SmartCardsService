using Common;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.ServiceModel;

namespace Client
{
    internal class ClientProxySCS : ChannelFactory<ISmartCardsService>, ISmartCardsService, IDisposable
    {
        ISmartCardsService factory;
        public ClientProxySCS(NetTcpBinding binding, EndpointAddress address)
            : base(binding, address)
        {
            this.Credentials.ServiceCertificate.Authentication.CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.ChainTrust;
            this.Credentials.ServiceCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;

            string clientName = CertManager.ParseName(WindowsIdentity.GetCurrent().Name);
            this.Credentials.ClientCertificate.Certificate = CertManager.GetClientCertificate(clientName);

            factory = this.CreateChannel();
        }
        public void TestCommunication()
        {
            try
            {
                factory.TestCommunication();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }

        public void CreateSmartCard(string username, int pin)
        {
            factory.CreateSmartCard(username, pin);
        }

        public bool ValidateSmartCard(string username, int pin)
        {
            return factory.ValidateSmartCard(username, pin);
        }

        public void UpdatePin(string username, int oldPin, int newPin)
        {
            factory.UpdatePin(username, oldPin, newPin);
        }

        public void SignedMessage(SignedRequest request)
        {
            throw new NotImplementedException();
        }

        public void ReplicateSmartCard(SmartCard card)
        {
            throw new NotImplementedException();
        }
    }
}
