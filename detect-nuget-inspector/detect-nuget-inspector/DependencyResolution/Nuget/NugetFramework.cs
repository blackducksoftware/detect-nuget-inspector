﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SCA.Detect.Nuget.Inspector.DependencyResolution.Nuget
{
    public class NugetFramework
    {
        public string Identifier;
        public int Major;
        public int Minor;

        public NugetFramework(string id, int major, int minor)
        {
            Identifier = id;
            Major = major;
            Minor = minor;
        }
    }
}
