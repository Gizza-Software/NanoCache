namespace NanoCache.Models;

internal class NanoClient
{
    public bool LoggedIn { get; set; }
    public string ConnectionId { get; set; }
    public NanoUserOptions Options { get; set; }

    public NanoClient(bool loggedIn, string connectionId)
    {
        this.LoggedIn = loggedIn;
        this.ConnectionId = connectionId;
        this.Options = new NanoUserOptions();
    }

    public void Login(NanoUserOptions options)
    {
        this.LoggedIn = true;
        this.Options = new NanoUserOptions
        {
            Username = options.Username,
            Password = options.Password,
            Instance = options.Instance,
            DefaultAbsoluteExpiration = options.DefaultAbsoluteExpiration,
            DefaultAbsoluteExpirationRelativeToNow = options.DefaultAbsoluteExpirationRelativeToNow,
            DefaultSlidingExpiration = options.DefaultSlidingExpiration,
        };
    }

    public void Logout()
    {
        this.LoggedIn = false;
        this.Options = null;
    }
}
