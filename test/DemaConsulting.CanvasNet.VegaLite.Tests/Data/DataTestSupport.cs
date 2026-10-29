using System.Text;

namespace DemaConsulting.CanvasNet.VegaLite.Tests.Data;

/// <summary>
///     An in-memory file source for resolver tests: a dictionary from logical path to bytes that records every path it
///     is asked for and the threads that asked.
/// </summary>
internal sealed class VirtualDataFiles
{
    /// <summary>The files by logical path.</summary>
    private readonly Dictionary<string, byte[]> _files = new(StringComparer.Ordinal);

    /// <summary>The recorded requests.</summary>
    private readonly List<string> _requests = [];

    /// <summary>The recorded requesting threads.</summary>
    private readonly List<int> _threads = [];

    /// <summary>The streams that were handed out, to check that they were disposed.</summary>
    private readonly List<TrackedStream> _streams = [];

    /// <summary>Gets the paths requested, in order, including requests that found nothing.</summary>
    public IReadOnlyList<string> Requests => _requests;

    /// <summary>Gets the managed thread ids of the requests.</summary>
    public IReadOnlyList<int> Threads => _threads;

    /// <summary>Gets the streams handed out so far.</summary>
    public IReadOnlyList<TrackedStream> Streams => _streams;

    /// <summary>
    ///     Adds or replaces a file given as text (written as UTF-8 without a byte-order mark).
    /// </summary>
    /// <param name="path">The logical path.</param>
    /// <param name="text">The text.</param>
    /// <returns>This file set.</returns>
    public VirtualDataFiles With(string path, string text) => With(path, new UTF8Encoding(false).GetBytes(text));

    /// <summary>
    ///     Adds or replaces a file given as bytes.
    /// </summary>
    /// <param name="path">The logical path.</param>
    /// <param name="bytes">The bytes.</param>
    /// <returns>This file set.</returns>
    public VirtualDataFiles With(string path, byte[] bytes)
    {
        _files[path] = bytes;
        return this;
    }

    /// <summary>
    ///     The resolver callback: returns a stream of the file or null.
    /// </summary>
    /// <param name="path">The requested logical path.</param>
    /// <returns>The stream, or <see langword="null"/>.</returns>
    public Stream? Resolve(string path)
    {
        _requests.Add(path);
        _threads.Add(Environment.CurrentManagedThreadId);
        if (!_files.TryGetValue(path, out var bytes))
        {
            return null;
        }

        var stream = new TrackedStream(bytes);
        _streams.Add(stream);
        return stream;
    }
}

/// <summary>
///     A memory stream that remembers whether it was disposed.
/// </summary>
internal sealed class TrackedStream : MemoryStream
{
    /// <summary>
    ///     Initializes the stream over bytes.
    /// </summary>
    /// <param name="bytes">The bytes.</param>
    public TrackedStream(byte[] bytes)
        : base(bytes)
    {
    }

    /// <summary>Gets a value indicating whether the stream was disposed.</summary>
    public bool IsDisposed { get; private set; }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        IsDisposed = true;
        base.Dispose(disposing);
    }
}

/// <summary>
///     A readable stream of spaces that never ends and counts the bytes read from it.
/// </summary>
internal sealed class EndlessDataStream : Stream
{
    /// <summary>Gets the number of bytes that were read.</summary>
    public long BytesRead { get; private set; }

    /// <inheritdoc/>
    public override bool CanRead => true;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => false;

    /// <inheritdoc/>
    public override long Length => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count)
    {
        Array.Fill(buffer, (byte)' ', offset, count);
        BytesRead += count;
        return count;
    }

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}

/// <summary>
///     A temporary directory that is deleted when disposed.
/// </summary>
internal sealed class DataTempDirectory : IDisposable
{
    /// <summary>
    ///     Initializes a new, empty temporary directory.
    /// </summary>
    public DataTempDirectory()
    {
        Path = Directory.CreateTempSubdirectory("vega-lite-test").FullName;
    }

    /// <summary>
    ///     Gets the full path of the directory.
    /// </summary>
    public string Path { get; }

    /// <summary>
    ///     Writes a text file as UTF-8 without a byte-order mark, creating its folders.
    /// </summary>
    /// <param name="relativePath">The path relative to the directory (forward slashes).</param>
    /// <param name="text">The file text.</param>
    /// <returns>The full path of the file.</returns>
    public string Write(string relativePath, string text) => Write(relativePath, new UTF8Encoding(false).GetBytes(text));

    /// <summary>
    ///     Writes a file of bytes, creating its folders.
    /// </summary>
    /// <param name="relativePath">The path relative to the directory (forward slashes).</param>
    /// <param name="bytes">The file bytes.</param>
    /// <returns>The full path of the file.</returns>
    public string Write(string relativePath, byte[] bytes)
    {
        var full = System.IO.Path.Combine(Path, relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, bytes);
        return full;
    }

    /// <summary>
    ///     Gets the full path of a location inside the directory.
    /// </summary>
    /// <param name="relativePath">The relative path.</param>
    /// <returns>The full path.</returns>
    public string Combine(string relativePath) => System.IO.Path.Combine(Path, relativePath);

    /// <inheritdoc/>
    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a leftover temporary folder must not fail a test
        }
        catch (UnauthorizedAccessException)
        {
            // Best effort, as above
        }
    }
}

/// <summary>
///     Helpers that create directory links for the tests that need real ones.
/// </summary>
internal static class DataLinkSupport
{
    /// <summary>
    ///     Tries to create a directory link (a symbolic link, or on Windows a junction when links need a privilege).
    /// </summary>
    /// <param name="link">The full path of the link to create.</param>
    /// <param name="target">The full path of the existing target directory.</param>
    /// <param name="reason">The reason the link could not be created, or an empty string.</param>
    /// <returns><see langword="true"/> when the link exists afterwards.</returns>
    public static bool TryCreateDirectoryLink(string link, string target, out string reason)
    {
        try
        {
            Directory.CreateSymbolicLink(link, target);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            reason = ex.Message;
        }

        // Without the privilege for symbolic links a junction can still be made on Windows
        return OperatingSystem.IsWindows() && TryCreateJunction(link, target, out reason);
    }

    /// <summary>
    ///     Tries to create a file symbolic link.
    /// </summary>
    /// <param name="link">The full path of the link to create.</param>
    /// <param name="target">The full path of the existing target file.</param>
    /// <param name="reason">The reason the link could not be created, or an empty string.</param>
    /// <returns><see langword="true"/> when the link was created.</returns>
    public static bool TryCreateFileLink(string link, string target, out string reason)
    {
        try
        {
            File.CreateSymbolicLink(link, target);
            reason = string.Empty;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            reason = ex.Message;
            return false;
        }
    }

    /// <summary>
    ///     Creates a Windows junction through the command interpreter (no privilege needed).
    /// </summary>
    /// <param name="link">The full path of the junction.</param>
    /// <param name="target">The full path of the target directory.</param>
    /// <param name="reason">The reason the junction could not be created, or an empty string.</param>
    /// <returns><see langword="true"/> when the junction exists afterwards.</returns>
    private static bool TryCreateJunction(string link, string target, out string reason)
    {
        var info = new System.Diagnostics.ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        info.ArgumentList.Add("/c");
        info.ArgumentList.Add("mklink");
        info.ArgumentList.Add("/J");
        info.ArgumentList.Add(Path.GetFullPath(link));
        info.ArgumentList.Add(Path.GetFullPath(target));
        try
        {
            using var process = System.Diagnostics.Process.Start(info);
            if (process is null)
            {
                reason = "cmd.exe could not be started";
                return false;
            }

            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            reason = process.ExitCode == 0 ? string.Empty : "mklink /J failed";
            return process.ExitCode == 0 && Directory.Exists(link);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            reason = ex.Message;
            return false;
        }
    }
}
