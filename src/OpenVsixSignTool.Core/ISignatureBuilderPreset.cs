﻿namespace OpenVsixSignTool.Core
{
    using System.Collections.Generic;

    /// <summary>
    /// Defines an interface for package signing presets.
    /// </summary>
    public interface ISignatureBuilderPreset
    {
        /// <summary>
        /// Returns a collection of parts that should be enqueued for signing.
        /// </summary>
        /// <param name="package">A package to list the parts from.</param>
        /// <returns>A collection of parts.</returns>
        IEnumerable<OpcPart> GetPartsForSigning(OpcPackage package);
    }
}
