using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Estudio.Setup.Windows;

namespace Estudio.Setup.WindowsHost;

public partial class App : System.Windows.Application
{
	protected override void OnStartup(StartupEventArgs e)
	{
		DispatcherUnhandledException += OnDispatcherUnhandledException;
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
		TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
		base.OnStartup(e);
	}

	private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
	{
		e.Handled = true;
		ReportUnhandledException(e.Exception);
	}

	private void OnUnhandledException(object? sender, UnhandledExceptionEventArgs e)
	{
		if (e.ExceptionObject is Exception exception)
		{
			ReportUnhandledException(exception);
		}
	}

	private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
	{
		e.SetObserved();
		ReportUnhandledException(e.Exception);
	}

	private void ReportUnhandledException(Exception exception)
	{
		var currentApplication = Current;
		if (currentApplication?.Dispatcher is null)
		{
			var state = InstallerUiExceptionSupport.CreateUnexpectedFailure(exception);
			System.Windows.MessageBox.Show(
				$"{state.Headline}{Environment.NewLine}{Environment.NewLine}{state.Body}",
				"Estudio Socrático",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
			return;
		}

		currentApplication.Dispatcher.Invoke(() =>
		{
			if (currentApplication.MainWindow is MainWindow window)
			{
				window.HandleUnhandledException(exception);
				return;
			}

			var state = InstallerUiExceptionSupport.CreateUnexpectedFailure(exception);
			System.Windows.MessageBox.Show(
				$"{state.Headline}{Environment.NewLine}{Environment.NewLine}{state.Body}",
				"Estudio Socrático",
				MessageBoxButton.OK,
				MessageBoxImage.Error);
		});
	}
}