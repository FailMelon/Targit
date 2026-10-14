using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GitLlama.Tests.Shared;

/// <summary>The Git LFS file locking API, enough for git-lfs's lock, unlock and locks commands.</summary>
public sealed class FakeLfsLockServer : IDisposable
{
    private const string Me = "Me";
    private readonly HttpListener _listener = new();
    private readonly List<JsonObject> _locks = [];
    private int _nextId = 1;

    public FakeLfsLockServer()
    {
        var port = FreePort();
        Url = $"http://localhost:{port}/lfs";
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();
        _ = Task.Run(ServeAsync);
    }

    public string Url { get; }

    public void Seed(string path, string owner)
    {
        lock (_locks)
        {
            _locks.Add(Lock(path, owner));
        }
    }

    public void Dispose() => _listener.Close();

    private async Task ServeAsync()
    {
        while (_listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await _listener.GetContextAsync();
            }
            catch (Exception e) when (e is HttpListenerException or ObjectDisposedException)
            {
                return;
            }

            try
            {
                Handle(context);
            }
            finally
            {
                context.Response.Close();
            }
        }
    }

    private void Handle(HttpListenerContext context)
    {
        var path = context.Request.Url!.AbsolutePath;
        var body = new StreamReader(context.Request.InputStream).ReadToEnd();
        var request = body.Length > 0 ? JsonNode.Parse(body) as JsonObject : null;
        lock (_locks)
        {
            if (context.Request.HttpMethod == "POST" && path == "/lfs/locks")
            {
                var file = request!["path"]!.GetValue<string>();
                if (_locks.FirstOrDefault(entry => entry["path"]!.GetValue<string>() == file) is { } existing)
                {
                    Reply(
                        context,
                        409,
                        new JsonObject { ["lock"] = existing.DeepClone(), ["message"] = "already locked" }
                    );
                    return;
                }

                var created = Lock(file, Me);
                _locks.Add(created);
                Reply(context, 201, new JsonObject { ["lock"] = created.DeepClone() });
            }
            else if (context.Request.HttpMethod == "GET" && path == "/lfs/locks")
            {
                var query = context.Request.QueryString;
                var matches = _locks.Where(entry =>
                    (query["path"] is not { } file || entry["path"]!.GetValue<string>() == file)
                    && (query["id"] is not { } id || entry["id"]!.GetValue<string>() == id)
                );
                Reply(context, 200, new JsonObject { ["locks"] = Array(matches), ["next_cursor"] = "" });
            }
            else if (context.Request.HttpMethod == "POST" && path == "/lfs/locks/verify")
            {
                Reply(
                    context,
                    200,
                    new JsonObject
                    {
                        ["ours"] = Array(_locks.Where(entry => Owner(entry) == Me)),
                        ["theirs"] = Array(_locks.Where(entry => Owner(entry) != Me)),
                        ["next_cursor"] = "",
                    }
                );
            }
            else if (context.Request.HttpMethod == "POST" && path.StartsWith("/lfs/locks/") && path.EndsWith("/unlock"))
            {
                var id = path["/lfs/locks/".Length..^"/unlock".Length];
                var existing = _locks.FirstOrDefault(entry => entry["id"]!.GetValue<string>() == id);
                var force = request?["force"]?.GetValue<bool>() == true;
                if (existing is null)
                {
                    Reply(context, 404, new JsonObject { ["message"] = "lock not found" });
                }
                else if (Owner(existing) != Me && !force)
                {
                    Reply(context, 403, new JsonObject { ["message"] = "locked by " + Owner(existing) });
                }
                else
                {
                    _locks.Remove(existing);
                    Reply(context, 200, new JsonObject { ["lock"] = existing.DeepClone() });
                }
            }
            else
            {
                Reply(context, 404, new JsonObject { ["message"] = "not found" });
            }
        }
    }

    private JsonObject Lock(string path, string owner) =>
        new()
        {
            ["id"] = (_nextId++).ToString(),
            ["path"] = path,
            ["locked_at"] = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ["owner"] = new JsonObject { ["name"] = owner },
        };

    private static string Owner(JsonObject entry) => entry["owner"]!["name"]!.GetValue<string>();

    private static JsonArray Array(IEnumerable<JsonObject> entries) =>
        new(entries.Select(entry => entry.DeepClone()).ToArray());

    private static void Reply(HttpListenerContext context, int status, JsonObject body)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/vnd.git-lfs+json";
        var bytes = Encoding.UTF8.GetBytes(body.ToJsonString(new JsonSerializerOptions()));
        context.Response.OutputStream.Write(bytes);
    }

    private static int FreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
