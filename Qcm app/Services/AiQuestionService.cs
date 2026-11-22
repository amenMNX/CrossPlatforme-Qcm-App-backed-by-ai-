using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
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
/// If the API call fails, the service returns no questions and exposes a
/// user-facing issue so the caller can offer a retry.
/// </summary>
public class AiQuestionService : IQuestionProvider
{
	// Configure the key through the GEMINI_API_KEY environment variable.
	// Never commit API keys to source control.
	private const string DefaultGeminiModel = "gemma-4-26b-a4b-it";
	private static readonly TimeSpan GeminiRequestTimeout = TimeSpan.FromSeconds(45);
	private static readonly TimeSpan GeminiAttemptTimeout = TimeSpan.FromSeconds(18);

	private readonly HttpClient httpClient;
	private readonly QuizDatabase database;
	private readonly ILogger<AiQuestionService> logger;

	public string? LastGenerationIssue { get; private set; }

	public AiQuestionService(
		HttpClient httpClient,
		QuizDatabase database,
		ILogger<AiQuestionService> logger)
	{
		this.httpClient = httpClient;
		this.database = database;
		this.logger = logger;
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
			return new List<Question>();
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

			LastGenerationIssue = "Gemini returned no usable questions.";
		}
		catch (HttpRequestException exception)
		{
			LastGenerationIssue = exception.Message;
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
		catch (OperationCanceledException)
		{
			LastGenerationIssue = "Gemini request timed out. Please try again.";
		}
		catch (Exception exception)
		{
			LastGenerationIssue = $"Gemini response could not be processed ({exception.GetType().Name}).";
			logger.LogError(
				"Gemini request processing failed for model {Model} after {DurationMs} ms ({ExceptionType}).",
				GetModelName(),
				0,
				exception.GetType().Name);
		}

		return new List<Question>();
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
				responseMimeType = "application/json",
				thinkingConfig = new { thinkingLevel = "MINIMAL" }
			}
		};

		string requestJson = JsonSerializer.Serialize(requestBody);

		string model = GetModelName();

		string url =
			$"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent";

		var timer = Stopwatch.StartNew();
		using var totalTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		totalTimeoutCts.CancelAfter(GeminiRequestTimeout);
		for (int attempt = 1; attempt <= 3; attempt++)
		{
			using var attemptTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(totalTimeoutCts.Token);
			attemptTimeoutCts.CancelAfter(GeminiAttemptTimeout);

			try
			{
				using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
				using var request = new HttpRequestMessage(HttpMethod.Post, url)
				{
					Content = content
				};
				request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

				using var response = await httpClient.SendAsync(
					request,
					HttpCompletionOption.ResponseHeadersRead,
					attemptTimeoutCts.Token);

				if (response.IsSuccessStatusCode)
				{
					string responseJson = await response.Content.ReadAsStringAsync(attemptTimeoutCts.Token);
					var questions = ParseGeneratedQuestions(responseJson, topic, difficulty);
					logger.LogInformation(
						"Gemini request succeeded for model {Model} with HTTP {StatusCode} in {DurationMs} ms (attempt {Attempt}).",
						model,
					(int)response.StatusCode,
					(int)timer.ElapsedMilliseconds,
					attempt);
					return questions;
				}

				string safeError = await ReadSafeErrorMessageAsync(response, apiKey, attemptTimeoutCts.Token);
				int statusCode = (int)response.StatusCode;
				logger.LogWarning(
					"Gemini request failed for model {Model} with HTTP {StatusCode} in {DurationMs} ms (attempt {Attempt}). {ErrorMessage}",
					model,
					statusCode,
					(int)timer.ElapsedMilliseconds,
					attempt,
					safeError);

				if (attempt == 3 || !IsTransientStatusCode(response.StatusCode))
					throw new HttpRequestException(
						$"Gemini returned HTTP {statusCode}: {safeError}",
						null,
						response.StatusCode);

				await Task.Delay(GetRetryDelay(response, attempt), totalTimeoutCts.Token);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (OperationCanceledException) when (totalTimeoutCts.IsCancellationRequested)
			{
				throw new OperationCanceledException("Gemini request timed out.");
			}
			catch (OperationCanceledException) when (attempt < 3)
			{
				logger.LogWarning(
					"Gemini request timed out for model {Model} in {DurationMs} ms (attempt {Attempt}); retrying.",
					model,
					(int)timer.ElapsedMilliseconds,
					attempt);
				await Task.Delay(TimeSpan.FromSeconds(attempt), totalTimeoutCts.Token);
			}
			catch (HttpRequestException exception) when (attempt < 3 && IsTransientStatusCode(exception.StatusCode))
			{
				int? statusCode = exception.StatusCode is null ? null : (int)exception.StatusCode.Value;
				logger.LogWarning(
					"Gemini transport failure for model {Model} with HTTP status {StatusCode} in {DurationMs} ms (attempt {Attempt}); retrying.",
					model,
					statusCode,
					(int)timer.ElapsedMilliseconds,
					attempt);
				await Task.Delay(TimeSpan.FromSeconds(attempt), totalTimeoutCts.Token);
			}
			catch (HttpRequestException exception) when (exception.StatusCode is null)
			{
				logger.LogWarning(
					"Gemini transport failure for model {Model} in {DurationMs} ms (attempt {Attempt}).",
					model,
					(int)timer.ElapsedMilliseconds,
					attempt);
				throw new HttpRequestException("Gemini network request failed. Please try again.");
			}
		}

		throw new HttpRequestException("Gemini remained unavailable after retries.", null, HttpStatusCode.ServiceUnavailable);
	}

	private static string GetModelName()
	{
		string? model = Environment.GetEnvironmentVariable("GEMINI_MODEL")?.Trim();
		return string.IsNullOrWhiteSpace(model) ? DefaultGeminiModel : model;
	}

	private static bool IsTransientStatusCode(HttpStatusCode? statusCode) =>
		statusCode is null || statusCode is HttpStatusCode.RequestTimeout or
			(HttpStatusCode)429 or HttpStatusCode.InternalServerError or
			HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or
			HttpStatusCode.GatewayTimeout;

	private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
	{
		TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
		if (retryAfter is null && response.Headers.RetryAfter?.Date is DateTimeOffset retryDate)
			retryAfter = retryDate - DateTimeOffset.UtcNow;

		return retryAfter is { } delay && delay > TimeSpan.Zero
			? TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds, 5))
			: TimeSpan.FromSeconds(attempt);
	}

	private static async Task<string> ReadSafeErrorMessageAsync(
		HttpResponseMessage response,
		string apiKey,
		CancellationToken cancellationToken)
	{
		string message = "No error details were provided by Gemini.";
		try
		{
			string body = await response.Content.ReadAsStringAsync(cancellationToken);
			using var document = JsonDocument.Parse(body);
			if (document.RootElement.TryGetProperty("error", out var error) &&
				error.TryGetProperty("message", out var errorMessage) &&
				errorMessage.ValueKind == JsonValueKind.String)
			{
				message = errorMessage.GetString() ?? message;
			}
		}
		catch (JsonException)
		{
		}

		message = message.Replace(apiKey, "[redacted]", StringComparison.Ordinal);
		message = string.Join(" ", message.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
		return message.Length <= 240 ? message : message[..240];
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
