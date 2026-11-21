using Qcm_app.ViewModels;

namespace Qcm_app.Views;

public partial class TeacherLoginPage : ContentPage
{
	public TeacherLoginPage(TeacherLoginViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}
