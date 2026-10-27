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
                var tree = new NugetTreeResolver(NugetSearchService, FailedPackageIdentities);

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
                                    deps, targetFrameworks);
                            }
                            else if (!CheckVersionOverride && versionOverrideMetaData != null)
                            {
                                Console.WriteLine(
                                    "The Central Package Version Overriding is disabled, please enable version override or remove VersionOverride tags from project");
                            }
                            else
                            {
                                addNugetDependency(reference.EvaluatedInclude, pkg.Version, deps, targetFrameworks);
                            }
                        }
                        else if (versionMetaData != null)
                        {
                            addNugetDependency(reference.EvaluatedInclude, versionMetaData.EvaluatedValue, deps, targetFrameworks);
                        }
                        else
                        {
                            Console.WriteLine("Framework dependency had no version, will not be included: " +
                                              reference.EvaluatedInclude);
                        }
                    }
                }

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

                        addNugetDependency(artifact, NuGet.Versioning.VersionRange.Parse(version), deps, targetFrameworks);
                    }
                }
                ProjectCollection.GlobalProjectCollection.UnloadProject(proj);

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
        
        private void addNugetDependency(string include, string versionMetadata, List<NugetDependency> deps, List<NuGetFramework> targetFrameworks)
        {
            NuGet.Versioning.VersionRange version;
            if (NuGet.Versioning.VersionRange.TryParse(versionMetadata, out version))
            {
                addNugetDependency(include, version, deps, targetFrameworks);
            }
        }

        private static void addNugetDependency(string include, NuGet.Versioning.VersionRange version, List<NugetDependency> deps, List<NuGetFramework> targetFrameworks)
        {
            if (targetFrameworks.Count == 0)
            {
                deps.Add(new NugetDependency(include, version));
                return;
            }

            // Keep one merged graph across TFMs, matching the lock-file resolver output.
            foreach (NuGetFramework framework in targetFrameworks)
            {
                deps.Add(new NugetDependency(include, version, framework));
            }
        }
    }
}
