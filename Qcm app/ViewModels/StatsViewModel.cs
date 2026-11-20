using CommunityToolkit.Mvvm.ComponentModel;
using Qcm_app.Data;

namespace Qcm_app.ViewModels;

/// <summary>
/// Shows aggregate stats computed from SQLite history (US18).
/// </summary>
public partial class StatsViewModel : ObservableObject
{
	private readonly QuizDatabase database;

	[ObservableProperty]
	private int gamesPlayed;

	[ObservableProperty]
	private string bestScoreText = "-";

	[ObservableProperty]
	private string averageScoreText = "-";

	[ObservableProperty]
	private bool hasNoData;

	public StatsViewModel(QuizDatabase database)
	{
		this.database = database;
	}

	public async Task InitializeAsync()
	{
		var stats = await database.GetStatsAsync();

		GamesPlayed = stats.GamesPlayed;
		HasNoData = stats.GamesPlayed == 0;

		BestScoreText = stats.GamesPlayed == 0
			? "-"
			: $"{stats.BestScorePercentage:0}%";

		AverageScoreText = stats.GamesPlayed == 0
			? "-"
			: $"{stats.AverageScorePercentage:0}%";
	}
}
