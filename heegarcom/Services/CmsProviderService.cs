using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace heegarcom.Services;

// Server-side access to two public CMS datasets, keyed by NPI:
//   * Medicare Physician & Other Practitioners (by Provider summary) — data.cms.gov
//   * Open Payments general payments (industry payments to providers) — openpaymentsdata.cms.gov
// Both are proxied here because the CMS APIs send no CORS headers, and both dataset IDs (and their data
// year) are resolved dynamically from the CMS catalogs so the feature keeps reporting the latest year
// available without a code change. Verified 2023 IDs are pinned only as a fallback if resolution fails.
public sealed class CmsProviderService
{
    private const string MedicareCatalogUrl = "https://data.cms.gov/data.json";
    // Brackets MUST be URL-encoded; the unencoded ?Rndrng_NPI= form is silently ignored (returns row 0).
    private const string MedicareDataUrl = "https://data.cms.gov/data-api/v1/dataset/{0}/data?filter%5BRndrng_NPI%5D={1}&size=1";
    private const string MedicareFallbackId = "8ba584c6-a43a-4b0b-a35a-eb9a59e3a571";
    private const int MedicareFallbackYear = 2023;

    private const string OpenPaymentsCatalogUrl = "https://openpaymentsdata.cms.gov/api/1/metastore/schemas/dataset/items?show-reference-ids=false";
    private const string OpenPaymentsDataUrl = "https://openpaymentsdata.cms.gov/api/1/datastore/query/{0}"
        + "?conditions%5B0%5D%5Bproperty%5D=covered_recipient_npi"
        + "&conditions%5B0%5D%5Boperator%5D=%3D"
        + "&conditions%5B0%5D%5Bvalue%5D={1}&limit={2}&offset={3}";
    private const string OpenPaymentsFallbackId = "004e1f11-aa67-5b16-943d-1fe1044b3512";
    private const int OpenPaymentsFallbackYear = 2023;
    private const int OpenPaymentsPageSize = 500;
    private const int OpenPaymentsMaxRows = 5000; // safety cap; beyond this the summary is marked partial

    private readonly IHttpClientFactory _httpFactory;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private DatasetRef? _medicare;
    private DatasetRef? _openPayments;
    private DateTime _cacheUntilUtc = DateTime.MinValue;

    public CmsProviderService(IHttpClientFactory httpFactory) => _httpFactory = httpFactory;

    public async Task<ProviderDetails> GetDetailsAsync(string npi, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient();
        await EnsureDatasetsResolvedAsync(client, ct);

        var medicareTask = FetchMedicareAsync(client, npi, ct);
        var openPaymentsTask = FetchOpenPaymentsAsync(client, npi, ct);
        await Task.WhenAll(medicareTask, openPaymentsTask);

        var (medicare, medicareFailed) = medicareTask.Result;
        var (openPayments, openPaymentsFailed) = openPaymentsTask.Result;
        return new ProviderDetails(medicare, medicareFailed, openPayments, openPaymentsFailed);
    }

    // ---- Dataset resolution (cached 24h) -------------------------------------------------------

    private async Task EnsureDatasetsResolvedAsync(HttpClient client, CancellationToken ct)
    {
        if (DateTime.UtcNow < _cacheUntilUtc && _medicare is not null && _openPayments is not null)
            return;

        await _gate.WaitAsync(ct);
        try
        {
            if (DateTime.UtcNow < _cacheUntilUtc && _medicare is not null && _openPayments is not null)
                return;

            _medicare = await ResolveMedicareAsync(client, ct);
            _openPayments = await ResolveOpenPaymentsAsync(client, ct);
            _cacheUntilUtc = DateTime.UtcNow.AddHours(24);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<DatasetRef> ResolveMedicareAsync(HttpClient client, CancellationToken ct)
    {
        try
        {
            var json = await client.GetStringAsync(MedicareCatalogUrl, ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("dataset", out var datasets) || datasets.ValueKind != JsonValueKind.Array)
                return new DatasetRef(MedicareFallbackId, MedicareFallbackYear);

            DatasetRef? best = null;
            foreach (var ds in datasets.EnumerateArray())
            {
                var title = GetString(ds, "title");
                // The " - by Provider :" summary (one row per provider), not "by Provider and Service" or "by Geography".
                if (!title.Contains("Medicare Physician & Other Practitioners - by Provider :", StringComparison.OrdinalIgnoreCase))
                    continue;

                var year = YearFromTemporal(GetString(ds, "temporal"));
                if (year == 0)
                    continue;

                var id = ExtractIdFromApiUrl(ApiDistributionUrl(ds));
                if (id is null)
                    continue;

                if (best is null || year > best.Year)
                    best = new DatasetRef(id, year);
            }

            return best ?? new DatasetRef(MedicareFallbackId, MedicareFallbackYear);
        }
        catch
        {
            return new DatasetRef(MedicareFallbackId, MedicareFallbackYear);
        }
    }

    private async Task<DatasetRef> ResolveOpenPaymentsAsync(HttpClient client, CancellationToken ct)
    {
        try
        {
            var json = await client.GetStringAsync(OpenPaymentsCatalogUrl, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return new DatasetRef(OpenPaymentsFallbackId, OpenPaymentsFallbackYear);

            DatasetRef? best = null;
            foreach (var ds in doc.RootElement.EnumerateArray())
            {
                if (!ds.TryGetProperty("distribution", out var dists) || dists.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var dist in dists.EnumerateArray())
                {
                    // The general-payments detail file is named OP_DTL_GNRL_PGYR{year}. The id we need for the
                    // datastore query is the identifier of the distribution that carries that download.
                    var downloadUrl = DistributionDownloadUrl(dist);
                    var match = Regex.Match(downloadUrl, @"OP_DTL_GNRL_PGYR(\d{4})");
                    if (!match.Success)
                        continue;

                    var year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                    var id = GetString(dist, "identifier");
                    if (id.Length == 0)
                        continue;

                    if (best is null || year > best.Year)
                        best = new DatasetRef(id, year);
                }
            }

            return best ?? new DatasetRef(OpenPaymentsFallbackId, OpenPaymentsFallbackYear);
        }
        catch
        {
            return new DatasetRef(OpenPaymentsFallbackId, OpenPaymentsFallbackYear);
        }
    }

    // ---- Per-NPI fetches ----------------------------------------------------------------------

    private async Task<(MedicareSummary? Data, bool Failed)> FetchMedicareAsync(HttpClient client, string npi, CancellationToken ct)
    {
        try
        {
            var url = string.Format(CultureInfo.InvariantCulture, MedicareDataUrl, _medicare!.Id, npi);
            var json = await client.GetStringAsync(url, ct);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return (null, false); // provider has no Medicare record

            var row = doc.RootElement[0];
            return (new MedicareSummary(
                Year: _medicare.Year,
                ProviderType: GetString(row, "Rndrng_Prvdr_Type"),
                Beneficiaries: ParseInt(row, "Tot_Benes"),
                Services: ParseInt(row, "Tot_Srvcs"),
                TotalPaid: ParseDecimal(row, "Tot_Mdcr_Pymt_Amt"),
                TotalAllowed: ParseDecimal(row, "Tot_Mdcr_Alowd_Amt"),
                DrugPaid: ParseDecimal(row, "Drug_Mdcr_Pymt_Amt"),
                MedicalPaid: ParseDecimal(row, "Med_Mdcr_Pymt_Amt"),
                AvgAge: ParseInt(row, "Bene_Avg_Age")), false);
        }
        catch (OperationCanceledException)
        {
            throw; // caller cancelled — never report this as "no record"
        }
        catch
        {
            return (null, true); // upstream/parse failure — distinct from "no record"
        }
    }

    private async Task<(OpenPaymentsSummary? Data, bool Failed)> FetchOpenPaymentsAsync(HttpClient client, string npi, CancellationToken ct)
    {
        try
        {
            decimal total = 0;
            var count = 0;
            var year = _openPayments!.Year;
            var natures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var manufacturers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            var offset = 0;
            var capped = false;
            while (true)
            {
                var url = string.Format(CultureInfo.InvariantCulture, OpenPaymentsDataUrl, _openPayments.Id, npi, OpenPaymentsPageSize, offset);
                var json = await client.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
                    break;

                var pageCount = results.GetArrayLength();
                if (pageCount == 0)
                    break;

                foreach (var r in results.EnumerateArray())
                {
                    count++;
                    total += ParseDecimal(r, "total_amount_of_payment_usdollars");

                    var programYear = ParseInt(r, "program_year");
                    if (programYear > 0)
                        year = programYear;

                    Tally(natures, GetString(r, "nature_of_payment_or_transfer_of_value"));
                    Tally(manufacturers, GetString(r, "applicable_manufacturer_or_applicable_gpo_making_payment_name"));
                }

                offset += pageCount;
                if (pageCount < OpenPaymentsPageSize)
                    break; // last page — totals are complete
                if (offset >= OpenPaymentsMaxRows)
                {
                    capped = true; // stopped at the safety cap; the summary is partial
                    break;
                }
            }

            if (count == 0)
                return (null, false); // provider has no reported payments

            return (new OpenPaymentsSummary(
                Year: year,
                TotalReceived: total,
                PaymentCount: count,
                Capped: capped,
                TopNatures: TopKeys(natures),
                TopManufacturers: TopKeys(manufacturers)), false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return (null, true);
        }
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

    private static string? ExtractIdFromApiUrl(string apiUrl)
    {
        var match = Regex.Match(apiUrl, @"/dataset/([a-f0-9-]{36})/");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string DistributionDownloadUrl(JsonElement dist)
    {
        var direct = GetString(dist, "downloadURL");
        if (direct.Length > 0)
            return direct;
        // Reference-resolved shape: { identifier, title, data: { downloadURL, ... } }
        return dist.TryGetProperty("data", out var data) ? GetString(data, "downloadURL") : "";
    }

    private static int YearFromTemporal(string temporal)
    {
        // e.g. "2023-01-01/2023-12-31"
        var match = Regex.Match(temporal, @"(\d{4})");
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

    private sealed record DatasetRef(string Id, int Year);
}

public record ProviderDetails(
    MedicareSummary? Medicare, bool MedicareFailed,
    OpenPaymentsSummary? OpenPayments, bool OpenPaymentsFailed);

public record MedicareSummary(
    int Year, string ProviderType, int Beneficiaries, int Services,
    decimal TotalPaid, decimal TotalAllowed, decimal DrugPaid, decimal MedicalPaid, int AvgAge);

public record OpenPaymentsSummary(
    int Year, decimal TotalReceived, int PaymentCount, bool Capped,
    string[] TopNatures, string[] TopManufacturers);
