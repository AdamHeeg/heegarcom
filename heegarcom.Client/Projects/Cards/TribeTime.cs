namespace heegarcom.Client.Projects;

public sealed class TribeTime : IProjectSource
{
    public int Order => 15;

    public ProjectItem Card => new()
    {
        Title = "TribeTime - AI Assisted",
        Summary = "A cross-platform mobile app for coordinating group events — shared calendars, \"tribes\", and notifications. Built solo, end to end.",
        Tags = new[] { "React Native", "Expo", "TypeScript", "Supabase", "Mobile" },
        Kind = "Case Study",
        Url = "/projects/tribetime",
        LinkText = "Screenshots",
        Problem = "Groups need reliable ways to send broadcast notifications without starting group chats!  Further, it's easy to have reminders and events get burried on our phones.  This can be solved with a broadcast application open to multiple groups, or tribes.",
        Built = "A cross-platform Expo / React Native app with Supabase auth and data: group \"tribes\", a shared event calendar, notifications, and light/dark theming — one codebase targeting iOS, Android, and web.",
        Outcome = "A successful communication tool to broadcast calendar events and notifications from a tribe leader to all members.  The notifications can be acknowledged and deleted.  Calendar events can be sync'd with host phone system.  Wonderful lightweight solution to easy group communication with support for color coded tribes.",
        Metrics = new[]
        {
            new MetricItem { Value = "3", Label = "platforms, one codebase" },
            new MetricItem { Value = "Solo + AI", Label = "designed + built" },
        },
    };
}
