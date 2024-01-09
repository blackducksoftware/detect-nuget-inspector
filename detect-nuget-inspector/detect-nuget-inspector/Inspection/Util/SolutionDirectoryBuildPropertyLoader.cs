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
    class SolutionDirectoryBuildPropertyLoader : PackageReferenceLoader
    {

        private string PropertyPath;
        private NugetSearchService NugetSearchService;
        private bool CheckVersionOverride;
        private HashSet<PackageId> CentrallyManagedPackages;
        private string ExcludedDependencyTypes;

        public SolutionDirectoryBuildPropertyLoader(string propertyPath, NugetSearchService nugetSearchService, HashSet<PackageId> centrallyManagedPackages, bool checkVersionOverride, string excludedDependencyTypes)
        {
            PropertyPath = propertyPath;
            NugetSearchService = nugetSearchService;
            CentrallyManagedPackages = centrallyManagedPackages;
            CheckVersionOverride = checkVersionOverride;
            ExcludedDependencyTypes = excludedDependencyTypes;
        }

        public HashSet<PackageId> Process()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            XmlDocument doc = new XmlDocument();
            doc.Load(PropertyPath);

            HashSet<PackageId> packageReferences = new HashSet<PackageId>();

            XmlNodeList packageReferenceNodes = doc.GetElementsByTagName("PackageReference");
            if (packageReferenceNodes != null && packageReferenceNodes.Count > 0)
            {
                packageReferences.UnionWith(GetPackageReference(packageReferenceNodes));
            }

            packageReferenceNodes = doc.GetElementsByTagName("PackageVersion");
            if (packageReferenceNodes != null && packageReferenceNodes.Count > 0)
            {
                packageReferences.UnionWith(GetPackageReference(packageReferenceNodes));
            }

            return packageReferences;
        }

        private HashSet<PackageId> GetPackageReference(XmlNodeList packageNodes)
        {
            HashSet<PackageId> packageReferences = new HashSet<PackageId>();
            foreach (XmlNode packageNode in packageNodes)
            {
                if (packageNode.NodeType != XmlNodeType.Comment)
                {
                    XmlAttributeCollection attributes = packageNode.Attributes;

                    string name = InspectorUtil.GetAttributeInformation(attributes, "Include", packageNode);
                    string versionOverrideStr = InspectorUtil.GetAttributeInformation(attributes, "VersionOverride", packageNode);
                    string versionStr = InspectorUtil.GetAttributeInformation(attributes, "Version", packageNode);
                    string privateAssets = InspectorUtil.GetAttributeInformation(attributes, "PrivateAssets", packageNode);

                    bool isDependency =
                        ExcludedDependencyTypeUtil.isDependencyTypeExcluded(ExcludedDependencyTypes) && !String.IsNullOrWhiteSpace(privateAssets);

                    if (!String.IsNullOrWhiteSpace(name) && !isDependency)
                    {
                       bool containsPkg =  CentrallyManagedPackages != null && CentrallyManagedPackages.Any(pkg => pkg.Name.Equals(name));
                       
                       if (containsPkg)
                       {
                           var pkg = CentrallyManagedPackages.First(pkg => pkg.Name.Equals(name));
                           
                           if (!String.IsNullOrWhiteSpace(versionOverrideStr) && (CheckVersionOverride || GetVersionOverrideEnabled()))
                           {
                               packageReferences.Add(new PackageId(name, versionOverrideStr));
                           }
                           else if (!String.IsNullOrWhiteSpace(versionOverrideStr) && !CheckVersionOverride)
                           {
                               Console.WriteLine("The Central Package Version Overriding is disabled, please enable version override or remove VersionOverride tags from project");
                           }
                           else
                           {
                               packageReferences.Add(pkg);
                           }
                       }
                       else
                       {
                           if (!String.IsNullOrWhiteSpace(versionStr))
                           {
                               packageReferences.Add(new PackageId(name, versionStr));
                           }
                       }
                    }
                }
            }
            return packageReferences;
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
    }
}
