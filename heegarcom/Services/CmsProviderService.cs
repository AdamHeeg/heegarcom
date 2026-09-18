using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace heegarcom.Services;

// Server-side access to the public CMS provider datasets, keyed by NPI. The CMS APIs send no CORS headers,
// so the WASM client can't call them directly; this proxies them and maps the raw JSON to small DTOs.
// Dataset IDs and their data year are resolved dynamically from the CMS catalogs (cached 24h) so the feature
// keeps reporting the latest year without a code change; verified IDs are pinned only as a fallback.
//
// Sources:
//   * Part D prescribing            — data.cms.gov (Medicare Part D Prescribers - by Provider)
//   * Open Payments (industry pay)  — openpaymentsdata.cms.gov (General Payments)
//   * Profile, affiliations, MIPS   — data.cms.gov/provider-data (Care Compare)
public sealed class CmsProviderService
{
    private const string CmsCatalogUrl = "https://data.cms.gov/data.json";
    private const string CmsDataUrl = "https://data.cms.gov/data-api/v1/dataset/{0}/data?filter%5B{1}%5D={2}&size=1";

    private const string PartDTitlePrefix = "Medicare Part D Prescribers - by Provider :";
    private const string PartDFallbackId = "2d38b6fd-f7a9-4edd-98f6-06ae3b4d1b8c";
    private const int PartDFallbackYear = 2024;

    private const string OpenPaymentsCatalogUrl = "https://openpaymentsdata.cms.gov/api/1/metastore/schemas/dataset/items?show-reference-ids=false";
    private const string OpenPaymentsDataUrl = "https://openpaymentsdata.cms.gov/api/1/datastore/query/{0}"
        + "?conditions%5B0%5D%5Bproperty%5D=covered_recipient_npi"
        + "&conditions%5B0%5D%5Boperator%5D=%3D"
        + "&conditions%5B0%5D%5Bvalue%5D={1}&limit={2}&offset={3}";
    private const string OpenPaymentsFallbackId = "004e1f11-aa67-5b16-943d-1fe1044b3512";
    private const int OpenPaymentsFallbackYear = 2023;
    private const int OpenPaymentsPageSize = 500;
    private const int OpenPaymentsMaxRows = 5000;

    private const string ProviderDataCatalogUrl = "https://data.cms.gov/provider-data/api/1/metastore/schemas/dataset/items?show-reference-ids=false";
    private const string ProviderDataUrl = "https://data.cms.gov/provider-data/api/1/datastore/query/{0}"
        + "?conditions%5B0%5D%5Bproperty%5D=npi&conditions%5B0%5D%5Boperator%5D=%3D&conditions%5B0%5D%5Bvalue%5D={1}&limit={2}";
    private const string ProfileFallbackId = "288f7073-7fc6-5f3b-aa25-44902d5d1f8f";      // National Downloadable File
    private const string AffiliationFallbackId = "3dc3f3a5-dc36-53fd-a929-51035d7b8854";  // Facility Affiliation Data
    private const string MipsFallbackId = "584f5358-62d9-50aa-a509-a37800ce2722";
    private const int MipsFallbackYear = 2024;

    // Generous per-call caps so only a genuinely stuck upstream trips them; a timeout counts as a section
    // failure (not a caller cancellation).
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ResolveTimeout = TimeSpan.FromSeconds(30);

    private readonly IHttpClientFactory _httpFactory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Resolved? _resolved;
    private DateTime _cacheUntilUtc = DateTime.MinValue;

    public CmsProviderService(IHttpClientFactory httpFactory) => _httpFactory = httpFactory;

    public async Task<CmsSections> GetDetailsAsync(string npi, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient();
        var r = await EnsureResolvedAsync(client, ct);

        var profileTask = FetchProfileAsync(client, r, npi, ct);
        var affiliationTask = FetchAffiliationsAsync(client, r, npi, ct);
        var partDTask = FetchPartDAsync(client, r, npi, ct);
        var openPaymentsTask = FetchOpenPaymentsAsync(client, r, npi, ct);
        var mipsTask = FetchMipsAsync(client, r, npi, ct);
        await Task.WhenAll(profileTask, affiliationTask, partDTask, openPaymentsTask, mipsTask);

        var (profile, profileFailed) = profileTask.Result;
        var (affiliations, affiliationsFailed) = affiliationTask.Result;
        var (partD, partDFailed) = partDTask.Result;
        var (openPayments, openPaymentsFailed) = openPaymentsTask.Result;
        var (mips, mipsFailed) = mipsTask.Result;

        return new CmsSections(
            profile, profileFailed,
            affiliations, affiliationsFailed,
            partD, partDFailed,
            openPayments, openPaymentsFailed,
            mips, mipsFailed);
    }

    // GET with an explicit timeout that covers response headers AND body, without treating the caller's own
    // cancellation as a timeout. If the timeout fires, this throws (caught as a section failure); if the
    // caller cancelled, the OperationCanceledException carries the caller's token so callers can rethrow it.
    private static async Task<string> GetStringWithTimeoutAsync(HttpClient client, string url, TimeSpan timeout, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        return await client.GetStringAsync(url, cts.Token);
    }

    // ---- Dataset resolution (cached 24h) -------------------------------------------------------

    private async Task<Resolved> EnsureResolvedAsync(HttpClient client, CancellationToken ct)
    {
        if (_resolved is not null && DateTime.UtcNow < _cacheUntilUtc)
            return _resolved;

        await _gate.WaitAsync(ct);
        try
        {
            if (_resolved is not null && DateTime.UtcNow < _cacheUntilUtc)
                return _resolved;

            var (partDId, partDYear) = await ResolvePartDAsync(client, ct);
            var (opId, opYear) = await ResolveOpenPaymentsAsync(client, ct);
            var (profileId, affiliationId, mipsId, mipsYear) = await ResolveProviderDataAsync(client, ct);

            _resolved = new Resolved(partDId, partDYear, opId, opYear, profileId, affiliationId, mipsId, mipsYear);
            _cacheUntilUtc = DateTime.UtcNow.AddHours(24);
            return _resolved;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<(string Id, int Year)> ResolvePartDAsync(HttpClient client, CancellationToken ct)
    {
        try
        {
            var json = await GetStringWithTimeoutAsync(client, CmsCatalogUrl, ResolveTimeout, ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("dataset", out var datasets) || datasets.ValueKind != JsonValueKind.Array)
                return (PartDFallbackId, PartDFallbackYear);

            var bestYear = 0;
            string? bestId = null;
            foreach (var ds in datasets.EnumerateArray())
            {
                if (!GetString(ds, "title").StartsWith(PartDTitlePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                var year = YearFrom(GetString(ds, "temporal"));
                var id = ExtractDatasetId(ApiDistributionUrl(ds));
                if (year == 0 || id is null || year <= bestYear)
                    continue;
                bestYear = year;
                bestId = id;
            }
            return bestId is null ? (PartDFallbackId, PartDFallbackYear) : (bestId, bestYear);
        }
        catch
        {
            return (PartDFallbackId, PartDFallbackYear);
        }
    }

    private async Task<(string Id, int Year)> ResolveOpenPaymentsAsync(HttpClient client, CancellationToken ct)
    {
        try
        {
            var json = await GetStringWithTimeoutAsync(client, OpenPaymentsCatalogUrl, ResolveTimeout, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return (OpenPaymentsFallbackId, OpenPaymentsFallbackYear);

            var bestYear = 0;
            string? bestId = null;
            foreach (var ds in doc.RootElement.EnumerateArray())
            {
                if (!ds.TryGetProperty("distribution", out var dists) || dists.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (var dist in dists.EnumerateArray())
                {
                    var match = Regex.Match(DistributionDownloadUrl(dist), @"OP_DTL_GNRL_PGYR(\d{4})");
                    if (!match.Success)
                        continue;
                    var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    var id = GetString(dist, "identifier");
                    if (id.Length == 0 || year <= bestYear)
                        continue;
                    bestYear = year;
                    bestId = id;
                }
            }
            return bestId is null ? (OpenPaymentsFallbackId, OpenPaymentsFallbackYear) : (bestId, bestYear);
        }
        catch
        {
            return (OpenPaymentsFallbackId, OpenPaymentsFallbackYear);
        }
    }

    private async Task<(string ProfileId, string AffiliationId, string MipsId, int MipsYear)> ResolveProviderDataAsync(HttpClient client, CancellationToken ct)
    {
        var profileId = ProfileFallbackId;
        var affiliationId = AffiliationFallbackId;
        var mipsId = MipsFallbackId;
        var mipsYear = MipsFallbackYear;
        try
        {
            var json = await GetStringWithTimeoutAsync(client, ProviderDataCatalogUrl, ResolveTimeout, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return (profileId, affiliationId, mipsId, mipsYear);

            var bestMipsYear = 0;
            foreach (var ds in doc.RootElement.EnumerateArray())
            {
                var title = GetString(ds, "title");
                var id = FirstDistributionId(ds);
                if (id is null)
                    continue;

                if (title.Equals("National Downloadable File", StringComparison.OrdinalIgnoreCase))
                    profileId = id;
                else if (title.Equals("Facility Affiliation Data", StringComparison.OrdinalIgnoreCase))
                    affiliationId = id;
                else if (title.Contains("Overall MIPS Performance", StringComparison.OrdinalIgnoreCase))
                {
                    var year = YearFrom(title); // title carries "PY 2024 ..."
                    if (year > bestMipsYear)
                    {
                        bestMipsYear = year;
                        mipsId = id;
                        mipsYear = year;
                    }
                }
            }
            return (profileId, affiliationId, mipsId, mipsYear);
        }
        catch
        {
            return (profileId, affiliationId, mipsId, mipsYear);
        }
    }

    // ---- Per-NPI fetches ----------------------------------------------------------------------

    private async Task<(PartDSummary? Data, bool Failed)> FetchPartDAsync(HttpClient client, Resolved r, string npi, CancellationToken ct)
    {
        try
        {
            var url = string.Format(CultureInfo.InvariantCulture, CmsDataUrl, r.PartDId, "Prscrbr_NPI", npi);
            using var doc = JsonDocument.Parse(await GetStringWithTimeoutAsync(client, url, FetchTimeout, ct));
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return (null, false);

            var row = doc.RootElement[0];
            return (new PartDSummary(
                Year: r.PartDYear,
                PrescriberType: GetString(row, "Prscrbr_Type"),
                TotalClaims: ParseInt(row, "Tot_Clms"),
                TotalDrugCost: ParseDecimal(row, "Tot_Drug_Cst"),
                BrandClaims: ParseInt(row, "Brnd_Tot_Clms"),
                GenericClaims: ParseInt(row, "Gnrc_Tot_Clms"),
                OpioidClaims: ParseInt(row, "Opioid_Tot_Clms"),
                OpioidRate: ParseDecimal(row, "Opioid_Prscrbr_Rate")), false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return (null, true); }
    }

    private async Task<(ProviderProfile? Data, bool Failed)> FetchProfileAsync(HttpClient client, Resolved r, string npi, CancellationToken ct)
    {
        try
        {
            var url = string.Format(CultureInfo.InvariantCulture, ProviderDataUrl, r.ProfileId, npi, 1);
            using var doc = JsonDocument.Parse(await GetStringWithTimeoutAsync(client, url, FetchTimeout, ct));
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
                return (null, false);

            var row = results[0];
            return (new ProviderProfile(
                Credential: GetString(row, "cred"),
                MedicalSchool: GetString(row, "med_sch"),
                GradYear: ParseInt(row, "grd_yr"),
                Specialty: GetString(row, "pri_spec"),
                Telehealth: GetString(row, "telehlth").Equals("Y", StringComparison.OrdinalIgnoreCase),
                AcceptsAssignment: GetString(row, "ind_assgn").Equals("Y", StringComparison.OrdinalIgnoreCase)), false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return (null, true); }
    }

    private async Task<(string[] Data, bool Failed)> FetchAffiliationsAsync(HttpClient client, Resolved r, string npi, CancellationToken ct)
    {
        try
        {
            var url = string.Format(CultureInfo.InvariantCulture, ProviderDataUrl, r.AffiliationId, npi, 200);
            using var doc = JsonDocument.Parse(await GetStringWithTimeoutAsync(client, url, FetchTimeout, ct));
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                return (Array.Empty<string>(), false);

            // Each row is one facility affiliation; summarize as "<Facility type> ×N".
            var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in results.EnumerateArray())
                Tally(counts, GetString(row, "facility_type"));

            var summary = counts
                .OrderByDescending(kv => kv.Value)
                .Select(kv => kv.Value > 1 ? $"{kv.Key} ×{kv.Value}" : kv.Key)
                .ToArray();
            return (summary, false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return (Array.Empty<string>(), true); }
    }

    private async Task<(MipsSummary? Data, bool Failed)> FetchMipsAsync(HttpClient client, Resolved r, string npi, CancellationToken ct)
    {
        try
        {
            var url = string.Format(CultureInfo.InvariantCulture, ProviderDataUrl, r.MipsId, npi, 1);
            using var doc = JsonDocument.Parse(await GetStringWithTimeoutAsync(client, url, FetchTimeout, ct));
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
                return (null, false);

            var row = results[0];
            var final = ParseDecimalOrNull(row, "final_mips_score");
            if (final is null)
                return (null, false); // no score reported — distinct from a real score of 0

            return (new MipsSummary(
                Year: r.MipsYear,
                FinalScore: final.Value,
                QualityScore: ParseDecimalOrNull(row, "quality_category_score"),
                CostScore: ParseDecimalOrNull(row, "cost_category_score")), false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return (null, true); }
    }

    private async Task<(OpenPaymentsSummary? Data, bool Failed)> FetchOpenPaymentsAsync(HttpClient client, Resolved r, string npi, CancellationToken ct)
    {
        try
        {
            decimal total = 0;
            var count = 0;
            var year = r.OpenPaymentsYear;
            var natures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var manufacturers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var offset = 0;
            var capped = false;
            while (true)
            {
                var url = string.Format(CultureInfo.InvariantCulture, OpenPaymentsDataUrl, r.OpenPaymentsId, npi, OpenPaymentsPageSize, offset);
                using var doc = JsonDocument.Parse(await GetStringWithTimeoutAsync(client, url, FetchTimeout, ct));
                if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                    break;

                var pageCount = results.GetArrayLength();
                if (pageCount == 0)
                    break;

                foreach (var payment in results.EnumerateArray())
                {
                    count++;
                    total += ParseDecimal(payment, "total_amount_of_payment_usdollars");
                    var programYear = ParseInt(payment, "program_year");
                    if (programYear > 0) year = programYear;
                    Tally(natures, GetString(payment, "nature_of_payment_or_transfer_of_value"));
                    Tally(manufacturers, GetString(payment, "applicable_manufacturer_or_applicable_gpo_making_payment_name"));
                }

                offset += pageCount;
                if (pageCount < OpenPaymentsPageSize)
                    break;
                if (offset >= OpenPaymentsMaxRows)
                {
                    capped = true;
                    break;
                }
            }

            if (count == 0)
                return (null, false);

            return (new OpenPaymentsSummary(year, total, count, capped, TopKeys(natures), TopKeys(manufacturers)), false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { return (null, true); }
    }

    // ---- Catalog parsing helpers --------------------------------------------------------------

    private static string ApiDistributionUrl(JsonElement dataset)
    {
        if (!dataset.TryGetProperty("distribution", out var dists) || dists.ValueKind != JsonValueKind.Array)
            return "";
        foreach (var dist in dists.EnumerateArray())
        {
            var url = GetString(dist, "accessURL");
            if (GetString(dist, "format").Equals("API", StringComparison.OrdinalIgnoreCase) && url.Contains("data-api", StringComparison.OrdinalIgnoreCase))
                return url;
        }
        return "";
    }

    private static string? ExtractDatasetId(string apiUrl)
    {
        var match = Regex.Match(apiUrl, @"/dataset/([a-f0-9-]{36})/");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string DistributionDownloadUrl(JsonElement dist)
    {
        var direct = GetString(dist, "downloadURL");
        if (direct.Length > 0)
            return direct;
        return dist.TryGetProperty("data", out var data) ? GetString(data, "downloadURL") : "";
    }

    // provider-data metastore: a dataset's distribution entries are { identifier, title, data:{...} }; the
    // datastore query wants the first entry's identifier.
    private static string? FirstDistributionId(JsonElement dataset)
    {
        if (!dataset.TryGetProperty("distribution", out var dists) || dists.ValueKind != JsonValueKind.Array)
            return null;
        foreach (var dist in dists.EnumerateArray())
        {
            var id = GetString(dist, "identifier");
            if (id.Length == 36)
                return id;
        }
        return null;
    }

    private static int YearFrom(string text)
    {
        var match = Regex.Match(text, @"(\d{4})");
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    // ---- Value helpers ------------------------------------------------------------------------

    private static void Tally(Dictionary<string, int> counts, string key)
    {
        key = key.Trim();
        if (key.Length == 0)
            return;
        counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;
    }

    private static string[] TopKeys(Dictionary<string, int> counts) =>
        counts.OrderByDescending(kv => kv.Value).Take(3).Select(kv => kv.Key).ToArray();

    private static string GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var p) && p.ValueKind == JsonValueKind.String ? (p.GetString() ?? "") : "";

    private static int ParseInt(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var p))
            return 0;
        if (p.ValueKind == JsonValueKind.Number)
            return p.TryGetInt32(out var n) ? n : (int)Math.Round(p.GetDouble());
        return int.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var s) ? s : 0;
    }

    private static decimal ParseDecimal(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var p))
            return 0;
        if (p.ValueKind == JsonValueKind.Number)
            return p.GetDecimal();
        return decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0;
    }

    // Like ParseDecimal, but null (not 0) when the field is missing, empty, or unparseable — so a genuine
    // zero can be told apart from "no value reported".
    private static decimal? ParseDecimalOrNull(JsonElement el, string prop)
    {
        if (!el.TryGetProperty(prop, out var p))
            return null;
        if (p.ValueKind == JsonValueKind.Number)
            return p.GetDecimal();
        var s = p.GetString();
        if (string.IsNullOrWhiteSpace(s))
            return null;
        return decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private sealed record Resolved(
        string PartDId, int PartDYear,
        string OpenPaymentsId, int OpenPaymentsYear,
        string ProfileId, string AffiliationId,
        string MipsId, int MipsYear);
}

public record CmsSections(
    ProviderProfile? Profile, bool ProfileFailed,
    string[] Affiliations, bool AffiliationsFailed,
    PartDSummary? PartD, bool PartDFailed,
    OpenPaymentsSummary? OpenPayments, bool OpenPaymentsFailed,
    MipsSummary? Mips, bool MipsFailed);

public record ProviderProfile(
    string Credential, string MedicalSchool, int GradYear, string Specialty,
    bool Telehealth, bool AcceptsAssignment);

public record PartDSummary(
    int Year, string PrescriberType, int TotalClaims, decimal TotalDrugCost,
    int BrandClaims, int GenericClaims, int OpioidClaims, decimal OpioidRate);

public record OpenPaymentsSummary(
    int Year, decimal TotalReceived, int PaymentCount, bool Capped,
    string[] TopNatures, string[] TopManufacturers);

public record MipsSummary(int Year, decimal FinalScore, decimal? QualityScore, decimal? CostScore);
