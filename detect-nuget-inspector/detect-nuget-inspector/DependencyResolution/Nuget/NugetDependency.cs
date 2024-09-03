using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget
{
    public class NugetDependency
    {
        public string Name;
        public VersionRange VersionRange;
        public NuGetFramework Framework = null;

        public NugetDependency(string name, VersionRange versionRange, NuGetFramework framework = null)
        {
            Name = name;
            VersionRange = versionRange;
            Framework = framework;
        }

        public NugetDependency(PackageDependency dependency)
        {
            Name = dependency.Id;
            VersionRange = dependency.VersionRange;
        }

        public Model.PackageSet ToEmptyPackageSet()
        {
            var packageSet = new Model.PackageSet();
            packageSet.PackageId = new Model.PackageId(Name, VersionRange.MinVersion.ToNormalizedString());
            return packageSet;
        }
    }
}
