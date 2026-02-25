using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Xml.Linq;

namespace RevitProjectDataAddin
{
    public partial class 柱配置リスト : Window
    {
        private const double ScaleFactor = 1.4;
        private readonly SolidColorBrush ConcreteBrush = new SolidColorBrush(Colors.Gray);
        private readonly SolidColorBrush ConcreteBrush1 = new SolidColorBrush(Colors.LightGray);
        private readonly SolidColorBrush DimBrush = new SolidColorBrush(Colors.Blue);
        private readonly SolidColorBrush TextBrush = new SolidColorBrush(Colors.Red);
        private readonly SolidColorBrush DashedBrush = new SolidColorBrush(Colors.Black);
        private readonly SolidColorBrush DashedBrush1 = new SolidColorBrush(Colors.White);


        private Point ScaleAndOffsetPoint(double x, double y, double scaleFactor, double offsetX, double offsetY)
        {
            return new Point(x * scaleFactor + offsetX, y * scaleFactor + offsetY);
        }

        // 図形を描画するメイン関数
        private void DrawIlluskiso(Canvas targetCanvas, double offsetX, double offsetY)
        {
            offsetX = 250;
            offsetY = -50;
            if (targetCanvas == null) return;

            targetCanvas.Children.Clear();

            var haichi = targetCanvas.DataContext as 柱セグメント;
            if (haichi == null) return;
            DrawConcreteShapes(targetCanvas, offsetX, offsetY);
            DrawDimensionLines(targetCanvas, offsetX, offsetY);
            DrawTextLabels(targetCanvas, haichi, offsetX, offsetY);
            DrawDashedLines(targetCanvas, offsetX, offsetY);
        }

        // コンクリートの形状を描画
        private void DrawConcreteShapes(Canvas targetCanvas, double offsetX, double offsetY)
        {
            Polygon concrete1 = new Polygon
            {
                Fill = ConcreteBrush,
                Points = new PointCollection
                {
                    ScaleAndOffsetPoint(50, 50, ScaleFactor, offsetX, offsetY),
                    ScaleAndOffsetPoint(170, 50, ScaleFactor, offsetX, offsetY),
                    ScaleAndOffsetPoint(170, 170, ScaleFactor, offsetX, offsetY),
                    ScaleAndOffsetPoint(50, 170, ScaleFactor, offsetX, offsetY),
                }
            };

            Polygon concrete2 = new Polygon
            {
                Fill = ConcreteBrush1,
                Points = new PointCollection
                {
                    ScaleAndOffsetPoint(90, 90, ScaleFactor, offsetX, offsetY),
                    ScaleAndOffsetPoint(130, 90, ScaleFactor, offsetX, offsetY),
                    ScaleAndOffsetPoint(130, 130, ScaleFactor, offsetX, offsetY),
                    ScaleAndOffsetPoint(90, 130, ScaleFactor, offsetX, offsetY),
                }
            };

            targetCanvas.Children.Add(concrete1);
            targetCanvas.Children.Add(concrete2);
        }

        // 寸法線を描画
        private void DrawDimensionLines(Canvas targetCanvas, double offsetX, double offsetY)
        {
            var lines = new (double X1, double Y1, double X2, double Y2)[]
            {
                (50, 20, 50, 45),
                (170, 20, 170, 45),
                (20, 50, 45, 50),
                (20, 170, 45, 170),
                (47, 32.5, 173, 32.5),
                (32.5, 47, 32.5, 173)
            };

            foreach (var line in lines)
            {
                Line dimLine = new Line
                {
                    X1 = ScaleAndOffsetPoint(line.X1, line.Y1, ScaleFactor, offsetX, offsetY).X,
                    Y1 = ScaleAndOffsetPoint(line.X1, line.Y1, ScaleFactor, offsetX, offsetY).Y,
                    X2 = ScaleAndOffsetPoint(line.X2, line.Y2, ScaleFactor, offsetX, offsetY).X,
                    Y2 = ScaleAndOffsetPoint(line.X2, line.Y2, ScaleFactor, offsetX, offsetY).Y,
                    Stroke = DimBrush,
                    StrokeThickness = 1 * ScaleFactor
                };
                targetCanvas.Children.Add(dimLine);
            }
        }


        // テキストラベルを描画
        private void DrawTextLabels(Canvas targetCanvas, 柱セグメント haichi, double offsetX, double offsetY)
        {
            // Center point for 柱の符号
            var center = ScaleAndOffsetPoint(110, 108, ScaleFactor, offsetX, offsetY);

            // Font settings
            var fontFamily = new FontFamily("Segoe UI");
            var fontWeight = FontWeights.Bold;

            // List of labels to center
            var centerLabels = new[]
            {
                new { Text = haichi.左側のズレ, X = 77.0, Y = 15.0, Brush = (Brush)TextBrush, FontSize = 0.0 },
                new { Text = haichi.右側のズレ, X = 140.0, Y = 15.0, Brush = (Brush)TextBrush, FontSize = 0.0 },
                new { Text = $"{通を選択ComboBox.SelectedItem}", X = 110.0, Y = 5.0, Brush = (Brush)DashedBrush, FontSize = 0.0 }
            };

            // List of labels to left-align
            var leftLabels = new[]
            {
                new { Text = haichi.上側のズレ, X = 30.0, Y = 68.0, Brush = (Brush)TextBrush, FontSize = 0.0 },
                new { Text = haichi.下側のズレ, X = 30.0, Y = 128.0, Brush = (Brush)TextBrush, FontSize = 0.0 },
                new { Text = haichi.位置表示?.Split('-').Last(), X = 18.0, Y = 100.0, Brush = (Brush)DashedBrush, FontSize = 0.0 }
            };

            // Draw centered labels
            foreach (var label in centerLabels)
            {
                double fontSize = (label.FontSize == 0 ? 12 : label.FontSize) * ScaleFactor;
                var formattedText = new FormattedText(
                    label.Text ?? "",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(fontFamily, FontStyles.Normal, fontWeight, FontStretches.Normal),
                    fontSize,
                    label.Brush,
                    VisualTreeHelper.GetDpi(targetCanvas).PixelsPerDip
                );

                var pt = ScaleAndOffsetPoint(label.X, label.Y, ScaleFactor, offsetX, offsetY);
                TextBlock textBlock = new TextBlock
                {
                    Text = label.Text,
                    Foreground = label.Brush,
                    FontWeight = fontWeight,
                    FontSize = fontSize
                };
                // Center horizontally
                Canvas.SetLeft(textBlock, pt.X - formattedText.Width / 2);
                Canvas.SetTop(textBlock, pt.Y);
                targetCanvas.Children.Add(textBlock);
            }

            // Draw right-aligned labels (chữ chạy về phía bên trái)
            foreach (var label in leftLabels)
            {
                double fontSize = (label.FontSize == 0 ? 12 : label.FontSize) * ScaleFactor;
                var formattedText = new FormattedText(
                    label.Text ?? "",
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(fontFamily, FontStyles.Normal, fontWeight, FontStretches.Normal),
                    fontSize,
                    label.Brush,
                    VisualTreeHelper.GetDpi(targetCanvas).PixelsPerDip
                );

                var pt = ScaleAndOffsetPoint(label.X, label.Y, ScaleFactor, offsetX, offsetY);
                TextBlock textBlock = new TextBlock
                {
                    Text = label.Text,
                    Foreground = label.Brush,
                    FontWeight = fontWeight,
                    FontSize = fontSize
                };
                // Right align: chữ sẽ dồn từ phải sang trái
                Canvas.SetLeft(textBlock, pt.X - formattedText.Width);
                Canvas.SetTop(textBlock, pt.Y);
                targetCanvas.Children.Add(textBlock);
            }

            // Center 柱の符号 at the intersection
            var fontSizeCenter = 20.0 * ScaleFactor;
            var formattedCenterText = new FormattedText(
                haichi.柱の符号 ?? "",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontFamily, FontStyles.Normal, fontWeight, FontStretches.Normal),
                fontSizeCenter,
                DashedBrush,
                VisualTreeHelper.GetDpi(targetCanvas).PixelsPerDip
            );

            TextBlock centerTextBlock = new TextBlock
            {
                Text = haichi.柱の符号,
                Foreground = DashedBrush,
                FontWeight = fontWeight,
                FontSize = fontSizeCenter
            };
            Canvas.SetLeft(centerTextBlock, center.X - formattedCenterText.Width / 2);
            Canvas.SetTop(centerTextBlock, center.Y - formattedCenterText.Height / 2);
            targetCanvas.Children.Add(centerTextBlock);
        }
        private void DrawDashedLines(Canvas targetCanvas, double offsetX, double offsetY)
        {
            Line dashedLine1 = new Line
            {
                X1 = ScaleAndOffsetPoint(20, 110, ScaleFactor, offsetX, offsetY).X,
                Y1 = ScaleAndOffsetPoint(20, 110, ScaleFactor, offsetX, offsetY).Y,
                X2 = ScaleAndOffsetPoint(190, 110, ScaleFactor, offsetX, offsetY).X,
                Y2 = ScaleAndOffsetPoint(200, 110, ScaleFactor, offsetX, offsetY).Y,
                Stroke = Brushes.Red,
                StrokeThickness = 1 * ScaleFactor,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };

            Line dashedLine2 = new Line
            {
                X1 = ScaleAndOffsetPoint(110, 20, ScaleFactor, offsetX, offsetY).X,
                Y1 = ScaleAndOffsetPoint(110, 20, ScaleFactor, offsetX, offsetY).Y,
                X2 = ScaleAndOffsetPoint(110, 200, ScaleFactor, offsetX, offsetY).X,
                Y2 = ScaleAndOffsetPoint(110, 190, ScaleFactor, offsetX, offsetY).Y,
                Stroke = Brushes.Blue,
                StrokeThickness = 1 * ScaleFactor,
                StrokeDashArray = new DoubleCollection { 4, 3 }
            };

            targetCanvas.Children.Add(dashedLine1);
            targetCanvas.Children.Add(dashedLine2);
        }
    }
}
