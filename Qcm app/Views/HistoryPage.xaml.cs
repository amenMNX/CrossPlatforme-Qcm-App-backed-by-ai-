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
		try
		{
			await viewModel.InitializeAsync();
		}
		catch (Exception exception)
		{
			System.Diagnostics.Debug.WriteLine(exception);
			await DisplayAlert("Loading error", "Could not load quiz history.", "OK");
		}
	}
}
