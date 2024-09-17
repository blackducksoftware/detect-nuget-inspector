using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project
{
    class ProjectAssetsJsonResolver : DependencyResolver
    {
        private string ProjectAssetsJsonPath;

        public ProjectAssetsJsonResolver(string projectAssetsJsonPath)
        {
            ProjectAssetsJsonPath = projectAssetsJsonPath;
        }

        public DependencyResult Process()
        {

            NuGet.ProjectModel.LockFile lockFile = NuGet.ProjectModel.LockFileUtilities.GetLockFile(ProjectAssetsJsonPath, null);

            var resolver = new NugetLockFileResolver(lockFile);

            return resolver.Process();
        }

    }
}
