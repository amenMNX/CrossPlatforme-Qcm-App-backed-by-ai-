using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace Qcm_app.ViewModels;

public partial class ResultViewModel : ObservableObject
{
	[ObservableProperty]
	private int score;

	[ObservableProperty]
	private int total;

	[ObservableProperty]
	private string percentageText = string.Empty;

	[ObservableProperty]
	private string message = string.Empty;

	public string ScoreText => $"{Score} / {Total}";

	public void SetResult(int quizScore, int quizTotal)
	{
		Score = quizScore;
		Total = quizTotal;

		int percentage = Total == 0
			? 0
			: (int)Math.Round((double)Score / Total * 100);

		PercentageText = $"{percentage}%";

		Message = percentage >= 80
			? "Excellent work!"
			: percentage >= 50
				? "Good effort!"
				: "Keep practicing!";

		OnPropertyChanged(nameof(ScoreText));
	}

	[RelayCommand]
	private async Task ReplayAsync()
	{
		await Shell.Current.GoToAsync("categories");
	}

	[RelayCommand]
	private async Task GoHomeAsync()
	{
		await Shell.Current.GoToAsync("//MainPage");
	}
}