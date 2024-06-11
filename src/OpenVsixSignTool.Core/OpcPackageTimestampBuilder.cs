﻿namespace OpenVsixSignTool.Core
{
    using OpenVsixSignTool.Core.Interop;

    using System;
    using System.Runtime.InteropServices;
    using System.Security.Cryptography;
    using System.Threading.Tasks;
    using System.Xml;
    using System.Xml.Linq;

    /// <summary>
    /// A builder for adding timestamps to a package.
    /// </summary>
    public class OpcPackageTimestampBuilder
    {
        private readonly OpcPart _part;

        internal OpcPackageTimestampBuilder(OpcPart part)
        {
            _part = part;
            this.Timeout = TimeSpan.FromSeconds(30);
        }

        /// <summary>
        /// Gets or sets the timeout for signing the package.
        /// The default is 30 earth seconds.
        /// </summary>
        public TimeSpan Timeout { get; set; }

        /// <summary>
        /// Signs the package with a timestamp.
        /// </summary>
        /// <param name="timestampServer">The URI of the timestamp server.</param>
        /// <param name="timestampAlgorithm">The hash algorithm to timestamp with.</param>
        /// <returns>A result of the timestamp operation.</returns>
        public Task<TimestampResult> SignAsync(Uri timestampServer, HashAlgorithmName timestampAlgorithm)
        {
            if (timestampServer == null)
            {
                throw new ArgumentNullException(nameof(timestampServer));
            }

            if (!timestampServer.IsAbsoluteUri)
            {
                throw new ArgumentException("The timestamp server must be an absolute URI.", nameof(timestampServer));
            }

            Oid oid = HashAlgorithmTranslator.TranslateFromNameToOid(timestampAlgorithm);
            using (var nonce = new TimestampNonceFactory())
            {
                var parameters = new CRYPT_TIMESTAMP_PARA();
                parameters.cExtension = 0;
                parameters.fRequestCerts = true;
                parameters.Nonce.cbData = nonce.Size;
                parameters.Nonce.pbData = nonce.Nonce;
                parameters.pszTSAPolicyId = null;
                (XDocument signatureDocument, byte[] timestampSubject) = GetSignatureToTimestamp(_part);
                var winResult = Crypt32.CryptRetrieveTimeStamp(
                    timestampServer.AbsoluteUri,
                    CryptRetrieveTimeStampRetrievalFlags.NONE,
                    (uint)this.Timeout.TotalMilliseconds,
                    oid.Value,
                    ref parameters,
                    timestampSubject,
                    (uint)timestampSubject.Length,
                    out CryptMemorySafeHandle context,
                    IntPtr.Zero,
                    IntPtr.Zero
                );
                if (!winResult)
                {
                    return Task.FromResult(TimestampResult.Failed);
                }

                using (context)
                {
                    var refSuccess = false;
                    try
                    {
                        context.DangerousAddRef(ref refSuccess);
                        if (!refSuccess)
                        {
                            return Task.FromResult(TimestampResult.Failed);
                        }

                        CRYPT_TIMESTAMP_CONTEXT structure = Marshal.PtrToStructure<CRYPT_TIMESTAMP_CONTEXT>(context.DangerousGetHandle());
                        var encoded = new byte[structure.cbEncoded];
                        Marshal.Copy(structure.pbEncoded, encoded, 0, encoded.Length);
                        ApplyTimestamp(signatureDocument, _part, encoded);
                        return Task.FromResult(TimestampResult.Success);
                    }
                    finally
                    {
                        if (refSuccess)
                        {
                            context.DangerousRelease();
                        }
                    }
                }
            }
        }

        private static (XDocument document, byte[] signature) GetSignatureToTimestamp(OpcPart signaturePart)
        {
            XNamespace xmlDSigNamespace = OpcKnownUris.XmlDSig.AbsoluteUri;
            using (System.IO.Stream signatureStream = signaturePart.Open())
            {
                var doc = XDocument.Load(signatureStream);
                var signature = doc.Element(xmlDSigNamespace + "Signature")?.Element(xmlDSigNamespace + "SignatureValue")?.Value?.Trim();
                return (doc, Convert.FromBase64String(signature));
            }
        }

        private static void ApplyTimestamp(XDocument originalSignatureDocument, OpcPart signaturePart, byte[] timestampSignature)
        {
            XNamespace xmlDSigNamespace = OpcKnownUris.XmlDSig.AbsoluteUri;
            XNamespace xmlSignatureNamespace = OpcKnownUris.XmlDigitalSignature.AbsoluteUri;
            var document = new XDocument(originalSignatureDocument);
            var signature = new XElement(xmlDSigNamespace + "Object",
                new XElement(xmlSignatureNamespace + "TimeStamp", new XAttribute("Id", "idSignatureTimestamp"),
                    new XElement(xmlSignatureNamespace + "Comment", ""),
                    new XElement(xmlSignatureNamespace + "EncodedTime", Convert.ToBase64String(timestampSignature))
                )
            );
            document.Element(xmlDSigNamespace + "Signature").Add(signature);
            using (System.IO.Stream copySignatureStream = signaturePart.Open())
            {
                using (var xmlWriter = new XmlTextWriter(copySignatureStream, System.Text.Encoding.UTF8))
                {
                    //The .NET implementation of OPC used by Visual Studio does not tollerate "white space" nodes.
                    xmlWriter.Formatting = Formatting.None;
                    document.Save(xmlWriter);
                }
            }
        }

        internal class TimestampNonceFactory : IDisposable
        {
            public TimestampNonceFactory(int nonceSize = 32)
            {
                Nonce = Marshal.AllocCoTaskMem(nonceSize);
                Size = checked((uint)nonceSize);
                var nonce = new byte[nonceSize];
                using (var rng = RandomNumberGenerator.Create())
                {
                    rng.GetBytes(nonce);
                }
                //The nonce is technically an integer. Some timestamp servers may not like a "negative" nonce. Clear the sign bit so it's positive.
                //That loses one bit of entropy, however is well within the security boundary of a properly sized nonce. Authenticode doesn't even use
                //a nonce.
                nonce[nonce.Length - 1] &= 0b01111111;
                Marshal.Copy(nonce, 0, Nonce, nonce.Length);
            }

            public IntPtr Nonce { get; }
            public uint Size { get; }

            public void Dispose()
            {
                Marshal.FreeCoTaskMem(Nonce);
            }
        }
    }
}
