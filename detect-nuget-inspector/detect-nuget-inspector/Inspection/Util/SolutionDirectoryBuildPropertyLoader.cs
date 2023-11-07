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

        public SolutionDirectoryBuildPropertyLoader(string propertyPath, NugetSearchService nugetSearchService)
        {
            PropertyPath = propertyPath;
            NugetSearchService = nugetSearchService;
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
                    }
                    if (String.IsNullOrWhiteSpace(version))
                    {
                        foreach (XmlNode node in packageNode.ChildNodes)
                        {
                            if (node.Name == "Version")
                            {
                                version = node.InnerXml;
                                break;
                            }
                        }
                    }
                    if (!String.IsNullOrWhiteSpace(name) && !String.IsNullOrWhiteSpace(version))
                    {
                        packageReferences.Add(new PackageId(name, version));
                    }
                }
            }
            return packageReferences;
        }
    }
}
