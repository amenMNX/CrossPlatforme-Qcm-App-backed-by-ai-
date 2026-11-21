using System;
using System.Collections.Generic;
using System.Text;
using SQLite;
namespace Qcm_app.Data
{
	[Table("TeacherQuestions")]
	public  class TeacherQuestion
	{
		[PrimaryKey,AutoIncrement]
		public int Id{ get; set; }
		[NotNull]
		public string Text { get; set; } = string.Empty;
		[NotNull]
		public string Choice1 { get; set; } = string.Empty;
		[NotNull]
		public string Choice2 { get; set; } = string.Empty;
		[NotNull]
		public string Choice3 { get; set; } = string.Empty;
		[NotNull]
		public string Choice4 { get; set; } = string.Empty;
		public int CorrectIndex{ get; set; }
		[NotNull]
		public string Category { get; set; } = string.Empty;

		[NotNull]
		public string Difficulty { get; set; } = "Medium";

	}
}
