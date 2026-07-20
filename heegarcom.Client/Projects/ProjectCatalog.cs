namespace heegarcom.Client.Projects;

// Discovers every IProjectSource in this assembly, so new card files under
// Projects/Cards are picked up automatically — no central list to edit.
public static class ProjectCatalog
{
    public static IReadOnlyList<ProjectItem> All { get; } = Discover();

    private static IReadOnlyList<ProjectItem> Discover()
    {
        var contract = typeof(IProjectSource);
        return contract.Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && contract.IsAssignableFrom(t))
            .Select(t => (IProjectSource)Activator.CreateInstance(t)!)
            .OrderBy(s => s.Order)
            .Select(s => s.Card)
            .ToList();
    }
}
