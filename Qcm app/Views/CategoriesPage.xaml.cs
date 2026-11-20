using Qcm_app.ViewModels;

namespace Qcm_app.Views;

public partial class CategoriesPage : ContentPage
{
	private readonly CategoriesViewModel viewModel;

	public CategoriesPage(CategoriesViewModel viewModel)
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
