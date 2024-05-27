using System;
using System.Collections.Generic;
using Flow.Launcher.Core.Resource;
using Flow.Launcher.Plugin;

namespace Flow.Launcher.SettingPages.ViewModels;

public class DropdownDataGeneric<TValue> : BaseModel where TValue : Enum
{
    public string Display { get; set; }
    public TValue Value { get; private init; }
    private string LocalizationKey { get; init; }

    public static List<TR> GetValues<TR>(string keyPrefix) where TR : DropdownDataGeneric<TValue>, new()
    {
        var data = new List<TR>();
        var enumValues = (TValue[])Enum.GetValues(typeof(TValue));

        foreach (var value in enumValues)
        {
            var key = keyPrefix + value;
            var display = InternationalizationManager.Instance.GetTranslation(key);
            data.Add(new TR { Display = display, Value = value, LocalizationKey = key });
        }

        return data;
    }

    public static void UpdateLabels<TR>(List<TR> options) where TR : DropdownDataGeneric<TValue>
    {
        foreach (var item in options)
        {
            item.Display = InternationalizationManager.Instance.GetTranslation(item.LocalizationKey);
        }
    }
}
