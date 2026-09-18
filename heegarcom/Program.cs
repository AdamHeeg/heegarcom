using System.Text.Json;
using System.Threading.RateLimiting;
using heegarcom.Components;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.Sqlite;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

// Trust X-Forwarded-For/-Proto so the referral endpoints see the real client IP when the app runs
// behind a proxy/CDN. NOTE: clearing known proxies trusts these headers from any source (a client
// could spoof its IP). Acceptable for lead metadata; restrict to known proxies before ever using
// the IP for a security decision.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Per-IP rate limit for the public form endpoints — throttles bot floods with no user friction.
// These are low-volume forms, so a small window is plenty; excess submissions get HTTP 429.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("form-submit", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(5),
                QueueLimit = 0
            }));
});

// Used by the NPI lookup endpoint to call the external CMS registry server-side (via IHttpClientFactory).
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseForwardedHeaders();
app.UseRateLimiter();

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

// Bidirectional ICD-10 <-> SNOMED search over the local SQLite DB (read-only).
// Pass either ?icd=... or ?snomed=...; returns the matching mapped pairs.
app.MapGet("/api/terminology/search", (string? icd, string? snomed, IWebHostEnvironment env) =>
{
    var byIcd = (icd ?? "").Trim();
    var bySnomed = (snomed ?? "").Trim();
    var icdMode = byIcd.Length >= 2;
    var term = icdMode ? byIcd : bySnomed;
    if (term.Length < 2)
        return Results.Ok(Array.Empty<object>());

    var dbPath = Path.Combine(env.ContentRootPath, "Data", "terminology.db");
    using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
    conn.Open();

    using var cmd = conn.CreateCommand();
    if (icdMode)
    {
        cmd.CommandText = @"
            SELECT i.code_dotted, i.description, c.snomed_id, c.term
            FROM icd_snomed_map m
            JOIN icd10 i ON i.code = m.icd10_code
            JOIN snomed_concept c ON c.snomed_id = m.snomed_id
            WHERE i.code LIKE $code || '%' OR i.description LIKE '%' || $q || '%'
            ORDER BY i.code, c.term
            LIMIT 100";
        cmd.Parameters.AddWithValue("$code", term.Replace(".", "").ToUpperInvariant());
        cmd.Parameters.AddWithValue("$q", term);
    }
    else
    {
        cmd.CommandText = @"
            SELECT i.code_dotted, i.description, c.snomed_id, c.term
            FROM icd_snomed_map m
            JOIN icd10 i ON i.code = m.icd10_code
            JOIN snomed_concept c ON c.snomed_id = m.snomed_id
            WHERE c.snomed_id LIKE $q || '%' OR c.term LIKE '%' || $q || '%'
            ORDER BY c.term, i.code
            LIMIT 100";
        cmd.Parameters.AddWithValue("$q", term);
    }

    var rows = new List<MapRow>();
    using var reader = cmd.ExecuteReader();
    while (reader.Read())
        rows.Add(new MapRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3)));

    return Results.Ok(rows);
});

// NPI provider lookup — proxies the public CMS NPI Registry API server-side (the CMS API sends no CORS
// headers, so the WASM client cannot call it directly). Pass first/last name and/or a 2-letter state.
app.MapGet("/api/npi/search", async (string? firstName, string? lastName, string? state, IHttpClientFactory httpFactory) =>
{
    var first = (firstName ?? "").Trim();
    var last = (lastName ?? "").Trim();
    var st = (state ?? "").Trim();

    // CMS requires at least one criterion; require a last name or a state so results stay meaningful.
    if (last.Length < 2 && st.Length == 0)
        return Results.Ok(Array.Empty<NpiResult>());

    var query = new List<string> { "version=2.1", "limit=50" };
    // First name is a "starts with" search: append the CMS wildcard (it needs >=2 chars before the '*').
    // A 1-char prefix is left off the API call and enforced by the local filter below instead.
    if (first.Length >= 2) query.Add("first_name=" + Uri.EscapeDataString(first) + "*");
    if (last.Length > 0) query.Add("last_name=" + Uri.EscapeDataString(last));
    if (st.Length > 0) query.Add("state=" + Uri.EscapeDataString(st));
    var url = "https://npiregistry.cms.hhs.gov/api/?" + string.Join("&", query);

    var client = httpFactory.CreateClient();
    string json;
    try
    {
        json = await client.GetStringAsync(url);
    }
    catch
    {
        return Results.Ok(Array.Empty<NpiResult>());
    }

    var results = new List<NpiResult>();
    using var doc = JsonDocument.Parse(json);
    if (!doc.RootElement.TryGetProperty("results", out var arr) || arr.ValueKind != JsonValueKind.Array)
        return Results.Ok(results);

    foreach (var item in arr.EnumerateArray())
    {
        var number = item.TryGetProperty("number", out var n) ? n.ToString() : "";
        var enumType = item.TryGetProperty("enumeration_type", out var et) ? (et.GetString() ?? "") : "";
        var isOrg = enumType == "NPI-2";
        var typeLabel = isOrg ? "Organization" : "Individual";

        var name = "";
        var status = "";
        var also = "";
        var firstNm = "";
        if (item.TryGetProperty("basic", out var basic))
        {
            if (isOrg)
            {
                name = basic.TryGetProperty("organization_name", out var org) ? (org.GetString() ?? "") : "";
            }
            else
            {
                var fn = basic.TryGetProperty("first_name", out var f) ? (f.GetString() ?? "") : "";
                var ln = basic.TryGetProperty("last_name", out var l) ? (l.GetString() ?? "") : "";
                var cred = basic.TryGetProperty("credential", out var c) ? (c.GetString() ?? "") : "";
                firstNm = fn;
                name = (fn + " " + ln).Trim();
                if (cred.Length > 0) name += ", " + cred;

                // The CMS API also matches former/other names, so a record can surface under a name that
                // isn't the current one. If the current name doesn't match what was typed, find the
                // other_names entry that did and surface it, so the row explains why it appeared.
                var currentMatches = (last.Length == 0 || ln.StartsWith(last, StringComparison.OrdinalIgnoreCase))
                                  && (first.Length == 0 || fn.StartsWith(first, StringComparison.OrdinalIgnoreCase));
                if (!currentMatches && item.TryGetProperty("other_names", out var others) && others.ValueKind == JsonValueKind.Array)
                {
                    foreach (var o in others.EnumerateArray())
                    {
                        var ofn = o.TryGetProperty("first_name", out var of) ? (of.GetString() ?? "") : "";
                        var oln = o.TryGetProperty("last_name", out var ol) ? (ol.GetString() ?? "") : "";
                        var otype = o.TryGetProperty("type", out var ot) ? (ot.GetString() ?? "") : "";
                        var oLastOk = last.Length == 0 || oln.StartsWith(last, StringComparison.OrdinalIgnoreCase);
                        var oFirstOk = first.Length == 0 || ofn.StartsWith(first, StringComparison.OrdinalIgnoreCase);
                        if (oLastOk && oFirstOk)
                        {
                            var full = (ofn + " " + oln).Trim();
                            also = otype.Contains("Former", StringComparison.OrdinalIgnoreCase) ? "formerly " + full : "also " + full;
                            break;
                        }
                    }
                }
            }
            var rawStatus = basic.TryGetProperty("status", out var s) ? (s.GetString() ?? "") : "";
            status = rawStatus == "A" ? "Active" : rawStatus;
        }

        // Enforce a strict "starts with" on the first name the user typed — covers the 1-char case the CMS
        // wildcard can't, and drops any first-name aliases the API adds. Organizations have no first name,
        // so a first-name search excludes them.
        if (first.Length > 0 && !firstNm.StartsWith(first, StringComparison.OrdinalIgnoreCase))
            continue;

        // Primary taxonomy is the provider's listed specialty; fall back to the first if none is flagged primary.
        var specialty = "";
        if (item.TryGetProperty("taxonomies", out var taxes) && taxes.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in taxes.EnumerateArray())
            {
                var desc = t.TryGetProperty("desc", out var d) ? (d.GetString() ?? "") : "";
                var isPrimary = t.TryGetProperty("primary", out var p) && p.ValueKind == JsonValueKind.True;
                if (isPrimary) { specialty = desc; break; }
                if (specialty.Length == 0) specialty = desc;
            }
        }

        // Prefer the practice LOCATION address over the MAILING address.
        var city = "";
        var addrState = "";
        if (item.TryGetProperty("addresses", out var addrs) && addrs.ValueKind == JsonValueKind.Array)
        {
            var chosen = default(JsonElement);
            var haveChosen = false;
            foreach (var a in addrs.EnumerateArray())
            {
                var purpose = a.TryGetProperty("address_purpose", out var pr) ? (pr.GetString() ?? "") : "";
                if (purpose == "LOCATION") { chosen = a; haveChosen = true; break; }
                if (!haveChosen) { chosen = a; haveChosen = true; }
            }
            if (haveChosen)
            {
                city = chosen.TryGetProperty("city", out var cy) ? (cy.GetString() ?? "") : "";
                addrState = chosen.TryGetProperty("state", out var ss) ? (ss.GetString() ?? "") : "";
            }
        }

        results.Add(new NpiResult(number, name, also, typeLabel, specialty, city, addrState, status));
    }

    return Results.Ok(results);
});

// DebtHelper attorney-referral intake store (local SQLite, write-enabled).
// Mirrors the read-only terminology.db pattern above, but lives in Data/debthelper.db
// (outside wwwroot, so it is never served to the browser).
// NOTE: this file is UNENCRYPTED. It is fine for dev/staging, but the referrals table holds
// a third party's personal data (referred clients). Move it to an encrypted, access-controlled
// store before collecting live client data.
var debtHelperDbPath = Path.Combine(app.Environment.ContentRootPath, "Data", "debthelper.db");
InitDebtHelperDb(debtHelperDbPath);

// MapStaticAssets serves files at their exact path only (no default-document behavior), so send the
// bare DebtHelper folder URL to its index page. One route covers both /DebtHelper and /DebtHelper/
// (routing normalizes the trailing slash).
app.MapGet("/DebtHelper", () => Results.Redirect("/DebtHelper/index.html"));
app.MapGet("/MarahMutual", () => Results.Redirect("/MarahMutual/index.html"));
app.MapGet("/UBD", () => Results.Redirect("/UBD/index.html"));

// Private, unlinked business docs (pricing sheet, services agreement). Files live in PrivateDocs/
// (outside wwwroot, so MapStaticAssets never serves them). Access is gated by a shared key; a wrong
// or missing key returns 404 so the routes look like they don't exist. Obscurity, not authentication.
// Responses are marked no-store so a browser never serves a stale copy of these
// frequently-edited docs.
const string DocsKey = "fourim";
IResult ServeDoc(string? key, HttpContext ctx, IWebHostEnvironment env, string file)
{
    if (key != DocsKey) return Results.NotFound();
    ctx.Response.Headers["Cache-Control"] = "no-store, must-revalidate";
    return Results.File(Path.Combine(env.ContentRootPath, "PrivateDocs", file), "text/html");
}
app.MapGet("/docs/pricing",   (string? key, HttpContext ctx, IWebHostEnvironment env) => ServeDoc(key, ctx, env, "pricing.html"));
app.MapGet("/docs/agreement", (string? key, HttpContext ctx, IWebHostEnvironment env) => ServeDoc(key, ctx, env, "services-agreement.html"));
app.MapGet("/docs/invoice",   (string? key, HttpContext ctx, IWebHostEnvironment env) => ServeDoc(key, ctx, env, "invoice.html"));
// Shared stylesheet for the docs. Not sensitive (styling only), so no key is required.
app.MapGet("/docs/docs.css", (HttpContext ctx, IWebHostEnvironment env) =>
{
    ctx.Response.Headers["Cache-Control"] = "no-store, must-revalidate";
    return Results.File(Path.Combine(env.ContentRootPath, "PrivateDocs", "docs.css"), "text/css");
});
// Billable line-item catalog for the invoice page (name/rate/billing from LineItemCatalog.cs).
app.MapGet("/api/docs/line-items", (string? key, HttpContext ctx) =>
{
    if (key != DocsKey) return Results.NotFound();
    ctx.Response.Headers["Cache-Control"] = "no-store, must-revalidate";
    return Results.Ok(LineItemCatalog.Items);
});

// Store an attorney's client referral (from refer-a-client.html).
app.MapPost("/api/referrals", (ReferralSubmission s, HttpContext ctx, IWebHostEnvironment env) =>
{
    // Bot filters: silently accept-and-discard honeypot hits and near-instant submits, so a bot
    // can't tell it was blocked. Referral form has many fields, so a real fill takes well over 3s.
    if (!string.IsNullOrWhiteSpace(s.Website) || (s.ElapsedMs is long ms && ms < 3000))
        return Results.Ok(new { ok = true });

    var dbPath = Path.Combine(env.ContentRootPath, "Data", "debthelper.db");
    using var conn = new SqliteConnection($"Data Source={dbPath}");
    conn.Open();

    var leadId = Guid.NewGuid().ToString();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO referrals
            (created_utc, atty_name, firm, atty_email, atty_phone,
             client_name, client_phone, client_email, client_state, need, notes, consent,
             ip, user_agent, referer, accept_language, timezone,
             lead_id, status, source_page, partner_code, utm_source, utm_medium, utm_campaign)
        VALUES
            ($created, $attyName, $firm, $attyEmail, $attyPhone,
             $clientName, $clientPhone, $clientEmail, $clientState, $need, $notes, $consent,
             $ip, $userAgent, $referer, $acceptLanguage, $timezone,
             $leadId, $status, $sourcePage, $partnerCode, $utmSource, $utmMedium, $utmCampaign)";
    cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
    cmd.Parameters.AddWithValue("$attyName", s.AttyName ?? "");
    cmd.Parameters.AddWithValue("$firm", s.Firm ?? "");
    cmd.Parameters.AddWithValue("$attyEmail", s.AttyEmail ?? "");
    cmd.Parameters.AddWithValue("$attyPhone", s.AttyPhone ?? "");
    cmd.Parameters.AddWithValue("$clientName", s.ClientName ?? "");
    cmd.Parameters.AddWithValue("$clientPhone", s.ClientPhone ?? "");
    cmd.Parameters.AddWithValue("$clientEmail", s.ClientEmail ?? "");
    cmd.Parameters.AddWithValue("$clientState", s.ClientState ?? "");
    cmd.Parameters.AddWithValue("$need", s.Need ?? "");
    cmd.Parameters.AddWithValue("$notes", s.Notes ?? "");
    cmd.Parameters.AddWithValue("$consent", s.Consent ? 1 : 0);
    cmd.Parameters.AddWithValue("$ip", ctx.Connection.RemoteIpAddress?.ToString() ?? "");
    cmd.Parameters.AddWithValue("$userAgent", ctx.Request.Headers.UserAgent.ToString());
    cmd.Parameters.AddWithValue("$referer", ctx.Request.Headers.Referer.ToString());
    cmd.Parameters.AddWithValue("$acceptLanguage", ctx.Request.Headers.AcceptLanguage.ToString());
    cmd.Parameters.AddWithValue("$timezone", s.Timezone ?? "");
    cmd.Parameters.AddWithValue("$leadId", leadId);
    cmd.Parameters.AddWithValue("$status", "new");
    cmd.Parameters.AddWithValue("$sourcePage", "refer-a-client");
    cmd.Parameters.AddWithValue("$partnerCode", s.PartnerCode ?? "");
    cmd.Parameters.AddWithValue("$utmSource", s.UtmSource ?? "");
    cmd.Parameters.AddWithValue("$utmMedium", s.UtmMedium ?? "");
    cmd.Parameters.AddWithValue("$utmCampaign", s.UtmCampaign ?? "");
    cmd.ExecuteNonQuery();

    return Results.Ok(new { ok = true, leadId });
}).DisableAntiforgery().RequireRateLimiting("form-submit");

// Store a firm's referral-partner request (from become-a-partner.html).
app.MapPost("/api/partners", (PartnerSubmission s, HttpContext ctx, IWebHostEnvironment env) =>
{
    // Bot filters: silently accept-and-discard honeypot hits and near-instant submits.
    if (!string.IsNullOrWhiteSpace(s.Website) || (s.ElapsedMs is long ms && ms < 2000))
        return Results.Ok(new { ok = true });

    var dbPath = Path.Combine(env.ContentRootPath, "Data", "debthelper.db");
    using var conn = new SqliteConnection($"Data Source={dbPath}");
    conn.Open();

    var interest = string.Join(", ", s.Interest ?? Array.Empty<string>());
    var leadId = Guid.NewGuid().ToString();

    using var cmd = conn.CreateCommand();
    cmd.CommandText = @"
        INSERT INTO partners
            (created_utc, firm, contact, email, phone, state, volume, interest, notes,
             ip, user_agent, referer, accept_language, timezone,
             lead_id, status, source_page, partner_code, utm_source, utm_medium, utm_campaign)
        VALUES
            ($created, $firm, $contact, $email, $phone, $state, $volume, $interest, $notes,
             $ip, $userAgent, $referer, $acceptLanguage, $timezone,
             $leadId, $status, $sourcePage, $partnerCode, $utmSource, $utmMedium, $utmCampaign)";
    cmd.Parameters.AddWithValue("$created", DateTime.UtcNow.ToString("o"));
    cmd.Parameters.AddWithValue("$firm", s.Firm ?? "");
    cmd.Parameters.AddWithValue("$contact", s.Contact ?? "");
    cmd.Parameters.AddWithValue("$email", s.Email ?? "");
    cmd.Parameters.AddWithValue("$phone", s.Phone ?? "");
    cmd.Parameters.AddWithValue("$state", s.State ?? "");
    cmd.Parameters.AddWithValue("$volume", s.Volume ?? "");
    cmd.Parameters.AddWithValue("$interest", interest);
    cmd.Parameters.AddWithValue("$notes", s.Notes ?? "");
    cmd.Parameters.AddWithValue("$ip", ctx.Connection.RemoteIpAddress?.ToString() ?? "");
    cmd.Parameters.AddWithValue("$userAgent", ctx.Request.Headers.UserAgent.ToString());
    cmd.Parameters.AddWithValue("$referer", ctx.Request.Headers.Referer.ToString());
    cmd.Parameters.AddWithValue("$acceptLanguage", ctx.Request.Headers.AcceptLanguage.ToString());
    cmd.Parameters.AddWithValue("$timezone", s.Timezone ?? "");
    cmd.Parameters.AddWithValue("$leadId", leadId);
    cmd.Parameters.AddWithValue("$status", "new");
    cmd.Parameters.AddWithValue("$sourcePage", "become-a-partner");
    cmd.Parameters.AddWithValue("$partnerCode", s.PartnerCode ?? "");
    cmd.Parameters.AddWithValue("$utmSource", s.UtmSource ?? "");
    cmd.Parameters.AddWithValue("$utmMedium", s.UtmMedium ?? "");
    cmd.Parameters.AddWithValue("$utmCampaign", s.UtmCampaign ?? "");
    cmd.ExecuteNonQuery();

    return Results.Ok(new { ok = true, leadId });
}).DisableAntiforgery().RequireRateLimiting("form-submit");

app.Run();

// Create the DebtHelper intake database and its tables on startup if they don't exist.
static void InitDebtHelperDb(string dbPath)
{
    Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

    using var conn = new SqliteConnection($"Data Source={dbPath}");
    conn.Open();

    using (var cmd = conn.CreateCommand())
    {
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS referrals (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                created_utc     TEXT NOT NULL,
                atty_name       TEXT,
                firm            TEXT,
                atty_email      TEXT,
                atty_phone      TEXT,
                client_name     TEXT,
                client_phone    TEXT,
                client_email    TEXT,
                client_state    TEXT,
                need            TEXT,
                notes           TEXT,
                consent         INTEGER NOT NULL DEFAULT 0,
                ip              TEXT,
                user_agent      TEXT,
                referer         TEXT,
                accept_language TEXT,
                timezone        TEXT,
                lead_id         TEXT,
                status          TEXT,
                source_page     TEXT,
                partner_code    TEXT,
                utm_source      TEXT,
                utm_medium      TEXT,
                utm_campaign    TEXT
            );
            CREATE TABLE IF NOT EXISTS partners (
                id              INTEGER PRIMARY KEY AUTOINCREMENT,
                created_utc     TEXT NOT NULL,
                firm            TEXT,
                contact         TEXT,
                email           TEXT,
                phone           TEXT,
                state           TEXT,
                volume          TEXT,
                interest        TEXT,
                notes           TEXT,
                ip              TEXT,
                user_agent      TEXT,
                referer         TEXT,
                accept_language TEXT,
                timezone        TEXT,
                lead_id         TEXT,
                status          TEXT,
                source_page     TEXT,
                partner_code    TEXT,
                utm_source      TEXT,
                utm_medium      TEXT,
                utm_campaign    TEXT
            );";
        cmd.ExecuteNonQuery();
    }

    // Additive migration: patch databases created before these columns existed.
    var trackedColumns = new[]
    {
        "ip", "user_agent", "referer", "accept_language", "timezone",
        "lead_id", "status", "source_page", "partner_code", "utm_source", "utm_medium", "utm_campaign"
    };
    foreach (var table in new[] { "referrals", "partners" })
        foreach (var column in trackedColumns)
            AddColumnIfMissing(conn, table, column);
}

// Add a TEXT column to a table if it isn't already present (SQLite has no ADD COLUMN IF NOT EXISTS).
static void AddColumnIfMissing(SqliteConnection conn, string table, string column)
{
    var exists = false;
    using (var check = conn.CreateCommand())
    {
        check.CommandText = $"PRAGMA table_info({table})";
        using var reader = check.ExecuteReader();
        while (reader.Read())
        {
            if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }
    }

    if (!exists)
    {
        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} TEXT";
        alter.ExecuteNonQuery();
    }
}

record MapRow(string IcdCode, string IcdDescription, string SnomedId, string SnomedTerm);

record NpiResult(string Npi, string Name, string Also, string Type, string Specialty, string City, string State, string Status);

record ReferralSubmission(
    string? AttyName, string? Firm, string? AttyEmail, string? AttyPhone,
    string? ClientName, string? ClientPhone, string? ClientEmail, string? ClientState,
    string? Need, string? Notes, bool Consent, string? Timezone,
    string? Website, long? ElapsedMs,
    string? PartnerCode, string? UtmSource, string? UtmMedium, string? UtmCampaign);

record PartnerSubmission(
    string? Firm, string? Contact, string? Email, string? Phone,
    string? State, string? Volume, string[]? Interest, string? Notes, string? Timezone,
    string? Website, long? ElapsedMs,
    string? PartnerCode, string? UtmSource, string? UtmMedium, string? UtmCampaign);
