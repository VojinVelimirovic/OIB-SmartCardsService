using System;
using System.Linq;
using System.ServiceModel;
using Common;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Security.Authentication;
using System.ServiceModel.Security;

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

            // Set SSL/TLS protocol for communication with service
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

        public bool AuthenticateUser(string username, int pin, byte[] clientCert)
        {
            Console.WriteLine($"\nAuthenticating user '{username}' with SmartCardsService...");

            _CheckClientOU("SmartCardUser", clientCert);


            bool success = TryAuthenticate(_usePrimary ? _primaryAddress : _backupAddress, username, pin);

            if (!success)
            {
                _usePrimary = !_usePrimary;
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Switched to {(_usePrimary ? "primary" : "backup")} endpoint");
                Console.ResetColor();
                success = TryAuthenticate(_usePrimary ? _primaryAddress : _backupAddress, username, pin);
            }

            if (!success)
            {
                ColorfulConsole.WriteError($"Authentication failed for user '{username}'. Both service endpoints are unavailable.");
                return false;
            }

            return isAuthenticated;
        }


        private bool TryAuthenticate(EndpointAddress address, string username, int pin)
        {
            ChannelFactory<ISmartCardsService> factory = null;
            IClientChannel channel = null;

            try
            {
                factory = new ChannelFactory<ISmartCardsService>(_binding, address);

                factory.Credentials.ClientCertificate.Certificate =
                    CertManager.GetCertificateFromStorage(StoreName.My, StoreLocation.LocalMachine, "oib_atm");

                factory.Credentials.ServiceCertificate.Authentication.CertificateValidationMode = X509CertificateValidationMode.ChainTrust;
                factory.Credentials.ServiceCertificate.Authentication.RevocationMode = X509RevocationMode.NoCheck;
                factory.Credentials.ServiceCertificate.Authentication.TrustedStoreLocation = StoreLocation.LocalMachine;

                channel = (IClientChannel)factory.CreateChannel();
                channel.Open();

                bool response = ((ISmartCardsService)channel).ValidateSmartCard(username, pin);
                channel.Close();

                if (response)
                    ColorfulConsole.WriteSuccess($"User '{username}' successfully authenticated.");
                else
                    ColorfulConsole.WriteError($"Authentication failed for user '{username}'. Invalid credentials.");

                isAuthenticated = response;
                return true; // Return true for successful communication (regardless of auth result)
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to forward to {address.Uri}: {ex.Message}");
                return false; // Return false for communication failure
            }
            finally
            {
                _SafeClose(channel);
                _SafeClose(factory);
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
                try
                {
                    _database.Deposit(username, amount);
                    return true;
                }
                catch (Exception ex)
                {
                    ColorfulConsole.WriteError($"Error depositing credits: {ex.Message}");
                }
            }
            return false;
        }

        public bool Withdraw(string username, double amount, out string message)
        {
            if (!isAuthenticated)
            {
                message = "User not authenticated";
                return false;
            }

            try
            {
                _database.Withdraw(username, amount);
                message = $"Successfully withdrew {amount:N2} RSD";
                return true;
            }
            catch (Exception ex)
            {
                ColorfulConsole.WriteError($"Error withdrawing credits: {ex.Message}");
                message = ex.Message;
                return false;
            }
        }

        public string[] GetActiveUserAccounts(byte[] clientCert)
        {
            try
            {
                _CheckClientOU("Manager", clientCert);

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
                    throw new FaultException<SecurityException>(
                        new SecurityException("Failed to get user accounts: " + ex.Message), 
                        new FaultReason("Failed to get user accounts: " + ex.Message));
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
                _SafeClose(factory);
            }
        }

        private void _CheckClientOU(string expectedOU, byte[] clientCert)
        {
            if (clientCert == null || clientCert.Length == 0)
                throw new FaultException<SecurityException>(new SecurityException("Client certificate not provided."), new FaultReason("Client certificate not provided."));

            X509Certificate2 cert;
            try
            {
                cert = new X509Certificate2(clientCert);
            }
            catch (Exception)
            {
                throw new FaultException<SecurityException>(new SecurityException("Invalid certificate format."), new FaultReason("Invalid certificate format."));
            }

            var subject = cert.Subject; // e.g., "CN=client, OU=Manager"
            var ouPart = subject.Split(',')
                                .Select(s => s.Trim())
                                .FirstOrDefault(p => p.StartsWith("OU=", StringComparison.OrdinalIgnoreCase));

            if (ouPart == null || !ouPart.Equals($"OU={expectedOU}", StringComparison.OrdinalIgnoreCase))
                throw new FaultException<SecurityException>(new SecurityException($"You must be a certified {expectedOU} to access this method."),
                    new FaultReason($"You must be a certified {expectedOU} to access this method."));
        }

        private static void _SafeClose(ICommunicationObject comObj)
        {
            if (comObj == null) return;
            try
            {
                if (comObj.State != CommunicationState.Faulted)
                    comObj.Close();
                else
                    comObj.Abort();
            }
            catch
            {
                comObj.Abort();
            }
        }

        // For testing purposes
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

        // testing
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

        // testing
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
    }
}
