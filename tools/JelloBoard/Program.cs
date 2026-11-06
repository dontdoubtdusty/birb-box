// JelloBoard: serves the Jello task board on http://localhost:5178/ from plain JSON files.
//
// Files (in the repo, committed): claude\board\<gameId>\game.json holds a game's title, pitch and
// columns; claude\board\<gameId>\cards\<cardId>.json holds one card. The file name is the id.
//
// The page (web\index.html) loads everything from /api/board and polls /api/rev every 1.5 s, so a
// file edited by hand or by Claude shows up on its own. Changes from the page go to /api/games/...
// as PUT (replace), PATCH (merge top-level fields) or DELETE, written through a temp file so a
// half-written file never sits on disk. Only localhost can reach it.
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

static class JelloServer
{
    const int Port = 5178;
    static readonly string Url = $"http://localhost:{Port}/";
    static readonly Regex IdPattern = new("^[A-Za-z0-9_-]{1,60}$");
    static readonly JsonSerializerOptions FileJson = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // keeps quotes, apostrophes and accents readable in the files
    };
    static readonly UTF8Encoding Utf8NoBom = new(false);

    static string boardRoot = "";
    static string webRoot = "";

    // Find the board folder, start listening (or hand over to a copy already running), open the browser, serve forever
    static int Main(string[] args)
    {
        Console.Title = "Jello board";
        string? boardArg = null;
        bool openBrowser = true;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--board" && i + 1 < args.Length) boardArg = args[++i];
            else if (args[i] == "--no-browser") openBrowser = false;
        }

        boardRoot = boardArg != null ? Path.GetFullPath(boardArg) : FindBoardRoot() ?? "";
        if (boardRoot == "")
        {
            Console.WriteLine("Could not find the project folder (a folder with CLAUDE.md and claude\\ in it).");
            Console.WriteLine("Run this from the birb-box folder, or pass --board <path to claude\\board>.");
            return 1;
        }
        Directory.CreateDirectory(boardRoot);
        webRoot = Path.Combine(AppContext.BaseDirectory, "web");

        var listener = new HttpListener();
        listener.Prefixes.Add(Url);
        try { listener.Start(); }
        catch (HttpListenerException)
        {
            if (AlreadyRunning())
            {
                Console.WriteLine("The board is already running; opening it.");
                if (openBrowser) OpenBrowser();
                return 0;
            }
            Console.WriteLine($"Port {Port} is taken by another program, so the board can't start.");
            return 1;
        }

        Console.WriteLine($"Jello board: {Url}");
        Console.WriteLine($"Files:       {boardRoot}");
        Console.WriteLine("Close this window to stop the board. Every change is saved as you make it.");
        if (openBrowser) OpenBrowser();

        while (true)
        {
            var ctx = listener.GetContext();
            try { Handle(ctx); }
            catch (Exception e)
            {
                Console.WriteLine($"{ctx.Request.HttpMethod} {ctx.Request.Url?.AbsolutePath}: {e.Message}");
                TrySend(ctx, 500, Error(e.Message));
            }
            finally { try { ctx.Response.Close(); } catch { } }
        }
    }

    // Walk up from the working folder and from the exe's folder to the project (CLAUDE.md plus a claude folder)
    static string? FindBoardRoot()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")) && Directory.Exists(Path.Combine(dir.FullName, "claude")))
                    return Path.Combine(dir.FullName, "claude", "board");
            }
        }
        return null;
    }

    // True when a board server already answers on the port
    static bool AlreadyRunning()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            return http.GetAsync(Url + "api/rev").Result.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    static void OpenBrowser()
    {
        try { Process.Start(new ProcessStartInfo(Url) { UseShellExecute = true }); }
        catch { Console.WriteLine($"Open {Url} in your browser."); }
    }

    // Route one request: the API under /api/, everything else is a file from web\
    static void Handle(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        string host = req.UserHostName ?? "";
        if (host != $"localhost:{Port}" && host != $"127.0.0.1:{Port}") { Send(ctx, 403, Error("Local use only.")); return; }

        string path = req.Url?.AbsolutePath ?? "/";
        if (!path.StartsWith("/api/")) { ServeStatic(ctx, path); return; }

        // Writes only from the board's own page (or tools that send no Origin)
        string? origin = req.Headers["Origin"];
        if (req.HttpMethod != "GET" && origin != null && origin != $"http://localhost:{Port}" && origin != $"http://127.0.0.1:{Port}")
        {
            Send(ctx, 403, Error("Wrong origin."));
            return;
        }

        if (path == "/api/rev" && req.HttpMethod == "GET") { Send(ctx, 200, new JsonObject { ["rev"] = Rev() }.ToJsonString()); return; }
        if (path == "/api/board" && req.HttpMethod == "GET") { Send(ctx, 200, Board().ToJsonString()); return; }

        // /api/games/<gameId> or /api/games/<gameId>/cards/<cardId>
        var parts = path.Substring("/api/".Length).Split('/');
        bool isGame = parts.Length == 2 && parts[0] == "games" && IdPattern.IsMatch(parts[1]);
        bool isCard = parts.Length == 4 && parts[0] == "games" && parts[2] == "cards" && IdPattern.IsMatch(parts[1]) && IdPattern.IsMatch(parts[3]);
        if (!isGame && !isCard) { Send(ctx, 404, Error("No such API path.")); return; }

        string gameDir = Path.Combine(boardRoot, parts[1]);
        string file = isGame ? Path.Combine(gameDir, "game.json") : Path.Combine(gameDir, "cards", parts[3] + ".json");

        switch (req.HttpMethod)
        {
            case "PUT":
                WriteDoc(file, ReadBody(req));
                break;
            case "PATCH":
            {
                if (!File.Exists(file)) { Send(ctx, 404, Error("That file is gone.")); return; }
                var doc = ReadDoc(file);
                foreach (var (key, value) in ReadBody(req)) doc[key] = value?.DeepClone();
                WriteDoc(file, doc);
                break;
            }
            case "DELETE":
                if (isCard) { if (File.Exists(file)) File.Delete(file); }
                else if (Directory.Exists(gameDir)) Directory.Delete(gameDir, true);
                break;
            default:
                Send(ctx, 405, Error("Method not allowed."));
                return;
        }
        Send(ctx, 200, new JsonObject { ["rev"] = Rev() }.ToJsonString());
    }

    // Every game with its cards, plus a list of files that could not be read
    static JsonObject Board()
    {
        var games = new JsonArray();
        var cards = new JsonObject();
        var problems = new JsonArray();
        foreach (var dir in Directory.GetDirectories(boardRoot).OrderBy(d => d, StringComparer.Ordinal))
        {
            string gid = Path.GetFileName(dir);
            string gameFile = Path.Combine(dir, "game.json");
            if (!IdPattern.IsMatch(gid) || !File.Exists(gameFile)) continue;
            try
            {
                var game = ReadDoc(gameFile);
                game["id"] = gid;
                games.Add(game);
            }
            catch (Exception e) { problems.Add($"{gid}/game.json: {e.Message}"); continue; }

            var list = new JsonArray();
            string cardDir = Path.Combine(dir, "cards");
            if (Directory.Exists(cardDir))
            {
                foreach (var cardFile in Directory.GetFiles(cardDir, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                {
                    string cid = Path.GetFileNameWithoutExtension(cardFile);
                    if (!IdPattern.IsMatch(cid)) continue;
                    try
                    {
                        var card = ReadDoc(cardFile);
                        card["id"] = cid;
                        list.Add(card);
                    }
                    catch (Exception e) { problems.Add($"{gid}/cards/{cid}.json: {e.Message}"); }
                }
            }
            cards[gid] = list;
        }
        return new JsonObject { ["rev"] = Rev(), ["games"] = games, ["cards"] = cards, ["problems"] = problems };
    }

    // A short fingerprint of every board file's name, size and time; changes whenever any file does
    static string Rev()
    {
        var sb = new StringBuilder();
        foreach (var f in Directory.EnumerateFiles(boardRoot, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
        {
            var info = new FileInfo(f);
            sb.Append(f).Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks).Append('\n');
        }
        return Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(sb.ToString())))[..16];
    }

    static JsonObject ReadDoc(string file) =>
        JsonNode.Parse(File.ReadAllText(file, Encoding.UTF8)) as JsonObject ?? throw new InvalidDataException("not a JSON object");

    // Replace a file: write a temp file next to it, then swap it in
    static void WriteDoc(string file, JsonObject doc)
    {
        doc.Remove("id"); // the id is the file name
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        string tmp = file + ".tmp";
        File.WriteAllText(tmp, doc.ToJsonString(FileJson) + "\n", Utf8NoBom);
        File.Move(tmp, file, true);
    }

    static JsonObject ReadBody(HttpListenerRequest req)
    {
        using var reader = new StreamReader(req.InputStream, Encoding.UTF8);
        return JsonNode.Parse(reader.ReadToEnd()) as JsonObject ?? throw new InvalidDataException("The body must be a JSON object.");
    }

    // The page and its libraries, from the web folder next to the exe
    static void ServeStatic(HttpListenerContext ctx, string path)
    {
        if (ctx.Request.HttpMethod != "GET") { Send(ctx, 405, Error("Method not allowed.")); return; }
        string rel = path == "/" ? "index.html" : Uri.UnescapeDataString(path.TrimStart('/'));
        string full = Path.GetFullPath(Path.Combine(webRoot, rel));
        if (!full.StartsWith(webRoot + Path.DirectorySeparatorChar) || !File.Exists(full)) { Send(ctx, 404, Error("Not found.")); return; }
        string type = Path.GetExtension(full).ToLowerInvariant() switch
        {
            ".html" => "text/html; charset=utf-8",
            ".js" => "text/javascript; charset=utf-8",
            ".css" => "text/css; charset=utf-8",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".ico" => "image/x-icon",
            _ => "application/octet-stream",
        };
        SendBytes(ctx, 200, File.ReadAllBytes(full), type);
    }

    static string Error(string message) => new JsonObject { ["error"] = message }.ToJsonString();

    static void Send(HttpListenerContext ctx, int status, string json) =>
        SendBytes(ctx, status, Encoding.UTF8.GetBytes(json), "application/json; charset=utf-8");

    static void SendBytes(HttpListenerContext ctx, int status, byte[] body, string type)
    {
        var res = ctx.Response;
        res.StatusCode = status;
        res.ContentType = type;
        res.Headers["Cache-Control"] = "no-store";
        res.ContentLength64 = body.Length;
        res.OutputStream.Write(body);
    }

    static void TrySend(HttpListenerContext ctx, int status, string json)
    {
        try { Send(ctx, status, json); } catch { }
    }
}
