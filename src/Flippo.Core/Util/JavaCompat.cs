using System.Text.RegularExpressions;

namespace Flippo.Core.Util;

/// <summary>
/// JVM-Verhaltens-Kompatibilität. Wo sich JVM und CLR unterscheiden, gewinnt hier immer
/// das Android-Verhalten (Plan 1.5 / 4.2), damit die Domain-Logik bit-genau gleich rechnet.
/// </summary>
public static partial class JavaCompat
{
    /// <summary>
    /// Java <c>Math.round(double)</c> = <c>floor(v + 0.5)</c> (half-up). .NET
    /// <c>Math.Round</c> nutzt banker's rounding (2.5 → 2) — für alle SRS-Rundungen falsch.
    /// </summary>
    public static long RoundHalfUp(double value) => (long)Math.Floor(value + 0.5);

    /// <summary>
    /// Java-Regex <c>\s</c> (ohne UNICODE_CHARACTER_CLASS) = <c>[ \t\n\x0B\f\r]</c> — NUR ASCII,
    /// inkl. Vertical Tab (U+000B). .NET-<c>\s</c> würde Unicode-Whitespace/NBSP mitfassen.
    /// </summary>
    [GeneratedRegex(@"[ \t\n\x0B\f\r]+")]
    public static partial Regex AsciiWhitespace();

    /// <summary>
    /// Java <c>\p{InCombiningDiacriticalMarks}</c> = Unicode-Block U+0300–U+036F.
    /// .NET-Äquivalent ist der Named Block <c>\p{IsCombiningDiacriticalMarks}</c> — NICHT Kategorie
    /// <c>Mn</c> (die würde arabische Harakat / hebräische Nikud strippen → Abweichung von Android).
    /// </summary>
    [GeneratedRegex(@"\p{IsCombiningDiacriticalMarks}+")]
    public static partial Regex CombiningDiacriticalMarks();
}
