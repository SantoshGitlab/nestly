using System.Text;
using FluentAssertions;
using Nestly.Infrastructure.Csv;
using Xunit;

namespace Nestly.Catalog.Tests;

/// <summary>
/// Task 133a (OWASP injection pass): <see cref="CsvWriter"/> is the shared
/// export path for every admin report and moderation CSV, several of which
/// include customer- or moderator-supplied free text (review text, notes).
/// These guard the CSV/formula-injection mitigation - a cell that opens with
/// '=', '+', '-', '@', a tab, or a carriage return is interpreted by Excel/
/// Sheets/LibreOffice as a formula, which is a real attack vector once an
/// admin opens the export (e.g. a review comment of
/// <c>=HYPERLINK("http://evil","click")</c>).
/// </summary>
public class CsvWriterTests
{
    [Theory]
    [InlineData("=HYPERLINK(\"http://evil.example\",\"x\")")]
    [InlineData("+1234")]
    [InlineData("-1234")]
    [InlineData("@SUM(A1:A2)")]
    public void Write_PrefixesFormulaTriggeringValuesWithAnApostrophe(string maliciousValue)
    {
        byte[] csv = CsvWriter.Write(["Comment"], [[maliciousValue]]);
        string text = Encoding.UTF8.GetString(csv);

        string dataLine = text.Split('\n')[1];

        // Whatever the exact quoting, the cell must no longer start with a
        // raw formula-trigger character - it must start with the neutralizing
        // apostrophe (directly, or as the first character inside an RFC 4180
        // quoted field).
        string cellStart = dataLine.TrimStart('"');
        cellStart.Should().StartWith("'");
    }

    [Fact]
    public void Write_LeavesOrdinaryValuesUnprefixed()
    {
        byte[] csv = CsvWriter.Write(["Name"], [["Regular Customer Name"]]);
        string text = Encoding.UTF8.GetString(csv);

        text.Should().Contain("Regular Customer Name");
        text.Should().NotContain("'Regular Customer Name");
    }

    [Fact]
    public void Write_StillQuotesFieldsContainingCommasAfterNeutralization()
    {
        byte[] csv = CsvWriter.Write(["Comment"], [["=A,B"]]);
        string text = Encoding.UTF8.GetString(csv);
        string dataLine = text.Split('\n')[1].TrimEnd('\r');

        dataLine.Should().Be("\"'=A,B\"");
    }

    [Fact]
    public void Write_EmptyAndNullValuesRoundTripAsEmptyFields()
    {
        byte[] csv = CsvWriter.Write(["A", "B"], [[null, ""]]);
        string text = Encoding.UTF8.GetString(csv);
        string dataLine = text.Split('\n')[1].TrimEnd('\r');

        dataLine.Should().Be(",");
    }
}
