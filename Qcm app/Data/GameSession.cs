using System;
using System.Collections.Generic;
using System.Text;
using SQLite;

namespace Qcm_app.Data
{
	public  class GameSession
	{	
		[PrimaryKey, AutoIncrement]
		public int Id { get; set; }
		public DateTime Date { get; set; }
		public string Category { get; set; }
		public string Difficulty { get; set; }
		public int Score { get; set; }
		public int TotalQuestions { get; set; }
		public int DurationSeconds { get; set; }
		public bool wasAiGenerated { get; set; }
	}
}
