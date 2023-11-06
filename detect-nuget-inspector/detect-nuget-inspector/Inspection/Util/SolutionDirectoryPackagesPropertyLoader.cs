using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Synopsys.Detect.Nuget.Inspector.Model;

namespace Synopsys.Detect.Nuget.Inspector.Inspection.Inspectors;

public class SolutionDirectoryPackagesPropertyLoader : PackageReferenceLoader
{

    private String PropertyPath;
    private NugetSearchService NugetSearchService;

    public SolutionDirectoryPackagesPropertyLoader(String propertyPath, NugetSearchService nugetSearchService)
    {
        PropertyPath = propertyPath;
        NugetSearchService = nugetSearchService;
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
            XmlNodeList packageVersionNodes = doc.GetElementsByTagName("PackageVersion");

            if (packageVersionNodes != null && packageVersionNodes.Count > 0)
            {
                GetPackageVersions(packageVersionNodes, packageReferences);
            }

            XmlNodeList globalPackageReferenceNodes = doc.GetElementsByTagName("GlobalPackageReference");

            if (globalPackageReferenceNodes != null && globalPackageReferenceNodes.Count > 0)
            {
                GetPackageVersions(globalPackageReferenceNodes,packageReferences);
            } 
        }
        else
        {
            Console.WriteLine("The user has disabled Central Package Management. Will skip parsing over this file ");
        }

        return packageReferences;
    }

    private bool CheckCentralPackageManagementEnabled(XmlNodeList manageCentralPackageVersion)
    {
        return manageCentralPackageVersion != null && manageCentralPackageVersion.Count > 0 &&
               manageCentralPackageVersion.Item(0).InnerXml == "true";
    }

    private void GetPackageVersions(XmlNodeList packageVersionNodes, HashSet<PackageId> packageReferences)
    {
        foreach (XmlNode packageVersionNode in packageVersionNodes)
        {
            if (packageVersionNode.NodeType != XmlNodeType.Comment)
            {
                XmlAttributeCollection attributes = packageVersionNode.Attributes;
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

                    if (!String.IsNullOrWhiteSpace(packageName) && !String.IsNullOrWhiteSpace(packageVersion))
                    {
                        packageReferences.Add(new PackageId(packageName, packageVersion));
                    }
                }
            }
        }
    }
}