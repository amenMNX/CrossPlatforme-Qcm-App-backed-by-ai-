using Qcm_app.ViewModels;

namespace Qcm_app.Views;

public partial class HistoryPage : ContentPage
{
	private readonly HistoryViewModel viewModel;

	public HistoryPage(HistoryViewModel viewModel)
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
