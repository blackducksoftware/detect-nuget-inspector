using Blackduck.Detect.Nuget.Inspector.DependencyResolution.Nuget;
using Blackduck.Detect.Nuget.Inspector.Inspection;

namespace Blackduck.Detect.Nuget.Inspector.DependencyResolution.Project.Test;

[TestClass]
public class ProjectXmlResolverTest
{
    [TestMethod]
    public void ProcessProjectWithDuplicatePackageReferences()
    {
        string projectDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
        string testProjectPath = Path.Combine(projectDirectory, "Files", "DuplicatePackageReference.csproj");
        
        var nugetSearchService = new NugetSearchService("", "");
        var projectXmlResolver = new ProjectXmlResolver(
            testProjectPath, 
            nugetSearchService, 
            "NONE"
        );

        DependencyResult dependencyResult = projectXmlResolver.Process();

        // Assert that both duplicate package references are captured
        var packageA = dependencyResult.Packages
            .Where(p => p.PackageId.Name == "PackageA")
            .ToList();

        Assert.AreEqual(2, packageA.Count);
        Assert.IsTrue(packageA.Any(p => p.PackageId.Version == "1.0.0"));
        Assert.IsTrue(packageA.Any(p => p.PackageId.Version == "2.0.0"));
    }

}