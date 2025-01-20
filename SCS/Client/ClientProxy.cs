using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    internal class ClientProxy : ChannelFactory<ISmartCardsService>, ISmartCardsService, IDisposable
    {
        ISmartCardsService factory;

        public ClientProxy(NetTcpBinding binding, string address) : base(binding, address)
        {
            factory = this.CreateChannel();
        }

        public void CreateSmartCard(string username, int pin)
        {
            try
            {
                factory.CreateSmartCard(username, pin);
            }
            catch (FaultException<SecurityException> e)
            {
                Console.WriteLine("Error while trying to update pin: {0}", e.Detail.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error while trying to update pin: {0}", e.Message);
            }
        }

        public void TestCommunication(string message)
        {
            try
            {
                factory.TestCommunication(message);
            }
            catch (FaultException<SecurityException> e)
            {
                Console.WriteLine("Error while trying to test communication: {0}", e.Detail.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error while trying to test communication: {0}", e.Message);
            }
        }

        public void UpdatePin(string username, int oldPin, int newPin)
        {
            try
            {
                factory.UpdatePin( username, oldPin, newPin);
            }
            catch (FaultException<SecurityException> e)
            {
                Console.WriteLine("Error while trying to update pin: {0}", e.Detail.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Error while trying to update pin: {0}", e.Message);
            }
        }

        public bool ValidateSmartCard(string username, int pin)
        {
            try
            {
                return factory.ValidateSmartCard(username, pin);
            }
            catch (FaultException<SecurityException> e)
            {
                Console.WriteLine("Error while trying to validate pin: {0}", e.Detail.Message);
                return false;
            }
            catch (Exception e)
            {

                Console.WriteLine("Error while trying to validate pin: {0}", e.Message);
                return false;
            }
        }

        public bool AddBalance(string username, int pin, float amount) {
            try
            {
                return factory.AddBalance(username, pin, amount);
            }
            catch (FaultException<SecurityException> e)
            {
                Console.WriteLine("Error while trying to add balance: {0}", e.Detail.Message);
                return false;
            }
            catch (Exception e)
            {

                Console.WriteLine("Error while trying to add balance: {0}", e.Message);
                return false;
            }
        }

        public bool RemoveBalance(string username, int pin, float amount)
        {
            try
            {
                return factory.RemoveBalance(username, pin, amount);
            }
            catch (FaultException<SecurityException> e)
            {
                Console.WriteLine("Error while trying to remove balance: {0}", e.Detail.Message);
                return false;
            }
            catch (Exception e)
            {

                Console.WriteLine("Error while trying to remove balance: {0}", e.Message);
                return false;
            }
        }

        public List<string> GetActiveUserAccounts()
        {
            try
            {
                return factory.GetActiveUserAccounts();
            }
            catch (FaultException<SecurityException> e)
            {
                Console.WriteLine("Error while trying to print active users: {0}", e.Detail.Message);
                return null;
            }
            catch (Exception e)
            {

                Console.WriteLine("Error while trying to print active users: {0}", e.Message);
                return null;
            }
        }
    }
}
