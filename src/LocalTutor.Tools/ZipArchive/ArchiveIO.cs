using System.Buffers.Binary;
using System.Security.Cryptography;
using LocalTutor.Core.Tools;
using Zip = System.IO.Compression.ZipArchive;

namespace LocalTutor.Tools.ZipArchive;

internal static class ArchiveIO
{
    internal static async Task<ToolResult<T>> RunAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken userToken)
    {
        userToken.ThrowIfCancellationRequested();
        using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(userToken);
        timeout.CancelAfter(ArchiveLimits.OperationTimeout);
        try { return new(true, await Task.Run(() => operation(timeout.Token), timeout.Token)); }
        catch (OperationCanceledException) when (!userToken.IsCancellationRequested)
        { return new(false, default, "TimedOut: the local archive operation exceeded its deadline."); }
        catch (ArchiveException e) { return new(false, default, e.Message); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { return new(false, default, "ArchiveFailed: invalid ZIP content or local file access prevented the operation."); }
    }

    internal static FileStream OpenRead(string path, long maximum)
    {
        ArchiveAccessScope.CheckPath(path);
        FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length > maximum) { stream.Dispose(); throw new ArchiveException("ResourceLimit"); }
        return stream;
    }

    internal static async Task<(long Bytes, byte[] Hash, uint Crc)> CopyAsync(Stream source, Stream? target, long maximum,
        CancellationToken token, Action<long>? progress = null)
    {
        byte[] buffer = new byte[65536];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        uint crc = uint.MaxValue;
        long bytes = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int read = await source.ReadAsync(buffer, token);
            if (read == 0) break;
            bytes += read;
            if (bytes > maximum) throw new ArchiveException("ResourceLimit");
            hash.AppendData(buffer, 0, read);
            for (int i = 0; i < read; i++) crc = CrcTable[(crc ^ buffer[i]) & 255] ^ (crc >> 8);
            if (target is not null) await target.WriteAsync(buffer.AsMemory(0, read), token);
            progress?.Invoke(bytes);
        }
        token.ThrowIfCancellationRequested();
        return (bytes, hash.GetHashAndReset(), ~crc);
    }

    private static readonly uint[] CrcTable = Enumerable.Range(0, 256).Select(value =>
    {
        uint crc = (uint)value;
        for (int bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? 0xedb88320U ^ (crc >> 1) : crc >> 1;
        return crc;
    }).ToArray();

    internal static void Cleanup(string? path, bool directory, CancellationToken token)
    {
        if (path is null) return;
        try { if (directory) Directory.Delete(path, recursive: true); else File.Delete(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            if (token.IsCancellationRequested) throw new OperationCanceledException("CanceledWithCleanupWarning: a partial archive output could not be removed.", e, token);
            throw new ArchiveException("CleanupFailed");
        }
    }

    internal static bool SamePlan(ArchivePlan before, ArchivePlan after) =>
        before.Output == after.Output && before.TotalBytes == after.TotalBytes && before.Exclusions.SequenceEqual(after.Exclusions) &&
        before.Entries.Length == after.Entries.Length && before.Entries.Zip(after.Entries).All(pair =>
            pair.First.Name == pair.Second.Name && pair.First.IsDirectory == pair.Second.IsDirectory && pair.First.Bytes == pair.Second.Bytes &&
            pair.First.Source == pair.Second.Source && pair.First.Crc == pair.Second.Crc &&
            (pair.First.Hash is null ? pair.Second.Hash is null : pair.Second.Hash is not null && CryptographicOperations.FixedTimeEquals(pair.First.Hash, pair.Second.Hash))) &&
        (before.ArchiveHash is null ? after.ArchiveHash is null : after.ArchiveHash is not null && CryptographicOperations.FixedTimeEquals(before.ArchiveHash, after.ArchiveHash));

    // Check the bounded ordinary central directory BEFORE ZipArchive allocates its entry objects. ZIP64/multidisk are outside this slice.
    internal static uint[] InspectCentralDirectory(FileStream stream, CancellationToken token)
    {
        if (stream.Length < 22) throw new ArchiveException("InvalidZip");
        stream.Position = 0;
        byte[] signature = new byte[4];
        stream.ReadExactly(signature);
        uint first = BinaryPrimitives.ReadUInt32LittleEndian(signature);
        if (first is not (0x04034b50 or 0x06054b50)) throw new ArchiveException("InvalidZip");
        int tailLength = (int)Math.Min(stream.Length, 65557);
        byte[] tail = new byte[tailLength];
        stream.Position = stream.Length - tailLength;
        stream.ReadExactly(tail);
        int end = -1;
        for (int i = tail.Length - 22; i >= 0; i--)
            if (U32(tail, i) == 0x06054b50 && i + 22 + U16(tail, i + 20) == tail.Length) { end = i; break; }
        if (end < 0) throw new ArchiveException("InvalidZip");
        int count = U16(tail, end + 10);
        long centralSize = U32(tail, end + 12), centralOffset = U32(tail, end + 16);
        if (count == ushort.MaxValue || centralSize == uint.MaxValue || centralOffset == uint.MaxValue ||
            U16(tail, end + 4) != 0 || U16(tail, end + 6) != 0 || U16(tail, end + 8) != count)
            throw new ArchiveException("UnsupportedZip");
        if (count > ArchiveLimits.MaxEntries || centralSize > 4 * 1024 * 1024) throw new ArchiveException("ResourceLimit");
        long centralEnd = stream.Length - tailLength + end;
        if (centralOffset + centralSize != centralEnd || count == 0 && (centralOffset != 0 || centralSize != 0)) throw new ArchiveException("InvalidZip");
        stream.Position = centralOffset;
        uint[] crcs = new uint[count];
        byte[] header = new byte[46];
        for (int i = 0; i < count; i++)
        {
            token.ThrowIfCancellationRequested();
            if (stream.Position + header.Length > centralEnd) throw new ArchiveException("InvalidZip");
            stream.ReadExactly(header);
            if (U32(header, 0) != 0x02014b50) throw new ArchiveException("InvalidZip");
            int flags = U16(header, 8), method = U16(header, 10), nameLength = U16(header, 28);
            if ((flags & (1 | 64 | 8192)) != 0 || method is not (0 or 8) || U16(header, 34) != 0 ||
                U32(header, 20) == uint.MaxValue || U32(header, 24) == uint.MaxValue || U32(header, 42) >= centralOffset)
                throw new ArchiveException("UnsupportedZip");
            if (nameLength is 0 or > 1024) throw new ArchiveException("UnsafeEntryName");
            int unixType = (int)(U32(header, 38) >> 16) & 0xf000;
            if (unixType is not (0 or 0x8000 or 0x4000) || (U32(header, 38) & (uint)(FileAttributes.ReparsePoint | FileAttributes.Device)) != 0)
                throw new ArchiveException("LinkedEntry");
            crcs[i] = U32(header, 16);
            long next = stream.Position + nameLength + U16(header, 30) + U16(header, 32);
            if (next > centralEnd) throw new ArchiveException("InvalidZip");
            stream.Position = next;
        }
        if (stream.Position != centralEnd) throw new ArchiveException("InvalidZip");
        stream.Position = 0;
        return crcs;
    }

    private static ushort U16(byte[] data, int start) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(start, 2));
    private static uint U32(byte[] data, int start) => BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(start, 4));

    internal static ArchivePlan ReadEntries(Zip archive, uint[] crcs, string output, byte[] hash, CancellationToken token)
    {
        if (archive.Entries.Count != crcs.Length) throw new ArchiveException("InvalidZip");
        List<PlannedEntry> entries = [];
        Dictionary<string, (string Spelling, bool Directory, bool Explicit)> tree = new(StringComparer.OrdinalIgnoreCase);
        long total = 0;
        for (int i = 0; i < archive.Entries.Count; i++)
        {
            token.ThrowIfCancellationRequested();
            var entry = archive.Entries[i];
            bool directory = entry.FullName.EndsWith('/');
            int type = (entry.ExternalAttributes >> 16) & 0xf000;
            if (type == 0x4000 && !directory || type == 0x8000 && directory ||
                (entry.ExternalAttributes & (int)FileAttributes.Directory) != 0 && !directory) throw new ArchiveException("InvalidZip");
            string name = ArchiveAccessScope.EntryName(entry.FullName, directory);
            if (directory && (entry.Length != 0 || entry.CompressedLength != 0 || crcs[i] != 0)) throw new ArchiveException("InvalidZip");
            if (entry.Length > ArchiveLimits.MaxFileBytes || entry.Length > ArchiveLimits.MaxTotalBytes - total ||
                entry.Length > 1024 * 1024 && entry.Length > Math.Max(1, entry.CompressedLength) * ArchiveLimits.MaxExpansionRatio)
                throw new ArchiveException("ResourceLimit");
            total += entry.Length;
            entries.Add(new(entry.FullName, directory, entry.Length, Crc: crcs[i]));
            AddToTree(tree, name, directory);
        }
        // Implied directories form part of the displayed tree and resource budget.
        foreach (var item in tree.Values.Where(v => v.Directory && !v.Explicit)) entries.Add(new(item.Spelling + "/", true, 0));
        if (entries.Count > ArchiveLimits.MaxEntries) throw new ArchiveException("ResourceLimit");
        return new(output, entries.ToArray(), total, [], ArchiveAccessScope.Exists(output) ? [output] : [], hash);
    }

    internal static void AddToTree(Dictionary<string, (string Spelling, bool Directory, bool Explicit)> tree, string name, bool directory)
    {
        string[] parts = name.Split('/');
        for (int i = 0; i < parts.Length; i++)
        {
            string key = string.Join('/', parts.Take(i + 1));
            bool isDirectory = i < parts.Length - 1 || directory;
            bool explicitEntry = i == parts.Length - 1;
            if (tree.TryGetValue(key, out var previous))
            {
                if (previous.Spelling != key || previous.Directory != isDirectory || explicitEntry && previous.Explicit)
                    throw new ArchiveException("EntryConflict");
                tree[key] = (key, isDirectory, previous.Explicit || explicitEntry);
            }
            else tree.Add(key, (key, isDirectory, explicitEntry));
            if (tree.Count > ArchiveLimits.MaxEntries) throw new ArchiveException("ResourceLimit");
        }
    }
}
