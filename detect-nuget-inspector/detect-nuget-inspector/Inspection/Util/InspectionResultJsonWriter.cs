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

        public void WriteInspectedFiles(String inspectedFilesInfoRelativePath)
        {
            if (string.IsNullOrWhiteSpace(inspectedFilesInfoRelativePath))
            {
                Console.WriteLine("No inspected files info path provided; skipping writing inspected files information.");
                return;
            }
            Console.WriteLine("Writing inspected files information...");
            var extractionDir = Result.OutputDirectory;
            var outputDir = Directory.GetParent(extractionDir)?.Parent?.Parent;
            if (extractionDir == null || outputDir == null)
            {
                Console.WriteLine("Could not determine parent output directory from extraction directory: " + extractionDir);
                Console.WriteLine("Inspected files information will not be written.");
                return;
            }

            // Combine outputDir with the provided relative path
            var relevantFilesJsonPath = Path.Combine(outputDir.FullName, inspectedFilesInfoRelativePath);
            var parentDir = Path.GetDirectoryName(relevantFilesJsonPath);
            if (!Directory.Exists(parentDir))
            {
                Console.WriteLine($"Creating directory for inspected files: {parentDir}");
                Directory.CreateDirectory(parentDir);
            }

            Console.WriteLine("About to write inspected files to: " + relevantFilesJsonPath);

            var map = new Dictionary<string, List<string>> { { "NI", GetAllInspectedFiles(Result) } };

            // Serialize and append as a new line
            var jsonLine = JsonConvert.SerializeObject(map);
            File.AppendAllText(relevantFilesJsonPath, jsonLine + "\n");
            Console.WriteLine($"Wrote inspected files map to {relevantFilesJsonPath}");
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
