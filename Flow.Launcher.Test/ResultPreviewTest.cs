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

        // Preview types that must stay out of JSON, for example ones holding a WPF control.
        // They have no registration on purpose and are dropped when a result is serialized.
        private static readonly Type[] JsonExcludedPreviewTypes = [];

        [Test]
        public void GivenEveryPreviewType_WhenInspectingDerivedTypes_ThenOnlyJsonSafeTypesAreRegistered()
        {
            var registrations = typeof(ResultPreview)
                .GetCustomAttributes<JsonDerivedTypeAttribute>()
                .ToList();
            var registeredTypes = registrations.Select(registration => registration.DerivedType).ToList();
            var discriminators = registrations.Select(registration => registration.TypeDiscriminator).ToList();
            var jsonSafeTypes = ConcretePreviewTypes.Except(JsonExcludedPreviewTypes).ToList();

            // Without a registration a preview type cannot be deserialized from a plugin response.
            // If that is intended, the type belongs in JsonExcludedPreviewTypes instead.
            ClassicAssert.IsTrue(
                jsonSafeTypes.All(registeredTypes.Contains),
                "Register every JSON-safe preview type with JsonDerivedType, or exclude it explicitly");
            ClassicAssert.AreEqual(jsonSafeTypes.Count, registeredTypes.Count);

            // Each discriminator has to be unique for the serializer to map it back to one type
            ClassicAssert.AreEqual(registeredTypes.Count, discriminators.Distinct().Count());
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

        [Test]
        public void GivenTextBlockWithoutEmphasis_WhenSerialized_ThenEmphasisIsNormal()
        {
            var json = JsonSerializer.Serialize<PreviewContentBlock>(new TextPreviewBlock { Text = "Line" });
            using var document = JsonDocument.Parse(json);

            var emphasis = document.RootElement.GetProperty(nameof(TextPreviewBlock.Emphasis)).GetString();
            ClassicAssert.AreEqual("normal", emphasis);
        }

        [Test]
        public void GivenMutedTextBlock_WhenSerializedAndDeserialized_ThenEmphasisIsKept()
        {
            var block = new TextPreviewBlock { Text = "Details", Emphasis = PreviewTextEmphasis.Muted };

            var json = JsonSerializer.Serialize<PreviewContentBlock>(block);
            using var document = JsonDocument.Parse(json);
            var emphasis = document.RootElement.GetProperty(nameof(TextPreviewBlock.Emphasis)).GetString();
            ClassicAssert.AreEqual("muted", emphasis);

            var roundTripped = JsonSerializer.Deserialize<PreviewContentBlock>(json);
            ClassicAssert.AreEqual(PreviewTextEmphasis.Muted, ((TextPreviewBlock)roundTripped).Emphasis);
        }
    }
}
