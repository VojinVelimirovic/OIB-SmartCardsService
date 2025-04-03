using System;
using System.Linq;
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
            string scsAddress = "net.tcp://localhost:9999/SmartCardsService";

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
                using (ClientProxyATM atmProxy = new ClientProxyATM(binding, atmAddress))
                {
                    // Set client credentials directly on the proxy
                    atmProxy.Credentials.ClientCertificate.Certificate = clientCert;
                    atmProxy.Credentials.Windows.AllowedImpersonationLevel = TokenImpersonationLevel.Impersonation;
                    {
                        Console.WriteLine("Checking connection to ATM...");

                        try
                        {
                            if (atmProxy.TestConnection())
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

                        string username = "Marko";
                        bool isAuthenticated = atmProxy.AuthenticateUser(username, 1234);

                        if (isAuthenticated)
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine("Authenticated! Welcome {0}!", username);
                            Console.ResetColor();
                            Menu(atmProxy, username);
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

        private static void Menu(ClientProxyATM proxy, string username)
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
                            Console.Write("Enter username: ");
                            string newUsername = Console.ReadLine();
                            Console.Write("Enter 4-digit PIN: ");

                            if (!int.TryParse(Console.ReadLine(), out int pin) || pin < 1000 || pin > 9999)
                            {
                                WriteError("Invalid PIN. Must be a 4-digit number.");
                                break;
                            }

                            // proxy.CreateSmartCard(newUsername, pin);
                            Console.WriteLine("Smart card created successfully.");
                            break;

                        case "2":
                            Console.Write("Enter username: ");
                            string changeUsername = Console.ReadLine();
                            Console.Write("Enter current PIN: ");

                            if (!int.TryParse(Console.ReadLine(), out int currentPin) || currentPin < 1000 || currentPin > 9999)
                            {
                                WriteError("Invalid current PIN. Must be a 4-digit number.");
                                break;
                            }

                            Console.Write("Enter new 4-digit PIN: ");
                            if (!int.TryParse(Console.ReadLine(), out int newPin) || newPin < 1000 || newPin > 9999)
                            {
                                WriteError("Invalid new PIN. Must be a 4-digit number.");
                                break;
                            }

                            // proxy.UpdatePin(changeUsername, currentPin, newPin);
                            Console.WriteLine("PIN updated successfully.");
                            break;

                        case "3":
                            Console.Write("Enter amount to deposit: ");

                            if (!double.TryParse(Console.ReadLine(), out double depositAmount) || depositAmount <= 0)
                            {
                                WriteError("Invalid deposit amount.");
                                break;
                            }

                            if (proxy.Deposit(username, depositAmount))
                            {
                                Console.WriteLine($"Deposited {depositAmount:C} successfully.");
                            }
                            else
                            {
                                WriteError("Deposit failed.");
                            }
                            break;

                        case "4":
                            Console.Write("Enter amount to withdraw: ");

                            if (!double.TryParse(Console.ReadLine(), out double withdrawAmount) || withdrawAmount <= 0)
                            {
                                WriteError("Invalid withdrawal amount.");
                                break;
                            }

                            if (proxy.Withdraw(username, withdrawAmount))
                            {
                                Console.WriteLine($"Withdrew {withdrawAmount:C} successfully.");
                            }
                            else
                            {
                                WriteError("Withdrawal failed.");
                            }
                            break;

                        case "5":
                            Console.WriteLine($"Current Balance: {proxy.GetBalance(username):C}");
                            break;

                        case "6":
                            var activeAccounts = proxy.GetActiveUserAccounts();
                            if (activeAccounts == null || !activeAccounts.Any())
                            {
                                WriteError("No active user accounts found.");
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
                            WriteError("Invalid option. Please try again.");
                            break;
                    }
                }
                catch (FaultException<SecurityException> faultEx)
                {
                    WriteError($"Security error: {faultEx.Detail.Message}");
                }
                catch (FaultException fault)
                {
                    WriteError($"Fault error: {fault.Message}");
                }
                catch (CommunicationException commEx)
                {
                    WriteError($"Communication error: {commEx.Message}");
                }
                catch (Exception ex)
                {
                    WriteError($"General error: {ex.Message}");
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

        private static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {message}");
            Console.ResetColor();
        }

        private static void WriteSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}