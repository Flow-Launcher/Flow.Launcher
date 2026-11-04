using Flow.Launcher.Core.ExternalPlugins.PythonDependencies;
using NUnit.Framework;

namespace Flow.Launcher.Test.PythonDependencies
{
    [TestFixture]
    public class DependencyRequirementsTest
    {
        [TestCase("requests==2.32.3")]
        [TestCase("requests===2.32.3")]
        [TestCase("Requests[socks]==2.32.3")]
        [TestCase("  requests == 2.32.3 ")]
        [TestCase("pywin32==306; sys_platform == \"win32\"")]
        [TestCase("  Requests[socks] == 2.32.3 ; python_version >= \"3.8\" ")]
        public void IsPinned_ExactVersion_ReturnsTrue(string requirement)
        {
            Assert.That(DependencyRequirements.IsPinned(requirement), Is.True);
        }

        [TestCase("requests")]
        [TestCase("requests>=2.31")]
        [TestCase("requests~=2.31")]
        [TestCase("requests==2.*")]
        [TestCase("requests==2.32.3,<3")]
        [TestCase("requests!=2.32.3")]
        [TestCase("requests @ git+https://github.com/psf/requests.git")]
        [TestCase("git+https://github.com/psf/requests.git")]
        [TestCase("./local_pkg")]
        [TestCase("requests==")]
        public void IsPinned_NotExact_ReturnsFalse(string requirement)
        {
            Assert.That(DependencyRequirements.IsPinned(requirement), Is.False);
        }

        [Test]
        public void Normalize_TrimsDropsBlankAndSortsOrdinal()
        {
            var normalized = DependencyRequirements.Normalize(new[] { " b==1 ", "", null, "A==2", "a==3" });

            Assert.That(normalized, Is.EqualTo(new[] { "A==2", "a==3", "b==1" }));
        }

        [Test]
        public void FindUnpinned_ReturnsOnlyUnpinned()
        {
            var unpinned = DependencyRequirements.FindUnpinned(new[] { "x==1", "y>=2", " z " });

            Assert.That(unpinned, Is.EqualTo(new[] { "y>=2", "z" }));
        }
    }
}
