using System.Formats.Asn1;
using System.Net;
using System.Net.Sockets;
using System.Text;
using SnmpSimulator.Core;

var path = args.Length > 0 ? args[0] : "samples/device.json";
var profile = DeviceProfile.Load(path);
var agent = new SnmpAgent(profile);
var checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception($"FAIL: {name}");
    Console.WriteLine($"PASS: {name}"); checks++;
}
byte[] Request(int version, int operation, string[] oids, string community = "public", int first = 0, int second = 0)
{
    var w = new AsnWriter(AsnEncodingRules.BER);
    using (w.PushSequence())
    {
        w.WriteInteger(version); w.WriteOctetString(Encoding.UTF8.GetBytes(community));
        using (w.PushSequence(new Asn1Tag(TagClass.ContextSpecific, operation, true)))
        {
            w.WriteInteger(42); w.WriteInteger(first); w.WriteInteger(second);
            using (w.PushSequence()) foreach (var oid in oids)
                using (w.PushSequence()) { w.WriteObjectIdentifier(oid); w.WriteNull(); }
        }
    }
    return w.Encode();
}
(int Error, int Index, List<(string Oid, byte[] Value)> Values) Parse(byte[] bytes)
{
    var reader = new AsnReader(bytes, AsnEncodingRules.BER);
    var m = reader.ReadSequence(); reader.ThrowIfNotEmpty();
    m.ReadInteger(); m.ReadOctetString();
    var pdu = m.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 2, true)); m.ThrowIfNotEmpty();
    if (pdu.ReadInteger() != 42) throw new Exception("Request ID mismatch");
    var error = (int)pdu.ReadInteger(); var index = (int)pdu.ReadInteger(); var list = pdu.ReadSequence(); pdu.ThrowIfNotEmpty();
    var result = new List<(string, byte[])>();
    while (list.HasData) { var b = list.ReadSequence(); result.Add((b.ReadObjectIdentifier(), b.ReadEncodedValue().ToArray())); b.ThrowIfNotEmpty(); }
    return (error, index, result);
}
var nameOid = "1.3.6.1.2.1.1.5.0";
foreach (var version in new[] { 0, 1 })
{
    var response = Parse(agent.Respond(Request(version, 0, [nameOid]))!);
    Check(response.Error == 0 && Encoding.UTF8.GetString(new AsnReader(response.Values[0].Value, AsnEncodingRules.BER).ReadOctetString()) == "demo-switch-01", $"v{version + 1} GET returns configured value");
    var next = Parse(agent.Respond(Request(version, 1, ["1.3.6.1.2.1.1.1.0"]))!);
    Check(next.Values[0].Oid == "1.3.6.1.2.1.1.2.0", $"v{version + 1} GETNEXT");
    var set = Parse(agent.Respond(Request(version, 3, [nameOid]))!);
    Check(set.Error == (version == 0 ? 4 : 17) && set.Index == 1, $"v{version + 1} SET rejected");
}
var missing = "1.3.6.1.2.1.99.0";
var v1missing = Parse(agent.Respond(Request(0, 0, [nameOid, missing]))!);
Check(v1missing.Error == 2 && v1missing.Index == 2 && v1missing.Values.Count == 2, "v1 noSuchName and original bindings");
var v2missing = Parse(agent.Respond(Request(1, 0, [missing]))!);
Check(v2missing.Error == 0 && v2missing.Values[0].Value[0] == 0x80, "v2c noSuchObject");
var noInstance = Parse(agent.Respond(Request(1, 0, ["1.3.6.1.2.1.1.5.1"]))!);
Check(noInstance.Values[0].Value[0] == 0x81, "v2c noSuchInstance");
var end = Parse(agent.Respond(Request(1, 1, ["2.100"]))!);
Check(end.Values[0].Value[0] == 0x82, "v2c endOfMibView");
var v1end = Parse(agent.Respond(Request(0, 1, ["2.100"]))!);
Check(v1end.Error == 2 && v1end.Index == 1, "v1 end of MIB");
var bulk = Parse(agent.Respond(Request(1, 5, ["1.3.6.1.2.1"], second: 10))!);
Check(bulk.Error == 0 && bulk.Values.Count == 8 && bulk.Values[^1].Value[0] == 0x82, "v2c GETBULK walks all seven OIDs and terminates");
Check(agent.Respond(Request(0, 5, [nameOid])) is null, "v1 GETBULK discarded");
Check(agent.Respond(Request(1, 0, [nameOid], "wrong")) is null, "wrong community discarded");
Check(agent.Respond(new byte[] { 0x30, 0xFF }) is null, "malformed BER discarded");
var tooBig = Parse(agent.Respond(Request(1, 0, Enumerable.Repeat(nameOid, 100).ToArray()))!);
Check(tooBig.Error == 1 && tooBig.Values.Count == 0, "oversized response returns tooBig");
// Exercise the actual UDP listener, then cancel and restart the same endpoint.
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
using var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
var endpoint = (IPEndPoint)reservation.Client.LocalEndPoint!; reservation.Close();
for (var iteration = 0; iteration < 2; iteration++)
{
    using var stop = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
    var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var run = agent.RunAsync(endpoint, stop.Token, () => ready.SetResult());
    await ready.Task.WaitAsync(timeout.Token);
    using var client = new UdpClient();
    await client.SendAsync(Request(1, 0, [nameOid]), endpoint, timeout.Token);
    var packet = await client.ReceiveAsync(timeout.Token);
    Check(Parse(packet.Buffer).Error == 0, $"UDP request/response, startup {iteration + 1}");
    await Task.Delay(30, timeout.Token);
    var ticks = Parse(agent.Respond(Request(1, 0, ["1.3.6.1.2.1.1.3.0"]))!).Values[0].Value;
    Check(new AsnReader(ticks, AsnEncodingRules.BER).ReadInteger(new Asn1Tag(TagClass.Application, 3)) > 0, $"uptime increases, startup {iteration + 1}");
    stop.Cancel(); await run;
}
Console.WriteLine($"Completed {checks} checks.");
