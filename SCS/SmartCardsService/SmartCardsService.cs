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
        private readonly string backupServerAddress = "net.tcp://localhost:9998/SmartCardsService";
        public SmartCardsService()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // store all JSON files in bin/Debug/SmartCards so that service and backup have each their own
            folderPath = Path.Combine(baseDir, "SmartCards");

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);
            //string solutionDir = GetSolutionDirectory();
            //folderPath = Path.Combine(solutionDir, "SmartCards");
        }

        private string GetSolutionDirectory()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var solutionDir = new DirectoryInfo(currentDirectory).Parent.Parent.Parent;
            return solutionDir.FullName;
        }

        public void TestCommunication()
        {
            Console.WriteLine("Communication established.");
        }
        public void SignedMessage(SignedRequest request)
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
            //ATM.Create(username);
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
                // Use the unsecured replication endpoint
                string replicationAddress = "net.tcp://localhost:9000/SmartCardsReplication";

                NetTcpBinding binding = new NetTcpBinding();
                binding.Security.Mode = SecurityMode.None; // No security for replication

                EndpointAddress address = new EndpointAddress(replicationAddress);

                var factory = new ChannelFactory<ISmartCardsService>(binding, address);
                proxy = factory.CreateChannel();
                channel = (IClientChannel)proxy;

                proxy.ReplicateSmartCard(card);
                Console.WriteLine("Data successfully replicated");
                Logger.LogEvent("Data sucessfully replicated");
                channel.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Replication failed: {ex.Message}");
                Logger.LogEvent($"ERROR: Replication failed: {ex.Message}");
                if (channel != null)
                {
                    channel.Abort();
                }
            }
        }

        public bool Deposit(string username, int pin, double sum)
        {
            if (!IsUserInValidGroup())
            {
                string name = Thread.CurrentPrincipal.Identity.Name;
                DateTime time = DateTime.Now;
                string exceptionMessage = String.Format("Access is denied.\n User {0} tried to call AddBalance method (time: {1}).\n " +
                    "For this method user needs to be member of the group SmartCardUser or the group Manager.\n", name, time.TimeOfDay);
                throw new FaultException<SecurityException>(new SecurityException(exceptionMessage));
            }
            if (!ValidateSmartCard(username, pin))
            {
                throw new FaultException<SecurityException>(new SecurityException("Access is denied.\nInvalid username/pin.\n"));
            }
            // ATM atm = ATM.Create(username);
            Logger.LogEvent($"User '{username}' added {sum} to his ATM balance.");
            return true;
          //  return atm.AddBalance(sum);
        }

        public bool Withdraw(string username, int pin, double sum)
        {
            if (!IsUserInValidGroup())
            {
                string name = Thread.CurrentPrincipal.Identity.Name;
                DateTime time = DateTime.Now;
                string exceptionMessage = String.Format("Access is denied.\n User {0} tried to call RemoveBalance method (time: {1}).\n " +
                    "For this method user needs to be member of the group SmartCardUser or the group Manager.\n", name, time.TimeOfDay);
                throw new FaultException<SecurityException>(new SecurityException(exceptionMessage));
            }
            if (!ValidateSmartCard(username, pin))
            {
                throw new FaultException<SecurityException>(new SecurityException("Access is denied.\nInvalid username/pin.\n"));
            }
            // ATM atm = ATM.Create(username);
            Logger.LogEvent($"User '{username}' added {sum} to his ATM balance.");
            return true;
            // return atm.RemoveBalance(sum);
        }

        public string[] GetActiveUserAccounts()
        {
            if (!Thread.CurrentPrincipal.IsInRole("SmartCardUser"))
            {
                throw new FaultException<SecurityException>(new SecurityException("Access is denied. For this method user needs to be member of the group Manager.\n"));
            }
            return new string[] { };
            //return ATM.UsersAccountBalance.Keys.ToList();
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
            string ou = ExtractOrganizationalUnit(clientCert.Subject);

            // 3) Only allow if OU is exactly one of these
            return ou == "SmartCardUser" || ou == "Manager";
        }

        private static string ExtractOrganizationalUnit(string subject)
        {
            var parts = subject.Split(',')
                               .Select(p => p.Trim())
                               .Where(p => p.StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
                               .ToList();
            return parts.Count > 0 ? parts[0].Substring(3) : null;
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
