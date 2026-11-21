using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Qcm_app.ViewModels;

public partial class TeacherLoginViewModel : ObservableObject
{
	// Demo-only gate: this value is part of the client and is not secure.
	private const string DemoPassword = "teacher-demo";

	[ObservableProperty]
	private string password = string.Empty;

	[ObservableProperty]
	private string errorMessage = string.Empty;

	[RelayCommand]
	private async Task LoginAsync()
	{
		if (!string.Equals(Password, DemoPassword, StringComparison.Ordinal))
		{
			ErrorMessage = "Incorrect password.";
			return;
		}

		ErrorMessage = string.Empty;
		Password = string.Empty;
		await Shell.Current.GoToAsync("teacher");
	}

	[RelayCommand]
	private async Task CancelAsync()
	{
		await Shell.Current.GoToAsync("..");
	}
}
