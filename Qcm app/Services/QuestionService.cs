using System.Text.Json;
using System.Text.Json.Serialization;
using Qcm_app.Data;
using Qcm_app.Models;

namespace Qcm_app.Services;

/// <summary>
/// Loads questions from the embedded local JSON bank (Resources/Raw/questions.json).
/// This is the offline, always-available question source and the fallback
/// used whenever AI generation (see AiQuestionService) is unavailable.
/// </summary>
public class QuestionService : IQuestionProvider
{
	private readonly QuizDatabase database;
	private readonly SemaphoreSlim questionsLock = new(1, 1);
	private List<Question>? embeddedQuestions;
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() }
	};

	public QuestionService(QuizDatabase database)
	{
		this.database = database;
	}

	public async Task<List<Question>> GetQuestionsAsync(
		string? category,
		Difficulty? difficulty,
		int count,
		CancellationToken cancellationToken = default)
	{
		var allQuestions = await LoadAllQuestionsAsync();

		IEnumerable<Question> filtered = allQuestions;

		if (!string.IsNullOrWhiteSpace(category) &&
			category != QuizSettings.AllCategories)
		{
			filtered = filtered.Where(q =>
				string.Equals(q.Category, category, StringComparison.OrdinalIgnoreCase));
		}

		if (difficulty is not null)
		{
			filtered = filtered.Where(q => q.Difficulty == difficulty);
		}

		var shuffled = filtered
			.OrderBy(_ => Random.Shared.Next())
			.Take(count)
			.ToList();

		// Also shuffle each question's answer choices so the correct index
		// doesn't always land in the same visual position (US08).
		foreach (var question in shuffled)
		{
			ShuffleChoicesInPlace(question);
		}

		return shuffled;
	}

	public async Task<List<string>> GetCategoriesAsync()
	{
		var allQuestions = await LoadAllQuestionsAsync();

		return allQuestions
			.Select(q => q.Category)
			.Distinct()
			.OrderBy(c => c)
			.ToList();
	}

	private async Task<List<Question>> LoadAllQuestionsAsync()
	{
		List<Question> embedded;

		await questionsLock.WaitAsync();
		try
		{
			embeddedQuestions ??= await LoadEmbeddedQuestionsAsync();
			embedded = embeddedQuestions.Select(CloneQuestion).ToList();
		}
		finally
		{
			questionsLock.Release();
		}

		var teacherQuestions = await database.GetTeacherQuestionsAsync();
		var generatedQuestions = await database.GetGeneratedQuestionsAsync();

		return embedded
			.Concat(teacherQuestions.Select(ToQuestion))
			.Concat(generatedQuestions.Select(ToQuestion))
			.ToList();
	}

	private static async Task<List<Question>> LoadEmbeddedQuestionsAsync()
	{
		await using Stream stream =
			await FileSystem.OpenAppPackageFileAsync("questions.json");
		return await JsonSerializer.DeserializeAsync<List<Question>>(stream, JsonOptions)
			?? new List<Question>();
	}

	private static Question ToQuestion(TeacherQuestion stored) => new()
	{
		Id = $"teacher-{stored.Id}",
		Text = stored.Text,
		Choices = new List<string>
		{
			stored.Choice1, stored.Choice2, stored.Choice3, stored.Choice4
		},
		CorrectIndex = stored.CorrectIndex,
		Category = stored.Category,
		Difficulty = ParseDifficulty(stored.Difficulty)
	};

	private static Question ToQuestion(GeneratedQuestion stored) => new()
	{
		Id = stored.Id,
		Text = stored.Text,
		Choices = new List<string>
		{
			stored.Choice1, stored.Choice2, stored.Choice3, stored.Choice4
		},
		CorrectIndex = stored.CorrectIndex,
		Category = stored.Category,
		Difficulty = ParseDifficulty(stored.Difficulty),
		Explanation = stored.Explanation
	};

	private static Difficulty ParseDifficulty(string value) =>
		Enum.TryParse<Difficulty>(value, ignoreCase: true, out var difficulty)
			? difficulty
			: Difficulty.Medium;

	private static Question CloneQuestion(Question question) => new()
	{
		Id = question.Id,
		Text = question.Text,
		Choices = question.Choices.ToList(),
		CorrectIndex = question.CorrectIndex,
		Category = question.Category,
		Difficulty = question.Difficulty,
		Explanation = question.Explanation
	};

	private static void ShuffleChoicesInPlace(Question question)
	{
		string correctAnswer = question.Choices[question.CorrectIndex];

		var shuffledChoices = question.Choices
			.OrderBy(_ => Random.Shared.Next())
			.ToList();

		question.Choices = shuffledChoices;
		question.CorrectIndex = shuffledChoices.IndexOf(correctAnswer);
	}
}
