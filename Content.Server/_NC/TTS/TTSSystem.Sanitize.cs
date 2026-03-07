using System.Text;
using System.Text.RegularExpressions;
using Content.Server.Chat.Systems;


namespace Content.Server._NC.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem
{
    // #Misfits Change: use source-generated cached regexes to fix RA0026
    [GeneratedRegex(@"[^a-zA-Z0-9,\-+?!. ]")]
    private static partial Regex SanitizeCharsRegex();

    [GeneratedRegex(@"(?<![a-zA-Z])[a-zA-Z]+?(?![a-zA-Z])", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex ReplaceWordRegex();

    [GeneratedRegex(@"(?<=[1-90])(\.|,)(?=[1-90])")]
    private static partial Regex DecimalPointRegex();

    [GeneratedRegex(@"\d+")]
    private static partial Regex DigitsRegex();

    private void OnTransformSpeech(TransformSpeechEvent args)
    {
        if (!_isEnabled) return;
        args.Message = args.Message.Replace("+", "");
    }

    private string Sanitize(string text)
    {
        text = text.Trim();
        text = SanitizeCharsRegex().Replace(text, "");
        text = ReplaceWordRegex().Replace(text, ReplaceMatchedWord);
        text = DecimalPointRegex().Replace(text, " point ");
        text = DigitsRegex().Replace(text, ReplaceWord2Num);
        text = text.Trim();
        return text;
    }

    private string ReplaceMatchedWord(Match word)
    {
        if (WordReplacement.TryGetValue(word.Value.ToLower(), out var replace))
            return replace;
        return word.Value;
    }

    private string ReplaceWord2Num(Match word)
    {
        if (!long.TryParse(word.Value, out var number))
            return word.Value;
        return NumberConverter.NumberToText(number);
    }

    private static readonly IReadOnlyDictionary<string, string> WordReplacement =
        new Dictionary<string, string>();
}

// Source: https://codelab.ru/s/csharp/digits2phrase
public static class NumberConverter
{
	private static readonly string[] Ones =
	{
		"", "one", "two", "three", "four", "five", "six",
		"seven", "eight", "nine", "ten", "eleven",
		"twelve", "thirteen", "fourteen", "fifteen",
		"sixteen", "seventeen", "eighteen", "nineteen"
	};

	private static readonly string[] Tens =
	{
		"", "", "twenty", "thirty", "forty", "fifty",
		"sixty", "seventy", "eighty", "ninety"
	};

	public static string NumberToText(long value, bool male = true)
    {
        if (value >= 1000000000000000L || value <= -1000000000000000L)
            return string.Empty;

        if (value == 0)
            return "zero";

		var str = new StringBuilder();

		if (value < 0)
		{
			str.Append("minus");
			value = -value;
		}

        value = AppendScale(value, 1000000000000000L, str, "quadrillion");
        value = AppendScale(value, 1000000000000L, str, "trillion");
        value = AppendScale(value, 1000000000L, str, "billion");
        value = AppendScale(value, 1000000L, str, "million");
        value = AppendScale(value, 1000L, str, "thousand");

        if (value > 0)
            AppendWithSpace(str, ConvertBelowThousand((int) value));

		return str.ToString();
	}

	private static long AppendScale(long value, long scale, StringBuilder str, string scaleName)
	{
		var amount = (int)(value / scale);
		if (amount > 0)
		{
			AppendWithSpace(str, ConvertBelowThousand(amount));
			AppendWithSpace(str, scaleName);
			return value % scale;
		}
		return value;
	}

	private static string ConvertBelowThousand(int value)
	{
		var sb = new StringBuilder();
		if (value >= 100)
		{
			sb.Append(Ones[value / 100]);
			sb.Append(" hundred");
			value %= 100;
			if (value > 0)
				sb.Append(' ');
		}

		if (value >= 20)
		{
			sb.Append(Tens[value / 10]);
			value %= 10;
			if (value > 0)
				sb.Append(' ').Append(Ones[value]);
		}
		else if (value > 0)
		{
			sb.Append(Ones[value]);
		}

		return sb.ToString();
	}

	private static void AppendWithSpace(StringBuilder stringBuilder, string str)
	{
		if (string.IsNullOrWhiteSpace(str))
			return;
		if (stringBuilder.Length > 0)
			stringBuilder.Append(' ');
		stringBuilder.Append(str);
	}
}
