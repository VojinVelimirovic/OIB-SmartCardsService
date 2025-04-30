using System;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Security;
using Common;

namespace ATM
{
    class Program
    {
        static void Main(string[] args)
        {
            var certBinding = new NetTcpBinding(SecurityMode.Transport);
            certBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;

            var host = new ServiceHost(typeof(ATMService), new Uri("net.tcp://localhost:10000/ATMService"));
            host.AddServiceEndpoint(typeof(IATMService), certBinding, "");

            host.Credentials.ClientCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.ChainTrust;
            host.Credentials.ClientCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
            host.Credentials.ServiceCertificate.Certificate = CertManager.GetCertificateFromStorage(
                StoreName.My, StoreLocation.LocalMachine, "oib_atm");

            if (!CertManager.CurrentUserHasCertificate("oib_atm"))
            {
                Console.WriteLine("Current user does not have the required certificate. Exiting...");
                Console.ReadLine();
                return;
            }

            host.Open();
            Console.WriteLine("ATM service running. Press Enter to stop.");
            Console.ReadLine();

            host.Close();
        }
    }
}
