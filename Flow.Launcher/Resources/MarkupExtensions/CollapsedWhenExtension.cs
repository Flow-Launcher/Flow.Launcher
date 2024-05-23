using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace Flow.Launcher.Resources.MarkupExtensions;

#nullable enable

public class CollapsedWhenExtension : MarkupExtension {
    private Binding? When { get; set; }
    public object? IsEqualTo { get; set; }

    public bool? IsEqualToBool
    {
        get => IsEqualTo switch
        {
            bool b => b,
            _ => null
        };
        set => IsEqualTo = value;
    }

    public int? IsEqualToInt
    {
        get => IsEqualTo switch
        {
            int i => i,
            _ => null
        };
        set => IsEqualTo = value;
    }

    protected virtual Visibility DefaultVisibility => Visibility.Visible;
    protected virtual Visibility InvertedVisibility => Visibility.Collapsed;

    public CollapsedWhenExtension(Binding when)
    {
        When = when;
    }

    public override object ProvideValue(IServiceProvider serviceProvider) {
        if (serviceProvider.GetService(typeof(IProvideValueTarget)) is not IProvideValueTarget provideValueTarget)
            return DependencyProperty.UnsetValue;

        if (provideValueTarget is not { TargetObject: not null, TargetProperty: not null })
            return DependencyProperty.UnsetValue;


        if (When is null)
            return DependencyProperty.UnsetValue;

        if (IsEqualTo is Binding isEqualToBinding)
        {
            var multiBinding = new MultiBinding
            {
                Converter = new HideableVisibilityConverter
                {
                    DefaultVisibility = DefaultVisibility,
                    InvertedVisibility = InvertedVisibility
                },
                Bindings = { When, isEqualToBinding }
            };

            return multiBinding.ProvideValue(serviceProvider);
        }

        When.Converter = new HideableVisibilityConverter
        {
            DefaultVisibility = DefaultVisibility,
            InvertedVisibility = InvertedVisibility,
            IsEqualTo = IsEqualTo
        };
        return When.ProvideValue(serviceProvider);
    }
}
