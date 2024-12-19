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

namespace SmartCardsService
{
    public class SmartCardsService : ISmartCardsService
    {
        private readonly string folderPath;

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
        public void TestCommunication(string message)
        {
            if (Thread.CurrentPrincipal.IsInRole("SmartCardUser") || Thread.CurrentPrincipal.IsInRole("Manager"))
            {
                Console.WriteLine(message);
            }
            else
            {
                string name = Thread.CurrentPrincipal.Identity.Name;
                DateTime time = DateTime.Now;
                string exceptionMessage = String.Format("Access is denied.\n User {0} tried to call TestCommunication method (time: {1}).\n " +
                    "For this method user needs to be member of the group SmartCardUser or the group Manager.\n", name, time.TimeOfDay);
                throw new FaultException<SecurityException>(new SecurityException(exceptionMessage));
            }

        }

        public void CreateSmartCard(string username, int pin)
        {
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

            LogEvent($"SmartCard for user '{username}' created successfully.");
        }

        public bool ValidateSmartCard(string username, int pin)
        {
            string filePath = Path.Combine(folderPath, $"{username}.json");
            if (!File.Exists(filePath))
                return false;

            string json = File.ReadAllText(filePath);
            SmartCard card = JsonSerializer.Deserialize<SmartCard>(json);

            string hashedPin = HashPin(pin);
            return card?.PIN == hashedPin;
        }


        public void UpdatePin(string username, int oldPin, int newPin)
        {
            if (!ValidateSmartCard(username, oldPin))
            {
                //throw new SecurityException("Invalid username or old PIN.");
            }

            string filePath = Path.Combine(folderPath, $"{username}.json");
            string json = File.ReadAllText(filePath);
            SmartCard card = JsonSerializer.Deserialize<SmartCard>(json);

            card.PIN = HashPin(newPin);
            File.WriteAllText(filePath, JsonSerializer.Serialize(card, new JsonSerializerOptions { WriteIndented = true }));

            LogEvent($"PIN changed for user '{username}'.");
        }

        private string HashPin(int pin)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hashBytes = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(pin.ToString()));
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        private void LogEvent(string message)
        {
            const string source = "SmartCardService"; // Name of the source for the event log
            const string logName = "Application"; // The log name you want to write to

            // Check if the source exists, if not, create it
            if (!EventLog.SourceExists(source))
            {
                EventLog.CreateEventSource(source, logName);
            }

            // Write an entry to the event log
            EventLog.WriteEntry(source, message, EventLogEntryType.Information);
        }


    }
}
