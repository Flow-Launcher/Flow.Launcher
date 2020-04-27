using Flow.Launcher.Plugin.Program.Programs;
using NUnit.Framework;
using System;
using Windows.ApplicationModel;

namespace Flow.Launcher.Test.Plugins
{
    [TestFixture]
    public class ProgramTest
    {
        [TestCase("Microsoft.WindowsCamera", "ms-resource:LensSDK/Resources/AppTitle", "ms-resource://Microsoft.WindowsCamera/LensSDK/Resources/AppTitle")]
        [TestCase("microsoft.windowscommunicationsapps", "ms-resource://microsoft.windowscommunicationsapps/hxoutlookintl/AppManifest_MailDesktop_DisplayName",
            "ms-resource://microsoft.windowscommunicationsapps/hxoutlookintl/AppManifest_MailDesktop_DisplayName")]
        [TestCase("windows.immersivecontrolpanel", "ms-resource:DisplayName", "ms-resource://windows.immersivecontrolpanel/Resources/DisplayName")]
        [TestCase("Microsoft.MSPaint", "ms-resource:AppName", "ms-resource://Microsoft.MSPaint/Resources/AppName")]
        public void WhenGivenPriReferenceValueShouldReturnCorrectFormat(string packageName, string rawPriReferenceValue, string expectedFormat)
        {
            // Arrange
            var app = new UWP.Application();

            // Act
            var result = app.FormattedPriReferenceValue(packageName, rawPriReferenceValue);

            // Assert
            Assert.IsTrue(result == expectedFormat, 
                $"Expected Pri reference format: {expectedFormat}{Environment.NewLine} " +
                $"Actual: {result}{Environment.NewLine}");
        }
    }
}
