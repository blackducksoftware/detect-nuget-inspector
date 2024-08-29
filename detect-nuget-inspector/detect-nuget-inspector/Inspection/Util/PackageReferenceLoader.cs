using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SCA.Detect.Nuget.Inspector.Model;

namespace SCA.Detect.Nuget.Inspector.Inspection.Inspectors
{
    interface PackageReferenceLoader
    {
        HashSet<PackageId> Process();
    }
}
