using Newtonsoft.Json.Linq;
using NuGet.LibraryModel;
using NuGet.ProjectModel;

namespace Synopsys.Detect.Nuget.Inspector.DependencyResolution.Project
{
    public class ProjectJsonResolver : DependencyResolver
    {
        private string ProjectName;
        private string ProjectJsonPath;

        public ProjectJsonResolver(string projectName, string projectJsonPath)
        {
            ProjectName = projectName;
            ProjectJsonPath = projectJsonPath;
        }

        public DependencyResult Process()
        {
            ProjectJsonResolver projectJsonResolver = new ProjectJsonResolver(ProjectName, ProjectJsonPath);
            var result = new DependencyResult();

            JObject packageSpecDependencies = projectJsonResolver.ExtractPackageSpecDependencies(ProjectJsonPath, projectJsonResolver);
            PackageSpec packageSpec = projectJsonResolver.CreatePackageSpecFromJson(ProjectName, packageSpecDependencies);

            IList<LibraryDependency> packages = packageSpec.Dependencies;

            foreach (LibraryDependency package in packages)
            {
                var set = new Model.PackageSet();
                set.PackageId = new Model.PackageId(package.Name, package.LibraryRange.VersionRange.OriginalString);
                result.Packages.Add(set);
                result.Dependencies.Add(set.PackageId);
            }
            return result;
        }

        public JObject ExtractPackageSpecDependencies(string projectJsonPath, ProjectJsonResolver projectJsonResolver)
        {
            const string TargetKey = "dependencies";
            JObject packageSpecDependencies = new JObject();
            
            JObject jsonObject = JObject.Parse(File.ReadAllText(projectJsonPath));

            JObject dependenciesObject = projectJsonResolver.FindDependencies(jsonObject, TargetKey);
            
            if (dependenciesObject is null)
            {
                throw new NullReferenceException($"In project.json file, '{TargetKey}' object is  not found.");
            }
            else
            {
                packageSpecDependencies.Add(TargetKey, dependenciesObject);
                return packageSpecDependencies;
            }
        }

        public PackageSpec CreatePackageSpecFromJson(string projectName, JObject packageSpecJsonObject)
        {
            string tempFilePath = Path.GetTempFileName();
            
            File.WriteAllText(tempFilePath, packageSpecJsonObject.ToString());

            PackageSpec packageSpec = JsonPackageSpecReader.GetPackageSpec(projectName, tempFilePath);

            File.Delete(tempFilePath);

            return packageSpec;
        }
        
        public JObject FindDependencies(JObject jsonObject, string targetKey)
        {
            foreach (var property in jsonObject.Properties())
            {
                if (property.Name.Equals(targetKey, StringComparison.OrdinalIgnoreCase) && property.Value is JObject)
                {
                    return (JObject)property.Value;
                }

                if (property.Value is JObject nestedObject)
                {
                    var result = FindDependencies(nestedObject, targetKey);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }
            return null;
        }
    }
}