namespace CS2AdminTool.Models;

public class ServerHealthSnapshot
{
    public double TickRateVariance { get; set; }
    public double ChokePercent { get; set; }
    public double LossPercent { get; set; }
    public int BotCount { get; set; }
    public string Alert { get; set; } = "Healthy";
}
