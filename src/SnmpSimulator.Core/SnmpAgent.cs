using System.Diagnostics;
using System.Formats.Asn1;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace SnmpSimulator.Core;

public sealed class SnmpAgent
{
    private readonly DeviceProfile profile;
    private readonly OidObject[] objects;
    private readonly Dictionary<string, OidObject> lookup;
    private readonly Stopwatch uptime = new();
    private long received, sent, dropped;
    public long Received => Interlocked.Read(ref received);
    public long Sent => Interlocked.Read(ref sent);
    public long Dropped => Interlocked.Read(ref dropped);
    public SnmpAgent(DeviceProfile profile)
    {
        this.profile = profile;
        objects = profile.Objects.OrderBy(o => o.Oid, OidComparer.Instance).ToArray();
        lookup = objects.ToDictionary(o => o.Oid);
    }

    public async Task RunAsync(IPEndPoint endpoint, CancellationToken cancellationToken, Action ready)
    {
        using var socket = new UdpClient(endpoint);
        uptime.Restart();
        ready();
        try
        {
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
        finally { uptime.Stop(); }
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
        if (!community.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(profile.Community))) return null;
        var tag = message.PeekTag();
        if (tag.TagClass != TagClass.ContextSpecific || tag.TagValue is not (0 or 1 or 3 or 5)) return null;
        if (version == 0 && tag.TagValue == 5) return null;
        var pdu = message.ReadSequence(tag); message.ThrowIfNotEmpty();
        var id = (int)pdu.ReadInteger();
        var field1 = (int)pdu.ReadInteger(); var field2 = (int)pdu.ReadInteger();
        var bindings = pdu.ReadSequence(); pdu.ThrowIfNotEmpty();
        var requests = new List<(string Oid, byte[] Value)>();
        while (bindings.HasData)
        {
            if (requests.Count >= 256) return null;
            var binding = bindings.ReadSequence();
            var oid = binding.ReadObjectIdentifier(); var value = binding.ReadEncodedValue().ToArray();
            binding.ThrowIfNotEmpty(); requests.Add((oid, value));
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
                    if (output.Count >= 256) return Encode(version, community, id, 1, 0, []);
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
                    lookup.TryGetValue(oid, out var item) ? (oid, item.Encode(uptime.Elapsed)) : (oid, Missing(oid));
                if (version == 0 && result.Item2[0] is 0x80 or 0x81 or 0x82)
                {
                    error = 2; index = i + 1; output.Clear(); output.AddRange(requests); break;
                }
                output.Add(result);
            }
        }
        var response = Encode(version, community, id, error, index, output);
        return response.Length <= 1400 ? response : Encode(version, community, id, 1, 0, version == 0 ? requests : []);
    }

    private byte[] Missing(string oid)
    {
        // Scalar instances have an explicit .0 suffix in the profile.
        var instanceMissing = objects.Any(o => o.Oid.EndsWith(".0", StringComparison.Ordinal) &&
            (oid == o.Oid[..^2] || oid.StartsWith(o.Oid[..^1], StringComparison.Ordinal)));
        return [(byte)(instanceMissing ? 0x81 : 0x80), 0];
    }

    private (string Oid, byte[] Value) Next(string oid, int version)
    {
        var lo = 0; var hi = objects.Length;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (OidComparer.Instance.Compare(objects[mid].Oid, oid) <= 0) lo = mid + 1; else hi = mid;
        }
        return lo < objects.Length ? (objects[lo].Oid, objects[lo].Encode(uptime.Elapsed)) : (oid, [0x82, 0]);
    }

    private static byte[] Encode(int version, byte[] community, int id, int error, int index, IEnumerable<(string Oid, byte[] Value)> values)
    {
        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        {
            writer.WriteInteger(version); writer.WriteOctetString(community);
            using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 2, true)))
            {
                writer.WriteInteger(id); writer.WriteInteger(error); writer.WriteInteger(index);
                using (writer.PushSequence())
                    foreach (var value in values)
                        using (writer.PushSequence()) { writer.WriteObjectIdentifier(value.Oid); writer.WriteEncodedValue(value.Value); }
            }
        }
        return writer.Encode();
    }
}
