using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Blackduck.Detect.Nuget.Inspector.Inspection.Util;
using Blackduck.Detect.Nuget.Inspector.Model;
using NuGet.Frameworks;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project
{
    class ProjectReferenceResolver : DependencyResolver
    {

        private string ProjectPath;
        private NugetSearchService NugetSearchService;
        private HashSet<PackageId> CentrallyManagedPackages;
        private bool CheckVersionOverride;
        private string ExcludedDependencyTypes;
        private ISet<string>? FailedPackageIdentities;
        
        public ProjectReferenceResolver(string projectPath, NugetSearchService nugetSearchService, String excludedDependencyTypes, ISet<string>? failedPackageIdentities = null)
        {
            ProjectPath = projectPath;
            NugetSearchService = nugetSearchService;
            ExcludedDependencyTypes = excludedDependencyTypes;
            FailedPackageIdentities = failedPackageIdentities;
        }
        
        public ProjectReferenceResolver(string projectPath, NugetSearchService nugetSearchService, String excludedDependencyTypes, HashSet<PackageId> centrallyManagedPackages, bool checkVersionOverride, ISet<string>? failedPackageIdentities = null): this(projectPath, nugetSearchService, excludedDependencyTypes, failedPackageIdentities)
        {
            CentrallyManagedPackages = centrallyManagedPackages;
            CheckVersionOverride = checkVersionOverride;
        }
        
        public DependencyResult Process()
        {
            try
            {
                Microsoft.Build.Evaluation.Project proj = new Microsoft.Build.Evaluation.Project(ProjectPath);
                // MSBuild evaluates $(...) properties here, unlike PXR's raw XML read.
                List<NuGetFramework> targetFrameworks = TargetFrameworkHelper.Parse(
                    proj.GetPropertyValue("TargetFramework"),
                    proj.GetPropertyValue("TargetFrameworks"));
                if (targetFrameworks.Count == 0)
                {
                    Console.WriteLine("Target framework could not be determined; using framework-independent dependency lookup.");
                }

                List<NugetDependency> deps = new List<NugetDependency>();
                // Keep condition-ignoring references so OS conditions never drop reported dependencies; omissions are worse than extras.
                foreach (ProjectItem reference in proj.GetItemsIgnoringCondition("PackageReference"))
                {
                    bool containsPkg = CentrallyManagedPackages != null && CentrallyManagedPackages.Any(pkg => pkg.Name.Equals(reference.EvaluatedInclude));
                    
                    var versionMetaData = reference.Metadata.Where(meta => meta.Name == "Version").FirstOrDefault();
                    var versionOverrideMetaData = reference.Metadata.Where(meta => meta.Name == "VersionOverride").FirstOrDefault();
                    var privateAssetsMetaData = reference.Metadata.Where(meta => meta.Name == "PrivateAssets").FirstOrDefault();

                    bool isDevDependencyTypeExcluded = ExcludedDependencyTypeUtil.isDependencyTypeExcluded(ExcludedDependencyTypes,"DEV");

                    bool excludeDevDependency = isDevDependencyTypeExcluded && privateAssetsMetaData != null;

                    if (!excludeDevDependency)
                    {
                        if (containsPkg)
                        {
                            PackageId pkg =
                                CentrallyManagedPackages.First(pkg => pkg.Name.Equals(reference.EvaluatedInclude));

                            if (CheckVersionOverride && versionOverrideMetaData != null)
                            {
                                addNugetDependency(reference.EvaluatedInclude, versionOverrideMetaData.EvaluatedValue,
                                    deps);
                            }
                            else if (!CheckVersionOverride && versionOverrideMetaData != null)
                            {
                                Console.WriteLine(
                                    "The Central Package Version Overriding is disabled, please enable version override or remove VersionOverride tags from project");
                            }
                            else
                            {
                                addNugetDependency(reference.EvaluatedInclude, pkg.Version, deps);
                            }
                        }
                        else if (versionMetaData != null)
                        {
                            addNugetDependency(reference.EvaluatedInclude, versionMetaData.EvaluatedValue, deps);
                        }
                        else
                        {
                            Console.WriteLine("Framework dependency had no version, will not be included: " +
                                              reference.EvaluatedInclude);
                        }
                    }
                }

                var packageReferences = deps.ToList();
                foreach (ProjectItem reference in proj.GetItemsIgnoringCondition("Reference"))
                {
                    if (reference.Xml != null && !String.IsNullOrWhiteSpace(reference.Xml.Include) && reference.Xml.Include.Contains("Version="))
                    {

                        string packageInfo = reference.Xml.Include;

                        var artifact = packageInfo.Substring(0, packageInfo.IndexOf(","));

                        string versionKey = "Version=";
                        int versionKeyIndex = packageInfo.IndexOf(versionKey);
                        int versionStartIndex = versionKeyIndex + versionKey.Length;
                        string packageInfoAfterVersionKey = packageInfo.Substring(versionStartIndex);

                        string seapirater = ",";
                        string version;
                        if (packageInfoAfterVersionKey.Contains(seapirater))
                        {
                            int firstSeapirater = packageInfoAfterVersionKey.IndexOf(seapirater);
                            version = packageInfoAfterVersionKey.Substring(0, firstSeapirater);
                        }
                        else
                        {
                            version = packageInfoAfterVersionKey;
                        }

                        var dep = new NugetDependency(artifact, NuGet.Versioning.VersionRange.Parse(version));
                        deps.Add(dep);
                    }
                }
                var globalProperties = new Dictionary<string, string>(proj.GlobalProperties);
                ProjectCollection.GlobalProjectCollection.UnloadProject(proj);

                if (targetFrameworks.Count > 0)
                {
                    var groups = GroupReferencesByFramework(deps, packageReferences, targetFrameworks, globalProperties);
                    return FrameworkGraphMerger.Merge(NugetSearchService, FailedPackageIdentities, groups);
                }

                // Without a valid TFM, dependency lookup cannot select framework-specific groups.
                var tree = new NugetTreeResolver(NugetSearchService, FailedPackageIdentities);
                foreach (var dep in deps)
                {
                    tree.Add(dep);
                }

                var result = new DependencyResult()
                {
                    Success = true,
                    Packages = tree.GetPackageList(),
                    Dependencies = new HashSet<PackageId>()
                };

                foreach (var package in result.Packages)
                {
                    var anyPackageReferences = result.Packages.Where(pkg => pkg.Dependencies.Contains(package.PackageId)).Any();
                    if (!anyPackageReferences)
                    {
                        result.Dependencies.Add(package.PackageId);
                    }
                }

                return result;
            }
            catch (InvalidProjectFileException e)
            {
                return new DependencyResult()
                {
                    Success = false
                };
            }
        }
        
        private Dictionary<NuGetFramework, List<NugetDependency>> GroupReferencesByFramework(
            List<NugetDependency> references, List<NugetDependency> packageReferences,
            List<NuGetFramework> frameworks, Dictionary<string, string> globalProperties)
        {
            var groups = new Dictionary<NuGetFramework, List<NugetDependency>>();
            var matched = new HashSet<NugetDependency>();
            var ambiguous = new HashSet<NugetDependency>();
            foreach (NuGetFramework framework in frameworks)
            {
                using var collection = new ProjectCollection();
                List<NugetDependency> evaluated;
                try
                {
                    // Conditions and version properties depend on the target framework's MSBuild evaluation.
                    var properties = new Dictionary<string, string>(globalProperties)
                    {
                        ["TargetFramework"] = framework.GetShortFolderName()
                    };
                    var project = new Microsoft.Build.Evaluation.Project(ProjectPath, properties, null, collection);
                    evaluated = ReadEvaluatedPackageReferences(project);
                }
                catch (Exception exception) when (exception is InvalidProjectFileException || exception is IOException)
                {
                    Console.WriteLine($"Unable to evaluate target framework '{framework.GetShortFolderName()}'; resolving all reported references for this framework: {exception.Message}");
                    groups.Add(framework, references.ToList());
                    matched.UnionWith(packageReferences);
                    continue;
                }
                finally
                {
                    collection.UnloadAllProjects();
                }

                foreach (NugetDependency item in evaluated.Where(item => !packageReferences.Any(reference => MatchesReference(reference, item))))
                {
                    ambiguous.UnionWith(packageReferences.Where(reference => string.Equals(reference.Name, item.Name, StringComparison.OrdinalIgnoreCase)));
                }
                var applicable = references.Where(reference => !packageReferences.Contains(reference) ||
                    evaluated.Any(item => MatchesReference(reference, item))).ToList();
                matched.UnionWith(applicable.Where(packageReferences.Contains));
                groups.Add(framework, applicable);
            }

            foreach (NugetDependency reference in ambiguous)
            {
                // Preserve the reported version when per-framework properties prevent reliable matching.
                Console.WriteLine($"PackageReference '{reference.Name}' version '{reference.VersionRange.OriginalString}' could not be matched unambiguously to target framework evaluations; resolving the reported reference for all target frameworks.");
                foreach (var group in groups.Values)
                {
                    if (!group.Contains(reference))
                    {
                        group.Add(reference);
                    }
                }
                matched.Add(reference);
            }

            foreach (NugetDependency reference in packageReferences.Where(reference => !matched.Contains(reference)))
            {
                // Retain references excluded everywhere rather than silently losing dependencies under non-framework conditions.
                Console.WriteLine($"PackageReference '{reference.Name}' was excluded by its condition in every target framework evaluation; resolving it for all target frameworks.");
                foreach (var group in groups.Values)
                {
                    if (!group.Contains(reference))
                    {
                        group.Add(reference);
                    }
                }
            }
            return groups;
        }

        private static bool MatchesReference(NugetDependency reference, NugetDependency evaluated)
        {
            return string.Equals(reference.Name, evaluated.Name, StringComparison.OrdinalIgnoreCase) &&
                reference.VersionRange.OriginalString == evaluated.VersionRange.OriginalString;
        }

        private List<NugetDependency> ReadEvaluatedPackageReferences(Microsoft.Build.Evaluation.Project project)
        {
            var dependencies = new List<NugetDependency>();
            foreach (ProjectItem item in project.GetItems("PackageReference"))
            {
                if (ExcludedDependencyTypeUtil.isDependencyTypeExcluded(ExcludedDependencyTypes, "DEV") &&
                    item.Metadata.Any(metadata => metadata.Name == "PrivateAssets"))
                {
                    continue;
                }
                var central = CentrallyManagedPackages?.FirstOrDefault(package => package.Name.Equals(item.EvaluatedInclude));
                var versionOverride = item.Metadata.FirstOrDefault(metadata => metadata.Name == "VersionOverride");
                if (central != null && versionOverride != null && !CheckVersionOverride)
                {
                    continue;
                }
                string? version = central != null
                    ? (CheckVersionOverride && versionOverride != null ? versionOverride.EvaluatedValue : central.Version)
                    : item.Metadata.FirstOrDefault(metadata => metadata.Name == "Version")?.EvaluatedValue;
                if (version != null)
                {
                    addNugetDependency(item.EvaluatedInclude, version, dependencies);
                }
            }
            return dependencies;
        }

        private void addNugetDependency(string include, string versionMetadata, List<NugetDependency> deps)
        {
            NuGet.Versioning.VersionRange version;
            if (NuGet.Versioning.VersionRange.TryParse(versionMetadata, out version))
            {
                var dep = new NugetDependency(include, version);
                deps.Add(dep);
            }
        }
    }
}
