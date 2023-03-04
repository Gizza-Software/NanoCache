namespace NanoCache.Enums;

public enum NanoOperation : byte
{
    Ping = 1,
    Login = 2,
    Logout = 3,
    Failed = 4,
    Timeout = 5,

    Set = 11,
    Get = 12,
    Refresh = 13,
    Remove = 14,
}
