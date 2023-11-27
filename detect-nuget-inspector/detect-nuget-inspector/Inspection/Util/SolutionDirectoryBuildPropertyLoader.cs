using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Synopsys.Detect.Nuget.Inspector.Model;

namespace Synopsys.Detect.Nuget.Inspector.Inspection.Inspectors
{
    class SolutionDirectoryBuildPropertyLoader : PackageReferenceLoader
    {

        private string PropertyPath;
        private NugetSearchService NugetSearchService;
        private bool CheckVersionOverride;

        public SolutionDirectoryBuildPropertyLoader(string propertyPath, NugetSearchService nugetSearchService, bool checkVersionOverride)
        {
            PropertyPath = propertyPath;
            NugetSearchService = nugetSearchService;
            CheckVersionOverride = checkVersionOverride;
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
                    string name = null;
                    string version = null;
                    string versionOverride = null;
                    foreach (XmlAttribute at in attributes)
                    {
                        if (at.LocalName.Contains("Include"))
                        {
                            name = at.Value;
                        } 
                        else if (at.LocalName.Contains("Version"))
                        {
                            version = at.Value;
                        }
                        else if (at.LocalName.Contains("VersionOverride"))
                        {
                            versionOverride = at.Value;
                        }
                    }
                    if (String.IsNullOrWhiteSpace(version) && String.IsNullOrWhiteSpace(versionOverride))
                    {
                        foreach (XmlNode node in packageNode.ChildNodes)
                        {
                            if (node.Name == "Version")
                            {
                                version = node.InnerXml;
                            }
                            else if(node.Name == "VersionOverride")
                            {
                                versionOverride = node.InnerXml;
                                break;
                            }
                        }
                    }

                    string versionStr = CheckVersionOverride && GetVersionOverrideEnabled() && !String.IsNullOrWhiteSpace(versionOverride) ? versionOverride : version;
                    if (!String.IsNullOrWhiteSpace(name) && !String.IsNullOrWhiteSpace(versionStr))
                    {
                        packageReferences.Add(new PackageId(name, versionStr));
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
