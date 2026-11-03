using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
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


            try
            {
                string projectAssestsJsonPath = GetProjectAssetsFileFromMSBuild();
                if (!String.IsNullOrWhiteSpace(projectAssestsJsonPath))
                {
                    return projectAssestsJsonPath;
                }
            }
            catch (IOException ex) when (ProjectInspector.IsMissingMSBuildAssembly(ex))
            {
                Console.WriteLine("MSBuild unavailable; reading props file as XML: " + ex.Message);
            }
            catch (Exception)
            {
                Console.WriteLine("There was an error building the project using API, falling back to XML Parser");
            }

            XmlNodeList projectAssetsFileNodes = doc.GetElementsByTagName("ProjectAssetsFile");
            if (projectAssetsFileNodes != null && projectAssetsFileNodes.Count > 0)
            {
                foreach (XmlNode projectAssetsFileNode in projectAssetsFileNodes)
                {
                    if (projectAssetsFileNode.NodeType != XmlNodeType.Comment)
                    {
                        string propertyDirectory = Path.GetDirectoryName(Path.GetFullPath(PropertyPath)) + Path.DirectorySeparatorChar;
                        return projectAssetsFileNode.InnerText.Replace("$(MSBuildThisFileDirectory)", propertyDirectory, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }
            return null;
        }

        // Kept out of Process so a missing Microsoft.Build assembly is thrown inside its try.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private string GetProjectAssetsFileFromMSBuild()
        {
            Microsoft.Build.Evaluation.Project proj = new Microsoft.Build.Evaluation.Project(PropertyPath);
            string projectAssestsJsonPath = proj.GetPropertyValue("ProjectAssetsFile");
            if (!String.IsNullOrWhiteSpace(projectAssestsJsonPath))
            {
                Microsoft.Build.Evaluation.ProjectCollection.GlobalProjectCollection.UnloadProject(proj);
            }
            return projectAssestsJsonPath;
        }
    }
}
