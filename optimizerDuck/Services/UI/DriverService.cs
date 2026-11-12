using System.Management;
using Microsoft.Extensions.Logging;
using optimizerDuck.Domain.Optimizations.Models.Drivers;
using optimizerDuck.Services.System;

namespace optimizerDuck.Services.UI;

/// <summary>
///     Reads the installed device drivers for the Drivers page. Read-only: it never installs,
///     updates, or removes a driver.
/// </summary>
public sealed class DriverService(ILogger<DriverService> logger)
{
    private const string Query =
        "SELECT DeviceName, DriverProviderName, DriverVersion, DriverDate, DeviceClass, InfName, HardWareID, DeviceID, IsSigned FROM Win32_PnPSignedDriver";

    /// <summary>
    ///     Returns one entry per device that has a named driver, ordered by device name.
    ///     Returns an empty list when WMI is unavailable.
    /// </summary>
    public async Task<IReadOnlyList<DriverInfo>> GetInstalledDriversAsync(
        CancellationToken cancellationToken = default
    )
    {
        var drivers = await Task.Run(
                () =>
                    WmiHelper.Query(
                        Query,
                        rows => rows.Select(row => Map(name => Read(row, name))).ToList()
                    ),
                cancellationToken
            )
            .ConfigureAwait(false);

        if (drivers is null)
        {
            logger.LogWarning("Driver query returned no data");
            return [];
        }

        var result = drivers
            .OfType<DriverInfo>()
            .DistinctBy(d => d.DeviceId.Length > 0 ? d.DeviceId : d.DeviceName + d.Version)
            .OrderBy(d => d.DeviceName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        logger.LogInformation("Found {Count} installed drivers", result.Count);
        return result;
    }

    /// <summary>
    ///     Builds a <see cref="DriverInfo"/> from one WMI row. Rows without a device name (for
    ///     example software-only components) are skipped, so this returns <see langword="null"/>.
    /// </summary>
    internal static DriverInfo? Map(Func<string, object?> get)
    {
        var name = (get("DeviceName") as string)?.Trim();
        if (string.IsNullOrEmpty(name))
            return null;

        return new DriverInfo
        {
            DeviceName = name,
            Provider = (get("DriverProviderName") as string)?.Trim() ?? string.Empty,
            Version = (get("DriverVersion") as string)?.Trim() ?? string.Empty,
            Date = ParseCimDate(get("DriverDate") as string),
            DeviceClass = (get("DeviceClass") as string)?.Trim() ?? string.Empty,
            InfName = (get("InfName") as string)?.Trim() ?? string.Empty,
            HardwareId = (get("HardWareID") as string)?.Trim() ?? string.Empty,
            DeviceId = (get("DeviceID") as string)?.Trim() ?? string.Empty,
            IsSigned = get("IsSigned") is not bool signed || signed,
        };
    }

    /// <summary>
    ///     Parses a CIM datetime (<c>yyyyMMddHHmmss.ffffff+UUU</c>). Driver dates carry no
    ///     meaningful time of day, so only the date part is kept.
    /// </summary>
    internal static DateTime? ParseCimDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 8)
            return null;

        return DateTime.TryParseExact(
            value[..8],
            "yyyyMMdd",
            global::System.Globalization.CultureInfo.InvariantCulture,
            global::System.Globalization.DateTimeStyles.None,
            out var date
        )
            ? date
            : null;
    }

    private static object? Read(ManagementBaseObject row, string name)
    {
        try
        {
            return row[name];
        }
        catch (ManagementException)
        {
            return null;
        }
    }
}
