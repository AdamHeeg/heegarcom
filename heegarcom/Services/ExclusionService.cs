using Microsoft.Data.Sqlite;

namespace heegarcom.Services;

// Checks a provider NPI against the HHS-OIG List of Excluded Individuals/Entities (LEIE) — providers barred
// from federal health programs for fraud, patient abuse, etc. OIG republishes the full list monthly as a
// CSV; there is no query API, so this imports the CSV into a local SQLite table and refreshes it lazily
// (Option A): the first check with a missing or >30-day-old copy triggers a background rebuild, and the
// current copy keeps serving until the new one is ready.
//
// Safety contract: an exclusion check is a NEGATIVE assertion. If the list is not loaded (never built, or a
// rebuild failed), the result is "unavailable" — never "clear". A false "clear" on a fraud check is worse
// than no answer.
public sealed class ExclusionService
{
    private const string LeieCsvUrl = "https://oig.hhs.gov/exclusions/downloadables/UPDATED.csv";
    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(30);
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromSeconds(120); // whole download (~15 MB)
    private const int MinExpectedRows = 5000; // sanity floor; a real import yields tens of thousands

    private readonly IHttpClientFactory _httpFactory;
    private readonly string _dbPath;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private volatile bool _refreshing;

    public ExclusionService(IHttpClientFactory httpFactory, IWebHostEnvironment env)
    {
        _httpFactory = httpFactory;
        _dbPath = Path.Combine(env.ContentRootPath, "Data", "leie.db");
    }

    public async Task<ExclusionResult> CheckAsync(string npi, CancellationToken ct)
    {
        // Kick off a rebuild when the local copy is missing or stale; never block the caller on it.
        if (IsStale())
            _ = RefreshAsync();

        if (!File.Exists(_dbPath))
            return new ExclusionResult("unavailable", null, null);

        try
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath};Mode=ReadOnly");
            await conn.OpenAsync(ct);
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT excl_type, excl_date FROM exclusions WHERE npi = $npi LIMIT 1";
            cmd.Parameters.AddWithValue("$npi", npi);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            if (await reader.ReadAsync(ct))
            {
                var type = reader.IsDBNull(0) ? "" : reader.GetString(0);
                var date = reader.IsDBNull(1) ? "" : reader.GetString(1);
                return new ExclusionResult("excluded", DescribeType(type), FormatDate(date));
            }

            return new ExclusionResult("clear", null, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            // A read failure is not proof of "clear" — report it as unavailable.
            return new ExclusionResult("unavailable", null, null);
        }
    }

    private bool IsStale()
    {
        if (_refreshing)
            return false;
        if (!File.Exists(_dbPath))
            return true;
        return DateTime.UtcNow - File.GetLastWriteTimeUtc(_dbPath) > MaxAge;
    }

    private async Task RefreshAsync()
    {
        if (!await _refreshGate.WaitAsync(0))
            return; // a rebuild is already running

        _refreshing = true;
        try
        {
            var dir = Path.GetDirectoryName(_dbPath)!;
            Directory.CreateDirectory(dir);
            var tempCsv = Path.Combine(dir, "leie_download.csv");
            var tempDb = Path.Combine(dir, "leie_new.db");

            var client = _httpFactory.CreateClient();
            // OIG returns 403 to requests without a browser-like User-Agent.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; heegarcom/1.0; +https://heegar.com)");
            using var dl = new CancellationTokenSource(DownloadTimeout);
            using (var response = await client.GetAsync(LeieCsvUrl, HttpCompletionOption.ResponseHeadersRead, dl.Token))
            {
                response.EnsureSuccessStatusCode();
                await using var src = await response.Content.ReadAsStreamAsync(dl.Token);
                await using var dst = File.Create(tempCsv);
                await src.CopyToAsync(dst, dl.Token);
            }

            var rows = BuildDb(tempCsv, tempDb);
            if (rows < MinExpectedRows)
                throw new InvalidOperationException($"LEIE import produced only {rows} rows; keeping the previous list.");

            // Swap the freshly built db into place, then clean up.
            File.Copy(tempDb, _dbPath, overwrite: true);
            TryDelete(tempDb);
            TryDelete(tempCsv);
        }
        catch
        {
            // Leave the existing db (if any) untouched; the next check simply serves the last good copy.
        }
        finally
        {
            _refreshing = false;
            _refreshGate.Release();
        }
    }

    // Builds the SQLite table from the LEIE CSV. Only rows with a real NPI (non-zero) are indexed, since a
    // lookup is always by NPI.
    private static int BuildDb(string csvPath, string dbPath)
    {
        TryDelete(dbPath);
        using var conn = new SqliteConnection($"Data Source={dbPath}");
        conn.Open();

        using (var create = conn.CreateCommand())
        {
            create.CommandText = @"
                CREATE TABLE exclusions (npi TEXT NOT NULL, excl_type TEXT, excl_date TEXT);
                CREATE INDEX ix_exclusions_npi ON exclusions (npi);";
            create.ExecuteNonQuery();
        }

        using var tx = conn.BeginTransaction();
        using var insert = conn.CreateCommand();
        insert.CommandText = "INSERT INTO exclusions (npi, excl_type, excl_date) VALUES ($npi, $type, $date)";
        var pNpi = insert.Parameters.Add("$npi", SqliteType.Text);
        var pType = insert.Parameters.Add("$type", SqliteType.Text);
        var pDate = insert.Parameters.Add("$date", SqliteType.Text);

        using var lines = new StreamReader(csvPath);
        var header = lines.ReadLine();
        if (header is null)
            throw new InvalidOperationException("LEIE download was empty.");

        var columns = ParseCsvLine(header);
        var npiIdx = Array.FindIndex(columns, c => c.Equals("NPI", StringComparison.OrdinalIgnoreCase));
        var typeIdx = Array.FindIndex(columns, c => c.Equals("EXCLTYPE", StringComparison.OrdinalIgnoreCase));
        var dateIdx = Array.FindIndex(columns, c => c.Equals("EXCLDATE", StringComparison.OrdinalIgnoreCase));
        if (npiIdx < 0)
            throw new InvalidOperationException("LEIE download is missing the NPI column — unexpected format.");

        var count = 0;
        string? line;
        while ((line = lines.ReadLine()) is not null)
        {
            var fields = ParseCsvLine(line);
            if (npiIdx >= fields.Length)
                continue;

            var npi = fields[npiIdx].Trim();
            if (npi.Length != 10 || npi == "0000000000")
                continue; // LEIE stores 0 when no NPI is on file — not indexable

            pNpi.Value = npi;
            pType.Value = typeIdx >= 0 && typeIdx < fields.Length ? fields[typeIdx].Trim() : "";
            pDate.Value = dateIdx >= 0 && dateIdx < fields.Length ? fields[dateIdx].Trim() : "";
            insert.ExecuteNonQuery();
            count++;
        }

        tx.Commit();
        return count;
    }

    // Minimal RFC-4180 field splitter (handles quoted fields with embedded commas and "" escapes).
    private static string[] ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                    else inQuotes = false;
                }
                else sb.Append(ch);
            }
            else if (ch == '"') inQuotes = true;
            else if (ch == ',') { fields.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(ch);
        }
        fields.Add(sb.ToString());
        return fields.ToArray();
    }

    private static string DescribeType(string exclType)
    {
        // The most common LEIE authorities. Full text isn't in the file — a short gloss on the code is enough.
        return exclType.ToUpperInvariant() switch
        {
            "1128A1" => "Conviction for program-related fraud",
            "1128A2" => "Conviction relating to patient abuse or neglect",
            "1128A3" => "Felony conviction for health-care fraud",
            "1128A4" => "Felony conviction for a controlled-substance offense",
            "1128B4" => "License revoked, suspended, or surrendered",
            "1128B7" => "Fraud or kickbacks",
            "1128B8" => "Entity controlled by a sanctioned individual",
            _ => exclType.Length > 0 ? $"OIG exclusion ({exclType})" : "OIG exclusion",
        };
    }

    private static string FormatDate(string yyyymmdd)
    {
        // LEIE dates are YYYYMMDD.
        if (yyyymmdd.Length == 8 && DateTime.TryParseExact(yyyymmdd, "yyyyMMdd", null, System.Globalization.DateTimeStyles.None, out var d))
            return d.ToString("MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
        return yyyymmdd;
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { /* best effort */ }
    }
}

public record ExclusionResult(string Status, string? Reason, string? Since); // Status: excluded | clear | unavailable
