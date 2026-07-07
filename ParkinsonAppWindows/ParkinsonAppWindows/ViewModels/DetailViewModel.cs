using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ParkinsonAppWindows.Models;
using ParkinsonAppWindows.Services;

namespace ParkinsonAppWindows.ViewModels
{
    public class ModelResultItem
    {
        public string ModelName { get; set; }
        public double Probability { get; set; }
        public string Diagnosis => Probability >= 50 ? "PD" : "Healthy";

        public Brush Color => Probability >= 50 ? Brushes.Red : Brushes.Green;

        public double BarWidth => Probability;
    }

    public class DetailViewModel : INotifyPropertyChanged
    {
        public SessionViewModel Session { get; }

        private DrawingParameter _thicknessParam;
        private DrawingParameter _colorParam;
        private bool _isDarkMode;
        private bool _isPlaying;
        private bool _showGrid = true;

        private double _scale = 1.0;
        private double _offsetX = 0;
        private double _offsetY = 0;

        private DispatcherTimer _timer;
        private Stopwatch _stopwatch;
        private double _lastElapsedSeconds = 0;

        private readonly FeatureExtractor _featureExtractor = new FeatureExtractor();
        private readonly OnnxService _onnxService = new OnnxService();

        private bool _isSidebarVisible = true;
        private bool _isAnalyzing = false;
        private string _errorMessage;

        private string _ensembleDiagnosis;
        private double _ensembleProbability;
        private Brush _ensembleColor;

        private ObservableCollection<ModelResultItem> _analysisResults;

        public DetailViewModel(SessionViewModel session, DrawingParameter initialThickness, DrawingParameter initialColor, bool initialDark)
        {
            Session = session;

            _thicknessParam = initialThickness;
            _colorParam = initialColor;
            _isDarkMode = initialDark;

            _stopwatch = new Stopwatch();
            _timer = new DispatcherTimer();
            _timer.Interval = TimeSpan.FromMilliseconds(16);
            _timer.Tick += Timer_Tick;
        }

        // ---------------------------------------------------------
        //  (Binding Properties)
        // ---------------------------------------------------------

        public IEnumerable<DrawingParameter> AvailableParameters =>
            Enum.GetValues(typeof(DrawingParameter)).Cast<DrawingParameter>();

        public DrawingParameter ThicknessParam
        {
            get => _thicknessParam;
            set
            {
                if (_thicknessParam != value)
                {
                    _thicknessParam = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ThicknessDescription));
                    OnPropertyChanged(nameof(IsThicknessNone));
                }
            }
        }

        public DrawingParameter ColorParam
        {
            get => _colorParam;
            set
            {
                if (_colorParam != value)
                {
                    _colorParam = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(ColorDescription));
                    OnPropertyChanged(nameof(IsColorNone));
                }
            }
        }

        public bool IsDarkMode
        {
            get => _isDarkMode;
            set
            {
                if (_isDarkMode != value)
                {
                    _isDarkMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PrimaryColorBrush));
                }
            }
        }

        public bool ShowGrid
        {
            get => _showGrid;
            set { _showGrid = value; OnPropertyChanged(); }
        }

        public bool IsPlaying
        {
            get => _isPlaying;
            set { _isPlaying = value; OnPropertyChanged(); }
        }

        // Zoom
        public double Scale { get => _scale; set { _scale = value; OnPropertyChanged(); } }
        public double OffsetX { get => _offsetX; set { _offsetX = value; OnPropertyChanged(); } }
        public double OffsetY { get => _offsetY; set { _offsetY = value; OnPropertyChanged(); } }

        // ---------------------------------------------------------
        // Legends
        // ---------------------------------------------------------

        public bool IsThicknessNone => ThicknessParam.ToString() == "None";
        public bool IsColorNone => ColorParam.ToString() == "None";

        public Brush PrimaryColorBrush => IsDarkMode ? Brushes.White : Brushes.Black;

        public string ThicknessDescription
        {
            get
            {
                switch (ThicknessParam)
                {
                    case DrawingParameter.Pressure: return "Line becomes thicker as pen pressure increases.";
                    case DrawingParameter.Altitude: return "Line becomes thicker as the pen becomes more upright (vertical).";
                    case DrawingParameter.Azimuth: return "Line thickness changes with pen direction.";
                    default: return "";
                }
            }
        }

        public string ColorDescription
        {
            get
            {
                switch (ColorParam)
                {
                    case DrawingParameter.Pressure:
                        return "Green indicates light touch, Red indicates heavy pressure.";
                    case DrawingParameter.Altitude:
                        return "Purple is low angle (flat), Orange is high angle (upright).";
                    case DrawingParameter.Azimuth:
                        return "Color indicates pen orientation (where the tail points). Red is Left, Cyan is Right.";
                    default: return "";
                }
            }
        }

        // ---------------------------------------------------------
        //  Sidebar and Analyzes
        // ---------------------------------------------------------

        public bool IsSidebarVisible
        {
            get => _isSidebarVisible;
            set { _isSidebarVisible = value; OnPropertyChanged(); }
        }

        public bool IsAnalyzing
        {
            get => _isAnalyzing;
            set { _isAnalyzing = value; OnPropertyChanged(); }
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        public string EnsembleDiagnosis
        {
            get => _ensembleDiagnosis;
            set { _ensembleDiagnosis = value; OnPropertyChanged(); }
        }

        public double EnsembleProbability
        {
            get => _ensembleProbability;
            set { _ensembleProbability = value; OnPropertyChanged(); }
        }

        public Brush EnsembleColor
        {
            get => _ensembleColor;
            set { _ensembleColor = value; OnPropertyChanged(); }
        }

        public ObservableCollection<ModelResultItem> AnalysisResults
        {
            get => _analysisResults;
            set { _analysisResults = value; OnPropertyChanged(); }
        }

        // ---------------------------------------------------------
        //  (Commands)
        // ---------------------------------------------------------

        private ICommand _playPauseCommand;
        public ICommand PlayPauseCommand => _playPauseCommand ??= new RelayCommand(_ =>
        {
            if (IsPlaying) PauseAnimation();
            else StartAnimation();
        });

        private ICommand _resetAnimationCommand;
        public ICommand ResetAnimationCommand => _resetAnimationCommand ??= new RelayCommand(_ =>
        {
            PauseAnimation();
            Session.Reset();
            _stopwatch.Reset();
            _lastElapsedSeconds = 0;
        });

        private ICommand _resetZoomCommand;
        public ICommand ResetZoomCommand => _resetZoomCommand ??= new RelayCommand(_ =>
        {
            Scale = 1.0;
            OffsetX = 0;
            OffsetY = 0;
        });

        private ICommand _toggleSidebarCommand;
        public ICommand ToggleSidebarCommand => _toggleSidebarCommand ??= new RelayCommand(_ =>
        {
            IsSidebarVisible = !IsSidebarVisible;
        });

        private ICommand _runAnalysisCommand;
        public ICommand RunAnalysisCommand => _runAnalysisCommand ??= new RelayCommand(async _ => await RunAnalysisAsync());

        private ICommand _clearAnalysisCommand;
        public ICommand ClearAnalysisCommand => _clearAnalysisCommand ??= new RelayCommand(_ =>
        {
            AnalysisResults = null;
            EnsembleDiagnosis = null;
            ErrorMessage = null;
        });

        // ---------------------------------------------------------
        // Animatiom
        // ---------------------------------------------------------

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (Session.IsFinished)
            {
                PauseAnimation();
                return;
            }

            double currentSeconds = _stopwatch.Elapsed.TotalSeconds;
            double delta = currentSeconds - _lastElapsedSeconds;
            _lastElapsedSeconds = currentSeconds;

            Session.AdvanceFrame(delta);
        }

        private void StartAnimation()
        {
            if (Session.IsFinished)
            {
                Session.Reset();
                _stopwatch.Reset();
                _lastElapsedSeconds = 0;
            }

            IsPlaying = true;
            if (!_stopwatch.IsRunning)
            {
                _stopwatch.Start();
                _lastElapsedSeconds = _stopwatch.Elapsed.TotalSeconds;
            }
            _timer.Start();
        }

        private void PauseAnimation()
        {
            IsPlaying = false;
            _timer.Stop();
            _stopwatch.Stop();
        }

        // ---------------------------------------------------------
        // (ML / ONNX)
        // ---------------------------------------------------------

        private async Task RunAnalysisAsync()
        {
            if (IsAnalyzing) return;

            IsAnalyzing = true;
            ErrorMessage = null;
            AnalysisResults = new ObservableCollection<ModelResultItem>();
            EnsembleDiagnosis = null;

            try
            {
                var extractionResult = await Task.Run(() => _featureExtractor.ExtractFeatures(Session.DataFile));

                if (extractionResult.Features == null || extractionResult.Features.All(f => f == 0))
                {
                    ErrorMessage = "Unable to extract features. Drawing might be too short.";
                    IsAnalyzing = false;
                    return;
                }

                string modelsDir = GetModelsDirectory();

                if (string.IsNullOrEmpty(modelsDir) || !Directory.Exists(modelsDir))
                {
                    ErrorMessage = $"Models folder 'ModelsONNX' not found!\nSearched in:\n{AppDomain.CurrentDomain.BaseDirectory}\nand DataDirectory.";
                    IsAnalyzing = false;
                    return;
                }

                var modelFiles = Directory.GetFiles(modelsDir, "*.onnx");
                if (modelFiles.Length == 0)
                {
                    ErrorMessage = $"No .onnx models found in:\n{modelsDir}";
                    IsAnalyzing = false;
                    return;
                }

                var tempResults = new List<ModelResultItem>();

                await Task.Run(() =>
                {
                    foreach (var modelPath in modelFiles)
                    {
                        double prob = _onnxService.RunInference(modelPath, extractionResult.Features);
                        string name = Path.GetFileNameWithoutExtension(modelPath);

                        tempResults.Add(new ModelResultItem
                        {
                            ModelName = name,
                            Probability = prob
                        });
                    }
                });

                if (tempResults.Any())
                {
                    double avgProb = tempResults.Average(x => x.Probability);

                    EnsembleProbability = avgProb;
                    EnsembleDiagnosis = avgProb >= 50.0 ? "PARKINSON" : "HEALTHY";
                    EnsembleColor = avgProb >= 50.0 ? Brushes.Red : Brushes.Green;

                    AnalysisResults = new ObservableCollection<ModelResultItem>(tempResults);
                }
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Analysis Failed: {ex.Message}";
            }
            finally
            {
                IsAnalyzing = false;
            }
        }

        private string GetModelsDirectory()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            string path = Path.Combine(basePath, "ModelsONNX");
            if (Directory.Exists(path)) return path;

            string dataDir = AppDomain.CurrentDomain.GetData("DataDirectory") as string;
            if (!string.IsNullOrEmpty(dataDir))
            {
                path = Path.Combine(dataDir, "ModelsONNX");
                if (Directory.Exists(path)) return path;
            }

            string upOne = Path.Combine(basePath, "..", "ModelsONNX");
            if (Directory.Exists(upOne)) return Path.GetFullPath(upOne);

            return null;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}