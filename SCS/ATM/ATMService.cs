using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Security.Principal;
using Common;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel.Channels;
using System.Net.Security;
using System.Security.Authentication;
using System.ServiceModel.Security;

namespace ATM
{
    public class ATMService : IATMService
    {
        private readonly InMemoryDatabase _database;
        private ISmartCardsService smartCardService;
        private bool isAuthenticated = false;

        private readonly NetTcpBinding _binding;
        private readonly EndpointAddress _primaryAddress;
        private readonly EndpointAddress _backupAddress;
        private bool _usePrimary = true;

        public ATMService()
        {
            _binding = new NetTcpBinding(SecurityMode.Transport);
            _binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Certificate;
            _binding.Security.Transport.ProtectionLevel = ProtectionLevel.EncryptAndSign;

            // Set SSL/TLS protocol (use modern protocols)
            _binding.Security.Transport.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11;

            var serviceCert = CertManager.GetCertificateFromStorage(StoreName.TrustedPeople, StoreLocation.LocalMachine, "wcfservice");

            _primaryAddress = new EndpointAddress(
                new Uri("net.tcp://localhost:9999/SmartCardService"),
                new X509CertificateEndpointIdentity(serviceCert)
            );
            _backupAddress = new EndpointAddress(
                new Uri("net.tcp://localhost:9998/SmartCardService"),
                new X509CertificateEndpointIdentity(serviceCert)
            );

            _database = InMemoryDatabase.Instance;
            
        }
        public bool Ping()
        {
            return true; // Just returns true if the server is reachable
        }

        //private void ConnectToSmartCardService()
        //{
        //    NetTcpBinding binding = new NetTcpBinding
        //    {
        //        Security =
        //        {
        //            Mode = SecurityMode.Transport,
        //            Transport =
        //            {
        //                ClientCredentialType = TcpClientCredentialType.Windows,
        //                ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign
        //            }
        //        }
        //    };

        //    try
        //    {
        //        Console.WriteLine("Connecting to primary SmartCardsService...");
        //        smartCardService = CreateChannel(binding, primaryAddress);
        //        Console.WriteLine("ATM connected successfully.");
        //        smartCardService.TestCommunication();
        //    }
        //    catch (Exception)
        //    {
        //        Console.WriteLine("Primary SmartCardsService failed. Attempting backup...");
        //        try
        //        {
        //            smartCardService = CreateChannel(binding, backupAddress);
        //            smartCardService.TestCommunication();
        //            Console.WriteLine("Connected to backup SmartCardsService.");
        //        }
        //        catch (Exception ex)
        //        {
        //            Console.WriteLine("Failed to connect to both primary and backup SmartCardsService: \n" + ex.Message);
        //        }
        //    }
        //}

        private ISmartCardsService CreateChannel(NetTcpBinding binding, string address)
        {
            ChannelFactory<ISmartCardsService> factory = new ChannelFactory<ISmartCardsService>(binding, new EndpointAddress(address));
            return factory.CreateChannel();
        }

        public bool AuthenticateUser(string username, int pin)
        {
            try
            {
                return isAuthenticated = smartCardService.ValidateSmartCard(username, pin);
            }
            catch (CommunicationException)
            {
                Console.WriteLine("Lost connection to SmartCardsService. Reconnecting...");
                //ConnectToSmartCardService();
                return false;
            }
        }

        public double? GetBalance(string username)
        {
            return _database.GetBalance(username);
        }

        public bool Deposit(string username, double amount)
        {
            if (isAuthenticated)
            {
                return _database.Deposit(username, amount);
            }
            return false;

            // TODO: Send a request to SmartCardService
            //Console.WriteLine($"{username} deposited {amount}. New balance: {accountBalances[username]}");
            //Logger.LogEvent($"User {username} deposited {amount}. New balance: {accountBalances[username]}");
        }

        public bool Withdraw(string username, double amount)
        {
            if (isAuthenticated)
            {
                return _database.Withdraw(username, amount);
            }
            return false;

            // TODO: Send a request to SmartCardService

            //if (accountBalances.ContainsKey(username) && accountBalances[username] >= amount)
            //{
            //    accountBalances[username] -= amount;
            //    Console.WriteLine($"{username} withdrew {amount}. Remaining balance: {accountBalances[username]}");
            //    Logger.LogEvent($"User {username} withdrew {amount}. Remaining balance: {accountBalances[username]}");
            //    return true;
            //}
            //else
            //{
            //    Logger.LogEvent($"ERROR: User {username} attempted to withdraw {amount} but had insufficient funds.");
            //    throw new FaultException("Insufficient funds.");
            //}
        }

        public string[] GetActiveUserAccounts()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            if (!principal.IsInRole("Manager"))
            {
                Logger.LogEvent("ACCESS DENIED: Unauthorized attempt to access all accounts.");
                throw new FaultException("Access Denied: Only Managers can access this information.");
            }
            return new string[] { };
            // TODO: Send a request to SmartCardService
            
            //return accountBalances.Keys.ToArray();
        }

        public void TestCommunication()
        {
            throw new NotImplementedException();
        }

        public void SignedMessage(SignedRequest request)
        {
            Console.WriteLine($"Received signed message from {request.SenderName}");

            try
            {
                string certCN = request.SenderName + "_sign";
                var cert = CertManager.GetClientCertificate(certCN);

                bool isValid = DigitalSignature.Verify(request.Message, HashAlgorithm.SHA1, request.Signature, cert);

                if (!isValid)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("[INTERMEDIARY] Invalid signature.");
                    Console.ResetColor();
                    return;
                }

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("[INTERMEDIARY] Valid signature. Forwarding...");
                Console.ResetColor();

                bool success = TryForwardRequest(_usePrimary ? _primaryAddress : _backupAddress, request);

                if (!success)
                {
                    _usePrimary = !_usePrimary;
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"$Switched to {(_usePrimary ? "primary" : "backup")} endpoint");
                    Console.ResetColor();
                    success = TryForwardRequest(_usePrimary ? _primaryAddress : _backupAddress, request);
                }

                if (!success)
                    throw new Exception("Both service endpoints are unavailable.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception while processing signed message: {ex.Message}");
            }
        }

        private bool TryForwardRequest(EndpointAddress address, SignedRequest signedRequest = null)
        {
            ChannelFactory<ISmartCardsService> factory = null;
            IClientChannel client = null;

            try
            {
                factory = new ChannelFactory<ISmartCardsService>(_binding, address);

                // Present client certificate
                factory.Credentials.ClientCertificate.Certificate =
                    CertManager.GetCertificateFromStorage(StoreName.My, StoreLocation.LocalMachine, "oib_atm");

                // Configure proper validation
                factory.Credentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.ChainTrust;
                factory.Credentials.ServiceCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
                factory.Credentials.ServiceCertificate.Authentication.TrustedStoreLocation = StoreLocation.LocalMachine;

                client = (IClientChannel)factory.CreateChannel();
                client.Open();

                if (signedRequest != null)
                {
                    ((ISmartCardsService)client).SignedMessage(signedRequest);
                    Console.WriteLine($"Successfully forwarded signed message to {address.Uri}");
                }
                else
                {
                    ((ISmartCardsService)client).TestCommunication();
                    Console.WriteLine($"Successfully forwarded to {address.Uri}");
                }

                client.Close();
                return true;
            }
            catch (Exception ex)
            {
                client?.Abort();
                Console.WriteLine($"Failed to forward to {address.Uri}: {ex.Message}");
                return false;
            }
            finally
            {
                if (factory?.State != CommunicationState.Faulted)
                    factory?.Close();
                else
                    factory?.Abort();
            }
        }

        private bool TryForwardRequest(EndpointAddress address)
        {

            try
            {
                using (var factory = new ChannelFactory<ATMService>(_binding, address))
                {

                    factory.Credentials.ClientCertificate.Certificate =
                        CertManager.GetCertificateFromStorage(StoreName.My, StoreLocation.LocalMachine, "oib_atm");

                    factory.Credentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.ChainTrust;
                    factory.Credentials.ServiceCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
                    factory.Credentials.ServiceCertificate.Authentication.TrustedStoreLocation = StoreLocation.LocalMachine;

                    var channel = factory.CreateChannel();
                    channel.TestCommunication();
                    ((IClientChannel)channel).Close();

                    Console.WriteLine($"Successfully forwarded to {address.Uri}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to forward to {address.Uri}: {ex}");
                return false;
            }
        }

        //public static void PrintBalances()
        //{
        //      Request needs to be forwarded to SmartCardService
        //    Console.WriteLine("\n--- User Account Balances ---");
        //    Console.WriteLine("{0,-20} {1,10}", "Username", "Balance");
        //    // Print a separator line
        //    Console.WriteLine(new string('-', 30));
        //    // Loop through the dictionary and print each username and balance
        //    foreach (var account in UsersAccountBalance)
        //    {
        //        Console.WriteLine("{0,-20} {1,10:C}", account.Key, account.Value);
        //    }
        //    // Print a separator line
        //    Console.WriteLine(new string('-', 30));
        //}

    }
}
