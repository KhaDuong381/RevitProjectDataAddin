using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RevitProjectDataAddin
{
    public partial class 基本入力 : Window
    {

        private void DrawIllustration1(Canvas canvas, int spanValue, string nameCanvas1, string nameCanvas2)
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
            DrawLine(canvas, dimStartX - 15, dimY, dimEndX + 15, dimY, Brushes.Blue, 2);

            // Vẽ đường tâm (màu đỏ, đứt nét)
            DrawLine(canvas, startX - 20, centerY, endX + blockWidth + 20, centerY, Brushes.Red, 1, new DoubleCollection { 4, 4 });

            // Vẽ hai đường gạch xanh dọc trên hai hình vuông
            DrawLine(canvas, dimStartX, centerY - blockWidth / 2 - 25, dimStartX, centerY + blockWidth / 2 + 25, Brushes.Blue, 2, new DoubleCollection { 4, 4 });
            DrawLine(canvas, dimEndX, centerY - blockWidth / 2 - 25, dimEndX, centerY + blockWidth / 2 + 25, Brushes.Blue, 2, new DoubleCollection { 4, 4 });

            // Vị trí chính xác của X1, X2 dựa trên blockSize
            DrawText(canvas, $"{nameCanvas1}", dimStartX - 10, dimY - 30, Brushes.Red);
            DrawText(canvas, $"{nameCanvas2}", dimEndX - 10, dimY - 30, Brushes.Red);

            // Ghi chú kích thước tổng (6500)
            DrawText(canvas, spanValue.ToString(), (dimStartX + dimEndX) / 2 - 20, dimY - 20, Brushes.Black);

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

        private void DrawText(Canvas canvas, string text, double x, double y, Brush color)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = 14,
                FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(textBlock, x);
            Canvas.SetTop(textBlock, y);
            canvas.Children.Add(textBlock);
        }
    }
}
