using Square.Platform;
#if PLATFORM_WIN32
using Square.Extensions;
#endif
using Xunit;

namespace Square.Platform.Tests;

public class FilePickerTests
{
    [Fact]
    public void FilterRequiresANameAndPatterns()
    {
        Assert.Throws<ArgumentException>(() => new FilePickerFilter("", ["*.txt"]));
        Assert.Throws<ArgumentException>(() => new FilePickerFilter("Text", []));
        Assert.Throws<ArgumentException>(() => new FilePickerFilter("Text", [""]));
        Assert.Throws<ArgumentException>(() => new FilePickerFilter("Text", ["*.txt;*.md"]));
    }

    [Fact]
    public async Task OptionsRejectMissingInitialDirectory()
    {
        var window = new Square.Hosting.AppWindow("File picker");
        var missingPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        await Assert.ThrowsAsync<DirectoryNotFoundException>(() => window.OpenFilesAsync(new OpenFilePickerOptions
        {
            InitialDirectory = missingPath
        }));
    }

#if PLATFORM_WIN32
    [Fact]
    public void Win32DialogBuildsDoubleNullTerminatedFilter()
    {
        var filter = Win32OpenFileDialog.BuildFilterString(
        [
            new FilePickerFilter("Images", ["*.png", "*.jpg"]),
            new FilePickerFilter("All files", ["*.*"])
        ]);

        Assert.Equal("Images\0*.png;*.jpg\0All files\0*.*\0\0", filter);
    }

    [Fact]
    public void Win32DialogParsesSingleAndMultipleSelections()
    {
        Assert.Equal([@"C:\files\one.txt"],
            Win32OpenFileDialog.ParseSelection("C:\\files\\one.txt\0\0".AsSpan()));

        Assert.Equal([@"C:\files\one.txt", @"C:\files\two.txt"],
            Win32OpenFileDialog.ParseSelection("C:\\files\0one.txt\0two.txt\0\0".AsSpan()));
    }
#endif
}
