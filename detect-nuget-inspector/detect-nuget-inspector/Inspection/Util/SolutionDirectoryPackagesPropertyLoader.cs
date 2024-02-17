using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Synopsys.Detect.Nuget.Inspector.Inspection.Util;
using Synopsys.Detect.Nuget.Inspector.Model;

namespace Synopsys.Detect.Nuget.Inspector.Inspection.Inspectors
{
    public class SolutionDirectoryPackagesPropertyLoader : PackageReferenceLoader
    {

        private String PropertyPath;
        private HashSet<PackageId> RootCentrallyManagedPackages;
        private String ExcludedDependencyTypes;

        public SolutionDirectoryPackagesPropertyLoader(String propertyPath, String excludedDependencyTypes)
        {
            PropertyPath = propertyPath;
            ExcludedDependencyTypes = excludedDependencyTypes;
        }
        
        public SolutionDirectoryPackagesPropertyLoader(String propertyPath, String excludedDependencyTypes, HashSet<PackageId> rootCentrallyManagedPackages): this(propertyPath, excludedDependencyTypes)
        {
            RootCentrallyManagedPackages = rootCentrallyManagedPackages;
        }

        public HashSet<PackageId> Process()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            XmlDocument doc = new XmlDocument();
            doc.Load(PropertyPath);

            XmlNodeList propertyGroupTags = doc.GetElementsByTagName("PropertyGroup");

            Dictionary<string, string> propertyGroups = GetPropertyGroups(propertyGroupTags);

            HashSet<PackageId> packageReferences = new HashSet<PackageId>();

            bool managePackageVersionsCentrally = propertyGroups.ContainsKey("ManagePackageVersionsCentrally")
                ? Boolean.Parse(propertyGroups["ManagePackageVersionsCentrally"])
                : true;
            
            if (managePackageVersionsCentrally)
            {
                XmlNodeList importNodes = doc.GetElementsByTagName("Import");

                var propsFilePath = GetPropsFilePath(importNodes);

                if (!String.IsNullOrWhiteSpace(propsFilePath) && !propsFilePath.Equals("..\\Directory.Packages.props"))
                {
                    Console.WriteLine("Import of Directory.Packages.props file from a non-standard location.");
                }
                
                XmlNodeList packageVersionNodes = doc.GetElementsByTagName("PackageVersion");

                if (packageVersionNodes != null && packageVersionNodes.Count > 0)
                {
                    packageReferences.UnionWith(GetPackageVersions(packageVersionNodes, propertyGroups));
                }
            }
            else
            {
                Console.WriteLine("The user has disabled Central Package Management. Will skip parsing over this file");
            }

            return packageReferences;
        }

        private HashSet<PackageId> GetPackageVersions(XmlNodeList packageNodes, Dictionary<string,string> propertyGroups)
        {
            HashSet<PackageId> packages = new HashSet<PackageId>();
            foreach (XmlNode packageNode in packageNodes)
            {
                if (packageNode.NodeType != XmlNodeType.Comment)
                {
                    XmlAttributeCollection attributes = packageNode.Attributes;
                    
                    string packageName = InspectorUtil.GetAttributeInformation(attributes, "Include", packageNode);
                    string packageVersion = InspectorUtil.GetAttributeInformation(attributes,"Version", packageNode);
                    string privateAssets = InspectorUtil.GetAttributeInformation(attributes,"PrivateAssets", packageNode);

                    bool isDevDependencyTypeExcluded = ExcludedDependencyTypeUtil.isDependencyTypeExcluded(ExcludedDependencyTypes,"DEV");
                                           
                    bool excludeDevDependency = isDevDependencyTypeExcluded && !String.IsNullOrWhiteSpace(privateAssets);

                    if (!String.IsNullOrWhiteSpace(packageVersion) && packageVersion.Contains("$("))
                    {
                        string propertyName = packageVersion.Substring(packageVersion.IndexOf("(") + 1, packageVersion.IndexOf(")") - 2);
                        if (propertyGroups.ContainsKey(propertyName))
                        {
                            packageVersion = propertyGroups[propertyName];
                        }
                    }

                    if (RootCentrallyManagedPackages != null && RootCentrallyManagedPackages.Count > 0)
                    {
                        bool containsPkg = RootCentrallyManagedPackages.Any(pkg => pkg.Name.Equals(packageName));

                        if (containsPkg)
                        {
                            var pkg = RootCentrallyManagedPackages.First(pkg => pkg.Name.Equals(packageName));
                            RootCentrallyManagedPackages.Remove(pkg);
                        }
                    }

                    if (!String.IsNullOrWhiteSpace(packageName) && !String.IsNullOrWhiteSpace(packageVersion) && !excludeDevDependency)
                    {
                        packages.Add(new PackageId(packageName, packageVersion));
                    }
                }
            }

            return packages;
        }
        
        public bool GetVersionOverrideEnabled()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            XmlDocument doc = new XmlDocument();
            doc.Load(PropertyPath);
            
            XmlNodeList centralVersionOverride = doc.GetElementsByTagName("CentralPackageVersionOverrideEnabled");

            bool checkVersionOverride = !(centralVersionOverride != null && centralVersionOverride.Count > 0 && centralVersionOverride.Item(0).InnerXml == "false");

            return checkVersionOverride;
        }

        public HashSet<PackageId> GetGlobalPackageReferences()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            XmlDocument doc = new XmlDocument();
            doc.Load(PropertyPath);

            HashSet<PackageId> globalPackageReferences = new HashSet<PackageId>();
            
            XmlNodeList propertyGroupTags = doc.GetElementsByTagName("PropertyGroup");
            
            Dictionary<string, string> propertyGroups = GetPropertyGroups(propertyGroupTags);

            bool managePackageVersionsCentrally = propertyGroups.ContainsKey("ManagePackageVersionsCentrally")
                ? Boolean.Parse(propertyGroups["ManagePackageVersionsCentrally"])
                : true;
            
            XmlNodeList globalPackageReferenceNodes = doc.GetElementsByTagName("GlobalPackageReference");

            if (managePackageVersionsCentrally && globalPackageReferenceNodes != null && globalPackageReferenceNodes.Count > 0)
            {
               globalPackageReferences = GetPackageVersions(globalPackageReferenceNodes,propertyGroups);
            }

            return globalPackageReferences;
        }

        public string GetPropsFilePath(XmlNodeList importNodes)
        {
            if (importNodes != null && importNodes.Count > 0)
            {
                foreach (XmlNode node in importNodes)
                {
                    XmlAttributeCollection attributes = node.Attributes;

                    foreach (XmlAttribute attr in attributes)
                    {
                        if (attr.LocalName.Contains("Project"))
                        {
                            return attr.Value;
                        }
                    }
                }
            }
            return null;
        }

        public Dictionary<string, string> GetPropertyGroups(XmlNodeList propertyGroups)
        {
            Dictionary<string, string> groups = new Dictionary<string, string>();

            if (propertyGroups != null && propertyGroups.Count > 0)
            {
                foreach (XmlNode propertyGroup in propertyGroups)
                {
                    foreach (XmlNode node in propertyGroup.ChildNodes)
                    {
                        if (!groups.ContainsKey(node.Name))
                        {
                            groups.Add(node.Name, node.InnerXml);
                        }
                    }
                }
            }

            return groups;
        }
    }
}
