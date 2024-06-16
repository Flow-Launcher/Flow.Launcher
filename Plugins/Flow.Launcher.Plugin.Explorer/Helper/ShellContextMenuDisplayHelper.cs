using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Flow.Launcher.Plugin.Explorer.Helper;

public static class ShellContextMenuDisplayHelper
{
    #region DllImport

    [DllImport("shell32.dll")]
    private static extern Int32 SHGetMalloc(out IntPtr hObject);

    [DllImport("shell32.dll")]
    private static extern Int32 SHParseDisplayName(
        [MarshalAs(UnmanagedType.LPWStr)] string pszName,
        IntPtr pbc,
        out IntPtr ppidl,
        UInt32 sfgaoIn,
        out UInt32 psfgaoOut
    );

    [DllImport("shell32.dll")]
    private static extern Int32 SHBindToParent(
        IntPtr pidl,
        [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
        out IntPtr ppv,
        ref IntPtr ppidlLast
    );

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr CreatePopupMenu();

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool DestroyMenu(IntPtr hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern uint GetMenuItemCount(IntPtr hMenu);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern uint GetMenuString(
        IntPtr hMenu, uint uIDItem, StringBuilder lpString, int nMaxCount, uint uFlag
    );

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr GetSubMenu(IntPtr hMenu, int nPos);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern bool GetMenuItemInfo(IntPtr hMenu, uint uItem, bool fByPosition, ref MENUITEMINFO lpmii);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    #endregion

    #region Constants

    private const uint ContextMenuStartId = 0x0001;
    private const uint ContextMenuEndId = 0x7FFF;

    private static readonly string[] IgnoredContextMenuCommands =
    {
        // We haven't managed to make these work, so we don't display them in the context menu.
        "Share",
        "Windows.ModernShare",
        "PinToStartScreen",
        "CopyAsPath",

        // Hide functionality provided by the Explorer plugin itself
        "Copy",
        "Delete"
    };

    #endregion

    #region Enums

    [Flags]
    enum ContextMenuFlags : uint
    {
        Normal = 0x00000000,
        DefaultOnly = 0x00000001,
        VerbsOnly = 0x00000002,
        Explore = 0x00000004,
        NoVerbs = 0x00000008,
        CanRename = 0x00000010,
        NoDefault = 0x00000020,
        IncludeStatic = 0x00000040,
        ItemMenu = 0x00000080,
        ExtendedVerbs = 0x00000100,
        DisabledVerbs = 0x00000200,
        AsyncVerbState = 0x00000400,
        OptimizeForInvoke = 0x00000800,
        SyncCascadeMenu = 0x00001000,
        DoNotPickDefault = 0x00002000,
        Reserved = 0xffff0000
    }

    [Flags]
    enum ContextMenuInvokeCommandFlags : uint
    {
        Icon = 0x00000010,
        Hotkey = 0x00000020,
        FlagNoUi = 0x00000400,
        Unicode = 0x00004000,
        NoConsole = 0x00008000,
        AsyncOk = 0x00100000,
        NoZoneChecks = 0x00800000,
        ShiftDown = 0x10000000,
        ControlDown = 0x40000000,
        FlagLogUsage = 0x04000000,
        PointInvoke = 0x20000000
    }

    [Flags]
    enum MenuItemInformationMask : uint
    {
        Bitmap = 0x00000080,
        Checkmarks = 0x00000008,
        Data = 0x00000020,
        Ftype = 0x00000100,
        Id = 0x00000002,
        State = 0x00000001,
        String = 0x00000040,
        Submenu = 0x00000004,
        Type = 0x00000010
    }

    enum MenuItemFtype : uint
    {
        Bitmap = 0x00000004,
        MenuBarBreak = 0x00000020,
        MenuBreak = 0x00000040,
        OwnerDraw = 0x00000100,
        RadioCheck = 0x00000200,
        RightJustify = 0x00004000,
        RightOrder = 0x00002000,
        Separator = 0x00000800,
        String = 0x00000000,
    }

    enum GetCommandStringFlags : uint
    {
        VerbA = 0x00000000,
        HelpTextA = 0x00000001,
        ValidateA = 0x00000002,
        Unicode = VerbW,
        Verb = VerbW,
        VerbW = 0x00000004,
        HelpText = HelpTextW,
        HelpTextW = 0x00000005,
        Validate = ValidateW,
        ValidateW = 0x00000006,
        VerbIconW = 0x00000014
    }
    #endregion

    private static IMalloc GetMalloc()
    {
        SHGetMalloc(out var pMalloc);
        return (IMalloc)Marshal.GetTypedObjectForIUnknown(pMalloc, typeof(IMalloc));
    }

    public static void ExecuteContextMenuItem(string fileName, uint menuItemId)
    {
        IMalloc malloc = null;
        IntPtr originalPidl = IntPtr.Zero;
        IntPtr pShellFolder = IntPtr.Zero;
        IntPtr pContextMenu = IntPtr.Zero;
        IntPtr hMenu = IntPtr.Zero;
        IContextMenu contextMenu = null;
        IShellFolder shellFolder = null;

        try
        {
            malloc = GetMalloc();
            var hr = SHParseDisplayName(fileName, IntPtr.Zero, out var pidl, 0, out _);
            if (hr != 0) throw new Exception("SHParseDisplayName failed");

            originalPidl = pidl;

            var guid = typeof(IShellFolder).GUID;
            hr = SHBindToParent(pidl, guid, out pShellFolder, ref pidl);
            if (hr != 0) throw new Exception("SHBindToParent failed");

            shellFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(pShellFolder, typeof(IShellFolder));
            hr = shellFolder.GetUIObjectOf(
                IntPtr.Zero, 1, new[] { pidl }, typeof(IContextMenu).GUID, IntPtr.Zero, out pContextMenu
            );
            if (hr != 0) throw new Exception("GetUIObjectOf failed");

            contextMenu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(pContextMenu, typeof(IContextMenu));

            hMenu = CreatePopupMenu();
            contextMenu.QueryContextMenu(hMenu, 0, ContextMenuStartId, ContextMenuEndId, (uint)ContextMenuFlags.Explore);

            var directory = Path.GetDirectoryName(fileName);
            var invokeCommandInfo = new CMINVOKECOMMANDINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(CMINVOKECOMMANDINFO)),
                fMask = (uint)ContextMenuInvokeCommandFlags.Unicode,
                hwnd = IntPtr.Zero,
                lpVerb = (IntPtr)(menuItemId - ContextMenuStartId),
                lpParameters = null,
                lpDirectory = null,
                nShow = 1,
                hIcon = IntPtr.Zero,
            };

            hr = contextMenu.InvokeCommand(ref invokeCommandInfo);
            if (hr != 0)
            {
                throw new Exception($"InvokeCommand failed with code {hr:X}");
            }
        }
        finally
        {
            if (hMenu != IntPtr.Zero)
                DestroyMenu(hMenu);

            if (contextMenu != null)
                Marshal.ReleaseComObject(contextMenu);

            if (pContextMenu != IntPtr.Zero)
                Marshal.Release(pContextMenu);

            if (shellFolder != null)
                Marshal.ReleaseComObject(shellFolder);

            if (pShellFolder != IntPtr.Zero)
                Marshal.Release(pShellFolder);

            if (originalPidl != IntPtr.Zero)
                malloc?.Free(originalPidl);

            if (malloc != null)
                Marshal.ReleaseComObject(malloc);
        }
    }

    public static List<ContextMenuItem> GetContextMenuWithIcons(string filePath)
    {
        IMalloc malloc = null;
        IntPtr originalPidl = IntPtr.Zero;
        IntPtr pShellFolder = IntPtr.Zero;
        IntPtr pContextMenu = IntPtr.Zero;
        IntPtr hMenu = IntPtr.Zero;
        IShellFolder shellFolder = null;
        IContextMenu contextMenu = null;

        try
        {
            malloc = GetMalloc();
            var hr = SHParseDisplayName(filePath, IntPtr.Zero, out var pidl, 0, out _);
            if (hr != 0) throw new Exception("SHParseDisplayName failed");

            originalPidl = pidl;

            var guid = typeof(IShellFolder).GUID;
            hr = SHBindToParent(pidl, guid, out pShellFolder, ref pidl);
            if (hr != 0) throw new Exception("SHBindToParent failed");

            shellFolder = (IShellFolder)Marshal.GetTypedObjectForIUnknown(pShellFolder, typeof(IShellFolder));
            hr = shellFolder.GetUIObjectOf(
                IntPtr.Zero, 1, new[] { pidl }, typeof(IContextMenu).GUID, IntPtr.Zero, out pContextMenu
            );
            if (hr != 0) throw new Exception("GetUIObjectOf failed");

            contextMenu = (IContextMenu)Marshal.GetTypedObjectForIUnknown(pContextMenu, typeof(IContextMenu));

            // Without waiting, some items, such as "Send to > Documents", don't always appear, which shifts item ids
            // even though it shouldn't. Please replace this if you find a better way to fix this bug.
            Thread.Sleep(200);

            hMenu = CreatePopupMenu();
            contextMenu.QueryContextMenu(hMenu, 0, ContextMenuStartId, ContextMenuEndId, (uint)ContextMenuFlags.Explore);

            var menuItems = new List<ContextMenuItem>();
            ProcessMenuWithIcons(hMenu, contextMenu, menuItems);

            return menuItems;
        }
        finally
        {
            if (hMenu != IntPtr.Zero)
                DestroyMenu(hMenu);

            if (contextMenu != null)
                Marshal.ReleaseComObject(contextMenu);

            if (pContextMenu != IntPtr.Zero)
                Marshal.Release(pContextMenu);

            if (shellFolder != null)
                Marshal.ReleaseComObject(shellFolder);

            if (pShellFolder != IntPtr.Zero)
                Marshal.Release(pShellFolder);

            if (originalPidl != IntPtr.Zero)
                malloc?.Free(originalPidl);

            if (malloc != null)
                Marshal.ReleaseComObject(malloc);
        }
    }


    private static void ProcessMenuWithIcons(IntPtr hMenu, IContextMenu contextMenu, List<ContextMenuItem> menuItems, string prefix = "")
    {
        uint menuCount = GetMenuItemCount(hMenu);

        for (uint i = 0; i < menuCount; i++)
        {
            var mii = new MENUITEMINFO
            {
                cbSize = (uint)Marshal.SizeOf(typeof(MENUITEMINFO)),
                fMask = (uint)(MenuItemInformationMask.Bitmap | MenuItemInformationMask.Ftype |
                               MenuItemInformationMask.Submenu | MenuItemInformationMask.Id)
            };

            GetMenuItemInfo(hMenu, i, true, ref mii);
            var menuText = new StringBuilder(256);
            uint result = GetMenuString(hMenu, mii.wID, menuText, menuText.Capacity, 0);

            if (result == 0 || string.IsNullOrWhiteSpace(menuText.ToString()))
            {
                continue;
            }

            menuText.Replace("&", "");

            IntPtr hSubMenu = GetSubMenu(hMenu, (int)i);
            if (hSubMenu != IntPtr.Zero)
            {
                ProcessMenuWithIcons(hSubMenu, contextMenu, menuItems, prefix + menuText + " > ");
            }
            else if (!string.IsNullOrWhiteSpace(menuText.ToString()))
            {
                var commandBuilder = new StringBuilder(256);
                contextMenu.GetCommandString(
                    mii.wID - ContextMenuStartId,
                    (uint)GetCommandStringFlags.Verb,
                    IntPtr.Zero,
                    commandBuilder,
                    commandBuilder.Capacity
                );
                if (IgnoredContextMenuCommands.Contains(commandBuilder.ToString(), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                ImageSource icon = null;
                if (mii.hbmpItem != IntPtr.Zero)
                {
                    icon = GetBitmapSourceFromHBitmap(mii.hbmpItem);
                }
                else if (mii.hbmpChecked != IntPtr.Zero)
                {
                    icon = GetBitmapSourceFromHBitmap(mii.hbmpChecked);
                }

                menuItems.Add(new ContextMenuItem(prefix + menuText, icon, mii.wID));
            }
        }
    }

    private static BitmapSource GetBitmapSourceFromHBitmap(IntPtr hBitmap)
    {
        try
        {
            var bitmapSource = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromWidthAndHeight(16, 16)
            );

            if (!DeleteObject(hBitmap))
            {
                throw new Exception("Failed to delete HBitmap.");
            }

            return bitmapSource;
        }
        catch (COMException)
        {
            // ignore
        }

        return null;
    }
}

#region Data Structures

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214E6-0000-0000-C000-000000000046")]
public interface IShellFolder
{
    [PreserveSig]
    int ParseDisplayName(
        IntPtr hwnd, IntPtr pbc, [In, MarshalAs(UnmanagedType.LPWStr)] string pszDisplayName, out uint pchEaten,
        out IntPtr ppidl, ref uint pdwAttributes
    );

    [PreserveSig]
    int EnumObjects(IntPtr hwnd, uint grfFlags, out IntPtr ppenumIDList);

    [PreserveSig]
    int BindToObject(IntPtr pidl, IntPtr pbc, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);

    [PreserveSig]
    int BindToStorage(IntPtr pidl, IntPtr pbc, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);

    [PreserveSig]
    int CompareIDs(IntPtr lParam, IntPtr pidl1, IntPtr pidl2);

    [PreserveSig]
    int CreateViewObject(IntPtr hwndOwner, [In, MarshalAs(UnmanagedType.LPStruct)] Guid riid, out IntPtr ppv);

    [PreserveSig]
    int GetAttributesOf(
        uint cidl, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] apidl, ref uint rgfInOut
    );

    [PreserveSig]
    int GetUIObjectOf(
        IntPtr hwndOwner, uint cidl, [In, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IntPtr[] apidl,
        [In, MarshalAs(UnmanagedType.LPStruct)]
        Guid riid, IntPtr rgfReserved, out IntPtr ppv
    );

    [PreserveSig]
    int GetDisplayNameOf(IntPtr pidl, uint uFlags, IntPtr pName);

    [PreserveSig]
    int SetNameOf(
        IntPtr hwnd, IntPtr pidl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszName, uint uFlags, out IntPtr ppidlOut
    );
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00000002-0000-0000-C000-000000000046")]
public interface IMalloc
{
    [PreserveSig]
    IntPtr Alloc(UInt32 cb);

    [PreserveSig]
    IntPtr Realloc(IntPtr pv, UInt32 cb);

    [PreserveSig]
    void Free(IntPtr pv);

    [PreserveSig]
    UInt32 GetSize(IntPtr pv);

    [PreserveSig]
    Int16 DidAlloc(IntPtr pv);

    [PreserveSig]
    void HeapMinimize();
}

[StructLayout(LayoutKind.Sequential)]
public struct CMINVOKECOMMANDINFO
{
    public uint cbSize;
    public uint fMask;
    public IntPtr hwnd;
    public IntPtr lpVerb;
    [MarshalAs(UnmanagedType.LPStr)] public string lpParameters;
    [MarshalAs(UnmanagedType.LPStr)] public string lpDirectory;
    public int nShow;
    public uint dwHotKey;
    public IntPtr hIcon;
}

[StructLayout(LayoutKind.Sequential)]
public struct MENUITEMINFO
{
    public uint cbSize;
    public uint fMask;
    public uint fType;
    public uint fState;
    public uint wID;
    public IntPtr hSubMenu;
    public IntPtr hbmpChecked;
    public IntPtr hbmpUnchecked;
    public IntPtr dwItemData;
    public IntPtr dwTypeData;
    public uint cch;
    public IntPtr hbmpItem;
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214E4-0000-0000-C000-000000000046")]
public interface IContextMenu
{
    [PreserveSig]
    int QueryContextMenu(IntPtr hmenu, uint indexMenu, uint idCmdFirst, uint idCmdLast, uint uFlags);

    [PreserveSig]
    int InvokeCommand(ref CMINVOKECOMMANDINFO pici);

    [PreserveSig]
    int GetCommandString(uint idcmd, uint uflags, IntPtr reserved, StringBuilder commandstring, int cch);
}

public record ContextMenuItem(string Label, ImageSource Icon, uint CommandId);

#endregion
