using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartCardsService
{
    public sealed class ATM
    {
        public static Dictionary<string, float> UsersAccountBalance = new Dictionary<string, float>();

        public string CurrentUser {  get; private set; }
        private ATM(string username)
        {
            CurrentUser = username;
        }

        public static ATM Create(string username)
        {
            // If user doesn't exist, add them to the dict
            if (!UsersAccountBalance.ContainsKey(username))
            {
                UsersAccountBalance.Add(username, 0);
            }
            return new ATM(username);
        }

        public bool AddBalance(float sum)
        {
            try
            {
                UsersAccountBalance[CurrentUser] += sum;
                SmartCardsService.LogEvent($"User {CurrentUser} has Added {sum} to their balance. " +
                    $"Balance: {UsersAccountBalance[CurrentUser]}");
            }
            catch (Exception ex)
            {
                SmartCardsService.LogEvent($"SYS_ERROR: User {CurrentUser} tried to Add {sum} to their balance, " +
                    $"but the operation failed. " +
                    $"{ex.Message}");
                return false;
            }
            return true;
        }

        public bool RemoveBalance(float sum)
        {
            // Check if user has enough balance
            if (UsersAccountBalance[CurrentUser] < sum)
            {
                SmartCardsService.LogEvent($"ERROR: User {CurrentUser} tried to Remove {sum} from their balance," +
                    $"but had insufficient credits. " +
                    $"Balance: {UsersAccountBalance[CurrentUser]}");
                return false;
            }
            try
            {
                UsersAccountBalance[CurrentUser] -= sum;
                SmartCardsService.LogEvent($"User {CurrentUser} has Removed {sum} from their balance. " +
                    $"Balance: {UsersAccountBalance[CurrentUser]}");
            }
            catch (Exception ex)
            {
                SmartCardsService.LogEvent($"SYS_ERROR: User {CurrentUser} tried to Remove {sum} from their balance, " +
                    $"but the operation failed. " +
                    $"{ex.Message}");
                return false;
            }

            return true;
        }
    }
}
