using Qcm_app.ViewModels;

namespace Qcm_app.Views;

public partial class TeacherPage : ContentPage
{
	public TeacherPage(TeacherViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
