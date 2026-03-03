using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blackduck.Detect.Nuget.Inspector.Inspection.Util;
using Blackduck.Detect.Nuget.Inspector.Model;
using NuGet.LibraryModel;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget
{
    public class NugetLockFileResolver
    {
        private NuGet.ProjectModel.LockFile LockFile;
        private string ExcludedDependencyTypes;
        private HashSet<string> ExcludedDependencies = new HashSet<string>();

        public NugetLockFileResolver(NuGet.ProjectModel.LockFile lockFile, string excludedDependencyTypes)
        {
            LockFile = lockFile;
            ExcludedDependencyTypes = excludedDependencyTypes;
        }

        private NuGet.Versioning.NuGetVersion BestVersion(string name, NuGet.Versioning.VersionRange range, IList<NuGet.ProjectModel.LockFileTargetLibrary> libraries)
        {
            var versions = libraries.Where(lib => lib.Name == name).Select(lib => lib.Version);
            var bestMatch = range.FindBestMatch(versions);
            if (bestMatch == null)
            {
                if (versions.Count() == 1)
                {
                    return versions.First();
                }
                else
                {
                    Console.WriteLine($"WARNING: Unable to find a version to satisfy range {range.PrettyPrint()} for the dependency " + name);
                    Console.WriteLine($"Instead will return the minimum range demanded: " + range.MinVersion.ToFullString());
                    return range.MinVersion;
                }
            }
            else
            {
                return bestMatch;
            }
        }

        private NuGet.Versioning.NuGetVersion BestLibraryVersion(string name, NuGet.Versioning.VersionRange range, IList<NuGet.ProjectModel.LockFileLibrary> libraries)
        {
            var versions = libraries.Where(lib => lib.Name == name).Select(lib => lib.Version);
            var bestMatch = range.FindBestMatch(versions);
            if (bestMatch == null)
            {
                if (versions.Count() == 1)
                {
                    return versions.First();
                }
                else
                {
                    Console.WriteLine($"WARNING: Unable to find a version to satisfy range {range.PrettyPrint()} for the dependency " + name);
                    if (range.HasUpperBound && !range.HasLowerBound)
                    {
                        Console.WriteLine($"Instead will return the maximum range demanded: " + range.MaxVersion.ToFullString());
                        return range.MaxVersion;
                    }
                    else
                    {
                        Console.WriteLine($"Instead will return the minimum range demanded: " + range.MinVersion.ToFullString());
                        return range.MinVersion;
                    }

                }
            }
            else
            {
                return bestMatch;
            }
        }

        public DependencyResult Process()
        {
            var builder = new Model.PackageSetBuilder();
            var result = new DependencyResult();

            foreach (var target in LockFile.Targets)
            {
                foreach (var library in target.Libraries)
                {
                    string name = library.Name;
                    string version = library.Version.ToNormalizedString();
                    var packageId = new Model.PackageId(name, version);

                    HashSet<Model.PackageId> dependencies = new HashSet<Model.PackageId>();
                    foreach (var dep in library.Dependencies)
                    {
                        var id = dep.Id;
                        var vr = dep.VersionRange;
                        //vr.Float.FloatBehavior = NuGet.Versioning.NuGetVersionFloatBehavior.
                        var lb = target.Libraries;
                        var bs = BestVersion(id, vr, lb);
                        if (bs == null)
                        {
                            Console.WriteLine(dep.Id);
                            bs = BestVersion(id, vr, lb);
                        }
                        else
                        {
                            var depId = new Model.PackageId(id, bs.ToNormalizedString());
                            dependencies.Add(depId);
                        }

                    }

                    builder.AddOrUpdatePackage(packageId, dependencies);
                }

            }

            // Track dependencies by name to handle duplicates from multiple sources.
            // This is necessary because project-based packaging (v3.3.0+) can cause the same
            // dependency to appear in multiple sources (PackageSpec.Dependencies, ProjectFileDependencyGroups,
            // and TargetFrameworks) with different version information.
            // With .nuspec files, all sources agreed. With project-based packaging, they can disagree.
            // Strategy: Keep the entry with the best version information (prefer non-null versions).
            var addedDependencies = new Dictionary<string, Model.PackageId>();

            // Source 1: PackageSpec.Dependencies
            foreach (var dep in LockFile.PackageSpec.Dependencies)
            {
                var version = builder.GetBestVersion(dep.Name, dep.LibraryRange.VersionRange);
                var packageId = new Model.PackageId(dep.Name, version);
                result.Dependencies.Add(packageId);
                addedDependencies[dep.Name] = packageId;
            }

            // Source 2: ProjectFileDependencyGroups
            foreach (var projectFileDependencyGroup in LockFile.ProjectFileDependencyGroups)
            {
                foreach (var projectFileDependency in projectFileDependencyGroup.Dependencies)
                {
                    var projectDependencyParsed = ParseProjectFileDependencyGroup(projectFileDependency);
                    string depName = projectDependencyParsed.GetName();
                    var libraryVersion = BestLibraryVersion(depName, projectDependencyParsed.GetVersionRange(), LockFile.Libraries);
                    
                    if (addedDependencies.ContainsKey(depName))
                    {
                        // Already added from Source 1 - check if this source provides better version info
                        var existingPackage = addedDependencies[depName];

                        // Replace if the new source has a version and the existing one is null
                        if (existingPackage.Version == null && libraryVersion != null)
                        {
                            string newVersion = libraryVersion.ToNormalizedString();
                            result.Dependencies.RemoveWhere(p => p.Name == depName);
                            var betterPackageId = new Model.PackageId(depName, newVersion);
                            result.Dependencies.Add(betterPackageId);
                            addedDependencies[depName] = betterPackageId;
                        }
                        // Otherwise, skip - existing version is either non-null or both are null
                    }
                    else
                    {
                        // Not added yet - add it
                        String version = null;
                        if (libraryVersion != null)
                        {
                            version = libraryVersion.ToNormalizedString();
                        }
                        var packageId = new Model.PackageId(depName, version);
                        result.Dependencies.Add(packageId);
                        addedDependencies[depName] = packageId;
                    }
                }
            }


            // Source 3: TargetFrameworks
            foreach (var framework in LockFile.PackageSpec.TargetFrameworks)
            {
                foreach (var dep in framework.Dependencies)
                {
                    bool isDevDependencyTypeExcluded = ExcludedDependencyTypeUtil.isDependencyTypeExcluded(ExcludedDependencyTypes,"DEV");
                    bool excludeDevDependency = isDevDependencyTypeExcluded && (dep.SuppressParent == LibraryIncludeFlags.All);

                    if (!excludeDevDependency)
                    {
                        var newVersion = builder.GetBestVersion(dep.Name, dep.LibraryRange.VersionRange);
                        if (addedDependencies.ContainsKey(dep.Name))
                        {
                            // Already added from Source 1 or 2 - check if this source provides better version info
                            var existingPackage = addedDependencies[dep.Name];
                            // Replace if the new source has a version and the existing one is null
                            if (existingPackage.Version == null && newVersion != null)
                            {
                                result.Dependencies.RemoveWhere(p => p.Name == dep.Name);
                                var betterPackageId = new PackageId(dep.Name, newVersion);
                                result.Dependencies.Add(betterPackageId);
                                addedDependencies[dep.Name] = betterPackageId;
                            }
                            // Otherwise, skip - existing version is either non-null or both are null
                        }
                        else
                        {
                            // Not added yet - add it
                            var packageId = new PackageId(dep.Name, newVersion);
                            result.Dependencies.Add(packageId);
                            addedDependencies[dep.Name] = packageId;
                        }
                    }
                    else
                    {
                        ExcludedDependencies.Add(dep.Name);
                        result.Dependencies.RemoveWhere(package => package.Name.Equals(dep.Name));
                        addedDependencies.Remove(dep.Name);
                    }
                }
            }
            


            if (result.Dependencies.Count == 0)
            {
                Console.WriteLine("Found no dependencies for lock file: " + LockFile.Path);
            }

            result.Packages = builder.GetPackageList();
            ExcludeDevDependenciesFromPackages(result);
            return result;
        }

        private void ExcludeDevDependenciesFromPackages(DependencyResult result)
        {
            if (ExcludedDependencies.Count != 0)
            {
                result.Packages.RemoveWhere(package => ExcludedDependencies.Contains(package.PackageId.Name));
            }
        }


        public ProjectFileDependency ParseProjectFileDependencyGroup(String projectFileDependency)
        {
            Console.WriteLine("ProjectFileDependency: " + projectFileDependency);
            //Reverse engineered from: https://github.com/NuGet/NuGet.Client/blob/538727480d93b7d8474329f90ccb9ff3b3543714/src/NuGet.Core/NuGet.LibraryModel/LibraryRange.cs#L68
            //With some hints from https://github.com/dotnet/NuGet.BuildTasks/pull/23/files


            if (ParseProjectFileDependencyGroupTokens(projectFileDependency, " >= ", out String projectName, out String versionRaw))
            {
                if (ParseProjectFileDependencyGroupTokens(versionRaw, " <= ", out String versionMin, out String versionMax))
                {
                    var minVersion = NuGet.Versioning.NuGetVersion.Parse(versionMin);
                    var maxVersion = NuGet.Versioning.NuGetVersion.Parse(versionMax);

                    return new ProjectFileDependency(projectName, new NuGet.Versioning.VersionRange(minVersion, true, maxVersion, true));
                }
                else if (ParseProjectFileDependencyGroupTokens(versionRaw, " < ", out String versionMin2, out String versionMax2))
                {
                    var minVersion = NuGet.Versioning.NuGetVersion.Parse(versionMin2);
                    var maxVersion = NuGet.Versioning.NuGetVersion.Parse(versionMax2);
                    return new ProjectFileDependency(projectName, new NuGet.Versioning.VersionRange(minVersion, true, maxVersion, true));
                }
                else
                {
                    return new ProjectFileDependency(projectName, MinVersionOrFloat(versionRaw, true /* Include min version. */));
                }
            }
            else if (ParseProjectFileDependencyGroupTokens(projectFileDependency, " > ", out String projectName2, out String versionRaw2))
            {
                if (ParseProjectFileDependencyGroupTokens(versionRaw2, " <= ", out String versionMin, out String versionMax))
                {
                    var minVersion = NuGet.Versioning.NuGetVersion.Parse(versionMin);
                    var maxVersion = NuGet.Versioning.NuGetVersion.Parse(versionMax);

                    return new ProjectFileDependency(projectName2, new NuGet.Versioning.VersionRange(minVersion, true, maxVersion, true));
                }
                else if (ParseProjectFileDependencyGroupTokens(versionRaw2, " < ", out String versionMin2, out String versionMax2))
                {
                    var minVersion = NuGet.Versioning.NuGetVersion.Parse(versionMin2);
                    var maxVersion = NuGet.Versioning.NuGetVersion.Parse(versionMax2);
                    return new ProjectFileDependency(projectName2, new NuGet.Versioning.VersionRange(minVersion, true, maxVersion, true));
                }
                else
                {
                    return new ProjectFileDependency(projectName2, MinVersionOrFloat(versionRaw2, false /* Do not include min version. */));
                }
            }
            else if (ParseProjectFileDependencyGroupTokens(projectFileDependency, " <= ", out String projectName3, out String versionRaw3))
            {
                var maxVersion = NuGet.Versioning.NuGetVersion.Parse(versionRaw3);
                return new ProjectFileDependency(projectName3, new NuGet.Versioning.VersionRange(null, false, maxVersion, true /* Include Max */));
            }
            else if (ParseProjectFileDependencyGroupTokens(projectFileDependency, " < ", out String projectName4, out String versionRaw4))
            {
                var maxVersion = NuGet.Versioning.NuGetVersion.Parse(versionRaw4);
                return new ProjectFileDependency(projectName4, new NuGet.Versioning.VersionRange(null, false, maxVersion, false /* Do NOT Include Max */));
            }
            throw new Exception("Unable to parse project file dependency group, please contact support: " + projectFileDependency);
        }

        private bool ParseProjectFileDependencyGroupTokens(string input, string tokens, out String prefixString, out String projectVersion)
        {
            if (input.Contains(tokens))
            {
                String[] pieces = input.Split(tokens);
                // this is often the project name but with some ranges it is the minimum version.
                prefixString = pieces[0].Trim();
                projectVersion = pieces[1].Trim();
                return true;
            }
            else
            {
                prefixString = null;
                projectVersion = null;
                return false;
            }
        }

        private NuGet.Versioning.VersionRange MinVersionOrFloat(String versionValueRaw, bool includeMin)
        {
            //could be Floating or MinVersion
            if (NuGet.Versioning.NuGetVersion.TryParse(versionValueRaw, out NuGet.Versioning.NuGetVersion minVersion))
            {
                return new NuGet.Versioning.VersionRange(minVersion, includeMin);
            }
            else
            {  
                return NuGet.Versioning.VersionRange.Parse(versionValueRaw, true);
            }
        }

        public class ProjectFileDependency
        {
            private readonly String name;
            private readonly NuGet.Versioning.VersionRange versionRange;

            public ProjectFileDependency(string name, NuGet.Versioning.VersionRange versionRange)
            {
                this.name = name;
                this.versionRange = versionRange;
            }

            public String GetName()
            {
                return name;
            }

            public NuGet.Versioning.VersionRange GetVersionRange()
            {
                return versionRange;
            }
        }

    }

}
