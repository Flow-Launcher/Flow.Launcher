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
    }
}
