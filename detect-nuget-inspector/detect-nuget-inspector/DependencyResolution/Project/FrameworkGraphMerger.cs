using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Blackduck.Detect.Nuget.Inspector.Model;
using NuGet.Frameworks;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project
{
    internal static class FrameworkGraphMerger
    {
        // One tree per TFM: NugetTreeResolver never re-expands a package it has seen, so a shared tree drops later TFMs' children.
        internal static DependencyResult Merge(NugetSearchService service, ISet<string>? failedPackageIdentities, Dictionary<NuGetFramework, List<NugetDependency>> referencesByFramework)
        {
            var builder = new PackageSetBuilder();
            var result = new DependencyResult();
            foreach (var group in referencesByFramework)
            {
                var tree = new NugetTreeResolver(service, failedPackageIdentities);
                foreach (NugetDependency reference in group.Value)
                {
                    tree.Add(new NugetDependency(reference.Name, reference.VersionRange, group.Key));

                    var frameworkBuilder = new PackageSetBuilder();
                    foreach (PackageSet package in tree.GetPackageList())
                    {
                        frameworkBuilder.AddOrUpdatePackage(package.PackageId, package.Dependencies);
                    }
                    string? version = frameworkBuilder.GetBestVersion(reference.Name, reference.VersionRange);
                    // A metadata failure retains the assumed minimum even when an exclusive range excludes it.
                    if (version == null && reference.VersionRange.MinVersion != null)
                    {
                        string assumedVersion = reference.VersionRange.MinVersion.ToNormalizedString();
                        if (frameworkBuilder.DoesPackageExist(new PackageId(reference.Name, assumedVersion)))
                        {
                            version = assumedVersion;
                        }
                    }
                    // A declared reference remains direct even when another package also depends on it.
                    if (version != null)
                    {
                        result.Dependencies.Add(new PackageId(reference.Name, version));
                    }
                }

                // Union framework-specific edges so shared packages retain dependencies from every target.
                foreach (PackageSet package in tree.GetPackageList())
                {
                    builder.AddOrUpdatePackage(package.PackageId, package.Dependencies);
                }
            }
            result.Packages = builder.GetPackageList();
            return result;
        }
    }
}
