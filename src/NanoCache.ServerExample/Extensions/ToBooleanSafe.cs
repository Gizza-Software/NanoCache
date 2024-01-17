namespace NanoCache.ServerExample.Extensions;

public static partial class ExtensionMethods
{
    public static bool ToBooleanSafe(this object @this)
    {
        var val = @this.ToStringSafe().ToLower().Trim().Replace("0", "").Replace(".", "").Replace(",", "");
        if (val == "" || val == "false" || val == "f" || val == "no" || val == "n" || val == "0")
            return false;

        return true;
    }

}