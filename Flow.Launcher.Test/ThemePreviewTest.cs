using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Moq;
using NUnit.Framework;

namespace Flow.Launcher.Test;

[TestFixture]
[NonParallelizable]
public class ThemePreviewTest
{
    [Test]
    [Repeat(2)]
    public async Task PreviewUsesSelectedThemeRegardlessOfPreviousBorderStyleAsync()
    {
        var application = await GetApplicationAsync();
#pragma warning disable VSTHRD001 // This test uses a WPF dispatcher, not a Visual Studio main thread.
        await application.Dispatcher.InvokeAsync(() => VerifyPreviews(application));
#pragma warning restore VSTHRD001
        Assert.That(application.Dispatcher.HasShutdownStarted, Is.False);
    }

    private static Task<Application> GetApplicationAsync()
    {
        if (Application.Current != null)
            return Task.FromResult(Application.Current);

        var ready = new TaskCompletionSource<Application>(TaskCreationOptions.RunContinuationsAsynchronously);
        // WPF permits only one Application per process. Keep its STA dispatcher alive for reuse.
        var thread = new Thread(() =>
        {
            try
            {
                var application = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                ready.SetResult(application);
                Dispatcher.Run();
            }
            catch (Exception exception)
            {
                ready.TrySetException(exception);
            }
        }) { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return ready.Task;
    }

    private static void VerifyPreviews(Application application)
    {
        var savedResources = application.Resources;
        var savedWindow = application.MainWindow;
        var savedShutdownMode = application.ShutdownMode;
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var portableDirectoryExisted = Directory.Exists(DataLocation.PortableDataPath);
        Directory.CreateDirectory(directory);
        Directory.CreateDirectory(DataLocation.PortableDataPath);
        var window = new Window();

        try
        {
            application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            application.Resources = new ResourceDictionary();
            application.MainWindow = window;
            var settings = new Settings { ColorScheme = "Light" };
            var theme = new Theme(Mock.Of<IPublicAPI>(), settings);
            var directories = (List<string>)typeof(Theme)
                .GetField("_themeDirectories", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(theme);
            directories.Insert(0, directory);
            var colorize = typeof(Theme).GetMethod("ColorizeWindow", BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var name in new[] { "A", "B", "C" })
            {
                var size = name[0] - 'A' + 1;
                File.WriteAllText(Path.Combine(directory, name + ".xaml"), $$"""
                    <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                                        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
                        <Color x:Key="LightBG">#40123456</Color>
                        <Color x:Key="DarkBG">#80765432</Color>
                        <Style x:Key="BaseBorder" TargetType="Border">
                            <Setter Property="Padding" Value="{{size}}" />
                        </Style>
                        <Style x:Key="WindowBorderStyle" TargetType="Border" BasedOn="{StaticResource BaseBorder}">
                            <Setter Property="BorderThickness" Value="{{size}}" />
                            <Setter Property="CornerRadius" Value="{{size * 3}}" />
                            <Setter Property="Margin" Value="{{size * 2}}" />
                            <Setter Property="BorderBrush" Value="Red" />
                            <Setter Property="Background" Value="#40123456" />
                        </Style>
                    </ResourceDictionary>
                    """);
            }

            foreach (var blur in new[] { false, true })
            foreach (var shadow in new[] { false, true })
            foreach (var scheme in new[] { "Light", "Dark" })
            {
                settings.ColorScheme = scheme;
                typeof(Theme).GetProperty(nameof(Theme.BlurEnabled)).SetValue(theme, blur);
                InstallPreviousStyle("C");
                // Includes A -> B -> A and B after A, C, and B itself.
                foreach (var name in new[] { "A", "B", "A", "C", "B", "B" })
                {
                    var previousStyle = application.Resources["WindowBorderStyle"];
                    colorize.Invoke(theme, new object[] { name, BackdropTypes.None });
                    var preview = new Border { Style = (Style)application.Resources["PreviewWindowBorderStyle"] };
                    var size = name[0] - 'A' + 1;
                    Assert.Multiple(() =>
                    {
                        Assert.That(preview.BorderThickness, Is.EqualTo(new Thickness(blur ? 1 : size)), name);
                        Assert.That(preview.CornerRadius, Is.EqualTo(new CornerRadius(blur ? 5 : size * 3)), name);
                        Assert.That(preview.Margin, Is.EqualTo(new Thickness(size * 2)), name);
                        Assert.That(preview.Padding, Is.EqualTo(new Thickness(size)), name);
                        Assert.That(((SolidColorBrush)preview.BorderBrush).Color, Is.EqualTo(Colors.Red), name);
                        Assert.That(preview.Effect, Is.Null, name);
                        Assert.That(application.Resources["WindowBorderStyle"], Is.SameAs(previousStyle));
                        Assert.That(((SolidColorBrush)preview.Background).Color,
                            Is.EqualTo((Color)ColorConverter.ConvertFromString(scheme == "Light" ? "#FF123456" : "#FF765432")), name);
                    });

                    InstallPreviousStyle(name);
                }

                // Model the global style installed after preview generation, including a runtime shadow override.
                void InstallPreviousStyle(string name)
                {
                    var selected = new ResourceDictionary { Source = new Uri(Path.Combine(directory, name + ".xaml")) };
                    var currentStyle = (Style)selected["WindowBorderStyle"];
                    if (shadow)
                    {
                        currentStyle.Setters.Add(new Setter(UIElement.EffectProperty, new DropShadowEffect()));
                        currentStyle.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(32)));
                    }
                    application.Resources["WindowBorderStyle"] = currentStyle;
                }
            }
        }
        finally
        {
            window.Close();
            application.Resources = savedResources;
            application.MainWindow = savedWindow;
            application.ShutdownMode = savedShutdownMode;
            Directory.Delete(directory, true);
            if (!portableDirectoryExisted)
                Directory.Delete(DataLocation.PortableDataPath, true);
        }
    }
}
