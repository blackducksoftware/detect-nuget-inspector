using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static SCA.Detect.Nuget.Inspector.Inspection.Util.AssemblyInfoVersionParser;
using System.Xml;

namespace SCA.Detect.Nuget.Inspector.Inspection.Util
{
    class InspectorUtil
    {
        public const string DEFAULT_OUTPUT_DIRECTORY = "blackduck";

        public static string GetProjectAssemblyVersion(string projectDirectory)
        {
            try
            {
                List<AssemblyVersionResult> results = Directory.GetFiles(projectDirectory, "*AssemblyInfo.*", SearchOption.AllDirectories).ToList()
                    .Select(path => {
                        if (!path.EndsWith(".obj"))
                        {
                            if (File.Exists(path))
                            {
                                return AssemblyInfoVersionParser.ParseVersion(path);
                            }
                        }
                        return null;
                    })
                    .Where(it => it != null)
                    .ToList();

                AssemblyVersionResult selected = null;
                if (results.Any(it => it.confidence == ConfidenceLevel.HIGH))
                {
                    selected = results.First(it => it.confidence == ConfidenceLevel.HIGH);
                }
                else if (results.Any(it => it.confidence == ConfidenceLevel.MEDIUM))
                {
                    selected = results.First(it => it.confidence == ConfidenceLevel.MEDIUM);
                }
                else if (results.Count > 0)
                {
                    selected = results.First();
                }

                if (selected != null)
                {
                    Console.WriteLine($"Selected version '{selected.version}' from '{selected.path}'.");
                    return selected.version;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to find version for project directory: " + projectDirectory);
                Console.WriteLine("The issue was: " + e.Message);
            }

            return null;
        }
        
        public static string GetAttributeInformation(XmlAttributeCollection attributes, string checkString, XmlNode package)
        {
            string attributeStr = null;
            
            XmlAttribute attribute = attributes[checkString];

            if (attribute == null)
            {
                foreach (XmlNode node in package.ChildNodes)
                {
                    if (node.Name == checkString)
                    {
                        attributeStr = node.InnerXml;
                        break;
                    }
                }
            }
            else
            {
                attributeStr = attribute.Value;
            }
            
            return attributeStr;
        }
    }
}
