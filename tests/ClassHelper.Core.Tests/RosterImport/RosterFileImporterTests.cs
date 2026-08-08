using System.Text;
using ClassHelper.Core.RosterImport;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace ClassHelper.Core.Tests.RosterImport;

public sealed class RosterFileImporterTests
{
    [Fact]
    public void Interpreter_DetectsHeaderBelowTitleAndMapsCommonChineseColumns()
    {
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["初一（3）班学生名单"],
            ["序号", "学生姓名", "学号", "是否参与"],
            ["1", "张三", "2026001", "是"],
            ["2", "李四", "2026002", "否"]
        ];

        var document = RosterTableInterpreter.Interpret("名单.xlsx", "一班", rows);
        var result = RosterImportMapper.Map(
            document,
            document.SuggestedNameColumnIndex,
            document.SuggestedNumberColumnIndex,
            document.SuggestedActiveColumnIndex);

        Assert.Equal(2, document.HeaderRowNumber);
        Assert.Equal(1, document.SuggestedNameColumnIndex);
        Assert.Equal(2, document.SuggestedNumberColumnIndex);
        Assert.Equal(3, document.SuggestedActiveColumnIndex);
        Assert.Collection(
            result.Members,
            member => Assert.Equal(("张三", "2026001", true), (member.Name, member.Number, member.IsActive)),
            member => Assert.Equal(("李四", "2026002", false), (member.Name, member.Number, member.IsActive)));
    }

    [Fact]
    public void Interpreter_InfersNameAndNumberColumnsWithoutHeader()
    {
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["01", "王小明"],
            ["02", "陈晓华"],
            ["03", "赵一凡"]
        ];

        var document = RosterTableInterpreter.Interpret("名单.csv", "CSV", rows);

        Assert.Null(document.HeaderRowNumber);
        Assert.Equal(1, document.SuggestedNameColumnIndex);
        Assert.Equal(0, document.SuggestedNumberColumnIndex);
    }

    [Fact]
    public void Mapper_IgnoresDuplicateNumbersAndEmptyNames()
    {
        IReadOnlyList<IReadOnlyList<string>> rows =
        [
            ["姓名", "座号"],
            ["张三", "01"],
            ["李四", "01"],
            ["", "03"]
        ];
        var document = RosterTableInterpreter.Interpret("名单.csv", "CSV", rows);

        var result = RosterImportMapper.Map(document, 0, 1, null);

        Assert.Single(result.Members);
        Assert.Equal(2, result.Warnings.Count);
    }

    [Fact]
    public void Load_ReadsQuotedUtf8Csv()
    {
        var path = CreateTemporaryPath("csv");
        try
        {
            File.WriteAllText(path, "姓名,座号\r\n\"张,三\",01\r\n李四,02", new UTF8Encoding(true));

            var document = RosterFileImporter.Load(path);
            var result = RosterImportMapper.Map(document, 0, 1, null);

            Assert.Equal(["张,三", "李四"], result.Members.Select(member => member.Name));
            Assert.Equal(["01", "02"], result.Members.Select(member => member.Number));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ReadsChineseAnsiCsv()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var path = CreateTemporaryPath("csv");
        try
        {
            File.WriteAllText(path, "姓名,学号\r\n张三,001", Encoding.GetEncoding(936));

            var document = RosterFileImporter.Load(path);
            var result = RosterImportMapper.Map(
                document,
                document.SuggestedNameColumnIndex,
                document.SuggestedNumberColumnIndex,
                null);

            var member = Assert.Single(result.Members);
            Assert.Equal("张三", member.Name);
            Assert.Equal("001", member.Number);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_ReadsXlsxAndChoosesRosterWorksheet()
    {
        var path = CreateTemporaryPath("xlsx");
        try
        {
            CreateWorkbook(path);

            var document = RosterFileImporter.Load(path);
            var result = RosterImportMapper.Map(
                document,
                document.SuggestedNameColumnIndex,
                document.SuggestedNumberColumnIndex,
                document.SuggestedActiveColumnIndex);

            Assert.Equal("学生名单", document.SheetName);
            Assert.Equal(2, result.Members.Count);
            Assert.Equal("王小明", result.Members[0].Name);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Load_PreservesNumericIdentifiersWithLeadingZeroFormat()
    {
        var path = CreateTemporaryPath("xlsx");
        try
        {
            CreateFormattedNumberWorkbook(path);

            var document = RosterFileImporter.Load(path);
            var result = RosterImportMapper.Map(document, 0, 1, null);

            Assert.Equal("0007", Assert.Single(result.Members).Number);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string CreateTemporaryPath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"classhelper-{Guid.NewGuid():N}.{extension}");

    private static void CreateWorkbook(string path)
    {
        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var sheets = workbookPart.Workbook.AppendChild(new Sheets());

        AddSheet(workbookPart, sheets, "说明", 1,
        [
            ["请勿修改此页"]
        ]);
        AddSheet(workbookPart, sheets, "学生名单", 2,
        [
            ["初二（1）班"],
            ["学生姓名", "座位号", "状态"],
            ["王小明", "01", "正常"],
            ["李晓雨", "02", "缺席"]
        ]);

        workbookPart.Workbook.Save();
    }

    private static void CreateFormattedNumberWorkbook(string path)
    {
        using var document = SpreadsheetDocument.Create(path, SpreadsheetDocumentType.Workbook);
        var workbookPart = document.AddWorkbookPart();
        workbookPart.Workbook = new Workbook();
        var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
        stylesPart.Stylesheet = new Stylesheet(
            new NumberingFormats(
                new NumberingFormat { NumberFormatId = 164, FormatCode = "0000" })
            { Count = 1 },
            new CellFormats(
                new CellFormat(),
                new CellFormat { NumberFormatId = 164, ApplyNumberFormat = true })
            { Count = 2 });

        var sheets = workbookPart.Workbook.AppendChild(new Sheets());
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);
        sheetData.Append(
            new Row(
                CreateInlineCell("A1", "姓名"),
                CreateInlineCell("B1", "座号"))
            { RowIndex = 1 });
        sheetData.Append(
            new Row(
                CreateInlineCell("A2", "周雨辰"),
                new Cell
                {
                    CellReference = "B2",
                    DataType = CellValues.Number,
                    StyleIndex = 1,
                    CellValue = new CellValue("7")
                })
            { RowIndex = 2 });
        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = 1,
            Name = "名单"
        });
        workbookPart.Workbook.Save();
    }

    private static Cell CreateInlineCell(string reference, string value) => new()
    {
        CellReference = reference,
        DataType = CellValues.InlineString,
        InlineString = new InlineString(new Text(value))
    };

    private static void AddSheet(
        WorkbookPart workbookPart,
        Sheets sheets,
        string name,
        uint sheetId,
        IReadOnlyList<IReadOnlyList<string>> values)
    {
        var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
        var sheetData = new SheetData();
        worksheetPart.Worksheet = new Worksheet(sheetData);

        for (var rowIndex = 0; rowIndex < values.Count; rowIndex++)
        {
            var row = new Row { RowIndex = (uint)(rowIndex + 1) };
            for (var columnIndex = 0; columnIndex < values[rowIndex].Count; columnIndex++)
            {
                row.Append(new Cell
                {
                    CellReference = $"{GetColumnName(columnIndex)}{rowIndex + 1}",
                    DataType = CellValues.InlineString,
                    InlineString = new InlineString(new Text(values[rowIndex][columnIndex]))
                });
            }

            sheetData.Append(row);
        }

        sheets.Append(new Sheet
        {
            Id = workbookPart.GetIdOfPart(worksheetPart),
            SheetId = sheetId,
            Name = name
        });
    }

    private static string GetColumnName(int index) => ((char)('A' + index)).ToString();
}
