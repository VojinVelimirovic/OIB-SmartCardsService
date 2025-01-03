﻿using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

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
            binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;

            ServiceHost host = new ServiceHost(typeof(SmartCardsService));
            host.AddServiceEndpoint(typeof(ISmartCardsService), binding, address);

            host.Open();
            Console.WriteLine("Main server is running at: " + address);
            Console.WriteLine("Korisnik se povezao na servis:" + WindowsIdentity.GetCurrent().Name);

            Console.WriteLine("Press Enter to stop the server.");
            Console.ReadLine();

            Console.WriteLine("Test");
            host.Close();

        }
    }
}
