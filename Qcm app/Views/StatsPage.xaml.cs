using Qcm_app.ViewModels;

namespace Qcm_app.Views;

public partial class StatsPage : ContentPage
{
	private readonly StatsViewModel viewModel;

	public StatsPage(StatsViewModel viewModel)
	{
		InitializeComponent();
		this.viewModel = viewModel;
		BindingContext = viewModel;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await viewModel.InitializeAsync();
	}
}
