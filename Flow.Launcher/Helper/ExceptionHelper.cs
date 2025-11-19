// This is a direct copy of the file at https://github.com/microsoft/PowerToys/blob/main/src/modules/launcher/PowerLauncher/Helper/ExceptionHelper.cs and adapted for flow. 

using System;
using System.Runtime.InteropServices;

namespace Flow.Launcher.Helper;

internal static class ExceptionHelper
{
    private const string PresentationFrameworkExceptionSource = "PresentationFramework";

    private const int DWM_E_COMPOSITIONDISABLED = unchecked((int)0x80263001);

    // HRESULT for NT STATUS STATUS_MESSAGE_LOST (0xC0000701 | 0x10000000 == 0xD0000701)
    private const int STATUS_MESSAGE_LOST_HR = unchecked((int)0xD0000701);

    /// <summary>
    /// Returns true if the exception is a recoverable DWM composition exception.
    /// </summary>
    internal static bool IsRecoverableDwmCompositionException(Exception exception)
    {
        if (exception is not COMException comException)
        {
            return false;
        }

        if (comException.HResult is DWM_E_COMPOSITIONDISABLED)
        {
            return true;
        }

        if (comException.HResult is STATUS_MESSAGE_LOST_HR && comException.Source == PresentationFrameworkExceptionSource)
        {
            return true;
        }

        // Check for common DWM composition changed patterns in the stack trace
        var stackTrace = comException.StackTrace;
        return !string.IsNullOrEmpty(stackTrace) &&
               stackTrace.Contains("DwmCompositionChanged", StringComparison.OrdinalIgnoreCase);
    }
}
