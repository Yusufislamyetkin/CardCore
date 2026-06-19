using System.Text.RegularExpressions;
using Serilog.Core;
using Serilog.Events;

namespace MiniBank.Shared.Infrastructure.Logging;

public sealed partial class PanScrubber : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var renderedMessage = logEvent.RenderMessage();
        if (!PanPattern().IsMatch(renderedMessage)) return;

        var scrubbed = PanPattern().Replace(renderedMessage, match =>
        {
            var pan = match.Value;
            return pan.Length >= 13
                ? $"{pan[..6]}{"".PadLeft(pan.Length - 10, '*')}{pan[^4..]}"
                : "****REDACTED****";
        });

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("SanitizedMessage", scrubbed));
    }

    [GeneratedRegex(@"\b\d{13,19}\b")]
    private static partial Regex PanPattern();
}
