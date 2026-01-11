#nullable enable
using System.Collections.Generic;
using RahBuilder.Common.Text;

namespace RahBuilder.Workflow;

public static class OutputSanitizer
{
    public static string Sanitize(string input)
    {
        return UserSafeText.Sanitize(input);
    }

    public static IReadOnlyList<string> Forbidden => UserSafeText.Forbidden;
}
