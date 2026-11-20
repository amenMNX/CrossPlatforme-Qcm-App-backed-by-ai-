using System;
using System.Collections.Generic;
using System.Text;
using SQLite;
using Qcm_app.Data;

namespace Qcm_app.Data;
public class QuizDatabase
{
	private readonly SQLiteAsyncConnection connection;
	private readonly Task initializationTask;

	public QuizDatabase()
	{
		string dbPath = GetDatabasePath();
		connection = new SQLiteAsyncConnection(dbPath);
		initializationTask = connection.CreateTableAsync<GameSession>();
	}

	private static string GetDatabasePath()
	{
#if WINDOWS && DEBUG
		var directory = new DirectoryInfo(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "Qcm app.csproj")))
				return Path.Combine(directory.FullName, "quiz_history.db3");

			directory = directory.Parent;
		}
#endif

		return Path.Combine(FileSystem.AppDataDirectory, "quiz_history.db3");
	}

	public async Task SaveSessionAsync(GameSession session)
	{
		await initializationTask;
		await connection.InsertAsync(session);
	}

	public async Task<List<GameSession>> GetHistoryAsync()
	{
		await initializationTask;
		return await connection.Table<GameSession>()
			.OrderByDescending(s => s.Date)
			.ToListAsync();
	}

	public async Task<QuizStats> GetStatsAsync()
	{
		await initializationTask;
		var sessions = await connection.Table<GameSession>().ToListAsync();

		if (sessions.Count == 0)
			return new QuizStats();

		return new QuizStats
		{
			GamesPlayed = sessions.Count,
			BestScorePercentage = sessions.Max(s =>
				s.TotalQuestions == 0 ? 0 : (double)s.Score / s.TotalQuestions * 100),
			AverageScorePercentage = sessions.Average(s =>
				s.TotalQuestions == 0 ? 0 : (double)s.Score / s.TotalQuestions * 100)
		};
	}
}

public class QuizStats
{
	public int GamesPlayed { get; set; }
	public double BestScorePercentage { get; set; }
	public double AverageScorePercentage { get; set; }
}
