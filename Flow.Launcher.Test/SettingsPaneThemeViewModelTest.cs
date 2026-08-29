using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Infrastructure.UserSettings;
using Flow.Launcher.Plugin;
using Flow.Launcher.SettingPages.ViewModels;
using Moq;
using NUnit.Framework;
using NUnit.Framework.Legacy;

namespace Flow.Launcher.Test;

[TestFixture]
internal class SettingsPaneThemeViewModelTest
{
    [Test]
    [Apartment(ApartmentState.STA)]
    public void GivenCustomizedSearchWindowSize_WhenThemeIsReset_ThenSizeDefaultsAndNotificationsAreRestored()
    {
        _ = Application.Current ?? new Application();
        var settings = new Settings
        {
            WindowSize = 720,
            MaxResultsToShow = 8
        };
        var changedProperties = new List<string>();
        settings.PropertyChanged += (_, args) => changedProperties.Add(args.PropertyName);
        var theme = new Theme(Mock.Of<IPublicAPI>(), settings);
        var viewModel = CreateViewModel(settings, theme);

        viewModel.Reset();

        ClassicAssert.AreEqual(580, settings.WindowSize);
        ClassicAssert.AreEqual(5, settings.MaxResultsToShow);
        CollectionAssert.AreEquivalent(
            new[] { nameof(Settings.WindowSize), nameof(Settings.MaxResultsToShow) },
            changedProperties);
    }

    private static SettingsPaneThemeViewModel CreateViewModel(Settings settings, Theme theme)
    {
        var viewModel = (SettingsPaneThemeViewModel)RuntimeHelpers.GetUninitializedObject(
            typeof(SettingsPaneThemeViewModel));
        typeof(SettingsPaneThemeViewModel)
            .GetField("<Settings>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(viewModel, settings);
        typeof(SettingsPaneThemeViewModel)
            .GetField("_theme", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(viewModel, theme);
        typeof(SettingsPaneThemeViewModel)
            .GetField("DefaultFont", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(viewModel, settings.QueryBoxFont);
        return viewModel;
    }
}
