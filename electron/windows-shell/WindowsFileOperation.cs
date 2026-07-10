using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace Lumina.WindowsShell
{
    [Flags]
    internal enum FileOperationFlags : uint
    {
        RenameOnCollision = 0x0008,
        NoConfirmation = 0x0010,
        AllowUndo = 0x0040,
        NoConfirmMkdir = 0x0200,
        WantNukeWarning = 0x4000,
        RecycleOnDelete = 0x00080000,
        ShowElevationPrompt = 0x00040000,
        PreserveFileExtensions = 0x00200000,
        AddUndoRecord = 0x20000000,
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComImport]
    [Guid("04B0F1A7-9490-44BC-96E1-4296A31252E2")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileOperationProgressSink
    {
    }

    [ComImport]
    [Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IFileOperation
    {
        uint Advise(IFileOperationProgressSink sink);
        void Unadvise(uint cookie);
        void SetOperationFlags(FileOperationFlags flags);
        void SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)] string message);
        void SetProgressDialog([MarshalAs(UnmanagedType.Interface)] object dialog);
        void SetProperties([MarshalAs(UnmanagedType.Interface)] object properties);
        void SetOwnerWindow(IntPtr owner);
        void ApplyPropertiesToItem(IShellItem item);
        void ApplyPropertiesToItems([MarshalAs(UnmanagedType.Interface)] object items);
        void RenameItem(IShellItem item, [MarshalAs(UnmanagedType.LPWStr)] string newName, IFileOperationProgressSink sink);
        void RenameItems([MarshalAs(UnmanagedType.Interface)] object items, [MarshalAs(UnmanagedType.LPWStr)] string newName);
        void MoveItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string newName, IFileOperationProgressSink sink);
        void MoveItems([MarshalAs(UnmanagedType.Interface)] object items, IShellItem destination);
        void CopyItem(IShellItem item, IShellItem destination, [MarshalAs(UnmanagedType.LPWStr)] string copyName, IFileOperationProgressSink sink);
        void CopyItems([MarshalAs(UnmanagedType.Interface)] object items, IShellItem destination);
        void DeleteItem(IShellItem item, IFileOperationProgressSink sink);
        void DeleteItems([MarshalAs(UnmanagedType.Interface)] object items);
        uint NewItem(IShellItem destination, uint attributes, [MarshalAs(UnmanagedType.LPWStr)] string name, [MarshalAs(UnmanagedType.LPWStr)] string templateName, IFileOperationProgressSink sink);
        void PerformOperations();
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetAnyOperationsAborted();
    }

    [ComImport]
    [Guid("3AD05575-8857-4850-9277-11B85BDB8E09")]
    internal class FileOperationComObject
    {
    }

    public sealed class FileOperationResult
    {
        public bool Aborted { get; set; }
    }

    public static class FileOperationRunner
    {
        private static readonly Guid ShellItemGuid = new Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE");
        private const uint FileAttributeDirectory = 0x10;

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        [return: MarshalAs(UnmanagedType.Interface)]
        private static extern object SHCreateItemFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string path,
            IntPtr bindContext,
            ref Guid interfaceId);

        private static IShellItem CreateShellItem(string path)
        {
            if (String.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A fully-qualified Shell path is required.", "path");
            Guid guid = ShellItemGuid;
            return (IShellItem)SHCreateItemFromParsingName(Path.GetFullPath(path), IntPtr.Zero, ref guid);
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value))
                Marshal.FinalReleaseComObject(value);
        }

        public static FileOperationResult Execute(
            string action,
            string[] sources,
            string destination,
            string newName,
            bool permanent,
            bool renameOnCollision,
            long ownerHandle,
            bool addUndoRecord,
            bool noConfirmation)
        {
            if (String.IsNullOrWhiteSpace(action))
                throw new ArgumentException("A file operation action is required.", "action");

            IFileOperation operation = null;
            IShellItem destinationItem = null;
            var sourceItems = new List<IShellItem>();
            try
            {
                operation = (IFileOperation)new FileOperationComObject();
                FileOperationFlags flags = FileOperationFlags.NoConfirmMkdir |
                    FileOperationFlags.ShowElevationPrompt;

                if (!permanent && addUndoRecord)
                    flags |= FileOperationFlags.AllowUndo | FileOperationFlags.AddUndoRecord;
                if (String.Equals(action, "delete", StringComparison.OrdinalIgnoreCase))
                    flags |= permanent
                        ? (noConfirmation ? 0 : FileOperationFlags.WantNukeWarning)
                        : FileOperationFlags.RecycleOnDelete;
                if (renameOnCollision)
                    flags |= FileOperationFlags.RenameOnCollision | FileOperationFlags.PreserveFileExtensions;
                if (noConfirmation)
                    flags |= FileOperationFlags.NoConfirmation;

                operation.SetOperationFlags(flags);
                if (ownerHandle != 0)
                    operation.SetOwnerWindow(new IntPtr(ownerHandle));

                if (!String.IsNullOrWhiteSpace(destination))
                    destinationItem = CreateShellItem(destination);

                if (String.Equals(action, "newFolder", StringComparison.OrdinalIgnoreCase))
                {
                    if (destinationItem == null || String.IsNullOrWhiteSpace(newName))
                        throw new ArgumentException("New-folder operations require a destination and name.");
                    operation.NewItem(destinationItem, FileAttributeDirectory, newName, null, null);
                }
                else
                {
                    if (sources == null || sources.Length == 0)
                        throw new ArgumentException("The file operation requires at least one source path.");

                    foreach (string source in sources)
                    {
                        IShellItem item = CreateShellItem(source);
                        sourceItems.Add(item);
                        if (String.Equals(action, "copy", StringComparison.OrdinalIgnoreCase))
                            operation.CopyItem(item, destinationItem, null, null);
                        else if (String.Equals(action, "move", StringComparison.OrdinalIgnoreCase))
                            operation.MoveItem(item, destinationItem, null, null);
                        else if (String.Equals(action, "delete", StringComparison.OrdinalIgnoreCase))
                            operation.DeleteItem(item, null);
                        else if (String.Equals(action, "rename", StringComparison.OrdinalIgnoreCase))
                            operation.RenameItem(item, newName, null);
                        else
                            throw new ArgumentOutOfRangeException("action", action, "Unsupported Shell file operation.");
                    }
                }

                operation.PerformOperations();
                return new FileOperationResult { Aborted = operation.GetAnyOperationsAborted() };
            }
            finally
            {
                foreach (IShellItem item in sourceItems)
                    Release(item);
                Release(destinationItem);
                Release(operation);
            }
        }
    }
}
