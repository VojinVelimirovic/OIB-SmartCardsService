using Common;
using System;
using System.ServiceModel;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Security;

namespace SmartCardsServiceBackup
{
    internal class Program
    {
        static void Main(string[] args)
        {
            NetTcpBinding binding = new NetTcpBinding();
            binding.Security.Mode = SecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Certificate;

            NetTcpBinding replicationBinding = new NetTcpBinding();
            replicationBinding.Security.Mode = SecurityMode.None;

            string address = "net.tcp://localhost:9998/SmartCardsService";
            string replicationAddress = "net.tcp://localhost:9000/SmartCardsReplication";

            ServiceHost host = new ServiceHost(typeof(SmartCardsService.SmartCardsService));
            host.AddServiceEndpoint(typeof(ISmartCardsService), binding, address);
            host.AddServiceEndpoint(typeof(ISmartCardsService), replicationBinding, replicationAddress);

            host.Credentials.ClientCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.ChainTrust;
            host.Credentials.ClientCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
            host.Credentials.ServiceCertificate.Certificate = CertManager.GetCertificateFromStorage(StoreName.My, StoreLocation.LocalMachine, "wcfservice");

            try
            {
                host.Open();
                Console.WriteLine("SmartCardsService Backup is running at: " + address);
                Console.WriteLine("Press Enter to stop the service.");
                Console.ReadLine();
                host.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SmartCardsService Backup failed to start: {ex.Message}");
            }
        }
    }
}
