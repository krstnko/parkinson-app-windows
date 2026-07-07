using System.Windows;
using System.Windows.Input;
using ParkinsonAppWindows.ViewModels;

namespace ParkinsonAppWindows
{
    // Важно: partial означает, что этот код соединяется с XAML
    public partial class DetailWindow : Window
    {
        private DetailViewModel _vm;
        private Point _lastMousePosition;
        private bool _isDragging;

        // Тот самый конструктор, который программа не могла найти
        public DetailWindow(DetailViewModel vm)
        {
            InitializeComponent(); // Соединяет код с дизайном
            DataContext = vm;      // Привязывает данные
            _vm = vm;
        }

        // Логика Зума (Колесико мыши)
        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_vm == null) return;

            double zoomFactor = 1.1;
            if (e.Delta < 0) zoomFactor = 1.0 / zoomFactor;

            double newScale = _vm.Scale * zoomFactor;

            // Ограничения зума
            if (newScale < 0.5) newScale = 0.5;
            if (newScale > 15.0) newScale = 15.0;

            _vm.Scale = newScale;

            // Отключаем прокрутку самого ScrollViewer, чтобы работал зум
            e.Handled = true;
        }

        // Логика Перетаскивания (Нажатие мыши)
        private void OnMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                _lastMousePosition = e.GetPosition(this);
                _isDragging = true;
                this.Cursor = Cursors.SizeAll;
                ((UIElement)sender).CaptureMouse();
            }
        }

        // Логика Перетаскивания (Движение мыши)
        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point currentPosition = e.GetPosition(this);
                double deltaX = currentPosition.X - _lastMousePosition.X;
                double deltaY = currentPosition.Y - _lastMousePosition.Y;

                _vm.OffsetX += deltaX;
                _vm.OffsetY += deltaY;

                _lastMousePosition = currentPosition;
            }
        }

        // Логика Перетаскивания (Отпускание мыши)
        private void OnMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                this.Cursor = Cursors.Arrow;
                ((UIElement)sender).ReleaseMouseCapture();
            }
        }
    }
}