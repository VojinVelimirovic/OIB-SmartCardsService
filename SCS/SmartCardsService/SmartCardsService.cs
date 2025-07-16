using Common;
using System;
using System.IdentityModel.Claims;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.ServiceModel;
using System.Text.Json;
using System.Threading;

namespace SmartCardsService
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class SmartCardsService : ISmartCardsService
    {
        private readonly string folderPath;
        public SmartCardsService()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // store all JSON files in bin/Debug/SmartCards so that service and backup have each their own
            folderPath = Path.Combine(baseDir, "SmartCards");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
        }

        public void TestCommunication() // testing
        {
            Console.WriteLine("Communication established.");
        }
        public void SignedMessage(SignedRequest request) // testing
        {
            Console.WriteLine("ATM message received");
        }

        public void CreateSmartCard(string username, int pin)
        {
            if (!IsUserInValidGroup())
            {
                string name = Thread.CurrentPrincipal.Identity.Name;
                DateTime time = DateTime.Now;
                string exceptionMessage = String.Format
                    ("Access is denied.\n User {0} tried to call CreateSmartCard method (time: {1}).\n " +
                    "For this method user needs to be member of the group SmartCardUser or the group Manager.\n", name, DateTime.Now.TimeOfDay);
                Logger.LogEvent("[SmartCardsService] ERROR: " + exceptionMessage);

                throw new FaultException<SecurityException>(new SecurityException(exceptionMessage));
            }

            if (string.IsNullOrWhiteSpace(username) || pin < 1000 || pin > 9999)
                throw new ArgumentException("Invalid username or PIN.");

            // Check if smart card already exists
            string filePath = Path.Combine(folderPath, $"{username}.json");
            if (File.Exists(filePath))
            {
                string errorMessage = $"SmartCard for user '{username}' already exists.";
                ColorfulConsole.WriteError(errorMessage);
                Logger.LogEvent("[SmartCardsService] ERROR: " + errorMessage);
                throw new ArgumentException(errorMessage);
            }

            string hashedPin = HashPin(pin);

            SmartCard card = new SmartCard
            {
                SubjectName = username,
                PIN = hashedPin
            };

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            string json = JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            ColorfulConsole.WriteSuccess($"SmartCard for user '{username}' created successfully.");
            Logger.LogEvent($"[SmartCardsService] SmartCard for user '{username}' created successfully.");
            ReplicateToBackupServer(card);
        }

        public bool SmartCardExists(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return false;

            string filePath = Path.Combine(folderPath, $"{username}.json");
            return File.Exists(filePath);
        }

        public void UpdatePin(string username, int oldPin, int newPin)
        {
            if (!IsUserInValidGroup())
            {
                string name = Thread.CurrentPrincipal.Identity.Name;
                DateTime time = DateTime.Now;
                string exceptionMessage = String.Format("Access is denied.\n User {0} tried to call UpdatePin method (time: {1}).\n " +
                    "For this method user needs to be member of the group SmartCardUser or the group Manager.\n", name, time.TimeOfDay);
                Logger.LogEvent("[SmartCardsService] ERROR: " + exceptionMessage);

                throw new FaultException<SecurityException>(new SecurityException(exceptionMessage));
            }

            if (!ValidateSmartCard(username, oldPin))
            {
                string name = Thread.CurrentPrincipal.Identity.Name;
                string exceptionMessage = String.Format("Security validation failed for user {0}! Invalid username or pin.", name);
                Logger.LogEvent("[SmartCardsService] ERROR: " + exceptionMessage);

                throw new FaultException<SecurityException>(
                    new SecurityException("Access is denied. Invalid username or pin.\n"), 
                    new FaultReason(exceptionMessage), 
                    new FaultCode("Sender"));
            }

            string filePath = Path.Combine(folderPath, $"{username}.json");
            string json = File.ReadAllText(filePath);
            SmartCard card = JsonSerializer.Deserialize<SmartCard>(json);

            card.PIN = HashPin(newPin);
            File.WriteAllText(filePath, JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true }));

            ColorfulConsole.WriteSuccess($"PIN changed for user '{username}'.");
            Logger.LogEvent($"[SmartCardsService] PIN changed for user '{username}'.");
            ReplicateToBackupServer(card);
        }

        public bool ValidateSmartCard(string username, int pin)
        {
            string filePath = Path.Combine(folderPath, $"{username}.json");
            if (!File.Exists(filePath)) return false;

            string json = File.ReadAllText(filePath);
            SmartCard card = JsonSerializer.Deserialize<SmartCard>(json);

            string hashedPin = HashPin(pin);

            Console.WriteLine($"Received validation request for {username}.");
            
            return card?.PIN == hashedPin;
        }

        private string HashPin(int pin)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(pin.ToString()));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private void ReplicateToBackupServer(SmartCard card)
        {
            ISmartCardsService proxy = null;
            IClientChannel channel = null;

            try
            {
                string localUri = OperationContext.Current.IncomingMessageHeaders.To?.ToString() ?? "";

                // Prevent infinite replication loop: if this came in on port 9000, it's a backup, don't send again
                if (localUri.Contains(":9000/SmartCardsReplication"))
                    return;

                // If this arrived on 9001 (we're in main), send to backup on 9000
                // Otherwise (shouldn't happen), default to main on 9001
                string replicationAddress = localUri.Contains(":9999/SmartCardsService")
                    ? "net.tcp://localhost:9000/SmartCardsReplication"  // Main -> Backup
                    : "net.tcp://localhost:9001/SmartCardsReplication"; // Backup -> Main

                NetTcpBinding binding = new NetTcpBinding();
                //binding.Security.Mode = SecurityMode.None;
                binding.Security.Mode = SecurityMode.Transport;
                binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
                binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
                binding.SendTimeout = TimeSpan.FromSeconds(5); // Fail fast if backup is down

                EndpointAddress address = new EndpointAddress(replicationAddress);

                var factory = new ChannelFactory<ISmartCardsService>(binding, address);
                proxy = factory.CreateChannel();
                channel = (IClientChannel)proxy;

                // Explicitly open channel to detect connection issues
                channel.Open();

                proxy.ReplicateSmartCard(card);

                Console.WriteLine("Data successfully replicated to backup");
                Logger.LogEvent("[SmartCardsService] Data successfully replicated to backup");
                channel.Close();
            }
            catch (Exception ex)
            {
                ColorfulConsole.WriteError($"Replication to backup failed: {ex.Message}");
                Logger.LogEvent($"[SmartCardsService] ERROR: Replication to backup failed: {ex.Message}");
                channel?.Abort();
            }
        }

        public string[] GetActiveUserAccounts()
        {
            try
            {
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartCards");

                if (!Directory.Exists(folderPath))
                {
                    return Array.Empty<string>();
                }

                string[] jsonFiles = Directory.GetFiles(folderPath, "*.json");

                string[] usernames = jsonFiles
                    .Select(file => Path.GetFileNameWithoutExtension(file))
                    .ToArray();

                return usernames;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in GetActiveUserAccounts: {ex.Message}");
                return Array.Empty<string>();
            }
        }


        public void ReplicateSmartCard(SmartCard card)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            try
            {
                string path = Path.Combine(folderPath, $"{card.SubjectName}.json");
                string json = JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);

                Logger.LogEvent($"[SmartCardsService Replication] Received SmartCard for '{card.SubjectName}' at {path}");
            }
            catch (Exception ex)
            {
                Logger.LogEvent($"[SmartCardsService Replication] ERROR: Failed to save SmartCard for '{card.SubjectName}': {ex.Message}");
                throw;
            }
        }

        //////////////////////////////////
        private static bool IsUserInValidGroup()
        {
            // 1) Find the X509 certificate claimset
            var certClaimSet = ServiceSecurityContext
                                 .Current
                                 .AuthorizationContext
                                 .ClaimSets
                                 .OfType<X509CertificateClaimSet>()
                                 .FirstOrDefault();

            if (certClaimSet == null)
                return false;

            // 2) Get the X509Certificate2 and extract its OU
            var clientCert = certClaimSet.X509Certificate;
            string ou = CertManager.ExtractOrganizationalUnit(clientCert.Subject);

            // 3) Only allow if OU is exactly one of these
            return ou == "SmartCardUser" || ou == "Manager";
        }
    }
}
