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

            //TestConnection(serviceBinding, atmBinding, ref currentAddress, primaryAddress, backupAddress, atmAddress);

            //ShowMenuNoProxy(serviceBinding, primaryAddress, backupAddress, atmBinding, atmAddress);

            // NOTE: Incorrect, proxies need to be recreated or don't use exceptions in bussiness logic
            // When an exception is thrown proxy goes to a faulted state from which it can't recover
            // Meaning that a new proxy needs to be created if an exception is thrown
            // Currently if fault occurs, client application needs to be restarted !!!
            using (var atmProxy = new ClientProxyATM(atmBinding, atmAddress))
            {
                ShowMenu(serviceBinding, atmProxy, ref currentAddress, primaryAddress, backupAddress);
            }
        }

        private static void ShowMenuNoProxy(
            NetTcpBinding scsBinding,
            EndpointAddress primaryAddress,
            EndpointAddress backupAddress,
            NetTcpBinding atmBinding,
            EndpointAddress atmAddress)
        {
            EndpointAddress currentAddress = primaryAddress;
            string username = null;

            while (true)
            {
                Console.WriteLine("\n--- Client Menu ---");
                Console.WriteLine("1. SmartCardsService/Create Smart Card");
                Console.WriteLine("2. SmartCardsService/Change PIN");
                Console.WriteLine("3. ATM/Deposit Funds");
                Console.WriteLine("4. ATM/Withdraw Funds");
                Console.WriteLine("5. ATM/View Balance");
                Console.WriteLine("6. ATM/View Active User Accounts (Manager Only)");
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
                                ColorfulConsole.WriteError("Invalid PIN. Must be a 4-digit number.");
                                break;
                            }
                            using (var scsProxy = new ClientProxySCS(scsBinding, currentAddress))
                            {
                                scsProxy.CreateSmartCard(newUser, pin);
                            }
                            Console.WriteLine("Smart card created.");
                            break;

                        case "2": // Change PIN
                            Console.Write("Enter username: ");
                            var changeUser = Console.ReadLine();
                            Console.Write("Enter current PIN: ");
                            if (!int.TryParse(Console.ReadLine(), out int currentPin) || currentPin < 1000 || currentPin > 9999)
                            {
                                ColorfulConsole.WriteError("Invalid current PIN. Must be a 4-digit number.");
                                break;
                            }
                            Console.Write("Enter new 4-digit PIN: ");
                            if (!int.TryParse(Console.ReadLine(), out int newPin) || newPin < 1000 || newPin > 9999)
                            {
                                ColorfulConsole.WriteError("Invalid new PIN. Must be a 4-digit number.");
                                break;
                            }

                            using (var scsProxy2 = new ClientProxySCS(scsBinding, currentAddress))
                            {
                                scsProxy2.UpdatePin(changeUser, currentPin, newPin);
                            }
                            Console.WriteLine("PIN updated.");
                            break;

                        case "3": // Deposit
                            if (!TwoFactorAuthNoProxy(atmBinding, atmAddress, out username))
                                break;

                            Console.Write("Enter amount to deposit: ");
                            if (!double.TryParse(Console.ReadLine(), out double depositAmount) || depositAmount <= 0)
                            {
                                ColorfulConsole.WriteError("Invalid deposit amount.");
                                break;
                            }

                            using (var atmProxy = new ClientProxyATM(atmBinding, atmAddress))
                            {
                                if (atmProxy.Deposit(username, depositAmount))
                                    ColorfulConsole.WriteAtmInfo($"Deposited {depositAmount:N2} RSD successfully.");
                                else
                                    ColorfulConsole.WriteAtmInfo("Deposit failed.");
                            }
                            break;

                        case "4": // Withdraw
                            if (!TwoFactorAuthNoProxy(atmBinding, atmAddress, out username))
                                break;

                            Console.Write("Enter amount to withdraw: ");
                            if (!double.TryParse(Console.ReadLine(), out double withdrawAmount) || withdrawAmount <= 0)
                            {
                                ColorfulConsole.WriteError("Invalid withdrawal amount.");
                                break;
                            }

                            using (var atmProxy = new ClientProxyATM(atmBinding, atmAddress))
                            {
                                if (atmProxy.Withdraw(username, withdrawAmount, out string message))
                                    ColorfulConsole.WriteAtmInfo(message);
                                else
                                    ColorfulConsole.WriteError(message);
                            }
                            break;

                        case "5": // View Balance
                            if (!TwoFactorAuthNoProxy(atmBinding, atmAddress, out username))
                                break;

                            using (var atmProxy = new ClientProxyATM(atmBinding, atmAddress))
                            {
                                ColorfulConsole.WriteAtmInfo($"Current Balance: {atmProxy.GetBalance(username):N2} RSD.");
                            }
                            break;

                        case "6": // View Active Accounts
                            using (var atmProxy = new ClientProxyATM(atmBinding, atmAddress))
                            {
                                string[] accounts = atmProxy.GetActiveUserAccounts();
                                string print = "";
                                if (accounts.Length == 0)
                                {
                                    ColorfulConsole.WriteAtmInfo("There are no active accounts.");
                                    break;
                                }
                                print += "Active accounts:";
                                foreach (string s in accounts)
                                {
                                    print += '\n' + s;
                                }
                                ColorfulConsole.WriteAtmInfo(print);
                            }
                            break;

                        case "7": // Switch endpoint
                            SwitchEndpoint(ref currentAddress, primaryAddress, backupAddress);
                            break;

                        case "8":
                            Console.WriteLine("Exiting...");
                            return;

                        default:
                            ColorfulConsole.WriteError("Invalid option. Please try again.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    ColorfulConsole.WriteError(ex.Message);
                }
            }
        }

        private static void ShowMenu(
            NetTcpBinding serviceBinding,
            ClientProxyATM atmProxy,
            ref EndpointAddress currentAddress,
            EndpointAddress primaryAddress,
            EndpointAddress backupAddress)
        {
            string username = null;

            while (true)
            {
                Console.WriteLine("\n--- Client Menu ---");
                Console.WriteLine("1. SmartCardsService/Create Smart Card");
                Console.WriteLine("2. SmartCardsService/Change PIN");
                Console.WriteLine("3. ATM/Deposit Funds");
                Console.WriteLine("4. ATM/Withdraw Funds");
                Console.WriteLine("5. ATM/View Balance");
                Console.WriteLine("6. ATM/View Active User Accounts (Manager Only)");
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
                                ColorfulConsole.WriteError("Invalid PIN. Must be a 4-digit number.");
                                break;
                            }

                            using (var scsProxy = new ClientProxySCS(serviceBinding, currentAddress))
                            {
                                try
                                {
                                    scsProxy.CreateSmartCard(newUser, pin);
                                    Console.WriteLine("Smart card created successfully.");
                                }
                                catch (EndpointNotFoundException ex)
                                {
                                    ColorfulConsole.WriteError($"- [SmartCardsService] Failed at {currentAddress.Uri}: {ex.Message}");
                                    SwitchEndpoint(ref currentAddress, primaryAddress, backupAddress);
                                    break;
                                }
                            }
                            break;

                        case "2": // Change PIN
                            Console.Write("Enter username: ");
                            var changeUser = Console.ReadLine();
                            Console.Write("Enter current PIN: ");
                            if (!int.TryParse(Console.ReadLine(), out int currentPin) || currentPin < 1000 || currentPin > 9999)
                            {
                                ColorfulConsole.WriteError("Invalid current PIN. Must be a 4-digit number.");
                                break;
                            }
                            Console.Write("Enter new 4-digit PIN: ");
                            if (!int.TryParse(Console.ReadLine(), out int newPin) || newPin < 1000 || newPin > 9999)
                            {
                                ColorfulConsole.WriteError("Invalid new PIN. Must be a 4-digit number.");
                                break;
                            }

                            using (var scsProxy = new ClientProxySCS(serviceBinding, currentAddress))
                            {
                                try
                                {
                                    scsProxy.UpdatePin(changeUser, currentPin, newPin);
                                    Console.WriteLine("PIN updated successfully.");
                                }
                                catch (EndpointNotFoundException ex)
                                {
                                    ColorfulConsole.WriteError($"- [SmartCardsService] Failed at {currentAddress.Uri}: {ex.Message}");
                                    SwitchEndpoint(ref currentAddress, primaryAddress, backupAddress);
                                    break;
                                }
                            }
                            break;

                        case "3": // Deposit
                            if (!TwoFactorAuth(atmProxy, out username))
                                break;

                            Console.Write("Enter amount to deposit: ");
                            if (!double.TryParse(Console.ReadLine(), out double depositAmount) || depositAmount <= 0)
                            {
                                ColorfulConsole.WriteError("Invalid deposit amount.");
                                break;
                            }

                            if (atmProxy.Deposit(username, depositAmount))
                                ColorfulConsole.WriteAtmInfo($"Deposited {depositAmount:N2} RSD successfully.");
                            else
                                ColorfulConsole.WriteAtmInfo("Deposit failed.");
                            break;

                        case "4": // Withdraw
                            if (!TwoFactorAuth(atmProxy, out username))
                                break;

                            Console.Write("Enter amount to withdraw: ");
                            if (!double.TryParse(Console.ReadLine(), out double withdrawAmount) || withdrawAmount <= 0)
                            {
                                ColorfulConsole.WriteError("Invalid withdrawal amount.");
                                break;
                            }
                            if (atmProxy.Withdraw(username, withdrawAmount, out string message))
                                ColorfulConsole.WriteAtmInfo(message);
                            else
                                ColorfulConsole.WriteError(message);
                            break;

                        case "5": // View Balance
                            if (!TwoFactorAuth(atmProxy, out username))
                                break;
                            ColorfulConsole.WriteAtmInfo($"Current Balance: {atmProxy.GetBalance(username):N2} RSD.");
                            break;

                        case "6": // View Active Accounts
                            string[] accounts = atmProxy.GetActiveUserAccounts();
                            string print = "";
                            if(accounts.Length == 0)
                            {
                                ColorfulConsole.WriteAtmInfo("There are no active accounts.");
                                break;
                            }
                            print += "Active accounts:";
                            foreach (string s in accounts)
                            {
                                print += '\n' + s;
                            }
                            ColorfulConsole.WriteAtmInfo(print);
                            break;

                        case "7": // Switch endpoint
                            SwitchEndpoint(ref currentAddress, primaryAddress, backupAddress);
                            break;

                        case "8":
                            Console.WriteLine("Exiting...");
                            return;

                        default:
                            ColorfulConsole.WriteError("Invalid option. Please try again.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    ColorfulConsole.WriteError(ex.Message);
                }
            }
        }
        private static bool TwoFactorAuth(ClientProxyATM atmProxy, out string username)
        {
            username = null;
            Console.Write("Enter username: ");
            username = Console.ReadLine();
            Console.Write("Enter 4-digit PIN: ");
            if (!int.TryParse(Console.ReadLine(), out int pin) || pin < 1000 || pin > 9999)
            {
                ColorfulConsole.WriteError("Invalid PIN. Must be a 4-digit number.");
                return false;
            }

            if (!atmProxy.AuthenticateUser(username, pin))
            {
                ColorfulConsole.WriteError($"Authentication failed. Invalid credentials.");
                return false;
            }

            return true;
        }
        private static bool TwoFactorAuthNoProxy(NetTcpBinding scsBinding, EndpointAddress currentAddress, out string username)
        {
            username = null;
            Console.Write("Enter username: ");
            username = Console.ReadLine();
            Console.Write("Enter 4-digit PIN: ");
            if (!int.TryParse(Console.ReadLine(), out int pin) || pin < 1000 || pin > 9999)
            {
                ColorfulConsole.WriteError("Invalid PIN. Must be a 4-digit number.");
                return false;
            }

            using (var atmProxy = new ClientProxyATM(scsBinding, currentAddress))
            {
                if (!atmProxy.AuthenticateUser(username, pin))
                {
                    ColorfulConsole.WriteError($"Authentication failed. Invalid credentials.");
                    return false;
                }
            }

            return true;
        }
        private static void SwitchEndpoint(ref EndpointAddress currentAddress, EndpointAddress primary, EndpointAddress backup)
        {
            currentAddress = currentAddress == primary ? backup : primary;
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[SmartCardsService] Switched to {(currentAddress == primary ? "primary" : "backup")} endpoint {currentAddress.Uri}");
            Console.WriteLine("Please try again.");
            Console.ResetColor();
        }
        static void TestSignedMessage(NetTcpBinding binding, EndpointAddress intermediaryAddress)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[ATM - SIGNED] Sending signed message...");
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
                    Console.WriteLine("[ATM - SIGNED] Sent successfully.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ATM - SIGNED] Failed: {ex.Message}");
            }
        }
        static void TestServiceConnection(NetTcpBinding binding, 
            ref EndpointAddress currentAddress, EndpointAddress primaryAddress, EndpointAddress backupAddress)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n[SmartCardsService] Attempting connection...");
            Console.ResetColor();

            try
            {
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
        public static void TestConnection(NetTcpBinding serviceBinding, NetTcpBinding atmBinding, 
            ref EndpointAddress currentAddress, EndpointAddress primaryAddress, EndpointAddress backupAddress, EndpointAddress atmAddress)
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
        }
    }
}