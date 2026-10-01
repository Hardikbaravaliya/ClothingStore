using System.Text;

namespace ClothingStore.Services.Common;

public static class SlugHelper
{
    /// <summary>"Girls Party Frock (Red)" => "girls-party-frock-red".</summary>
    public static string Generate(string text, int maxLength = 100)
    {
        var sb = new StringBuilder();
        var lastWasDash = false;
        foreach (var c in text.Trim().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash && sb.Length > 0)
            {
                sb.Append('-');
                lastWasDash = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        if (slug.Length > maxLength)
            slug = slug[..maxLength].TrimEnd('-');
        return slug.Length == 0 ? "item" : slug;
    }

    /// <summary>Appends -2, -3 ... until <paramref name="exists"/> returns false.</summary>
    public static async Task<string> MakeUniqueAsync(string baseSlug, Func<string, Task<bool>> exists)
    {
        var candidate = baseSlug;
        for (var n = 2; await exists(candidate); n++)
            candidate = $"{baseSlug}-{n}";
        return candidate;
    }
}
