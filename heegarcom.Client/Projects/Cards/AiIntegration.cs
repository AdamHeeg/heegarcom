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
        Built = @"Agentic AI workflows that generate EMR documents. <br /> 
        <ul>
        <li><strong>Version 1:</strong> inline I-Frame linked to provider and patient with import logic.</li>
        <li><strong>Version 2:</strong> API calls to custom medical document generation managed in house.</li>
        <li><strong>Version 3:</strong> Agentic AI calls built through custom agents run in order to specialize in different tasks creating well definted specialized content.</li>
        <li><strong>Version 4:</strong> Added a new free tier and a high end tier.  The high end version tied in coding and medications with current published lists for effortless integrations to both billing and prescribing.</li>
        </ul>",
        Outcome = "Seamlessly built and transitioned 4 versions of AI in a live production EMR to enable not only enhanced user experience, but also allow multiple pricing tiers and functionality.",
    };
}
