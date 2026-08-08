namespace ClassHelper.Core.RosterImport;

public sealed record RosterImportColumn(int Index, string DisplayName);

public sealed record RosterImportRow(int SourceRowNumber, IReadOnlyList<string> Values);

public sealed record ImportedRosterMember(string Name, string? Number, bool IsActive, int SourceRowNumber);

public sealed record RosterImportResult(
    IReadOnlyList<ImportedRosterMember> Members,
    IReadOnlyList<string> Warnings);

public sealed class RosterImportDocument
{
    public RosterImportDocument(
        string sourceName,
        string sheetName,
        int? headerRowNumber,
        IReadOnlyList<RosterImportColumn> columns,
        IReadOnlyList<RosterImportRow> rows,
        int suggestedNameColumnIndex,
        int? suggestedNumberColumnIndex,
        int? suggestedActiveColumnIndex)
    {
        SourceName = sourceName;
        SheetName = sheetName;
        HeaderRowNumber = headerRowNumber;
        Columns = columns;
        Rows = rows;
        SuggestedNameColumnIndex = suggestedNameColumnIndex;
        SuggestedNumberColumnIndex = suggestedNumberColumnIndex;
        SuggestedActiveColumnIndex = suggestedActiveColumnIndex;
    }

    public string SourceName { get; }

    public string SheetName { get; }

    public int? HeaderRowNumber { get; }

    public IReadOnlyList<RosterImportColumn> Columns { get; }

    public IReadOnlyList<RosterImportRow> Rows { get; }

    public int SuggestedNameColumnIndex { get; }

    public int? SuggestedNumberColumnIndex { get; }

    public int? SuggestedActiveColumnIndex { get; }
}
