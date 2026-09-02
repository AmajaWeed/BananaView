using System;
using System.Collections.Generic;

namespace Viewer.Services;

public sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    public int Compare(string? a, string? b)
    {
        if (a is null || b is null) return string.CompareOrdinal(a, b);

        int i = 0, j = 0;
        while (i < a.Length && j < b.Length)
        {
            if (char.IsDigit(a[i]) && char.IsDigit(b[j]))
            {
                int si = i, sj = j;
                while (i < a.Length && char.IsDigit(a[i])) i++;
                while (j < b.Length && char.IsDigit(b[j])) j++;

                var na = a.Substring(si, i - si).TrimStart('0');
                var nb = b.Substring(sj, j - sj).TrimStart('0');
                if (na.Length != nb.Length) return na.Length - nb.Length;

                var cmp = string.CompareOrdinal(na, nb);
                if (cmp != 0) return cmp;
            }
            else
            {
                var cmp = char.ToLowerInvariant(a[i]).CompareTo(char.ToLowerInvariant(b[j]));
                if (cmp != 0) return cmp;
                i++; j++;
            }
        }

        return (a.Length - i) - (b.Length - j);
    }
}
