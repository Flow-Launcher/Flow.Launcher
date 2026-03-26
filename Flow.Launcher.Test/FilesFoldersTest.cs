using Flow.Launcher.Plugin.SharedCommands;
using NUnit.Framework;
using NUnit.Framework.Legacy;
using System.IO;

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

        [Test]
        public void TryDeleteDirectoryRobust_WhenDirectoryDoesNotExist_ReturnsTrue()
        {
            // Arrange
            string nonExistentPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            // Act
            bool result = FilesFolders.TryDeleteDirectoryRobust(nonExistentPath);

            // Assert
            ClassicAssert.IsTrue(result);
        }

        [Test]
        public void TryDeleteDirectoryRobust_WhenDirectoryIsEmpty_DeletesSuccessfully()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            // Act
            bool result = FilesFolders.TryDeleteDirectoryRobust(tempDir);

            // Assert
            ClassicAssert.IsTrue(result);
            ClassicAssert.IsFalse(Directory.Exists(tempDir));
        }

        [Test]
        public void TryDeleteDirectoryRobust_WhenDirectoryHasFiles_DeletesSuccessfully()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            File.WriteAllText(Path.Combine(tempDir, "test.txt"), "test content");

            // Act
            bool result = FilesFolders.TryDeleteDirectoryRobust(tempDir);

            // Assert
            ClassicAssert.IsTrue(result);
            ClassicAssert.IsFalse(Directory.Exists(tempDir));
        }

        [Test]
        public void TryDeleteDirectoryRobust_WhenDirectoryHasNestedStructure_DeletesSuccessfully()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            string subDir1 = Path.Combine(tempDir, "SubDir1");
            string subDir2 = Path.Combine(tempDir, "SubDir2");
            Directory.CreateDirectory(subDir1);
            Directory.CreateDirectory(subDir2);
            File.WriteAllText(Path.Combine(subDir1, "file1.txt"), "content1");
            File.WriteAllText(Path.Combine(subDir2, "file2.txt"), "content2");
            File.WriteAllText(Path.Combine(tempDir, "root.txt"), "root content");

            // Act
            bool result = FilesFolders.TryDeleteDirectoryRobust(tempDir);

            // Assert
            ClassicAssert.IsTrue(result);
            ClassicAssert.IsFalse(Directory.Exists(tempDir));
        }

        [Test]
        public void TryDeleteDirectoryRobust_WhenFileIsReadOnly_RemovesAttributeAndDeletes()
        {
            // Arrange
            string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);
            string filePath = Path.Combine(tempDir, "readonly.txt");
            File.WriteAllText(filePath, "readonly content");
            File.SetAttributes(filePath, FileAttributes.ReadOnly);

            // Act
            bool result = FilesFolders.TryDeleteDirectoryRobust(tempDir);

            // Assert
            ClassicAssert.IsTrue(result);
            ClassicAssert.IsFalse(Directory.Exists(tempDir));
        }
    }
}
