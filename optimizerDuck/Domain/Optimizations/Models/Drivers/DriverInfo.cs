using optimizerDuck.Services.Configuration;

namespace optimizerDuck.Domain.Optimizations.Models.Drivers;

/// <summary>
///     An installed device driver, as reported by <c>Win32_PnPSignedDriver</c>.
/// </summary>
/// <remarks>
///     Read-only snapshot: the Drivers page lists and searches drivers but never changes them, so
///     nothing here goes through <c>OperationRunner</c>.
/// </remarks>
public sealed class DriverInfo
{
    private const string CatalogSearchBase =
        "https://www.catalog.update.microsoft.com/Search.aspx?q=";

    public required string DeviceName { get; init; }

    public string Provider { get; init; } = string.Empty;

    public string Version { get; init; } = string.Empty;

    public DateTime? Date { get; init; }

    public string DeviceClass { get; init; } = string.Empty;

    public string InfName { get; init; } = string.Empty;

    public string HardwareId { get; init; } = string.Empty;

    public string DeviceId { get; init; } = string.Empty;

    public bool IsSigned { get; init; } = true;

    /// <summary>Inbox drivers shipped by Microsoft; hidden by default so vendor drivers stand out.</summary>
    public bool IsMicrosoft => Provider.Contains("Microsoft", StringComparison.OrdinalIgnoreCase);

    public bool HasDate => Date.HasValue;

    public string DateText => Date?.ToString("d", Loc.CurrentCulture) ?? string.Empty;

    public bool HasHardwareId => !string.IsNullOrWhiteSpace(HardwareId);

    /// <summary>
    ///     Microsoft Update Catalog search for this device, keyed on its hardware ID when one is
    ///     known (the catalog indexes drivers by ID), otherwise on the device name.
    /// </summary>
    public string CatalogSearchUrl =>
        CatalogSearchBase + Uri.EscapeDataString(BuildCatalogQuery(HardwareId, DeviceName));

    /// <summary>
    ///     Reduces a hardware ID to the vendor and device pair the catalog matches on, e.g.
    ///     <c>PCI\VEN_10DE&amp;DEV_2484&amp;SUBSYS_...&amp;REV_A1</c> becomes
    ///     <c>VEN_10DE&amp;DEV_2484</c>. Subsystem and revision suffixes make catalog searches
    ///     miss drivers that cover the whole device family.
    /// </summary>
    public static string BuildCatalogQuery(string? hardwareId, string deviceName)
    {
        if (string.IsNullOrWhiteSpace(hardwareId))
            return deviceName.Trim();

        var id = hardwareId.Trim();
        var slash = id.IndexOf('\\', StringComparison.Ordinal);
        var body = slash >= 0 ? id[(slash + 1)..] : id;
        var parts = body.Split('&', StringSplitOptions.RemoveEmptyEntries);

        string? vendor = null;
        string? device = null;
        foreach (var part in parts)
        {
            if (
                vendor is null
                && (
                    part.StartsWith("VEN_", StringComparison.OrdinalIgnoreCase)
                    || part.StartsWith("VID_", StringComparison.OrdinalIgnoreCase)
                )
            )
                vendor = part;
            else if (
                device is null
                && (
                    part.StartsWith("DEV_", StringComparison.OrdinalIgnoreCase)
                    || part.StartsWith("PID_", StringComparison.OrdinalIgnoreCase)
                )
            )
                device = part;
        }

        if (vendor is not null && device is not null)
            return $"{vendor}&{device}";

        return id;
    }

    /// <summary>
    ///     True when the driver mentions <paramref name="term"/> in any field a user would search by.
    /// </summary>
    public bool Matches(string term) =>
        DeviceName.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Provider.Contains(term, StringComparison.OrdinalIgnoreCase)
        || Version.Contains(term, StringComparison.OrdinalIgnoreCase)
        || DeviceClass.Contains(term, StringComparison.OrdinalIgnoreCase)
        || InfName.Contains(term, StringComparison.OrdinalIgnoreCase)
        || HardwareId.Contains(term, StringComparison.OrdinalIgnoreCase);
}
