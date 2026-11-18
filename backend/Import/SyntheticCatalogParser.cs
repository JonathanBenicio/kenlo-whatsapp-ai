using System.Globalization;
using System.Xml;
using System.Xml.Linq;

namespace Kenlo.Api.Import;

/// <summary>A record from the deliberately synthetic demo format, not a Kenlo format.</summary>
public sealed record SyntheticProperty(
    string Code,
    string Title,
    string City,
    string Neighborhood,
    decimal Price,
    int Bedrooms,
    int Parking,
    string[] Features,
    string ListingUrl,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Parses a bounded synthetic catalog without touching the current catalog.
/// Callers must commit the returned batch atomically, only after parsing succeeds.
/// Absence from a batch does not imply that a property was removed or sold.
/// </summary>
public sealed class SyntheticCatalogParser
{
    public const long MaxXmlCharacters = 1_000_000;
    public const int MaxProperties = 5_000;
    private static readonly string[] Fields =
        ["title", "city", "neighborhood", "price", "bedrooms", "parking", "features", "listingUrl", "updatedAt"];

    public IReadOnlyList<SyntheticProperty> Parse(Stream input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxXmlCharacters,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
            CloseInput = false
        };
        using var reader = XmlReader.Create(input, settings);
        var root = XDocument.Load(reader).Root;
        if (root is null || root.Name != "synthetic-catalog" || (string?)root.Attribute("format") != "demo-v1" ||
            root.Attributes().Any(a => a.Name != "format"))
            throw new InvalidDataException("Expected the synthetic-catalog demo-v1 format; this is not a Kenlo XML parser.");

        var nodes = root.Elements().ToList();
        if (nodes.Count is 0 or > MaxProperties || nodes.Any(n => n.Name != "property"))
            throw new InvalidDataException("Synthetic catalog has an invalid number or type of entries.");

        var properties = new List<SyntheticProperty>(nodes.Count);
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in nodes)
        {
            if (node.Attributes().Any(a => a.Name != "code") || node.Elements().Any(e => !Fields.Contains(e.Name.LocalName) || e.Name.Namespace != XNamespace.None))
                throw new InvalidDataException("Invalid synthetic property fields.");
            var code = RequiredText((string?)node.Attribute("code"), "code", 80).ToUpperInvariant();
            if (!codes.Add(code)) throw new InvalidDataException("Duplicate property code in batch.");
            var priceText = SingleText(node, "price", 32);
            if (!decimal.TryParse(priceText, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var price) || price < 0 || price > 1_000_000_000m)
                throw new InvalidDataException("Invalid price.");
            var bedrooms = Number(node, "bedrooms");
            var parking = Number(node, "parking");
            var listingUrl = SingleText(node, "listingUrl", 2048);
            if (!Uri.TryCreate(listingUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || uri.Host != "example.invalid" || !string.IsNullOrEmpty(uri.UserInfo))
                throw new InvalidDataException("Synthetic listings must use HTTPS at example.invalid without credentials.");
            var updated = SingleText(node, "updatedAt", 40);
            if (!HasExplicitOffset(updated) || !DateTimeOffset.TryParse(updated, CultureInfo.InvariantCulture, DateTimeStyles.None, out var updatedAt))
                throw new InvalidDataException("Invalid timestamp or missing time zone.");

            var featureElements = node.Elements("features").ToList();
            if (featureElements.Count > 1) throw new InvalidDataException("Duplicate features field.");
            var featureContainer = featureElements.SingleOrDefault();
            if (featureContainer is not null && (featureContainer.HasAttributes || featureContainer.Elements().Any(e => e.Name != "feature" || e.HasElements || e.HasAttributes)))
                throw new InvalidDataException("Invalid features.");
            var features = featureContainer?.Elements("feature").Select(e => RequiredText(e.Value, "feature", 120)).ToArray() ?? [];
            if (features.Length > 32) throw new InvalidDataException("Too many features.");

            properties.Add(new SyntheticProperty(code, SingleText(node, "title", 200), SingleText(node, "city", 120),
                SingleText(node, "neighborhood", 120), price, bedrooms, parking, features, listingUrl, updatedAt));
        }
        return properties.AsReadOnly();
    }

    private static int Number(XElement parent, string field)
    {
        if (!int.TryParse(SingleText(parent, field, 3), NumberStyles.None, CultureInfo.InvariantCulture, out var n) || n is < 0 or > 100)
            throw new InvalidDataException($"Invalid {field}.");
        return n;
    }

    private static string SingleText(XElement parent, string field, int maxLength)
    {
        var items = parent.Elements(field).ToList();
        if (items.Count != 1 || items[0].HasElements || items[0].HasAttributes)
            throw new InvalidDataException($"Missing or duplicate {field}.");
        return RequiredText(items[0].Value, field, maxLength);
    }

    private static string RequiredText(string? value, string field, int maxLength)
    {
        var clean = value?.Trim();
        if (string.IsNullOrEmpty(clean) || clean.Length > maxLength || clean.Any(char.IsControl))
            throw new InvalidDataException($"Invalid {field}.");
        return clean;
    }

    private static bool HasExplicitOffset(string value) =>
        value.EndsWith('Z') || (value.Length >= 6 && value[^3] == ':' && (value[^6] is '+' or '-'));
}
