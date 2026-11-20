using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Qcm_app.Data;

namespace Qcm_app.ViewModels;

/// <summary>
/// Shows past quiz sessions stored in SQLite (US17).
/// </summary>
public partial class HistoryViewModel : ObservableObject
{
	private readonly QuizDatabase database;

	public ObservableCollection<GameSession> Sessions { get; } = new();

	[ObservableProperty]
	private bool isEmpty;

	public HistoryViewModel(QuizDatabase database)
	{
		this.database = database;
	}

	public async Task InitializeAsync()
	{
		Sessions.Clear();

		var history = await database.GetHistoryAsync();

		foreach (var session in history)
			Sessions.Add(session);

		IsEmpty = Sessions.Count == 0;
	}
}
