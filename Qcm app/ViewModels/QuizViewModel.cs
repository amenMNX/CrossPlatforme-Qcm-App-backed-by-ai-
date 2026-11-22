using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qcm_app.Data;
using Qcm_app.Models;
using Qcm_app.Services;

namespace Qcm_app.ViewModels;

public partial class QuizViewModel : ObservableObject, IQueryAttributable
{
	private readonly QuestionService localQuestionService;
	private readonly AiQuestionService aiQuestionService;
	private readonly QuizDatabase database;

	private List<Question> questions = new();
	private int currentIndex;
	private bool isInitialized;
	private DateTime quizStartedAt;

	private string category = QuizSettings.AllCategories;
	private Difficulty? difficulty;
	private bool useAiGeneration;

	[ObservableProperty]
	private Question? currentQuestion;

	[ObservableProperty]
	private string feedbackMessage = string.Empty;

	[ObservableProperty]
	private string progressText = string.Empty;

	[ObservableProperty]
	private double progress;

	[ObservableProperty]
	private int score;

	[ObservableProperty]
	private bool isAnswered;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private bool hasGenerationError;

	[ObservableProperty]
	private bool isQuizReady;

	[ObservableProperty]
	private string loadingMessage = "Loading questions...";

	[ObservableProperty]
	private string scoreText = "Score: 0";

	[ObservableProperty]
	private string topicText = "General knowledge";

	[ObservableProperty]
	private string difficultyText = "Mixed difficulty";

	[ObservableProperty]
	private string feedbackTitle = string.Empty;

	[ObservableProperty]
	private string feedbackColor = "#2E9B62";

	public QuizViewModel(
		QuestionService localQuestionService,
		AiQuestionService aiQuestionService,
		QuizDatabase database)
	{
		this.localQuestionService = localQuestionService;
		this.aiQuestionService = aiQuestionService;
		this.database = database;
	}

	public void ApplyQueryAttributes(IDictionary<string, object> query)
	{
		if (query.TryGetValue("category", out var categoryValue))
			category = categoryValue?.ToString() ?? QuizSettings.AllCategories;

		if (query.TryGetValue("difficulty", out var difficultyValue) &&
			Enum.TryParse<Difficulty>(difficultyValue?.ToString(), out var parsedDifficulty))
		{
			difficulty = parsedDifficulty;
		}
		else
		{
			difficulty = null;
		}

		if (query.TryGetValue("useAi", out var useAiValue))
			useAiGeneration = useAiValue is bool b && b;
	}

	public async Task InitializeAsync()
	{
		if (isInitialized)
			return;

		isInitialized = true;
		await LoadQuestionsAsync();
	}

	[RelayCommand]
	private async Task RetryAsync()
	{
		await LoadQuestionsAsync();
	}

	private async Task LoadQuestionsAsync()
	{
		IsLoading = true;
		HasGenerationError = false;
		IsQuizReady = false;
		FeedbackMessage = string.Empty;
		LoadingMessage = useAiGeneration
			? "Generating questions with AI..."
			: "Loading questions...";

		IQuestionProvider provider = useAiGeneration ? aiQuestionService : localQuestionService;

		try
		{
			// Smaller AI batches are much more likely to complete before the
			// request deadline. Local quizzes keep their original 10 questions.
			int questionCount = useAiGeneration ? 4 : 10;
			questions = await provider.GetQuestionsAsync(category, difficulty, questionCount);
		}
		catch (OperationCanceledException)
		{
			FeedbackMessage = "Question generation was canceled.";
			ShowGenerationError();
			return;
		}
		catch (Exception)
		{
			FeedbackMessage = useAiGeneration
			? "AI generation failed. Please try again."
				: "Could not load questions. Please try again.";
			ShowGenerationError();
			return;
		}

		IsLoading = false;

		if (questions.Count == 0)
		{
			FeedbackMessage = useAiGeneration
				? aiQuestionService.LastGenerationIssue ?? "Could not generate questions. Please try again."
				: "No questions available. Please try a different category.";
			HasGenerationError = true;
			return;
		}

		currentIndex = 0;
		Score = 0;
		TopicText = category == QuizSettings.AllCategories ? "General knowledge" : category;
		DifficultyText = difficulty?.ToString() ?? "Mixed difficulty";
		quizStartedAt = DateTime.UtcNow;
		DisplayCurrentQuestion();
		IsQuizReady = true;
	}

	private void ShowGenerationError()
	{
		IsLoading = false;
		HasGenerationError = true;
	}

	private void DisplayCurrentQuestion()
	{
		CurrentQuestion = questions[currentIndex];
		ProgressText = $"Question {currentIndex + 1} / {questions.Count}";
		Progress = (double)(currentIndex + 1) / questions.Count;
		ScoreText = $"Score: {Score}";
		FeedbackMessage = string.Empty;
		FeedbackTitle = string.Empty;
		IsAnswered = false;
	}

	[RelayCommand]
	private async Task SelectAnswerAsync(string selectedAnswer)
	{
		if (CurrentQuestion is null || IsAnswered)
			return;

		IsAnswered = true;

		int selectedIndex = CurrentQuestion.Choices.IndexOf(selectedAnswer);

		if (selectedIndex == CurrentQuestion.CorrectIndex)
		{
			Score++;
			FeedbackTitle = "Correct!";
			FeedbackColor = "#2E9B62";
			FeedbackMessage = CurrentQuestion.Explanation ?? "Excellent choice. Keep going!";
		}
		else
		{
			FeedbackTitle = "Not quite";
			FeedbackColor = "#D85C5C";
			FeedbackMessage =
				$"Wrong answer. Correct answer: " +
				$"{CurrentQuestion.Choices[CurrentQuestion.CorrectIndex]}" +
				(string.IsNullOrWhiteSpace(CurrentQuestion.Explanation)
					? string.Empty
					: $"\n{CurrentQuestion.Explanation}");
		}

		ScoreText = $"Score: {Score}";
		OnPropertyChanged(nameof(CanAdvance));
	}

	public bool CanAdvance => IsAnswered;

	[RelayCommand]
	private async Task NextAsync()
	{
		if (!CanAdvance)
			return;

		if (currentIndex < questions.Count - 1)
		{
			currentIndex++;
			DisplayCurrentQuestion();
		}
		else
		{
			await SaveSessionAsync();

			await Shell.Current.GoToAsync(
				$"result?score={Score}&total={questions.Count}");
		}
	}

	private async Task SaveSessionAsync()
	{
		int durationSeconds = (int)(DateTime.UtcNow - quizStartedAt).TotalSeconds;

		var session = new GameSession
		{
			Date = DateTime.Now,
			Category = category,
			Difficulty = difficulty?.ToString() ?? "Mixed",
			Score = Score,
			TotalQuestions = questions.Count,
			DurationSeconds = durationSeconds,
			wasAiGenerated = useAiGeneration
		};

		try
		{
			await database.SaveSessionAsync(session);
		}
		catch (Exception exception)
		{
			// History is a "Should have" extra; never let a save failure
			// block the user from seeing their result.
			System.Diagnostics.Debug.WriteLine($"Failed to save session: {exception}");
		}
	}
}
