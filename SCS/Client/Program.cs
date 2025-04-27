using System;
using System.Linq;
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
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Certificate;

            // 1) serviceBinding: mutual‐TLS (client+server cert)
            var serviceBinding = new NetTcpBinding(SecurityMode.Transport);
            serviceBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Certificate;

            // 2) atmBinding: TLS with server cert only (chain‐trust), no client cert
            var atmBinding = new NetTcpBinding(SecurityMode.Transport);
            atmBinding.Security.Transport.ClientCredentialType = TcpClientCredentialType.None;

            var srvCert = CertManager.GetCertificateFromStorage(StoreName.TrustedPeople, StoreLocation.LocalMachine, "wcfservice");
            var interCert = CertManager.GetCertificateFromStorage(StoreName.TrustedPeople, StoreLocation.LocalMachine, "oib_atm");

            // Direct service endpoints
            EndpointAddress primaryAddress = new EndpointAddress(
               new Uri("net.tcp://localhost:9999/SmartCardsService"),
               new X509CertificateEndpointIdentity(srvCert) // attach expected server certificate
           );
            EndpointAddress backupAddress = new EndpointAddress(
                new Uri("net.tcp://localhost:9998/SmartCardsService"),
                new X509CertificateEndpointIdentity(srvCert) // attach expected server certificate
            );

            // ATM endpoint
            EndpointAddress atmAddress = new EndpointAddress(
                new Uri("net.tcp://localhost:10000/ATMService"),
                new X509CertificateEndpointIdentity(interCert)
            );

            EndpointAddress currentAddress = primaryAddress;

            Console.WriteLine("Current user: " + CertManager.ParseName(WindowsIdentity.GetCurrent().Name));

            // Create proxies
            using (var scsProxy = new ClientProxySCS(serviceBinding, currentAddress))
            using (var atmProxy = new ClientProxyATM(atmBinding, atmAddress))
            {
                Console.WriteLine("Test Commands:");
                Console.WriteLine("'d' - Test Service connection");
                Console.WriteLine("'s' - Test ATM connection (digital signatures)");
                Console.WriteLine("Any other key - Continue to application");

                while (true)
                {
                    var key = Console.ReadKey(intercept: true);

                    if (key.KeyChar == 'd' || key.KeyChar == 'D')
                    {
                        TestServiceConnection(serviceBinding, ref currentAddress, primaryAddress, backupAddress);
                    }
                    else if (key.KeyChar == 's' || key.KeyChar == 'S')
                    {
                        TestSignedMessage(atmBinding, atmAddress);
                    }
                    else
                    {
                        break;
                    }
                }
                ShowMenu(scsProxy, atmProxy, ref currentAddress, primaryAddress, backupAddress);
            }
        }

        private static void ShowMenu(
            ClientProxySCS scsProxy,
            ClientProxyATM atmProxy,
            ref EndpointAddress currentAddress,
            EndpointAddress primaryAddress,
            EndpointAddress backupAddress)
        {
            string username = CertManager.ParseName(WindowsIdentity.GetCurrent().Name);
            while (true)
            {
                Console.WriteLine("\n--- Client Menu ---");
                Console.WriteLine("'d' - Test Service connection");
                Console.WriteLine("'s' - Test ATM connection (digital signatures)");
                Console.WriteLine("1. SmartCardsService/Create Smart Card");
                Console.WriteLine("2. SmartCardsService/Change PIN");
                Console.WriteLine("3. ATM/Deposit Funds");
                Console.WriteLine("4. ATM/Withdraw Funds");
                Console.WriteLine("5. ATM/View Balance");
                Console.WriteLine("6. SmartCardsService/View Active User Accounts (Manager Only)");
                Console.WriteLine("7. Switch SmartCardsService Endpoint Manually");
                Console.WriteLine("8. Exit");
                Console.Write("Choose an option: ");

                var choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1": // Create Smart Card
                            Console.Write("Enter username: ");
                            var newUser = Console.ReadLine();
                            Console.Write("Enter 4-digit PIN: ");
                            if (!int.TryParse(Console.ReadLine(), out int pin) || pin < 1000 || pin > 9999)
                            {
                                WriteError("Invalid PIN. Must be a 4-digit number.");
                                break;
                            }
                            scsProxy.CreateSmartCard(newUser, pin);
                            Console.WriteLine("Smart card created successfully.");
                            break;

                        case "2": // Change PIN
                            Console.Write("Enter username: ");
                            var changeUser = Console.ReadLine();
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
                            scsProxy.UpdatePin(changeUser, currentPin, newPin);
                            Console.WriteLine("PIN updated successfully.");
                            break;

                        case "3": // Deposit
                            Console.Write("Enter amount to deposit: ");
                            if (!double.TryParse(Console.ReadLine(), out double depositAmount) || depositAmount <= 0)
                            {
                                WriteError("Invalid deposit amount.");
                                break;
                            }
                            if (atmProxy.Deposit(username, depositAmount))
                                Console.WriteLine($"Deposited {depositAmount:C} successfully.");
                            else
                                WriteError("Deposit failed.");
                            break;

                        case "4": // Withdraw
                            Console.Write("Enter amount to withdraw: ");
                            if (!double.TryParse(Console.ReadLine(), out double withdrawAmount) || withdrawAmount <= 0)
                            {
                                WriteError("Invalid withdrawal amount.");
                                break;
                            }
                            if (atmProxy.Withdraw(username, withdrawAmount))
                                Console.WriteLine($"Withdrew {withdrawAmount:C} successfully.");
                            else
                                WriteError("Withdrawal failed.");
                            break;

                        case "5": // View Balance
                            Console.WriteLine($"Current Balance: {atmProxy.GetBalance(username):C}");
                            break;

                        case "6": // View Active Accounts
                            //var accounts = scsProxy.GetActiveUserAccounts();
                            //if (accounts == null || !accounts.Any())
                            //    WriteError("No active user accounts found.");
                            //else
                            //{
                            //    Console.WriteLine("Active User Accounts:");
                            //    foreach (var acct in accounts)
                            //        Console.WriteLine(acct);
                            //}
                            break;

                        case "7": // Switch endpoint
                            SwitchEndpoint(ref currentAddress, primaryAddress, backupAddress);
                            break;

                        case "8":
                            Console.WriteLine("Exiting...");
                            return;

                        default:
                            WriteError("Invalid option. Please try again.");
                            break;
                    }
                }
                catch (FaultException<SecurityException> secEx)
                {
                    WriteError($"Security error: {secEx.Detail.Message}");
                }
                catch (FaultException fault)
                {
                    WriteError($"Service fault: {fault.Message}");
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

        private static void SwitchEndpoint(ref EndpointAddress currentAddress, EndpointAddress primary, EndpointAddress backup)
        {
            currentAddress = currentAddress == primary ? backup : primary;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[SmartCardsService] Switched to {(currentAddress == primary ? "primary" : "backup")} endpoint {currentAddress.Uri}");
            Console.ResetColor();
        }

        private static void Menu(ClientProxyATM proxy, string username)
        {
            while (true)
            {
                Console.WriteLine("\n--- Client Menu ---");
                Console.WriteLine("1. SmartCardsService/Create Smart Card");
                Console.WriteLine("2. SmartCardsService/Change PIN");
                Console.WriteLine("3. ATM/Deposit Funds");
                Console.WriteLine("4. ATM/Withdraw Funds");
                Console.WriteLine("5. ATM/View Balance");
                Console.WriteLine("6. SmartCardsService/View Active User Accounts (Manager Only)");
                Console.WriteLine("7. Exit");
                Console.Write("Choose an option: ");

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

        static void TestSignedMessage(NetTcpBinding binding, EndpointAddress intermediaryAddress)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[INTERMEDIARY - SIGNED] Sending signed message...");
            Console.ResetColor();

            try
            {
                using (var proxy = new ClientProxyATM(binding, intermediaryAddress))
                {
                    string msg = "This is a signed message.";
                    string clientName = CertManager.ParseName(WindowsIdentity.GetCurrent().Name);
                    string certCN = clientName + "_sign";

                    var cert = CertManager.GetClientCertificate(certCN);
                    var signature = DigitalSignature.Create(msg, HashAlgorithm.SHA1, cert);

                    var request = new SignedRequest
                    {
                        Message = msg,
                        Signature = signature,
                        SenderName = clientName
                    };

                    proxy.SignedMessage(request);
                    Console.WriteLine("[INTERMEDIARY - SIGNED] Sent successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[INTERMEDIARY - SIGNED] Failed: {ex.Message}");
            }
        }

        static void TestServiceConnection(NetTcpBinding binding, ref EndpointAddress currentAddress, EndpointAddress primaryAddress, EndpointAddress backupAddress)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[SmartCardsService] Attempting connection...");
            Console.ResetColor();

            try
            {
                // NOTE: When an exception is thrown proxy goes to a proxy state from which he can't recover
                // Meaning that a new proxy needs to be created if an exception is thrown
                using (var proxy = new ClientProxySCS(binding, currentAddress))
                {
                    proxy.TestCommunication();
                    Console.WriteLine($"[SmartCardsService] Success at: {currentAddress.Uri}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartCardsService] Failed at {currentAddress.Uri}: {ex.Message}");
                SwitchEndpoint(ref currentAddress, primaryAddress, backupAddress);
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