using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blackduck.Detect.Nuget.Inspector.Model;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution
{
    public class DependencyResult
    {
        public bool Success { get; set; } = true;
        public string ProjectVersion { get; set; } = null;
        public HashSet<PackageSet> Packages { get; set; } = new HashSet<PackageSet>();
        public HashSet<PackageId> Dependencies { get; set; } = new HashSet<PackageId>();
    }
}
