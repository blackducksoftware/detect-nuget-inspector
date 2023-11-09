using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Synopsys.Detect.Nuget.Inspector.Model;

namespace Synopsys.Detect.Nuget.Inspector.DependencyResolution.Project
{
    class ProjectReferenceResolver : DependencyResolver
    {

        private string ProjectPath;
        private NugetSearchService NugetSearchService;
        private HashSet<PackageId> Packages;
        private bool CheckVersionOverride;
        
        public ProjectReferenceResolver(string projectPath, NugetSearchService nugetSearchService)
        {
            ProjectPath = projectPath;
            NugetSearchService = nugetSearchService;
        }
        
        public ProjectReferenceResolver(string projectPath, NugetSearchService nugetSearchService, HashSet<PackageId> packages, bool checkVersionOverride)
        {
            ProjectPath = projectPath;
            NugetSearchService = nugetSearchService;
            Packages = packages;
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
                    bool containsPkg = Packages.Any(pkg => pkg.Name.Equals(reference.EvaluatedInclude));
                    var versionMetaData = reference.Metadata.Where(meta => meta.Name == "Version").FirstOrDefault();
                    var versionOverrideMetaData = reference.Metadata.Where(meta => meta.Name == "VersionOverride").FirstOrDefault();
                    if (containsPkg && versionOverrideMetaData != null && CheckVersionOverride)
                    {
                        NuGet.Versioning.VersionRange version;
                        if (NuGet.Versioning.VersionRange.TryParse(versionOverrideMetaData.EvaluatedValue, out version))
                        {
                            var dep = new NugetDependency(reference.EvaluatedInclude, version);
                            deps.Add(dep);
                        }
                    }
                    else if (versionMetaData != null && !containsPkg)
                    {
                        NuGet.Versioning.VersionRange version;
                        if (NuGet.Versioning.VersionRange.TryParse(versionMetaData.EvaluatedValue, out version))
                        {
                            var dep = new NugetDependency(reference.EvaluatedInclude, version);
                            deps.Add(dep);
                        }
                    }
                    else if (containsPkg)
                    {
                        PackageId pkg = Packages.First(pkg => pkg.Name.Equals(reference.EvaluatedInclude));
                        
                        NuGet.Versioning.VersionRange version;
                        if (NuGet.Versioning.VersionRange.TryParse(pkg.Version, out version))
                        {
                            var dep = new NugetDependency(reference.EvaluatedInclude, version);
                            deps.Add(dep);
                        }
                    }
                    else
                    {
                        Console.WriteLine("Framework dependency had no version, will not be included: " + reference.EvaluatedInclude);
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

                        var dep = new NugetDependency(artifact, NuGet.Versioning.VersionRange.Parse(version));
                        deps.Add(dep);
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
    }
}
