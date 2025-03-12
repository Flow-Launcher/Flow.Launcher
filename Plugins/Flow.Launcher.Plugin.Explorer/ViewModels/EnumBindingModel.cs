using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Flow.Launcher.Plugin.Explorer.ViewModels;

public class EnumBindingModel<T> where T : struct, Enum
{
    public static IReadOnlyList<EnumBindingModel<T>> CreateList()
    {
        return Enum.GetValues<T>()
            .Select(value => new EnumBindingModel<T>
            {
                Value = value, LocalizationKey = GetDescriptionAttr(value)
            })
            .ToArray();
    }

    public EnumBindingModel<T> From(T value)
    {
        var name = value.ToString();
        var description = GetDescriptionAttr(value);
        
        return new EnumBindingModel<T>
        {
            Name = name,
            LocalizationKey = description,
            Value = value
        };
    }

    private static string GetDescriptionAttr(T source)
    {
        var fi = source.GetType().GetField(source.ToString());

        var attributes = (DescriptionAttribute[])fi?.GetCustomAttributes(
            typeof(DescriptionAttribute), false);

        return attributes is { Length: > 0 } ? attributes[0].Description : source.ToString();

    }
    
    public string Name { get; set; }
    private string LocalizationKey { get; set; }
    public string Description => Main.Context.API.GetTranslation(LocalizationKey);
    public T Value { get; set; }
}