using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SCA.Detect.Nuget.Inspector.DependencyResolution.Nuget;

namespace SCA.Detect.Nuget.Inspector.DependencyResolution.Project
{
    class ProjectLockJsonResolver : DependencyResolver
    {
        private string ProjectLockJsonPath;

        public ProjectLockJsonResolver(string projectLockJsonPath)
        {
            ProjectLockJsonPath = projectLockJsonPath;
        }

        public DependencyResult Process()
        {

            NuGet.ProjectModel.LockFile lockFile = NuGet.ProjectModel.LockFileUtilities.GetLockFile(ProjectLockJsonPath, null);

            var resolver = new NugetLockFileResolver(lockFile);

            return resolver.Process();
        }

    }
}
