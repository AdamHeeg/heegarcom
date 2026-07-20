namespace heegarcom.Client.Projects;

public sealed class AiIntegration : IProjectSource
{
    public int Order => 30;

    public ProjectItem Card => new()
    {
        Title = "AI Integration",
        Summary = "Agentic AI workflows built into an EMR for automated document generation.",
        Tags = new[] { "AI", "C# / .NET", "Healthcare", "Automation" },
        Kind = "Case Study",
        Problem = "Leverage AI into medical documentation workflows.",
        Built = "Agentic AI workflows that generate EMR documents. <br /> <ul><li>Version 1: inline I-Frame linked to provider.</li><li>Version 2: API calls to custom document generation managed in house.</li><li>Version 3: Agentic AI calls built through custom agents run in order to specialize in different tasks creating well definted specialized content.</li></ul>",
        Outcome = "[Fill in: documents automated, time saved, accuracy/adoption.]",
    };
}
