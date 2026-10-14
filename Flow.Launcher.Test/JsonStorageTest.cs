using System;
using System.IO;
using System.Threading.Tasks;
using Flow.Launcher.Infrastructure.Storage;
using NUnit.Framework;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class JsonStorageTest
    {
        [Test]
        public async Task SaveAsync_WhenExistingTempFileIsLonger_OverwritesWithoutTrailingBytesAsync()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"json-storage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var filePath = Path.Combine(tempDir, "settings.json");
                await File.WriteAllTextAsync($"{filePath}.tmp", new string('x', 4096));

                var storage = new JsonStorage<JsonStoragePayload>(filePath);
                var data = await storage.LoadAsync();
                data.Value = "ok";

                await storage.SaveAsync();

                var reloaded = await new JsonStorage<JsonStoragePayload>(filePath).LoadAsync();

                Assert.That(reloaded.Value, Is.EqualTo("ok"));
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public async Task LoadAsync_WhenOnlyBackupExists_RestoresBackupAsync()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"json-storage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var filePath = Path.Combine(tempDir, "settings.json");
                var backupPath = $"{filePath}.bak";
                await File.WriteAllTextAsync(backupPath, "{\"Value\":\"from-backup\"}");

                var data = await new JsonStorage<JsonStoragePayload>(filePath).LoadAsync();

                Assert.That(data.Value, Is.EqualTo("from-backup"));
                Assert.That(File.Exists(filePath), Is.True);
                Assert.That(File.Exists(backupPath), Is.False);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public async Task LoadAsync_WhenBackupIsInvalidJson_ReturnsDefaultAndKeepsBackupAsync()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"json-storage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var filePath = Path.Combine(tempDir, "settings.json");
                var backupPath = $"{filePath}.bak";
                await File.WriteAllTextAsync(backupPath, "{");

                var data = await new JsonStorage<JsonStoragePayload>(filePath).LoadAsync();

                Assert.That(data.Value, Is.EqualTo(string.Empty));
                Assert.That(File.Exists(backupPath), Is.True);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public async Task LoadAsync_WhenFileNonExistent_StillReturnsBackupDataAsync()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"json-storage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var filePath = Path.Combine(tempDir, "settings.json");
                var backupPath = $"{filePath}.bak";
                await File.WriteAllTextAsync(backupPath, "{\"Value\":\"from-backup\"}");

                var storage = new JsonStorage<JsonStoragePayload>(filePath);
                using var lockOnBackup = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read);

                var data = await storage.LoadAsync();

                Assert.That(data.Value, Is.EqualTo("from-backup"));
                Assert.That(File.Exists(backupPath), Is.True);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        [TestCase("")]
        [TestCase("{")]
        [TestCase("null")]
        public async Task LoadAsync_WhenFileIsUnusableAndBackupExists_ReplacesFileWithBackupAsync(string unusableContent)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), $"json-storage-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDir);

            try
            {
                var filePath = Path.Combine(tempDir, "settings.json");
                var backupPath = $"{filePath}.bak";
                await File.WriteAllTextAsync(filePath, unusableContent);
                await File.WriteAllTextAsync(backupPath, "{\"Value\":\"from-backup\"}");

                var data = await new JsonStorage<JsonStoragePayload>(filePath).LoadAsync();
                var reloaded = await new JsonStorage<JsonStoragePayload>(filePath).LoadAsync();

                Assert.That(data.Value, Is.EqualTo("from-backup"));
                Assert.That(reloaded.Value, Is.EqualTo("from-backup"));
                Assert.That(File.Exists(backupPath), Is.False);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }

        private sealed class JsonStoragePayload
        {
            public string Value { get; set; } = string.Empty;
        }
    }
}
