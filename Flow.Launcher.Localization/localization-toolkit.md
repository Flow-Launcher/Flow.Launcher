The Localization Toolkit helps Flow Launcher C# plugin developers make the localization process easier.

## Getting Started

For C# plugins, install and reference [Flow.Launcher.Localization](www.nuget.org/packages/Flow.Launcher.Localization) via NuGet.

## Build Properties

These are properties you can configure in your `.csproj` file to customize the localization process. You can set them in the `<PropertyGroup>` section. For example, to set the `FLLUseDependencyInjection` property to `true`, add the following lines:

```xml
<PropertyGroup>
    <FLLUseDependencyInjection>true</FLLUseDependencyInjection>
</PropertyGroup>
```

### `FLLUseDependencyInjection`

This flag specifies whether to use dependency injection to obtain an `IPublicAPI` instance. The default is `false`.
- If set to `false`, the Main class (which must implement **[IPlugin](/API-Reference/Flow.Launcher.Plugin/IPlugin.md)** or **[IAsyncPlugin](/API-Reference/Flow.Launcher.Plugin/IAsyncPlugin.md)**)
  must have a [PluginInitContext](/API-Reference/Flow.Launcher.Plugin/PluginInitContext.md) property that is at least `internal static`.
- If set to `true`, you can access the `IPublicAPI` instance via `PublicApi.Instance` using dependency injection, and the Main class does not need to include a [PluginInitContext](/API-Reference/Flow.Launcher.Plugin/PluginInitContext.md) property.
  (Note: This approach is not recommended for plugin projects at the moment since it limits compatibility to Flow Launcher 1.20.0 or later.)

## Usage

### Main Class

The Main class must implement [IPluginI18n](/API-Reference/Flow.Launcher.Plugin/IPluginI18n.md).

If `FLLUseDependencyInjection` is `false`, include a [PluginInitContext](/API-Reference/Flow.Launcher.Plugin/PluginInitContext.md) property, for example:

```csharp
 // Must implement IPluginI18n
public class Main : IPlugin, IPluginI18n
{
    // Must be at least internal static
    internal static PluginInitContext Context { get; private set; } = null!;
}
```

### Localized Strings

You can simplify your code by replacing calls like:
```csharp
Context.API.GetTranslation("flowlauncher_plugin_localization_demo_plugin_name")
```
with:
```csharp
Localize.flowlauncher_plugin_localization_demo_plugin_name()
```

If your localization string uses variables, it becomes even simpler! From this:
```csharp
string.Format(Context.API.GetTranslation("flowlauncher_plugin_localization_demo_value_with_keys"), firstName, lastName);
```
To this:
```csharp
Localize.flowlauncher_plugin_localization_demo_value_with_keys(firstName, lastName);
```

### Localized Enums

For enum types (e.g., `DemoEnum`) that need localization in UI controls such as combo boxes, use the `EnumLocalize` attribute to enable localization. For each enum field:
- Use `EnumLocalizeKey` to provide a custom localization key.
- Use `EnumLocalizeValue` to provide a constant localization string.

Example:

```csharp
[EnumLocalize] // Enable localization support
public enum DemoEnum
{
    // Specific localization key
    [EnumLocalizeKey("localize_key_1")]
    Value1,

    // Specific localization value
    [EnumLocalizeValue("This is my enum value localization")]
    Value2,

    // Key takes precedence if both are present
    [EnumLocalizeKey("localize_key_3")]
    [EnumLocalizeValue("Localization Value")]
    Value3,

    // Using the Localize class. This way, you can't misspell localization keys, and if you rename
    // them in your .xaml file, you won't forget to rename them here as well because the build will fail.
    [EnumLocalizeKey(nameof(Localize.flowlauncher_plugin_localization_demo_plugin_description))]
    Value4,
}
```

Then, use the generated `DemoEnumLocalized` class within your view model to bind to a combo box control:

```csharp
// ComboBox ItemSource
public List<DemoEnumLocalized> AllDemoEnums { get; } = DemoEnumLocalized.GetValues();

// ComboBox SelectedValue
public DemoEnum SelectedDemoEnum { get; set; }
```

In your XAML, bind as follows:

```xml
<ComboBox
    DisplayMemberPath="Display"
    ItemsSource="{Binding AllDemoEnums}"
    SelectedValue="{Binding SelectedDemoEnum}"
    SelectedValuePath="Value" />
```

To update localization strings when the language changes, you can call:

```csharp
DemoEnumLocalize.UpdateLabels(AllDemoEnums);
```
