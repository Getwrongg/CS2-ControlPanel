namespace CS2AdminTool.Models;

public class RunnerOptions
{
    public int CommandDelayMs { get; set; } = 250;
    public bool ContinueOnCommandFailure { get; set; }
    public bool AllowBlankCommands { get; set; }
}
