﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blackduck.Detect.Nuget.Inspector.Inspection
{
    public class InspectionOptions
    {
        public string TargetPath { get; set; } = "";
        public bool Verbose { get; set; } = false;
        public string PackagesRepoUrl { get; set; } = "";
        public string NugetConfigPath { get; set; } = "";
        public string OutputDirectory { get; set; } = "";
        public string ExcludedModules { get; set; } = "";
        public string IncludedModules { get; set; } = "";
        public bool IgnoreFailure { get; set; } = false;
        public string ExcludedDependencyTypes { get; set; } = "NONE";
        public string ArtifactsPath { get; set; } = "";
    }
}
