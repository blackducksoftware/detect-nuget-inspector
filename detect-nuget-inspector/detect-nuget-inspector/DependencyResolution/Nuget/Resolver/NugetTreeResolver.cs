﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging.Core;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget
{
    public class NugetTreeResolver
    {

        private NugetSearchService nuget;
        private Model.PackageSetBuilder builder = new Model.PackageSetBuilder();

        public NugetTreeResolver(NugetSearchService service)
        {
            nuget = service;
        }

        public HashSet<Model.PackageSet> GetPackageList()
        {
            return builder.GetPackageList();
        }

        public void AddAll(List<NugetDependency> packages)
        {
            foreach (NugetDependency package in packages)
            {
                Add(package);
            }
        }

        public void Add(NugetDependency packageDependency)
        {
            var package = nuget.FindBestPackage(packageDependency.Name, packageDependency.VersionRange);
            if (package == null)
            {
                var version = packageDependency.VersionRange.MinVersion.ToNormalizedString();
                Console.WriteLine($"Nuget was unable to find the package '{packageDependency.Name}' with version range '{packageDependency.VersionRange}', assuming it is using version '{version}'");
                builder.AddOrUpdatePackage(new Model.PackageId(packageDependency.Name, version));
                return;
            }

            var packageId = new Model.PackageId(packageDependency.Name, package.Identity.Version.ToNormalizedString());
            HashSet<Model.PackageId> dependencies = new HashSet<Model.PackageId>();

            var packages = nuget.DependenciesForPackage(package.Identity, packageDependency.Framework);

            foreach (PackageDependency dependency in packages)
            {
                var bestExisting = builder.GetBestVersion(dependency.Id, dependency.VersionRange);
                if (bestExisting != null)
                {
                    var id = new Model.PackageId(dependency.Id, bestExisting);
                    dependencies.Add(id);
                }
                else
                {
                    var depPackage = nuget.FindBestPackage(dependency.Id, dependency.VersionRange);
                    if (depPackage == null)
                    {
                        Console.WriteLine($"Unable to find package for '{dependency.Id}' version '{dependency.VersionRange}'");
                        continue;
                    }

                    var id = new Model.PackageId(depPackage.Identity.Id, depPackage.Identity.Version.ToNormalizedString());
                    dependencies.Add(id);

                    if (!builder.DoesPackageExist(id))
                    {
                        Add(new NugetDependency(dependency.Id, dependency.VersionRange, packageDependency.Framework));
                    }
                }
            }


            builder.AddOrUpdatePackage(packageId, dependencies);

        }
    }
}
