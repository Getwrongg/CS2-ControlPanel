namespace CS2AdminTool.Models;

public class SavedServerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 27015;
    public string Password { get; set; } = string.Empty;
}
