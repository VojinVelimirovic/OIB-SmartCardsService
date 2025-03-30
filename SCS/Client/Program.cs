using System;
using System.Runtime.Serialization;
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
            // Configure binding with both certificate and Windows auth
            NetTcpBinding binding = new NetTcpBinding();
            binding.Security.Mode = SecurityMode.TransportWithMessageCredential;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Certificate;
            binding.Security.Message.ClientCredentialType = MessageCredentialType.Windows;
            binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;

            string atmAddress = "net.tcp://localhost:8888/ATMService";

            // Get current Windows user identity
            WindowsIdentity windowsIdentity = WindowsIdentity.GetCurrent();
            string userName = ParseName(windowsIdentity.Name);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Current user: " + windowsIdentity.Name);

            X509Certificate2 clientCert = GetClientCertificate(userName);
            if (clientCert == null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: No valid certificate found for user '{userName}'");
                Console.ResetColor();
                Console.ReadLine();
                return;
            }

            try
            {
                using (ClientProxy proxy = new ClientProxy(binding, atmAddress))
                {
                    // Set client credentials directly on the proxy
                    proxy.Credentials.ClientCertificate.Certificate = clientCert;
                    proxy.Credentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
                    {
                        Console.WriteLine("Checking connection to ATM...");

                        try
                        {
                            if (proxy.TestConnection())
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("Connection to ATM established successfully.");
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Connection test failed. Exiting...");
                                return;
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Failed to establish connection: " + ex.Message);
                            return;
                        }

                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("Authenticating user...");

                        bool isAuthenticated = proxy.AuthenticateUser(userName, 1234);

                        if (isAuthenticated)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Authenticated! Welcome {0}!", userName);
                            Console.ResetColor();
                            Menu(proxy, userName);
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Authentication failed.");
                            Console.ResetColor();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: " + ex.Message);
                Console.ResetColor();
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Press Enter to exit.");
            Console.ResetColor();
            Console.ReadLine();
        }

        private static void Menu(ClientProxy proxy, string username)
        {
            while (true)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("\n--- Client Menu ---");
                Console.WriteLine("1. Create Smart Card");
                Console.WriteLine("2. Change PIN");
                Console.WriteLine("3. Deposit Funds");
                Console.WriteLine("4. Withdraw Funds");
                Console.WriteLine("5. View Balance");
                Console.WriteLine("6. View Active User Accounts (Manager Only)");
                Console.WriteLine("7. Exit");
                Console.Write("Choose an option: ");
                Console.ResetColor();

                string choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write("Enter username: ");
                            Console.Write("Enter 4-digit PIN: ");
                            int pin = int.Parse(Console.ReadLine());
                            // proxy.CreateSmartCard(username, pin);
                            Console.WriteLine("Smart card created successfully.");
                            Console.ResetColor();
                            break;

                        case "2":
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write("Enter username: ");
                            string changeUsername = Console.ReadLine();
                            Console.Write("Enter current PIN: ");
                            int currentPin = int.Parse(Console.ReadLine());
                            Console.Write("Enter new 4-digit PIN: ");
                            int newPin = int.Parse(Console.ReadLine());
                            // proxy.UpdatePin(changeUsername, currentPin, newPin);
                            Console.WriteLine("PIN updated successfully.");
                            Console.ResetColor();
                            break;

                        case "3":
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write("Enter amount to deposit: ");
                            double depositAmount = double.Parse(Console.ReadLine());

                            if (proxy.Deposit(username, depositAmount))
                            {
                                Console.WriteLine($"Deposited {depositAmount} successfully.");
                            }
                            Console.WriteLine("Deposit error");
                            break;

                        case "4":
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.Write("Enter amount to withdraw: ");
                            double withdrawAmount = double.Parse(Console.ReadLine());

                            if (proxy.Withdraw(username, withdrawAmount))
                            {
                                Console.WriteLine($"Withdrew {withdrawAmount} successfully.");
                            }
                            Console.WriteLine("Withdraw error");
                            break;
                        case "5":
                            Console.WriteLine(proxy.GetBalance(username));
                            break;
                        case "6":
                            var activeAccounts = proxy.GetActiveUserAccounts();
                            if (activeAccounts == null)
                            {
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("No active user accounts found.");
                                Console.ResetColor();
                            }
                            else
                            {
                                Console.ForegroundColor = ConsoleColor.Green;
                                Console.WriteLine("Active User Accounts:");
                                foreach (var account in activeAccounts)
                                {
                                    Console.WriteLine(account);
                                }
                                Console.ResetColor();
                            }
                            break;

                        case "7":
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("Exiting...");
                            Console.ResetColor();
                            return;

                        default:
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("Invalid option. Please try again.");
                            Console.ResetColor();
                            break;
                    }
                }
                catch (FaultException<SecurityException> faultEx)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"An error occurred: {faultEx.Detail.Message}");
                    Console.ResetColor();
                }
                catch (FaultException fault)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("An error occurred: " + fault.Message);
                    Console.ResetColor();
                }
                catch (CommunicationException commEx)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("An error occurred: " + commEx.Message);
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    Console.ResetColor();
                }
            }
        }
        private static string ParseName(string winLogonName)
        {
            string[] parts = new string[] { };

            if (winLogonName.Contains("@"))
            {
                ///UPN format
                parts = winLogonName.Split('@');
                return parts[0];
            }
            else if (winLogonName.Contains("\\"))
            {
                /// SPN format
                parts = winLogonName.Split('\\');
                return parts[1];
            }
            else
            {
                return winLogonName;
            }
        }
        private static X509Certificate2 GetClientCertificate(string userName)
        {
            string ou = userName == "oib_manager" ? "Manager" :
                       userName == "oib_smartcarduser" ? "SmartCardUser" :
                       string.Empty;

            if (string.IsNullOrEmpty(ou))
                return null;

            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            try
            {
                foreach (X509Certificate2 cert in store.Certificates)
                {
                    if (cert.Subject == $"CN={userName}, OU={ou}" && cert.HasPrivateKey)
                    {
                        return cert;
                    }
                }
                return null;
            }
            finally
            {
                store.Close();
            }
        }
    }
}