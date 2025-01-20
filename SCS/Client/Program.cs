using Common;
using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
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

                    isConnected = true;
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Connected to the primary server.");
                    Console.ForegroundColor = ConsoleColor.White;
                    Menu(proxy);
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
                        isConnected=true;
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Connected to the backup server.");
                        Console.ForegroundColor = ConsoleColor.White;
                        Menu(proxy);
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
        private static void Menu(ClientProxy proxy)
        {
            while (true)
            {
                Console.WriteLine("\n--- Client Menu ---");
                Console.WriteLine("1. Create Smart Card");
                Console.WriteLine("2. Change PIN");
                Console.WriteLine("3. Deposit Funds");
                Console.WriteLine("4. Withdraw Funds");
                Console.WriteLine("5. View Active User Accounts (Manager Only)");
                Console.WriteLine("6. Exit");
                Console.Write("Choose an option: ");

                string choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            Console.Write("Enter username: ");
                            string username = Console.ReadLine();
                            Console.Write("Enter 4-digit PIN: ");
                            int pin = int.Parse(Console.ReadLine());
                            proxy.CreateSmartCard(username, pin);
                            Console.WriteLine("Smart card created successfully.");
                            break;

                        case "2":
                            Console.Write("Enter username: ");
                            string changeUsername = Console.ReadLine();
                            Console.Write("Enter current PIN: ");
                            int currentPin = int.Parse(Console.ReadLine());
                            Console.Write("Enter new 4-digit PIN: ");
                            int newPin = int.Parse(Console.ReadLine());
                            proxy.UpdatePin(changeUsername, currentPin, newPin);
                            Console.WriteLine("PIN updated successfully.");
                            break;

                        case "3":
                            Console.Write("Enter username: ");
                            string depositUsername = Console.ReadLine();
                            Console.Write("Enter 4-digit PIN: ");
                            int depositPin = int.Parse(Console.ReadLine());
                            Console.Write("Enter amount to deposit: ");
                            float depositAmount = float.Parse(Console.ReadLine());
                            proxy.AddBalance(depositUsername, depositPin, depositAmount);
                            Console.WriteLine($"Deposited {depositAmount} successfully.");
                            break;

                        case "4":
                            Console.Write("Enter username: ");
                            string withdrawUsername = Console.ReadLine();
                            Console.Write("Enter 4-digit PIN: ");
                            int withdrawPin = int.Parse(Console.ReadLine());
                            Console.Write("Enter amount to withdraw: ");
                            float withdrawAmount = float.Parse(Console.ReadLine());
                            proxy.RemoveBalance(withdrawUsername, withdrawPin, withdrawAmount);
                            Console.WriteLine($"Withdrew {withdrawAmount} successfully.");
                            break;

                        case "5":
                            var activeAccounts = proxy.GetActiveUserAccounts();
                            if (activeAccounts == null) {
                            }else if(activeAccounts.Count == 0)
                            {
                                Console.WriteLine("No active user accounts found.");
                            }
                            else
                            {
                                Console.WriteLine("Active User Accounts:");
                                foreach (var account in activeAccounts)
                                {
                                    Console.WriteLine(account);
                                }
                            }
                            break;

                        case "6":
                            Console.WriteLine("Exiting...");
                            return;

                        default:
                            Console.WriteLine("Invalid option. Please try again.");
                            break;
                    }
                }
                catch (FaultException<SecurityException> faultEx)
                {
                    Console.WriteLine($"An error occurred: {faultEx.Detail.Message}");
                }
                catch (FaultException fault)
                {
                    Console.WriteLine("An error occurred: " + fault.Message);
                }
                catch (CommunicationException commEx)
                {
                    Console.WriteLine("An error occurred: " + commEx.Message);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                }
            }
        }
    }
}
