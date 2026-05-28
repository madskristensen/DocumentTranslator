using System.Windows;
using DocumentTranslator.Services;

namespace DocumentTranslator;

public partial class App : Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		base.OnStartup(e);
		ThemeManager.Apply(SettingsStore.Load().Theme);
		new MainWindow().Show();
	}
}
