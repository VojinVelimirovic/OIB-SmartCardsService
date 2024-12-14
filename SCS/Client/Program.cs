using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace Client
{
    internal class Program
    {
        static void Main(string[] args)
        {
            NetTcpBinding binding = new NetTcpBinding();
            string address = "net.tcp://localhost:9999/SmartCardsService";

            binding.Security.Mode = SecurityMode.Transport;
            binding.Security.Transport.ClientCredentialType = TcpClientCredentialType.Windows;
            binding.Security.Transport.ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;

            Console.WriteLine("Korisnik koji je pokrenuo klijenta je : " + WindowsIdentity.GetCurrent().Name);

            using (ClientProxy proxy = new ClientProxy(binding, address))
            {
                //NAPOMENA: DA BI RADILO MORAS POKRENUTI KLIJENTA PREKO WINDOWS IDENTITIY-JA
                //KOJI JE ILI U GRUPI Manager ILI U GRUPI SmartCardUser
                //NAKON DODAVANJA WINDOWS IDENTITY-JA U GRUPU MORAS DA SE IZLOGUJES I ULOGUJES ILI SE NECE UPDATE-OVATI
                proxy.TestCommunication("Zdravo");
                proxy.TestCommunication("Klikni nesto da bi ugasio");
            }

            Console.ReadLine();
        }
    }
}
