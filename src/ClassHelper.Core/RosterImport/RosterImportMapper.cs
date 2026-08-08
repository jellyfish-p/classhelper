namespace ClassHelper.Core.RosterImport;

public static class RosterImportMapper
{
    private static readonly string[] InactiveValues =
    [
        "否", "不参与", "不参加", "禁用", "停用", "缺席", "请假", "false", "no", "n", "0", "×", "x"
    ];

    public static RosterImportResult Map(
        RosterImportDocument document,
        int nameColumnIndex,
        int? numberColumnIndex,
        int? activeColumnIndex)
    {
        ArgumentNullException.ThrowIfNull(document);

        if (nameColumnIndex < 0 || nameColumnIndex >= document.Columns.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(nameColumnIndex));
        }

        var members = new List<ImportedRosterMember>();
        var warnings = new List<string>();
        var numbers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var namesWithoutNumber = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var emptyNameCount = 0;
        var duplicateCount = 0;

        foreach (var row in document.Rows)
        {
            var name = GetValue(row, nameColumnIndex).Trim();
            if (string.IsNullOrWhiteSpace(name) || RosterTableInterpreter.IsNameHeader(name))
            {
                emptyNameCount++;
                continue;
            }

            var number = numberColumnIndex is { } numberIndex && numberIndex != nameColumnIndex
                ? NullIfWhiteSpace(GetValue(row, numberIndex))
                : null;
            var isActive = activeColumnIndex is { } activeIndex && activeIndex != nameColumnIndex
                ? ParseActive(GetValue(row, activeIndex))
                : true;

            var isDuplicate = number is not null
                ? !numbers.Add(number)
                : !namesWithoutNumber.Add(NormalizeKey(name));

            if (isDuplicate)
            {
                duplicateCount++;
                continue;
            }

            members.Add(new ImportedRosterMember(name, number, isActive, row.SourceRowNumber));
        }

        if (emptyNameCount > 0)
        {
            warnings.Add($"已忽略 {emptyNameCount} 行空姓名或重复表头");
        }

        if (duplicateCount > 0)
        {
            warnings.Add($"已忽略 {duplicateCount} 行重复编号或重复姓名");
        }

        return new RosterImportResult(members, warnings);
    }

    private static string GetValue(RosterImportRow row, int index) =>
        index >= 0 && index < row.Values.Count ? row.Values[index] : string.Empty;

    private static string? NullIfWhiteSpace(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static bool ParseActive(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        return normalized.Length == 0 || !InactiveValues.Contains(normalized, StringComparer.OrdinalIgnoreCase);
    }

    private static string NormalizeKey(string value) =>
        string.Concat(value.Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant();
}
