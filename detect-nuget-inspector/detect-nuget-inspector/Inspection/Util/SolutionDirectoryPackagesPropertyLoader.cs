using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Synopsys.Detect.Nuget.Inspector.Model;

namespace Synopsys.Detect.Nuget.Inspector.Inspection.Inspectors
{
    public class SolutionDirectoryPackagesPropertyLoader : PackageReferenceLoader
    {

        private String PropertyPath;

        public SolutionDirectoryPackagesPropertyLoader(String propertyPath)
        {
            PropertyPath = propertyPath;
        }

        public HashSet<PackageId> Process()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            XmlDocument doc = new XmlDocument();
            doc.Load(PropertyPath);

            XmlNodeList manageCentralPackageVersion = doc.GetElementsByTagName("ManagePackageVersionsCentrally");

            HashSet<PackageId> packageReferences = new HashSet<PackageId>();
            
            if (CheckCentralPackageManagementEnabled(manageCentralPackageVersion))
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
                    packageReferences.UnionWith(GetPackageVersions(packageVersionNodes));
                }
            }
            else
            {
                Console.WriteLine("The user has disabled Central Package Management. Will skip parsing over this file");
            }

            return packageReferences;
        }

        private bool CheckCentralPackageManagementEnabled(XmlNodeList manageCentralPackageVersion)
        {
            if (manageCentralPackageVersion != null && manageCentralPackageVersion.Count > 0)
            {
                return manageCentralPackageVersion.Item(0).InnerXml == "true";
            }
            return true;
        }

        private HashSet<PackageId> GetPackageVersions(XmlNodeList packageNodes)
        {
            HashSet<PackageId> packages = new HashSet<PackageId>();
            foreach (XmlNode packageNode in packageNodes)
            {
                if (packageNode.NodeType != XmlNodeType.Comment)
                {
                    XmlAttributeCollection attributes = packageNode.Attributes;
                    String packageName = null;
                    String packageVersion = null;

                    foreach (XmlAttribute attribute in attributes)
                    {
                        if (attribute.LocalName.Contains("Include"))
                        {
                            packageName = attribute.Value;
                        }

                        if (attribute.LocalName.Contains("Version"))
                        {
                            packageVersion = attribute.Value;
                        }
                    }

                    if (String.IsNullOrWhiteSpace(packageVersion))
                    {
                        foreach (XmlNode node in packageNode.ChildNodes)
                        {
                            if (node.Name == "Version")
                            {
                                packageVersion = node.InnerXml;
                                break;
                            }
                        }
                    }

                    if (!String.IsNullOrWhiteSpace(packageName) && !String.IsNullOrWhiteSpace(packageVersion))
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
            
            XmlNodeList manageCentralPackageVersion = doc.GetElementsByTagName("ManagePackageVersionsCentrally");

            HashSet<PackageId> globalPackageReferences = new HashSet<PackageId>();
            
            XmlNodeList globalPackageReferenceNodes = doc.GetElementsByTagName("GlobalPackageReference");

            if (CheckCentralPackageManagementEnabled(manageCentralPackageVersion) && globalPackageReferenceNodes != null && globalPackageReferenceNodes.Count > 0)
            {
               globalPackageReferences = GetPackageVersions(globalPackageReferenceNodes);
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
    }
}
