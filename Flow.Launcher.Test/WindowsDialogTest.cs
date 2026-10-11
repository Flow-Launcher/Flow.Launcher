extern alias Infrastructure;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using Flow.Launcher.Infrastructure.DialogJump.Models;
using Flow.Launcher.Plugin;
using Microsoft.Win32;
using NUnit.Framework;
using WindowsInput;
using WindowsInput.Native;
using PInvoke = Infrastructure::Windows.Win32.PInvoke;
using HWND = Infrastructure::Windows.Win32.Foundation.HWND;

namespace Flow.Launcher.Test;

[TestFixture]
[Explicit("Requires an interactive Windows desktop and opens native file dialogs.")]
public class WindowsDialogTest
{
    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint from, uint to, bool attach);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hwnd, IntPtr processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hwnd, uint command);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [TestCase(false)]
    [TestCase(true)]
    [Apartment(ApartmentState.STA)]
    public void JumpFolder_PreservesFileNameAcrossRepeatedNavigation(bool save)
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        var first = Directory.CreateDirectory(Path.Combine(root, "first")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(root, "second")).FullName;
        var title = $"Flow dialog regression {Guid.NewGuid()}";
        var owner = new Window { Title = title + " owner", Width = 200, Height = 100 };
        owner.Show();
        var ownerHandle = new HWND(new WindowInteropHelper(owner).Handle);
        CommonDialog nativeDialog = save
            ? new SaveFileDialog { Title = title, InitialDirectory = root, FileName = "preserve-me" }
            : new OpenFileDialog { Title = title, InitialDirectory = root, FileName = "preserve-me" };
        var worker = Task.Run(() =>
        {
            HWND handle = HWND.Null;
            try
            {
                Assert.That(SpinWait.SpinUntil(() =>
                {
                    handle = PInvoke.FindWindowEx(HWND.Null, HWND.Null, "#32770", title);
                    return !handle.IsNull;
                }, 10000), Is.True, "Dialog did not open.");
                Assert.That(handle.IsNull, Is.False);
                PInvoke.SetForegroundWindow(handle);
                IDialogJumpDialogWindow dialog = null;
                var initialized = SpinWait.SpinUntil(() =>
                {
                    dialog = new WindowsDialog().CheckDialogWindow((IntPtr)handle);
                    return dialog?.GetCurrentTab().GetCurrentFile() == "preserve-me";
                }, 5000);
                Assert.That(initialized, Is.True, $"File name editor did not initialize: dialog={dialog != null}, file={dialog?.GetCurrentTab().GetCurrentFile()}.");
                using var tab = dialog.GetCurrentTab();
                Assert.That(tab.GetCurrentFile(), Is.EqualTo("preserve-me"));
                Thread.Sleep(1000);
                foreach (var destination in new[] { first, second })
                {
                    var foreground = PInvoke.GetForegroundWindow();
                    if (foreground != handle)
                    {
                        var foregroundThread = GetWindowThreadProcessId((IntPtr)foreground, IntPtr.Zero);
                        var thread = GetCurrentThreadId();
                        AttachThreadInput(thread, foregroundThread, true);
                        new InputSimulator().Keyboard.KeyPress(VirtualKeyCode.MENU);
                        PInvoke.SetForegroundWindow(handle);
                        AttachThreadInput(thread, foregroundThread, false);
                    }
                    Assert.That(SpinWait.SpinUntil(() =>
                    {
                        PInvoke.SetForegroundWindow(handle);
                        return PInvoke.GetForegroundWindow() == handle;
                    }, 3000), Is.True, "Dialog is not foreground.");
                    Assert.That(tab.JumpFolder(destination, false), Is.True);
                    // Wait for the shell to process Enter before reopening the address editor.
                    Thread.Sleep(250);
                    new InputSimulator().Keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_L);
                    Assert.That(SpinWait.SpinUntil(() => tab.GetCurrentFolder() == destination, 3000), Is.True, $"Current folder: {tab.GetCurrentFolder()}, file: {tab.GetCurrentFile()}.");
                    Assert.That(tab.GetCurrentFile(), Is.EqualTo("preserve-me"));
                }
            }
            finally
            {
                CloseDialog(handle, ownerHandle);
            }
        });
        try
        {
            nativeDialog.ShowDialog(owner);
            // The modal loop has ended; the worker does not dispatch back to this thread.
#pragma warning disable VSTHRD002
            worker.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
        finally
        {
            owner.Close();
            Directory.Delete(root, true);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    [Apartment(ApartmentState.STA)]
    public void DialogDiscoveryFailure_ClosesModalDialog(bool save)
    {
        var title = $"Flow discovery timeout {Guid.NewGuid()}";
        var owner = new Window { Width = 200, Height = 100 };
        owner.Show();
        var ownerHandle = new HWND(new WindowInteropHelper(owner).Handle);
        CommonDialog nativeDialog = save
            ? new SaveFileDialog { Title = title }
            : new OpenFileDialog { Title = title };
        // Rescue a broken cleanup path so the regression reports a failure instead of hanging.
        using var watchdog = new Timer(_ =>
        {
            var dialog = PInvoke.FindWindowEx(HWND.Null, HWND.Null, "#32770", title);
            if (!dialog.IsNull) PInvoke.PostMessage(dialog, 0x0010, 0, 0);
        }, null, 3000, Timeout.Infinite);
        var elapsed = Stopwatch.StartNew();
        var worker = Task.Run(() =>
        {
            HWND handle = HWND.Null;
            try
            {
                if (!SpinWait.SpinUntil(() =>
                {
                    handle = PInvoke.FindWindowEx(HWND.Null, HWND.Null, "#32770", title + " missing");
                    return !handle.IsNull;
                }, 200)) throw new TimeoutException("Dialog did not open.");
            }
            finally
            {
                CloseDialog(handle, ownerHandle);
            }
        });
        try
        {
            nativeDialog.ShowDialog(owner);
#pragma warning disable VSTHRD002
            Assert.Throws<TimeoutException>(() => worker.GetAwaiter().GetResult());
#pragma warning restore VSTHRD002
            Assert.That(elapsed.Elapsed, Is.LessThan(TimeSpan.FromSeconds(2)), "Cleanup needed the watchdog.");
        }
        finally
        {
            owner.Close();
        }
    }

    private static void CloseDialog(HWND handle, HWND ownerHandle)
    {
        if (handle.IsNull)
        {
            var candidate = HWND.Null;
            while (!(candidate = PInvoke.FindWindowEx(HWND.Null, candidate, "#32770", null)).IsNull)
            {
                if (GetWindow((IntPtr)candidate, 4) != (IntPtr)ownerHandle) continue; // GW_OWNER
                handle = candidate;
                break;
            }
        }
        if (!handle.IsNull) PInvoke.PostMessage(handle, 0x0010, 0, 0);
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void JumpFolder_LegacySaveDialogFallsBackWithoutWaitingForAddressBar()
    {
        var title = $"Flow legacy save {Guid.NewGuid()}";
        using var owner = new System.Windows.Forms.Form { Width = 200, Height = 100 };
        owner.Show();
        var ownerHandle = new HWND(owner.Handle);
        using var nativeDialog = new System.Windows.Forms.SaveFileDialog
        {
            AutoUpgradeEnabled = false,
            Title = title,
            FileName = "preserve-me",
        };
        var worker = Task.Run(() =>
        {
            HWND handle = HWND.Null;
            try
            {
                Assert.That(SpinWait.SpinUntil(() =>
                {
                    handle = PInvoke.FindWindowEx(HWND.Null, HWND.Null, "#32770", title);
                    return !handle.IsNull;
                }, 10000), Is.True, "Legacy dialog did not open.");
                var dialog = new WindowsDialog().CheckDialogWindow((IntPtr)handle);
                Assert.That(dialog, Is.Not.Null);
                using var tab = dialog.GetCurrentTab();
                var elapsed = Stopwatch.StartNew();
                Assert.That(tab.JumpFolder(Path.GetTempPath(), false), Is.True);
                Assert.That(elapsed.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)));
            }
            finally
            {
                CloseDialog(handle, ownerHandle);
            }
        });
        nativeDialog.ShowDialog(owner);
#pragma warning disable VSTHRD002
        worker.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
    }
}
