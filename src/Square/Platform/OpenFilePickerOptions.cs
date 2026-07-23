namespace Square.Platform;

public sealed class OpenFilePickerOptions
{
    public string? Title { get; set; }
    public string? InitialDirectory { get; set; }
    public bool AllowMultiple { get; set; }
    public IReadOnlyList<FilePickerFilter> Filters { get; set; } = [];

    internal void Validate()
    {
        if (Title?.Contains('\0') == true)
            throw new ArgumentException("The file picker title cannot contain a null character.", nameof(Title));

        if (InitialDirectory != null)
        {
            if (string.IsNullOrWhiteSpace(InitialDirectory))
                throw new ArgumentException("The initial directory cannot be empty.", nameof(InitialDirectory));
            if (InitialDirectory.Contains('\0'))
                throw new ArgumentException("The initial directory cannot contain a null character.",
                    nameof(InitialDirectory));
            if (!Directory.Exists(InitialDirectory))
                throw new DirectoryNotFoundException($"The initial directory does not exist: '{InitialDirectory}'.");
        }

        ArgumentNullException.ThrowIfNull(Filters);
        for (var index = 0; index < Filters.Count; index++)
        {
            if (Filters[index] == null)
                throw new ArgumentException($"The file picker filter at index {index} cannot be null.", nameof(Filters));
        }
    }
}

public sealed class FilePickerFilter
{
    public FilePickerFilter(string name, IEnumerable<string> patterns)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(patterns);
        if (name.Contains('\0'))
            throw new ArgumentException("The filter name cannot contain a null character.", nameof(name));

        var values = patterns.ToArray();
        if (values.Length == 0)
            throw new ArgumentException("A file picker filter must contain at least one pattern.", nameof(patterns));
        for (var index = 0; index < values.Length; index++)
        {
            var pattern = values[index];
            if (string.IsNullOrWhiteSpace(pattern))
                throw new ArgumentException($"The filter pattern at index {index} cannot be empty.", nameof(patterns));
            if (pattern.Contains('\0') || pattern.Contains(';'))
                throw new ArgumentException(
                    $"The filter pattern at index {index} cannot contain a null character or semicolon.",
                    nameof(patterns));
        }

        Name = name;
        Patterns = values;
    }

    public string Name { get; }
    public IReadOnlyList<string> Patterns { get; }
}
