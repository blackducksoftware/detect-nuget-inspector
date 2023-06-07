using Newtonsoft.Json.Linq;
using Synopsys.Detect.Nuget.Inspector.DependencyResolution.Project;

namespace detect_nuget_inspector_tests.DependencyResolution.Project;

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
        JObject result = projectJsonResolverForOldFormat.ExtractPackageSpecDependencies(projectJsonPath, projectJsonResolverForOldFormat);

        Assert.IsNotNull(result);
        Assert.IsTrue(result.ContainsKey("dependencies"));

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
        JObject result = projectJsonResolverForNewFormat.ExtractPackageSpecDependencies(projectJsonPath, projectJsonResolverForNewFormat);
        
        Assert.IsNotNull(result);
        Assert.IsTrue(result.ContainsKey("dependencies"));

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
}