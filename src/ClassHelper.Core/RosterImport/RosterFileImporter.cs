using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ClassHelper.Core.RosterImport;

public static partial class RosterFileImporter
{
    private const int MaximumRows = 10_000;
    private const int MaximumColumns = 64;

    public static RosterImportDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("找不到要导入的名单文件。", path);
        }

        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".csv" => LoadDelimited(path),
            ".xlsx" => LoadWorkbook(path),
            _ => throw new NotSupportedException("仅支持 .xlsx 和 .csv 名单文件。")
        };
    }

    private static RosterImportDocument LoadDelimited(string path)
    {
        var rows = DelimitedTextReader.Read(path);
        return RosterTableInterpreter.Interpret(Path.GetFileName(path), "CSV", rows);
    }

    private static RosterImportDocument LoadWorkbook(string path)
    {
        using var spreadsheet = SpreadsheetDocument.Open(path, false);
        var workbookPart = spreadsheet.WorkbookPart
            ?? throw new InvalidDataException("XLSX 文件缺少工作簿内容。");
        var workbook = workbookPart.Workbook
            ?? throw new InvalidDataException("XLSX 文件缺少工作簿定义。");
        var sheets = workbook.Sheets?.Elements<Sheet>().ToList()
            ?? throw new InvalidDataException("XLSX 文件中没有工作表。");

        RosterImportDocument? bestDocument = null;
        var bestScore = int.MinValue;

        foreach (var sheet in sheets)
        {
            if (sheet.Id?.Value is not { Length: > 0 } relationshipId
                || workbookPart.GetPartById(relationshipId) is not WorksheetPart worksheetPart)
            {
                continue;
            }

            var (rows, rowNumbers) = ReadWorksheet(workbookPart, worksheetPart);
            if (rows.Count == 0)
            {
                continue;
            }

            try
            {
                var document = RosterTableInterpreter.Interpret(
                    Path.GetFileName(path),
                    sheet.Name?.Value ?? "工作表",
                    rows,
                    rowNumbers);
                var preview = RosterImportMapper.Map(
                    document,
                    document.SuggestedNameColumnIndex,
                    document.SuggestedNumberColumnIndex,
                    document.SuggestedActiveColumnIndex);
                var score = preview.Members.Count + (document.HeaderRowNumber is null ? 0 : 1_000);
                if (score > bestScore)
                {
                    bestDocument = document;
                    bestScore = score;
                }
            }
            catch (InvalidDataException)
            {
                // Continue looking for a worksheet that contains roster-like data.
            }
        }

        return bestDocument ?? throw new InvalidDataException("没有在工作簿中找到可识别的名单数据。");
    }

    private static (IReadOnlyList<IReadOnlyList<string>> Rows, IReadOnlyList<int> RowNumbers) ReadWorksheet(
        WorkbookPart workbookPart,
        WorksheetPart worksheetPart)
    {
        var rows = new List<IReadOnlyList<string>>();
        var rowNumbers = new List<int>();
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable?
            .Elements<SharedStringItem>()
            .Select(item => item.InnerText)
            .ToArray() ?? [];
        var sheetData = worksheetPart.Worksheet?.GetFirstChild<SheetData>();
        if (sheetData is null)
        {
            return (rows, rowNumbers);
        }

        foreach (var row in sheetData.Elements<Row>().Take(MaximumRows))
        {
            var values = new string[MaximumColumns];
            var lastUsedColumn = -1;

            foreach (var cell in row.Elements<Cell>())
            {
                var columnIndex = GetColumnIndex(cell.CellReference?.Value);
                if (columnIndex is < 0 or >= MaximumColumns)
                {
                    continue;
                }

                values[columnIndex] = ReadCellValue(workbookPart, cell, sharedStrings);
                if (values[columnIndex].Length > 0)
                {
                    lastUsedColumn = Math.Max(lastUsedColumn, columnIndex);
                }
            }

            if (lastUsedColumn < 0)
            {
                continue;
            }

            rows.Add(values[..(lastUsedColumn + 1)]);
            rowNumbers.Add((int)(row.RowIndex?.Value ?? (uint)(rowNumbers.Count + 1)));
        }

        return (rows, rowNumbers);
    }

    private static string ReadCellValue(WorkbookPart workbookPart, Cell cell, IReadOnlyList<string> sharedStrings)
    {
        var rawValue = cell.CellValue?.InnerText ?? cell.InlineString?.InnerText ?? string.Empty;
        if (rawValue.Length == 0)
        {
            return string.Empty;
        }

        if (cell.DataType?.Value == CellValues.SharedString
            && int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sharedIndex)
            && sharedIndex >= 0
            && sharedIndex < sharedStrings.Count)
        {
            return sharedStrings[sharedIndex].Trim();
        }

        if (cell.DataType?.Value == CellValues.Boolean)
        {
            return rawValue == "1" ? "是" : "否";
        }

        if (cell.DataType?.Value == CellValues.InlineString || cell.DataType?.Value == CellValues.String)
        {
            return (cell.InlineString?.InnerText ?? rawValue).Trim();
        }

        return ApplyLeadingZeroFormat(workbookPart, cell, rawValue).Trim();
    }

    private static string ApplyLeadingZeroFormat(WorkbookPart workbookPart, Cell cell, string value)
    {
        var stylesheet = workbookPart.WorkbookStylesPart?.Stylesheet;
        if (cell.StyleIndex?.Value is not { } styleIndex
            || stylesheet?.CellFormats is not { } cellFormats
            || styleIndex >= (cellFormats.Count?.Value ?? 0)
            || cellFormats.ElementAt((int)styleIndex) is not CellFormat cellFormat
            || cellFormat.NumberFormatId?.Value is not { } numberFormatId)
        {
            return value;
        }

        var formatCode = stylesheet.NumberingFormats?
            .Elements<NumberingFormat>()
            .FirstOrDefault(format => format.NumberFormatId?.Value == numberFormatId)?
            .FormatCode?.Value;
        if (formatCode is null || !LeadingZeroFormatRegex().IsMatch(formatCode))
        {
            return value;
        }

        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number.ToString(formatCode, CultureInfo.InvariantCulture)
            : value;
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
        {
            return -1;
        }

        var index = 0;
        var foundLetter = false;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
            {
                break;
            }

            foundLetter = true;
            index = index * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        }

        return foundLetter ? index - 1 : -1;
    }

    [GeneratedRegex("^0+$", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingZeroFormatRegex();
}
