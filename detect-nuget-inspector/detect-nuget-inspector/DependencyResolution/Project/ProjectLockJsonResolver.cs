using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project
{
    class ProjectLockJsonResolver : DependencyResolver
    {
        private string ProjectLockJsonPath;
        private string ExcludedDependencyTypes;

        public ProjectLockJsonResolver(string projectLockJsonPath, string excludedDependencyTypes)
        {
            ProjectLockJsonPath = projectLockJsonPath;
            ExcludedDependencyTypes = excludedDependencyTypes;
        }

        public DependencyResult Process()
        {

            NuGet.ProjectModel.LockFile lockFile = NuGet.ProjectModel.LockFileUtilities.GetLockFile(ProjectLockJsonPath, null);

            var resolver = new NugetLockFileResolver(lockFile, ExcludedDependencyTypes);

            return resolver.Process();
        }

    }
}
