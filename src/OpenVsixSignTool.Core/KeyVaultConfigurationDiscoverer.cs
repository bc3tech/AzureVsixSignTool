namespace OpenVsixSignTool.Core
{
    using System.Security.Cryptography.X509Certificates;
    using System.Threading.Tasks;

    using Azure.Identity;
    using Azure.Security.KeyVault.Certificates;
    using Azure.Security.KeyVault.Keys.Cryptography;

    using Crypto = System.Security.Cryptography;

    internal static class KeyVaultConfigurationDiscoverer
    {
        public static async Task<AzureKeyVaultMaterializedConfiguration> Materialize(AzureKeyVaultSignConfigurationSet configuration)
        {
            var creds = new ClientSecretCredential(configuration.AzureTenantId, configuration.AzureClientId, configuration.AzureClientSecret);

            var certClient = new CertificateClient(configuration.AzureKeyVaultUrl, creds);
            KeyVaultCertificateWithPolicy azureCertificate = await certClient.GetCertificateAsync(configuration.AzureKeyVaultCertificateName);
            var x509Certificate = new X509Certificate2(azureCertificate.Cer);

            return new AzureKeyVaultMaterializedConfiguration(new CryptographyClient(azureCertificate.KeyId, creds), x509Certificate, configuration.FileDigestAlgorithm, configuration.PkcsDigestAlgorithm);
        }
    }

    public class AzureKeyVaultMaterializedConfiguration
    {
        public AzureKeyVaultMaterializedConfiguration(CryptographyClient cryptographyClient, X509Certificate2 publicCertificate,
            Crypto.HashAlgorithmName fileDigestAlgorithm, Crypto.HashAlgorithmName pkcsDigestAlgorithm)
        {
            this.Client = cryptographyClient;
            this.PublicCertificate = publicCertificate;
            this.FileDigestAlgorithm = fileDigestAlgorithm;
            this.PkcsDigestAlgorithm = pkcsDigestAlgorithm;
        }

        public Crypto.HashAlgorithmName FileDigestAlgorithm { get; }
        public Crypto.HashAlgorithmName PkcsDigestAlgorithm { get; }

        public X509Certificate2 PublicCertificate { get; }
        public CryptographyClient Client { get; }
    }
}
