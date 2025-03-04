﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blackduck.Detect.Nuget.Inspector.Inspection.Util
{
    public static class SupportedProjectPatterns
    {
        public static List<String> AsList = new List<String> { 
            //C#
            "*.csproj",
            //F#
            "*.fsproj",
            //VB
            "*.vbproj",
            //Azure Stream Analytics
            "*.asaproj",
            //Docker Compose
            "*.dcproj",
            //Shared Projects
            "*.shproj",
            //Cloud Computing
            "*.ccproj",
            //Fabric Application
            "*.sfproj",
            //Node.js
            "*.njsproj",
            //VC++
            "*.vcxproj",
            //VC++
            "*.vcproj",
            //.NET Core
            "*.xproj",
            //Python
            "*.pyproj",
            //Hive
            "*.hiveproj",
            //Pig
            "*.pigproj",
            //JavaScript
            "*.jsproj",
            //U-SQL
            "*.usqlproj",
            //Deployment
            "*.deployproj",
            //Common Project System Files
            "*.msbuildproj",
            //SQL
            "*.sqlproj",
            //SQL Project Files
            "*.dbproj",
            //RStudio
            "*.rproj"
        };

    }
}
