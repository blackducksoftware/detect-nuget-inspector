using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blackduck.Detect.Nuget.Inspector.Model
{
    public class PackageSet
    {
        public PackageId PackageId;
        public HashSet<PackageId> Dependencies = new HashSet<PackageId>();
    }
}
