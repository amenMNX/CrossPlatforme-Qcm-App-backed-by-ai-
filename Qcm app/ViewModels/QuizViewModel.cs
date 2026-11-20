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
	private int score;

	[ObservableProperty]
	private bool isAnswered;

	[ObservableProperty]
	private bool isLoading;

	[ObservableProperty]
	private string loadingMessage = "Loading questions...";

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
		IsLoading = true;
		LoadingMessage = useAiGeneration
			? "Generating questions with AI..."
			: "Loading questions...";

		IQuestionProvider provider = useAiGeneration ? aiQuestionService : localQuestionService;

		questions = await provider.GetQuestionsAsync(category, difficulty, count: 10);

		IsLoading = false;

		if (questions.Count == 0)
		{
			FeedbackMessage = "No questions available. Please try a different category.";
			return;
		}

		currentIndex = 0;
		Score = 0;
		quizStartedAt = DateTime.UtcNow;
		DisplayCurrentQuestion();
	}

	private void DisplayCurrentQuestion()
	{
		CurrentQuestion = questions[currentIndex];
		ProgressText = $"Question {currentIndex + 1} / {questions.Count}";
		FeedbackMessage = string.Empty;
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
			FeedbackMessage = "Correct answer!";
		}
		else
		{
			FeedbackMessage =
				$"Wrong answer. Correct answer: " +
				$"{CurrentQuestion.Choices[CurrentQuestion.CorrectIndex]}";
		}

		await Task.Delay(1200);

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
