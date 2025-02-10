using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;

namespace Blackduck.Detect.Nuget.Inspector.Inspection.Inspectors
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
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            XmlDocument doc = new XmlDocument();
            doc.Load(PropertyPath);
            
            Microsoft.Build.Evaluation.Project proj = new Microsoft.Build.Evaluation.Project(PropertyPath);
            string projectAssestsJsonPath = proj.GetPropertyValue("ProjectAssetsFile");
            
            if (!String.IsNullOrWhiteSpace(projectAssestsJsonPath))
            {    
                Microsoft.Build.Evaluation.ProjectCollection.GlobalProjectCollection.UnloadProject(proj);    
                return projectAssestsJsonPath;
            }

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
