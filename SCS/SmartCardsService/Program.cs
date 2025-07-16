using Common;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Security;

namespace SmartCardsService
{
    internal class Program
    {
        static void Main(string[] args)
        {
            string winUser = CertManager.ParseName(WindowsIdentity.GetCurrent().Name);
            if (winUser != "wcfservice")
            {
                ColorfulConsole.WriteError("You need to launch the service as wcfservice user.");
                Console.ReadKey();
                return;
            }

            NetTcpBinding binding = new NetTcpBinding();
            binding.Security.Mode = SecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Certificate;

            var replicationBinding = new NetTcpBinding();
            replicationBinding.Security.Mode = SecurityMode.Transport;
            replicationBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            replicationBinding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;

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
                Console.WriteLine("Press Enter to stop the service.\n");
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