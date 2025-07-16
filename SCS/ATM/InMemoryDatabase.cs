using Common;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;

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
            Console.WriteLine($"User {username} deposited {amount}. New balance: {_userCreditBalance[username]:N2} RSD.");
            Logger.LogEvent($"[ATM] User {username} deposited {amount}. New balance: {_userCreditBalance[username]:N2} RSD.");
        }

        public void Withdraw(string username, double amount)
        {
            if (!UserExists(username))
            {
                _userCreditBalance.Add(username, 0);
                Console.WriteLine($"User {username} failed to withdraw {amount:N2} RSD. Balance: 0.0 RSD.");
                Logger.LogEvent($"[ATM] User {username} failed to withdraw {amount::N2} RSD. Balance: 0.0 RSD.");
                throw new Exception("Insufficient balance. Balance: 0.0 RSD.");
            }
            else if(_userCreditBalance[username] >= amount)
            {
                _userCreditBalance[username] -= amount;
                Console.WriteLine($"User {username} withdrew {amount:N2} RSD. New balance: {_userCreditBalance[username]:N2} RSD.");
                Logger.LogEvent($"[ATM] User {username} withdrew {amount:N2} RSD. New balance: {_userCreditBalance[username]:N2} RSD.");
            }
        }
        
        // Check if user exists in AccountBalance database, it is assumed that Service has already authenticated their existance
        private bool UserExists(string username)
        {
            return _userCreditBalance.ContainsKey(username);
        }

        private void InitializeSampleData()
        {
            _userCreditBalance.Add("test", 500);
        }
    }
}