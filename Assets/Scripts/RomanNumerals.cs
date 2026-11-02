using System.Text;
using System.Text.RegularExpressions;

// formation names keep a plain int ordinal in the data and turn into roman numerals only when shown
public static class RomanNumerals
{
    private static readonly int[] Vals = { 1000, 900, 500, 400, 100, 90, 50, 40, 10, 9, 5, 4, 1 };
    private static readonly string[] Syms = { "M", "CM", "D", "CD", "C", "XC", "L", "XL", "X", "IX", "V", "IV", "I" };

    public static string To(int n)
    {
        if (n < 1 || n > 3999) return n.ToString();
        var sb = new StringBuilder();
        for (int i = 0; i < Vals.Length; i++) while (n >= Vals[i]) { sb.Append(Syms[i]); n -= Vals[i]; }
        return sb.ToString();
    }

    // "Blue Century 1" -> "Blue Century I", "Archers 2" -> "Archers II"; names without a trailing number pass through
    public static string Pretty(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        return Regex.Replace(name, @"\b(\d{1,4})$", m => To(int.Parse(m.Value)));
    }
}
