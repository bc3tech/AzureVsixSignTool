namespace OpenVsixSignTool.Core
{
    using System;
    using System.Security.Cryptography;

    internal interface ICertificateSign : IDisposable
    {
        byte[] SignDigest(byte[] digest, HashAlgorithmName pkcsAlgorithm);
        bool VerifyDigest(byte[] digest, byte[] signature, HashAlgorithmName pkcsAlgorithm);
    }
}