using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synopsys.Detect.Nuget.Inspector.Model
{
    public class InspectionOutput
    {
        public string Name = "Nuget Inspector Inspection Result";
        public string Version = "1.0.0";
        public List<Container> Containers = new List<Container>();

    }
}
