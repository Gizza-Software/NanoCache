namespace NanoCache.Models;

public class NanoUserCredentials
{
    public string Username { get; set; }
    public string Password { get; set; }

    public NanoUserCredentials()
    {
    }

    public NanoUserCredentials(string username, string password)
    {
        this.Username = username;
        this.Password = password;
    }
}
