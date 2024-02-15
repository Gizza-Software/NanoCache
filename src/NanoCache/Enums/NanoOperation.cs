namespace NanoCache;

public enum NanoOperation : byte
{
    Ping = 1,
    Login = 2,
    Logout = 3,
    Failed = 4,
    Timeout = 5,

    Set = 11,
    Get = 12,
    Remove = 13,
    Refresh = 14,
}
