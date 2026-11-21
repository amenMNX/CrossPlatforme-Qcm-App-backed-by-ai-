using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qcm_app.Data;
using Qcm_app.Models;
using Qcm_app.Services;
using SQLitePCL;

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
	// Configure the key through the GEMINI_API_KEY environment variable.
	// Never commit API keys to source control.
	private const string DefaultGeminiModel = "gemma-4-26b-a4b-it";
	private static readonly TimeSpan GeminiRequestTimeout = TimeSpan.FromSeconds(30);

	private readonly HttpClient httpClient;
	private readonly QuestionService fallbackService;
	private readonly QuizDatabase database;

	public string? LastGenerationIssue { get; private set; }

	public AiQuestionService(HttpClient httpClient, QuestionService fallbackService, QuizDatabase database)
	{
		this.httpClient = httpClient;
		this.fallbackService = fallbackService;
		this.database = database;
	}
	private static GeneratedQuestion ToStoredQuestion(Question question)
	{
		return new GeneratedQuestion
		{
			Id = question.Id,
			Text = question.Text,
			Choice1 = question.Choices[0],
			Choice2 = question.Choices[1],
			Choice3 = question.Choices[2],
			Choice4 = question.Choices[3],
			CorrectIndex = question.CorrectIndex,
			Category = question.Category,
			Difficulty = question.Difficulty.ToString(),
			Explanation = question.Explanation
		};
	}
	public async Task<List<Question>> GenerateQuestionsForTeacherAsync(
	string topic,
	int count,
	Difficulty? difficulty = null,
	CancellationToken cancellationToken = default)
	{
		LastGenerationIssue = null;

		if (string.IsNullOrWhiteSpace(topic))
		{
			LastGenerationIssue = "Enter a topic.";
			return new List<Question>();
		}

		if (count is < 1 or > 20)
		{
			LastGenerationIssue = "Choose between 1 and 20 questions.";
			return new List<Question>();
		}

		string apiKey = GeminiApiKeyProvider.ApiKey;
		if (string.IsNullOrWhiteSpace(apiKey))
		{
			LastGenerationIssue = "GEMINI_API_KEY is not available to the app process.";
			return new List<Question>();
		}

		try
		{
			var generated = await RequestQuestionsFromGeminiAsync(
				apiKey,
				topic.Trim(),
				difficulty,
				count,
				cancellationToken);

			if (generated is { Count: > 0 })
				return generated;

			LastGenerationIssue = "Gemini returned no usable questions.";
		}
		catch (HttpRequestException exception)
		{
			string status = exception.StatusCode is null
				? "No HTTP status (network or transport error)."
				: $"HTTP {(int)exception.StatusCode.Value} ({exception.StatusCode.Value}).";

			LastGenerationIssue = $"Gemini request failed: {status}";
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
		{
			LastGenerationIssue = "Gemini request timed out. Please try again.";
		}
		catch (Exception exception)
		{
			LastGenerationIssue =
				$"Gemini response could not be processed ({exception.GetType().Name}).";
		}

		return new List<Question>();
	}
	public async Task<List<Question>> GetQuestionsAsync(
		string? category,
		Difficulty? difficulty,
		int count,
		CancellationToken cancellationToken = default)
	{
		LastGenerationIssue = null;
		string apiKey = GeminiApiKeyProvider.ApiKey;

		if (string.IsNullOrWhiteSpace(apiKey))
		{
			LastGenerationIssue = "GEMINI_API_KEY is not available to the app process.";
			System.Diagnostics.Debug.WriteLine(
				$"AI generation skipped: {LastGenerationIssue} Falling back to local bank.");
			return await fallbackService.GetQuestionsAsync(category, difficulty, count, cancellationToken);
		}

		try
		{
			var generated = await RequestQuestionsFromGeminiAsync(
				apiKey, category, difficulty, count, cancellationToken);

			if (generated is { Count: > 0 })
			{
				await database.SaveGeneratedQuestionsAsync(
															generated.Select(ToStoredQuestion).ToList());
				return generated;
			}

			System.Diagnostics.Debug.WriteLine(
				"AI generation returned no questions. Falling back to local bank.");
			LastGenerationIssue = "Gemini returned no usable questions.";
		}
		catch (HttpRequestException exception)
		{
			string status = exception.StatusCode is null
				? "No HTTP status (network or transport error)."
				: $"HTTP {(int)exception.StatusCode.Value} ({exception.StatusCode.Value}).";
			LastGenerationIssue = $"Gemini request failed: {status}";
			// Do not log the exception message: the request URL contains the API key.
			System.Diagnostics.Debug.WriteLine(LastGenerationIssue);
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (OperationCanceledException)
		{
			LastGenerationIssue = "Gemini request timed out. Local questions will be used.";
			System.Diagnostics.Debug.WriteLine(LastGenerationIssue);
		}
		catch (Exception exception)
		{
			LastGenerationIssue = $"Gemini response could not be processed ({exception.GetType().Name}).";
			// Avoid exception details because they may include request data or the key-bearing URL.
			System.Diagnostics.Debug.WriteLine(LastGenerationIssue);
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
			"Provide a short one-sentence explanation for each correct answer. " +
			"Return only a JSON array, with no Markdown fences, using exactly these fields: " +
			"text, choices, correctIndex, and explanation.";

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
				responseMimeType = "application/json"
			}
		};

		string requestJson = JsonSerializer.Serialize(requestBody);

		string model = Environment.GetEnvironmentVariable("GEMINI_MODEL")?.Trim();
		if (string.IsNullOrWhiteSpace(model))
			model = DefaultGeminiModel;

		string url =
			$"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

		using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		timeoutCts.CancelAfter(GeminiRequestTimeout);
		for (int attempt = 0; attempt < 3; attempt++)
		{
			using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
			using var request = new HttpRequestMessage(HttpMethod.Post, url)
			{
				Content = content
			};
			// Keep the key out of the URL: URLs can be captured by debugger/network logs.
			request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

			using var response = await httpClient.SendAsync(
				request,
				HttpCompletionOption.ResponseHeadersRead,
				timeoutCts.Token);

			if (response.StatusCode == HttpStatusCode.ServiceUnavailable && attempt < 2)
			{
				// 503 can be transient. Retry briefly without
				// exposing the API key or waiting through another full HttpClient timeout.
				await Task.Delay(TimeSpan.FromSeconds(attempt == 0 ? 2 : 5), timeoutCts.Token);
				continue;
			}

			response.EnsureSuccessStatusCode();
			string responseJson = await response.Content.ReadAsStringAsync(timeoutCts.Token);
			return ParseGeneratedQuestions(responseJson, topic, difficulty);
		}

		throw new HttpRequestException(
			"Gemini remained unavailable after retries.",
			null,
			HttpStatusCode.ServiceUnavailable);
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

		// When responseMimeType = "application/json" is set, Gemini returns the
		// structured data directly as a JSON value inside parts[0].text — it is
		// already a JSON string, not a JSON-encoded string. We read it as a raw
		// string and deserialize it directly. If the value is instead a JSON
		// array element (some SDK versions embed it differently), fall back to
		// re-parsing the text string.
		var partsElement = document.RootElement
			.GetProperty("candidates")[0]
			.GetProperty("content")
			.GetProperty("parts")[0];

		List<GeneratedQuestionDto>? rawQuestions = null;

		// Try treating parts[0].text as a JSON string to re-parse (old behaviour
		// for plain-text responses or non-structured output mode).
		if (partsElement.TryGetProperty("text", out var textElement))
		{
			string? textValue = textElement.GetString();
			if (!string.IsNullOrWhiteSpace(textValue))
			{
				// textValue may be a JSON array string (structured output) or a
				// markdown-fenced block from a plain-text response.
				string trimmed = textValue.Trim();
				// Strip optional ```json ... ``` fences emitted by some model versions.
				if (trimmed.StartsWith("```"))
				{
					int firstNewline = trimmed.IndexOf('\n');
					int lastFence = trimmed.LastIndexOf("```");
					if (firstNewline >= 0 && lastFence > firstNewline)
						trimmed = trimmed[(firstNewline + 1)..lastFence].Trim();
				}
				else if (trimmed.EndsWith("```", StringComparison.Ordinal))
				{
					trimmed = trimmed[..^3].Trim();
				}

				if (trimmed.StartsWith("["))
				{
					rawQuestions = JsonSerializer.Deserialize<List<GeneratedQuestionDto>>(
						trimmed,
						new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
				}
			}
		}

		if (rawQuestions is null || rawQuestions.Count == 0)
			return null;

		var questions = new List<Question>();

		foreach (var raw in rawQuestions)
		{
			if (raw.Choices is null || raw.Choices.Count != 4)
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
