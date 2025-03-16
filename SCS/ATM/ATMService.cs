using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Security.Principal;
using Common;
using System.Diagnostics;

namespace ATM
{
    public class ATMService : IATMService
    {
        private ISmartCardsService smartCardService;
        private readonly InMemoryDatabase _database;
        private string primaryAddress = "net.tcp://localhost:9999/SmartCardsService";
        private string backupAddress = "net.tcp://localhost:9998/SmartCardsService";
        private bool isAuthenticated = false;

        public ATMService()
        {
            _database = InMemoryDatabase.Instance;
            ConnectToSmartCardService();
        }
        public bool Ping()
        {
            return true; // Just returns true if the server is reachable
        }

        private void ConnectToSmartCardService()
        {
            NetTcpBinding binding = new NetTcpBinding
            {
                Security =
                {
                    Mode = SecurityMode.Transport,
                    Transport =
                    {
                        ClientCredentialType = TcpClientCredentialType.Windows,
                        ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign
                    }
                }
            };

            try
            {
                Console.WriteLine("Connecting to primary SmartCardsService...");
                smartCardService = CreateChannel(binding, primaryAddress);
                Console.WriteLine("ATM connected successfully.");
                smartCardService.TestCommunication();
            }
            catch (Exception)
            {
                Console.WriteLine("Primary SmartCardsService failed. Attempting backup...");
                try
                {
                    smartCardService = CreateChannel(binding, backupAddress);
                    smartCardService.TestCommunication();
                    Console.WriteLine("Connected to backup SmartCardsService.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to connect to both primary and backup SmartCardsService: \n" + ex.Message);
                }
            }
        }

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
                ConnectToSmartCardService();
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
