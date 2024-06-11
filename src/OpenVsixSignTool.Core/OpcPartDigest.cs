namespace OpenVsixSignTool.Core
{
    using System;

    internal class OpcPartDigest
    {
        public Uri ReferenceUri { get; }
        public Uri DigestAlgorithmIdentifier { get; }
        public byte[] Digest { get; }

        public OpcPartDigest(Uri referenceUri, Uri digestAlgorithmIdentifer, byte[] digest)
        {
            this.ReferenceUri = referenceUri;
            this.DigestAlgorithmIdentifier = digestAlgorithmIdentifer;
            this.Digest = digest;
        }
    }
}
