﻿namespace OpenVsixSignTool.Core.Tests
{
    using Crypto = System.Security.Cryptography;
    using System.Threading.Tasks;
    using System.Security.Cryptography;
    using Xunit;
    using System;

    public class KeyVaultSigningContextTests
    {
        private readonly AzureKeyVaultSignConfigurationSet _configuration;

        public KeyVaultSigningContextTests()
        {
            TestAzureCredentials creds = TestAzureCredentials.Credentials;
            if (creds == null)
            {
                return;
            }

            _configuration = new AzureKeyVaultSignConfigurationSet
            {
                FileDigestAlgorithm = Crypto.HashAlgorithmName.SHA256,
                PkcsDigestAlgorithm = Crypto.HashAlgorithmName.SHA256,
                AzureClientId = creds.ClientId,
                AzureClientSecret = creds.ClientSecret,
                AzureKeyVaultUrl = Uri.TryCreate(creds.AzureKeyVaultUrl, UriKind.Absolute, out Uri u) ? u : throw new ArgumentException("Key Vault URL is not a valid URL"),
                AzureKeyVaultCertificateName = creds.AzureKeyVaultCertificateName
            };
        }

        [AzureFact]
        public async Task ShouldRoundTripASignature()
        {
            AzureKeyVaultMaterializedConfiguration materialized = await KeyVaultConfigurationDiscoverer.Materialize(_configuration);
            using (var context = new KeyVaultSigningContext(materialized))
            using (var sha256 = SHA256.Create())
            {
                byte[] data = new byte[] { 1, 2, 3 };
                byte[] digest = sha256.ComputeHash(data);
                var signature = await context.SignDigestAsync(digest);
                var result = await context.VerifyDigestAsync(digest, signature);
                Assert.True(result);
            }
        }

        [AzureFact]
        public async Task ShouldFailToVerifyBadSignature()
        {
            AzureKeyVaultMaterializedConfiguration materialized = await KeyVaultConfigurationDiscoverer.Materialize(_configuration);
            using (var context = new KeyVaultSigningContext(materialized))
            using (var sha256 = SHA256.Create())
            {
                byte[] data = new byte[] { 1, 2, 3 };
                byte[] digest = sha256.ComputeHash(data);
                var signature = await context.SignDigestAsync(digest);
                signature[0] = (byte)~signature[0]; //Flip some bits.
                var result = await context.VerifyDigestAsync(digest, signature);
                Assert.False(result);
            }
        }
    }
}
