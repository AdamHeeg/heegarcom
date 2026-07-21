namespace heegarcom.Client.Projects;

public sealed class TribeTime : IProjectSource
{
    public int Order => 15;

    public ProjectItem Card => new()
    {
        Title = "TribeTime",
        Summary = "A cross-platform mobile app for coordinating group events — shared calendars, \"tribes\", and notifications. Built solo, end to end.",
        Tags = new[] { "React Native", "Expo", "TypeScript", "Supabase", "Mobile" },
        Kind = "Case Study",
        Problem = "Groups — families, teams, friend circles — need a simple shared way to plan events and keep everyone in the loop.",
        Built = "A cross-platform Expo / React Native app with Supabase auth and data: group \"tribes\", a shared event calendar, notifications, and light/dark theming — one codebase targeting iOS, Android, and web.",
        Outcome = "[Fill in: platforms shipped, users, and what you took from building a full app solo.]",
        Metrics = new[]
        {
            new MetricItem { Value = "3", Label = "platforms, one codebase" },
            new MetricItem { Value = "Solo", Label = "designed + built" },
        },
    };
}
