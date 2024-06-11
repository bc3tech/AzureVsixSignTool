namespace OpenVsixSignTool.Core
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    public sealed class CertificateSignConfigurationSet
    {
        public X509Certificate2 SigningCertificate { get; set; }
        public HashAlgorithmName FileDigestAlgorithm { get; set; }
        public HashAlgorithmName PkcsDigestAlgorithm { get; set; }

        public bool Validate()
        {
            // Logging candidate.
            if (this.SigningCertificate?.HasPrivateKey != true)
            {
                return false;
            }

            return true;
        }
    }
}
