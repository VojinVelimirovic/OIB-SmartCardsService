using Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmartCardsService
{
    public class SmartCardsService : ISmartCardsService
    {
        public void TestCommunication(string message)
        {
            if (Thread.CurrentPrincipal.IsInRole("SmartCardUser") || Thread.CurrentPrincipal.IsInRole("Manager")) {
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
    }
}
