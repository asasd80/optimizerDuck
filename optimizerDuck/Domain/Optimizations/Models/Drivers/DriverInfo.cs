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

    /// <summary>
    ///     Buses whose IDs carry no vendor/device pair but are still indexed by the catalog
    ///     (for example <c>ACPI\INT33A1</c> or <c>MONITOR\DELA0F0</c>).
    /// </summary>
    private static readonly string[] WholeIdBuses = ["ACPI", "MONITOR"];

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
    ///     The Microsoft Update Catalog query for this device, or <see langword="null"/> when the
    ///     catalog cannot have a driver for it (virtual and software devices).
    /// </summary>
    public string? CatalogQuery => BuildCatalogQuery(HardwareId);

    /// <summary>
    ///     False for devices whose driver only ships with Windows, such as Bluetooth services
    ///     (<c>BTHENUM</c>), software devices (<c>SWD</c>) and root-enumerated devices.
    /// </summary>
    public bool CanSearchCatalog => CatalogQuery is not null;

    public string CatalogSearchUrl =>
        CatalogSearchBase + Uri.EscapeDataString(CatalogQuery ?? string.Empty);

    /// <summary>
    ///     Trims a hardware ID to the form the catalog lists in a driver's supported IDs: the bus,
    ///     then every segment up to the device, e.g.
    ///     <c>PCI\VEN_10DE&amp;DEV_2484&amp;SUBSYS_...&amp;REV_A1</c> becomes
    ///     <c>PCI\VEN_10DE&amp;DEV_2484</c>. Subsystem, revision and interface suffixes would
    ///     make the search miss drivers written for the whole device family.
    ///     Returns <see langword="null"/> when the ID names no hardware the catalog could serve.
    /// </summary>
    public static string? BuildCatalogQuery(string? hardwareId)
    {
        if (string.IsNullOrWhiteSpace(hardwareId))
            return null;

        var id = hardwareId.Trim();
        var slash = id.IndexOf('\\', StringComparison.Ordinal);
        if (slash <= 0 || slash == id.Length - 1)
            return null;

        var bus = id[..slash];
        var parts = id[(slash + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries);

        var hasVendor = false;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (
                part.StartsWith("VEN_", StringComparison.OrdinalIgnoreCase)
                || part.StartsWith("VID_", StringComparison.OrdinalIgnoreCase)
            )
            {
                hasVendor = true;
            }
            else if (
                hasVendor
                && (
                    part.StartsWith("DEV_", StringComparison.OrdinalIgnoreCase)
                    || part.StartsWith("PID_", StringComparison.OrdinalIgnoreCase)
                )
            )
            {
                return $"{bus}\\{string.Join('&', parts[..(i + 1)])}";
            }
        }

        if (WholeIdBuses.Contains(bus, StringComparer.OrdinalIgnoreCase))
            return $"{bus}\\{parts[0]}";

        return null;
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
