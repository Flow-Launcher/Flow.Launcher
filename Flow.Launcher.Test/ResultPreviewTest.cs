using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flow.Launcher.Plugin;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test
{
    [TestFixture]
    public class ResultPreviewTest
    {
        private static readonly Type[] ConcretePreviewTypes = typeof(ResultPreview).Assembly
            .GetTypes()
            .Where(type => typeof(ResultPreview).IsAssignableFrom(type) && !type.IsAbstract)
            .ToArray();

        [Test]
        public void GivenEveryPreviewType_WhenInspectingDerivedTypes_ThenEachIsRegisteredForDeserialization()
        {
            var registrations = typeof(ResultPreview)
                .GetCustomAttributes<JsonDerivedTypeAttribute>()
                .ToList();
            var registeredTypes = registrations.Select(registration => registration.DerivedType).ToList();
            var discriminators = registrations.Select(registration => registration.TypeDiscriminator).ToList();

            // Without a registration a preview type cannot be deserialized from a plugin response
            ClassicAssert.IsTrue(
                ConcretePreviewTypes.All(registeredTypes.Contains),
                "Every preview type needs a JsonDerivedType registration");
            ClassicAssert.AreEqual(ConcretePreviewTypes.Length, registeredTypes.Count);

            // Each discriminator has to be unique for the serializer to map it back to one type
            ClassicAssert.AreEqual(ConcretePreviewTypes.Length, discriminators.Distinct().Count());
        }

        // Stands in for a preview type that is only usable inside Flow, such as a WPF control
        private sealed record UnregisteredPreview : ResultPreview
        {
            internal override ResultPreview ClonePreview() => this;
        }

        [Test]
        public void GivenRegisteredPreview_WhenSerialized_ThenTypeAndContentAreWritten()
        {
            var result = new Result
            {
                PreviewDefinition = new BlocksPreview
                {
                    ContentBlocks = [new TextPreviewBlock { Text = "Line" }]
                }
            };

            var json = JsonSerializer.Serialize(result);
            using var document = JsonDocument.Parse(json);
            var preview = document.RootElement.GetProperty(nameof(Result.PreviewDefinition));

            ClassicAssert.AreEqual("blocks", preview.GetProperty("type").GetString());
            ClassicAssert.AreEqual(
                "Line",
                preview.GetProperty(nameof(BlocksPreview.ContentBlocks))[0].GetProperty(nameof(TextPreviewBlock.Text)).GetString());
        }

        [Test]
        public void GivenUnregisteredPreview_WhenSerialized_ThenPreviewIsNull()
        {
            var result = new Result
            {
                Title = "Unserializable preview",
                PreviewDefinition = new UnregisteredPreview()
            };

            var json = JsonSerializer.Serialize(result);
            using var document = JsonDocument.Parse(json);
            var preview = document.RootElement.GetProperty(nameof(Result.PreviewDefinition));

            ClassicAssert.AreEqual(JsonValueKind.Null, preview.ValueKind);
        }
    }
}
