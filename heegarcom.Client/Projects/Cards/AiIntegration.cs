namespace heegarcom.Client.Projects;

public sealed class AiIntegration : IProjectSource
{
    public int Order => 30;

    public ProjectItem Card => new()
    {
        Title = "AI Integration",
        Summary = "Agentic AI workflows built into an EMR for automated document generation and intergrated prescribing and billing.",
        Tags = new[] { "AI", "C# / .NET", "Healthcare", "Automation" },
        Kind = "Case Study",
        Problem = "Leverage AI into medical documentation workflows.",
        Built = "Agentic AI workflows that generate EMR documents. <br /> <ul><li>Version 1: inline I-Frame linked to provider.</li><li>Version 2: API calls to custom document generation managed in house.</li><li>Version 3: Agentic AI calls built through custom agents run in order to specialize in different tasks creating well definted specialized content.</li><li>Version 4: Added a new free tier and a high end tier.  The high end version tied in coding and medications with current published lists for effortless integrations to both billing and prescribing.</li></ul>",
        Outcome = "Seamlessly built and transitioned 4 versions of AI in a live production EMR to enable not only enhanced user experience, but also allow multiple pricing tiers and functionality.",
    };
}
