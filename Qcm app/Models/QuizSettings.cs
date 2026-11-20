using System;
using System.Collections.Generic;
using System.Text;
using Qcm_app.Models;

namespace Qcm_app.Models
{
	public class QuizSettings
	{
		public const string AllCategories = "All categories";
		public string Category { get; set; } = AllCategories;

		public Difficulty? Difficulty { get; set; } = null;
		public bool UseAiGeneration { get; set; } = false;
		public int QuestionCount { get; set; } = 10;
	}
}
