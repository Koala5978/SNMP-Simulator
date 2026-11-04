using System.Formats.Asn1;
using System.Text.Json;

namespace SnmpSimulator.Core;

public sealed record DeviceProfile(int SchemaVersion, string Name, string Community, List<OidObject> Objects)
{
    public const int MaxFileBytes = 16 * 1024 * 1024;
    public static DeviceProfile Load(string path) => LoadAsync(path).GetAwaiter().GetResult();
    public static async Task<DeviceProfile> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        await using var file = File.OpenRead(path);
        if (file.Length > MaxFileBytes) throw new InvalidDataException("設定檔超過 16 MiB。");
        using var data = new MemoryStream();
        var buffer = new byte[8192];
        int count;
        while ((count = await file.ReadAsync(buffer, cancellationToken)) != 0)
        {
            if (data.Length + count > MaxFileBytes) throw new InvalidDataException("設定檔超過 16 MiB。");
            await data.WriteAsync(buffer.AsMemory(0, count), cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return Parse(System.Text.Encoding.UTF8.GetString(data.ToArray()).TrimStart('\uFEFF'));
    }

    public static DeviceProfile Parse(string json)
    {
        if (System.Text.Encoding.UTF8.GetByteCount(json) > MaxFileBytes) throw new InvalidDataException("設定檔超過 16 MiB。");
        try
        {
            var profile = JsonSerializer.Deserialize<DeviceProfile>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidDataException("設定檔不可為空。");
            if (profile.SchemaVersion != 1 || string.IsNullOrWhiteSpace(profile.Name) ||
                string.IsNullOrEmpty(profile.Community) || profile.Objects is not { Count: > 0 })
                throw new InvalidDataException("需要 schemaVersion=1、name、community 及至少一個 OID。");
            if (profile.Objects.Count > 100_000) throw new InvalidDataException("OID 數量超出第一版限制。");
            var seen = new HashSet<string>();
            for (var index = 0; index < profile.Objects.Count; index++)
            {
                var item = profile.Objects[index];
                try
                {
                    if (item is null) throw new InvalidDataException("物件不可為 null。");
                    if (string.IsNullOrEmpty(item.Oid)) throw new InvalidDataException("OID 不可為空。");
                    if (item.Oid.Split('.').Length is < 2 or > 128) throw new InvalidDataException("OID 必須有 2～128 段。");
                    var canonical = string.Join('.', item.Oid.Split('.').Select(s => uint.Parse(s, System.Globalization.CultureInfo.InvariantCulture)));
                    if (canonical != item.Oid || !seen.Add(canonical)) throw new InvalidDataException($"重複或非標準 OID：{item.Oid}");
                    var writer = new AsnWriter(AsnEncodingRules.BER);
                    writer.WriteObjectIdentifier(item.Oid);
                    item.Encode(TimeSpan.Zero);
                }
                catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException or InvalidDataException)
                { throw new InvalidDataException($"objects[{index}]：{ex.Message}", ex); }
            }
            return profile;
        }
        catch (JsonException ex) { throw new InvalidDataException($"JSON {ex.Path}：{ex.Message}", ex); }
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
