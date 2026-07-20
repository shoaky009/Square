using System.Text;
using System.Text.Json;

namespace Square.Extensions.RichText;

public static class RichTextFragmentCodec
{
    public const string MediaType = "application/x-square-richtext+json";
    public const int CurrentVersion = 1;

    public static string Serialize(RichTextFragment fragment)
    {
        ArgumentNullException.ThrowIfNull(fragment);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", CurrentVersion);
            writer.WriteStartArray("blocks");
            foreach (var block in fragment.Blocks)
            {
                writer.WriteStartObject();
                writer.WriteString("kind", block.Kind.ToString());
                if (block.Kind == RichTextBlockKind.Heading)
                    writer.WriteNumber("level", block.HeadingLevel);
                writer.WriteStartArray("runs");
                foreach (var inline in block.Inlines)
                {
                    if (inline is not RichTextRun run) continue;
                    writer.WriteStartObject();
                    writer.WriteString("text", run.Text);
                    WriteMarks(writer, run.Marks);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static RichTextFragment Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("version", out var version) || version.GetInt32() != CurrentVersion)
            throw new InvalidDataException("Unsupported Square rich text fragment version.");
        if (!root.TryGetProperty("blocks", out var blocksElement) || blocksElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Rich text fragment has no blocks array.");

        var blocks = new List<RichTextBlock>();
        foreach (var blockElement in blocksElement.EnumerateArray())
        {
            if (!blockElement.TryGetProperty("kind", out var kindElement) ||
                !Enum.TryParse<RichTextBlockKind>(kindElement.GetString(), out var kind))
                throw new InvalidDataException("Rich text fragment contains an invalid block kind.");
            var level = blockElement.TryGetProperty("level", out var levelElement) ? levelElement.GetInt32() : 0;
            var runs = new List<RichTextInline>();
            if (blockElement.TryGetProperty("runs", out var runsElement) && runsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var runElement in runsElement.EnumerateArray())
                {
                    var text = runElement.TryGetProperty("text", out var textElement) ? textElement.GetString() ?? "" : "";
                    runs.Add(new RichTextRun(text, ReadMarks(runElement)));
                }
            }
            blocks.Add(new RichTextBlock(kind, runs, level));
        }
        return new RichTextFragment(blocks);
    }

    public static bool TryDeserialize(string? json, out RichTextFragment? fragment)
    {
        fragment = null;
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            fragment = Deserialize(json);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static void WriteMarks(Utf8JsonWriter writer, RichTextMarks marks)
    {
        if (marks.IsEmpty) return;
        writer.WriteStartObject("marks");
        if (marks.Bold) writer.WriteBoolean("bold", true);
        if (marks.Italic) writer.WriteBoolean("italic", true);
        if (marks.Underline) writer.WriteBoolean("underline", true);
        if (!string.IsNullOrEmpty(marks.Link)) writer.WriteString("link", marks.Link);
        if (!string.IsNullOrEmpty(marks.Foreground)) writer.WriteString("foreground", marks.Foreground);
        if (!string.IsNullOrEmpty(marks.Background)) writer.WriteString("background", marks.Background);
        writer.WriteEndObject();
    }

    private static RichTextMarks ReadMarks(JsonElement runElement)
    {
        if (!runElement.TryGetProperty("marks", out var marks) || marks.ValueKind != JsonValueKind.Object)
            return RichTextMarks.Empty;
        return new RichTextMarks(
            Bold: ReadBoolean(marks, "bold"),
            Italic: ReadBoolean(marks, "italic"),
            Underline: ReadBoolean(marks, "underline"),
            Link: ReadString(marks, "link"),
            Foreground: ReadString(marks, "foreground"),
            Background: ReadString(marks, "background"));
    }

    private static bool ReadBoolean(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}