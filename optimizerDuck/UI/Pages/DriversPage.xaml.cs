using optimizerDuck.UI.ViewModels.Pages;
using Wpf.Ui.Abstractions.Controls;

namespace optimizerDuck.UI.Pages;

public partial class DriversPage : INavigableView<DriversViewModel>
{
    public DriversPage(DriversViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = this;

        InitializeComponent();
    }

    public DriversViewModel ViewModel { get; }
}
