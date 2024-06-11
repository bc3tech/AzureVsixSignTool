﻿namespace OpenVsixSignTool.Core
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Security.Cryptography;

    /// <summary>
    /// Parses DER encoded OIDs.
    /// </summary>
    internal static class OidParser
    {
        private const byte MAGIC_OID_VALUE = 6;
        private const int MAGIC_OID_OFFSET = 0;
        private const int DATA_LENGTH_OFFSET = 1;
        private const int FIRST_OCTET_OFFSET = 2;
        private const int VLQ_DATA_OFFSET = 3;

        /// <summary>
        /// Reads a DER encoded OID.
        /// </summary>
        /// <param name="data">A TLV triplet of the OID. This is the tag, data length, and the VLQ data.</param>
        /// <returns>An instance of a <see cref="Oid"/>.</returns>
        public static Oid ReadFromBytes(byte[] data)
        {
            if (data == null || data.Length < FIRST_OCTET_OFFSET)
            {
                return null;
            }

            var magicValue = data[MAGIC_OID_OFFSET];
            if (magicValue != MAGIC_OID_VALUE)
            {
                return null;
            }

            var dataLength = data[DATA_LENGTH_OFFSET];
            if (data.Length - FIRST_OCTET_OFFSET != dataLength)
            {
                return null;
            }

            var firstValue = data[FIRST_OCTET_OFFSET] / 40L;
            var secondValue = data[FIRST_OCTET_OFFSET] % 40L;
            IEnumerable<byte> remainder = data.Skip(VLQ_DATA_OFFSET);
            try
            {
                return new Oid(string.Join(".", new[] { firstValue, secondValue }.Concat(ReadVlqData(remainder))));
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }

        private static IEnumerable<long> ReadVlqData(IEnumerable<byte> data)
        {
            var value = 0L;
            foreach (var item in data)
            {
                value <<= 7;
                if ((item & 0x80) == 0x80)
                {
                    value |= (byte)(item & ~0x80);
                }
                else
                {
                    yield return value | item;
                    value = 0;
                }
            }

            if (value != 0)
            {
                throw new InvalidOperationException();
            }
        }
    }
}