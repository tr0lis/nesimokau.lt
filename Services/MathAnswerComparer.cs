using System.Globalization;

namespace nesimokau.lt.Services;

public static class MathAnswerComparer
{
    public static bool IsMatch(string studentAnswer, IEnumerable<string> acceptedAnswers)
        => acceptedAnswers.Any(answer => IsMatch(studentAnswer, answer));

    public static bool IsMatch(string studentAnswer, string expectedAnswer)
    {
        var student = Normalize(studentAnswer);
        var expected = Normalize(expectedAnswer);

        if (string.Equals(student, expected, StringComparison.OrdinalIgnoreCase)) return true;

        return decimal.TryParse(student, NumberStyles.Number, CultureInfo.InvariantCulture, out var studentNumber)
            && decimal.TryParse(expected, NumberStyles.Number, CultureInfo.InvariantCulture, out var expectedNumber)
            && studentNumber == expectedNumber;
    }

    private static string Normalize(string value)
        => value.Trim()
            .Replace(" ", string.Empty)
            .TrimEnd('.', ',')
            .Replace(',', '.');
}
