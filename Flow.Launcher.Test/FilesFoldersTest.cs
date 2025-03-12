using Flow.Launcher.Plugin.SharedCommands;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    
    public class FilesFoldersTest
    {
        // Testcases from https://stackoverflow.com/a/31941905/20703207
        // Disk
        [TestCase(@"c:", @"c:\foo", true)]
        [TestCase(@"c:\", @"c:\foo", true)]
        // Slash
        [TestCase(@"c:\foo\bar\", @"c:\foo\", false)]
        [TestCase(@"c:\foo\bar", @"c:\foo\", false)]
        [TestCase(@"c:\foo", @"c:\foo\bar", true)]
        [TestCase(@"c:\foo\", @"c:\foo\bar", true)]
        // File
        [TestCase(@"c:\foo", @"c:\foo\a.txt", true)]
        [TestCase(@"c:\foo", @"c:/foo/a.txt", true)]
        [TestCase(@"c:\FOO\a.txt", @"c:\foo", false)]
        [TestCase(@"c:\foo\a.txt", @"c:\foo\", false)]
        [TestCase(@"c:\foobar\a.txt", @"c:\foo", false)]
        [TestCase(@"c:\foobar\a.txt", @"c:\foo\", false)]
        [TestCase(@"c:\foo\", @"c:\foo.txt", false)]
        // Prefix
        [TestCase(@"c:\foo", @"c:\foobar", false)]
        [TestCase(@"C:\Program", @"C:\Program Files\", false)]
        [TestCase(@"c:\foobar", @"c:\foo\a.txt", false)]
        [TestCase(@"c:\foobar\", @"c:\foo\a.txt", false)]
        // Edge case
        [TestCase(@"c:\foo", @"c:\foo\..\bar\baz", false)]
        [TestCase(@"c:\bar", @"c:\foo\..\bar\baz", true)]
        [TestCase(@"c:\barr", @"c:\foo\..\bar\baz", false)]
        public void GivenTwoPaths_WhenCheckPathContains_ThenShouldBeExpectedResult(string parentPath, string path, bool expectedResult)
        {
            ClassicAssert.AreEqual(expectedResult, FilesFolders.PathContains(parentPath, path));
        }

        // Equality
        [TestCase(@"c:\foo", @"c:\foo", false)]
        [TestCase(@"c:\foo\", @"c:\foo", false)]
        [TestCase(@"c:\foo", @"c:\foo\", false)]
        [TestCase(@"c:\foo", @"c:\foo", true)]
        [TestCase(@"c:\foo\", @"c:\foo", true)]
        [TestCase(@"c:\foo", @"c:\foo\", true)]
        public void GivenTwoPathsAreTheSame_WhenCheckPathContains_ThenShouldBeExpectedResult(string parentPath, string path, bool expectedResult)
        {
            ClassicAssert.AreEqual(expectedResult, FilesFolders.PathContains(parentPath, path, allowEqual: expectedResult));
        }
    }
}
