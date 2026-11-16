using System.Text;
using System.Xml;
using Kenlo.Api.Import;
using Xunit;

namespace Kenlo.Api.Tests;

public class ImportTests
{
    private const string ValidXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <synthetic-catalog format="demo-v1">
          <property code="SIM-01">
            <title>Casa fictícia</title><city>Campinas</city><neighborhood>Centro</neighborhood>
            <price>700000.00</price><bedrooms>3</bedrooms><parking>2</parking>
            <features><feature>Varanda</feature></features>
            <listingUrl>https://example.invalid/imoveis/SIM-01</listingUrl>
            <updatedAt>2026-01-01T00:00:00Z</updatedAt>
          </property>
        </synthetic-catalog>
        """;

    private static IReadOnlyList<SyntheticProperty> Parse(string value)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(value));
        return new SyntheticCatalogParser().Parse(input);
    }

    [Fact]
    public void Parses_Explicit_Synthetic_Format_Without_Inventing_Fields()
    {
        var property = Assert.Single(Parse(ValidXml));
        Assert.Equal("SIM-01", property.Code);
        Assert.Equal("Campinas", property.City);
        Assert.Equal(700000.00m, property.Price);
        Assert.Equal(3, property.Bedrooms);
        Assert.Equal(2, property.Parking);
        Assert.Equal(new[] { "Varanda" }, property.Features);
        Assert.Equal(TimeSpan.Zero, property.UpdatedAt.Offset);
    }

    [Fact]
    public void Normalizes_Synthetic_Code_For_Idempotent_Reimports()
    {
        var upper = Assert.Single(Parse(ValidXml));
        var lower = Assert.Single(Parse(ValidXml.Replace("code=\"SIM-01\"", "code=\"sim-01\"")));
        Assert.Equal(upper.Code, lower.Code);
    }

    [Fact]
    public void Rejects_Dtd_And_External_Entities()
    {
        var xml = "<!DOCTYPE synthetic-catalog [<!ENTITY xxe SYSTEM 'file:///etc/passwd'>]>" +
                  ValidXml.Replace("<?xml version=\"1.0\" encoding=\"utf-8\"?>", "").Replace("Casa fictícia", "&xxe;");
        Assert.Throws<XmlException>(() => Parse(xml));
    }

    [Fact]
    public void Rejects_A_Different_Format_Instead_Of_Claiming_Kenlo_Compatibility()
    {
        Assert.Throws<InvalidDataException>(() => Parse("<imoveis><imovel codigo=\"123\" /></imoveis>"));
    }

    [Fact]
    public void Rejects_Duplicate_Codes_Ignoring_Case()
    {
        const string first = "<property code=\"SIM-01\">";
        var start = ValidXml.IndexOf(first, StringComparison.Ordinal);
        var end = ValidXml.IndexOf("</property>", start, StringComparison.Ordinal) + "</property>".Length;
        var duplicate = ValidXml[start..end].Replace("SIM-01", "sim-01");
        var xml = ValidXml.Insert(end, duplicate);
        Assert.Throws<InvalidDataException>(() => Parse(xml));
    }

    [Theory]
    [InlineData("<price>700000.00</price>", "<price>-1</price>")]
    [InlineData("<price>700000.00</price>", "<price>700000,00</price>")]
    [InlineData("<bedrooms>3</bedrooms>", "<bedrooms>-1</bedrooms>")]
    [InlineData("<listingUrl>https://example.invalid/imoveis/SIM-01</listingUrl>", "<listingUrl>javascript:alert(1)</listingUrl>")]
    [InlineData("<listingUrl>https://example.invalid/imoveis/SIM-01</listingUrl>", "<listingUrl>https://real.example/imovel</listingUrl>")]
    [InlineData("<updatedAt>2026-01-01T00:00:00Z</updatedAt>", "<updatedAt>2026-01-01T00:00:00</updatedAt>")]
    [InlineData("<city>Campinas</city>", "<city>Campinas</city><city>Outro</city>")]
    public void Rejects_Invalid_Fields(string original, string replacement)
    {
        Assert.Throws<InvalidDataException>(() => Parse(ValidXml.Replace(original, replacement)));
    }

    [Fact]
    public void Rejects_Documents_Exceeding_Character_Limit()
    {
        var xml = ValidXml.Replace("Casa fictícia", new string('x', (int)SyntheticCatalogParser.MaxXmlCharacters + 1));
        Assert.Throws<XmlException>(() => Parse(xml));
    }
}
