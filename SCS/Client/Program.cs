using System;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.ServiceModel;
using Common;

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

            string atmAddress = "net.tcp://localhost:8888/ATMService";

            Console.WriteLine("Current user: " + WindowsIdentity.GetCurrent().Name);

            try
            {
                using (ClientProxy proxy = new ClientProxy(binding, atmAddress))
                {
                    Console.WriteLine("Checking connection to ATM...");

                    // Attach Client Certificate
                    //proxy.Credentials.ClientCertificate.SetCertificate(
                    //    StoreLocation.CurrentUser,
                    //    StoreName.My,
                    //    X509FindType.FindBySubjectName,
                    //    "YourClientCertificateName" // Replace with actual client certificate name
                    //);

                    // Call the method that verifies both authentication methods
                    // string verificationResult = proxy.VerifyClient();
                    // Console.WriteLine(verificationResult);

                    try
                    {
                        if (proxy.TestConnection())
                        {
                            Console.WriteLine("Connection to ATM established successfully.");
                        }
                        else
                        {
                            Console.WriteLine("Connection test failed. Exiting...");
                            return;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Failed to establish connection: " + ex.Message);
                        return;
                    }

                    Console.WriteLine("Authenticating user...");
                    Console.ReadLine();
                    bool isAuthenticated = proxy.AuthenticateUser("Marko", 1234);

                    Console.ReadLine();
                    Console.WriteLine("Authenticating user...");
                    bool isAuthenticate2d = proxy.AuthenticateUser("Marko", 1234);

                    if (isAuthenticated)
                    {
                        Console.WriteLine("Authenticated! Performing transactions...");

                        proxy.Deposit("Marko", 500);
                        proxy.Withdraw("Marko", 100);
                    }
                    else
                    {
                        Console.WriteLine("Authentication failed.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
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
                           // proxy.CreateSmartCard(username, pin);
                            Console.WriteLine("Smart card created successfully.");
                            break;

                        case "2":
                            Console.Write("Enter username: ");
                            string changeUsername = Console.ReadLine();
                            Console.Write("Enter current PIN: ");
                            int currentPin = int.Parse(Console.ReadLine());
                            Console.Write("Enter new 4-digit PIN: ");
                            int newPin = int.Parse(Console.ReadLine());
                           // proxy.UpdatePin(changeUsername, currentPin, newPin);
                            Console.WriteLine("PIN updated successfully.");
                            break;

                        case "3":
                            Console.Write("Enter username: ");
                            string depositUsername = Console.ReadLine();
                            Console.Write("Enter 4-digit PIN: ");
                            int depositPin = int.Parse(Console.ReadLine());
                            Console.Write("Enter amount to deposit: ");
                            float depositAmount = float.Parse(Console.ReadLine());
                           // proxy.AddBalance(depositUsername, depositPin, depositAmount);
                            Console.WriteLine($"Deposited {depositAmount} successfully.");
                            break;

                        case "4":
                            Console.Write("Enter username: ");
                            string withdrawUsername = Console.ReadLine();
                            Console.Write("Enter 4-digit PIN: ");
                            int withdrawPin = int.Parse(Console.ReadLine());
                            Console.Write("Enter amount to withdraw: ");
                            float withdrawAmount = float.Parse(Console.ReadLine());
                           // proxy.RemoveBalance(withdrawUsername, withdrawPin, withdrawAmount);
                            Console.WriteLine($"Withdrew {withdrawAmount} successfully.");
                            break;

                        case "5":
                            var activeAccounts = proxy.GetActiveUserAccounts();
                            if (activeAccounts == null)
                            {
                            }
                            //else if (activeAccounts.Count == 0) { Console.WriteLine("No active user accounts found."); }
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
