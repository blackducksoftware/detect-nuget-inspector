﻿using Synopsys.Detect.Nuget.Inspector.Model;

namespace Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget
{
    public class NugetLockFileResolver
    {
        private NuGet.ProjectModel.LockFile LockFile;
        
        private static Dictionary<string, string> directDependenciesMap = new Dictionary<string, string>();


        public NugetLockFileResolver(NuGet.ProjectModel.LockFile lockFile)
        {
            LockFile = lockFile;
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
            var packageSetBuilder = new Model.PackageSetBuilder();
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
                        var lb = target.Libraries;
                        
                        var bs = BestVersion(id, vr, lb);
                       
                        if (bs != null)
                        {
                            var depId = new Model.PackageId(id, bs.ToNormalizedString());
                            dependencies.Add(depId);
                        }
                        else
                        {
                            Console.WriteLine($"WARNING: Unable to resolve a version for the dependency {id}");
                        }

                    }

                    packageSetBuilder.AddOrUpdatePackage(packageId, dependencies);
                }

            }



            if (LockFile.PackageSpec.Dependencies.Count != 0)
            {
                foreach (var dep in LockFile.PackageSpec.Dependencies)
                {
                    var version = packageSetBuilder.GetBestVersion(dep.Name, dep.LibraryRange.VersionRange);
                    saveDependency(dep.Name, version, result, packageSetBuilder);
                }
            }
            else
            {
                foreach (var framework in LockFile.PackageSpec.TargetFrameworks)
                {
                    foreach (var dep in framework.Dependencies)
                    {
                        var version = packageSetBuilder.GetBestVersion(dep.Name, dep.LibraryRange.VersionRange);
                        saveDependency(dep.Name, version, result, packageSetBuilder);
                    }
                }
            }

            foreach (var projectFileDependencyGroup in LockFile.ProjectFileDependencyGroups)
            {
                foreach (var projectFileDependency in projectFileDependencyGroup.Dependencies)
                {
                    var projectDependencyParsed = ParseProjectFileDependencyGroup(projectFileDependency);
                    var libraryVersion = BestLibraryVersion(projectDependencyParsed.GetName(),
                        projectDependencyParsed.GetVersionRange(), LockFile.Libraries);
                    String version = null;
                    if (libraryVersion != null)
                    {
                        version = libraryVersion.ToNormalizedString();
                    }
                    accumulateDirectDependenciesAccrossSubProjects(projectDependencyParsed.GetName(), version);
                    result.Dependencies.Add(new Model.PackageId(projectDependencyParsed.GetName(), version));
                }
            }


            if (result.Dependencies.Count == 0)
            {
                Console.WriteLine("Found no dependencies for lock file: " + LockFile.Path);
            }

            result.Packages = packageSetBuilder.GetPackageList();

            return  dismissTransitiveVersionConflictsOfDirectDependencies(result);
        }

        private DependencyResult dismissTransitiveVersionConflictsOfDirectDependencies(DependencyResult result)
        {
            // Remove conflicting transitive dependencies 
            result.Packages.RemoveAll(packageSet =>
            {
                return packageSet.PackageId != null
                       && directDependenciesMap.ContainsKey(packageSet.PackageId.Name.ToLower())
                       && !packageSet.PackageId.Version.Equals(directDependenciesMap[packageSet.PackageId.Name.ToLower()]);
            });
            
            // Remove dependencies of transitive dependencies
            result.Packages.ForEach(packageSet =>
            {
                packageSet.Dependencies.RemoveWhere(packageId =>
                {
                    return packageId != null
                           && directDependenciesMap.ContainsKey(packageId.Name.ToLower())
                           && !packageId.Version.Equals(directDependenciesMap[packageId.Name.ToLower()]);
                });
            });
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

        private void saveDependency(String dependencyName, String dependencyVersion, DependencyResult result, PackageSetBuilder packageSetBuilder)
        {             
            if (dependencyVersion != null)
            {
                accumulateDirectDependenciesAccrossSubProjects(dependencyName, dependencyVersion);
                result.Dependencies.Add(new Model.PackageId(dependencyName, dependencyVersion));
            }
            else
            {
                Console.WriteLine($"WARNING: Unable to resolve a version for the dependency {dependencyVersion}");
            }
        }

        private void accumulateDirectDependenciesAccrossSubProjects(String name, String version)
        {
            name = name.ToLower();
            if (!directDependenciesMap.ContainsKey(name))
            {
                directDependenciesMap.Add(name,version);
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