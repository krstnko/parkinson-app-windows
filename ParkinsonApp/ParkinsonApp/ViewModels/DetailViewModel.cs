using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using ParkinsonAppWindows.Models;

namespace ParkinsonAppWindows.ViewModels
{
    public class DetailViewModel : INotifyPropertyChanged
    {
        private SessionViewModel _session;
        public SessionViewModel Session { get => _session; set { _session = value; OnPropertyChanged(); } }

        private DrawingParameter _thicknessParam = DrawingParameter.Pressure;
        private DrawingParameter _colorParam = DrawingParameter.Azimuth;
        private bool _isDarkMode = false;

        public DrawingParameter ThicknessParam { get => _thicknessParam; set { _thicknessParam = value; OnPropertyChanged(); } }
        public DrawingParameter ColorParam { get => _colorParam; set { _colorParam = value; OnPropertyChanged(); } }
        public bool IsDarkMode { get => _isDarkMode; set { _isDarkMode = value; OnPropertyChanged(); } }
        public IEnumerable<DrawingParameter> AvailableParameters => Enum.GetValues(typeof(DrawingParameter)).Cast<DrawingParameter>();


        private DispatcherTimer _timer;
        private Stopwatch _stopwatch;
        private bool _isPlaying;
        private double _lastElapsedSeconds = 0;

        public bool IsPlaying { get => _isPlaying; set { _isPlaying = value; OnPropertyChanged(); } }

        public DetailViewModel()
        {
            _stopwatch = new Stopwatch();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _timer.Tick += Timer_Tick;
        }

        private ICommand _loadFileCommand;
        public ICommand LoadFileCommand => _loadFileCommand ??= new RelayCommand(_ => LoadFile());

        public ICommand PlayPauseCommand => new RelayCommand(_ => TogglePlay());
        public ICommand ResetCommand => new RelayCommand(_ => ResetAnimation());

        private void LoadFile()
        {
            var dlg = new OpenFileDialog { Filter = "JSON Files|*.json" };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string json = File.ReadAllText(dlg.FileName);
                    var dataFile = JsonSerializer.Deserialize<DrawingDataFile>(json);
                    dataFile.FileName = Path.GetFileName(dlg.FileName);

                    var flatPoints = new List<DrawingPoint>();
                    if (dataFile.Data != null)
                        foreach (var stroke in dataFile.Data) flatPoints.AddRange(stroke);

                    Session = new SessionViewModel(dataFile, flatPoints);
                    ResetAnimation();
                }
                catch (Exception ex) { /* Error */ }
            }
        }

        private void TogglePlay()
        {
            if (IsPlaying) { IsPlaying = false; _timer.Stop(); _stopwatch.Stop(); }
            else
            {
                if (Session == null) return;
                if (Session.IsFinished) Session.Reset();
                IsPlaying = true;
                if (!_stopwatch.IsRunning) { _stopwatch.Start(); _lastElapsedSeconds = _stopwatch.Elapsed.TotalSeconds; }
                _timer.Start();
            }
        }

        private void ResetAnimation()
        {
            IsPlaying = false; _timer.Stop(); _stopwatch.Reset(); _lastElapsedSeconds = 0; Session?.Reset();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (Session == null || Session.IsFinished) { PauseAnimation(); return; }
            double current = _stopwatch.Elapsed.TotalSeconds;
            Session.AdvanceFrame(current - _lastElapsedSeconds);
            _lastElapsedSeconds = current;
        }
        private void PauseAnimation() { IsPlaying = false; _timer.Stop(); _stopwatch.Stop(); }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}