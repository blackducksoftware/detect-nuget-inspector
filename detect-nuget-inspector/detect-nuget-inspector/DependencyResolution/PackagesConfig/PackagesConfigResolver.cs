﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Packaging;
using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Blackduck.Detect.Nuget.Inspector.Inspection.Util;
using Blackduck.Detect.Nuget.Inspector.Model;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.PackagesConfig
{
    class PackagesConfigResolver : DependencyResolver
    {
        private string PackagesConfigPath;
        private NugetSearchService NugetSearchService;
        private String ExcludedDependencyTypes;

        public PackagesConfigResolver(string packagesConfigPath, NugetSearchService nugetSearchService, String excludedDependencyTypes )
        {
            PackagesConfigPath = packagesConfigPath;
            NugetSearchService = nugetSearchService;
            ExcludedDependencyTypes = excludedDependencyTypes;
        }

        public DependencyResult Process()
        {

            List<NugetDependency> dependencies = GetDependencies();

            var result = new DependencyResult();
            result.Packages = CreatePackageSets(dependencies);

            result.Dependencies = new HashSet<PackageId>();
            foreach (var package in result.Packages)
            {
                var anyPackageReferences = result.Packages.Where(pkg => pkg.Dependencies.Contains(package.PackageId)).Any();
                if (!anyPackageReferences)
                {
                    result.Dependencies.Add(package.PackageId);
                }
            }

            return result;
        }

        private List<NugetDependency> GetDependencies()
        {
            System.IO.Stream stream = new System.IO.FileStream(PackagesConfigPath, System.IO.FileMode.Open, System.IO.FileAccess.Read);
            PackagesConfigReader reader = new PackagesConfigReader(stream);
            List<PackageReference> packages = reader.GetPackages().ToList();
            
            bool isDevDependencyTypeExcluded = ExcludedDependencyTypeUtil.isDependencyTypeExcluded(ExcludedDependencyTypes,"DEV");

            var dependencies = new List<NugetDependency>();

            foreach (var packageRef in packages)
            {
                string componentName = packageRef.PackageIdentity.Id;
                var version = packageRef.PackageIdentity.Version;
                var versionRange = new NuGet.Versioning.VersionRange(version, true, version, true);
                var framework = NuGet.Frameworks.NuGetFramework.Parse(packageRef.TargetFramework.Framework);

                bool excludeDevDependency = isDevDependencyTypeExcluded && packageRef.IsDevelopmentDependency;
                
                if (!excludeDevDependency)
                {
                    var dep = new NugetDependency(componentName, versionRange, framework);
                    dependencies.Add(dep);
                }
            }

            return dependencies;
        }

        private HashSet<Model.PackageSet> CreatePackageSets(List<NugetDependency> dependencies)
        {
            try
            {
                var flatResolver = new NugetFlatResolver(NugetSearchService);
                var packages = flatResolver.ProcessAll(dependencies);
                return packages;
            }
            catch (Exception flatException)
            {
                Console.WriteLine("There was an issue processing packages.config as flat: " + flatException.Message);
                try
                {
                    var treeResolver = new NugetTreeResolver(NugetSearchService);
                    treeResolver.AddAll(dependencies);
                    return treeResolver.GetPackageList();
                }
                catch (Exception treeException)
                {
                    Console.WriteLine("There was an issue processing packages.config as a tree: " + treeException.Message);
                    var packages = new HashSet<PackageSet>(dependencies.Select(dependency => dependency.ToEmptyPackageSet()));
                    return packages;
                }
            }
        }
    }
}
