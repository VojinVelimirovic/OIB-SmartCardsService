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

        private bool UserExists(string username)
        {
            return _userCreditBalance.ContainsKey(username);
        }

        public double? GetBalance(string username)
        {
            if (UserExists(username))
            {
                return _userCreditBalance[username];
            }
            return null; // User not found
        }

        public bool Deposit(string username, double amount)
        {
            if (UserExists(username))
            {
                _userCreditBalance[username] += amount;
                return true; // Success
            }
            return false; // User not found
        }

        public bool Withdraw(string username, double amount) // not sufficient to be a bool
        {
            if (UserExists(username))
            {
                if (_userCreditBalance[username] >= amount)
                {
                    _userCreditBalance[username] -= amount;
                    return true; // Success
                }
            }
            return false; // User not found or insufficient funds
        }

        private void InitializeSampleData()
        {
            _userCreditBalance.Add("Marko", 500);
        }
    }
}