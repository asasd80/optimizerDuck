using optimizerDuck.Domain.Optimizations.Models.Drivers;
using optimizerDuck.Services.UI;

namespace optimizerDuck.Test.Services.UI;

public class DriverServiceTests
{
    [Theory]
    [InlineData(@"PCI\VEN_10DE&DEV_2484&SUBSYS_146B10DE&REV_A1", @"PCI\VEN_10DE&DEV_2484")]
    [InlineData(@"USB\VID_046D&PID_C52B&REV_2411&MI_00", @"USB\VID_046D&PID_C52B")]
    [InlineData(@"HID\VID_046D&PID_C52B&MI_01&Col02", @"HID\VID_046D&PID_C52B")]
    [InlineData(
        @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0257&SUBSYS_17AA2290",
        @"HDAUDIO\FUNC_01&VEN_10EC&DEV_0257"
    )]
    [InlineData(@"ACPI\INT33A1", @"ACPI\INT33A1")]
    [InlineData(@"MONITOR\DELA0F0", @"MONITOR\DELA0F0")]
    public void BuildCatalogQuery_KeepsBusAndTrimsAfterDevice(string hardwareId, string expected)
    {
        Assert.Equal(expected, DriverInfo.BuildCatalogQuery(hardwareId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"BTHENUM\{00007034-0000-1000-8000-00805f9b34fb}_LOCALMFG&0002")]
    [InlineData(@"BTHENUM\{0000110b-0000-1000-8000-00805f9b34fb}_VID&0002054c_PID&0268")]
    [InlineData(@"SWD\MSRRAS\MS_NDISWANIP")]
    [InlineData(@"ROOT\SYSTEM\0001")]
    [InlineData(@"{5d624f94-8850-40c3-a3fa-a4fd2080baf3}\vwifimp_wfd")]
    [InlineData("NoBusSeparator")]
    public void BuildCatalogQuery_VirtualOrUnknownDevice_IsNotSearchable(string? hardwareId)
    {
        Assert.Null(DriverInfo.BuildCatalogQuery(hardwareId));
        Assert.False(
            new DriverInfo { DeviceName = "x", HardwareId = hardwareId ?? "" }.CanSearchCatalog
        );
    }

    [Fact]
    public void CatalogSearchUrl_IsEscaped()
    {
        var driver = new DriverInfo
        {
            DeviceName = "GPU",
            HardwareId = @"PCI\VEN_10DE&DEV_2484&SUBSYS_146B10DE",
        };

        Assert.True(driver.CanSearchCatalog);
        Assert.Equal(
            "https://www.catalog.update.microsoft.com/Search.aspx?q=PCI%5CVEN_10DE%26DEV_2484",
            driver.CatalogSearchUrl
        );
    }

    [Theory]
    [InlineData("20230915000000.******+***", 2023, 9, 15)]
    [InlineData("20060621000000.000000-000", 2006, 6, 21)]
    public void ParseCimDate_ReadsDatePart(string value, int year, int month, int day)
    {
        Assert.Equal(new DateTime(year, month, day), DriverService.ParseCimDate(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2023")]
    [InlineData("notadate.000000+000")]
    public void ParseCimDate_InvalidInput_ReturnsNull(string? value)
    {
        Assert.Null(DriverService.ParseCimDate(value));
    }

    [Fact]
    public void Map_RowWithoutDeviceName_IsSkipped()
    {
        Assert.Null(DriverService.Map(_ => null));
        Assert.Null(DriverService.Map(name => name == "DeviceName" ? "   " : null));
    }

    [Fact]
    public void Map_ReadsAllFields()
    {
        var row = new Dictionary<string, object?>
        {
            ["DeviceName"] = " NVIDIA GeForce RTX 3060 ",
            ["DriverProviderName"] = "NVIDIA",
            ["DriverVersion"] = "32.0.15.6094",
            ["DriverDate"] = "20240814000000.******+***",
            ["DeviceClass"] = "DISPLAY",
            ["InfName"] = "oem42.inf",
            ["HardWareID"] = @"PCI\VEN_10DE&DEV_2504",
            ["DeviceID"] = @"PCI\VEN_10DE&DEV_2504\4&1",
            ["IsSigned"] = false,
        };

        var driver = DriverService.Map(name => row.GetValueOrDefault(name));

        Assert.NotNull(driver);
        Assert.Equal("NVIDIA GeForce RTX 3060", driver.DeviceName);
        Assert.Equal("NVIDIA", driver.Provider);
        Assert.Equal("32.0.15.6094", driver.Version);
        Assert.Equal(new DateTime(2024, 8, 14), driver.Date);
        Assert.Equal("DISPLAY", driver.DeviceClass);
        Assert.Equal("oem42.inf", driver.InfName);
        Assert.False(driver.IsSigned);
        Assert.False(driver.IsMicrosoft);
    }

    [Fact]
    public void Map_MissingIsSigned_TreatedAsSigned()
    {
        var driver = DriverService.Map(name => name == "DeviceName" ? "Device" : null);

        Assert.NotNull(driver);
        Assert.True(driver.IsSigned);
        Assert.False(driver.HasDate);
        Assert.False(driver.HasHardwareId);
    }

    [Theory]
    [InlineData("rtx", true)]
    [InlineData("nvidia", true)]
    [InlineData("32.0.15", true)]
    [InlineData("oem42", true)]
    [InlineData("VEN_10DE", true)]
    [InlineData("display", true)]
    [InlineData("realtek", false)]
    public void Matches_SearchesUserFacingFields(string term, bool expected)
    {
        var driver = new DriverInfo
        {
            DeviceName = "NVIDIA GeForce RTX 3060",
            Provider = "NVIDIA",
            Version = "32.0.15.6094",
            DeviceClass = "DISPLAY",
            InfName = "oem42.inf",
            HardwareId = @"PCI\VEN_10DE&DEV_2504",
        };

        Assert.Equal(expected, driver.Matches(term));
    }

    [Fact]
    public void IsMicrosoft_DetectsInboxProvider()
    {
        Assert.True(new DriverInfo { DeviceName = "x", Provider = "Microsoft" }.IsMicrosoft);
        Assert.False(new DriverInfo { DeviceName = "x", Provider = "Intel" }.IsMicrosoft);
    }
}
