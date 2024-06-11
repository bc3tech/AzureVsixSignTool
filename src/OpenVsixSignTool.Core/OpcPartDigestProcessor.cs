namespace OpenVsixSignTool.Core
{
    using System;
    using System.Security.Cryptography;

    internal static class OpcPartDigestProcessor
    {
        public static (byte[] digest, Uri identifier) Digest(OpcPart part, HashAlgorithmName algorithmName)
        {
            using (HashAlgorithm hashAlgorithm = HashAlgorithmTranslator.TranslateFromNameToxmlDSigUri(algorithmName, out Uri identifier))
            {
                using (System.IO.Stream partStream = part.Open())
                {
                    var digest = hashAlgorithm.ComputeHash(partStream);
                    return (digest, identifier);
                }
            }
        }
    }
}
