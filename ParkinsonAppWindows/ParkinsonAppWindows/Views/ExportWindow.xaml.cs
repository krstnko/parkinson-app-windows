using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using ParkinsonAppWindows.ViewModels;

namespace ParkinsonAppWindows.Views
{
    public partial class ExportWindow : Window
    {
        public ExportWindow(ExportViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>
        /// Handler for "Save Result Image" button in Analysis Tab.
        /// Captures the right panel (Drawing + Percentages) and saves it to a file.
        /// </summary>
        private void OnExportAnalysisClick(object sender, RoutedEventArgs e)
        {
            // Check if format is selected
            if (AnalysisFormatCombo.SelectedItem is ComboBoxItem item)
            {
                string format = item.Content.ToString();
                SaveAnalysisResult(format);
            }
        }

        private void SaveAnalysisResult(string format)
        {
            // Configure save dialog
            SaveFileDialog dlg = new SaveFileDialog();
            dlg.FileName = $"Analysis_Result_{DateTime.Now:yyyyMMdd_HHmm}";

            switch (format)
            {
                case "PNG":
                    dlg.Filter = "PNG Image|*.png";
                    dlg.DefaultExt = ".png";
                    break;
                case "JPEG":
                    dlg.Filter = "JPEG Image|*.jpg";
                    dlg.DefaultExt = ".jpg";
                    break;
                case "PDF":
                    // Note: Native WPF cannot save Vector PDF from VisualTree easily.
                    // Saving as Image-based PDF for compatibility.
                    dlg.Filter = "PDF Document (Image-based)|*.pdf";
                    dlg.DefaultExt = ".pdf";
                    break;
                default:
                    dlg.Filter = "PNG Image|*.png";
                    break;
            }

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // 1. Find the element to capture (from XAML x:Name="CaptureGrid")
                    FrameworkElement elementToCapture = CaptureGrid;

                    if (elementToCapture == null)
                    {
                        MessageBox.Show("Error: Capture area (CaptureGrid) not found.");
                        return;
                    }

                    // Get dimensions
                    double dpi = 96d;
                    int width = (int)elementToCapture.ActualWidth;
                    int height = (int)elementToCapture.ActualHeight;

                    // Ensure element is measured/arranged if not visible
                    if (width == 0 || height == 0)
                    {
                        elementToCapture.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        elementToCapture.Arrange(new Rect(new Size(elementToCapture.DesiredSize.Width, elementToCapture.DesiredSize.Height)));
                        width = (int)elementToCapture.ActualWidth;
                        height = (int)elementToCapture.ActualHeight;
                    }

                    // 2. Render UI to Bitmap
                    RenderTargetBitmap bmp = new RenderTargetBitmap(
                        width, height, dpi, dpi, PixelFormats.Pbgra32);

                    bmp.Render(elementToCapture);

                    // 3. Select Encoder
                    BitmapEncoder encoder;

                    if (format == "JPEG")
                    {
                        encoder = new JpegBitmapEncoder();
                    }
                    else
                    {
                        // Default to PNG (also used for PDF wrapper here)
                        encoder = new PngBitmapEncoder();
                    }

                    encoder.Frames.Add(BitmapFrame.Create(bmp));

                    // 4. Save to file
                    string finalPath = dlg.FileName;

                    using (var fs = File.Create(finalPath))
                    {
                        encoder.Save(fs);
                    }

                    MessageBox.Show($"File saved successfully:\n{finalPath}", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}