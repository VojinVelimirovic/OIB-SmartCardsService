using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Common
{
    public static class CertManager
    {
        public static string ParseName(string winLogonName)
        {
            string[] parts = new string[] { };

            if (winLogonName.Contains("@"))
            {
                ///UPN format
                parts = winLogonName.Split('@');
                return parts[0];
            }
            else if (winLogonName.Contains("\\"))
            {
                /// SPN format
                parts = winLogonName.Split('\\');
                return parts[1];
            }
            else
            {
                return winLogonName;
            }
        }

        // Gets cert based on username (default doesn't work because of OU field)
        public static X509Certificate2 GetClientCertificate(string userName)
        {
            string ou = userName == "oib_manager" || userName == "oib_manager_sign" ? "Manager" :
                        userName == "oib_smartcarduser" || userName == "oib_smartcarduser_sign" ? "SmartCardUser" :
                        string.Empty;

            if (string.IsNullOrEmpty(ou))
                return null;

            var store = new X509Store(StoreName.My, StoreLocation.LocalMachine);
            store.Open(OpenFlags.ReadOnly);

            try
            {
                foreach (X509Certificate2 cert in store.Certificates)
                {
                    if (cert.Subject == $"CN={userName}, OU={ou}" && cert.HasPrivateKey)
                    {
                        return cert;
                    }
                }
                return null;
            }
            finally
            {
                store.Close();
            }
        }

        // Works if there is no OU field
        public static X509Certificate2 GetCertificateFromStorage(StoreName storeName, StoreLocation storeLocation, string subjectName)
        {
            X509Store store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);

            X509Certificate2Collection certCollection = store.Certificates.Find(X509FindType.FindBySubjectName, subjectName, true);

            /// Check whether the subjectName of the certificate is exactly the same as the given "subjectName"
            foreach (X509Certificate2 c in certCollection)
            {
                if (c.SubjectName.Name.Equals(string.Format("CN={0}", subjectName)))
                {
                    return c;
                }
            }

            return null;
        }

        public static bool CurrentUserHasCertificate(string subjectName)
        {
            using (var store = new X509Store(StoreName.My, StoreLocation.LocalMachine))
            {
                store.Open(OpenFlags.ReadOnly | OpenFlags.OpenExistingOnly);

                foreach (var cert in store.Certificates)
                {
                    if (cert.Subject.IndexOf(subjectName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        try
                        {
                            // Try accessing the private key (throws if no permission)
                            using (var privateKey = cert.GetRSAPrivateKey())
                            {
                                if (privateKey != null)
                                    return true; // User has access
                            }
                        }
                        catch (CryptographicException)
                        {
                            // User lacks permissions for this cert
                            continue;
                        }
                    }
                }
            }
            return false; // Cert not found or no access
        }
    }
}
