using System;
using System.Collections.Generic;
using System.Globalization;
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
        NoErrorUi = 0x0400,
        WantNukeWarning = 0x4000,
        RecycleOnDelete = 0x00080000,
        ShowElevationPrompt = 0x00040000,
        PreserveFileExtensions = 0x00200000,
        AddUndoRecord = 0x20000000,
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler(IntPtr pbc, ref Guid bhid, ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName(uint sigdnName, out IntPtr ppszName);
        void GetAttributes(uint sfgaoMask, out uint psfgaoAttribs);
        void Compare(IShellItem psi, uint hint, out int piOrder);
    }

    [ComVisible(true)]
    [Guid("0C9FB851-E5C9-43EB-A370-F0677B13874C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOperationsProgressDialog
    {
        [PreserveSig]
        int StartProgressDialog(IntPtr owner, uint flags);
        [PreserveSig]
        int StopProgressDialog();
        [PreserveSig]
        int SetOperation(uint action);
        [PreserveSig]
        int SetMode(uint mode);
        [PreserveSig]
        int UpdateProgress(
            ulong pointsCurrent,
            ulong pointsTotal,
            ulong sizeCurrent,
            ulong sizeTotal,
            ulong itemsCurrent,
            ulong itemsTotal);
        [PreserveSig]
        int UpdateLocations(IShellItem source, IShellItem target, IShellItem item);
        [PreserveSig]
        int ResetTimer();
        [PreserveSig]
        int PauseTimer();
        [PreserveSig]
        int ResumeTimer();
        [PreserveSig]
        int GetMilliseconds(out ulong elapsed, out ulong remaining);
        [PreserveSig]
        int GetOperationStatus(out uint status);
    }

    /// <summary>
    /// Stands in for the Windows copy-progress window: the Shell drives this
    /// object instead of showing its own dialog, so progress goes to stdout
    /// (for Lumina's themed dialog) and cancellation comes back through a
    /// marker file the Electron main process creates on demand.
    /// </summary>
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class LuminaProgressBridge : IOperationsProgressDialog
    {
        private const int Success = 0;
        private const uint StatusRunning = 1;   // PDOPS_RUNNING
        private const uint StatusCancelled = 3; // PDOPS_CANCELLED
        private const long PublishIntervalTicks = TimeSpan.TicksPerMillisecond * 75;

        private readonly string cancellationPath;
        private long startedAtTicks;
        private long lastPublishedAtTicks;
        private ulong[] latest = new ulong[6];

        public LuminaProgressBridge(string cancellationPath)
        {
            this.cancellationPath = cancellationPath ?? String.Empty;
            startedAtTicks = DateTime.UtcNow.Ticks;
        }

        private bool IsCancellationRequested()
        {
            return cancellationPath.Length > 0 && File.Exists(cancellationPath);
        }

        private void Publish(bool force)
        {
            long now = DateTime.UtcNow.Ticks;
            if (!force && now - lastPublishedAtTicks < PublishIntervalTicks)
                return;
            lastPublishedAtTicks = now;
            Console.WriteLine(String.Format(
                CultureInfo.InvariantCulture,
                "LUMINA_FILE_PROGRESS:{0},{1},{2},{3},{4},{5}",
                latest[0], latest[1], latest[2], latest[3], latest[4], latest[5]));
            Console.Out.Flush();
        }

        public int StartProgressDialog(IntPtr owner, uint flags)
        {
            startedAtTicks = DateTime.UtcNow.Ticks;
            Publish(true);
            return Success;
        }

        public int StopProgressDialog()
        {
            // Emit the newest counters even if the throttle just swallowed them.
            Publish(true);
            return Success;
        }

        public int SetOperation(uint action) { return Success; }
        public int SetMode(uint mode) { return Success; }

        public int UpdateProgress(
            ulong pointsCurrent,
            ulong pointsTotal,
            ulong sizeCurrent,
            ulong sizeTotal,
            ulong itemsCurrent,
            ulong itemsTotal)
        {
            latest[0] = pointsCurrent;
            latest[1] = pointsTotal;
            latest[2] = sizeCurrent;
            latest[3] = sizeTotal;
            latest[4] = itemsCurrent;
            latest[5] = itemsTotal;
            // Force through the throttle when the points axis completes so the
            // renderer always sees a 100% frame before the result line.
            Publish(pointsTotal > 0 && pointsCurrent >= pointsTotal);
            return Success;
        }

        public int UpdateLocations(IShellItem source, IShellItem target, IShellItem item)
        {
            return Success;
        }

        public int ResetTimer()
        {
            startedAtTicks = DateTime.UtcNow.Ticks;
            return Success;
        }

        public int PauseTimer() { return Success; }
        public int ResumeTimer() { return Success; }

        public int GetMilliseconds(out ulong elapsed, out ulong remaining)
        {
            elapsed = (ulong)Math.Max(0, (DateTime.UtcNow.Ticks - startedAtTicks) / TimeSpan.TicksPerMillisecond);
            remaining = 0;
            if (latest[0] > 0 && latest[1] > latest[0])
            {
                double estimate = elapsed * ((double)(latest[1] - latest[0]) / latest[0]);
                if (estimate > 0 && estimate < UInt64.MaxValue)
                    remaining = (ulong)estimate;
            }
            return Success;
        }

        public int GetOperationStatus(out uint status)
        {
            status = IsCancellationRequested() ? StatusCancelled : StatusRunning;
            return Success;
        }
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
        void SetProgressDialog(IOperationsProgressDialog dialog);
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
            bool noConfirmation,
            string cancellationPath,
            bool reportProgress)
        {
            if (String.IsNullOrWhiteSpace(action))
                throw new ArgumentException("A file operation action is required.", "action");

            IFileOperation operation = null;
            IShellItem destinationItem = null;
            var sourceItems = new List<IShellItem>();
            try
            {
                operation = (IFileOperation)new FileOperationComObject();
                // NoErrorUi: failures come back as HRESULTs and reach the
                // renderer as themed error messages instead of Shell dialogs.
                FileOperationFlags flags = FileOperationFlags.NoConfirmMkdir |
                    FileOperationFlags.NoErrorUi |
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
                if (reportProgress)
                    operation.SetProgressDialog(new LuminaProgressBridge(cancellationPath));

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

                try
                {
                    operation.PerformOperations();
                }
                catch (COMException error)
                {
                    // A user cancellation surfaces as a failed HRESULT; report
                    // it as a normal aborted result, not a command failure.
                    bool aborted = operation.GetAnyOperationsAborted();
                    const int ErrorCancelled = unchecked((int)0x800704C7);
                    const int Abort = unchecked((int)0x80004004);
                    if (aborted || error.ErrorCode == ErrorCancelled || error.ErrorCode == Abort)
                        return new FileOperationResult { Aborted = true };
                    throw;
                }
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
