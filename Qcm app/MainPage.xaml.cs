using Qcm_app.ViewModels;

namespace Qcm_app;

public partial class MainPage : ContentPage
{
	public MainPage(HomeViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
