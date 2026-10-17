using backend.Data;
using backend.Entities;
using backend.Services.Implementations;

namespace backend.Tests;

public sealed class LabelParserServiceTests
{
    [Theory]
    [InlineData("SUPPLIER: BASF - Sodium Hydroxide - LOT: B-2026-X89 - EXP: 15.11.2027", "B-2026-X89", "2027-11-15")]
    [InlineData("Sodium Hydroxide / PARTİ NO: ABC-42 / SKT: 2028-03-09", "ABC-42", "2028-03-09")]
    [InlineData("PRODUCT NAOH; P.NO: MIX_17; BBD 11/27", "MIX_17", "2027-11-30")]
    [InlineData("NaOH LOT: B-2026-X89 EXP: 15/11/2027", "B-2026-X89", "2027-11-15")]
    [InlineData("Sodum Hydroxide LOT: FUZZY-1 EXPIRY: 2029-04-03", "FUZZY-1", "2029-04-03")]
    public async Task ParseAsync_ExtractsLotDateAndFuzzyChemicalMatch(string rawText, string expectedLot, string expectedDate)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        database.Context.Chemicals.Add(new Chemical
        {
            Name = "Sodium Hydroxide",
            ChemicalCode = "NAOH",
            Stopped = false
        });
        await database.Context.SaveChangesAsync();

        var result = await new LabelParserService(database.Context).ParseAsync(rawText);

        Assert.Equal(expectedLot, result.ExtractedLotNumber);
        Assert.Equal(DateTime.Parse(expectedDate, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal),
            result.ExtractedExpirationDate);
        Assert.Equal("BAT-" + expectedLot, result.SuggestedBarcodePayload);
        Assert.Equal("Sodium Hydroxide", result.SuggestedChemicalName);
        Assert.Equal(database.Context.Chemicals.Single().Id, result.SuggestedChemicalId);
        Assert.Equal(rawText, result.RawText);
    }

    [Fact]
    public async Task ParseAsync_DoesNotSuggestStoppedChemical_AndHandlesMissingFields()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        database.Context.Chemicals.Add(new Chemical
        {
            Name = "Sodium Hydroxide",
            ChemicalCode = "NAOH",
            Stopped = true
        });
        await database.Context.SaveChangesAsync();

        var result = await new LabelParserService(database.Context).ParseAsync("LOT: X-1 EXP: 99/99 unknown item");

        Assert.Null(result.SuggestedChemicalId);
        Assert.Null(result.SuggestedChemicalName);
        Assert.Null(result.ExtractedExpirationDate);
        Assert.Equal("X-1", result.ExtractedLotNumber);
    }

    [Fact]
    public async Task ParseAsync_ExtractsLotAndDateFromNoisyUnlabeledOcr()
    {
        const string rawText = """
            Clr. L2
            IDO'M:('“&/ 3 g’w*aﬂ‘*r e/
            — s
            W /(o7:TEST-LOT-41
            N
            P :15.11.2013
            )
            ç 'l'—*ı.
            URDERR O DL, e SA A 170 NĞ T W o
            """;

        await using var database = await SqliteTestDatabase.CreateAsync();
        var result = await new LabelParserService(database.Context).ParseAsync(rawText);

        Assert.Equal("TEST-LOT-41", result.ExtractedLotNumber);
        Assert.Equal(new DateTime(2013, 11, 15, 0, 0, 0, DateTimeKind.Utc), result.ExtractedExpirationDate);
    }

    [Fact]
    public async Task ParseAsync_PrefersFutureDateWhenOcrContainsMultipleDates()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var result = await new LabelParserService(database.Context)
            .ParseAsync("Manufactured 01.02.2013; P : 15.11.2027");

        Assert.Equal(new DateTime(2027, 11, 15, 0, 0, 0, DateTimeKind.Utc), result.ExtractedExpirationDate);
    }

    [Fact]
    public async Task ParseAsync_UsesTokenContainmentForNoisyChemicalText()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        database.Context.Chemicals.Add(new Chemical
        {
            Name = "Polymer Stabilizer",
            ChemicalCode = "POLY-STAB",
            Stopped = false
        });
        await database.Context.SaveChangesAsync();

        var result = await new LabelParserService(database.Context).ParseAsync("POLYMER / STABIL");

        Assert.Equal("Polymer Stabilizer", result.SuggestedChemicalName);
    }
}
