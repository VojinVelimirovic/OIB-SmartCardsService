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
            string address = "net.tcp://localhost:9999/SmartCardsService";

            // Configure binding for certificate authentication
            binding.Security.Mode = SecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Certificate;
            binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;

            ServiceHost host = new ServiceHost(typeof(SmartCardsService));

            host.Credentials.ServiceCertificate.SetCertificate(
                StoreLocation.LocalMachine,
                StoreName.My,
                X509FindType.FindBySubjectDistinguishedName,
                "CN=wcfservice");
            
            // Configure client certificate validation
            host.Credentials.ClientCertificate.Authentication.CertificateValidationMode =
                X509CertificateValidationMode.ChainTrust;
            host.Credentials.ClientCertificate.Authentication.RevocationMode =
                X509RevocationMode.Online;

            // Map certificates to Windows accounts for authorization
            host.Credentials.ClientCertificate.Authentication.MapClientCertificateToWindowsAccount = true;

            // Add authorization behavior (optional)
            var authBehavior = host.Description.Behaviors.Find<ServiceAuthorizationBehavior>();
            if (authBehavior == null)
            {
                authBehavior = new ServiceAuthorizationBehavior();
                host.Description.Behaviors.Add(authBehavior);
            }
            authBehavior.PrincipalPermissionMode = PrincipalPermissionMode.UseWindowsGroups;

            host.AddServiceEndpoint(typeof(ISmartCardsService), binding, address);

            try
            {
                host.Open();
                Console.WriteLine("SmartCard Service is running at: " + address);
                Console.WriteLine("Press Enter to stop the service.");
                Console.ReadLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Service failed to start: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
            finally
            {
                if (host.State == CommunicationState.Opened)
                    host.Close();
            }
        }
    }
}