using MessagePack;
using System;

namespace NanoCache.Models
{
    [MessagePackObject]
    public class NanoUserOptions
    {
        [Key(1)]
        public string Username { get; set; }

        [Key(2)]
        public string Password { get; set; }

        [Key(3)]
        public string Instance { get; set; }

        [Key(4)]
        public bool UseCompression { get; set; }

        [Key(5)]
        public DateTimeOffset? DefaultAbsoluteExpiration { get; set; }

        [Key(6)]
        public TimeSpan? DefaultAbsoluteExpirationRelativeToNow { get; set; }

        [Key(7)]
        public TimeSpan? DefaultSlidingExpiration { get; set; }
    }
}
