using System;
using System.Security.Cryptography.X509Certificates;

using Azure.Identity;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace OpenVsixSignTool
{
    public class AzureKeyVaultMaterializedConfiguration
    {
        public AzureKeyVaultMaterializedConfiguration(ClientSecretCredential creds, Uri keyId, X509Certificate2 x509Certificate)
        {
            this.Credentials = creds;
            this.KeyId = keyId;
            this.PublicCertificate = x509Certificate;
        }

        public X509Certificate2 PublicCertificate { get; }
        public ClientSecretCredential Credentials { get; }
        public Uri KeyId { get; }
    }
}
