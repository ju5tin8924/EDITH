using Edith.Screening.Core.Normalization;

namespace Edith.Screening.Tests;

public class TextNormalizerTests
{
    private readonly TextNormalizer _normalizer = new();

    [Theory]
    [InlineData("José Raúl Capablanca", "JOSE RAUL CAPABLANCA")]
    [InlineData("François Müller", "FRANCOIS MULLER")]
    [InlineData("Al-Mansoor, Tariq M.", "AL MANSOOR TARIQ M")]
    [InlineData("  SADDAM    HUSSEIN  ", "SADDAM HUSSEIN")]
    [InlineData("O'Connor-Smith", "O CONNOR SMITH")]
    public void Normalizes_accents_and_punctuation_cleanly(string input, string expected)
    {
        var result = _normalizer.Normalize(input, stripCorporateNoise: false);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("AL HILAL TRADING LLC", "AL HILAL")]
    [InlineData("GULF COMMERCE FZE", "GULF COMMERCE")]
    [InlineData("EMIRATES HOLDINGS GROUP LTD", "EMIRATES")]
    [InlineData("CENTRAL BANK", "CENTRAL")] // Strips BANK suffix, leaving core name
    public void Strips_corporate_suffixes_when_requested(string input, string expected)
    {
        var result = _normalizer.Normalize(input, stripCorporateNoise: true);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Handles_null_and_whitespace_inputs()
    {
        Assert.Equal(string.Empty, _normalizer.Normalize(null));
        Assert.Equal(string.Empty, _normalizer.Normalize("   "));
        Assert.Equal(string.Empty, _normalizer.Normalize("---"));
    }
}
