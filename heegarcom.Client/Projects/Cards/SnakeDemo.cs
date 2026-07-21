namespace heegarcom.Client.Projects;

public sealed class SnakeDemo : IProjectSource
{
    public int Order => 80;

    public ProjectItem Card => new()
    {
        Title = "Snake",
        Summary = "The classic game, written in C# and running in the browser via Blazor WebAssembly — no JavaScript.",
        Tags = new[] { "Blazor WASM", "C# / .NET", "Game" },
        Kind = "Demo",
        Url = "/demos/snake",
    };
}
