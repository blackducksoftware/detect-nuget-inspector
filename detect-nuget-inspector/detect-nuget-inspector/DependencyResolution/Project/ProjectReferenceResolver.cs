using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Synopsys.Detect.Nuget.Inspector.Inspection.Util;
using Synopsys.Detect.Nuget.Inspector.Model;

namespace Synopsys.Detect.Nuget.Inspector.DependencyResolution.Project
{
    class ProjectReferenceResolver : DependencyResolver
    {

        private string ProjectPath;
        private NugetSearchService NugetSearchService;
        private HashSet<PackageId> CentrallyManagedPackages;
        private bool CheckVersionOverride;
        private string ExcludedDependencyTypes;
        
        public ProjectReferenceResolver(string projectPath, NugetSearchService nugetSearchService, String excludedDependencyTypes)
        {
            ProjectPath = projectPath;
            NugetSearchService = nugetSearchService;
            ExcludedDependencyTypes = excludedDependencyTypes;
        }
        
        public ProjectReferenceResolver(string projectPath, NugetSearchService nugetSearchService, String excludedDependencyTypes, HashSet<PackageId> centrallyManagedPackages, bool checkVersionOverride): this(projectPath, nugetSearchService, excludedDependencyTypes)
        {
            CentrallyManagedPackages = centrallyManagedPackages;
            CheckVersionOverride = checkVersionOverride;
        }
        
        public DependencyResult Process()
        {
            try
            {
                var tree = new NugetTreeResolver(NugetSearchService);

                Microsoft.Build.Evaluation.Project proj = new Microsoft.Build.Evaluation.Project(ProjectPath);

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
                            PackageId pkg = CentrallyManagedPackages.First(pkg => pkg.Name.Equals(reference.EvaluatedInclude));

                            if (CheckVersionOverride && versionOverrideMetaData != null)
                            {
                                addNugetDependency(reference.EvaluatedInclude, versionOverrideMetaData.EvaluatedValue, tree);
                            }
                            else if (!CheckVersionOverride && versionOverrideMetaData != null)
                            {
                                Console.WriteLine("The Central Package Version Overriding is disabled, please enable version override or remove VersionOverride tags from project");
                            }
                            else
                            {
                                addNugetDependency(reference.EvaluatedInclude, pkg.Version, tree);
                            }
                        }
                        else if (versionMetaData != null)
                        {
                            addNugetDependency(reference.EvaluatedInclude, versionMetaData.EvaluatedValue, tree);
                        }
                        else
                        {
                            Console.WriteLine("Framework dependency had no version, will not be included: " + reference.EvaluatedInclude);
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

                        tree.Add(new NugetDependency(artifact, NuGet.Versioning.VersionRange.Parse(version)));
                    }
                }
                ProjectCollection.GlobalProjectCollection.UnloadProject(proj);

                var result = new DependencyResult()
                {
                    Success = true,
                    Packages = tree.GetPackageList(),
                    Dependencies = new List<PackageId>()
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
        
        private void addNugetDependency(string include, string versionMetadata, NugetTreeResolver tree)
        {
            NuGet.Versioning.VersionRange version;
            if (NuGet.Versioning.VersionRange.TryParse(versionMetadata, out version))
            {
                tree.Add(new NugetDependency(include, version));
            }
        }
    }
}
