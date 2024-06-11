﻿namespace OpenVsixSignTool.Core
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Xml.Linq;

    public class OpcRelationship : IEquatable<OpcRelationship>
    {
        public Uri Target { get; }
        public string Id { get; internal set; }
        public Uri Type { get; }

        public OpcRelationship(Uri target, string id, Uri type)
        {
            this.Target = target;
            this.Id = id;
            this.Type = type;
        }

        public OpcRelationship(Uri target, Uri type)
        {
            this.Target = target;
            this.Type = type;
        }

        public bool Equals(OpcRelationship other)
        {
            return other is object && this.Target == other.Target && this.Type == other.Type && this.Id == other.Id;
        }

        public override bool Equals(object obj)
        {
            if (obj is OpcRelationship rel)
            {
                return Equals(rel);
            }

            return false;
        }

        public override int GetHashCode() => this.Target.GetHashCode() ^ this.Type.GetHashCode();
    }

    public class OpcRelationships : IList<OpcRelationship>
    {
        private static readonly XNamespace OpcRelationshipNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
        private readonly List<OpcRelationship> _relationships = new List<OpcRelationship>();

        internal OpcRelationships(Uri documentUri, XDocument document, bool isReadOnly)
        {
            this.IsReadOnly = isReadOnly;
            this.DocumentUri = documentUri;
            IEnumerable<XElement> relationships = document?.Root?.Elements(OpcRelationshipNamespace + "Relationship");
            if (relationships == null)
            {
                return;
            }

            foreach (XElement relationship in relationships)
            {
                var target = relationship.Attribute("Target")?.Value;
                var id = relationship.Attribute("Id")?.Value;
                var type = relationship.Attribute("Type")?.Value;
                if (type == null || id == null || target == null)
                {
                    continue;
                }

                _relationships.Add(new OpcRelationship(new Uri(target, UriKind.Relative), id,
                    new Uri(type, UriKind.RelativeOrAbsolute)));
            }
        }

        internal OpcRelationships(Uri documentUri, bool isReadOnly)
        {
            this.IsReadOnly = isReadOnly;
            this.DocumentUri = documentUri;
        }

        public XDocument ToXml()
        {
            var document = new XDocument();
            var root = new XElement(OpcRelationshipNamespace + "Relationships");
            foreach (OpcRelationship relationship in _relationships)
            {
                var element = new XElement(OpcRelationshipNamespace + "Relationship");
                element.SetAttributeValue("Target", relationship.Target.ToQualifiedPath());
                element.SetAttributeValue("Id", relationship.Id);
                element.SetAttributeValue("Type", relationship.Type);
                root.Add(element);
            }

            document.Add(root);
            return document;
        }

        public int Count => _relationships.Count;

        public bool IsReadOnly { get; }

        internal Uri DocumentUri { get; }

        public OpcRelationship this[int index]
        {
            get => _relationships[index];
            set
            {
                AssertNotReadOnly();
                this.IsDirty = true;
                AssignRelationshipId(value);
                _relationships[index] = value;
            }
        }

        internal bool IsDirty { get; set; }

        public int IndexOf(OpcRelationship item) => _relationships.IndexOf(item);

        public void Insert(int index, OpcRelationship item)
        {
            AssertNotReadOnly();
            this.IsDirty = true;
            AssignRelationshipId(item);
            _relationships.Insert(index, item);
        }

        public void RemoveAt(int index)
        {
            AssertNotReadOnly();
            this.IsDirty = true;
            _relationships.RemoveAt(index);
        }

        public void Add(OpcRelationship item)
        {
            AssertNotReadOnly();
            this.IsDirty = true;
            AssignRelationshipId(item);
            _relationships.Add(item);
        }

        public void Clear()
        {
            AssertNotReadOnly();
            this.IsDirty = true;
            _relationships.Clear();
        }

        public bool Contains(OpcRelationship item) => _relationships.Contains(item);

        public void CopyTo(OpcRelationship[] array, int arrayIndex) => _relationships.CopyTo(array, arrayIndex);

        public bool Remove(OpcRelationship item)
        {
            AssertNotReadOnly();
            return this.IsDirty = _relationships.Remove(item);
        }

        public IEnumerator<OpcRelationship> GetEnumerator() => _relationships.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private void AssertNotReadOnly()
        {
            if (this.IsReadOnly)
            {
                throw new InvalidOperationException("Cannot update relationships in a read only package. Please open the package in write mode.");
            }
        }

        private void AssignRelationshipId(OpcRelationship relationship)
        {
            if (!string.IsNullOrWhiteSpace(relationship.Id))
            {
                return;
            }

            using (var rng = RandomNumberGenerator.Create())
            {
                var data = new byte[4];
                while(true)
                {
                    rng.GetBytes(data);
                    var uintValue = BitConverter.ToUInt32(data, 0);
                    var id = "R" + uintValue.ToString("X8");
                    if (_relationships.Any(r => r.Id == id))
                    {
                        continue;
                    }

                    relationship.Id = id;
                    break;
                }
            }
        }
    }
}
