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
using System.Net;

namespace ATM
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class ATMService : IATMService
    {
        private readonly InMemoryDatabase _database;
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
                new Uri("net.tcp://localhost:9999/SmartCardsService"),
                new X509CertificateEndpointIdentity(serviceCert)
            );
            _backupAddress = new EndpointAddress(
                new Uri("net.tcp://localhost:9998/SmartCardsService"),
                new X509CertificateEndpointIdentity(serviceCert)
            );

            _database = InMemoryDatabase.Instance;
        }
        public void TestCommunication()
        {
            Console.WriteLine($"Client called Intermediary at {DateTime.Now}");

            bool success = TryForwardRequest(_usePrimary ? _primaryAddress : _backupAddress);

            if (!success)
            {
                // Switch endpoint and retry
                _usePrimary = !_usePrimary;

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Switched to {(_usePrimary ? "primary" : "backup")} endpoint");
                Console.ResetColor();
                success = TryForwardRequest(_usePrimary ? _primaryAddress : _backupAddress);
            }

            if (!success)
            {
                throw new EndpointNotFoundException("Both service endpoints are unavailable");
            }
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

        public bool AuthenticateUser(string username, int pin)
        {
            try
            {
                Console.WriteLine($"Authenticating user '{username}' with SmartCardsService...");

                // TODO: Make it work with backup
                using (var factory = new ChannelFactory<ISmartCardsService>(_binding, _primaryAddress))
                {

                    factory.Credentials.ClientCertificate.Certificate =
                        CertManager.GetCertificateFromStorage(StoreName.My, StoreLocation.LocalMachine, "oib_atm");

                    factory.Credentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.ChainTrust;
                    factory.Credentials.ServiceCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
                    factory.Credentials.ServiceCertificate.Authentication.TrustedStoreLocation = StoreLocation.LocalMachine;

                    var channel = factory.CreateChannel();
                    bool response = channel.ValidateSmartCard(username, pin);
                    ((IClientChannel)channel).Close();

                    if (response)
                        ColorfulConsole.WriteSuccess($"User '{username}' successfully authenticated.");
                    else
                        ColorfulConsole.WriteError($"Authentication failed for user '{username}'. Invalid credentials.");
                    
                    isAuthenticated = response;
                    return response;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to forward to {_primaryAddress.Uri}: {ex}");
                return false;
            }

            //ISmartCardsService proxy = null;
            //IClientChannel channel = null;

            //try
            //{
            //    var factory = new ChannelFactory<ISmartCardsService>(_binding, _primaryAddress);

            //    proxy = factory.CreateChannel();
            //    channel = (IClientChannel)proxy;
            //    bool response = proxy.ValidateSmartCard(username, pin);
            //    channel.Close();
            //    factory.Close();

            //    return response;
            //}
            //catch (Exception ex)
            //{
            //    Console.WriteLine($"ERROR: Failed to reach service: {ex.Message}");
            //    Logger.LogEvent($"ERROR: Failed to reach service: {ex.Message}");
                
            //    if (channel != null)
            //        channel.Abort();
                
            //    return false;
            //}
        }

        public double? GetBalance(string username)
        {
            return _database.GetBalance(username);
        }

        public bool Deposit(string username, double amount)
        {
            if (isAuthenticated)
            {
                try
                {
                    _database.Deposit(username, amount);
                    return true;
                }
                catch (Exception ex)
                {
                    ColorfulConsole.WriteError($"Error depositing credis {ex.Message}");
                }
            }
            return false;
        }

        public bool Withdraw(string username, double amount)
        {
            if (isAuthenticated)
            {
                _database.Withdraw(username, amount);
                return true;
            }
            return false;
        }

        public string[] GetActiveUserAccounts(byte[] clientCert)
        {
            try
            {
                if (clientCert == null || clientCert.Length == 0)
                    throw new FaultException<SecurityException>(new SecurityException("Client certificate not provided."),new FaultReason("Client certificate not provided."));

                X509Certificate2 cert;
                try
                {
                    cert = new X509Certificate2(clientCert);
                }
                catch (Exception)
                {
                    throw new FaultException<SecurityException>(new SecurityException("Invalid certificate format."), new FaultReason("Invalid certificate format."));
                }

                var subject = cert.Subject; // e.g., "CN=client, OU=Manager, O=Org, C=US"
                var ouPart = subject.Split(',')
                                    .Select(s => s.Trim())
                                    .FirstOrDefault(p => p.StartsWith("OU=", StringComparison.OrdinalIgnoreCase));

                if (ouPart == null || !ouPart.Equals("OU=Manager", StringComparison.OrdinalIgnoreCase))
                    throw new FaultException<SecurityException>(new SecurityException("You must be a certified Manager to access this method."), new FaultReason("You must be a certified Manager to access this method."));

                string[] result = null;
                bool success = false;

                try
                {
                    result = CallGetActiveUserAccounts(_usePrimary ? _primaryAddress : _backupAddress, out success);

                    if (!success)
                    {
                        _usePrimary = !_usePrimary;
                        Console.WriteLine($"Switched to {(_usePrimary ? "primary" : "backup")} endpoint");

                        result = CallGetActiveUserAccounts(_usePrimary ? _primaryAddress : _backupAddress, out success);
                    }

                    if (!success)
                        throw new EndpointNotFoundException("Both service endpoints are unavailable.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during CallGetActiveUserAccounts: {ex.Message}");
                    throw new FaultException<SecurityException>(new SecurityException("Failed to get user accounts: " + ex.Message), new FaultReason("Failed to get user accounts: " + ex.Message));
                }

                return result;
            }
            catch (FaultException<SecurityException>)
            {
                throw; // allow explicitly thrown security faults through
            }
            catch (Exception ex)
            {
                // Catch-all to ensure you never return an untyped Fault
                throw new FaultException<SecurityException>(new SecurityException("Internal server error: " + ex.Message));
            }
        }


        private string[] CallGetActiveUserAccounts(EndpointAddress address, out bool success)
        {
            success = false;
            ChannelFactory<ISmartCardsService> factory = null;
            IClientChannel client = null;

            try
            {
                factory = new ChannelFactory<ISmartCardsService>(_binding, address);

                factory.Credentials.ClientCertificate.Certificate =
                    CertManager.GetCertificateFromStorage(StoreName.My, StoreLocation.LocalMachine, "oib_atm");

                factory.Credentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.ChainTrust;
                factory.Credentials.ServiceCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
                factory.Credentials.ServiceCertificate.Authentication.TrustedStoreLocation = StoreLocation.LocalMachine;

                var channel = factory.CreateChannel();
                client = (IClientChannel)channel;

                client.Open();

                var users = channel.GetActiveUserAccounts();

                client.Close();
                success = true;
                return users;
            }
            catch (Exception ex)
            {
                client?.Abort();
                Console.WriteLine($"Failed to get active user accounts from {address.Uri}: {ex.Message}");
                return Array.Empty<string>();
            }
            finally
            {
                if (factory?.State != CommunicationState.Faulted)
                    factory?.Close();
                else
                    factory?.Abort();
            }
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
                using (var factory = new ChannelFactory<ISmartCardsService>(_binding, address))
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
