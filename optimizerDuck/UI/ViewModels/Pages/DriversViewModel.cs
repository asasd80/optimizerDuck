using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using optimizerDuck.Common.Helpers;
using optimizerDuck.Domain.Optimizations.Models.Drivers;
using optimizerDuck.Services.Configuration;
using optimizerDuck.Services.UI;
using Wpf.Ui;
using Wpf.Ui.Controls;

namespace optimizerDuck.UI.ViewModels.Pages;

/// <summary>
///     Lists installed device drivers with search, and links out to Windows Update, Device
///     Manager, and the Microsoft Update Catalog to find newer versions.
/// </summary>
public partial class DriversViewModel : ViewModel
{
    private const string WindowsUpdateDriversUri = "ms-settings:windowsupdate-optionalupdates";

    private readonly List<DriverInfo> _allDrivers = [];
    private readonly DriverService _driverService;
    private readonly ILogger<DriversViewModel> _logger;
    private readonly ISnackbarService _snackbarService;

    private CancellationTokenSource? _searchCts;

    [ObservableProperty]
    private bool _hideMicrosoftDrivers = true;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedSortByIndex; // 0=Name, 1=Provider, 2=Date (oldest first)

    public DriversViewModel(
        DriverService driverService,
        ISnackbarService snackbarService,
        ILogger<DriversViewModel> logger
    )
    {
        _driverService = driverService;
        _snackbarService = snackbarService;
        _logger = logger;
    }

    public ObservableCollection<DriverInfo> Drivers { get; } = [];

    public bool HasData => _allDrivers.Count > 0;

    public int VisibleCount => Drivers.Count;

    partial void OnSearchTextChanged(string value) => ScheduleApplyFilter();

    partial void OnHideMicrosoftDriversChanged(bool value) => ApplyFilter();

    partial void OnSelectedSortByIndexChanged(int value) => ApplyFilter();

    protected override Task InitializeOnceAsync() => LoadAsync();

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    private async Task LoadAsync()
    {
        IsLoading = true;
        _allDrivers.Clear();
        Drivers.Clear();
        UpdateProperties();

        try
        {
            var drivers = await _driverService.GetInstalledDriversAsync();
            _allDrivers.AddRange(drivers);
            ApplyFilter();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load installed drivers");
        }
        finally
        {
            IsLoading = false;
            UpdateProperties();
        }
    }

    private void ScheduleApplyFilter()
    {
        _searchCts?.Cancel();
        _searchCts?.Dispose();
        _searchCts = new CancellationTokenSource();
        var token = _searchCts.Token;

        _ = Task.Run(
            async () =>
            {
                try
                {
                    await Task.Delay(250, token).ConfigureAwait(false);
                    if (!token.IsCancellationRequested)
                        await UiThread.InvokeAsync(ApplyFilter, DispatcherPriority.Background);
                }
                catch (OperationCanceledException) { }
            },
            token
        );
    }

    private void ApplyFilter()
    {
        var query = _allDrivers.AsEnumerable();

        if (HideMicrosoftDrivers)
            query = query.Where(d => !d.IsMicrosoft);

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var term = SearchText.Trim();
            query = query.Where(d => d.Matches(term));
        }

        query = SelectedSortByIndex switch
        {
            1 => query
                .OrderBy(d => d.Provider, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(d => d.DeviceName, StringComparer.CurrentCultureIgnoreCase),
            2 => query.OrderBy(d => d.Date ?? DateTime.MaxValue),
            _ => query.OrderBy(d => d.DeviceName, StringComparer.CurrentCultureIgnoreCase),
        };

        var filtered = query.ToList();
        Drivers.Clear();
        foreach (var driver in filtered)
            Drivers.Add(driver);

        UpdateProperties();
    }

    private void UpdateProperties()
    {
        OnPropertyChanged(nameof(HasData));
        OnPropertyChanged(nameof(VisibleCount));
    }

    [RelayCommand]
    private void OpenWindowsUpdate() => Open(WindowsUpdateDriversUri);

    [RelayCommand]
    private void OpenDeviceManager() => Open("devmgmt.msc");

    [RelayCommand]
    private void SearchOnline(DriverInfo? driver)
    {
        if (driver is null)
            return;

        _logger.LogInformation(
            "Searching Microsoft Update Catalog for {Device} ({HardwareId})",
            driver.DeviceName,
            driver.HardwareId
        );
        Open(driver.CatalogSearchUrl);
    }

    private void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to open {Target}", target);
            _snackbarService.Show(
                Loc.Instance["Snackbar.OpenLinkFailed.Title"],
                Loc.Instance["Snackbar.OpenLinkFailed.Message"],
                ControlAppearance.Danger,
                new SymbolIcon { Symbol = SymbolRegular.ErrorCircle24, Filled = true },
                TimeSpan.FromSeconds(5)
            );
        }
    }
}
