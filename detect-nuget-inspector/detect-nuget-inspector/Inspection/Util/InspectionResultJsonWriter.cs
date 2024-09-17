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

    }
}
