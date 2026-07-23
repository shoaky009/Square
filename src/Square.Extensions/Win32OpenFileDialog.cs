#if PLATFORM_WIN32
using System.ComponentModel;
using System.Runtime.InteropServices;
using Square.Platform;

namespace Square.Extensions;

internal static partial class Win32OpenFileDialog
{
    private const int InitialBufferLength = 32 * 1024;
    private const uint OfnHideReadOnly = 0x00000004;
    private const uint OfnNoChangeDirectory = 0x00000008;
    private const uint OfnAllowMultiSelect = 0x00000200;
    private const uint OfnPathMustExist = 0x00000800;
    private const uint OfnFileMustExist = 0x00001000;
    private const uint OfnExplorer = 0x00080000;
    private const uint BufferTooSmall = 0x00003003;

    public static unsafe IReadOnlyList<string> Show(IntPtr owner, OpenFilePickerOptions options)
    {
        var filter = BuildFilterString(options.Filters);
        var bufferLength = InitialBufferLength;

        while (true)
        {
            var buffer = new char[bufferLength];
            fixed (char* fileBuffer = buffer)
            fixed (char* filterBuffer = filter)
            fixed (char* titleBuffer = options.Title)
            fixed (char* initialDirectoryBuffer = options.InitialDirectory)
            {
                var dialog = new OpenFileName
                {
                    StructSize = (uint)Marshal.SizeOf<OpenFileName>(),
                    Owner = owner,
                    Filter = (IntPtr)filterBuffer,
                    FilterIndex = 1,
                    File = (IntPtr)fileBuffer,
                    MaxFile = (uint)buffer.Length,
                    InitialDirectory = (IntPtr)initialDirectoryBuffer,
                    Title = (IntPtr)titleBuffer,
                    Flags = OfnExplorer | OfnFileMustExist | OfnPathMustExist | OfnHideReadOnly |
                            OfnNoChangeDirectory | (options.AllowMultiple ? OfnAllowMultiSelect : 0)
                };

                if (GetOpenFileName(ref dialog)) return ParseSelection(buffer);

                var error = CommDlgExtendedError();
                if (error == 0) return [];
                if (error != BufferTooSmall)
                    throw new Win32Exception((int)error, $"The open file dialog failed with error 0x{error:X8}.");

                var requiredLength = (int)buffer[0];
                if (requiredLength <= bufferLength) requiredLength = Math.Min(bufferLength * 2, char.MaxValue);
                if (requiredLength <= bufferLength)
                    throw new InvalidOperationException("The selected file paths exceed the Win32 dialog buffer limit.");
                bufferLength = requiredLength;
            }
        }
    }

    internal static string BuildFilterString(IReadOnlyList<FilePickerFilter> filters)
    {
        if (filters.Count == 0) return "All files\0*.*\0\0";

        var parts = new List<string>(filters.Count * 2 + 1);
        foreach (var filter in filters)
        {
            parts.Add(filter.Name);
            parts.Add(string.Join(';', filter.Patterns));
        }
        parts.Add("");
        return string.Join('\0', parts) + '\0';
    }

    internal static IReadOnlyList<string> ParseSelection(ReadOnlySpan<char> buffer)
    {
        var values = new List<string>();
        var start = 0;
        while (start < buffer.Length)
        {
            var end = buffer[start..].IndexOf('\0');
            if (end < 0) end = buffer.Length - start;
            if (end == 0) break;
            values.Add(new string(buffer.Slice(start, end)));
            start += end + 1;
        }

        if (values.Count <= 1) return values;

        var directory = values[0];
        var paths = new string[values.Count - 1];
        for (var index = 1; index < values.Count; index++)
            paths[index - 1] = Path.Combine(directory, values[index]);
        return paths;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW")]
    private static partial bool GetOpenFileName(ref OpenFileName openFileName);

    [LibraryImport("comdlg32.dll", EntryPoint = "CommDlgExtendedError")]
    private static partial uint CommDlgExtendedError();

    [StructLayout(LayoutKind.Sequential)]
    private struct OpenFileName
    {
        public uint StructSize;
        public IntPtr Owner;
        public IntPtr Instance;
        public IntPtr Filter;
        public IntPtr CustomFilter;
        public uint MaxCustomFilter;
        public uint FilterIndex;
        public IntPtr File;
        public uint MaxFile;
        public IntPtr FileTitle;
        public uint MaxFileTitle;
        public IntPtr InitialDirectory;
        public IntPtr Title;
        public uint Flags;
        public ushort FileOffset;
        public ushort FileExtension;
        public IntPtr DefaultExtension;
        public IntPtr CustomData;
        public IntPtr Hook;
        public IntPtr TemplateName;
        public IntPtr Reserved;
        public uint ReservedValue;
        public uint ExtendedFlags;
    }
}
#endif
