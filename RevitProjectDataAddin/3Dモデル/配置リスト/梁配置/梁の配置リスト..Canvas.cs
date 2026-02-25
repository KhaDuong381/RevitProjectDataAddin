using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RevitProjectDataAddin
{
    public partial class 梁の配置リスト : Window
    {
        private void DrawIllustration3(Canvas canvas, string left, string right, string G0, string 上側のズレ寸法, string 下側のズレ寸法, string 梁の段差)
        {
            // Xóa canvas trước khi vẽ
            canvas.Children.Clear();
            double canvasWidth = 900;  // Tổng chiều rộng cửa sổ
            double canvasHeight = 300; // Tổng chiều cao cửa sổ

            double totalLength = 6500;  // Kích thước chiều dài thực tế
            double scale = canvasWidth / totalLength; // Tính tỷ lệ thu nhỏ

            // Kích thước các phần theo hình mẫu
            double blockSize = 800;  // Kích thước khối vuông lớn
            double smallBlockSize = 300;  // Kích thước khối nhỏ bên trong
            double connectorHeight = blockSize * 0.8; // Chiều cao phần nối
            double centerY = canvasHeight / 2;

            // Chuyển đổi kích thước theo tỷ lệ
            double blockWidth = blockSize * scale;
            double smallBlockWidth = smallBlockSize * scale;
            double connectorWidth = (totalLength / 2.2 - 2 * blockSize) * scale;// Chiều dài phần nối
            double connectorHeightScaled = connectorHeight * scale; // Chiều cao phần nối

            double startX = 100;
            double endX = startX + connectorWidth + blockWidth;

            // Vẽ 2 khối vuông lớn (màu xám)
            DrawRectangle(canvas, startX, centerY - blockWidth / 2, blockWidth, blockWidth, Brushes.DarkGray);
            DrawRectangle(canvas, endX, centerY - blockWidth / 2, blockWidth, blockWidth, Brushes.DarkGray);

            // Vẽ 2 khối nhỏ (màu be)
            DrawRectangle(canvas, startX + (blockWidth - smallBlockWidth) / 2, centerY - smallBlockWidth / 2, smallBlockWidth, smallBlockWidth, Brushes.Beige);
            DrawRectangle(canvas, endX + (blockWidth - smallBlockWidth) / 2, centerY - smallBlockWidth / 2, smallBlockWidth, smallBlockWidth, Brushes.Beige);

            // Vẽ phần nối (màu xám đậm)
            DrawRectangle(canvas, startX + blockWidth, centerY - connectorHeightScaled / 2, connectorWidth, connectorHeightScaled, Brushes.Gray);

            // Vẽ đường kích thước (màu xanh)
            double dimY = centerY - blockWidth / 2 - 20; // Vị trí đường kích thước
            double dimStartX = startX + blockWidth / 2; // Điểm đầu nằm giữa khối lớn
            double dimEndX = endX + blockWidth / 2; // Điểm cuối nằm giữa khối lớn

            // Vẽ đường tâm (màu đỏ, đứt nét)
            DrawLine(canvas, startX - 20, centerY, endX + blockWidth + 20, centerY, Brushes.Red, 1, new DoubleCollection { 4, 4 });

            // Vẽ hai đường gạch xanh dọc trên hai hình vuông
            DrawLine(canvas, dimStartX, centerY - blockWidth / 2 - 25, dimStartX, centerY + blockWidth / 2 + 25, Brushes.Blue, 2, new DoubleCollection { 2, 2 });
            DrawLine(canvas, dimEndX, centerY - blockWidth / 2 - 25, dimEndX, centerY + blockWidth / 2 + 25, Brushes.Blue, 2, new DoubleCollection { 2, 2 });

            // Centered text for left and right
            DrawText(canvas, $"{left}", dimStartX - 2, dimY - 30, Brushes.Red, 20, HorizontalAlignment.Center);
            DrawText(canvas, $"{right}", dimEndX - 2, dimY - 30, Brushes.Red, 20, HorizontalAlignment.Center);

            // Right-aligned text for 通を選択ComboBox.SelectedItem
            DrawText(canvas, $"{通を選択ComboBox.SelectedItem}", (dimStartX + dimEndX) / 2 - 210, dimY + 50, Brushes.Black, 20, HorizontalAlignment.Right);

            // Other texts (keep as before)
            DrawText(canvas, $"{G0}", (dimStartX + dimEndX) / 2 - 80, dimY + 48, Brushes.Black, 20, HorizontalAlignment.Left);
            DrawText(canvas, $"{梁の段差}", (dimStartX + dimEndX) / 2 - 80, dimY + 5, Brushes.Blue, 15, HorizontalAlignment.Left);
            DrawText(canvas, $"{上側のズレ寸法}", (dimStartX + dimEndX) / 2 + 10, dimY + 45, Brushes.Yellow, 15, HorizontalAlignment.Left);
            DrawText(canvas, $"{下側のズレ寸法}", (dimStartX + dimEndX) / 2 + 10, dimY + 85, Brushes.Blue, 15, HorizontalAlignment.Left);
        }

        private void DrawRectangle(Canvas canvas, double x, double y, double width, double height, Brush color)
        {
            Rectangle rect = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = color,
                Stroke = Brushes.Black,
                StrokeThickness = 0
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            canvas.Children.Add(rect);
        }

        private void DrawLine(Canvas canvas, double x1, double y1, double x2, double y2, Brush color, double thickness, DoubleCollection dashArray = null)
        {
            Line line = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = color,
                StrokeThickness = thickness
            };
            if (dashArray != null)
            {
                line.StrokeDashArray = dashArray;
            }
            canvas.Children.Add(line);
        }

        // Updated DrawText with alignment
        private void DrawText(Canvas canvas, string text, double x, double y, Brush color, int fontSize, HorizontalAlignment alignment = HorizontalAlignment.Left)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = fontSize,
                FontWeight = FontWeights.Bold
            };

            // Measure text width
            var formattedText = new FormattedText(
                text ?? "",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(textBlock.FontFamily, textBlock.FontStyle, textBlock.FontWeight, textBlock.FontStretch),
                fontSize,
                color,
                VisualTreeHelper.GetDpi(canvas).PixelsPerDip
            );

            double textWidth = formattedText.Width;

            // Adjust X based on alignment
            double drawX = x;
            if (alignment == HorizontalAlignment.Center)
                drawX = x - textWidth / 2;
            else if (alignment == HorizontalAlignment.Right)
                drawX = x - textWidth;

            Canvas.SetLeft(textBlock, drawX);
            Canvas.SetTop(textBlock, y);
            canvas.Children.Add(textBlock);
        }
    }
}
