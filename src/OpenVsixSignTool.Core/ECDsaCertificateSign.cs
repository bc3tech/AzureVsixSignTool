namespace OpenVsixSignTool.Core
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    internal class ECDsaCertificateSign : ICertificateSign
    {
        public ECDsaCurve ECDsaCurve { get; }
        private readonly ECDsa _algorithm;

        public ECDsaCertificateSign(X509Certificate2 certificate)
        {
            Oid curveOid = OidParser.ReadFromBytes(certificate.PublicKey.EncodedParameters.RawData);
            switch (curveOid.Value)
            {
                case KnownOids.EccCurves.EcdsaP256:
                    this.ECDsaCurve = ECDsaCurve.p256;
                    break;
                case KnownOids.EccCurves.EcdsaP384:
                    this.ECDsaCurve = ECDsaCurve.p384;
                    break;
                case KnownOids.EccCurves.EcdsaP521:
                    this.ECDsaCurve = ECDsaCurve.p521;
                    break;
                default:
                    throw new NotSupportedException("The specified ECC curve is not supported.");
            }

            _algorithm = certificate.GetECDsaPrivateKey();
        }

        //ECDSA doesn't have the PKCS#1 / PSS hashing problem, so the hash is thrown away.
        public byte[] SignDigest(byte[] digest, HashAlgorithmName pkcsAlgorithm) => _algorithm.SignHash(digest);

        public bool VerifyDigest(byte[] digest, byte[] signature, HashAlgorithmName pkcsAlgorithm) => _algorithm.VerifyHash(digest, signature);

        public void Dispose()
        {
            _algorithm.Dispose();
        }
    }
}