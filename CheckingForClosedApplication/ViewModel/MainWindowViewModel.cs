using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CheckingForClosedApplication.Helpers;
using CheckingForClosedApplication.Model;
using Serilog;
using CommunityToolkit.Mvvm.Input;
using System.Windows.Threading;
using System.Threading;
using System.Threading.Tasks;

namespace CheckingForClosedApplication.ViewModel;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly JsonHelper _jsonHelper = new();
    private readonly string _settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
    private readonly string _logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
    private bool _isAppRunning;
    private Process? _app = new();
    private Task? _monitorTask;
    private CancellationTokenSource? _monitorCts; 

    [ObservableProperty] private int _processId;
    [ObservableProperty] private bool _isClosed;
    [ObservableProperty] private Settings _settings = new();

    private DispatcherTimer _timer = new();
    private int _sec = 0;
    private bool _shouldRestart = true;

    private readonly ILogger _logger;

    public MainWindowViewModel()
    {
        if (!Directory.Exists(_logsDir))
            Directory.CreateDirectory(_logsDir);

        _logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(Path.Combine(_logsDir, "log-.txt"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            _logger.Information("Приложение запустилось");
            GetSettings();
            StartProcessing();
        }
        catch (Exception ex)
        {
            _logger.Fatal(ex, "Ошибка инициализации приложения");
            throw;
        }
    }

    private void GetSettings()
    {
        try
        {
            Settings = _jsonHelper.ReadJsonFromFile(_settingsPath, Settings);
            if (Settings.ExplorerKill)
            {
                ExplorerHelper.KillExplorer();
            }
            _logger.Information("Настройки загружены из {SettingsPath}, IsButton: {IsButton}", _settingsPath, Settings.IsButton);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка загрузки настроек из {SettingsPath}", _settingsPath);
            throw;
        }
    }

    private void StartProcessing()
    {
        if (_isAppRunning || !_shouldRestart)
        {
            _logger.Information("Запуск приложения отменён: _isAppRunning = {IsAppRunning}, _shouldRestart = {ShouldRestart}", _isAppRunning, _shouldRestart);
            return;
        }

        try
        {
            var appPath = Settings.ApplicationName ?? throw new InvalidOperationException("Имя приложения не указано");

            if (!File.Exists(appPath))
            {
                _logger.Error("Файл по пути {AppPath} не найден", appPath);
                throw new FileNotFoundException("Указанный файл не найден", appPath);
            }

            _logger.Information("Попытка запуска приложения: {AppPath}", appPath);
            _app = Process.Start(appPath);

            if (_app == null)
            {
                _logger.Error("Ошибка запуска приложения, вернуло Null для {AppPath}", appPath);
                return;
            }

            IsClosed = false;
            _processId = _app.Id;
            _isAppRunning = true;
            _logger.Information("Приложение запущено ID процесса: {ProcessId}", _processId);

            _monitorCts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorApp(_monitorCts.Token));
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка запуска приложения: {AppName}", Settings.ApplicationName);
            _isAppRunning = false;
            IsClosed = true;
        }
    }

    private async Task MonitorApp(CancellationToken cancellationToken)
    {
        if (_app == null)
        {
            _logger.Warning("Ошибка мониторинга приложения");
            return;
        }

        try
        {
            _app.EnableRaisingEvents = true;
            _app.Exited += (sender, args) =>
            {
                _logger.Information("Приложение {AppName} (ID процесса: {ProcessId}) было закрыто с кодом: {ExitCode}",
                    Settings.ApplicationName, _processId, _app.ExitCode);

                if (_app.ExitCode != 0)
                {
                    _logger.Error("Приложение {AppName} завершило работу с кодом: {ExitCode}",
                        Settings.ApplicationName, _app.ExitCode);
                }
            };

            await _app.WaitForExitAsync(cancellationToken);
            _logger.Information("Процесс {AppName} завершился естественно", Settings.ApplicationName);

            IsClosed = true;
            _isAppRunning = false;

            if (cancellationToken.IsCancellationRequested || !_shouldRestart)
            {
                _logger.Information("Перезапуск отменён: cancellation = {Cancelled}, _shouldRestart = {ShouldRestart}",
                    cancellationToken.IsCancellationRequested, _shouldRestart);
                return;
            }

            _logger.Information("Ожидание перед перезапуском...");
            await Task.Delay(1000, cancellationToken);
            StartProcessing();
        }
        catch (OperationCanceledException)
        {
            _logger.Information("Мониторинг приложения {AppName} был отменён", Settings.ApplicationName);
            IsClosed = true;
            _isAppRunning = false;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка мониторинга {AppName}", Settings.ApplicationName);
            IsClosed = true;
            _isAppRunning = false;

            if (cancellationToken.IsCancellationRequested || !_shouldRestart) return;

            await Task.Delay(1000, cancellationToken);
            StartProcessing();
        }
    }

    private void Timer(object sender, EventArgs eventArgs)
    {
        _sec++;
        if (_sec < Settings.TimerSecond) return;

        ExplorerHelper.RunExplorer();
        CloseApp();
        Application.Current.Shutdown();
    }

    [RelayCommand]
    private void StopTimer()
    {
        _timer.Tick -= Timer;
        _timer.Stop();
        _sec = 0;
    }

    [RelayCommand]
    private void StartTimer()
    {
        if (!Settings.OnTimer)
        {
            CloseApp();
            return;
        }

        _timer?.Stop();
        _sec = 0;
        _timer.Interval = TimeSpan.FromSeconds(1);
        _timer.Tick += Timer;
        _timer.Start();
    }

    private async void CloseApp()
    {
        try
        {
            _logger.Information("Попытка закрытия приложения {AppName}", Settings.ApplicationName);

            _shouldRestart = false;
            StopTimerCommand.Execute(null);

            if (_monitorCts != null)
            {
                _monitorCts.Cancel();
                if (_monitorTask != null)
                {
                    _logger.Information("Ожидание завершения задачи мониторинга...");
                    await _monitorTask;
                    _logger.Information("Задача мониторинга завершена");
                }
                _monitorCts.Dispose();
                _monitorCts = null;
                _monitorTask = null;
            }

            if (_app is { HasExited: false })
            {
                _logger.Information("Закрытие процесса с ID: {ProcessId}", _processId);

                _app.CloseMainWindow();

                if (!_app.WaitForExit(2000))
                {
                    _logger.Warning("Приложение не закрылось корректно, выполняется принудительное завершение");
                    _app.Kill();
                }

                _logger.Information("Приложение успешно закрыто");
            }

            _isAppRunning = false;
            IsClosed = true;
            _app = null;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Ошибка при закрытии приложения {AppName}", Settings.ApplicationName);
        }
    }
}