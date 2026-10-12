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

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Hwnd;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public System.Drawing.Point Point;
        public uint Private;
    }

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out NativeMessage message, IntPtr hwnd, uint min, uint max, uint remove);

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    [Apartment(ApartmentState.STA)]
    public void JumpFolder_PreservesFileNameAcrossRepeatedNavigation(bool save, bool hotkeyModifierHeld)
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
                    if (hotkeyModifierHeld)
                    {
                        // Reproduce the Dialog Jump hotkey: its handler runs while Alt is still held.
                        PressHotkeyAndJump(tab, destination);
                    }
                    else
                    {
                        Assert.That(tab.JumpFolder(destination, false), Is.True);
                    }
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
    public void JumpFolder_CancelsWhenHotkeyModifierRemainsHeld(bool save)
    {
        var title = $"Flow held hotkey {Guid.NewGuid()}";
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(root, "destination")).FullName;
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
                PInvoke.SetForegroundWindow(handle);
                Assert.That(SpinWait.SpinUntil(() => PInvoke.GetForegroundWindow() == handle &&
                    new WindowsDialog().CheckDialogWindow((IntPtr)handle)?.GetCurrentTab().GetCurrentFile() == "preserve-me", 5000),
                    Is.True, "Dialog did not initialize or gain focus.");
                using var tab = new WindowsDialog().CheckDialogWindow((IntPtr)handle).GetCurrentTab();
                // The address editor is created on demand; make sure this test reaches the Ctrl-L path.
                new InputSimulator().Keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_L);
                Assert.That(SpinWait.SpinUntil(() => !string.IsNullOrEmpty(tab.GetCurrentFolder()), 3000), Is.True,
                    "Address editor did not initialize.");
                PressHotkeyAndExpectCancel(tab, destination);
                Assert.That(tab.GetCurrentFile(), Is.EqualTo("preserve-me"));
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
        }, null, 15000, Timeout.Infinite);
        var elapsed = new Stopwatch();
        var worker = Task.Run(() =>
        {
            HWND handle = HWND.Null;
            try
            {
                // Wait for the real modal first so the discovery failure cannot race ShowDialog.
                Assert.That(SpinWait.SpinUntil(() =>
                {
                    var dialog = PInvoke.FindWindowEx(HWND.Null, HWND.Null, "#32770", title);
                    return !dialog.IsNull && IsWindowVisible((IntPtr)dialog);
                }, 10000), Is.True, "Dialog did not open.");
                elapsed.Start();
                if (!SpinWait.SpinUntil(() =>
                {
                    handle = PInvoke.FindWindowEx(HWND.Null, HWND.Null, "#32770", title + " missing");
                    return !handle.IsNull;
                }, 200)) throw new TimeoutException("Dialog discovery timed out.");
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

    private static void PressHotkeyAndJump(IDialogJumpDialogWindowTab tab, string destination)
    {
        const int hotkeyId = 0x4696;
        const uint modAlt = 0x0001, modNoRepeat = 0x4000;
        // F13 avoids clashing with a running Flow Launcher; the registration swallows it like the real hotkey.
        Assert.That(RegisterHotKey(IntPtr.Zero, hotkeyId, modAlt | modNoRepeat, (uint)VirtualKeyCode.F13), Is.True, "Could not register the test hotkey.");
        var keyboard = new InputSimulator().Keyboard;
        Task release = null;
        try
        {
            keyboard.KeyDown(VirtualKeyCode.LMENU);
            keyboard.KeyPress(VirtualKeyCode.F13);
            // The user lets go of Alt shortly after the hotkey fires.
            release = Task.Delay(300).ContinueWith(_ => keyboard.KeyUp(VirtualKeyCode.LMENU), TaskScheduler.Default);
            Assert.That(tab.JumpFolder(destination, false), Is.True);
        }
        finally
        {
#pragma warning disable VSTHRD002
            release?.Wait();
#pragma warning restore VSTHRD002
            if (release == null) keyboard.KeyUp(VirtualKeyCode.LMENU);
            UnregisterHotKey(IntPtr.Zero, hotkeyId);
        }
    }

    private static void PressHotkeyAndExpectCancel(IDialogJumpDialogWindowTab tab, string destination)
    {
        const int hotkeyId = 0x4696;
        const int ctrlAltLId = 0x4697;
        const uint modAlt = 0x0001, modControl = 0x0002, modNoRepeat = 0x4000;
        Assert.That(RegisterHotKey(IntPtr.Zero, hotkeyId, modAlt | modNoRepeat, (uint)VirtualKeyCode.F13), Is.True,
            "Could not register the test hotkey.");
        var keyboard = new InputSimulator().Keyboard;
        Task release = null;
        var ctrlAltLRegistered = false;
        try
        {
            // If JumpFolder injects Ctrl-L while Alt is held, Windows reports Ctrl-Alt-L here.
            ctrlAltLRegistered = RegisterHotKey(IntPtr.Zero, ctrlAltLId, modAlt | modControl | modNoRepeat,
                (uint)VirtualKeyCode.VK_L);
            Assert.That(ctrlAltLRegistered, Is.True, "Could not register the Ctrl-Alt-L probe hotkey.");
            keyboard.KeyDown(VirtualKeyCode.LMENU);
            keyboard.KeyPress(VirtualKeyCode.F13);
            // Keep Alt down beyond the 500 ms limit; the delayed release also prevents a regression from hanging the test.
            release = Task.Delay(1200).ContinueWith(_ => keyboard.KeyUp(VirtualKeyCode.LMENU), TaskScheduler.Default);
            var elapsed = Stopwatch.StartNew();
            Assert.That(tab.JumpFolder(destination, false), Is.False);
            elapsed.Stop();
            Assert.That(elapsed.Elapsed, Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(450)),
                "The modifier release window ended too early.");
            Assert.That((GetAsyncKeyState((int)VirtualKeyCode.LMENU) & 0x8000) != 0, Is.True,
                "Alt was released before JumpFolder cancelled.");
            // Let the worker's hotkey queue receive any injected shortcut before inspecting it.
            Thread.Sleep(100);
            Assert.That(WasHotkeyPressed(ctrlAltLId), Is.False, "Ctrl-L was injected while Alt remained held.");
            // Verify the probe would observe the exact shortcut that JumpFolder must not send.
            keyboard.ModifiedKeyStroke(VirtualKeyCode.LCONTROL, VirtualKeyCode.VK_L);
            Assert.That(SpinWait.SpinUntil(() => WasHotkeyPressed(ctrlAltLId), 500), Is.True,
                "The Ctrl-Alt-L probe did not detect an injected Ctrl-L.");
        }
        finally
        {
#pragma warning disable VSTHRD002
            release?.Wait();
#pragma warning restore VSTHRD002
            if (release == null) keyboard.KeyUp(VirtualKeyCode.LMENU);
            if (ctrlAltLRegistered) UnregisterHotKey(IntPtr.Zero, ctrlAltLId);
            UnregisterHotKey(IntPtr.Zero, hotkeyId);
        }
    }

    private static bool WasHotkeyPressed(int hotkeyId)
    {
        const uint wmHotkey = 0x0312, pmRemove = 0x0001;
        var pressed = false;
        while (PeekMessage(out var message, IntPtr.Zero, wmHotkey, wmHotkey, pmRemove))
        {
            if (message.WParam == (IntPtr)hotkeyId) pressed = true;
        }
        return pressed;
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
        var root = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).FullName;
        var initial = Directory.CreateDirectory(Path.Combine(root, "initial")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(root, "destination")).FullName;
        using var owner = new System.Windows.Forms.Form { Width = 200, Height = 100 };
        owner.Show();
        var ownerHandle = new HWND(owner.Handle);
        using var nativeDialog = new System.Windows.Forms.SaveFileDialog
        {
            AutoUpgradeEnabled = false,
            Title = title,
            InitialDirectory = initial,
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
                Assert.That(SpinWait.SpinUntil(() => GetLegacyDialogText(handle, CDM_GETFOLDERPATH) == initial, 5000), Is.True,
                    $"Legacy dialog did not open in the initial folder: {GetLegacyDialogText(handle, CDM_GETFOLDERPATH)}.");
                var dialog = new WindowsDialog().CheckDialogWindow((IntPtr)handle);
                Assert.That(dialog, Is.Not.Null);
                using var tab = dialog.GetCurrentTab();
                var elapsed = Stopwatch.StartNew();
                Assert.That(tab.JumpFolder(destination, false), Is.True);
                Assert.That(elapsed.Elapsed, Is.LessThan(TimeSpan.FromMilliseconds(500)));
                Assert.That(SpinWait.SpinUntil(() => GetLegacyDialogText(handle, CDM_GETFOLDERPATH) == destination, 3000), Is.True,
                    $"Current folder: {GetLegacyDialogText(handle, CDM_GETFOLDERPATH)}, file: {GetLegacyDialogText(handle, CDM_GETSPEC)}.");
                Assert.That(IsWindow((IntPtr)handle), Is.True, "Legacy dialog closed instead of navigating.");
                Assert.That(GetLegacyDialogText(handle, CDM_GETSPEC), Is.EqualTo("preserve-me"));
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
            worker.GetAwaiter().GetResult();
#pragma warning restore VSTHRD002
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    private const uint CDM_GETSPEC = 0x0464; // WM_USER + 100
    private const uint CDM_GETFOLDERPATH = 0x0466; // WM_USER + 102

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wParam, char[] lParam);

    private static string GetLegacyDialogText(HWND handle, uint message)
    {
        var buffer = new char[1024];
        var length = (int)SendMessage((IntPtr)handle, message, buffer.Length, buffer);
        // The returned length includes the terminating null character.
        return length > 1 ? new string(buffer, 0, length - 1) : string.Empty;
    }
}
