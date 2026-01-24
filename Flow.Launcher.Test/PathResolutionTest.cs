using System;
using System.IO;
using Xunit;
using Flow.Launcher.Infrastructure;

namespace Flow.Launcher.Test
{
    public class PathResolutionTest
    {
        [Fact]
        public void ResolveAbsolutePath_WithAbsolutePath_ReturnsOriginalPath()
        {
            // Arrange
            var absolutePath = @"C:\Program Files\Python\python.exe";

            // Act
            var result = Constant.ResolveAbsolutePath(absolutePath);

            // Assert
            Assert.Equal(absolutePath, result);
        }

        [Fact]
        public void ResolveAbsolutePath_WithRelativePath_ResolvesToProgramDirectory()
        {
            // Arrange
            var relativePath = @".\runtimes\python\pythonw.exe";

            // Act
            var result = Constant.ResolveAbsolutePath(relativePath);

            // Assert
            Assert.True(Path.IsPathRooted(result), "Result should be an absolute path");
            Assert.Contains(Constant.ProgramDirectory, result);
            Assert.EndsWith(@"runtimes\python\pythonw.exe", result);
        }

        [Fact]
        public void ResolveAbsolutePath_WithDotDotPath_ResolvesCorrectly()
        {
            // Arrange
            var relativePath = @"..\runtimes\node\node.exe";

            // Act
            var result = Constant.ResolveAbsolutePath(relativePath);

            // Assert
            Assert.True(Path.IsPathRooted(result), "Result should be an absolute path");
        }

        [Fact]
        public void ResolveAbsolutePath_WithNullPath_ReturnsNull()
        {
            // Arrange
            string nullPath = null;

            // Act
            var result = Constant.ResolveAbsolutePath(nullPath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ResolveAbsolutePath_WithEmptyPath_ReturnsEmpty()
        {
            // Arrange
            var emptyPath = string.Empty;

            // Act
            var result = Constant.ResolveAbsolutePath(emptyPath);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void ResolveAbsolutePath_WithForwardSlashes_ResolvesCorrectly()
        {
            // Arrange
            var relativePath = @"./runtimes/python/pythonw.exe";

            // Act
            var result = Constant.ResolveAbsolutePath(relativePath);

            // Assert
            Assert.True(Path.IsPathRooted(result), "Result should be an absolute path");
            Assert.Contains(Constant.ProgramDirectory, result);
        }

        [Fact]
        public void ResolveAbsolutePath_WithUNCPath_ReturnsOriginalPath()
        {
            // Arrange
            var uncPath = @"\\server\share\python\pythonw.exe";

            // Act
            var result = Constant.ResolveAbsolutePath(uncPath);

            // Assert
            Assert.Equal(uncPath, result);
        }

        [Fact]
        public void ConvertToRelativePathIfPossible_WithPathInProgramDirectory_ReturnsRelativePath()
        {
            // Arrange
            var absolutePath = Path.Combine(Constant.ProgramDirectory, "runtimes", "python", "pythonw.exe");

            // Act
            var result = Constant.ConvertToRelativePathIfPossible(absolutePath);

            // Assert
            Assert.True(result.StartsWith(".\\"), "Result should start with .\\");
            Assert.Contains("runtimes", result);
            Assert.Contains("python", result);
        }

        [Fact]
        public void ConvertToRelativePathIfPossible_WithPathOutsideProgramDirectory_ReturnsAbsolutePath()
        {
            // Arrange
            var absolutePath = @"C:\Python\python.exe";

            // Act
            var result = Constant.ConvertToRelativePathIfPossible(absolutePath);

            // Assert
            Assert.Equal(absolutePath, result);
        }

        [Fact]
        public void ConvertToRelativePathIfPossible_WithRelativePath_ReturnsOriginalPath()
        {
            // Arrange
            var relativePath = @".\runtimes\python\pythonw.exe";

            // Act
            var result = Constant.ConvertToRelativePathIfPossible(relativePath);

            // Assert
            Assert.Equal(relativePath, result);
        }

        [Fact]
        public void ConvertToRelativePathIfPossible_WithNullPath_ReturnsNull()
        {
            // Arrange
            string nullPath = null;

            // Act
            var result = Constant.ConvertToRelativePathIfPossible(nullPath);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void ConvertToRelativePathIfPossible_WithEmptyPath_ReturnsEmpty()
        {
            // Arrange
            var emptyPath = string.Empty;

            // Act
            var result = Constant.ConvertToRelativePathIfPossible(emptyPath);

            // Assert
            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void RoundTripTest_RelativePathResolutionAndConversion()
        {
            // Arrange
            var originalRelative = @".\runtimes\python\pythonw.exe";
            
            // Act - Resolve to absolute
            var absolute = Constant.ResolveAbsolutePath(originalRelative);
            // Convert back to relative
            var backToRelative = Constant.ConvertToRelativePathIfPossible(absolute);
            
            // Assert
            Assert.True(Path.IsPathRooted(absolute), "Resolved path should be absolute");
            Assert.True(backToRelative.StartsWith(".\\"), "Converted path should be relative");
            Assert.Contains("runtimes\\python\\pythonw.exe", backToRelative);
        }
    }
}
