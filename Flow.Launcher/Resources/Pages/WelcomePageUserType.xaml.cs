using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Flow.Launcher.Helper;
using Flow.Launcher.Infrastructure.Hotkey;
using Flow.Launcher.Infrastructure.UserSettings;
using System.Windows.Navigation;
using CommunityToolkit.Mvvm.Input;
using Flow.Launcher.ViewModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.DependencyInjection;
using Flow.Launcher.Core.Plugin;
using Flow.Launcher.Infrastructure;
using System.IO;
using System.Text.Json;

namespace Flow.Launcher.Resources.Pages;

public class RadioCardData
{
    public string Key { get; set; }
    public string Icon { get; set; }
    public string Description1 { get; set; }
    public string Bullet1 { get; set; }
    public string Bullet2 { get; set; }
    public string Bullet3 { get; set; }
}
public partial class WelcomePageUserType
{
    public Settings Settings { get; set; }
    
    public WelcomePageUserType()
    {
        InitializeComponent();
    }
    
    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        Settings = Ioc.Default.GetRequiredService<Settings>();
        // Sometimes the navigation is not triggered by button click,
        // so we need to reset the page number
        Ioc.Default.GetRequiredService<WelcomeViewModel>().PageNum = 3;
        InitializeComponent();
    }
    
    private void OnStyleChecked(object sender, RoutedEventArgs e)
    {
        var rb = sender as RadioButton;
        if (rb?.Tag is not RadioCardData data) return;
        
        var settings = Ioc.Default.GetRequiredService<Settings>();
        
        var pluginDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins", "Flow.Launcher.Plugin.Explorer");
        var explorerSettingsPath = Path.Combine(pluginDirectory, "Settings.json");

        if (File.Exists(explorerSettingsPath))
        {
            var explorerSettingsJson = File.ReadAllText(explorerSettingsPath);
            var explorerSettings = JsonSerializer.Deserialize<Dictionary<string, object>>(explorerSettingsJson);

            switch (data.Key)
            {
                case "CLI":
                    if (explorerSettings != null)
                        explorerSettings["UseLocationAsWorkingDir"] = false;
                    settings.AutoCompleteHotkey = "Tab";
                    break;

                case "GUI":
                    if (explorerSettings != null)
                        explorerSettings["UseLocationAsWorkingDir"] = true;
                    settings.AutoCompleteHotkey = $"{KeyConstant.Alt} + Right";
                    break;
            }
            
            File.WriteAllText(explorerSettingsPath, JsonSerializer.Serialize(explorerSettings, new JsonSerializerOptions { WriteIndented = true }));
        }

        settings.Save();
    }
}

