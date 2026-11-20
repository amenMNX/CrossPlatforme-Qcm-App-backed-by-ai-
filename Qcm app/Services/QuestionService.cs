using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Qcm_app.Models;
using System.Text.Json.Serialization;

namespace Qcm_app.Services;

/// <summary>
/// Loads questions from the embedded local JSON bank (Resources/Raw/questions.json).
/// This is the offline, always-available question source and the fallback
/// used whenever AI generation (see AiQuestionService) is unavailable.
/// </summary>
public class QuestionService : IQuestionProvider
{
	private List<Question>? cachedQuestions;

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
		if (cachedQuestions is not null)
			return cachedQuestions;

		try
		{
			using Stream stream =
				await FileSystem.OpenAppPackageFileAsync("questions.json");

			var questions =
				await JsonSerializer.DeserializeAsync<List<Question>>(
					stream,
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true,
						Converters =
						{
							new JsonStringEnumConverter()
						}
					});

			cachedQuestions = questions ?? new List<Question>();
		}
		catch (Exception exception)
		{
			System.Diagnostics.Debug.WriteLine(
				$"Question loading error: {exception}");

			cachedQuestions = new List<Question>();
		}

		return cachedQuestions;
	}

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
