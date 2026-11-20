using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qcm_app.Models;
using Qcm_app.Services;

namespace Qcm_app.ViewModels;

/// <summary>
/// Lets the user pick a category, a difficulty, and whether to play with
/// AI-generated questions (Gemini) or the local offline question bank,
/// before a quiz starts (US02, US14, US15).
/// </summary>
public partial class CategoriesViewModel : ObservableObject
{
	private readonly QuestionService questionService;

	public ObservableCollection<string> Categories { get; } = new();

	public List<string> DifficultyOptions { get; } =
		new() { "Any", "Easy", "Medium", "Hard" };

	[ObservableProperty]
	private string selectedCategory = QuizSettings.AllCategories;

	[ObservableProperty]
	private string selectedDifficulty = "Any";

	[ObservableProperty]
	private bool useAiGeneration;

	[ObservableProperty]
	private bool isBusy;

	public CategoriesViewModel(QuestionService questionService)
	{
		this.questionService = questionService;
	}

	public async Task InitializeAsync()
	{
		if (Categories.Count > 0)
			return;

		Categories.Add(QuizSettings.AllCategories);

		var categories = await questionService.GetCategoriesAsync();
		foreach (var category in categories)
			Categories.Add(category);
	}

	[RelayCommand]
	private async Task StartAsync()
	{
		Difficulty? difficulty = SelectedDifficulty switch
		{
			"Easy" => Difficulty.Easy,
			"Medium" => Difficulty.Medium,
			"Hard" => Difficulty.Hard,
			_ => null
		};

		var query = new Dictionary<string, object>
		{
			["category"] = SelectedCategory,
			["difficulty"] = difficulty?.ToString() ?? string.Empty,
			["useAi"] = UseAiGeneration
		};

		await Shell.Current.GoToAsync("quiz", query);
	}
}
