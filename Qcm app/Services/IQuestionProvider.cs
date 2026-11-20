using System;
using System.Collections.Generic;
using System.Text;
using Qcm_app.Models;

namespace Qcm_app.Services
{
	public interface IQuestionProvider
	{
		Task<List<Question>> GetQuestionsAsync(
			string? category,
			Difficulty? difficulty,
			int count,
			CancellationToken cancellationToken = default
		);
	}
}
