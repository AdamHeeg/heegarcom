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
        Problem = "[Fill in: the clinical/documentation burden this addressed.]",
        Built = "Agentic AI workflows that generate EMR documents. [Fill in: models, orchestration, guardrails, review steps.]",
        Outcome = "[Fill in: documents automated, time saved, accuracy/adoption.]",
    };
}
