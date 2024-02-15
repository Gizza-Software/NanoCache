namespace NanoCache;

internal static class BinaryHelpers
{
    public static byte[] Serialize(object data)
    {
        var ms = new MemoryStream();
        using (var writer = new BsonWriter(ms))
        {
            var serializer = new JsonSerializer();
            serializer.Serialize(writer, data);
        }

        return ms.ToArray();
    }

    public static T Deserialize<T>(byte[] data) where T : new()
    {
        var ms = new MemoryStream(data);
        using (var reader = new BsonReader(ms))
        {
            var serializer = new JsonSerializer();
            return serializer.Deserialize<T>(reader);
        }
    }
}
