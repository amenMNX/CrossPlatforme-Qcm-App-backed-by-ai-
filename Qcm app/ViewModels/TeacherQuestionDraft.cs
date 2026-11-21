using CommunityToolkit.Mvvm.ComponentModel;
using Qcm_app.Data;
using Qcm_app.Models;

namespace Qcm_app.ViewModels;

public partial class TeacherQuestionDraft : ObservableObject
{
	[ObservableProperty]
	private string text = string.Empty;


	[ObservableProperty]
	private string choice1 = string.Empty;

	[ObservableProperty]
	private string choice2 = string.Empty;

	[ObservableProperty]
	private string choice3 = string.Empty;

	[ObservableProperty]
	private string choice4 = string.Empty;

	[ObservableProperty]
	private int correctIndex;

	[ObservableProperty]
	private string category = string.Empty;

	[ObservableProperty]
	private string difficulty = "Medium";

	public List<string> DifficultyOptions { get; } =
		new() { "Easy", "Medium", "Hard" };

	public List<string> CorrectChoiceOptions { get; } =
		new() { "Choice 1", "Choice 2", "Choice 3", "Choice 4" };

	public static TeacherQuestionDraft FromQuestion(Question question) => new()
	{
		Text = question.Text,
		Choice1 = question.Choices[0],
		Choice2 = question.Choices[1],
		Choice3 = question.Choices[2],
		Choice4 = question.Choices[3],
		CorrectIndex = question.CorrectIndex,
		Category = question.Category,
		Difficulty = question.Difficulty.ToString()
	};

	public TeacherQuestion ToTeacherQuestion() => new()
	{
		Text = Text.Trim(),
		Choice1 = Choice1.Trim(),
		Choice2 = Choice2.Trim(),
		Choice3 = Choice3.Trim(),
		Choice4 = Choice4.Trim(),
		CorrectIndex = CorrectIndex,
		Category = Category.Trim(),
		Difficulty = Difficulty
	};
}