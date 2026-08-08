using System.Text;

namespace ClassHelper.Core.RosterImport;

internal static class DelimitedTextReader
{
    private static readonly char[] CandidateDelimiters = [',', '\t', ';', '，'];

    public static IReadOnlyList<IReadOnlyList<string>> Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var text = Decode(bytes);
        var delimiter = DetectDelimiter(text);
        return Parse(text, delimiter);
    }

    internal static IReadOnlyList<IReadOnlyList<string>> Parse(string text, char delimiter)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;

        for (var index = 0; index < text.Length; index++)
        {
            var character = text[index];
            if (character == '"')
            {
                if (quoted && index + 1 < text.Length && text[index + 1] == '"')
                {
                    field.Append('"');
                    index++;
                }
                else
                {
                    quoted = !quoted;
                }

                continue;
            }

            if (!quoted && character == delimiter)
            {
                row.Add(field.ToString());
                field.Clear();
                continue;
            }

            if (!quoted && character is '\r' or '\n')
            {
                if (character == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
                {
                    index++;
                }

                row.Add(field.ToString());
                field.Clear();
                if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
                {
                    rows.Add(row.ToArray());
                }

                row.Clear();
                continue;
            }

            field.Append(character);
        }

        row.Add(field.ToString());
        if (row.Any(value => !string.IsNullOrWhiteSpace(value)))
        {
            rows.Add(row.ToArray());
        }

        return rows;
    }

    private static string Decode(byte[] bytes)
    {
        if (bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble))
        {
            return Encoding.UTF8.GetString(bytes.AsSpan(Encoding.UTF8.Preamble.Length));
        }

        if (bytes.AsSpan().StartsWith(Encoding.Unicode.Preamble))
        {
            return Encoding.Unicode.GetString(bytes.AsSpan(Encoding.Unicode.Preamble.Length));
        }

        if (bytes.AsSpan().StartsWith(Encoding.BigEndianUnicode.Preamble))
        {
            return Encoding.BigEndianUnicode.GetString(bytes.AsSpan(Encoding.BigEndianUnicode.Preamble.Length));
        }

        try
        {
            return new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(936).GetString(bytes);
        }
    }

    private static char DetectDelimiter(string text)
    {
        var sample = text.Length > 16_384 ? text[..16_384] : text;
        return CandidateDelimiters
            .Select(delimiter => (Delimiter: delimiter, Score: CountOutsideQuotes(sample, delimiter)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => Array.IndexOf(CandidateDelimiters, item.Delimiter))
            .First().Delimiter;
    }

    private static int CountOutsideQuotes(string text, char delimiter)
    {
        var count = 0;
        var quoted = false;
        foreach (var character in text)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (!quoted && character == delimiter)
            {
                count++;
            }
        }

        return count;
    }
}
