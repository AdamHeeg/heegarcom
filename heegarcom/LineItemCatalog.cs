// Catalog of billable line items for the invoice generator (/docs/invoice).
// Single source of truth for names + rates, mirrored from the pricing sheet and
// served as JSON to the page. Billing is "once" (one-time) or "monthly".
static class LineItemCatalog
{
    public static readonly IReadOnlyList<CatalogItem> Items = new CatalogItem[]
    {
        new CatalogItem("Website — Landing (single page)",   400m,  "once"),
        new CatalogItem("Website — Standard (up to 5 pages)", 1200m,  "once"),
        new CatalogItem("Website — Premium (up to 10 pages)", 1500m, "once"),
        new CatalogItem("Additional page",                        100m,  "once"),
        new CatalogItem("LinkedIn Company Page setup",            300m,  "once"),
        new CatalogItem("YouTube Channel setup",                  300m,  "once"),
        new CatalogItem("Online presence setup — bundle",    450m,  "once"),
        new CatalogItem("Hourly (ad-hoc)",                        75m,   "once"),
        new CatalogItem("Website Care — Basic",              75m,   "monthly"),
        new CatalogItem("Website Care — Plus",               150m,  "monthly"),
        new CatalogItem("Social Content — Basic",            125m,  "monthly"),
        new CatalogItem("Social Content — Extra",            225m,  "monthly"),
    };
}

record CatalogItem(string Name, decimal Rate, string Billing);
