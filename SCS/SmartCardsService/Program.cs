using Common;
using System;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Security;

namespace SmartCardsService
{
    internal class Program
    {
        static void Main(string[] args)
        {
            NetTcpBinding binding = new NetTcpBinding();
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Certificate;

            var replicationBinding = new NetTcpBinding();
            replicationBinding.Security.Mode = SecurityMode.Transport;

            string address = "net.tcp://localhost:9999/SmartCardsService";
            string replicationAddress = "net.tcp://localhost:9001/SmartCardsReplication";

            ServiceHost host = new ServiceHost(typeof(SmartCardsService));
            host.AddServiceEndpoint(typeof(ISmartCardsService), binding, address);
            host.AddServiceEndpoint(typeof(ISmartCardsService), replicationBinding, replicationAddress);

            host.Credentials.ClientCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.ChainTrust;
            host.Credentials.ClientCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
            host.Credentials.ServiceCertificate.Certificate = CertManager.GetCertificateFromStorage(StoreName.My, StoreLocation.LocalMachine, "wcfservice");

            try
            {
                host.Open();
                Console.WriteLine("SmartCardsService is running at: " + address);
                Console.WriteLine("Press Enter to stop the service.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SmartCardsService failed to start: {ex.Message}");
            }

            host.Close();
        }
    }
}