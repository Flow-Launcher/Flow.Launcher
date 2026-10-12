using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Flow.Launcher.Avalonia.ViewModel.SettingPages;

public partial class DropdownDataGeneric<T> : ObservableObject where T : struct, Enum
{
    private readonly string _keyPrefix;
    private readonly Func<T, string> _getData;

    [ObservableProperty]
    private string _display = string.Empty;

    public T Value { get; }

    public DropdownDataGeneric(T value, string keyPrefix, Func<T, string> getData)
    {
        Value = value;
        _keyPrefix = keyPrefix;
        _getData = getData;
        UpdateLabels();
    }

    public void UpdateLabels()
    {
        var key = _keyPrefix + _getData(Value);
        Display = App.API?.GetTranslation(key) ?? key;
    }

    public static List<DropdownDataGeneric<T>> GetEnumData(string keyPrefix, Func<T, string>? getData = null)
    {
        getData ??= (v => v.ToString());
        return Enum.GetValues<T>()
            .Select(v => new DropdownDataGeneric<T>(v, keyPrefix, getData))
            .ToList();
    }
}

public static class DropdownDataGeneric
{
    public static List<DropdownDataGeneric<T>> GetEnumData<T>(string keyPrefix, Func<T, string>? getData = null) where T : struct, Enum
    {
        return DropdownDataGeneric<T>.GetEnumData(keyPrefix, getData);
    }
}

