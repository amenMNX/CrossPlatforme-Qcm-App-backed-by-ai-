using System;
using System.Collections.Generic;
using System.Text;

namespace Qcm_app.Services
{
	public static class GeminiApiKeyProvider
	{
		public static string ApiKey => Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? string.Empty;
	}
}
