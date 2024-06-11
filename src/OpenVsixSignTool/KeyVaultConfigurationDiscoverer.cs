using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Azure.Identity;
using Azure.Security.KeyVault.Certificates;
using Azure.Security.KeyVault.Keys.Cryptography;

namespace OpenVsixSignTool
{
    internal class KeyVaultConfigurationDiscoverer
    {
        public async Task<ErrorOr<AzureKeyVaultMaterializedConfiguration>> Materialize(AzureKeyVaultSignConfigurationSet configuration)
        {
            var creds = new ClientSecretCredential(configuration.AzureTenantId, configuration.AzureClientId, configuration.AzureClientSecret);

            var certClient = new CertificateClient(new System.Uri(configuration.AzureKeyVaultUrl), creds);
            KeyVaultCertificateWithPolicy azureCertificate = await certClient.GetCertificateAsync(configuration.AzureKeyVaultCertificateName);
            var x509Certificate = new X509Certificate2(azureCertificate.Cer);
            return new AzureKeyVaultMaterializedConfiguration(creds, azureCertificate.KeyId, x509Certificate);
        }
    }
}
