using System.Diagnostics;
using System.Formats.Asn1;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SnmpSimulator.Core;

public sealed class SnmpAgent
{
    private sealed record Entry(string Oid, uint[] Parts, byte[] Value, bool Dynamic);
    private readonly byte[] expectedCommunity;
    private readonly Entry[] objects;
    private readonly Dictionary<string, Entry> lookup;
    private readonly HashSet<string> scalarRoots;
    private readonly TimeProvider time;
    private long started;
    private int active;
    public const int MaxResponseBytes = 1400;
    private long received, sent, dropped;
    public long Received => Interlocked.Read(ref received);
    public long Sent => Interlocked.Read(ref sent);
    public long Dropped => Interlocked.Read(ref dropped);
    public SnmpAgent(DeviceProfile profile, TimeProvider? timeProvider = null)
    {
        time = timeProvider ?? TimeProvider.System;
        expectedCommunity = Encoding.UTF8.GetBytes(profile.Community);
        objects = profile.Objects.OrderBy(o => o.Oid, OidComparer.Instance)
            .Select(o => new Entry(o.Oid, o.Oid.Split('.').Select(uint.Parse).ToArray(), o.Encode(TimeSpan.Zero), o.Type == "Uptime")).ToArray();
        lookup = objects.ToDictionary(o => o.Oid);
        scalarRoots = objects.Where(o => o.Oid.EndsWith(".0", StringComparison.Ordinal)).Select(o => o.Oid[..^2]).ToHashSet();
        started = time.GetTimestamp();
    }
    private byte[] Value(Entry entry)
    {
        if (!entry.Dynamic) return entry.Value;
        var writer = new AsnWriter(AsnEncodingRules.BER);
        var ticks = unchecked((uint)(long)(time.GetElapsedTime(started).TotalMilliseconds / 10));
        writer.WriteInteger(ticks, new Asn1Tag(TagClass.Application, 3));
        return writer.Encode();
    }

    public async Task RunAsync(IPEndPoint endpoint, CancellationToken cancellationToken, Action ready)
    {
        if (Interlocked.CompareExchange(ref active, 1, 0) != 0) throw new InvalidOperationException("設備已執行。");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var socket = new UdpClient(endpoint);
            started = time.GetTimestamp();
            Interlocked.Exchange(ref received, 0); Interlocked.Exchange(ref sent, 0); Interlocked.Exchange(ref dropped, 0);
            ready();
            while (true)
            {
                var packet = await socket.ReceiveAsync(cancellationToken);
                Interlocked.Increment(ref received);
                var response = Respond(packet.Buffer);
                if (response is null) { Interlocked.Increment(ref dropped); continue; }
                await socket.SendAsync(response, packet.RemoteEndPoint, cancellationToken);
                Interlocked.Increment(ref sent);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        finally { Interlocked.Exchange(ref active, 0); }
    }

    public byte[]? Respond(ReadOnlyMemory<byte> bytes)
    {
        try { return Process(bytes); }
        catch (Exception ex) when (ex is AsnContentException or ArgumentException or OverflowException) { return null; }
    }

    private byte[]? Process(ReadOnlyMemory<byte> bytes)
    {
        var reader = new AsnReader(bytes, AsnEncodingRules.BER);
        var message = reader.ReadSequence(); reader.ThrowIfNotEmpty();
        var version = (int)message.ReadInteger();
        if (version is not (0 or 1)) return null;
        var community = message.ReadOctetString();
        if (!community.AsSpan().SequenceEqual(expectedCommunity)) return null;
        var tag = message.PeekTag();
        if (tag.TagClass != TagClass.ContextSpecific || tag.TagValue is not (0 or 1 or 3 or 5)) return null;
        if (version == 0 && tag.TagValue == 5) return null;
        var pdu = message.ReadSequence(tag); message.ThrowIfNotEmpty();
        var id = (int)pdu.ReadInteger();
        var field1 = (int)pdu.ReadInteger(); var field2 = (int)pdu.ReadInteger();
        if (tag.TagValue != 5 && (field1 != 0 || field2 != 0)) return null;
        var bindings = pdu.ReadSequence(); pdu.ThrowIfNotEmpty();
        var requests = new List<(string Oid, byte[] Value)>();
        while (bindings.HasData)
        {
            if (requests.Count >= 256) return null;
            var binding = bindings.ReadSequence();
            var oid = binding.ReadObjectIdentifier(); var value = binding.ReadEncodedValue().ToArray();
            binding.ThrowIfNotEmpty();
            if (oid.Split('.').Length > 128 || (tag.TagValue != 3 && !value.AsSpan().SequenceEqual(new byte[] { 5, 0 }))) return null;
            requests.Add((oid, value));
        }
        var output = new List<(string Oid, byte[] Value)>();
        var error = 0; var index = 0;
        if (tag.TagValue == 3)
        {
            if (requests.Count > 0) { error = version == 0 ? 4 : 17; index = 1; }
            output.AddRange(requests);
        }
        else if (tag.TagValue == 5)
        {
            var nonRepeaters = Math.Clamp(field1, 0, requests.Count);
            var repetitions = Math.Clamp(field2, 0, 100);
            for (var i = 0; i < nonRepeaters; i++) output.Add(Next(requests[i].Oid, version));
            var cursors = requests.Skip(nonRepeaters).Select(r => r.Oid).ToArray();
            for (var r = 0; r < repetitions && cursors.Length > 0; r++)
            {
                var allEnded = true;
                for (var i = 0; i < cursors.Length; i++)
                {
                    if (output.Count >= 256) return SizedError(version, community, id, requests);
                    var next = Next(cursors[i], version); output.Add(next); cursors[i] = next.Oid;
                    if (next.Value[0] != 0x82) allEnded = false;
                }
                if (allEnded) break;
            }
        }
        else
        {
            for (var i = 0; i < requests.Count; i++)
            {
                var oid = requests[i].Oid;
                var result = tag.TagValue == 1 ? Next(oid, version) :
                    lookup.TryGetValue(oid, out var item) ? (oid, Value(item)) : (oid, Missing(oid));
                if (version == 0 && result.Item2[0] is 0x80 or 0x81 or 0x82)
                {
                    error = 2; index = i + 1; output.Clear(); output.AddRange(requests); break;
                }
                output.Add(result);
            }
        }
        var response = Encode(version, community, id, error, index, output);
        return response.Length is > 0 and <= MaxResponseBytes ? response : SizedError(version, community, id, requests);
    }

    private byte[] Missing(string oid)
    {
        var parts = oid.Split('.');
        var instanceMissing = scalarRoots.Contains(oid);
        for (var i = 2; !instanceMissing && i < parts.Length; i++)
            instanceMissing = scalarRoots.Contains(string.Join('.', parts.Take(i)));
        return [(byte)(instanceMissing ? 0x81 : 0x80), 0];
    }

    private static byte[]? SizedError(int version, byte[] community, int id, List<(string Oid, byte[] Value)> requests)
    {
        var result = Encode(version, community, id, 1, 0, version == 0 ? requests : []);
        return result.Length is > 0 and <= MaxResponseBytes ? result : null;
    }

    private (string Oid, byte[] Value) Next(string oid, int version)
    {
        var parts = oid.Split('.').Select(uint.Parse).ToArray();
        var lo = 0; var hi = objects.Length;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (Compare(objects[mid].Parts, parts) <= 0) lo = mid + 1; else hi = mid;
        }
        return lo < objects.Length ? (objects[lo].Oid, Value(objects[lo])) : (oid, [0x82, 0]);
    }

    private static int Compare(uint[] a, uint[] b)
    {
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        { var comparison = a[i].CompareTo(b[i]); if (comparison != 0) return comparison; }
        return a.Length.CompareTo(b.Length);
    }

    private static byte[] Encode(int version, byte[] community, int id, int error, int index, IEnumerable<(string Oid, byte[] Value)> values)
    {
        // Bound the full varbind list before building a response. Empty means oversized.
        var encoded = new List<byte[]>();
        var total = 0;
        foreach (var value in values)
        {
            if (value.Value.Length > MaxResponseBytes) return [];
            var binding = new AsnWriter(AsnEncodingRules.BER);
            using (binding.PushSequence()) { binding.WriteObjectIdentifier(value.Oid); binding.WriteEncodedValue(value.Value); }
            var data = binding.Encode(); total += data.Length;
            if (total > MaxResponseBytes) return [];
            encoded.Add(data);
        }
        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(version); writer.WriteOctetString(community);
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 2, true)))
            {
                writer.WriteInteger(id); writer.WriteInteger(error); writer.WriteInteger(index);
                using (writer.PushSequence())
                    foreach (var value in encoded) writer.WriteEncodedValue(value);
            }
        }
        return writer.Encode();
    }
}
