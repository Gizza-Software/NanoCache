namespace NanoCache;

internal static class BinaryHelpers
{
    public static byte[] Serialize(object data)
    {
        var ms = new MemoryStream();
#pragma warning disable CS0618 // Type or member is obsolete
        using var writer = new BsonWriter(ms);
        var serializer = new JsonSerializer();
        serializer.Serialize(writer, data);
#pragma warning restore CS0618 // Type or member is obsolete

        return ms.ToArray();
    }

    public static T Deserialize<T>(byte[] data) where T : new()
    {
        var ms = new MemoryStream(data);
#pragma warning disable CS0618 // Type or member is obsolete
        using var reader = new BsonReader(ms);
        var serializer = new JsonSerializer();
        return serializer.Deserialize<T>(reader);
#pragma warning restore CS0618 // Type or member is obsolete
    }
}
