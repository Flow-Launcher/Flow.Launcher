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

        private sealed class JsonStoragePayload
        {
            public string Value { get; set; } = string.Empty;
        }
    }
}
