namespace OpenVsixSignTool.Core
{
    using System;

    public static class UriHelpers
    {
        private static readonly Uri PackageBaseUri = new Uri("package:///", UriKind.Absolute);
        private static readonly Uri RootedPackageBaseUri = new Uri("package:", UriKind.Absolute);

        /// <summary>
        /// Converts a package URI to a path within the package zip file.
        /// </summary>
        /// <param name="partUri">The URI to convert.</param>
        /// <returns>A string to the path in a zip file.</returns>
        public static string ToPackagePath(this Uri partUri)
        {
            Uri absolute = partUri.IsAbsoluteUri ? partUri : new Uri(PackageBaseUri, partUri);
            var pathUri = new Uri(absolute.GetComponents(UriComponents.SchemeAndServer | UriComponents.Path, UriFormat.Unescaped), UriKind.Absolute);
            Uri resolved = PackageBaseUri.MakeRelativeUri(pathUri);
            return resolved.ToString();
        }

        public static string ToQualifiedPath(this Uri partUri)
        {
            Uri absolute = partUri.IsAbsoluteUri ? partUri : new Uri(RootedPackageBaseUri, partUri);
            var pathUri = new Uri(absolute.GetComponents(UriComponents.SchemeAndServer | UriComponents.PathAndQuery, UriFormat.Unescaped), UriKind.Absolute);
            Uri resolved = RootedPackageBaseUri.MakeRelativeUri(pathUri);
            return resolved.ToString();
        }

        public static Uri ToQualifiedUri(this Uri partUri)
        {
            Uri absolute = partUri.IsAbsoluteUri ? partUri : new Uri(RootedPackageBaseUri, partUri);
            var pathUri = new Uri(absolute.GetComponents(UriComponents.SchemeAndServer | UriComponents.PathAndQuery, UriFormat.Unescaped), UriKind.Absolute);
            return RootedPackageBaseUri.MakeRelativeUri(pathUri);
        }
    }
}
