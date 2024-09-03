using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blackduck.Detect.Nuget.Inspector.Inspection.Inspectors
{
    class SolutionInspectionOptions : InspectionOptions
    {
        public SolutionInspectionOptions() { }

        public SolutionInspectionOptions(InspectionOptions old)
        {
            this.TargetPath = old.TargetPath;
            this.Verbose = old.Verbose;
            this.PackagesRepoUrl = old.PackagesRepoUrl;
            this.OutputDirectory = old.OutputDirectory;
            this.ExcludedModules = old.ExcludedModules;
            this.IncludedModules = old.IncludedModules;
            this.IgnoreFailure = old.IgnoreFailure;
            this.ExcludedDependencyTypes = old.ExcludedDependencyTypes;
        }

        public string SolutionName { get; set; }
    }
}
