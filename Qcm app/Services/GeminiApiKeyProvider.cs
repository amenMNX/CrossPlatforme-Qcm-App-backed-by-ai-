using System;
using System.IO;

namespace Qcm_app.Services;

public static class GeminiApiKeyProvider
{
	private const string VariableName = "GEMINI_API_KEY";

	public static string ApiKey
	{
		get
		{
			string? environmentValue = Environment.GetEnvironmentVariable(VariableName);
			if (!string.IsNullOrWhiteSpace(environmentValue))
				return environmentValue.Trim();

			return ReadFromDotEnv() ?? string.Empty;
		}
	}

	private static string? ReadFromDotEnv()
	{
#if WINDOWS && DEBUG
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null)
		{
			if (File.Exists(Path.Combine(directory.FullName, "Qcm app.csproj")))
			{
				string workspaceDirectory = directory.Parent?.FullName ?? directory.FullName;
				return ReadValueFromFile(Path.Combine(workspaceDirectory, ".env"))
					?? ReadValueFromFile(Path.Combine(directory.FullName, ".env"));
			}

			directory = directory.Parent;
		}
#endif
		return null;
	}

	private static string? ReadValueFromFile(string path)
	{
		if (!File.Exists(path))
			return null;

		try
		{
			foreach (string line in File.ReadLines(path))
			{
				string trimmedLine = line.Trim();
				if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
					continue;

				int separator = trimmedLine.IndexOf('=');
				if (separator < 0 ||
					!string.Equals(trimmedLine[..separator].Trim(), VariableName, StringComparison.Ordinal))
				{
					continue;
				}

				string value = trimmedLine[(separator + 1)..].Trim();
				if (value.Length >= 2 &&
					((value[0] == '"' && value[^1] == '"') ||
					 (value[0] == '\'' && value[^1] == '\'')))
				{
					value = value[1..^1];
				}

				return string.IsNullOrWhiteSpace(value) ? null : value;
			}
		}
		catch (IOException)
		{
			return null;
		}
		catch (UnauthorizedAccessException)
		{
			return null;
		}

		return null;
	}
}
