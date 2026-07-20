using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Square.Graphics;
using Square.Graphics.Codecs;
using Square.Hosting;
using Square.Platform;

namespace Square.Tooling;

public sealed class ToolingServer : IAsyncDisposable, IDisposable
{
    public const string TokenHeader = "X-Square-Tooling-Token";

    private readonly WebApplication _webApplication;

    private ToolingServer(WebApplication webApplication, string accessToken, int port)
    {
        _webApplication = webApplication;
        AccessToken = accessToken;
        Port = port;
    }

    public string AccessToken { get; }
    public int Port { get; }
    public string BaseAddress => $"http://127.0.0.1:{Port}";

    public static ToolingServer Start(DesktopApplication application, ToolingOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(application);
        options ??= new ToolingOptions();
        if (options.Port is < 0 or > 65535) throw new ArgumentOutOfRangeException(nameof(options.Port));

        var token = string.IsNullOrWhiteSpace(options.AccessToken)
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant()
            : options.AccessToken;

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{options.Port}");
        builder.Services.ConfigureHttpJsonOptions(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });

        var web = builder.Build();
        web.Use(async (context, next) =>
        {
            if (!context.Request.Headers.TryGetValue(TokenHeader, out var supplied) ||
                !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.UTF8.GetBytes(supplied.ToString()),
                    System.Text.Encoding.UTF8.GetBytes(token)))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsync("{\"error\":\"unauthorized\"}");
                return;
            }
            await next(context);
        });

        web.MapGet("/api/v1/health", (HttpContext context) => Results.Text(
            $"{{\"status\":\"ok\",\"processId\":{Environment.ProcessId},\"port\":{context.Connection.LocalPort}," +
            $"\"baseAddress\":\"http://127.0.0.1:{context.Connection.LocalPort}\"," +
            $"\"inputInjection\":{options.AllowInputInjection.ToString().ToLowerInvariant()}}}",
            "application/json"));

        web.MapGet("/api/v1/screenshot", async () =>
        {
            using var bitmap = await application.CaptureRendererBitmapAsync();
            using var stream = new MemoryStream();
            BitmapPngEncoder.Save(bitmap, stream);
            return Results.File(stream.ToArray(), "image/png", "square-screenshot.png");
        });

        web.MapPost("/api/v1/input/pointer", async (HttpRequest request) =>
        {
            if (!options.AllowInputInjection) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var payload = await ReadJsonAsync(request);
            var input = new ToolingPointerInput(
                new Point(ReadFloat(payload, "x"), ReadFloat(payload, "y")),
                ReadEnum<MouseAction>(payload, "action"),
                ReadModifiers(payload));
            await application.InjectPointerAsync(input);
            return Results.NoContent();
        });

        web.MapPost("/api/v1/input/key", async (HttpRequest request) =>
        {
            if (!options.AllowInputInjection) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var payload = await ReadJsonAsync(request);
            var input = new ToolingKeyInput(
                ReadInt(payload, "keyCode"),
                ReadEnum<KeyAction>(payload, "action"),
                ReadModifiers(payload));
            await application.InjectKeyAsync(input);
            return Results.NoContent();
        });

        web.MapPost("/api/v1/input/text", async (HttpRequest request) =>
        {
            if (!options.AllowInputInjection) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var payload = await ReadJsonAsync(request);
            await application.InjectTextAsync(ReadString(payload, "text"));
            return Results.NoContent();
        });

        web.MapPost("/api/v1/input/wheel", async (HttpRequest request) =>
        {
            if (!options.AllowInputInjection) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var payload = await ReadJsonAsync(request);
            var input = new ToolingWheelInput(
                new Point(ReadFloat(payload, "x"), ReadFloat(payload, "y")),
                ReadInt(payload, "delta"),
                ReadModifiers(payload));
            await application.InjectWheelAsync(input);
            return Results.NoContent();
        });

        web.MapGet("/api/v1/inspect/tree", async () =>
        {
            if (!options.AllowInspector) return Results.StatusCode(StatusCodes.Status403Forbidden);
            return Json(await application.CaptureInspectionSnapshotAsync(options.IncludeSourcePaths, options.IncludeTextContent));
        });

        web.MapGet("/api/v1/inspect/hit-test", async (HttpRequest request) =>
        {
            if (!options.AllowInspector) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var x = ReadFloat(request.Query, "x");
            var y = ReadFloat(request.Query, "y");
            var result = await application.HitTestInspectionAsync(new Point(x, y), options.IncludeSourcePaths, options.IncludeTextContent);
            return result == null ? Results.NotFound(new { error = "not_found" }) : Json(result);
        });

        web.MapGet("/api/v1/inspect/elements/{id:int}", async (int id) =>
        {
            if (!options.AllowInspector) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var result = await application.InspectElementAsync(id, options.IncludeSourcePaths, options.IncludeTextContent);
            return result == null ? Results.NotFound(new { error = "not_found" }) : Json(result);
        });

        web.Start();
        var actualPort = ResolveBoundPort(web);
        return new ToolingServer(web, token, actualPort);
    }

    private static int ResolveBoundPort(WebApplication webApplication)
    {
        var server = webApplication.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        var address = addresses?.SingleOrDefault();
        if (address == null || !Uri.TryCreate(address, UriKind.Absolute, out var uri) || uri.Port < 1)
            throw new InvalidOperationException("Tooling server started without a discoverable loopback port.");
        return uri.Port;
    }

    public void Dispose()
    {
        _webApplication.StopAsync().GetAwaiter().GetResult();
        _webApplication.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        await _webApplication.StopAsync();
        await _webApplication.DisposeAsync();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpRequest request)
    {
        using var document = await JsonDocument.ParseAsync(request.Body);
        return document.RootElement.Clone();
    }

    private static IResult Json<T>(T value) => Results.Text(JsonSerializer.Serialize(value, JsonOptions), "application/json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        TypeInfoResolver = new DefaultJsonTypeInfoResolver()
    };

    private static string ReadString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
            throw new BadHttpRequestException($"'{name}' must be a string.");
        return value.GetString() ?? "";
    }

    private static int ReadInt(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || !value.TryGetInt32(out var result))
            throw new BadHttpRequestException($"'{name}' must be an integer.");
        return result;
    }

    private static float ReadFloat(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || !value.TryGetSingle(out var result))
            throw new BadHttpRequestException($"'{name}' must be a number.");
        return result;
    }

    private static float ReadFloat(IQueryCollection query, string name)
    {
        if (!query.TryGetValue(name, out var value) ||
            !float.TryParse(value.ToString(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result))
            throw new BadHttpRequestException($"'{name}' must be a number.");
        return result;
    }

    private static T ReadEnum<T>(JsonElement element, string name) where T : struct, Enum
    {
        var value = ReadString(element, name);
        if (!Enum.TryParse<T>(value, ignoreCase: true, out var result))
            throw new BadHttpRequestException($"'{name}' has an unsupported value.");
        return result;
    }

    private static KeyModifiers ReadModifiers(JsonElement element)
    {
        if (!element.TryGetProperty("modifiers", out var value) || value.ValueKind != JsonValueKind.Array)
            return KeyModifiers.None;
        var result = KeyModifiers.None;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String &&
                Enum.TryParse<KeyModifiers>(item.GetString(), ignoreCase: true, out var modifier))
                result |= modifier;
        }
        return result;
    }
}
