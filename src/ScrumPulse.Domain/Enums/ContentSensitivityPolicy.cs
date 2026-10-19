namespace ScrumPulse.Domain.Enums;

/// <summary>How much free text a board sync is permitted to bring across.</summary>
public enum ContentSensitivityPolicy
{
    // Default: client free text is a liability in a hosted database and adds nothing to the metrics.
    MetricsOnly = 0,

    RedactedTitles = 1,

    RedactedTitlesAndDescriptions = 2
}
