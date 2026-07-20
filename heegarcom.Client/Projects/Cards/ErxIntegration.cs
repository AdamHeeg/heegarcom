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
        Problem = "Providers need to write prescriptions to a legal prescribing entity.",
        Built = "Updated existing VB6 medication prescription framework.  This included customizations to logic flow to support early VB6 design decisions.",
        Outcome = "Highly testable and supported backend API to the Ensora ERx framework enabling e-prescribing and monitoring of prescriptions.",
    };
}
