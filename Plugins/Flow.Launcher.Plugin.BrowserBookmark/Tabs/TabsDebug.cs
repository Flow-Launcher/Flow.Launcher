using System;
using System.Windows.Automation;
using static Flow.Launcher.Plugin.BrowserBookmark.Main;

namespace Flow.Launcher.Plugin.BrowserBookmark.Tabs;

/// <summary>
/// Just for debugging.
/// Call DumpElements whenever you need to analyze browser's internal structure.
/// </summary>
internal class TabsDebug
{
    private static readonly string ClassName = nameof(TabsDebug);

    public static void DumpElements(AutomationElement parent, string classNameOnly = null, string controlTypeOnly = null, int indent = 0)
    {
        AutomationElementCollection children;
        try
        {
            children = parent.FindAll(TreeScope.Children, Condition.TrueCondition);
        }
        catch (ElementNotAvailableException ex)
        {
            Context.API.LogDebug(ClassName, $"Parent not available: {ex.Message}");
            return;
        }

        foreach (AutomationElement child in children)
        {
            try
            {
                var ct = child.Current.ControlType;
                var type = ct?.ProgrammaticName?.Replace("ControlType.", "");
                var name = child.Current.Name;
                var className = child.Current.ClassName;
                var isOffscreen = child.Current.IsOffscreen;
                var isEnabled = child.Current.IsEnabled;
                var rect = child.Current.BoundingRectangle;

                var dump = true;
                if (!string.IsNullOrEmpty(classNameOnly) && className != classNameOnly)
                    dump = false;

                if (!string.IsNullOrEmpty(controlTypeOnly) && type != controlTypeOnly)
                    dump = false;

                if (dump)
                {
                    Context.API.LogDebug(
                        ClassName,
                        $"{new string(' ', indent)}" +
                        $"Type='{type}', " +
                        $"ClassName='{className}', " +
                        $"Name='{name}', " +
                        $"IsOffscreen={isOffscreen}, " +
                        $"IsEnabled={isEnabled}, " +
                        $"BoundingRectangle={rect}"
                    );
                }

                DumpElements(child, classNameOnly, controlTypeOnly, indent + 2);
            }
            catch (ElementNotAvailableException ex)
            {
                Context.API.LogDebug(ClassName, $"Child not available: {ex.Message}");
            }
            catch (Exception ex)
            {
                Context.API.LogException(ClassName, $"Unexpected error", ex);
            }
        }
    }
}
