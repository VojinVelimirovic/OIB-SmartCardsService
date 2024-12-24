using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {

            NetTcpBinding binding = new NetTcpBinding();
            binding.Security.Mode = SecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;

            string primaryAddress = "net.tcp://localhost:9999/SmartCardsService";
            string backupAddress = "net.tcp://localhost:9998/SmartCardsService";


            Console.WriteLine("Korisnik koji je pokrenuo klijenta je : " + WindowsIdentity.GetCurrent().Name);
            bool isConnected = false;

            try
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Attempting to connect to the primary server...");
                Console.ForegroundColor = ConsoleColor.White;
                using (ClientProxy proxy = new ClientProxy(binding, primaryAddress))
                {
                    //NAPOMENA: DA BI RADILO MORAS POKRENUTI KLIJENTA PREKO WINDOWS IDENTITIY-JA
                    //KOJI JE ILI U GRUPI Manager ILI U GRUPI SmartCardUser
                    //NAKON DODAVANJA WINDOWS IDENTITY-JA U GRUPU MORAS DA SE IZLOGUJES I ULOGUJES ILI SE NECE UPDATE-OVATI
                    proxy.TestCommunication("Zdravo");
                    proxy.TestCommunication("Klikni nesto da bi ugasio");
                    proxy.CreateSmartCard("Marko", 1234);

                    isConnected = true;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Connected to the primary server.");
                    Console.ForegroundColor = ConsoleColor.White;
                }
            }
            catch (FaultException fault)
            {
                Console.WriteLine("A service fault occurred on the primary server: " + fault.Message);
            }
            catch (CommunicationException commEx)
            {
                Console.WriteLine("A communication error occurred on the primary server: " + commEx.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An unexpected error occurred on the primary server: " + ex.Message);
            }

            // If not connected to the primary, try connecting to the backup server
            if (!isConnected)
            {
                try
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Attempting to connect to the backup server...");
                    Console.ForegroundColor = ConsoleColor.White;
                    using (ClientProxy proxy = new ClientProxy(binding, backupAddress))
                    {
                        proxy.TestCommunication("Zdravo");
                        proxy.CreateSmartCard("Marko", 1234);
                        isConnected=true;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Connected to the backup server.");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
                catch (FaultException fault)
                {
                    Console.WriteLine("A service fault occurred on the backup server: " + fault.Message);
                }
                catch (CommunicationException commEx)
                {
                    Console.WriteLine("A communication error occurred on the backup server: " + commEx.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("An unexpected error occurred on the backup server: " + ex.Message);
                }
            }

            if (!isConnected)
            {
                Console.WriteLine("Unable to connect to neither the primary nor the backup server.");
            }

            Console.WriteLine("Press Enter to exit.");
            Console.ReadLine();
        }
    }
}
