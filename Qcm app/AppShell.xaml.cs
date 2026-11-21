using Qcm_app.Views;

namespace Qcm_app
{
	public partial class AppShell : Shell
	{
		public AppShell()
		{
			InitializeComponent();
			Routing.RegisterRoute("categories", typeof(CategoriesPage));
			Routing.RegisterRoute("quiz", typeof(QuizPage));
			Routing.RegisterRoute("result", typeof(ResultPage));
			Routing.RegisterRoute("history", typeof(HistoryPage));
			Routing.RegisterRoute("stats", typeof(StatsPage));
			Routing.RegisterRoute("teacher-login", typeof(TeacherLoginPage));
			Routing.RegisterRoute("teacher", typeof(TeacherPage));
		}
	}
}
