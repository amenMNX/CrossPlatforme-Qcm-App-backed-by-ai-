using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Qcm_app.Data;
using Qcm_app.Services;
using Qcm_app.ViewModels;
using Qcm_app.Views;

namespace Qcm_app
{
	public static class MauiProgram
	{
		public static MauiApp CreateMauiApp()
		{
			var builder = MauiApp.CreateBuilder();
			builder
				.UseMauiApp<App>()
				.ConfigureFonts(fonts =>
				{
					fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
					fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
				});

#if DEBUG
			builder.Logging.AddDebug();
#endif

			// --- Services ---
			builder.Services.AddSingleton<QuestionService>();
			builder.Services.AddSingleton<QuizDatabase>();
			builder.Services.AddHttpClient<AiQuestionService>(client =>
			{
				// The service also has its own linked-token timeout; this is a safety net
				// so HttpClient never waits its default 100 seconds.
				client.Timeout = TimeSpan.FromSeconds(35);
			});

			// --- ViewModels ---
			builder.Services.AddTransient<HomeViewModel>();
			builder.Services.AddTransient<CategoriesViewModel>();
			builder.Services.AddTransient<QuizViewModel>();
			builder.Services.AddTransient<ResultViewModel>();
			builder.Services.AddTransient<HistoryViewModel>();
			builder.Services.AddTransient<StatsViewModel>();
			builder.Services.AddTransient<TeacherLoginViewModel>();
			builder.Services.AddTransient<TeacherViewModel>();

			// --- Pages ---
			builder.Services.AddTransient<MainPage>();
			builder.Services.AddTransient<CategoriesPage>();
			builder.Services.AddTransient<QuizPage>();
			builder.Services.AddTransient<ResultPage>();
			builder.Services.AddTransient<HistoryPage>();
			builder.Services.AddTransient<StatsPage>();
			builder.Services.AddTransient<TeacherLoginPage>();
			builder.Services.AddTransient<TeacherPage>();


			return builder.Build();
		}
	}
}
