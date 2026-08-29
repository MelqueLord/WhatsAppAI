using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using WhatsAppAI.Application.Contacts;

namespace WhatsAppAI.Infrastructure.Contacts;

public sealed class ContactImportFileReader : IContactImportFileReader
{
    private const long MaxExpandedPartBytes = 10 * 1024 * 1024;
    private static readonly XNamespace SpreadsheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

    public async Task<IReadOnlyList<ContactImportRow>> ReadAsync(
        Stream stream,
        string fileName,
        int maxRows,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".csv" => await ReadCsvAsync(stream, maxRows, cancellationToken),
            ".xlsx" => ReadXlsx(stream, maxRows),
            _ => throw new ContactImportFileException("Use um arquivo .csv ou .xlsx.")
        };
    }

    private static async Task<IReadOnlyList<ContactImportRow>> ReadCsvAsync(
        Stream stream,
        int maxRows,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, true, leaveOpen: true);
        var content = await reader.ReadToEndAsync(cancellationToken);
        var delimiter = DetectDelimiter(content);
        return MapRows(ParseDelimited(content, delimiter), maxRows);
    }

    private static List<ContactImportRow> ReadXlsx(Stream stream, int maxRows)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var workbook = LoadXml(archive, "xl/workbook.xml");
            var relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels");
            var firstSheet = workbook.Root?
                .Element(SpreadsheetNs + "sheets")?
                .Elements(SpreadsheetNs + "sheet")
                .FirstOrDefault()
                ?? throw new ContactImportFileException("A planilha não possui abas.");

            var relationshipId = firstSheet.Attribute(OfficeRelationshipNs + "id")?.Value;
            var target = relationships.Root?
                .Elements(PackageRelationshipNs + "Relationship")
                .FirstOrDefault(item => item.Attribute("Id")?.Value == relationshipId)?
                .Attribute("Target")?.Value
                ?? throw new ContactImportFileException("Não foi possível ler a primeira aba da planilha.");

            var sharedStrings = ReadSharedStrings(archive);
            var worksheet = LoadXml(archive, ResolvePartPath("xl/workbook.xml", target));
            var table = worksheet
                .Descendants(SpreadsheetNs + "row")
                .Select(ReadWorksheetRow)
                .Where(row => row.Cells.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
                .Select(row => new ParsedRow(row.RowNumber, row.Cells))
                .ToList();

            var resolved = table.Select(row => new ParsedRow(
                row.RowNumber,
                row.Cells.ToDictionary(
                    pair => pair.Key,
                    pair => ResolveCellValue(pair.Value, sharedStrings))));

            return MapRows(resolved, maxRows);
        }
        catch (ContactImportFileException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw new ContactImportFileException("O arquivo .xlsx é inválido ou está corrompido.");
        }
        catch (Exception exception) when (exception is IOException or System.Xml.XmlException or FormatException)
        {
            throw new ContactImportFileException("Não foi possível ler o arquivo .xlsx.");
        }
    }

    private static XDocument LoadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path)
            ?? throw new ContactImportFileException("O arquivo .xlsx não contém a estrutura esperada.");
        if (entry.Length > MaxExpandedPartBytes)
            throw new ContactImportFileException("O conteúdo interno da planilha excede o limite permitido.");

        using var entryStream = entry.Open();
        return XDocument.Load(entryStream, LoadOptions.None);
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
            return [];
        if (entry.Length > MaxExpandedPartBytes)
            throw new ContactImportFileException("O conteúdo interno da planilha excede o limite permitido.");

        using var entryStream = entry.Open();
        var document = XDocument.Load(entryStream, LoadOptions.None);
        return document.Descendants(SpreadsheetNs + "si")
            .Select(item => string.Concat(item.Descendants(SpreadsheetNs + "t").Select(text => text.Value)))
            .ToList();
    }

    private static (int RowNumber, Dictionary<int, string> Cells) ReadWorksheetRow(XElement row)
    {
        var rowNumber = int.TryParse(row.Attribute("r")?.Value, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
        var cells = new Dictionary<int, string>();

        foreach (var cell in row.Elements(SpreadsheetNs + "c"))
        {
            var reference = cell.Attribute("r")?.Value ?? string.Empty;
            var column = GetColumnIndex(reference);
            var type = cell.Attribute("t")?.Value;
            var raw = type == "inlineStr"
                ? string.Concat(cell.Descendants(SpreadsheetNs + "t").Select(text => text.Value))
                : cell.Element(SpreadsheetNs + "v")?.Value ?? string.Empty;
            cells[column] = type == "s" ? $"#shared:{raw}" : raw;
        }

        return (rowNumber, cells);
    }

    private static string ResolveCellValue(string value, List<string> sharedStrings)
    {
        const string prefix = "#shared:";
        if (!value.StartsWith(prefix, StringComparison.Ordinal))
            return value;

        return int.TryParse(value[prefix.Length..], CultureInfo.InvariantCulture, out var index) &&
               index >= 0 && index < sharedStrings.Count
            ? sharedStrings[index]
            : string.Empty;
    }

    private static int GetColumnIndex(string reference)
    {
        var index = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter))
            index = (index * 26) + char.ToUpperInvariant(character) - 'A' + 1;
        return index - 1;
    }

    private static string ResolvePartPath(string source, string target)
    {
        if (target.StartsWith('/'))
            return target.TrimStart('/');

        var segments = new List<string>(source.Split('/').SkipLast(1));
        foreach (var segment in target.Replace('\\', '/').Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count > 0) segments.RemoveAt(segments.Count - 1);
            }
            else if (segment is not "." and not "")
            {
                segments.Add(segment);
            }
        }

        return string.Join('/', segments);
    }

    private static char DetectDelimiter(string content)
    {
        var header = content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
        var candidates = new[] { ',', ';', '\t' };
        return candidates.MaxBy(candidate => header.Count(character => character == candidate));
    }

    private static List<ParsedRow> ParseDelimited(string content, char delimiter)
    {
        var rows = new List<ParsedRow>();
        var cells = new List<string>();
        var value = new StringBuilder();
        var quoted = false;
        var rowNumber = 1;

        var index = 0;
        while (index < content.Length)
        {
            var character = content[index];
            if (character == '"')
            {
                if (quoted && index + 1 < content.Length && content[index + 1] == '"')
                {
                    value.Append('"');
                    index += 2;
                    continue;
                }
                else
                {
                    quoted = !quoted;
                }
            }
            else if (character == delimiter && !quoted)
            {
                cells.Add(value.ToString());
                value.Clear();
            }
            else if ((character == '\r' || character == '\n') && !quoted)
            {
                cells.Add(value.ToString());
                value.Clear();
                AddParsedRow(rows, rowNumber++, cells);
                cells = [];
                if (character == '\r' && index + 1 < content.Length && content[index + 1] == '\n')
                {
                    index += 2;
                    continue;
                }
            }
            else
            {
                value.Append(character);
            }

            index++;
        }

        if (quoted)
            throw new ContactImportFileException("O arquivo CSV contém aspas não finalizadas.");

        cells.Add(value.ToString());
        AddParsedRow(rows, rowNumber, cells);
        return rows;
    }

    private static void AddParsedRow(List<ParsedRow> rows, int rowNumber, IReadOnlyList<string> cells)
    {
        if (cells.All(string.IsNullOrWhiteSpace))
            return;

        rows.Add(new ParsedRow(
            rowNumber,
            cells.Select((cell, index) => (cell, index)).ToDictionary(item => item.index, item => item.cell)));
    }

    private static List<ContactImportRow> MapRows(IEnumerable<ParsedRow> source, int maxRows)
    {
        var rows = source.ToList();
        if (rows.Count == 0)
            throw new ContactImportFileException("O arquivo está vazio.");

        var header = rows[0];
        var nameColumn = FindHeader(header.Cells, "nome");
        var contactColumn = FindHeader(header.Cells, "contato");
        if (nameColumn is null || contactColumn is null)
            throw new ContactImportFileException("O arquivo deve conter os cabeçalhos nome e contato.");

        var dataRows = rows.Skip(1).ToList();
        if (dataRows.Count > maxRows)
            throw new ContactImportFileException($"O arquivo deve conter no máximo {maxRows} contatos.");

        return dataRows
            .Select(row => new ContactImportRow(
                row.RowNumber,
                row.Cells.GetValueOrDefault(nameColumn.Value),
                row.Cells.GetValueOrDefault(contactColumn.Value)))
            .ToList();
    }

    private static int? FindHeader(IReadOnlyDictionary<int, string> cells, string expected)
    {
        foreach (var cell in cells)
        {
            if (cell.Value.Trim().Equals(expected, StringComparison.OrdinalIgnoreCase))
                return cell.Key;
        }

        return null;
    }

    private sealed record ParsedRow(int RowNumber, Dictionary<int, string> Cells);
}
