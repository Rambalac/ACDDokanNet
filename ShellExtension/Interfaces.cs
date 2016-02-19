using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ShellExtension
{
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e8-0000-0000-c000-000000000046")]
    public interface IShellExtInit
    {
        void Initialize(
            IntPtr pidlFolder,
            IntPtr pDataObj,
            IntPtr /*HKEY*/ hKeyProgID);
    }

    public delegate bool AddPropertySheetPageDelegate(IntPtr hPropSheetPage, IntPtr lParam);

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e9-0000-0000-c000-000000000046")]
    public interface IShellPropSheetExt
    {
        int AddPages(IntPtr pfnAddPage, IntPtr lParam);
        int ReplacePage(uint uPageID, AddPropertySheetPageDelegate lpfnReplacePage, IntPtr lParam);
    }


    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214e4-0000-0000-c000-000000000046")]
    public interface IContextMenu
    {
        [PreserveSig]
        int QueryContextMenu(
            IntPtr /*HMENU*/ hMenu,
            uint iMenu,
            uint idCmdFirst,
            uint idCmdLast,
            uint uFlags);

        void InvokeCommand(IntPtr pici);

        void GetCommandString(
            UIntPtr idCmd,
            uint uFlags,
            IntPtr pReserved,
            StringBuilder pszName,
            uint cchMax);
    }

}
