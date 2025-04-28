using Common;
using System;
using System.Collections.Generic;

namespace ATM
{
    public sealed class InMemoryDatabase
    {
        private static readonly Lazy<InMemoryDatabase> _instance = new Lazy<InMemoryDatabase>(
            () => new InMemoryDatabase());

        public static InMemoryDatabase Instance => _instance.Value;
        private Dictionary<string, double> _userCreditBalance;

        public InMemoryDatabase()
        {
            _userCreditBalance = new Dictionary<string, double>();
            InitializeSampleData();
        }

        public double? GetBalance(string username)
        {
            if (UserExists(username))
            {
                return _userCreditBalance[username];
            }
            return null; // User not found
        }

        public void Deposit(string username, double amount)
        {
            if (!UserExists(username))
                _userCreditBalance.Add(username, 0);

            _userCreditBalance[username] += amount;
            Console.WriteLine($"User {username} deposited {amount}. New balance: {_userCreditBalance[username]}");
            Logger.LogEvent($"User {username} deposited {amount}. New balance: {_userCreditBalance[username]}");
        }

        public void Withdraw(string username, double amount)
        {
            if (!UserExists(username))
            {
                _userCreditBalance.Add(username, 0);
                Console.WriteLine($"User {username} failed to withdraw {amount}. Balance: 0");
                Logger.LogEvent($"User {username} failed to withdraw {amount}. Balance: 0");
                return;
            }

            if (UserExists(username))
            {
                if (_userCreditBalance[username] >= amount)
                {
                    _userCreditBalance[username] -= amount;
                    Console.WriteLine($"User {username} withdrew {amount}. New balance: {_userCreditBalance[username]}");
                    Logger.LogEvent($"User {username} withdrew {amount}. New balance: {_userCreditBalance[username]}");
                }
            }
        }
        
        // Check if user exists in AccountBalance database, it is assumed that Service has already authenticated their existance
        private bool UserExists(string username)
        {
            return _userCreditBalance.ContainsKey(username);
        }

        private void InitializeSampleData()
        {
            _userCreditBalance.Add("Marko", 500);
        }
    }
}