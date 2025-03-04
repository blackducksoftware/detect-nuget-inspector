﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Blackduck.Detect.Nuget.Inspector.Configuration
{
    class CommandLineArgAttribute : Attribute
    {
        public string Key;
        public string Description;
        public CommandLineArgAttribute(string key, string description = "")
        {
            Key = key;
            Description = description;
        }
    }
}
