﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blackduck.Detect.Nuget.Inspector.Inspection.Inspectors
{
    class ProjectInspectionOptions : InspectionOptions
    {
        public ProjectInspectionOptions() { }

        public ProjectInspectionOptions(InspectionOptions old)
        {
            this.TargetPath = old.TargetPath;
            this.Verbose = old.Verbose;
            this.PackagesRepoUrl = old.PackagesRepoUrl;
            this.OutputDirectory = old.OutputDirectory;
            this.ExcludedModules = old.ExcludedModules;
            this.IncludedModules = old.IncludedModules;
            this.IgnoreFailure = old.IgnoreFailure;
            this.ExcludedDependencyTypes = old.ExcludedDependencyTypes;
            this.ArtifactsPath = old.ArtifactsPath;
        }

        public string ProjectName { get; set; }
        public string ProjectUniqueId { get; set; }
        public string ProjectDirectory { get; set; }
        public string VersionName { get; set; }
        public string PackagesConfigPath { get; set; }
        public string ProjectJsonPath { get; set; }
        public string ProjectJsonLockPath { get; set; }
        public string ProjectAssetsJsonPath { get; set; }
        public string DirectoryPackagesPropsPath { get; set; }
    }
}
