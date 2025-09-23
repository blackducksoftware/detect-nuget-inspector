[TestClass]
public class SolutionInspectorTests
{
    private string _tempRootDirectory;

    [TestInitialize]
    public void Setup()
    {
        // Create temporary root directory
        _tempRootDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempRootDirectory);

        // Create root-level Directory.Packages.props
        File.WriteAllText(Path.Combine(_tempRootDirectory, "Directory.Packages.props"), 
            @"<Project>
                <PropertyGroup>
                    <ManagementType>CentralPackageManagement</ManagementType>
                </PropertyGroup>
            </Project>");

        // Create Solution1
        string solution1Path = Path.Combine(_tempRootDirectory, "Solution1");
        Directory.CreateDirectory(solution1Path);
        File.WriteAllText(Path.Combine(solution1Path, "Directory.Packages.props"), 
            @"<Project>
                <PropertyGroup>
                    <SolutionSpecificManagement>Enabled</SolutionSpecificManagement>
                </PropertyGroup>
            </Project>");

        // Create Project1
        string project1Path = Path.Combine(solution1Path, "Project1");
        Directory.CreateDirectory(project1Path);
        File.WriteAllText(Path.Combine(project1Path, "Project1.csproj"), 
            @"<Project Sdk=""Microsoft.NET.Sdk"">
                <PropertyGroup>
                    <TargetFramework>net6.0</TargetFramework>
                </PropertyGroup>
            </Project>");

        // Create Solution2
        string solution2Path = Path.Combine(_tempRootDirectory, "Solution2");
        Directory.CreateDirectory(solution2Path);

        // Create Project2
        string project2Path = Path.Combine(solution2Path, "Project2");
        Directory.CreateDirectory(project2Path);
        File.WriteAllText(Path.Combine(project2Path, "Project2.csproj"), 
            @"<Project Sdk=""Microsoft.NET.Sdk"">
                <PropertyGroup>
                    <TargetFramework>net6.0</TargetFramework>
                </PropertyGroup>
            </Project>");
    }

    [TestMethod]
    public void InspectDirectory_WithComplexStructure_DiscoversAllProjects()
    {
        // Arrange
        var options = new InspectionOptions 
        { 
            TargetPath = _tempRootDirectory,
            Verbose = true
        };
        var nugetService = new Mock<NugetSearchService>().Object;

        // Act
        var inspectorDispatch = new InspectorDispatch();
        var inspectors = inspectorDispatch.CreateInspectors(options, nugetService);

        // Assert
        Assert.IsNotNull(inspectors);
        Assert.AreEqual(2, inspectors.Count, 
            "Expected two project inspectors to be created");

        // Verify project paths
        var projectPaths = inspectors.Select(i => 
            ((ProjectInspectionOptions)i.GetType()
                .GetProperty("Options")
                .GetValue(i)).TargetPath).ToList();

        Assert.IsTrue(projectPaths.Any(p => p.Contains("Project1.csproj")));
        Assert.IsTrue(projectPaths.Any(p => p.Contains("Project2.csproj")));
    }

    [TestCleanup]
    public void Cleanup()
    {
        // Remove temporary directory
        if (Directory.Exists(_tempRootDirectory))
        {
            Directory.Delete(_tempRootDirectory, true);
        }
    }
}
