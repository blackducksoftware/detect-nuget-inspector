using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Blackduck.Detect.Nuget.Inspector.Inspection.Util;
using Blackduck.Detect.Nuget.Inspector.Model;
using NuGet.Frameworks;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project
{
    class ProjectXmlResolver : DependencyResolver
    {

        private string ProjectPath;
        private NugetSearchService NugetSearchService;
        private HashSet<PackageId> CentrallyManagedPackages;
        private bool CheckVersionOverride;
        private string ExcludedDependencyTypes;
        private ISet<string>? FailedPackageIdentities;

        public ProjectXmlResolver(string projectPath, NugetSearchService nugetSearchService, string excludedDependencyTypes, ISet<string>? failedPackageIdentities = null)
        {
            ProjectPath = projectPath;
            NugetSearchService = nugetSearchService;
            ExcludedDependencyTypes = excludedDependencyTypes;
            FailedPackageIdentities = failedPackageIdentities;
        }
        
        public ProjectXmlResolver(string projectPath, NugetSearchService nugetSearchService, string excludedDependencyTypes, HashSet<PackageId> centrallyManagedPackages, bool checkVersionOverride, ISet<string>? failedPackageIdentities = null): this(projectPath, nugetSearchService, excludedDependencyTypes, failedPackageIdentities)
        {
            CentrallyManagedPackages = centrallyManagedPackages;
            CheckVersionOverride = checkVersionOverride;
        }


        public DependencyResult Process()
        {
            var result = new DependencyResult();
            var deps = new List<NugetDependency>();
            
            // .NET core default version
            result.ProjectVersion = "1.0.0";
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            XmlDocument doc = new XmlDocument();
            doc.Load(ProjectPath);
            // PXR reads raw XML, so unexpanded $(...) values are rejected and keep the null-framework fallback.
            List<NuGetFramework> targetFrameworks = TargetFrameworkHelper.Parse(
                GetRawFrameworkValues(doc, "TargetFramework"),
                GetRawFrameworkValues(doc, "TargetFrameworks"));
            if (targetFrameworks.Count == 0)
            {
                Console.WriteLine("Target framework could not be determined; using framework-independent dependency lookup.");
            }

            XmlNodeList versionNodes = doc.GetElementsByTagName("Version");
            if (versionNodes != null && versionNodes.Count > 0)
            {
                foreach (XmlNode version in versionNodes)
                {
                    if (version.NodeType != XmlNodeType.Comment)
                    {
                        result.ProjectVersion = version.InnerText;
                    }
                }
            }
            else
            {
                string prefix = "1.0.0";
                string suffix = "";
                XmlNodeList prefixNodes = doc.GetElementsByTagName("VersionPrefix");
                if (prefixNodes != null && prefixNodes.Count > 0)
                {
                    foreach (XmlNode prefixNode in prefixNodes)
                    {
                        if (prefixNode.NodeType != XmlNodeType.Comment)
                        {
                            prefix = prefixNode.InnerText;
                        }
                    }
                }
                XmlNodeList suffixNodes = doc.GetElementsByTagName("VersionSuffix");
                if (suffixNodes != null && suffixNodes.Count > 0)
                {
                    foreach (XmlNode suffixNode in suffixNodes)
                    {
                        if (suffixNode.NodeType != XmlNodeType.Comment)
                        {
                            suffix = suffixNode.InnerText;
                        }
                    }

                }
                result.ProjectVersion = String.Format("{0}-{1}", prefix, suffix); ;
            }
            XmlNodeList packagesNodes = doc.GetElementsByTagName("PackageReference");
            if (packagesNodes.Count > 0)
            {
                foreach (XmlNode package in packagesNodes)
                {
                    XmlAttributeCollection attributes = package.Attributes;
                    if (attributes != null)
                    {
                        string include = InspectorUtil.GetAttributeInformation(attributes, "Include", package);
                        string versionOverrideStr = InspectorUtil.GetAttributeInformation(attributes, "VersionOverride", package);
                        string versionStr = InspectorUtil.GetAttributeInformation(attributes, "Version", package);
                        string privateAssets = InspectorUtil.GetAttributeInformation(attributes, "PrivateAssets", package);

                        bool isDevDependencyTypeExcluded = ExcludedDependencyTypeUtil.isDependencyTypeExcluded(ExcludedDependencyTypes,"DEV");
                                           
                        bool excludeDevDependency = isDevDependencyTypeExcluded && !String.IsNullOrWhiteSpace(privateAssets);
                        
                        if (!String.IsNullOrWhiteSpace(include) && !excludeDevDependency)
                        {
                            bool containsPkg = CentrallyManagedPackages != null && CentrallyManagedPackages.Any(pkg => pkg.Name.Equals(include));
                            
                            if (containsPkg)
                            {
                                PackageId pkg = CentrallyManagedPackages.First(pkg => pkg.Name.Equals(include));
                                
                                if (!String.IsNullOrWhiteSpace(versionOverrideStr) && CheckVersionOverride)
                                { 
                                    addNugetDependency(deps, include, versionOverrideStr);
                                }
                                else if (!String.IsNullOrWhiteSpace(versionOverrideStr) && !CheckVersionOverride)
                                {
                                    Console.WriteLine("The Central Package Version Overriding is disabled, please enable version override or remove VersionOverride tags from project");
                                }
                                else
                                {
                                    addNugetDependency(deps, include, pkg.Version);
                                }
                            }
                            else
                            {
                                if (!String.IsNullOrWhiteSpace(versionStr))
                                {
                                    addNugetDependency(deps, include, versionStr);
                                }
                            }
                        }
                    }
                }
            }

            PackageSetBuilder? mergedBuilder = null;
            // No valid TFM: keep the original single framework-independent tree.
            if (targetFrameworks.Count == 0)
            {
                var tree = new NugetTreeResolver(NugetSearchService, FailedPackageIdentities);
                foreach (var dep in deps)
                {
                    tree.Add(dep);
                }
                result.Packages = tree.GetPackageList();
            }
            else
            {
                mergedBuilder = FrameworkGraphMerger.Merge(NugetSearchService, FailedPackageIdentities, deps, targetFrameworks);
                result.Packages = mergedBuilder.GetPackageList();
            }
            result.Dependencies = new HashSet<PackageId>();
            foreach (var package in result.Packages)
            {
                var anyPackageReferences = result.Packages.Where(pkg => pkg.Dependencies.Contains(package.PackageId)).Any();
                if (!anyPackageReferences)
                {
                    result.Dependencies.Add(package.PackageId);
                }
            }

            // Only merged TFM graphs can form the cycles that hide direct references from the root check above.
            if (mergedBuilder != null)
            {
                FrameworkGraphMerger.AddUnreachableDirectReferences(result, mergedBuilder, deps);
            }

            return result;
        }

        private static string GetRawFrameworkValues(XmlDocument doc, string elementName)
        {
            return string.Join(";", doc.GetElementsByTagName(elementName)
                .Cast<XmlNode>()
                .Where(node => node.NodeType != XmlNodeType.Comment)
                .Select(node => node.InnerText));
        }

        private void addNugetDependency(List<NugetDependency> deps, string include, string version)
        {
            if (!NuGet.Versioning.VersionRange.TryParse(version, out var versionRange))
            {
                Console.WriteLine($"WARNING: Skipping package {include}: unresolved version '{version}' (MSBuild properties can't be evaluated without a .NET SDK).");
                return;
            }
            deps.Add(new NugetDependency(include, versionRange));
        }
    }
}
