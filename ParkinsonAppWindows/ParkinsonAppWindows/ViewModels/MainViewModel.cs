using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using Microsoft.Win32;
using ParkinsonAppWindows.Models;
using ParkinsonAppWindows.Views; // Для DetailWindow и ExportWindow

namespace ParkinsonAppWindows.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private DrawingParameter _thicknessParam = DrawingParameter.Pressure;
        private DrawingParameter _colorParam = DrawingParameter.Azimuth;
        private bool _isDarkMode = false;
        private bool _isPlaying = false;
        private bool _isLoading = false;

        private string _clearButtonText = "🗑 Clear All";

        private DispatcherTimer _timer;
        private Stopwatch _stopwatch;
        private double _lastElapsedSeconds = 0;

        public ObservableCollection<SessionViewModel> Sessions { get; set; } = new ObservableCollection<SessionViewModel>();

        public DrawingParameter ThicknessParam { get => _thicknessParam; set { _thicknessParam = value; OnPropertyChanged(); } }
        public DrawingParameter ColorParam { get => _colorParam; set { _colorParam = value; OnPropertyChanged(); } }
        public bool IsDarkMode { get => _isDarkMode; set { _isDarkMode = value; OnPropertyChanged(); } }
        public bool IsPlaying { get => _isPlaying; set { _isPlaying = value; OnPropertyChanged(); } }
        public bool IsLoading { get => _isLoading; set { _isLoading = value; OnPropertyChanged(); } }

        public MainViewModel()
        {
            _stopwatch = new Stopwatch();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(16); // ~60 FPS
            _timer.Tick += Timer_Tick;
            UpdateClearText();
        }

        public string ClearButtonText
        {
            get => _clearButtonText;
            set { _clearButtonText = value; OnPropertyChanged(); }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            double currentSeconds = _stopwatch.Elapsed.TotalSeconds;
            double delta = currentSeconds - _lastElapsedSeconds;
            _lastElapsedSeconds = currentSeconds;

            foreach (var session in Sessions)
            {
                session.AdvanceFrame(delta);
            }
        }

        private ICommand _loadFilesCommand;
        public ICommand LoadFilesCommand => _loadFilesCommand ??= new RelayCommand(_ => LoadFiles());

        private ICommand _clearCommand;
        public ICommand ClearCommand => _clearCommand ??= new RelayCommand(_ => ClearAll());

        private ICommand _playPauseCommand;
        public ICommand PlayPauseCommand => _playPauseCommand ??= new RelayCommand(_ => TogglePlay());

        private ICommand _resetCommand;
        public ICommand ResetCommand => _resetCommand ??= new RelayCommand(_ => ResetAnimation());

        private ICommand _openDetailCommand;
        public ICommand OpenDetailCommand => _openDetailCommand ??= new RelayCommand(OpenDetail);

        private ICommand _openExportCommand;
        public ICommand OpenExportCommand => _openExportCommand ??= new RelayCommand(_ => OpenExportWindow());

        public async void LoadFiles()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "JSON files (*.json)|*.json";
            openFileDialog.Multiselect = true;

            if (openFileDialog.ShowDialog() == true)
            {
                IsLoading = true;
                var files = openFileDialog.FileNames;

                await Task.Run(() =>
                {
                    foreach (string filename in files)
                    {
                        try
                        {
                            string json = File.ReadAllText(filename);
                            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                            var data = JsonSerializer.Deserialize<DrawingDataFile>(json, options);

                            if (data != null && data.Data != null)
                            {
                                data.FileName = Path.GetFileName(filename);

                                var points = data.Data.SelectMany(x => x).ToList();

                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    var newSession = new SessionViewModel(data, points);
                                    newSession.PropertyChanged += Session_PropertyChanged;

                                    Sessions.Add(newSession);
                                });
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Error loading file {filename}: {ex.Message}");
                        }
                    }
                });

                UpdateClearText();
                IsLoading = false;
            }
        }

        private void Session_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SessionViewModel.IsSelected))
            {
                UpdateClearText();
            }
        }

        private void UpdateClearText()
        {
            int selectedCount = Sessions.Count(s => s.IsSelected);
            if (selectedCount > 0)
            {
                ClearButtonText = $"🗑 Delete ({selectedCount})";
            }
            else
            {
                ClearButtonText = "🗑 Clear All";
            }
        }

        public void ClearAll()
        {
            StopAnimation();

            var selectedItems = Sessions.Where(s => s.IsSelected).ToList();

            if (selectedItems.Any())
            {
                foreach (var item in selectedItems)
                {
                    item.PropertyChanged -= Session_PropertyChanged;
                    Sessions.Remove(item);
                }
            }
            else
            {
                foreach (var item in Sessions)
                {
                    item.PropertyChanged -= Session_PropertyChanged;
                }
                Sessions.Clear();
            }

            UpdateClearText();
        }

        public void TogglePlay()
        {
            if (IsPlaying)
            {
                StopAnimation();
            }
            else
            {
                if (Sessions.Count > 0 && Sessions.All(s => s.IsFinished))
                {
                    ResetAnimation();
                }
                StartAnimation();
            }
        }

        private void StartAnimation()
        {
            IsPlaying = true;

            if (!_stopwatch.IsRunning)
            {
                _stopwatch.Start();
                _lastElapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            }

            _timer.Start();
        }

        private void StopAnimation()
        {
            IsPlaying = false;
            _timer.Stop();
            _stopwatch.Stop();
        }

        public void ResetAnimation()
        {
            StopAnimation();
            _stopwatch.Reset();
            _lastElapsedSeconds = 0;

            foreach (var s in Sessions) s.Reset();
        }

        private void OpenDetail(object parameter)
        {
            if (parameter is SessionViewModel session)
            {
                var detailVM = new DetailViewModel(session, ThicknessParam, ColorParam, IsDarkMode);
                var detailWindow = new DetailWindow(detailVM); 
                detailWindow.Show();
            }
        }

        private void OpenExportWindow()
        {
            var selectedSession = Sessions.FirstOrDefault(s => s.IsSelected);

            if (selectedSession == null)
            {
                MessageBox.Show("Please select at least one drawing to export (check the box).", "Export", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var exportVM = new ExportViewModel(selectedSession);
            exportVM.IsDarkMode = this.IsDarkMode;

            var exportWindow = new ExportWindow(exportVM);
            exportWindow.ShowDialog();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}