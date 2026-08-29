namespace WhatsAppAI.Application.Contacts;

public sealed record ContactImportRow(int RowNumber, string? Name, string? Contact);

public sealed record ContactImportError(int Row, string Code, string Message);

public sealed record ContactImportResult(
    int Total,
    int Imported,
    int Skipped,
    int Invalid,
    IReadOnlyList<ContactImportError> Errors);

public sealed class ContactImportFileException : Exception
{
    public ContactImportFileException()
    {
    }

    public ContactImportFileException(string message) : base(message)
    {
    }

    public ContactImportFileException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public interface IContactImportFileReader
{
    Task<IReadOnlyList<ContactImportRow>> ReadAsync(
        Stream stream,
        string fileName,
        int maxRows,
        CancellationToken cancellationToken = default);
}
