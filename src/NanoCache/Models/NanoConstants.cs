namespace NanoCache.Models;

internal static class NanoConstants
{
    internal static MessagePackSerializerOptions MessagePackOptions = MessagePackSerializerOptions.Standard;
    internal static MessagePackSerializerOptions MessagePackOptionsWithCompression = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4Block);
}
