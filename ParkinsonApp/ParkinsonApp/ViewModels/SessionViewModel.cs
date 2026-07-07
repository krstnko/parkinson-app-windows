using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using ParkinsonAppWindows.Models;

namespace ParkinsonAppWindows.ViewModels
{
    public class SessionViewModel : INotifyPropertyChanged
    {
        private readonly DrawingDataFile _dataFile;

        private List<DrawingPoint> _drawingPoints;
        private int _currentIndex;
        private bool _isFinished = true;
        private bool _isSelected;
        private DrawingStats _stats;

        public SessionViewModel(DrawingDataFile file, List<DrawingPoint> points)
        {
            _dataFile = file; 
            FileName = file?.FileName ?? "Unknown";

            DrawingPoints = points;

            _currentIndex = points != null ? points.Count : 0;

            Stats = DrawingStats.Calculate(points);
        }
        public DrawingDataFile DataFile => _dataFile;

        public string FileName { get; }

        public List<DrawingPoint> DrawingPoints
        {
            get => _drawingPoints;
            set { _drawingPoints = value; OnPropertyChanged(); }
        }

        public DrawingStats Stats
        {
            get => _stats;
            set { _stats = value; OnPropertyChanged(); }
        }

        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                _currentIndex = value;
                OnPropertyChanged();

                // Проверяем, закончена ли анимация
                if (DrawingPoints != null)
                {
                    IsFinished = _currentIndex >= DrawingPoints.Count;
                }
            }
        }

        public bool IsFinished
        {
            get => _isFinished;
            set { _isFinished = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public void AdvanceFrame(double seconds)
        {
            if (IsFinished || DrawingPoints == null) return;

            int pointsToAdd = (int)(seconds * 140);
            if (pointsToAdd < 1) pointsToAdd = 1;

            int newIndex = CurrentIndex + pointsToAdd;
            if (newIndex >= DrawingPoints.Count)
            {
                newIndex = DrawingPoints.Count;
            }

            CurrentIndex = newIndex;
        }

        public void Reset()
        {
            CurrentIndex = 0;
            IsFinished = false;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}