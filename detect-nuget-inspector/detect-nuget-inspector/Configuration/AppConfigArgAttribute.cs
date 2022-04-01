using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Synopsys.Detect.Nuget.Inspector.Configuration
{
    class AppConfigArgAttribute : Attribute
    {
        public string Key;
        public AppConfigArgAttribute(string key)
        {
            Key = key;
        }
    }
}
