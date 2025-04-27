using System;
using System.ServiceModel;
using Common;

namespace ATM
{
    class Program
    {
        static void Main(string[] args)
        {
            NetTcpBinding binding = new NetTcpBinding();
            binding.Security.Mode = SecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;

            string address = "net.tcp://localhost:10000/ATMService";
            ServiceHost host = new ServiceHost(typeof(ATMService));
            host.AddServiceEndpoint(typeof(IATMService), binding, address);

            try
            {
                host.Open();
                Console.WriteLine("ATMService is running...");
                Console.WriteLine("Press Enter to stop the service.");
                Console.ReadLine();
            }
            catch (Exception e)
            {
                Console.WriteLine("Error: " + e.Message);
            }
            finally
            {
                if (host.State == CommunicationState.Opened)
                    host.Close();
            }
        }
    }
}
