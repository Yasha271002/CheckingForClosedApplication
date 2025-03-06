using CommunityToolkit.Mvvm.ComponentModel;

namespace CheckingForClosedApplication.Model;

public partial class Settings : ObservableObject
{
    [ObservableProperty] private string? _applicationName;
    [ObservableProperty] private bool _explorerKill;

    [ObservableProperty] private bool _isThereBackground;
    [ObservableProperty] private string _backgroundPath = string.Empty;

    [ObservableProperty] private bool _isButton;
    [ObservableProperty] private bool _isBackgroundButton;

    [ObservableProperty] private bool _onTimer;
    [ObservableProperty] private double _timerSecond = 5;

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;

    [ObservableProperty] private double _widthButton;
    [ObservableProperty] private double _heightButton;
}