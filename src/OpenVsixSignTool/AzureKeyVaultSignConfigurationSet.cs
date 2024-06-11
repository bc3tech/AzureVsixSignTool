﻿
namespace OpenVsixSignTool
{
    public sealed class AzureKeyVaultSignConfigurationSet
    {
        public string AzureClientId { get; set; }
        public string AzureClientSecret { get; set; }
        public string AzureKeyVaultUrl { get; set; }
        public string AzureKeyVaultCertificateName { get; set; }
        public string AzureAccessToken { get; set; }
        public string AzureTenantId { get; set; }
    }
}
