using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCA.Detect.Nuget.Inspector.Inspection.Util
{
    static class PathUtil
    {
        public static string Combine(List<string> pathSegments)
        {
            return Combine(pathSegments.ToArray());
        }

        public static string Combine(params string[] pathSegments)
        {
            String path = Path.Combine(pathSegments);
            return path.Replace("\\", "/");
        }

        public static string Sanitize(String path)
        {
            return path.Replace("\\", "/");
        }
    }
}
