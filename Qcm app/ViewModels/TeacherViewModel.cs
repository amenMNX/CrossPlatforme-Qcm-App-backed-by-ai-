using System;
using System.Collections.Generic;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Qcm_app.Data;
using System.Collections.ObjectModel;
using Qcm_app.Models;
using Qcm_app.Services;

namespace Qcm_app.ViewModels;

public partial class TeacherViewModel : ObservableObject
{
	private readonly AiQuestionService aiQuestionService;

	public ObservableCollection<TeacherQuestionDraft> AiPreviewQuestions { get; } = new();

	[ObservableProperty]
	private string aiTopic = string.Empty;

	[ObservableProperty]
	private int aiQuestionCount = 5;

	[ObservableProperty]
	private bool isGenerating;

	[ObservableProperty]
	private string aiStatusMessage = string.Empty;
	private readonly QuizDatabase database;

	public List<string> DifficultyOptions { get; } =
		new() { "Easy", "Medium", "Hard" };

	public List<string> CorrectChoiceOptions { get; } =
		new() { "Choice 1", "Choice 2", "Choice 3", "Choice 4" };

	[ObservableProperty]
	private string questionText = string.Empty;

	[ObservableProperty]
	private string choice1 = string.Empty;

	[ObservableProperty]
	private string choice2 = string.Empty;

	[ObservableProperty]
	private string choice3 = string.Empty;

	[ObservableProperty]
	private string choice4 = string.Empty;

	[ObservableProperty]
	private int correctChoiceIndex = -1;

	[ObservableProperty]
	private string category = string.Empty;

	[ObservableProperty]
	private string selectedDifficulty = "Medium";

	[ObservableProperty]
	private string statusMessage = string.Empty;

	[ObservableProperty]
	private bool isSaving;

	public TeacherViewModel(QuizDatabase database, AiQuestionService aiQuestionService)
	{
		this.database = database;
		this.aiQuestionService = aiQuestionService;
	}

	[RelayCommand]
	private async Task GenerateAiQuestionsAsync()
	{
		if (string.IsNullOrWhiteSpace(AiTopic))
		{
			AiStatusMessage = "Enter a topic.";
			return;
		}

		if (AiQuestionCount is < 1 or > 20)
		{
			AiStatusMessage = "Choose between 1 and 20 questions.";
			return;
		}

		IsGenerating = true;
		AiStatusMessage = string.Empty;
		AiPreviewQuestions.Clear();

		try
		{
			var generated = await aiQuestionService.GenerateQuestionsForTeacherAsync(
				AiTopic,
				AiQuestionCount);

			foreach (var question in generated)
				AiPreviewQuestions.Add(TeacherQuestionDraft.FromQuestion(question));

			AiStatusMessage = generated.Count > 0
				? "Review the generated questions before saving."
				: aiQuestionService.LastGenerationIssue ?? "No questions were generated.";
		}
		catch
		{
			AiStatusMessage = "Could not generate questions.";
		}
		finally
		{
			IsGenerating = false;
		}
	}
	[RelayCommand]
	private async Task SavePreviewQuestionAsync(TeacherQuestionDraft draft)
	{
		string[] choices = { draft.Choice1, draft.Choice2, draft.Choice3, draft.Choice4 };

		if (string.IsNullOrWhiteSpace(draft.Text) ||
			string.IsNullOrWhiteSpace(draft.Category) ||
			choices.Any(string.IsNullOrWhiteSpace) ||
			draft.CorrectIndex is < 0 or > 3 ||
			!draft.DifficultyOptions.Contains(draft.Difficulty))
		{
			AiStatusMessage = "Complete and verify the question before saving.";
			return;
		}

		IsSaving = true;
		try
		{
			await database.AddTeacherQuestionAsync(draft.ToTeacherQuestion());
			AiPreviewQuestions.Remove(draft);
			AiStatusMessage = "Question accepted and saved.";
		}
		catch
		{
			AiStatusMessage = "Could not save the question.";
		}
		finally
		{
			IsSaving = false;
		}
	}

	[RelayCommand]
	private async Task SaveQuestionAsync()
	{
		string[] choices = { Choice1, Choice2, Choice3, Choice4 };

		if (string.IsNullOrWhiteSpace(QuestionText) ||
			string.IsNullOrWhiteSpace(Category) ||
			choices.Any(string.IsNullOrWhiteSpace))
		{
			StatusMessage = "Fill in the question, all four choices, and category.";
			return;
		}

		if (CorrectChoiceIndex is < 0 or > 3)
		{
			StatusMessage = "Select the correct choice.";
			return;
		}

		IsSaving = true;
		StatusMessage = string.Empty;

		try
		{
			await database.AddTeacherQuestionAsync(new TeacherQuestion
			{
				Text = QuestionText.Trim(),
				Choice1 = Choice1.Trim(),
				Choice2 = Choice2.Trim(),
				Choice3 = Choice3.Trim(),
				Choice4 = Choice4.Trim(),
				CorrectIndex = CorrectChoiceIndex,
				Category = Category.Trim(),
				Difficulty = SelectedDifficulty
			});

			StatusMessage = "Question saved.";
			QuestionText = string.Empty;
			Choice1 = string.Empty;
			Choice2 = string.Empty;
			Choice3 = string.Empty;
			Choice4 = string.Empty;
			CorrectChoiceIndex = -1;
		}
		catch
		{
			StatusMessage = "Could not save the question.";
		}
		finally
		{
			IsSaving = false;
		}
	}
}