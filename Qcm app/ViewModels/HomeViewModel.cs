using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Qcm_app.ViewModels;

public partial class HomeViewModel : ObservableObject
{
	[RelayCommand]
	private async Task StartQuizAsync()
	{
		// Goes through the Categories screen first (US02), instead of
		// jumping straight into a quiz with no category/difficulty choice.
		await Shell.Current.GoToAsync("categories");
	}

	[RelayCommand]
	private async Task ViewHistoryAsync()
	{
		await Shell.Current.GoToAsync("history");
	}

	[RelayCommand]
	private async Task ViewStatsAsync()
	{
		await Shell.Current.GoToAsync("stats");
	}

	[RelayCommand]
	private async Task OpenTeacherModeAsync()
	{
		await Shell.Current.GoToAsync("teacher-login");
	}
}
