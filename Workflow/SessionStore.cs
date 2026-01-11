#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace RahBuilder.Workflow;

public sealed class SessionStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string SessionsRoot { get; }

    public SessionStore()
    {
        var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = AppContext.BaseDirectory;
        SessionsRoot = Path.Combine(basePath, "Builder-Rah", "Sessions");
        Directory.CreateDirectory(SessionsRoot);
    }

    public void Save(SessionState state)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        if (string.IsNullOrWhiteSpace(state.SessionId))
            state.SessionId = Guid.NewGuid().ToString("N");

        state.LastTouched = DateTimeOffset.UtcNow;
        Directory.CreateDirectory(SessionsRoot);
        var path = GetPath(state.SessionId);
        var tempPath = path + ".tmp";
        var json = JsonSerializer.Serialize(state, Options);
        File.WriteAllText(tempPath, json);
        if (File.Exists(path))
            File.Delete(path);
        File.Move(tempPath, path);
    }

    public SessionState? Load(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return null;
        var path = GetPath(sessionId);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SessionState>(json, Options);
        }
        catch
        {
            return null;
        }
    }

    public IReadOnlyList<SessionState> ListSessions()
    {
        Directory.CreateDirectory(SessionsRoot);
        var files = Directory.GetFiles(SessionsRoot, "*.json");
        var list = new List<SessionState>();
        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var state = JsonSerializer.Deserialize<SessionState>(json, Options);
                if (state != null)
                    list.Add(state);
            }
            catch
            {
            }
        }
        return list.OrderByDescending(s => s.LastTouched).ToList();
    }

    public void Delete(string sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId)) return;
        var path = GetPath(sessionId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void Export(string sessionId, string outputPath)
    {
        var state = Load(sessionId);
        if (state == null) throw new InvalidOperationException("Session not found.");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        var json = JsonSerializer.Serialize(state, Options);
        File.WriteAllText(outputPath, json);
    }

    public SessionState Import(string inputPath)
    {
        if (!File.Exists(inputPath)) throw new FileNotFoundException("Import file not found.", inputPath);
        var json = File.ReadAllText(inputPath);
        var state = JsonSerializer.Deserialize<SessionState>(json, Options) ?? new SessionState();
        state.SessionId = Guid.NewGuid().ToString("N");
        state.LastTouched = DateTimeOffset.UtcNow;
        Save(state);
        return state;
    }

    private string GetPath(string sessionId) => Path.Combine(SessionsRoot, sessionId + ".json");
}
