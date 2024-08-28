using System.Security.Policy;
using Blackduck.Detect.Nuget.Inspector.Inspection.Inspectors;
using Blackduck.Detect.Nuget.Inspector.Model;

namespace detect_nuget_inspector_tests.Inspection.Util
{
    [TestClass]
    public class SolutionDirectoryPackagesPropertyLoaderTests
    {
        private string GetFilePath(string fileName)
        {
            string projectDirectory = Directory.GetParent(Environment.CurrentDirectory).Parent.Parent.FullName;
            string filePath = Path.Combine(projectDirectory, "Files", fileName);

            return filePath;
        }
        
        [TestMethod]
        public void ParseStandardDirectoryPackagesPropsFile()
        {
            string propertyPath = GetFilePath("Standard_Directory.Packages.props"); 
            
            var solutionDirectoryPackagesPropertyLoader =
                new SolutionDirectoryPackagesPropertyLoader(GetFilePath(propertyPath), "NONE");

            HashSet<PackageId> packageVersions = solutionDirectoryPackagesPropertyLoader.Process();
            bool versionOverrideEnabled = solutionDirectoryPackagesPropertyLoader.GetVersionOverrideEnabled();

            Assert.IsNotNull(packageVersions);
            Assert.AreEqual(22, packageVersions.Count);
            Assert.AreEqual(true,versionOverrideEnabled);
            Assert.AreEqual("2.88.6",packageVersions.First(pkg => pkg.Name.Equals("SkiaSharp.Views.Uno.WinUI")).Version);
            Assert.AreEqual("4.6.0",packageVersions.First(pkg => pkg.Name.Equals("Microsoft.CodeAnalysis.CSharp")).Version);
            Assert.AreEqual("1.1.2-beta1.23357.1",packageVersions.First(pkg => pkg.Name.Equals("Microsoft.CodeAnalysis.CSharp.CodeFix.Testing.XUnit")).Version);
        }

        [TestMethod]
        public void ParseDirectoryPackagesPropsFileWithCpmDisabled()
        {
            string propertyPath = GetFilePath("CPM_Disabled_Directory.Packages.props");
            
            var solutionDirectoryPackagesPropertyLoader =
                new SolutionDirectoryPackagesPropertyLoader(GetFilePath(propertyPath),"NONE");
            
            StringWriter stringWriter = new StringWriter();
            Console.SetOut(stringWriter);
            
            HashSet<PackageId> packageVersions = solutionDirectoryPackagesPropertyLoader.Process();
            
            Assert.AreEqual(0, packageVersions.Count);
            Assert.AreEqual( "The user has disabled Central Package Management. Will skip parsing over this file\n",stringWriter.ToString());
        }

        [TestMethod]
        public void ParseDirectoryPackagesPropsFileWithVersionOverrideDisabled()
        {
            string propertyPath = GetFilePath("VersionOverride_Disabled_Directory.Packages.props");
            
            var solutionDirectoryPackagesPropertyLoader =
                new SolutionDirectoryPackagesPropertyLoader(GetFilePath(propertyPath), "NONE");
            
            bool versionOverrideEnabled = solutionDirectoryPackagesPropertyLoader.GetVersionOverrideEnabled();
            
            Assert.AreEqual(false, versionOverrideEnabled);
        }
        
        [TestMethod]
        public void ParseDirectoryPackagesPropsFileWithGlobalPackageReferences()
        {
            string propertyPath = GetFilePath("GlobalPackageReference_Directory.Packages.props");
            
            var solutionDirectoryPackagesPropertyLoader =
                new SolutionDirectoryPackagesPropertyLoader(GetFilePath(propertyPath), "NONE");

            HashSet<PackageId> packageVersions = solutionDirectoryPackagesPropertyLoader.Process();

            HashSet<PackageId> globalPackageReferences =
                solutionDirectoryPackagesPropertyLoader.GetGlobalPackageReferences();
            
            Assert.AreEqual(19,packageVersions.Count);
            Assert.AreEqual(7,globalPackageReferences.Count);
            Assert.AreEqual("1.2.0.507",globalPackageReferences.First(pkg => pkg.Name.Equals("StyleCop.Analyzers.Unstable")).Version);
            Assert.AreEqual("1.1.1",globalPackageReferences.First(pkg => pkg.Name.Equals("Microsoft.SourceLink.GitHub")).Version);
        }
    }
}