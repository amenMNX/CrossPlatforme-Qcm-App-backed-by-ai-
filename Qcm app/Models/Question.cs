using System;
using System.Collections.Generic;
using System.Text;
using Qcm_app.Models;

namespace Qcm_app.Models;

public class Question
{
	public string Id { get; set; } = string.Empty;

	public string Text { get; set; } = string.Empty;

	public List<string> Choices { get; set; } = [];

	public int CorrectIndex { get; set; }

	public string Category { get; set; } = string.Empty;

	public Difficulty Difficulty { get; set; }

	public string? Explanation { get; set; }
}