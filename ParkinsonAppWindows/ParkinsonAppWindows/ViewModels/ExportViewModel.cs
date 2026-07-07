using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging; //RenderAndSave
using Microsoft.Win32;
using ParkinsonAppWindows.Models;
using ParkinsonAppWindows.Services;
using ParkinsonAppWindows.Controls; // DrawingCanvas в RenderBitmap

namespace ParkinsonAppWindows.ViewModels
{


    // "Export Series"
    public class ExportViewItem : INotifyPropertyChanged
    {
        private DrawingParameter _thicknessParam;
        private DrawingParameter _colorParam;

        public SessionViewModel Session { get; }

        public ExportViewItem(SessionViewModel session, DrawingParameter thickness, DrawingParameter color)
        {
            Session = session;
            ThicknessParam = thickness;
            ColorParam = color;
        }

        public DrawingParameter ThicknessParam
        {
            get => _thicknessParam;
            set { _thicknessParam = value; OnPropertyChanged(); }
        }
        public DrawingParameter ColorParam
        {
            get => _colorParam;
            set { _colorParam = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // "Model Analysis"
    public class PredictionModelItem : INotifyPropertyChanged
    {
        private bool _isSelected = true;
        private double _probability;
        public string Name { get; set; }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
        public double Probability
        {
            get => _probability;
            set { _probability = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    // --- MAIN VIEWMODEL ---

    public class ExportViewModel : INotifyPropertyChanged
    {
        public SessionViewModel Session { get; }

        private readonly FeatureExtractor _extractor;
        private readonly OnnxService _onnxService;

        // --- Series ---
        public ObservableCollection<ExportViewItem> ExportItems { get; set; } = new ObservableCollection<ExportViewItem>();

        private bool _isDarkMode;
        public bool IsDarkMode
        {
            get => _isDarkMode;
            set { _isDarkMode = value; OnPropertyChanged(); }
        }

        private bool _showGrid = true;
        public bool ShowGrid
        {
            get => _showGrid;
            set { _showGrid = value; OnPropertyChanged(); }
        }

        // --- Analysis ---
        public ObservableCollection<PredictionModelItem> AnalysisModels { get; set; } = new ObservableCollection<PredictionModelItem>();

        private DrawingParameter _singleThicknessParam = DrawingParameter.Pressure;
        private DrawingParameter _singleColorParam = DrawingParameter.Azimuth;

        public DrawingParameter SingleThicknessParam
        {
            get => _singleThicknessParam;
            set { _singleThicknessParam = value; OnPropertyChanged(); }
        }
        public DrawingParameter SingleColorParam
        {
            get => _singleColorParam;
            set { _singleColorParam = value; OnPropertyChanged(); }
        }

        // Statistics
        private string _statDuration = "0.0s";
        private string _statPressure = "0.0";
        private string _statAltitude = "0.0";
        private string _statAzimuth = "0.0";
        private string _finalDiagnosis = "NOT ANALYZED";
        private string _finalAvg = "0%";
        private Brush _diagnosisColor = Brushes.Gray;

        public string StatDuration { get => _statDuration; set { _statDuration = value; OnPropertyChanged(); } }
        public string StatPressure { get => _statPressure; set { _statPressure = value; OnPropertyChanged(); } }
        public string StatAltitude { get => _statAltitude; set { _statAltitude = value; OnPropertyChanged(); } }
        public string StatAzimuth { get => _statAzimuth; set { _statAzimuth = value; OnPropertyChanged(); } }

        public string FinalDiagnosis { get => _finalDiagnosis; set { _finalDiagnosis = value; OnPropertyChanged(); } }
        public string FinalAvg { get => _finalAvg; set { _finalAvg = value; OnPropertyChanged(); } }
        public Brush DiagnosisColor { get => _diagnosisColor; set { _diagnosisColor = value; OnPropertyChanged(); } }

        // ComboBox
        public IEnumerable<DrawingParameter> AvailableParameters =>
            Enum.GetValues(typeof(DrawingParameter)).Cast<DrawingParameter>();


        public ExportViewModel(SessionViewModel session)
        {
            Session = session;

            _extractor = new FeatureExtractor();
            _onnxService = new OnnxService();

            AddView();

            LoadModels();
        }


        // ====================================================================
        // EXPORT SERIES 
        // ====================================================================

        private ICommand _addViewCommand;
        public ICommand AddViewCommand => _addViewCommand ??= new RelayCommand(_ => AddView());

        private ICommand _removeViewCommand;
        public ICommand RemoveViewCommand => _removeViewCommand ??= new RelayCommand(param =>
        {
            if (param is ExportViewItem item) ExportItems.Remove(item);
        });

        private ICommand _exportAllCommand;
        public ICommand ExportAllCommand => _exportAllCommand ??= new RelayCommand(ExportAll);

        private ICommand _exportCommand;
        public ICommand ExportCommand => _exportCommand ??= new RelayCommand(RenderAndSave);

        private void AddView() => ExportItems.Add(new ExportViewItem(Session, DrawingParameter.Pressure, DrawingParameter.Azimuth));

        private void ExportAll(object param)
        {
            if (ExportItems.Count == 0) return;

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.FileName = $"Parkinson_Multi_{DateTime.Now:yyyyMMdd_HHmm}";
            dlg.Filter = "SVG Vector (.svg)|*.svg|PDF Document (.pdf)|*.pdf|PNG Image (.png)|*.png|JPEG Image (.jpg)|*.jpg";

            if (dlg.ShowDialog() == true)
            {
                string ext = Path.GetExtension(dlg.FileName).ToLower();

                try
                {
                    if (ext == ".svg")
                    {
                        SvgExporter.ExportAllToSvg(ExportItems, dlg.FileName, IsDarkMode, ShowGrid);
                    }
                    else if (ext == ".pdf")
                    {
                        SimplePdfExporter.ExportAll(ExportItems, dlg.FileName, IsDarkMode, ShowGrid);
                    }
                    else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg")
                    {
                        BitmapExporter.ExportAll(ExportItems, dlg.FileName, ext, IsDarkMode, ShowGrid);
                    }
                    MessageBox.Show("Exported successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        private void RenderAndSave(object parameter)
        {
            if (parameter == null) return;
            if (!(parameter is FrameworkElement element)) return;
            if (!(element.DataContext is ExportViewItem viewItem)) return;

            SaveFileDialog dlg = new SaveFileDialog();
            dlg.FileName = $"{Session.FileName}_Graph";
            dlg.Filter = "SVG Vector (.svg)|*.svg|PDF Document (.pdf)|*.pdf|PNG Image (.png)|*.png|JPEG Image (.jpg)|*.jpg";
            dlg.DefaultExt = ".png";

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    string ext = Path.GetExtension(dlg.FileName).ToLower();

                    if (ext == ".svg")
                    {
                        SvgExporter.ExportToSvg(Session.DrawingPoints, Session.Stats, dlg.FileName, viewItem.ThicknessParam, viewItem.ColorParam, IsDarkMode, ShowGrid, Session.FileName);
                    }
                    else if (ext == ".pdf")
                    {
                        SimplePdfExporter.Export(Session.DrawingPoints, Session.Stats, dlg.FileName, viewItem.ThicknessParam, viewItem.ColorParam, IsDarkMode, ShowGrid, Session.FileName);
                    }
                    else 
                    {
                        RenderBitmap(dlg.FileName, ext, viewItem);
                    }

                    MessageBox.Show($"File saved: {dlg.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void RenderBitmap(string filePath, string ext, ExportViewItem viewItem)
        {
            int width = 2048;
            int height = 1536;

            var drawingCanvas = new DrawingCanvas
            {
                Width = width,
                Height = height,
                FullPoints = Session.DrawingPoints,
                Limit = Session.DrawingPoints.Count,
                ThicknessParam = viewItem.ThicknessParam,
                ColorParam = viewItem.ColorParam,
                IsDarkMode = IsDarkMode,
                ShowGrid = ShowGrid
            };

            // Layout pass
            drawingCanvas.Measure(new Size(width, height));
            drawingCanvas.Arrange(new Rect(new Size(width, height)));
            drawingCanvas.UpdateLayout();

            RenderTargetBitmap rtb = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);

            DrawingVisual backgroundVisual = new DrawingVisual();
            using (DrawingContext ctx = backgroundVisual.RenderOpen())
            {
                ctx.DrawRectangle(IsDarkMode ? Brushes.Black : Brushes.White, null, new Rect(0, 0, width, height));
            }
            rtb.Render(backgroundVisual);
            rtb.Render(drawingCanvas); 

            BitmapEncoder encoder = (ext == ".jpg" || ext == ".jpeg") ? (BitmapEncoder)new JpegBitmapEncoder() : new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));

            using (var fs = File.Open(filePath, FileMode.Create))
            {
                encoder.Save(fs);
            }
        }


        // ====================================================================
        // MODEL ANALYSIS 
        // ====================================================================

        private ICommand _calculateCommand;
        public ICommand CalculateCommand => _calculateCommand ??= new RelayCommand(obj => RunAnalysis());

        private void LoadModels()
        {
            try
            {
                string modelsDir = GetModelsDirectory();

                if (!string.IsNullOrEmpty(modelsDir) && Directory.Exists(modelsDir))
                {
                    var files = Directory.GetFiles(modelsDir, "*.onnx");
                    foreach (var file in files)
                    {
                        AnalysisModels.Add(new PredictionModelItem
                        {
                            Name = Path.GetFileNameWithoutExtension(file),
                            IsSelected = true,
                            Probability = 0
                        });
                    }
                }

                if (AnalysisModels.Count == 0)
                {
                    AnalysisModels.Add(new PredictionModelItem { Name = "Models not found in " + (modelsDir ?? "null"), IsSelected = false });
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading models: {ex.Message}");
            }
        }

        private void RunAnalysis()
        {
            if (Session.DrawingPoints == null || Session.DrawingPoints.Count < 2)
            {
                MessageBox.Show("Not enough data points to analyze.");
                return;
            }

            try
            {
                var tempData = new DrawingDataFile
                {
                    Data = new List<List<DrawingPoint>> { new List<DrawingPoint>(Session.DrawingPoints) }
                };

                var result = _extractor.ExtractFeatures(tempData);

                StatDuration = $"{result.Duration:F2} s";
                StatPressure = $"{result.MeanPressure:F3}";
                StatAltitude = $"{result.MeanAltitude:F3}";
                StatAzimuth = $"{result.MeanAzimuth:F3}";

               
                string modelsDir = GetModelsDirectory();

                if (string.IsNullOrEmpty(modelsDir) || !Directory.Exists(modelsDir))
                {
                    MessageBox.Show("Models directory not found during analysis.");
                    return;
                }

                List<double> probabilities = new List<double>();

                foreach (var model in AnalysisModels)
                {
                    if (model.IsSelected)
                    {
                        string path = Path.Combine(modelsDir, model.Name + ".onnx");
                        if (File.Exists(path))
                        {
                            double p = _onnxService.RunInference(path, result.Features);
                            model.Probability = p;
                            probabilities.Add(p);
                        }
                    }
                    else
                    {
                        model.Probability = 0;
                    }
                }

                if (probabilities.Count > 0)
                {
                    double avg = probabilities.Average();
                    FinalAvg = $"{avg:F1}%";

                    if (avg > 50.0)
                    {
                        FinalDiagnosis = "PARKINSON DETECTED";
                        DiagnosisColor = Brushes.Red;
                    }
                    else
                    {
                        FinalDiagnosis = "PARKINSON NOT DETECTED";
                        DiagnosisColor = Brushes.Green;
                    }
                }
                else
                {
                    FinalDiagnosis = "NO MODELS SELECTED";
                    FinalAvg = "0%";
                    DiagnosisColor = Brushes.Gray;
                }

                var temp = new List<PredictionModelItem>(AnalysisModels);
                AnalysisModels.Clear();
                foreach (var item in temp) AnalysisModels.Add(item);

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Analysis failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}