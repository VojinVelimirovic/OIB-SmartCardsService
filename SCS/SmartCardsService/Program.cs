using Common;
using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.ServiceModel;
using System.ServiceModel.Security;

namespace SmartCardsService
{
    internal class Program
    {
        static void Main(string[] args)
        {
            NetTcpBinding binding = new NetTcpBinding();
            string address = "net.tcp://localhost:9999/SmartCardsService";

            //jednostavna windows autentifikacija za sada
            binding.Security.Mode = SecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            //binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Certificate;
            binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;


            ServiceHost host = new ServiceHost(typeof(SmartCardsService));
            //host.Credentials.ClientCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.PeerOrChainTrust;
            //host.Credentials.ClientCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;

            // Set the service's certificate (used for encryption)
            //host.Credentials.ServiceCertificate.SetCertificate(
            //    StoreLocation.LocalMachine,
            //    StoreName.My,
            //    X509FindType.FindBySubjectName,
            //    "YourServerCertificateName" // Replace with actual server certificate name
            //);

            host.AddServiceEndpoint(typeof(ISmartCardsService), binding, address);

            host.Open();
            Console.WriteLine("Main server is running at: " + address);

            Console.WriteLine("Press Enter to stop the server.");
            Console.ReadLine();

            Console.WriteLine("Test");
            host.Close();

        }
    }
}
