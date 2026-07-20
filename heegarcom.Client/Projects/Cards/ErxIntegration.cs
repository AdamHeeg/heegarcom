namespace heegarcom.Client.Projects;

public sealed class ErxIntegration : IProjectSource
{
    public int Order => 40;

    public ProjectItem Card => new()
    {
        Title = "eRx Integration",
        Summary = "Electronic prescribing integration connecting the EMR to an external eRx network.",
        Tags = new[] { "Integration", "eRx", "C# / .NET", "Healthcare", "API" },
        Kind = "Case Study",
        Problem = "[Fill in: the prescribing / interoperability need.]",
        Built = "[Fill in: endpoints, message formats, error handling, compliance.]",
        Outcome = "[Fill in: throughput, reliability, providers served.]",
    };
}
