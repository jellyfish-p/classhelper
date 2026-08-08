using System.Globalization;
using System.Text;

namespace ClassHelper.Core.RosterImport;

public static class RosterTableInterpreter
{
    private const int HeaderSearchRowLimit = 12;

    private static readonly HashSet<string> NameHeaders = CreateAliases(
        "姓名", "名字", "学生姓名", "学员姓名", "同学姓名", "name", "studentname", "student");

    private static readonly HashSet<string> NumberHeaders = CreateAliases(
        "学号", "座号", "座位号", "编号", "序号", "号码", "学生编号", "studentid", "studentno", "id", "no", "number");

    private static readonly HashSet<string> ActiveHeaders = CreateAliases(
        "参与点名", "是否参与", "是否点名", "参与", "启用", "状态", "active", "enabled", "status");

    public static RosterImportDocument Interpret(
        string sourceName,
        string sheetName,
        IReadOnlyList<IReadOnlyList<string>> sourceRows,
        IReadOnlyList<int>? sourceRowNumbers = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceName);
        ArgumentNullException.ThrowIfNull(sourceRows);

        var rows = sourceRows
            .Select((values, index) => new RosterImportRow(
                sourceRowNumbers is not null && index < sourceRowNumbers.Count ? sourceRowNumbers[index] : index + 1,
                values.Select(value => value?.Trim() ?? string.Empty).ToArray()))
            .Where(row => row.Values.Any(value => !string.IsNullOrWhiteSpace(value)))
            .ToList();

        if (rows.Count == 0)
        {
            throw new InvalidDataException("文件中没有可导入的数据。");
        }

        var columnCount = rows.Max(row => row.Values.Count);
        if (columnCount == 0)
        {
            throw new InvalidDataException("文件中没有可识别的列。");
        }

        var header = FindHeader(rows, columnCount);
        var dataRows = header is null
            ? rows
            : rows.Skip(header.RowIndex + 1).ToList();

        if (dataRows.Count == 0)
        {
            throw new InvalidDataException("识别到表头，但表头下方没有名单数据。");
        }

        var nameColumn = header?.NameColumn ?? InferNameColumn(dataRows, columnCount);
        var numberColumn = header?.NumberColumn ?? InferNumberColumn(dataRows, columnCount, nameColumn);
        var activeColumn = header?.ActiveColumn;
        var columns = CreateColumns(rows, header, columnCount);

        return new RosterImportDocument(
            sourceName,
            sheetName,
            header is null ? null : rows[header.RowIndex].SourceRowNumber,
            columns,
            dataRows,
            nameColumn,
            numberColumn,
            activeColumn);
    }

    public static bool IsNameHeader(string value) => NameHeaders.Contains(NormalizeHeader(value));

    private static HeaderMatch? FindHeader(IReadOnlyList<RosterImportRow> rows, int columnCount)
    {
        HeaderMatch? best = null;
        var searchCount = Math.Min(rows.Count, HeaderSearchRowLimit);

        for (var rowIndex = 0; rowIndex < searchCount; rowIndex++)
        {
            int? nameColumn = null;
            int? numberColumn = null;
            int? activeColumn = null;
            var bestNameScore = 0;
            var bestNumberScore = 0;
            var bestActiveScore = 0;

            for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
            {
                var value = GetValue(rows[rowIndex], columnIndex);
                var nameScore = ScoreHeader(value, NameHeaders, "姓名");
                var numberScore = ScoreNumberHeader(value);
                var activeScore = ScoreHeader(value, ActiveHeaders, "参与", "启用", "状态");

                if (nameScore > bestNameScore)
                {
                    nameColumn = columnIndex;
                    bestNameScore = nameScore;
                }

                if (numberScore > bestNumberScore)
                {
                    numberColumn = columnIndex;
                    bestNumberScore = numberScore;
                }

                if (activeScore > bestActiveScore)
                {
                    activeColumn = columnIndex;
                    bestActiveScore = activeScore;
                }
            }

            var score = bestNameScore + bestNumberScore + bestActiveScore;
            if (nameColumn is null || score < 8)
            {
                continue;
            }

            var match = new HeaderMatch(rowIndex, nameColumn.Value, numberColumn, activeColumn, score);
            if (best is null || match.Score > best.Score)
            {
                best = match;
            }
        }

        return best;
    }

    private static int InferNameColumn(IReadOnlyList<RosterImportRow> rows, int columnCount)
    {
        var bestColumn = 0;
        var bestScore = double.MinValue;

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            var values = rows.Select(row => GetValue(row, columnIndex)).Where(value => value.Length > 0).Take(80).ToList();
            if (values.Count == 0)
            {
                continue;
            }

            var likelyNames = values.Count(LooksLikeName);
            var identifiers = values.Count(LooksLikeIdentifier);
            var score = likelyNames * 3.0 / values.Count - identifiers * 1.2 / values.Count - columnIndex * 0.01;
            if (score > bestScore)
            {
                bestScore = score;
                bestColumn = columnIndex;
            }
        }

        return bestColumn;
    }

    private static int? InferNumberColumn(IReadOnlyList<RosterImportRow> rows, int columnCount, int nameColumn)
    {
        int? bestColumn = null;
        var bestScore = 0.45;

        for (var columnIndex = 0; columnIndex < columnCount; columnIndex++)
        {
            if (columnIndex == nameColumn)
            {
                continue;
            }

            var values = rows.Select(row => GetValue(row, columnIndex)).Where(value => value.Length > 0).Take(80).ToList();
            if (values.Count == 0)
            {
                continue;
            }

            var identifierRatio = values.Count(LooksLikeIdentifier) / (double)values.Count;
            var uniqueRatio = values.Distinct(StringComparer.OrdinalIgnoreCase).Count() / (double)values.Count;
            var score = identifierRatio * 0.75 + uniqueRatio * 0.25;
            if (score > bestScore)
            {
                bestScore = score;
                bestColumn = columnIndex;
            }
        }

        return bestColumn;
    }

    private static IReadOnlyList<RosterImportColumn> CreateColumns(
        IReadOnlyList<RosterImportRow> rows,
        HeaderMatch? header,
        int columnCount)
    {
        var columns = new List<RosterImportColumn>(columnCount);
        for (var index = 0; index < columnCount; index++)
        {
            var excelName = GetColumnName(index);
            var headerText = header is null ? string.Empty : GetValue(rows[header.RowIndex], index);
            var displayName = string.IsNullOrWhiteSpace(headerText)
                ? $"{excelName} 列"
                : $"{headerText}（{excelName} 列）";
            columns.Add(new RosterImportColumn(index, displayName));
        }

        return columns;
    }

    private static int ScoreHeader(string value, IReadOnlySet<string> aliases, params string[] fragments)
    {
        var normalized = NormalizeHeader(value);
        if (aliases.Contains(normalized))
        {
            return 10;
        }

        return fragments.Any(fragment => normalized.Contains(fragment, StringComparison.OrdinalIgnoreCase)) ? 7 : 0;
    }

    private static int ScoreNumberHeader(string value)
    {
        var normalized = NormalizeHeader(value);
        return normalized switch
        {
            "序号" => 5,
            "编号" => 7,
            _ => ScoreHeader(value, NumberHeaders, "学号", "座号", "编号")
        };
    }

    private static bool LooksLikeName(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is < 2 or > 32 || LooksLikeIdentifier(trimmed))
        {
            return false;
        }

        return trimmed.Any(character => IsCjk(character) || char.IsLetter(character));
    }

    private static bool LooksLikeIdentifier(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is 0 or > 24 || trimmed.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return trimmed.All(character => char.IsLetterOrDigit(character) || character is '-' or '_' or '.')
            && trimmed.Any(char.IsDigit);
    }

    private static bool IsCjk(char character) =>
        character is >= '\u3400' and <= '\u9FFF';

    private static string GetValue(RosterImportRow row, int columnIndex) =>
        columnIndex < row.Values.Count ? row.Values[columnIndex] : string.Empty;

    private static HashSet<string> CreateAliases(params string[] values) =>
        values.Select(NormalizeHeader).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string NormalizeHeader(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value.Trim().Normalize(NormalizationForm.FormKC))
        {
            if (char.IsLetterOrDigit(character) || IsCjk(character))
            {
                builder.Append(char.ToLower(character, CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static string GetColumnName(int index)
    {
        var result = string.Empty;
        for (var value = index + 1; value > 0; value = (value - 1) / 26)
        {
            result = (char)('A' + (value - 1) % 26) + result;
        }

        return result;
    }

    private sealed record HeaderMatch(
        int RowIndex,
        int NameColumn,
        int? NumberColumn,
        int? ActiveColumn,
        int Score);
}
