﻿using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
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
            factory.CreateSmartCard(username, pin);
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
            factory.UpdatePin(username, oldPin, newPin);
        }

        public bool ValidateSmartCard(string username, int pin)
        {
            return factory.ValidateSmartCard(username, pin);
        }
    }
}
