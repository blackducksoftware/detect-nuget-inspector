namespace Synopsys.Detect.Nuget.Inspector.DependencyResolution.Nuget.Test
{
    [TestClass]
    public class NugetLockFileResolverTest
    {
        [TestMethod]
        public void ParseProjectFileDependencyGroupTestWithMinInclusiveOnly()
        {
            NugetLockFileResolver lockFileResolver = new NugetLockFileResolver(null);
            var lockFileDependency = "Newtonsoft.Json >= 13.0.1";

            var dependency = lockFileResolver.ParseProjectFileDependencyGroup(lockFileDependency);
            Assert.IsNotNull(dependency);

        }

        [TestMethod]
        public void ParseProjectFileDependencyGroupTestWithMinExclusiveOnly()
        {
            NugetLockFileResolver lockFileResolver = new NugetLockFileResolver(null);
            var lockFileDependency = "Newtonsoft.Json > 13.0.1";

            var dependency = lockFileResolver.ParseProjectFileDependencyGroup(lockFileDependency);
            Assert.IsNotNull(dependency);

        }

        [TestMethod]
        public void ParseProjectFileDependencyGroupTestWithSameRange()
        {
            NugetLockFileResolver lockFileResolver = new NugetLockFileResolver(null);
            var lockFileDependency = "Newtonsoft.Json >= 13.0.1 <= 13.0.1";

            var dependency = lockFileResolver.ParseProjectFileDependencyGroup(lockFileDependency);
            Assert.IsNotNull(dependency);

        }

        [TestMethod]
        public void ParseProjectFileDependencyGroupTestWithMinAndMaxExclusiveRange()
        {
            NugetLockFileResolver lockFileResolver = new NugetLockFileResolver(null);
            var lockFileDependency = "Newtonsoft.Json >= 12.0.1 < 13.0.1";

            var dependency = lockFileResolver.ParseProjectFileDependencyGroup(lockFileDependency);
            Assert.IsNotNull(dependency);

        }

        [TestMethod]
        public void ParseProjectFileDependencyGroupTestWithMinExclusiveAndMaxInclusiveRange()
        {
            NugetLockFileResolver lockFileResolver = new NugetLockFileResolver(null);
            var lockFileDependency = "Newtonsoft.Json > 12.0.1 <= 13.0.1";

            var dependency = lockFileResolver.ParseProjectFileDependencyGroup(lockFileDependency);
            Assert.IsNotNull(dependency);

        }

        [TestMethod]
        public void ParseProjectFileDependencyGroupTestWithMinExclusiveAndMaxExclusiveRange()
        {
            NugetLockFileResolver lockFileResolver = new NugetLockFileResolver(null);
            var lockFileDependency = "Newtonsoft.Json > 12.0.1 < 13.0.1";

            var dependency = lockFileResolver.ParseProjectFileDependencyGroup(lockFileDependency);
            Assert.IsNotNull(dependency);

        }

        [TestMethod]
        public void ParseProjectFileDependencyGroupTestWithMaxInclusiveOnly()
        {
            NugetLockFileResolver lockFileResolver = new NugetLockFileResolver(null);
            var lockFileDependency = "Newtonsoft.Json <= 13.0.1";

            var dependency = lockFileResolver.ParseProjectFileDependencyGroup(lockFileDependency);
            Assert.IsNotNull(dependency);

        }

        [TestMethod]
        public void ParseProjectFileDependencyGroupTestWithMaxExclusiveOnly()
        {
            NugetLockFileResolver lockFileResolver = new NugetLockFileResolver(null);
            var lockFileDependency = "Newtonsoft.Json < 13.0.1";

            var dependency = lockFileResolver.ParseProjectFileDependencyGroup(lockFileDependency);
            Assert.IsNotNull(dependency);

        }


    }
}
