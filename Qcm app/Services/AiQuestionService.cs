using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qcm_app.Models;
using Qcm_app.Services;

namespace Qcm_app.Services;

/// <summary>
/// Generates quiz questions on the fly using the Gemini API, instead of
/// pulling from the static local JSON bank. Implements the same
/// IQuestionProvider interface as QuestionService, so QuizViewModel can
/// switch between "local bank" and "AI generated" without any other change.
///
/// If the API call fails for any reason (no network, invalid key, quota
/// exceeded, malformed response...) this falls back to the local
/// QuestionService, per the cahier des charges requirement that AI content
/// must never leave the user stuck without a working quiz (US30: "fallback
/// sur le JSON local hors-ligne").
/// </summary>
public class AiQuestionService : IQuestionProvider
{
	// NOTE for students / reviewers:
	// This is a free-tier Gemini API key placeholder. Get your own key at
	// https://aistudio.google.com/apikey and set it via GeminiApiKeyProvider
	// (see Services/GeminiApiKeyProvider.cs) rather than committing it to source control.
	private const string GeminiModel = "gemini-2.0-flash";

	private readonly HttpClient httpClient;
	private readonly QuestionService fallbackService;

	public AiQuestionService(HttpClient httpClient, QuestionService fallbackService)
	{
		this.httpClient = httpClient;
		this.fallbackService = fallbackService;
	}

	public async Task<List<Question>> GetQuestionsAsync(
		string? category,
		Difficulty? difficulty,
		int count,
		CancellationToken cancellationToken = default)
	{
		string apiKey = GeminiApiKeyProvider.ApiKey;

		if (string.IsNullOrWhiteSpace(apiKey))
		{
			System.Diagnostics.Debug.WriteLine(
				"AI generation skipped: no Gemini API key configured. Falling back to local bank.");
			return await fallbackService.GetQuestionsAsync(category, difficulty, count, cancellationToken);
		}

		try
		{
			var generated = await RequestQuestionsFromGeminiAsync(
				apiKey, category, difficulty, count, cancellationToken);

			if (generated is { Count: > 0 })
				return generated;

			System.Diagnostics.Debug.WriteLine(
				"AI generation returned no questions. Falling back to local bank.");
		}
		catch (Exception exception)
		{
			System.Diagnostics.Debug.WriteLine($"AI generation failed: {exception}");
		}

		// Fallback path: always guarantees the user gets a playable quiz,
		// even with no internet connection or an API error.
		return await fallbackService.GetQuestionsAsync(category, difficulty, count, cancellationToken);
	}

	private async Task<List<Question>?> RequestQuestionsFromGeminiAsync(
		string apiKey,
		string? category,
		Difficulty? difficulty,
		int count,
		CancellationToken cancellationToken)
	{
		string topic = string.IsNullOrWhiteSpace(category) || category == QuizSettings.AllCategories
			? "general knowledge"
			: category;

		string difficultyText = difficulty?.ToString() ?? "mixed (easy, medium and hard)";

		string prompt =
			$"Generate exactly {count} multiple choice quiz questions about \"{topic}\". " +
			$"Difficulty: {difficultyText}. " +
			"Each question must have exactly 4 distinct answer choices, with exactly one correct answer. " +
			"Keep questions factual, unambiguous, and appropriate for a general audience. " +
			"Provide a short one-sentence explanation for each correct answer.";

		var requestBody = new
		{
			contents = new[]
			{
				new
				{
					role = "user",
					parts = new[] { new { text = prompt } }
				}
			},
			generationConfig = new
			{
				responseMimeType = "application/json",
				responseSchema = BuildResponseSchema()
			}
		};

		string requestJson = JsonSerializer.Serialize(requestBody);

		string url =
			$"https://generativelanguage.googleapis.com/v1beta/models/{GeminiModel}:generateContent?key={apiKey}";

		using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
		using var response = await httpClient.PostAsync(url, content, cancellationToken);

		response.EnsureSuccessStatusCode();

		string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

		return ParseGeneratedQuestions(responseJson, topic, difficulty);
	}

	/// <summary>
	/// Builds a Gemini structured-output schema so the model is constrained
	/// to return exactly the shape we need, rather than free-form text we'd
	/// have to parse hopefully.
	/// </summary>
	private static object BuildResponseSchema()
	{
		return new
		{
			type = "ARRAY",
			items = new
			{
				type = "OBJECT",
				properties = new
				{
					text = new { type = "STRING" },
					choices = new
					{
						type = "ARRAY",
						items = new { type = "STRING" }
					},
					correctIndex = new { type = "INTEGER" },
					explanation = new { type = "STRING" }
				},
				required = new[] { "text", "choices", "correctIndex", "explanation" }
			}
		};
	}

	private static List<Question>? ParseGeneratedQuestions(
		string geminiResponseJson, string topic, Difficulty? difficulty)
	{
		using var document = JsonDocument.Parse(geminiResponseJson);

		string? innerJson = document.RootElement
			.GetProperty("candidates")[0]
			.GetProperty("content")
			.GetProperty("parts")[0]
			.GetProperty("text")
			.GetString();

		if (string.IsNullOrWhiteSpace(innerJson))
			return null;

		var rawQuestions = JsonSerializer.Deserialize<List<GeneratedQuestionDto>>(
			innerJson,
			new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

		if (rawQuestions is null)
			return null;

		var questions = new List<Question>();
		int index = 0;

		foreach (var raw in rawQuestions)
		{
			if (raw.Choices is null || raw.Choices.Count < 2)
				continue;

			if (raw.CorrectIndex < 0 || raw.CorrectIndex >= raw.Choices.Count)
				continue;

			questions.Add(new Question
			{
				Id = $"ai-{Guid.NewGuid():N}",
				Text = raw.Text ?? string.Empty,
				Choices = raw.Choices,
				CorrectIndex = raw.CorrectIndex,
				Category = topic,
				Difficulty = difficulty ?? Models.Difficulty.Medium,
				Explanation = raw.Explanation
			});

			index++;
		}

		return questions;
	}

	private class GeneratedQuestionDto
	{
		[JsonPropertyName("text")]
		public string? Text { get; set; }

		[JsonPropertyName("choices")]
		public List<string>? Choices { get; set; }

		[JsonPropertyName("correctIndex")]
		public int CorrectIndex { get; set; }

		[JsonPropertyName("explanation")]
		public string? Explanation { get; set; }
	}
}
