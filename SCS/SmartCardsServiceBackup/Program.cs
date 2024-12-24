using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;
using SmartCardsService;

namespace SmartCardsServiceBackup
{
    internal class Program
    {
        static void Main(string[] args)
        {
            NetTcpBinding binding = new NetTcpBinding();
            binding.Security.Mode = SecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;

            string backupServerAddress = "net.tcp://localhost:9998/SmartCardsService";

            ServiceHost host = new ServiceHost(typeof(SmartCardsService.SmartCardsService));
            host.AddServiceEndpoint(typeof(ISmartCardsService), binding, backupServerAddress);
            host.Open();

            Console.WriteLine("Backup server is running at: " + backupServerAddress);
            Console.WriteLine("User running the backup server: " + WindowsIdentity.GetCurrent().Name);
            Console.WriteLine("Press Enter to stop the server.");

            Console.ReadLine();
            host.Close();
        }
    }
}
