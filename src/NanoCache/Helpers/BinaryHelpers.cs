namespace NanoCache.Helpers;

internal static class BinaryHelpers
{
    public static byte[] Serialize(object data)
    {
        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
    }

    public static T Deserialize<T>(byte[] data)
    {
        return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(data));
    }
}
