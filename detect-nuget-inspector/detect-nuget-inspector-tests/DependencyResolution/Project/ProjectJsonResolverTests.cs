using Newtonsoft.Json.Linq;

namespace SCA.Detect.Nuget.Inspector.DependencyResolution.Project.Test;

[TestClass]
public class ProjectJsonResolverTests
{
    public string GetJsonPath(string jsonFileName)
    {
        string projectDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
        string jsonFilePath = Path.Combine(projectDirectory, "Files", jsonFileName);

        return jsonFilePath;
    }

    [TestMethod]
    public void ExtractPackageSpecDependenciesTestForOldFormat()
    {
        string projectJsonPath = GetJsonPath("oldFormat_Project.json");
        var projectJsonResolverForOldFormat = new ProjectJsonResolver("ProjectName", projectJsonPath);
        JObject result =
            projectJsonResolverForOldFormat.ExtractPackageSpecDependencies(projectJsonPath,
                projectJsonResolverForOldFormat);

        Assert.IsNotNull(result);

        JObject dependenciesObject = result.GetValue("dependencies") as JObject;

        Assert.IsNotNull(dependenciesObject);
        Assert.AreEqual(8, dependenciesObject.Count);
        Assert.AreEqual("4.0.1", dependenciesObject.GetValue("System.Xml.XmlDocument").ToString());
        Assert.AreEqual("4.1.0", dependenciesObject.GetValue("System.AppContext").ToString());
    }

    [TestMethod]
    public void ExtractPackageSpecDependenciesTestForNewFormat()
    {
        string projectJsonPath = GetJsonPath("newFormat_Project.json");
        var projectJsonResolverForNewFormat = new ProjectJsonResolver("ProjectName", projectJsonPath);
        JObject result =
            projectJsonResolverForNewFormat.ExtractPackageSpecDependencies(projectJsonPath,
                projectJsonResolverForNewFormat);

        Assert.IsNotNull(result);

        JObject dependenciesObject = result.GetValue("dependencies") as JObject;

        Assert.IsNotNull(dependenciesObject);
        Assert.AreEqual(5, dependenciesObject.Count);
        Assert.AreEqual("1.0.0", dependenciesObject.GetValue("Microsoft.AspNetCore.Server.IISIntegration").ToString());
        Assert.AreEqual("4.1.0", dependenciesObject.GetValue("System.AppContext").ToString());
    }

    [TestMethod]
    [ExpectedException(typeof(NullReferenceException))]
    public void ExtractPackageSpecDependenciesTestWhenDependenciesNotFound()
    {
        string projectJsonPath = GetJsonPath("withoutDependencies_Project.json");
        var projectJsonResolver = new ProjectJsonResolver("ProjectName", projectJsonPath);

        projectJsonResolver.ExtractPackageSpecDependencies(projectJsonPath, projectJsonResolver);
    }

    [TestMethod]
    public void CreatePackageSpecFromJsonTest()
    {
        JObject packageSpecJsonObject = JObject.Parse(@"{
                ""dependencies"": {
                    ""Dependency1"": ""1.2.3"",
                    ""Dependency2"": ""4.5.6""
                  }
        }");

        string fakeJsonPath = GetJsonPath("fake.json");
        File.WriteAllText(fakeJsonPath, "{}");

        ProjectJsonResolver projectJsonResolver = new ProjectJsonResolver("projectName", fakeJsonPath);
        var result = projectJsonResolver.CreatePackageSpecFromJson("projectName", packageSpecJsonObject);

        Assert.AreEqual("1.0.0", result.Version.ToString());
        Assert.AreEqual(2, result.Dependencies.Count);

        Assert.IsTrue(result.Dependencies.Any(dependency => dependency.Name == "Dependency2"));

        File.Delete(fakeJsonPath);
    }

    [TestMethod]
    public void ProcessTestForNewFormat()
    {
        string projectName = "ProjectName";
        string projectJsonPath = GetJsonPath("newFormat_Project.json");
        ProjectJsonResolver projectJsonResolver = new ProjectJsonResolver(projectName, projectJsonPath);

        var result = projectJsonResolver.Process();

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Packages);
        Assert.IsNotNull(result.Dependencies);

        Assert.AreEqual(5, result.Packages.Count);
        Assert.AreEqual(5, result.Dependencies.Count);

        CollectionAssert.Contains(result.Dependencies.Select(dep => dep.Name).ToList(),
            "Microsoft.AspNetCore.Server.Kestrel");
    }

    [TestMethod]
    public void ProcessTestForOldFormat()
    {
        string projectName = "ProjectName";
        string projectJsonPath = GetJsonPath("oldFormat_Project.json");
        ProjectJsonResolver projectJsonResolver = new ProjectJsonResolver(projectName, projectJsonPath);

        var result = projectJsonResolver.Process();

        Assert.IsNotNull(result);
        Assert.IsNotNull(result.Packages);
        Assert.IsNotNull(result.Dependencies);

        Assert.AreEqual(8, result.Packages.Count);
        Assert.AreEqual(8, result.Dependencies.Count);

        CollectionAssert.Contains(result.Dependencies.Select(dep => dep.Name).ToList(),
            "System.Collections.NonGeneric");
    }

    [TestMethod]
    [ExpectedException(typeof(NullReferenceException))]
    public void ProcessTestWhenDependenciesNotFound()
    {
        string projectName = "ProjectName";
        string projectJsonPath = GetJsonPath("withoutDependencies_Project.json");
        ProjectJsonResolver projectJsonResolver = new ProjectJsonResolver(projectName, projectJsonPath);

        var result = projectJsonResolver.Process();
    }
}