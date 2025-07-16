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
        private bool _initializationFailed = false;
        public bool InitializationFailed => _initializationFailed;

        public ClientProxyATM(NetTcpBinding binding, EndpointAddress address) 
            : base(binding, address)
        {
            this.Credentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.ChainTrust;
            this.Credentials.ServiceCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;

            string clientName = CertManager.ParseName(WindowsIdentity.GetCurrent().Name);
            string certCN = clientName + "_sign";

            try
            { 
                this.Credentials.ClientCertificate.SetCertificate(
                    StoreLocation.LocalMachine,
                    StoreName.My,
                    X509FindType.FindBySubjectName,
                    certCN
                );
                factory = this.CreateChannel();
            }
            catch (Exception ex)
            {
                _initializationFailed = true;
                ColorfulConsole.WriteError("Current user does not have the required certificate.");
                ColorfulConsole.WriteError($"Details: {ex.Message}");
            }
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

        public bool AuthenticateUser(string username, int pin, byte[] clientCert = null)
        {
            var cert = this.Credentials.ClientCertificate.Certificate;
            byte[] certBytes = cert.Export(X509ContentType.Cert);
            return factory.AuthenticateUser(username, pin, certBytes);
        }

        public double? GetBalance(string username)
        {
            return factory.GetBalance(username);
        }

        public bool Deposit(string username, double amount)
        {
            return factory.Deposit(username, amount);
        }

        public bool Withdraw(string username, double amount, out string message)
        {
            message = string.Empty;
            return factory.Withdraw(username, amount, out message);
        }

        public string[] GetActiveUserAccounts()
        {
            var cert = this.Credentials.ClientCertificate.Certificate;
            byte[] certBytes = cert.Export(X509ContentType.Cert);
            return factory.GetActiveUserAccounts(certBytes);
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

        public string[] GetActiveUserAccounts(byte[] clientCert)
        {
            throw new NotImplementedException();
        }
    }
}
