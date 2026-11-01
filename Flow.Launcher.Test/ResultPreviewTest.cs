using System;
using System.Linq;
using System.Reflection;
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
    }
}
