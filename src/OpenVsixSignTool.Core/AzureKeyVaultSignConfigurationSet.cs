namespace OpenVsixSignTool.Core
{
    using System;

    using Crypto = System.Security.Cryptography;

    public sealed class AzureKeyVaultSignConfigurationSet
    {
        public string AzureTenantId { get; set; }
        public string AzureClientId { get; set; }
        public string AzureClientSecret { get; set; }
        public Uri AzureKeyVaultUrl { get; set; }
        public string AzureKeyVaultCertificateName { get; set; }
        public string AzureAccessToken { get; set; }

        public Crypto.HashAlgorithmName FileDigestAlgorithm { get; set; }
        public Crypto.HashAlgorithmName PkcsDigestAlgorithm { get; set; }

        public bool Validate()
        {
            // Logging candidate.
            if (string.IsNullOrWhiteSpace(this.AzureAccessToken))
            {
                if (string.IsNullOrWhiteSpace(this.AzureClientId))
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(this.AzureClientSecret))
                {
                    return false;
                }
            }

            if (string.IsNullOrWhiteSpace(this.AzureKeyVaultCertificateName))
            {
                return false;
            }

            return true;
        }
    }
}
