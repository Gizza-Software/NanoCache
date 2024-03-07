namespace NanoCache;

public static class NanoCacheConstants
{
    // IPWorks
    public static string IPWorksNode { get => "9Y1BJ5JC"; }
    public static string IPWorksSerial { get => "C1XM7VYVFR0V8FIZ44FZIW"; }
    public static string IPWorksKey { get => "H99M2ZE8DD8P"; }
    public static string IPWorksRuntimeKey { get => "4331584D37565956465230563846495A3434465A4957000000000000000000000000000000000000395931424A354A4300004839394D325A4538444438500000"; }

    // Socket
    public static byte[] PacketHeader { get; set; } = [0xF1, 0xF2];
    public static SocketSecurity SocketSecurity { get; set; } = SocketSecurity.None;

}