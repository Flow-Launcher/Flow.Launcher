extern alias Infrastructure;

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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
                if (!handle.IsNull) PInvoke.PostMessage(handle, 0x0010, 0, 0);
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
}
