#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RahBuilder.Workflow;

public enum OutputCardKind
{
    Vision,
    File,
    Code,
    Diff,
    Final,
    Program,
    ProgramTree,
    ProgramFile,
    ProgramZip,
    Tree,
    Archive,
    Error
}

public sealed class OutputCard
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public OutputCardKind Kind { get; init; }
    public string Title { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Preview { get; init; } = "";
    public string FullContent { get; init; } = "";
    public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
    public string RelatedAttachment { get; init; } = "";
    public string ToolId { get; init; } = "";
    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;
    public string Metadata { get; init; } = "";
    public string DownloadUrl { get; init; } = "";

    public string ToDisplayText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[{Kind}] {Title}");
        if (!string.IsNullOrWhiteSpace(ToolId))
            sb.AppendLine("Tool: " + ToolId);
        sb.AppendLine("Timestamp: " + CreatedUtc.ToString("O"));
        if (!string.IsNullOrWhiteSpace(RelatedAttachment))
            sb.AppendLine($"Attachment: {RelatedAttachment}");
        if (Tags.Count > 0)
            sb.AppendLine("Tags: " + string.Join(", ", Tags));
        if (!string.IsNullOrWhiteSpace(Metadata))
            sb.AppendLine("Meta: " + Metadata);
        if (!string.IsNullOrWhiteSpace(DownloadUrl))
            sb.AppendLine("Download: " + DownloadUrl);
        if (!string.IsNullOrWhiteSpace(Summary))
            sb.AppendLine("Summary: " + Summary);
        if (!string.IsNullOrWhiteSpace(Preview))
        {
            sb.AppendLine("Preview:");
            sb.AppendLine(Preview);
        }
        if (!string.IsNullOrWhiteSpace(FullContent))
        {
            sb.AppendLine();
            sb.AppendLine("Full Content:");
            sb.AppendLine(FullContent);
        }
        return sb.ToString().TrimEnd();
    }

    public static OutputCard Truncate(OutputCard card, int maxPreview = 480, int maxContent = 2400)
    {
        return new OutputCard
        {
            Id = card.Id,
            Kind = card.Kind,
            Title = card.Title,
            Summary = card.Summary,
            RelatedAttachment = card.RelatedAttachment,
            Tags = card.Tags,
            ToolId = card.ToolId,
            CreatedUtc = card.CreatedUtc,
            Metadata = card.Metadata,
            DownloadUrl = card.DownloadUrl,
            Preview = Trim(card.Preview, maxPreview),
            FullContent = Trim(card.FullContent, maxContent)
        };
    }

    private static string Trim(string text, int max)
    {
        text ??= "";
        if (max <= 0) return text;
        return text.Length <= max ? text : text.Substring(0, max) + "…";
    }
}
