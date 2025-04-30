using Common;
using System;
using System.Linq;
using System.ServiceModel;
using System.Threading;
using System.Security.Cryptography;
using System.IO;
using System.Text.Json;
using System.Security.Cryptography.X509Certificates;
using System.IdentityModel.Claims;
using System.ServiceModel.Security;

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
                throw new FaultException<SecurityException>(new SecurityException(exceptionMessage));
            }

            if (string.IsNullOrWhiteSpace(username) || pin < 1000 || pin > 9999)
                throw new ArgumentException("Invalid username or PIN.");

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

            string filePath = Path.Combine(folderPath, $"{username}.json");
            string json = JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(filePath, json);
            ColorfulConsole.WriteSuccess($"SmartCard for user '{username}' created successfully.");
            Logger.LogEvent($"SmartCard for user '{username}' created successfully.");
            ReplicateToBackupServer(card);
        }
        public void UpdatePin(string username, int oldPin, int newPin)
        {
            if (!IsUserInValidGroup())
            {
                string name = Thread.CurrentPrincipal.Identity.Name;
                DateTime time = DateTime.Now;
                string exceptionMessage = String.Format("Access is denied.\n User {0} tried to call UpdatePin method (time: {1}).\n " +
                    "For this method user needs to be member of the group SmartCardUser or the group Manager.\n", name, time.TimeOfDay);
                throw new FaultException<SecurityException>(new SecurityException(exceptionMessage));
            }

            if (!ValidateSmartCard(username, oldPin))
            {
                throw new FaultException<SecurityException>(
                    new SecurityException("Access is denied. Invalid username or pin.\n"), 
                    new FaultReason("Security validation failed! Invalid username or pin."), 
                    new FaultCode("Sender"));
            }

            string filePath = Path.Combine(folderPath, $"{username}.json");
            string json = File.ReadAllText(filePath);
            SmartCard card = JsonSerializer.Deserialize<SmartCard>(json);

            card.PIN = HashPin(newPin);
            File.WriteAllText(filePath, JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true }));

            ColorfulConsole.WriteSuccess($"PIN changed for user '{username}'.");
            Logger.LogEvent($"PIN changed for user '{username}'.");
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
                string replicationAddress = localUri.Contains(":9001/SmartCardsReplication")
                    ? "net.tcp://localhost:9000/SmartCardsReplication"  // Main -> Backup
                    : "net.tcp://localhost:9001/SmartCardsReplication"; // Shouldn't normally happen

                NetTcpBinding binding = new NetTcpBinding();
                binding.Security.Mode = SecurityMode.None;
                binding.SendTimeout = TimeSpan.FromSeconds(5); // Fail fast if backup is down

                EndpointAddress address = new EndpointAddress(replicationAddress);

                var factory = new ChannelFactory<ISmartCardsService>(binding, address);
                proxy = factory.CreateChannel();
                channel = (IClientChannel)proxy;

                // Explicitly open channel to detect connection issues
                channel.Open();

                proxy.ReplicateSmartCard(card);

                // Verify channel state
                if (channel.State == CommunicationState.Faulted)
                {
                    throw new CommunicationException("Replication failed - channel faulted");
                }

                Console.WriteLine("Data successfully replicated to backup");
                Logger.LogEvent("Data successfully replicated to backup");
                channel.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Replication to backup failed: {ex.Message}");
                Logger.LogEvent($"ERROR: Replication to backup failed: {ex.Message}");
                channel?.Abort();

                // Consider throwing if caller needs to know replication failed
                // throw; 
            }
        }

        public string[] GetActiveUserAccounts()
        {
            try
            {
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SmartCards");

                if (!Directory.Exists(folderPath))
                {
                    Logger.LogEvent("SmartCards folder not found.");
                    return Array.Empty<string>();
                }

                string[] jsonFiles = Directory.GetFiles(folderPath, "*.json");

                string[] usernames = jsonFiles
                    .Select(file => Path.GetFileNameWithoutExtension(file))
                    .ToArray();

                Logger.LogEvent($"GetActiveUserAccounts called. Found {usernames.Length} users.");

                return usernames;
            }
            catch (Exception ex)
            {
                Logger.LogEvent($"Error in GetActiveUserAccounts: {ex.Message}");
                return Array.Empty<string>();
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

        public void ReplicateSmartCard(SmartCard card)
        {
            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string path = Path.Combine(folderPath, $"{card.SubjectName}.json");
            string json = JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);

            Logger.LogEvent($"[Replication] Received SmartCard for '{card.SubjectName}' at {path}");
        }
    }
}
