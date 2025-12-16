using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blackduck.Detect.Nuget.Inspector.Model;
using Newtonsoft.Json;

namespace Blackduck.Detect.Nuget.Inspector.Inspection.Util
{
    class InspectionResultJsonWriter
    {
        private InspectionResult Result;
        private InspectionOutput InspectionOutput;

        public InspectionResultJsonWriter(InspectionResult result)
        {
            Result = result;
            InspectionOutput = new InspectionOutput();
            InspectionOutput.Containers = result.Containers;
        }

        public string FilePath()
        {
            return PathUtil.Combine(Result.OutputDirectory, Result.ResultName + "_inspection.json");
        }

        public void Write()
        {
            Write(Result.OutputDirectory);
        }

        public void Write(string outputDirectory)
        {
            Write(outputDirectory, FilePath());
        }

        public void Write(string outputDirectory, string outputFilePath)
        {

            if (outputDirectory == null)
            {
                Console.WriteLine("Could not create output directory: " + outputDirectory);
            }
            else
            {
                Console.WriteLine("Creating output directory: " + outputDirectory);
                Directory.CreateDirectory(outputDirectory);
            }

            Console.WriteLine("Creating output file path: " + outputFilePath);
            using (var fs = new FileStream(outputFilePath, FileMode.Create))
            {
                using (var sw = new StreamWriter(fs))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    serializer.NullValueHandling = NullValueHandling.Ignore;
                    JsonTextWriter writer = new JsonTextWriter(sw);
                    serializer.Formatting = Formatting.Indented;
                    serializer.Serialize(writer, InspectionOutput);
                }
            }
        }

        public void WriteInspectedFiles()
        {
            // Get the location of invokedDetectorsAndTheirRelevantFiles.json from outputDirectory, refactor later
            var extractionDir = Result.OutputDirectory;
            var outputDir = Directory.GetParent(extractionDir)?.Parent?.Parent;
            if (extractionDir == null)
            {
                Console.WriteLine("Could not determine scan directory from extraction directory: " + extractionDir);
                return;
            }
            
            // Build the path to the target file
            var relevantFilesJsonFilePath = Path.Combine(outputDir.FullName, "scan", "quack", "invokedDetectorsAndTheirRelevantFiles.json");

            // Create the map
            var map = new Dictionary<string, List<string>> { { "NI", GetAllInspectedFiles(Result) } };

            // Serialize and append as a new line
            var jsonLine = JsonConvert.SerializeObject(map);
            File.AppendAllText(relevantFilesJsonFilePath, jsonLine + "\n");
            Console.WriteLine($"Appended inspected files map to {relevantFilesJsonFilePath}");
        
        }
        
        public static List<string> GetAllInspectedFiles(InspectionResult result)
        {
            return result.Containers
                .SelectMany(container => container.InspectedFiles)
                .Distinct()
                .ToList();
        }

    }
}
