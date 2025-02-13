using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.IO;
using System.Text.Json;
using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace SmartCardsService
{
    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public class SmartCardsService : ISmartCardsService
    {
        private readonly string folderPath;
        private readonly string backupServerAddress = "net.tcp://backuphost:9998/SmartCardsService";


        public SmartCardsService()
        {
            string solutionDir = GetSolutionDirectory();
            folderPath = Path.Combine(solutionDir, "SmartCards");
        }

        private string GetSolutionDirectory()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var solutionDir = new DirectoryInfo(currentDirectory).Parent.Parent.Parent;
            return solutionDir.FullName;
        }

        public static bool IsUserInValidGroup()
        {
            return Thread.CurrentPrincipal.IsInRole("SmartCardUser") || Thread.CurrentPrincipal.IsInRole("Manager");
        }

        public void TestCommunication()
        {
            if (!IsUserInValidGroup())
            {
                string name = Thread.CurrentPrincipal.Identity.Name;
                string exceptionMessage = String.Format
                    ("Access is denied.\n User {0} tried to call TestCommunication method (time: {1}).\n ", name, DateTime.Now.TimeOfDay);
            }
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

        public bool ValidateSmartCard(string username, int pin)
        {
            string filePath = Path.Combine(folderPath, $"{username}.json");
            if (!File.Exists(filePath)) return false;

            string json = File.ReadAllText(filePath);
            SmartCard card = JsonSerializer.Deserialize<SmartCard>(json);

            string hashedPin = HashPin(pin);
            return card?.PIN == hashedPin;
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
                throw new FaultException<SecurityException>(new SecurityException("Access is denied. Invalid username or pin.\n"));
            }

            string filePath = Path.Combine(folderPath, $"{username}.json");
            string json = File.ReadAllText(filePath);
            SmartCard card = JsonSerializer.Deserialize<SmartCard>(json);

            card.PIN = HashPin(newPin);
            File.WriteAllText(filePath, JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true }));

            Logger.LogEvent($"PIN changed for user '{username}'.");
            ReplicateToBackupServer(card);
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
            try
            {
                NetTcpBinding binding = new NetTcpBinding();
                binding.Security.Mode = SecurityMode.Transport;
                binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;

                EndpointAddress address = new EndpointAddress(backupServerAddress);

                using (ChannelFactory<ISmartCardsService> factory = new ChannelFactory<ISmartCardsService>(binding, address))
                {
                    ISmartCardsService backupService = factory.CreateChannel();
                    backupService.CreateSmartCard(card.SubjectName, int.Parse(card.PIN)); // Call backup server
                    Logger.LogEvent($"Replication to backup server succeeded for user '{card.SubjectName}'.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogEvent($"ERROR: Replication to backup server failed: {ex.Message}");
            }
        }

        public bool Deposit(string username, int pin, float sum)
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

        public bool Withdraw(string username, int pin, float sum)
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
        public string VerifyClient()
        {
            try
            {
                // Step 1: Get the Windows Identity
                string windowsUser = ServiceSecurityContext.Current.WindowsIdentity?.Name ?? "Unknown Windows User";

                // Step 2: Retrieve the Client Certificate
                X509Certificate2 clientCert = OperationContext.Current.ServiceSecurityContext?.AuthorizationContext?.Properties["TransportSecurity"] as X509Certificate2;

                if (clientCert == null)
                {
                    throw new FaultException("Client certificate is missing.");
                }

                // Step 3: Extract the Organizational Unit (OU) from the certificate
                string subject = clientCert.Subject;
                string ou = ExtractOrganizationalUnit(subject) ?? "OU not found";

                // Step 4: Return combined authentication result
                return $"Windows User: {windowsUser} | Client Certificate OU: {ou}";
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        private string ExtractOrganizationalUnit(string subject)
        {
            // Extract "OU=" from certificate subject
            var parts = subject.Split(',')
                               .Select(part => part.Trim())
                               .Where(part => part.StartsWith("OU="))
                               .ToList();

            return parts.Count > 0 ? parts[0].Substring(3) : null;
        }
    }
}
