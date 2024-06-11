namespace OpenVsixSignTool.Core
{
    using System;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using System.Security.Cryptography.Xml;
    using System.Threading.Tasks;
    using System.Xml;

    internal class XmlSignatureBuilder
    {
        private readonly XmlDocument _document;
        private readonly ISigningContext _signingContext;
        private readonly XmlElement _signatureElement;
        private XmlElement _objectElement;

        /// <summary>
        /// Creates a new signature with the correct namespace and empty root <c>Signature</c> element.
        /// </summary>
        internal XmlSignatureBuilder(ISigningContext signingContext)
        {
            _signingContext = signingContext;
            _document = new XmlDocument();
            var manager = new XmlNamespaceManager(_document.NameTable);
            manager.AddNamespace("", OpcKnownUris.XmlDSig.AbsoluteUri);
            _signatureElement = CreateDSigElement("Signature");
        }

        private XmlElement CreateDSigElement(string name) => _document.CreateElement(name, OpcKnownUris.XmlDSig.AbsoluteUri);

        public async Task<XmlDocument> BuildAsync()
        {
            if (_objectElement == null)
            {
                throw new InvalidOperationException("A manifest has not been set on the builder.");
            }

            XmlElement keyInfoElement, signedInfo, signatureValue;
            using (System.Security.Cryptography.HashAlgorithm canonicalHashAlgorithm = HashAlgorithmTranslator.TranslateFromNameToxmlDSigUri(_signingContext.FileDigestAlgorithmName, out Uri canonicalHashAlgorithmIdentifier))
            {
                byte[] objectElementHash;
                string canonicalizationMethodObjectId;
                using (Stream objectElementCanonicalData = CanonicalizeElement(_objectElement, out canonicalizationMethodObjectId))
                {
                    objectElementHash = canonicalHashAlgorithm.ComputeHash(objectElementCanonicalData);
                }

                keyInfoElement = BuildKeyInfoElement();
                Stream signerInfoCanonicalStream;
                (signerInfoCanonicalStream, signedInfo) = BuildSignedInfoElement(
                    (_objectElement, objectElementHash, canonicalHashAlgorithmIdentifier.AbsoluteUri, canonicalizationMethodObjectId)
                );
                byte[] signerInfoElementHash;
                using (signerInfoCanonicalStream)
                {
                    signerInfoElementHash = canonicalHashAlgorithm.ComputeHash(signerInfoCanonicalStream);
                }

                signatureValue = await BuildSignatureValueAsync(signerInfoElementHash);
            }

            _signatureElement.AppendChild(signedInfo);
            _signatureElement.AppendChild(signatureValue);
            _signatureElement.AppendChild(keyInfoElement);
            _signatureElement.AppendChild(_objectElement);
            _document.AppendChild(_signatureElement);
            return _document;
        }

        private async Task<XmlElement> BuildSignatureValueAsync(byte[] signerInfoElementHash)
        {
            XmlElement signatureValueElement = CreateDSigElement("SignatureValue");
            signatureValueElement.InnerText = Convert.ToBase64String(await _signingContext.SignDigestAsync(signerInfoElementHash));
            return signatureValueElement;
        }

        private Stream CanonicalizeElement(XmlElement element, out string canonicalizationMethodUri, Action<string> setCanonicalization = null)
        {
            //The canonicalization transformer can't reasonable do just an element. It
            //seems content to do an entire XmlDocument.
            var transformer = new XmlDsigC14NTransform(false);
            setCanonicalization?.Invoke(transformer.Algorithm);

            var newDocument = new XmlDocument(_document.NameTable);
            newDocument.LoadXml(element.OuterXml);
            
            transformer.LoadInput(newDocument);

            var result = transformer.GetOutput(typeof(Stream));
            canonicalizationMethodUri = transformer.Algorithm;
            if (result is Stream s)
            {
                return s;
            }

            throw new NotSupportedException("Unable to canonicalize element.");
        }

        private (Stream, XmlElement) BuildSignedInfoElement(params (XmlElement element, byte[] canonicalDigest, string digestAlgorithm, string canonicalizationMethod)[] objects)
        {
            Uri signingIdentifier = _signingContext.XmlDSigIdentifier;

            XmlElement signedInfoElement = CreateDSigElement("SignedInfo");
            XmlElement canonicalizationMethodElement = CreateDSigElement("CanonicalizationMethod");
            XmlAttribute canonicalizationMethodAlgorithmAttribute = _document.CreateAttribute("Algorithm");
            canonicalizationMethodElement.Attributes.Append(canonicalizationMethodAlgorithmAttribute);

            XmlElement signatureMethodElement = CreateDSigElement("SignatureMethod");
            XmlAttribute signatureMethodAlgorithmAttribute = _document.CreateAttribute("Algorithm");
            signatureMethodAlgorithmAttribute.Value = signingIdentifier.AbsoluteUri;
            signatureMethodElement.Attributes.Append(signatureMethodAlgorithmAttribute);

            signedInfoElement.AppendChild(canonicalizationMethodElement);
            signedInfoElement.AppendChild(signatureMethodElement);

            foreach((XmlElement element, byte[] digest, string digestAlgorithm, string method) in objects)
            {
                var idFromElement = element.GetAttribute("Id");
                var reference = "#" + idFromElement;

                XmlElement referenceElement = CreateDSigElement("Reference");
                XmlAttribute referenceUriAttribute = _document.CreateAttribute("URI");
                XmlAttribute referenceTypeAttribute = _document.CreateAttribute("Type");
                referenceUriAttribute.Value = reference;
                referenceTypeAttribute.Value = OpcKnownUris.XmlDSigObject.AbsoluteUri;

                referenceElement.Attributes.Append(referenceUriAttribute);
                referenceElement.Attributes.Append(referenceTypeAttribute);

                XmlElement referencesTransformsElement = CreateDSigElement("Transforms");
                XmlElement transformElement = CreateDSigElement("Transform");
                XmlAttribute transformAlgorithmAttribute = _document.CreateAttribute("Algorithm");
                transformAlgorithmAttribute.Value = method;
                transformElement.Attributes.Append(transformAlgorithmAttribute);
                referencesTransformsElement.AppendChild(transformElement);
                referenceElement.AppendChild(referencesTransformsElement);

                XmlElement digestMethodElement = CreateDSigElement("DigestMethod");
                XmlAttribute digestMethodAlgorithmAttribute = _document.CreateAttribute("Algorithm");
                digestMethodAlgorithmAttribute.Value = digestAlgorithm;
                digestMethodElement.Attributes.Append(digestMethodAlgorithmAttribute);
                referenceElement.AppendChild(digestMethodElement);

                XmlElement digestValueElement = CreateDSigElement("DigestValue");
                digestValueElement.InnerText = Convert.ToBase64String(digest);
                referenceElement.AppendChild(digestValueElement);

                signedInfoElement.AppendChild(referenceElement);
            }

            Stream canonicalSignerInfo = CanonicalizeElement(signedInfoElement, out _, c => canonicalizationMethodAlgorithmAttribute.Value = c);
            return (canonicalSignerInfo, signedInfoElement);
        }

        private XmlElement BuildKeyInfoElement()
        {
            var publicCertificate = Convert.ToBase64String(_signingContext.Certificate.Export(X509ContentType.Cert));
            XmlElement keyInfoElement = CreateDSigElement("KeyInfo");
            XmlElement x509DataElement = CreateDSigElement("X509Data");
            XmlElement x509CertificateElement = CreateDSigElement("X509Certificate");
            x509CertificateElement.InnerText = publicCertificate;
            x509DataElement.AppendChild(x509CertificateElement);
            keyInfoElement.AppendChild(x509DataElement);
            return keyInfoElement;
        }

        public void SetFileManifest(OpcSignatureManifest manifest)
        {
            XmlElement objectElement = CreateDSigElement("Object");
            XmlAttribute objectElementId = _document.CreateAttribute("Id");
            objectElementId.Value = "idPackageObject";
            objectElement.Attributes.Append(objectElementId);

            XmlElement manifestElement = CreateDSigElement("Manifest");

            foreach (OpcPartDigest file in manifest.Manifest)
            {
                XmlElement referenceElement = CreateDSigElement("Reference");
                XmlAttribute referenceElementUriAttribute = _document.CreateAttribute("URI");
                referenceElementUriAttribute.Value = file.ReferenceUri.ToQualifiedPath();
                referenceElement.Attributes.Append(referenceElementUriAttribute);

                XmlElement digestMethod = CreateDSigElement("DigestMethod");
                XmlAttribute digestMethodAlgorithmAttribute = _document.CreateAttribute("Algorithm");
                digestMethodAlgorithmAttribute.Value = file.DigestAlgorithmIdentifier.AbsoluteUri;
                digestMethod.Attributes.Append(digestMethodAlgorithmAttribute);
                referenceElement.AppendChild(digestMethod);

                XmlElement digestValue = CreateDSigElement("DigestValue");
                digestValue.InnerText = System.Convert.ToBase64String(file.Digest);
                referenceElement.AppendChild(digestValue);

                manifestElement.AppendChild(referenceElement);
                objectElement.AppendChild(manifestElement);
            }

            XmlElement signaturePropertiesElement = CreateDSigElement("SignatureProperties");
            XmlElement signaturePropertyElement = CreateDSigElement("SignatureProperty");
            XmlAttribute signaturePropertyIdAttribute = _document.CreateAttribute("Id");
            XmlAttribute signaturePropertyTargetAttribute = _document.CreateAttribute("Target");
            signaturePropertyIdAttribute.Value = "idSignatureTime";
            signaturePropertyTargetAttribute.Value = "";

            signaturePropertyElement.Attributes.Append(signaturePropertyIdAttribute);
            signaturePropertyElement.Attributes.Append(signaturePropertyTargetAttribute);

            XmlElement signatureTimeElement = _document.CreateElement("SignatureTime", OpcKnownUris.XmlDigitalSignature.AbsoluteUri);
            XmlElement signatureTimeFormatElement = _document.CreateElement("Format", OpcKnownUris.XmlDigitalSignature.AbsoluteUri);
            XmlElement signatureTimeValueElement = _document.CreateElement("Value", OpcKnownUris.XmlDigitalSignature.AbsoluteUri);
            signatureTimeFormatElement.InnerText = "YYYY-MM-DDThh:mm:ss.sTZD";
            signatureTimeValueElement.InnerText = _signingContext.ContextCreationTime.ToString("yyyy-MM-ddTHH:mm:ss.fzzz");

            signatureTimeElement.AppendChild(signatureTimeFormatElement);
            signatureTimeElement.AppendChild(signatureTimeValueElement);

            signaturePropertyElement.AppendChild(signatureTimeElement);
            signaturePropertiesElement.AppendChild(signaturePropertyElement);
            objectElement.AppendChild(signaturePropertiesElement);

            _objectElement = objectElement;
        }
    }
}
