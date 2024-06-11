namespace OpenVsixSignTool.Core
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Linq;
    using System.Xml.Linq;

    /// <summary>
    /// Represents a part inside of a package.
    /// </summary>
    public class OpcPart : IEquatable<OpcPart>
    {
        internal OpcRelationships _relationships;
        private readonly OpcPackageFileMode _mode;
        private readonly string _path;

        internal OpcPart(OpcPackage package, string path, ZipArchiveEntry entry, OpcPackageFileMode mode)
        {
            this.Uri = new Uri(OpcPackage.BasePackageUri, path);
            Package = package;
            _path = path;
            Entry = entry;
            _mode = mode;
        }

        internal OpcPackage Package { get; }

        internal ZipArchiveEntry Entry { get; }

        public Uri Uri { get; }

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

        public string ContentType
        {
            get
            {
                var extension = Path.GetExtension(_path)?.TrimStart('.');
                return Package.ContentTypes.FirstOrDefault(ct => string.Equals(ct.Extension, extension, StringComparison.OrdinalIgnoreCase))?.ContentType ?? OpcKnownMimeTypes.OctetString;
            }
        }

        private string GetRelationshipFilePath()
        {
            return Path.Combine(Path.GetDirectoryName(_path), "_rels/" + Path.GetFileName(_path) + ".rels").Replace('\\', '/');
        }

        private OpcRelationships ConstructRelationships()
        {
            var path = GetRelationshipFilePath();
            ZipArchiveEntry entry = Package._archive.GetEntry(path);
            var readOnlyMode = _mode != OpcPackageFileMode.ReadWrite;
            var location = new Uri(OpcPackage.BasePackageUri, path);
            if (entry == null)
            {
                return new OpcRelationships(location, readOnlyMode);
            }
            else
            {
                using (Stream stream = entry.Open())
                {
                    return new OpcRelationships(location, XDocument.Load(stream, LoadOptions.PreserveWhitespace), readOnlyMode);
                }
            }
        }

        public Stream Open() => Entry.Open();

        public bool Equals(OpcPart other) => !(other is null) && this.Uri.Equals(other.Uri);

        public override bool Equals(object obj)
        {
            if (obj is OpcPart part)
            {
                return Equals(part);
            }

            return false;
        }

        public override int GetHashCode() => this.Uri.GetHashCode();
    }
}