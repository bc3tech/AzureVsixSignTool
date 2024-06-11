﻿namespace OpenVsixSignTool.Core
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Xml.Linq;

    /// <summary>
    /// Allow manipulating and signing an OPC package, such as a VSIX.
    /// </summary>
    public class OpcPackage : IDisposable
    {
        internal static readonly Uri BasePackageUri = new Uri("package:///", UriKind.Absolute);
        private const string CONTENT_TYPES_XML = "[Content_Types].xml";
        private const string GLOBAL_RELATIONSHIPS = "_rels/.rels";

        internal readonly ZipArchive _archive;
        private readonly OpcPackageFileMode _mode;
        private bool _disposed;
        private OpcContentTypes _contentTypes;
        private OpcRelationships _relationships;
        private readonly Dictionary<string, OpcPart> _partTracker;

        /// <summary>
        /// Opens an OPC package.
        /// </summary>
        /// <param name="path">The path to the OPC package.</param>
        /// <param name="mode">The mode of the OPC package. Read allows the package to be inspected, but not changed.</param>
        /// <returns>An instance of an <see cref="OpcPackage"/>.</returns>
        public static OpcPackage Open(string path, OpcPackageFileMode mode = OpcPackageFileMode.Read)
        {
            ZipArchiveMode zipMode = GetZipModeFromOpcPackageMode(mode);
            ZipArchive zip = ZipFile.Open(path, zipMode);
            return new OpcPackage(zip, mode);
        }

        private OpcPackage(ZipArchive archive, OpcPackageFileMode mode)
        {
            _disposed = false;
            _archive = archive;
            _mode = mode;
            _partTracker = new Dictionary<string, OpcPart>();
        }

        /// <summary>
        /// A collection of content types that are known and registered within the package.
        /// </summary>
        public OpcContentTypes ContentTypes
        {
            get
            {
                if (_contentTypes == null)
                {
                    _contentTypes = ConstructContentTypes();
                }

                return _contentTypes;
            }
        }

        /// <summary>
        /// Closes the OPC package, and finishes all pending write operations.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                Flush();
                _archive.Dispose();
            }
        }

        /// <summary>
        /// Gets all package-wide parts.
        /// </summary>
        /// <returns>An enumerable source of parts.</returns>
        public IEnumerable<OpcPart> GetParts()
        {
            foreach (ZipArchiveEntry entry in _archive.Entries)
            {
                if (entry.FullName.Equals(CONTENT_TYPES_XML, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                OpcPart part;
                if (!_partTracker.TryGetValue(entry.FullName, out part))
                {
                    part = new OpcPart(this, entry.FullName, entry, _mode);
                    _partTracker.Add(entry.FullName, part);
                }

                yield return part;
            }
        }

        /// <summary>
        /// Gets a part by URI.
        /// </summary>
        /// <param name="partUri">A relative URI to the part.</param>
        /// <returns>An instance of <see cref="OpcPart"/>, or null if the part cannot be found.</returns>
        public OpcPart GetPart(Uri partUri)
        {
            var path = partUri.ToPackagePath();

            OpcPart part;
            if (!_partTracker.TryGetValue(path, out part))
            {
                ZipArchiveEntry entry = _archive.GetEntry(path);
                if (entry == null)
                {
                    return null;
                }

                part = new OpcPart(this, entry.FullName, entry, _mode);
                _partTracker.Add(path, part);
            }

            return part;
        }

        /// <summary>
        /// Creates a new part.
        /// </summary>
        /// <param name="partUri">A relative URI where the part will exist in the package.</param>
        /// <param name="mimeType">The Content Type of the part. If the content type is not registered in the <see cref="ContentTypes"/>, it will automatically be added.</param>
        /// <returns>An instance of the part just created.</returns>
        public OpcPart CreatePart(Uri partUri, string mimeType)
        {
            var path = partUri.ToPackagePath();

            if (_archive.GetEntry(path) != null)
            {
                throw new InvalidOperationException("The part already exists.");
            }

            var extension = Path.GetExtension(path)?.TrimStart('.');
            if (!this.ContentTypes.Any(ct => string.Equals(extension, ct.Extension, StringComparison.OrdinalIgnoreCase)))
            {
                this.ContentTypes.Add(new OpcContentType(extension, mimeType.ToLower(), OpcContentTypeMode.Default));
            }

            ZipArchiveEntry zipEntry = _archive.CreateEntry(path, CompressionLevel.NoCompression);
            var part = new OpcPart(this, zipEntry.FullName, zipEntry, _mode);
            _partTracker.Add(zipEntry.FullName, part);
            return part;
        }

        /// <summary>
        /// Checks if a part already exists in the package.
        /// </summary>
        /// <param name="partUri">A relative URI of the part.</param>
        /// <returns>True if the part exists, otherwise false.</returns>
        public bool HasPart(Uri partUri)
        {
            var path = partUri.ToPackagePath();
            return _archive.GetEntry(path) != null;
        }

        /// <summary>
        /// Removes an existing part from the package, and its relationships.
        /// </summary>
        /// <param name="part">The part to remove from the package. Use <see cref="GetPart(Uri)"/> to obtain the part to remove.</param>
        /// <remarks>This does not validate or clean up other references to this part.</remarks>
        public void RemovePart(OpcPart part)
        {
            Uri relationshipUri = part.Relationships.DocumentUri;
            var relationshipPath = relationshipUri.ToPackagePath();
            ZipArchiveEntry relationshipEntry = _archive.GetEntry(relationshipPath);
            relationshipEntry?.Delete();
            _partTracker.Remove(relationshipPath);

            var path = part.Uri.ToPackagePath();
            part.Entry.Delete();
            _partTracker.Remove(path);
        }

        /// <summary>
        /// Gets all package-wide relationships.
        /// </summary>
        /// <returns>A source of relationships.</returns>
        public OpcRelationships Relationships
        {
            get
            {
                if (_relationships == null)
                {
                    _relationships = ConstructRelationships();
                }

                return _relationships;
            }
        }

        /// <summary>
        /// Flushes all changes of the package to disk. This automatically occurs when the <see cref="OpcPackage"/> is disposed.
        /// </summary>
        public void Flush()
        {
            foreach (OpcPart part in _partTracker.Values)
            {
                if (part._relationships?.IsDirty == true)
                {
                    SaveRelationships(part._relationships);
                    part._relationships.IsDirty = false;
                }
            }

            if (_relationships?.IsDirty == true)
            {
                SaveRelationships(_relationships);
                _relationships.IsDirty = false;
            }

            if (_contentTypes?.IsDirty == true)
            {
                ZipArchiveEntry entry = _archive.GetEntry(CONTENT_TYPES_XML) ?? _archive.CreateEntry(CONTENT_TYPES_XML);
                using (Stream stream = entry.Open())
                {
                    XDocument newXml = _contentTypes.ToXml();
                    stream.SetLength(0L);
                    newXml.Save(stream, SaveOptions.None);
                    _contentTypes.IsDirty = false;
                }
            }
        }

        private void SaveRelationships(OpcRelationships relationships)
        {
            if (!this.ContentTypes.Any(ct => ct.Extension.Equals("rels", StringComparison.OrdinalIgnoreCase)))
            {
                this.ContentTypes.Add(new OpcContentType("rels", OpcKnownMimeTypes.OpenXmlRelationship, OpcContentTypeMode.Default));
            }

            var path = relationships.DocumentUri.ToPackagePath();
            ZipArchiveEntry entry = _archive.GetEntry(path) ?? _archive.CreateEntry(path);
            using (Stream stream = entry.Open())
            {
                stream.SetLength(0L);
                XDocument newXml = relationships.ToXml();
                newXml.Save(stream, SaveOptions.None);
            }
        }

        /// <summary>
        /// Creates a signature builder for applying a digital signature to the package.
        /// </summary>
        /// <returns>A builder instance for configuring and applying a signature.</returns>
        public OpcPackageSignatureBuilder CreateSignatureBuilder() => new OpcPackageSignatureBuilder(this);

        /// <summary>
        /// Enumerates over all of the signatures in the package.
        /// </summary>
        /// <returns>An enumerable collection of signatures in the package.</returns>
        public IEnumerable<OpcSignature> GetSignatures()
        {
            OpcRelationship originFileRelationship = this.Relationships.FirstOrDefault(r => r.Type.Equals(OpcKnownUris.DigitalSignatureOrigin));
            if (originFileRelationship == null)
            {
                yield break;
            }

            OpcPart originPart = GetPart(originFileRelationship.Target);
            if (originPart == null)
            {
                yield break;
            }

            var signatureRelationships = originPart.Relationships.Where(r => r.Type.Equals(OpcKnownUris.DigitalSignatureSignature)).ToList();
            foreach (OpcRelationship signatureRelationship in signatureRelationships)
            {
                OpcPart signaturePart = GetPart(signatureRelationship.Target);
                if (signaturePart == null)
                {
                    continue;
                }

                yield return new OpcSignature(signaturePart);
            }
        }

        private OpcContentTypes ConstructContentTypes()
        {
            ZipArchiveEntry entry = _archive.GetEntry(CONTENT_TYPES_XML);
            var readOnlyMode = _mode != OpcPackageFileMode.ReadWrite;
            if (entry == null)
            {
                return new OpcContentTypes(readOnlyMode);
            }
            else
            {
                using (Stream stream = entry.Open())
                {
                    return new OpcContentTypes(XDocument.Load(stream, LoadOptions.PreserveWhitespace), readOnlyMode);
                }
            }
        }

        private OpcRelationships ConstructRelationships()
        {
            ZipArchiveEntry entry = _archive.GetEntry(GLOBAL_RELATIONSHIPS);
            var readOnlyMode = _mode != OpcPackageFileMode.ReadWrite;
            if (entry == null)
            {
                var location = new Uri(BasePackageUri, GLOBAL_RELATIONSHIPS);
                return new OpcRelationships(location, readOnlyMode);
            }
            else
            {
                var location = new Uri(BasePackageUri, entry.FullName);
                using (Stream stream = entry.Open())
                {
                    return new OpcRelationships(location, XDocument.Load(stream, LoadOptions.PreserveWhitespace), readOnlyMode);
                }
            }
        }

        private static ZipArchiveMode GetZipModeFromOpcPackageMode(OpcPackageFileMode mode)
        {
            switch (mode)
            {
                case OpcPackageFileMode.Read:
                    return ZipArchiveMode.Read;
                case OpcPackageFileMode.ReadWrite:
                    return ZipArchiveMode.Update;
                default:
                    throw new ArgumentException($"Specified {nameof(OpcPackageFileMode)} is invalid.", nameof(mode));
            }
        }
    }
}
