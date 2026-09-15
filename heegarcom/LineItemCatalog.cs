// Catalog of billable line items — the single source of truth for names, rates,
// descriptions, and grouping. Served as JSON to BOTH the invoice generator
// (/docs/invoice) and the pricing sheet (/docs/pricing). Edit prices here only.
// Billing is "once" (one-time) or "monthly".
static class LineItemCatalog
{
    public static readonly IReadOnlyList<CatalogItem> Items = new CatalogItem[]
    {
        new CatalogItem("Website — Landing (single page)",    400m,  "once",    "Website Build",         "Single page — hero, key info, contact form."),
        new CatalogItem("Website — Standard (up to 5 pages)", 1200m, "once",    "Website Build",         "Up to 5 pages — a full small-business site."),
        new CatalogItem("Website — Premium (up to 10 pages)", 1500m, "once",    "Website Build",         "Up to 10 pages, plus custom features / integrations."),
        new CatalogItem("Additional page",                    100m,  "once",    "Website Build",         "Added to any build."),
        new CatalogItem("LinkedIn Company Page setup",        300m,  "once",    "Online Presence Setup", "Branded page, About, and first posts."),
        new CatalogItem("YouTube Channel setup",              300m,  "once",    "Online Presence Setup", "Branded channel, About, initial uploads & organization."),
        new CatalogItem("Online presence setup — bundle",     450m,  "once",    "Online Presence Setup", "LinkedIn + YouTube set up together."),
        new CatalogItem("Hourly (ad-hoc)",                    75m,   "once",    "Ad-Hoc",                "One-off work or changes outside a plan."),
        new CatalogItem("Website Care — Basic",               75m,   "monthly", "Website Care",          "Updates, backups, security, and up to 1 hr of edits / month."),
        new CatalogItem("Website Care — Plus",                150m,  "monthly", "Website Care",          "Everything in Basic, priority, up to 3 hrs of edits / month."),
        new CatalogItem("Social Content — Basic",             125m,  "monthly", "Social Content",        "~4 posts / month plus light profile upkeep."),
        new CatalogItem("Social Content — Extra",             225m,  "monthly", "Social Content",        "~8 posts / month plus one page/blog update."),
    };
}

record CatalogItem(string Name, decimal Rate, string Billing, string Category, string Description);
