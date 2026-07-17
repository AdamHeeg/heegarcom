using heegarcom.Components;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(heegarcom.Client._Imports).Assembly);

// ICD-10 search API for the mapper demo. Read-only query against the local SQLite DB.
app.MapGet("/api/icd/search", (string? q, IWebHostEnvironment env) =>
{
    var term = (q ?? "").Trim();
    if (term.Length < 2)
        return Results.Ok(Array.Empty<object>());

    var dbPath = Path.Combine(env.ContentRootPath, "Data", "terminology.db");
    var codeTerm = term.Replace(".", "").ToUpperInvariant();

    using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    // Match by code prefix or description text.
    var matches = new List<(string Code, string Dotted, string Description)>();
    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
            SELECT code, code_dotted, description
            FROM icd10
            WHERE code LIKE $code || '%' OR description LIKE '%' || $desc || '%'
            ORDER BY code
            LIMIT 50";
        cmd.Parameters.AddWithValue("$code", codeTerm);
        cmd.Parameters.AddWithValue("$desc", term);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            matches.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2)));
    }

    // Attach any SNOMED mappings for the matched codes (table empty until data is loaded).
    var snomedByCode = new Dictionary<string, List<SnomedItem>>();
    if (matches.Count > 0)
    {
        using var cmd = conn.CreateCommand();
        var names = matches.Select((_, i) => "$c" + i).ToArray();
        cmd.CommandText = $"SELECT icd10_code, snomed_id, snomed_term FROM snomed_map WHERE icd10_code IN ({string.Join(",", names)})";
        for (var i = 0; i < matches.Count; i++)
            cmd.Parameters.AddWithValue(names[i], matches[i].Code);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var code = reader.GetString(0);
            if (!snomedByCode.TryGetValue(code, out var list))
                snomedByCode[code] = list = new List<SnomedItem>();
            list.Add(new SnomedItem(reader.GetString(1), reader.GetString(2)));
        }
    }

    var response = matches.Select(m => new IcdResult(
        m.Dotted,
        m.Description,
        snomedByCode.TryGetValue(m.Code, out var s) ? s : new List<SnomedItem>()));

    return Results.Ok(response);
});

app.Run();

record IcdResult(string Code, string Description, List<SnomedItem> Snomed);
record SnomedItem(string Id, string Term);
