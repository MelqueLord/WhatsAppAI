using System.IO.Compression;
using System.Text;
using WhatsAppAI.Application.Contacts;
using WhatsAppAI.Infrastructure.Contacts;

namespace WhatsAppAI.UnitTests.Contacts;

public sealed class ContactImportFileReaderTests
{
    private readonly ContactImportFileReader _reader = new();

    [Fact]
    public async Task ReadAsync_ReadsQuotedSemicolonCsvWithCaseInsensitiveHeaders()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "NOME;CONTATO\r\n\"Ana; Silva\";5511999990000\r\n"));

        var rows = await _reader.ReadAsync(stream, "contatos.csv", 5_000);

        var row = Assert.Single(rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("Ana; Silva", row.Name);
        Assert.Equal("5511999990000", row.Contact);
    }

    [Fact]
    public async Task ReadAsync_RejectsFileWithoutRequiredHeaders()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("nome;telefone\nAna;5511999990000"));

        var exception = await Assert.ThrowsAsync<ContactImportFileException>(
            () => _reader.ReadAsync(stream, "contatos.csv", 5_000));

        Assert.Contains("nome e contato", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_RejectsRowsAboveConfiguredLimit()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(
            "nome,contato\nAna,5511999990000\nBruno,5511888880000"));

        var exception = await Assert.ThrowsAsync<ContactImportFileException>(
            () => _reader.ReadAsync(stream, "contatos.csv", 1));

        Assert.Contains("no máximo 1", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReadAsync_ReadsFirstWorksheetFromXlsx()
    {
        await using var stream = BuildXlsx();

        var rows = await _reader.ReadAsync(stream, "contatos.xlsx", 5_000);

        var row = Assert.Single(rows);
        Assert.Equal("Ana", row.Name);
        Assert.Equal("5511999990000", row.Contact);
    }

    private static MemoryStream BuildXlsx()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "xl/workbook.xml", """
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Contatos" sheetId="1" r:id="rId1" /></sheets>
                </workbook>
                """);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
                </Relationships>
                """);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", """
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1"><c r="A1" t="inlineStr"><is><t>nome</t></is></c><c r="B1" t="inlineStr"><is><t>contato</t></is></c></row>
                    <row r="2"><c r="A2" t="inlineStr"><is><t>Ana</t></is></c><c r="B2"><v>5511999990000</v></c></row>
                  </sheetData>
                </worksheet>
                """);
        }

        stream.Position = 0;
        return stream;
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
