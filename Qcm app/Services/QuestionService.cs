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
	private readonly SemaphoreSlim questionsLock = new(1, 1);
	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNameCaseInsensitive = true,
		WriteIndented = true,
		Converters = { new JsonStringEnumConverter() }
	};

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

	public async Task AddGeneratedQuestionsAsync(
		IReadOnlyCollection<Question> questions,
		CancellationToken cancellationToken = default)
	{
		if (questions.Count == 0)
			return;

		await questionsLock.WaitAsync(cancellationToken);
		try
		{
			cachedQuestions ??= await LoadQuestionsFromStorageAsync();

			var knownIds = cachedQuestions
				.Select(question => question.Id)
				.ToHashSet(StringComparer.Ordinal);
			var additions = questions
				.Where(question => knownIds.Add(question.Id))
				.ToList();

			if (additions.Count == 0)
				return;

			var updatedQuestions = cachedQuestions.Concat(additions).ToList();
			var path = GetWritableQuestionsPath();
			var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";

			try
			{
				string json = JsonSerializer.Serialize(updatedQuestions, JsonOptions);
				await File.WriteAllTextAsync(temporaryPath, json, cancellationToken);
				File.Move(temporaryPath, path, overwrite: true);
				cachedQuestions = updatedQuestions;
			}
			finally
			{
				if (File.Exists(temporaryPath))
					File.Delete(temporaryPath);
			}
		}
		finally
		{
			questionsLock.Release();
		}
	}

	private async Task<List<Question>> LoadAllQuestionsAsync()
	{
		await questionsLock.WaitAsync();
		try
		{
			cachedQuestions ??= await LoadQuestionsFromStorageAsync();
		}
		catch (Exception exception)
		{
			System.Diagnostics.Debug.WriteLine(
				$"Question loading error: {exception}");

			cachedQuestions = new List<Question>();
		}
		finally
		{
			questionsLock.Release();
		}

		return cachedQuestions;
	}

	private static async Task<List<Question>> LoadQuestionsFromStorageAsync()
	{
		Directory.CreateDirectory(FileSystem.AppDataDirectory);
		string path = GetWritableQuestionsPath();

		if (!File.Exists(path))
		{
			await using Stream packagedQuestions =
				await FileSystem.OpenAppPackageFileAsync("questions.json");
			await using var localQuestions = File.Create(path);
			await packagedQuestions.CopyToAsync(localQuestions);
		}

		await using Stream stream = File.OpenRead(path);
		return await JsonSerializer.DeserializeAsync<List<Question>>(stream, JsonOptions)
			?? new List<Question>();
	}

	private static string GetWritableQuestionsPath() =>
		Path.Combine(FileSystem.AppDataDirectory, "questions.json");

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
