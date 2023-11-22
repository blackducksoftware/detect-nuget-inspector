using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Synopsys.Detect.Nuget.Inspector.Model;

namespace Synopsys.Detect.Nuget.Inspector.DependencyResolution.Project
{
    class ProjectXmlResolver : DependencyResolver
    {

        private string ProjectPath;
        private NugetSearchService NugetSearchService;
        private HashSet<PackageId> CentrallyManagedPackages;
        private bool CheckVersionOverride;

        public ProjectXmlResolver(string projectPath, NugetSearchService nugetSearchService)
        {
            ProjectPath = projectPath;
            NugetSearchService = nugetSearchService;
        }
        
        public ProjectXmlResolver(string projectPath, NugetSearchService nugetSearchService, HashSet<PackageId> packages, bool checkVersionOverride): this(projectPath, nugetSearchService)
        {
            CentrallyManagedPackages = packages;
            CheckVersionOverride = checkVersionOverride;
        }


        public DependencyResult Process()
        {
            var result = new DependencyResult();
            var tree = new NugetTreeResolver(NugetSearchService);
            
            // .NET core default version
            result.ProjectVersion = "1.0.0";
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            XmlDocument doc = new XmlDocument();
            doc.Load(ProjectPath);

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
                        XmlAttribute include = attributes["Include"];
                        
                        string versionOverrideStr = GetVersionInformation(attributes, "VersionOverride", package);
                        string versionStr = GetVersionInformation(attributes, "Version", package);

                        if (include != null)
                        {
                            bool containsPkg = CentrallyManagedPackages != null && CentrallyManagedPackages.Any(pkg => pkg.Name.Equals(include.Value));
                            
                            if (containsPkg)
                            {
                                PackageId pkg = CentrallyManagedPackages.First(pkg => pkg.Name.Equals(include.Value));
                                
                                if (!String.IsNullOrWhiteSpace(versionOverrideStr) && CheckVersionOverride)
                                { 
                                    addNugetDependency(tree, include.Value, versionStr);
                                }
                                else if (!String.IsNullOrWhiteSpace(versionOverrideStr) && !CheckVersionOverride)
                                {
                                    Console.WriteLine("The Central Package Version Overriding is disabled, please enable version override or remove VersionOverride tags from project");
                                }
                                else
                                {
                                    addNugetDependency(tree, include.Value, pkg.Version);
                                }
                            }
                            else
                            {
                                if (!String.IsNullOrWhiteSpace(versionStr))
                                {
                                    addNugetDependency(tree, include.Value, versionStr);
                                }
                            }
                        }
                    }
                }
            }

            result.Packages = tree.GetPackageList();
            result.Dependencies = new List<PackageId>();
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
        
        private string GetVersionInformation(XmlAttributeCollection attributes, string checkString, XmlNode package)
        {
            string versionStr = null;
            
            XmlAttribute version = attributes[checkString];

            if (version == null)
            {
                foreach (XmlNode node in package.ChildNodes)
                {
                    if (node.Name == checkString)
                    {
                        versionStr = node.InnerXml;
                        break;
                    }
                }
            }
            else
            {
                versionStr = version.Value;
            }
            
            return versionStr;
        }

        private void addNugetDependency(NugetTreeResolver tree, string include, string version)
        {
            var dep = new NugetDependency(include, NuGet.Versioning.VersionRange.Parse(version));
            tree.Add(dep);
        }
    }
}
