using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget;

namespace Synopsys.Detect.Nuget.Inspector.Inspection.Inspectors
{
    class ProjectNugetgPropertyLoader : PropertyLoader
    {

        private string PropertyPath;
        private NugetSearchService NugetSearchService;

        public ProjectNugetgPropertyLoader(string propertyPath, NugetSearchService nugetSearchService)
        {
            PropertyPath = propertyPath;
            NugetSearchService = nugetSearchService;
        }

        public string Process()
        {
            var tree = new NugetTreeResolver(NugetSearchService);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            XmlDocument doc = new XmlDocument();
            doc.Load(PropertyPath);

            XmlNodeList projectAssetsFileNodes = doc.GetElementsByTagName("ProjectAssetsFile");
            if (projectAssetsFileNodes != null && projectAssetsFileNodes.Count > 0)
            {
                foreach (XmlNode projectAssetsFileNode in projectAssetsFileNodes)
                {
                    if (projectAssetsFileNode.NodeType != XmlNodeType.Comment)
                    {
                        return projectAssetsFileNode.InnerText;
                    }
                }
            }
            return null;
        }
    }
}
