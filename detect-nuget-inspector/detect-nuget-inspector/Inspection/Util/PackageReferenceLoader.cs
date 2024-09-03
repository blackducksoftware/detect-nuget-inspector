using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Blackduck.Detect.Nuget.Inspector.Model;

namespace Blackduck.Detect.Nuget.Inspector.Inspection.Inspectors
{
    interface PackageReferenceLoader
    {
        HashSet<PackageId> Process();
    }
}
