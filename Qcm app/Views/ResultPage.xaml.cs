using System.Collections;
using Qcm_app.ViewModels;

namespace Qcm_app.Views;

public partial class ResultPage : ContentPage, IQueryAttributable
{
	private readonly ResultViewModel viewModel;

	public ResultPage(ResultViewModel viewModel)
	{
		InitializeComponent();
		this.viewModel = viewModel;
		BindingContext = viewModel;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		int score = int.Parse(query["score"].ToString()!);
		int total = int.Parse(query["total"].ToString()!);

		viewModel.SetResult(score, total);
	}
}
