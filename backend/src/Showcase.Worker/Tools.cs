using System.Diagnostics;
using System.Text;

namespace Showcase.Worker;

public class PipelineOptions
{
    /// <summary>Blender executable (the Docker image puts it on PATH).</summary>
    public string Blender { get; set; } = "blender";

    /// <summary>gltf-transform CLI (@gltf-transform/cli).</summary>
    public string GltfTransform { get; set; } = "gltf-transform";

    /// <summary>Scratch folder; each job gets its own sub-folder, removed afterwards.</summary>
    public string WorkDir { get; set; } = Path.Combine(Path.GetTempPath(), "showcase-jobs");

    public int TimeoutMinutes { get; set; } = 15;

    /// <summary>Largest texture side in the optimized file; the phone copy uses half.</summary>
    public int MaxTextureSize { get; set; } = 2048;
}

public class ToolFailedException(string message) : Exception(message);

/// <summary>Runs a command-line tool with a timeout and keeps the end of its output for error messages.</summary>
public static class Tool
{
    public static async Task<string> RunAsync(string file, IEnumerable<string> args, TimeSpan timeout, CancellationToken ct)
    {
        var psi = new ProcessStartInfo(file) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var process = new Process { StartInfo = psi };
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) lock (output) output.AppendLine(e.Data); };
        try
        {
            process.Start();
        }
        catch (Exception e)
        {
            throw new ToolFailedException($"{Path.GetFileName(file)} is not installed or cannot start ({e.Message}).");
        }
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timer = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timer.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timer.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new ToolFailedException($"{Path.GetFileName(file)} took longer than {timeout.TotalMinutes:0} minutes and was stopped.");
        }

        string text;
        lock (output) text = output.ToString();
        if (process.ExitCode != 0)
            throw new ToolFailedException($"{Path.GetFileName(file)} exited with code {process.ExitCode}: {Tail(text)}");
        return text;
    }

    public static string Tail(string text, int max = 600)
    {
        text = text.Trim();
        return text.Length <= max ? text : "…" + text[^max..];
    }
}
