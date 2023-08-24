﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Synopsys.Detect.Nuget.Inspector.Model;

namespace Synopsys.Detect.Nuget.Inspector.Inspection.Inspectors
{
    interface PackageReferenceLoader
    {
        HashSet<PackageId> Process();
    }
}
