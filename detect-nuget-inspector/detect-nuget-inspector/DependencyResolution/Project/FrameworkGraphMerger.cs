using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Blackduck.Detect.Nuget.Inspector.Model;
using NuGet.Frameworks;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project
{
    internal static class FrameworkGraphMerger
    {
        // One tree per TFM: NugetTreeResolver never re-expands a package it has seen, so a shared tree drops later TFMs' children.
        internal static PackageSetBuilder Merge(NugetSearchService service, ISet<string>? failedPackageIdentities, List<NugetDependency> directReferences, List<NuGetFramework> targetFrameworks)
        {
            var builder = new PackageSetBuilder();
            foreach (NuGetFramework framework in targetFrameworks)
            {
                var tree = new NugetTreeResolver(service, failedPackageIdentities);
                foreach (NugetDependency reference in directReferences)
                {
                    tree.Add(new NugetDependency(reference.Name, reference.VersionRange, framework));
                }

                // Union into one graph, matching the lock-file resolver's merged output across targets.
                foreach (PackageSet package in tree.GetPackageList())
                {
                    builder.AddOrUpdatePackage(package.PackageId, package.Dependencies);
                }
            }
            return builder;
        }

        // Merged TFMs can form a cycle (A->B on one TFM, B->A on another) that leaves direct references with no root.
        internal static void AddUnreachableDirectReferences(DependencyResult result, PackageSetBuilder builder, List<NugetDependency> directReferences)
        {
            var dependenciesById = result.Packages.ToDictionary(package => package.PackageId, package => package.Dependencies);
            var reachable = new HashSet<PackageId>();
            var pending = new Stack<PackageId>(result.Dependencies);
            while (pending.Count > 0)
            {
                PackageId id = pending.Pop();
                if (reachable.Add(id) && dependenciesById.TryGetValue(id, out var children))
                {
                    foreach (PackageId child in children)
                    {
                        pending.Push(child);
                    }
                }
            }

            foreach (NugetDependency reference in directReferences)
            {
                // Map to the version actually resolved; only unreachable ones are added, so acyclic output is unchanged.
                string? version = builder.GetBestVersion(reference.Name, reference.VersionRange);
                if (version != null && !reachable.Contains(new PackageId(reference.Name, version)))
                {
                    result.Dependencies.Add(new PackageId(reference.Name, version));
                }
            }
        }
    }
}
