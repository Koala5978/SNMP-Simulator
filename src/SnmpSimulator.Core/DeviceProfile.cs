using System.Formats.Asn1;
using System.Text.Json;

namespace SnmpSimulator.Core;

public sealed record DeviceProfile(int SchemaVersion, string Name, string Community, List<OidObject> Objects)
{
    public static DeviceProfile Load(string path) => Parse(File.ReadAllText(path));

    public static DeviceProfile Parse(string json)
    {
        var profile = JsonSerializer.Deserialize<DeviceProfile>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException("設定檔不可為空。");
        if (profile.SchemaVersion != 1 || string.IsNullOrWhiteSpace(profile.Name) ||
            string.IsNullOrEmpty(profile.Community) || profile.Objects is not { Count: > 0 })
            throw new InvalidDataException("需要 schemaVersion=1、name、community 及至少一個 OID。");
        if (profile.Objects.Count > 100_000) throw new InvalidDataException("OID 數量超出第一版限制。");
        var seen = new HashSet<string>();
        foreach (var item in profile.Objects)
        {
            if (string.IsNullOrEmpty(item.Oid)) throw new InvalidDataException("OID 不可為空。");
            var canonical = string.Join('.', item.Oid.Split('.').Select(s => uint.Parse(s, System.Globalization.CultureInfo.InvariantCulture)));
            if (canonical != item.Oid || !seen.Add(canonical)) throw new InvalidDataException($"重複或非標準 OID：{item.Oid}");
            var writer = new AsnWriter(AsnEncodingRules.BER);
            writer.WriteObjectIdentifier(item.Oid);
            item.Encode(TimeSpan.Zero); // Validate type and value before starting the listener.
        }
        return profile;
    }
}

public sealed record OidObject(string Oid, string Type, JsonElement Value)
{
    public byte[] Encode(TimeSpan elapsed)
    {
        var writer = new AsnWriter(AsnEncodingRules.BER);
        switch (Type)
        {
            case "Integer32": writer.WriteInteger(Value.GetInt32()); break;
            case "OctetString": writer.WriteOctetString(System.Text.Encoding.UTF8.GetBytes(Value.GetString() ?? throw new InvalidDataException("字串不可為 null。"))); break;
            case "ObjectIdentifier": writer.WriteObjectIdentifier(Value.GetString() ?? ""); break;
            case "Counter32": writer.WriteInteger(Value.GetUInt32(), new Asn1Tag(TagClass.Application, 1)); break;
            case "Gauge32": writer.WriteInteger(Value.GetUInt32(), new Asn1Tag(TagClass.Application, 2)); break;
            case "TimeTicks": writer.WriteInteger(Value.GetUInt32(), new Asn1Tag(TagClass.Application, 3)); break;
            case "Uptime": writer.WriteInteger(unchecked((uint)(long)(elapsed.TotalMilliseconds / 10)), new Asn1Tag(TagClass.Application, 3)); break;
            default: throw new InvalidDataException($"不支援的型別：{Type}");
        }
        return writer.Encode();
    }
}

internal sealed class OidComparer : IComparer<string>
{
    public static readonly OidComparer Instance = new();
    public int Compare(string? x, string? y)
    {
        var a = x!.Split('.'); var b = y!.Split('.');
        for (var i = 0; i < Math.Min(a.Length, b.Length); i++)
        {
            var result = uint.Parse(a[i]).CompareTo(uint.Parse(b[i]));
            if (result != 0) return result;
        }
        return a.Length.CompareTo(b.Length);
    }
}
