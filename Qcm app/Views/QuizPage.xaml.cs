using Qcm_app.ViewModels;

namespace Qcm_app.Views;

public partial class QuizPage : ContentPage
{
	private readonly QuizViewModel viewModel;

	public QuizPage(QuizViewModel viewModel)
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
