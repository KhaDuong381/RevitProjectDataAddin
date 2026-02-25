using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace RevitProjectDataAddin
{
    public partial class 計算値設定 : Window
    {
        private void DrawBeamDiagram(bool X1, bool X2, bool X3, bool X4)
        {
            myCanvas.Width = 400;
            myCanvas.Height = 300; // Increase the height to accommodate the vertical rectangle

            // Vẽ phần bên trái (dọc), màu xám nhạt
            Rectangle leftRect = new Rectangle
            {
                Width = 100,
                Height = 200,
                Fill = new SolidColorBrush(Color.FromRgb(211, 211, 211)), // Xám nhạt
                Stroke = Brushes.Black,
                StrokeThickness = 0
            };
            Canvas.SetLeft(leftRect, 0);
            Canvas.SetTop(leftRect, 0); // Adjust the top position to align with the bottom of the horizontal rectangle
            myCanvas.Children.Add(leftRect);

            // Vẽ phần bên phải (ngang), màu xám nhạt
            Rectangle rightRect = new Rectangle
            {
                Width = 265,
                Height = 150,
                Fill = new SolidColorBrush(Color.FromRgb(211, 211, 211)), // Xám nhạt
                Stroke = Brushes.Black,
                StrokeThickness = 0
            };
            Canvas.SetLeft(rightRect, 50);
            Canvas.SetTop(rightRect, 0); // Adjust the top position to align with the top of the canvas
            myCanvas.Children.Add(rightRect);

            if (X1 == false)
            {
                //// hang tren cung
                DrawQuarterCircle(myCanvas, 30, 23.5, 10, 180);   // Góc 180° (hướng trái)
                LineDoc(20, 23.5, 46.5);
                LineNgang(30, 13.5);
            }
            else
            {
                LineNgang(24.5, 13.5);
                RedDot(4);
            }

            if (X2 == false)
            {
                ////hang tren
                DrawQuarterCircle(myCanvas, 45, 42, 10, 180);   // Góc 180° (hướng trái)
                LineDoc(34.5, 42, 69.5);
                LineNgang(45, 32);
            }
            else
            {
                LineNgang(24.5, 32);
                RedDot(24);
            }

            if (X3 == false)
            {
                //// hang duoi:
                DrawQuarterCircle(myCanvas, 45, 110, 10, 90);    // Góc 90° (hướng lên)
                LineDoc(34.5, 87, 110);
                LineNgang(45, 120);
            }
            else
            {
                LineNgang(24.5, 120);
                RedDot(110.5);
            }

            if (X4 == false)
            {
                ////hang duoi cung
                DrawQuarterCircle(myCanvas, 30, 128.5, 10, 90);    // Góc 90° (hướng lên)
                LineDoc(20, 105.5, 128.5);
                LineNgang(30, 138.5);
            }
            else
            {
                LineNgang(24.5, 138.5);
                RedDot(130);
            }
        }

        private void LineNgang(double x, double y)
        {
            //x: 30 || 40
            //y: 13.5, 138.5 || 32, 120
            Line line = new Line
            {
                X1 = x,
                X2 = 315,
                Y1 = y,
                Y2 = y,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            myCanvas.Children.Add(line);
        }
        private void LineDoc(double x, double y1, double y2)
        {
            Line line = new Line
            {
                X1 = x,
                X2 = x,
                Y1 = y1,
                Y2 = y2,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            myCanvas.Children.Add(line);
        }
        private void DrawQuarterCircle(Canvas canvas, double centerX, double centerY, double radius, double startAngle)
        {
            Path path = new Path
            {
                Stroke = Brushes.Black,       // Màu đỏ để dễ phân biệt
                StrokeThickness = 2
            };
            //x1 0 1,x2 0 1,x3 0 1,x4 0 1 // 0000,1000,01000, ... 01111,1111

            // Tính toán góc bắt đầu và kết thúc cho 1/4 đường tròn (90 độ)
            double startAngleRad = startAngle * Math.PI / 180;
            double endAngleRad = (startAngle + 90) * Math.PI / 180;

            // Tính toán điểm bắt đầu và kết thúc của cung
            Point startPoint = new Point(
                centerX + radius * Math.Cos(startAngleRad),
                centerY + radius * Math.Sin(startAngleRad));
            Point endPoint = new Point(
                centerX + radius * Math.Cos(endAngleRad),
                centerY + radius * Math.Sin(endAngleRad));

            // Tạo PathGeometry cho cung tròn
            PathGeometry geometry = new PathGeometry();
            PathFigure figure = new PathFigure { StartPoint = startPoint };
            ArcSegment arc = new ArcSegment
            {
                Point = endPoint,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise, // Vẽ theo chiều kim đồng hồ
                IsLargeArc = false // Không phải cung lớn (dưới 180 độ)
            };
            figure.Segments.Add(arc);
            geometry.Figures.Add(figure);
            path.Data = geometry;

            canvas.Children.Add(path);
        }

        private void RedDot(double x)// 4, 22.5, 110.5, 130
        {
            Rectangle redDot = new Rectangle
            {
                Width = 8,
                Height = 17.5,
                Fill = Brushes.Red,
                Stroke = Brushes.Red,
                StrokeThickness = 0
            };
            Canvas.SetLeft(redDot, 17);
            Canvas.SetTop(redDot, x);
            myCanvas.Children.Add(redDot);
        }

        private Point ScalePoint(double x, double y, double scaleFactor)
        {
            return new Point(x * scaleFactor, y * scaleFactor);
        }

        private void DrawBeamDiagram()
        {
            if (MainCanvas == null) return;
            MainCanvas.Children.Clear();

            // Khởi tạo các cọ màu
            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush dimBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Blue);

            double scaleFactor = 2; // Hệ số tỉ lệ
            double offsetY = -60;     // Chỉ số Di chuyển khối bê tông ở phía dưới
            double offsetX1 = 0;      // Di chuyển toàn bộ hình ảnh
            double offsetY1 = -150;      // Di chuyển toàn bộ hình ảnh

            // Hàm ScalePoint được điều chỉnh để thêm offsetX1 và offsetY1
            Point ScalePoint(double x, double y, double scale) => new Point(x * scale + offsetX1, y * scale + offsetY1);

            // Vẽ hình đa giác cho bê tông
            Polygon concrete1 = new Polygon
            {
                Fill = concreteBrush,
                Points = new PointCollection
                {
                    ScalePoint(50, 80, scaleFactor),
                    ScalePoint(100, 80, scaleFactor),
                    ScalePoint(180, 80, scaleFactor),
                    ScalePoint(180, 120, scaleFactor),
                    ScalePoint(100, 120, scaleFactor),
                    ScalePoint(100, 150, scaleFactor),
                    ScalePoint(50, 150, scaleFactor)
                }
            };

            Polygon concrete2 = new Polygon
            {
                Fill = concreteBrush,
                Points = new PointCollection
                {
                    ScalePoint(50, 200 + offsetY, scaleFactor),
                    ScalePoint(100, 200 + offsetY, scaleFactor),
                    ScalePoint(100, 230 + offsetY, scaleFactor),
                    ScalePoint(180, 230 + offsetY, scaleFactor),
                    ScalePoint(180, 270 + offsetY, scaleFactor),
                    ScalePoint(100, 270 + offsetY, scaleFactor),
                    ScalePoint(100, 300 + offsetY, scaleFactor),
                    ScalePoint(50, 300 + offsetY, scaleFactor)
                }
            };

            // Vẽ các đường thẳng và đường cong cho thép
            LineGeometry line1 = new LineGeometry(ScalePoint(60, 92, scaleFactor), ScalePoint(60, 120, scaleFactor));
            LineGeometry line2 = new LineGeometry(ScalePoint(62, 90, scaleFactor), ScalePoint(181, 90, scaleFactor));

            PathFigure arcFigure1 = new PathFigure { StartPoint = ScalePoint(60, 92, scaleFactor) };
            arcFigure1.Segments.Add(new ArcSegment(ScalePoint(62, 90, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc1 = new PathGeometry();
            arc1.Figures.Add(arcFigure1);

            GeometryGroup steelGeometry1 = new GeometryGroup();
            steelGeometry1.Children.Add(line1);
            steelGeometry1.Children.Add(line2);
            steelGeometry1.Children.Add(arc1);

            Path steel1 = new Path
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1.5 * scaleFactor,
                Data = steelGeometry1
            };

            // Vẽ các đường kích thước
            Line dim1 = new Line
            {
                X1 = ScalePoint(52, 90, scaleFactor).X,
                Y1 = ScalePoint(52, 90, scaleFactor).Y,
                X2 = ScalePoint(56, 90, scaleFactor).X,
                Y2 = ScalePoint(56, 90, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim2 = new Line
            {
                X1 = ScalePoint(52, 120, scaleFactor).X,
                Y1 = ScalePoint(52, 120, scaleFactor).Y,
                X2 = ScalePoint(56, 120, scaleFactor).X,
                Y2 = ScalePoint(56, 120, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim3 = new Line
            {
                X1 = ScalePoint(54, 90, scaleFactor).X,
                Y1 = ScalePoint(54, 90, scaleFactor).Y,
                X2 = ScalePoint(54, 120, scaleFactor).X,
                Y2 = ScalePoint(54, 120, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };

            TextBlock textBlock = new TextBlock
            {
                Text = AnkaNagaRFTextBox.Text + "d",
                Foreground = textBrush,
                FontSize = 7.5 * scaleFactor
            };
            textBlock.SizeChanged += (s, e) =>
            {
                Canvas.SetLeft(textBlock, ScalePoint(44, 100, scaleFactor).X - textBlock.ActualWidth);
            };
            Canvas.SetLeft(textBlock, ScalePoint(44, 100, scaleFactor).X - textBlock.Width);
            Canvas.SetTop(textBlock, ScalePoint(44, 100, scaleFactor).Y);

            //TextBlock textBlock2 = new TextBlock
            //{
            //    Text = "mm",
            //    Foreground = textBrush,
            //    FontSize = 7.5 * scaleFactor
            //};
            //Canvas.SetLeft(textBlock2, ScalePoint(39, 100, scaleFactor).X);
            //Canvas.SetTop(textBlock2, ScalePoint(39, 100, scaleFactor).Y);

            // Thêm các phần tử vào MainCanvas
            MainCanvas.Children.Add(concrete1);
            MainCanvas.Children.Add(concrete2);
            MainCanvas.Children.Add(steel1);
            MainCanvas.Children.Add(dim1);
            MainCanvas.Children.Add(dim2);
            MainCanvas.Children.Add(dim3);
            MainCanvas.Children.Add(textBlock);
            //MainCanvas.Children.Add(textBlock2);

            // Vẽ thêm các phần tử khi Option1 được chọn
            if (RadioButton1.IsChecked == true)
            {
                LineGeometry line3 = new LineGeometry(ScalePoint(60, 242 + offsetY, scaleFactor), ScalePoint(60, 260 + offsetY, scaleFactor));
                LineGeometry line4 = new LineGeometry(ScalePoint(62, 240 + offsetY, scaleFactor), ScalePoint(181, 240 + offsetY, scaleFactor));

                PathFigure arcFigure2 = new PathFigure { StartPoint = ScalePoint(60, 242 + offsetY, scaleFactor) };
                arcFigure2.Segments.Add(new ArcSegment(ScalePoint(62, 240 + offsetY, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
                PathGeometry arc2 = new PathGeometry();
                arc2.Figures.Add(arcFigure2);

                GeometryGroup steelGeometry2 = new GeometryGroup();
                steelGeometry2.Children.Add(line3);
                steelGeometry2.Children.Add(line4);
                steelGeometry2.Children.Add(arc2);

                Path steel2 = new Path
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 1.5 * scaleFactor,
                    Data = steelGeometry2
                };

                Line dim4 = new Line
                {
                    X1 = ScalePoint(52, 240 + offsetY, scaleFactor).X,
                    Y1 = ScalePoint(52, 240 + offsetY, scaleFactor).Y,
                    X2 = ScalePoint(56, 240 + offsetY, scaleFactor).X,
                    Y2 = ScalePoint(56, 240 + offsetY, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };
                Line dim5 = new Line
                {
                    X1 = ScalePoint(52, 260 + offsetY, scaleFactor).X,
                    Y1 = ScalePoint(52, 260 + offsetY, scaleFactor).Y,
                    X2 = ScalePoint(56, 260 + offsetY, scaleFactor).X,
                    Y2 = ScalePoint(56, 260 + offsetY, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };
                Line dim6 = new Line
                {
                    X1 = ScalePoint(54, 240 + offsetY, scaleFactor).X,
                    Y1 = ScalePoint(54, 240 + offsetY, scaleFactor).Y,
                    X2 = ScalePoint(54, 260 + offsetY, scaleFactor).X,
                    Y2 = ScalePoint(54, 260 + offsetY, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };

                TextBlock textBlock1 = new TextBlock
                {
                    Text = "アンカ長さ",
                    Foreground = textBrush,
                    FontSize = 7.5 * scaleFactor
                };
                Canvas.SetLeft(textBlock1, ScalePoint(15, 248 + offsetY, scaleFactor).X);
                Canvas.SetTop(textBlock1, ScalePoint(15, 248 + offsetY, scaleFactor).Y);

                MainCanvas.Children.Add(textBlock1);
                MainCanvas.Children.Add(steel2);
                MainCanvas.Children.Add(dim4);
                MainCanvas.Children.Add(dim5);
                MainCanvas.Children.Add(dim6);
            }
            else if (RadioButton2.IsChecked == true)
            {
                Path steel3 = new Path
                {
                    Stroke = steelBrush,
                    StrokeThickness = 1.5 * scaleFactor,
                    Data = new LineGeometry(ScalePoint(75, 240 + offsetY, scaleFactor), ScalePoint(181, 240 + offsetY, scaleFactor))
                };

                Line dim7 = new Line
                {
                    X1 = ScalePoint(75, 242 + offsetY, scaleFactor).X,
                    Y1 = ScalePoint(75, 242 + offsetY, scaleFactor).Y,
                    X2 = ScalePoint(75, 246 + offsetY, scaleFactor).X,
                    Y2 = ScalePoint(75, 246 + offsetY, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };
                Line dim8 = new Line
                {
                    X1 = ScalePoint(100, 242 + offsetY, scaleFactor).X,
                    Y1 = ScalePoint(100, 242 + offsetY, scaleFactor).Y,
                    X2 = ScalePoint(100, 246 + offsetY, scaleFactor).X,
                    Y2 = ScalePoint(100, 246 + offsetY, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };
                Line dim9 = new Line
                {
                    X1 = ScalePoint(75, 244 + offsetY, scaleFactor).X,
                    Y1 = ScalePoint(75, 244 + offsetY, scaleFactor).Y,
                    X2 = ScalePoint(100, 244 + offsetY, scaleFactor).X,
                    Y2 = ScalePoint(100, 244 + offsetY, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };

                TextBlock textBlock1 = new TextBlock
                {
                    Text = "定着長分",
                    Foreground = textBrush,
                    FontSize = 7.5 * scaleFactor
                };
                Canvas.SetLeft(textBlock1, ScalePoint(72, 248 + offsetY, scaleFactor).X);
                Canvas.SetTop(textBlock1, ScalePoint(72, 248 + offsetY, scaleFactor).Y);

                MainCanvas.Children.Add(steel3);
                MainCanvas.Children.Add(dim7);
                MainCanvas.Children.Add(dim8);
                MainCanvas.Children.Add(dim9);
                MainCanvas.Children.Add(textBlock1);
            }
        }
        private void DrawBeamDiagram1()
        {
            if (MainCanvas1 == null) return;

            MainCanvas1.Children.Clear();
            // Khởi tạo các cọ màu
            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);

            double scaleFactor = 1.4;
            // Vẽ hình đa giác cho bê tông
            Polygon concrete1 = new Polygon
            {
                Fill = concreteBrush,
                Points = new PointCollection
                {
                    ScalePoint(50, 85, scaleFactor),
                    ScalePoint(125, 85, scaleFactor),
                    ScalePoint(125, 50, scaleFactor),
                    ScalePoint(175, 50, scaleFactor),
                    ScalePoint(175, 85, scaleFactor),
                    ScalePoint(250, 85, scaleFactor),
                    ScalePoint(250, 140, scaleFactor),
                    ScalePoint(175, 140, scaleFactor),
                    ScalePoint(175, 160, scaleFactor),
                    ScalePoint(125, 160, scaleFactor),
                    ScalePoint(125, 150, scaleFactor),
                    ScalePoint(50, 150, scaleFactor)
                }
            };
            // Vẽ các đường thẳng cho thép
            LineGeometry line1 = new LineGeometry(ScalePoint(49, 90, scaleFactor), ScalePoint(251, 90, scaleFactor));
            LineGeometry line2dai = new LineGeometry(ScalePoint(49, 100, scaleFactor), ScalePoint(185, 100, scaleFactor));
            LineGeometry line3 = new LineGeometry(ScalePoint(80, 135, scaleFactor), ScalePoint(251, 135, scaleFactor));
            LineGeometry line4ngang = new LineGeometry(ScalePoint(49, 145, scaleFactor), ScalePoint(165, 145, scaleFactor));
            LineGeometry line2short = new LineGeometry(ScalePoint(49, 100, scaleFactor), ScalePoint(165, 100, scaleFactor));
            LineGeometry line3short = new LineGeometry(ScalePoint(135, 135, scaleFactor), ScalePoint(251, 135, scaleFactor));
            LineGeometry line4doc = new LineGeometry(ScalePoint(170, 161, scaleFactor), ScalePoint(170, 150, scaleFactor));
            LineGeometry line2doc = new LineGeometry(ScalePoint(170, 105, scaleFactor), ScalePoint(170, 115, scaleFactor));
            LineGeometry line3doc = new LineGeometry(ScalePoint(130, 140, scaleFactor), ScalePoint(130, 157, scaleFactor));
            // Vẽ các đường cong cho thép
            PathFigure arcFigure4 = new PathFigure { StartPoint = ScalePoint(165, 145, scaleFactor) };
            arcFigure4.Segments.Add(new ArcSegment(ScalePoint(170, 150, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc4 = new PathGeometry();
            arc4.Figures.Add(arcFigure4);

            PathFigure arcFigure2 = new PathFigure { StartPoint = ScalePoint(165, 100, scaleFactor) };
            arcFigure2.Segments.Add(new ArcSegment(ScalePoint(170, 105, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc2 = new PathGeometry();
            arc2.Figures.Add(arcFigure2);

            PathFigure arcFigure3 = new PathFigure { StartPoint = ScalePoint(130, 140, scaleFactor) };
            arcFigure3.Segments.Add(new ArcSegment(ScalePoint(135, 135, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc3 = new PathGeometry();
            arc3.Figures.Add(arcFigure3);
            // Vẽ đường thép
            GeometryGroup steelGeometry = new GeometryGroup();
            // Các line thay đổi
            if (harinaitei1.IsChecked == true)
            {
                steelGeometry.Children.Add(line2dai);
                steelGeometry.Children.Add(line3);
            }
            else if (harinaitei2.IsChecked == true)
            {
                steelGeometry.Children.Add(line2short);
                steelGeometry.Children.Add(line2doc);
                steelGeometry.Children.Add(arc2);
                steelGeometry.Children.Add(line3short);
                steelGeometry.Children.Add(line3doc);
                steelGeometry.Children.Add(arc3);
            }
            else if (harinaitei3.IsChecked == true)
            {
                steelGeometry.Children.Add(line2short);
                steelGeometry.Children.Add(line2doc);
                steelGeometry.Children.Add(arc2);
                steelGeometry.Children.Add(line3);
            }

            // Các line cố định
            steelGeometry.Children.Add(line1);
            steelGeometry.Children.Add(line4ngang);
            steelGeometry.Children.Add(line4doc);
            steelGeometry.Children.Add(arc4);
            // Vẽ các đường kích thước
            Path steelPath = new Path
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1.5 * scaleFactor,
                Data = steelGeometry
            };
            // Thêm các phần tử vào MainCanvas1
            MainCanvas1.Children.Add(concrete1);
            MainCanvas1.Children.Add(steelPath);
        }

        private void DrawBeamDiagram2()
        {
            if (MainCanvas2 == null) return;

            MainCanvas2.Children.Clear();

            // Định nghĩa các cọ vẽ
            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush dimBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Blue);

            double scaleFactor = 1.4;

            // Vẽ hình đa giác đại diện cho bê tông
            Polygon concrete1 = new Polygon
            {
                Fill = concreteBrush,
                Points = new PointCollection
                {
                    ScalePoint(50, 50, scaleFactor),
                    ScalePoint(125, 50, scaleFactor),
                    ScalePoint(125, 70, scaleFactor),
                    ScalePoint(250, 70, scaleFactor),
                    ScalePoint(250, 140, scaleFactor),
                    ScalePoint(125, 140, scaleFactor),
                    ScalePoint(125, 160, scaleFactor),
                    ScalePoint(50, 160, scaleFactor),
                }
            };

            // Vẽ các đường thẳng và cung tròn
            LineGeometry line1ngangshort = new LineGeometry(ScalePoint(92.5, 85, scaleFactor), ScalePoint(251, 85, scaleFactor));
            LineGeometry line1ngangdai = new LineGeometry(ScalePoint(75, 85, scaleFactor), ScalePoint(251, 85, scaleFactor));
            LineGeometry line1docshort = new LineGeometry(ScalePoint(87.5, 90, scaleFactor), ScalePoint(87.5, 130, scaleFactor));
            LineGeometry line1docdai = new LineGeometry(ScalePoint(70, 90, scaleFactor), ScalePoint(70, 120, scaleFactor));

            PathFigure arcFigure1 = new PathFigure { StartPoint = ScalePoint(87.5, 90, scaleFactor) };
            arcFigure1.Segments.Add(new ArcSegment(ScalePoint(92.5, 85, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc1 = new PathGeometry();
            arc1.Figures.Add(arcFigure1);

            PathFigure arcFigure2 = new PathFigure { StartPoint = ScalePoint(70, 90, scaleFactor) };
            arcFigure2.Segments.Add(new ArcSegment(ScalePoint(75, 85, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc2 = new PathGeometry();
            arc2.Figures.Add(arcFigure2);

            // Vẽ các đường kích thước
            Line dim1 = new Line
            {
                X1 = ScalePoint(50, 52, scaleFactor).X,
                Y1 = ScalePoint(50, 52, scaleFactor).Y,
                X2 = ScalePoint(50, 56, scaleFactor).X,
                Y2 = ScalePoint(50, 56, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim2 = new Line
            {
                X1 = ScalePoint(50, 54, scaleFactor).X,
                Y1 = ScalePoint(50, 54, scaleFactor).Y,
                X2 = ScalePoint(87.5, 54, scaleFactor).X,
                Y2 = ScalePoint(87.5, 54, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim3 = new Line
            {
                X1 = ScalePoint(87.5, 52, scaleFactor).X,
                Y1 = ScalePoint(87.5, 52, scaleFactor).Y,
                X2 = ScalePoint(87.5, 56, scaleFactor).X,
                Y2 = ScalePoint(87.5, 56, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };

            // Thêm các phần tử vào canvas
            MainCanvas2.Children.Add(concrete1);
            MainCanvas2.Children.Add(dim1);
            MainCanvas2.Children.Add(dim2);
            MainCanvas2.Children.Add(dim3);

            // Thêm các TextBlock
            TextBlock textBlock = new TextBlock
            {
                Text = "設定したにげ",
                Foreground = textBrush,
                FontSize = 10 * scaleFactor
            };
            Canvas.SetLeft(textBlock, ScalePoint(50, 35, scaleFactor).X);
            Canvas.SetTop(textBlock, ScalePoint(50, 35, scaleFactor).Y);

            TextBlock textBlock1 = new TextBlock
            {
                Text = "ノミコミ",
                Foreground = textBrush,
                FontSize = 10 * scaleFactor
            };
            Canvas.SetLeft(textBlock1, ScalePoint(120, 110, scaleFactor).X);
            Canvas.SetTop(textBlock1, ScalePoint(120, 110, scaleFactor).Y);

            // Nhóm các hình học thép
            GeometryGroup steelGeometry = new GeometryGroup();

            if (shukintei1.IsChecked == true)
            {
                Line dim4 = new Line
                {
                    X1 = ScalePoint(70, 100, scaleFactor).X,
                    Y1 = ScalePoint(70, 100, scaleFactor).Y,
                    X2 = ScalePoint(70, 104, scaleFactor).X,
                    Y2 = ScalePoint(70, 104, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };
                Line dim5 = new Line
                {
                    X1 = ScalePoint(70, 102, scaleFactor).X,
                    Y1 = ScalePoint(70, 102, scaleFactor).Y,
                    X2 = ScalePoint(125, 102, scaleFactor).X,
                    Y2 = ScalePoint(125, 102, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };
                Line dim6 = new Line
                {
                    X1 = ScalePoint(125, 100, scaleFactor).X,
                    Y1 = ScalePoint(125, 100, scaleFactor).Y,
                    X2 = ScalePoint(125, 104, scaleFactor).X,
                    Y2 = ScalePoint(125, 104, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };
                steelGeometry.Children.Add(line1ngangdai);
                steelGeometry.Children.Add(line1docdai);
                steelGeometry.Children.Add(arc2);
                MainCanvas2.Children.Add(dim4);
                MainCanvas2.Children.Add(dim5);
                MainCanvas2.Children.Add(dim6);
            }
            else if (shukintei2.IsChecked == true)
            {
                Line dim7 = new Line
                {
                    X1 = ScalePoint(87.5, 100, scaleFactor).X,
                    Y1 = ScalePoint(87.5, 100, scaleFactor).Y,
                    X2 = ScalePoint(87.5, 104, scaleFactor).X,
                    Y2 = ScalePoint(87.5, 104, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };
                Line dim8 = new Line
                {
                    X1 = ScalePoint(87.5, 102, scaleFactor).X,
                    Y1 = ScalePoint(87.5, 102, scaleFactor).Y,
                    X2 = ScalePoint(125, 102, scaleFactor).X,
                    Y2 = ScalePoint(125, 102, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };
                Line dim9 = new Line
                {
                    X1 = ScalePoint(125, 100, scaleFactor).X,
                    Y1 = ScalePoint(125, 100, scaleFactor).Y,
                    X2 = ScalePoint(125, 104, scaleFactor).X,
                    Y2 = ScalePoint(125, 104, scaleFactor).Y,
                    Stroke = dimBrush,
                    StrokeThickness = 1 * scaleFactor
                };
                steelGeometry.Children.Add(line1ngangshort);
                steelGeometry.Children.Add(line1docshort);
                steelGeometry.Children.Add(arc1);
                MainCanvas2.Children.Add(dim7);
                MainCanvas2.Children.Add(dim8);
                MainCanvas2.Children.Add(dim9);
            }

            // Thêm đường thép vào canvas
            Path steelPath = new Path
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1.5 * scaleFactor,
                Data = steelGeometry
            };

            // Thêm đường nét đứt
            Line dashedLine = new Line
            {
                X1 = ScalePoint(87.5, 50, scaleFactor).X,
                Y1 = ScalePoint(87.5, 50, scaleFactor).Y,
                X2 = ScalePoint(87.5, 160, scaleFactor).X,
                Y2 = ScalePoint(87.5, 160, scaleFactor).Y,
                Stroke = Brushes.Black,
                StrokeThickness = 0.75 * scaleFactor,
                StrokeDashArray = new DoubleCollection { 14, 6 }
            };

            // Thêm các phần tử vào canvas
            MainCanvas2.Children.Add(steelPath);
            MainCanvas2.Children.Add(dashedLine);
            MainCanvas2.Children.Add(textBlock);
            MainCanvas2.Children.Add(textBlock1);
        }
        private void DrawBeamDiagram3()
        {
            tanbutsuryoiki4.Children.Clear();
            tanbutsuryoiki4.Width = 400;
            tanbutsuryoiki4.Height = 300; // Increase the height to accommodate the vertical rectangle
                                          // Vẽ phần bên phải (ngang), màu xám nhạt
            Rectangle rightRect = new Rectangle
            {
                Width = 291,
                Height = 50,
                Fill = new SolidColorBrush(Color.FromRgb(211, 211, 211)), // Xám nhạt
                Stroke = Brushes.Black,
                StrokeThickness = 0
            };
            Canvas.SetLeft(rightRect, 50);
            Canvas.SetTop(rightRect, 0); // Adjust the top position to align with the top of the canvas
            tanbutsuryoiki4.Children.Add(rightRect); // Vẽ phần bên phải (ngang), màu xám nhạt

            Rectangle rightRect1 = new Rectangle
            {
                Width = 291,
                Height = 50,
                Fill = new SolidColorBrush(Color.FromRgb(211, 211, 211)), // Xám nhạt
                Stroke = Brushes.Black,
                StrokeThickness = 0
            };
            Canvas.SetLeft(rightRect1, 50);
            Canvas.SetTop(rightRect1, 124); // Adjust the top position to align with the top of the canvas
            tanbutsuryoiki4.Children.Add(rightRect1);

            // Vẽ phần bên trái (dọc), màu xám nhạt
            Rectangle leftRect = new Rectangle
            {
                Width = 50,
                Height = 225,
                Fill = new SolidColorBrush(Color.FromRgb(211, 211, 211)), // Xám nhạt
                Stroke = Brushes.Black,
                StrokeThickness = 0
            };
            Canvas.SetLeft(leftRect, 0);
            Canvas.SetTop(leftRect, 0); // Adjust the top position to align with the bottom of the horizontal rectangle
            tanbutsuryoiki4.Children.Add(leftRect);

            Rectangle leftRect1 = new Rectangle
            {
                Width = 50,
                Height = 225,
                Fill = new SolidColorBrush(Color.FromRgb(211, 211, 211)), // Xám nhạt
                Stroke = Brushes.Black,
                StrokeThickness = 0
            };
            Canvas.SetLeft(leftRect1, 266);
            Canvas.SetTop(leftRect1, 0); // Adjust the top position to align with the bottom of the horizontal rectangle
            tanbutsuryoiki4.Children.Add(leftRect1);


            BlackLine();
            PolyLine(0, 8);
            PolyLine(0, 132);
            if (tanbutsuryoiki1.IsChecked == true)
            {
                DotPoint(0, 0);
                DrawText(0, 0);
                RedLine(0, 0);

            }
            else if (tanbutsuryoiki2.IsChecked == true)
            {
                DotPoint(0, 45);
                DrawText(0, 45);
                RedLine(0, 45);
                Line(95, 95, 181, 189, Brushes.Red);
                Line(220, 220, 181, 189, Brushes.Red);
                Text("D", 70, 187, Brushes.Black);
                Text("D", 235, 187, Brushes.Black);
                Text("D", 274, 140, Brushes.Black);
                Line(270, 270, 126, 172, Brushes.Black);
                Line(266, 274, 126, 126, Brushes.Black);
                Line(266, 274, 172, 172, Brushes.Black);
            }
            else if (tanbutsuryoiki3.IsChecked == true)
            {
                DotPoint(45, 45);
                DrawText(45, 45);
                RedLine(45, 45);
                Line(95, 95, 181, 189, Brushes.Red);
                Line(220, 220, 181, 189, Brushes.Red);
                Text("D", 70, 187, Brushes.Black);
                Text("D", 235, 187, Brushes.Black);
                Text("D", 274, 140, Brushes.Black);
                Line(270, 270, 126, 172, Brushes.Black);
                Line(266, 274, 126, 126, Brushes.Black);
                Line(266, 274, 172, 172, Brushes.Black);
                Line(95, 95, 56, 64, Brushes.Red);
                Line(220, 220, 56, 64, Brushes.Red);
                Text("D", 70, 59, Brushes.Black);
                Text("D", 235, 59, Brushes.Black);
                Text("D", 274, 15, Brushes.Black);
                Line(270, 270, 0, 49, Brushes.Black);
                Line(266, 274, 0, 0, Brushes.Black);
                Line(266, 274, 49, 49, Brushes.Black);
            }
        }

        private void DotPoint(double j, double i)
        {
            var list = new List<(double x, double y)>()
            {
                (50+j,35),
                (211-j,35),
                (50+i,159),
                (211-i,159),
            };
            foreach (var (x, y) in list)
            {
                Rectangle point = new Rectangle
                {
                    Width = 54,
                    Height = 15,
                    Fill = Brushes.Blue,
                    Stroke = Brushes.Red,
                    StrokeThickness = 0
                };
                Canvas.SetLeft(point, x);
                Canvas.SetTop(point, y);
                tanbutsuryoiki4.Children.Add(point);
            }
        }
        private void PolyLine(double X, double Y)
        {
            Polygon polygon = new Polygon
            {
                Fill = Brushes.LightGray,
                Stroke = Brushes.LightGray,
                StrokeThickness = 0
            };
            var point = new List<(double x, double y)>()
            {
                (342, -20),
                (342, 10),
                (330, 15),
                (354, 20),
                (342, 25),
                (342, 55),
                (342, 25),
                (334, 20),
                (328, 15),
                (342, 10),
            };
            foreach (var (x, y) in point)
            {
                polygon.Points.Add(new Point(x + X, y + Y));
            }
            tanbutsuryoiki4.Children.Add(polygon);

            Polygon polygon1 = new Polygon
            {
                Fill = Brushes.White,
                Stroke = Brushes.White,
                StrokeThickness = 0
            };
            var point2 = new List<(double x, double y)>()
            {

                (342, 10),
                (330, 15),
                (354, 20),

            };
            foreach (var (x, y) in point2)
            {
                polygon1.Points.Add(new Point(x + X, y + Y));
            }
            tanbutsuryoiki4.Children.Add(polygon1);

            Polyline polyline = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            var point1 = new List<(double x, double y)>()
            {
                (342, -20),
                (342, 10),
                (330, 15),
                (354, 20),
                (342, 25),
                (342, 55)
            };
            foreach (var (x, y) in point1)
            {
                polyline.Points.Add(new Point(x + X, y + Y));
            }
            tanbutsuryoiki4.Children.Add(polyline);
        }
        private void DrawText(double j, double i)
        {

            var text = new List<(string a, double x, double y)>()
            {
                ("L", 155, -40),
                ("L", 155, 87),

                ("L/4",65+j,59),
                ("L/4",230-j,59),
                ("L/4",65+i,187),
                ("L/4",230-i,187),
            };
            foreach (var (a, x, y) in text)
            {
                Text(a, x, y, Brushes.Black);
            }
        }
        private void BlackLine()
        {
            var listLine = new List<(double x1, double x2, double y1, double y2)>()
            {
                (50,266,-18,-18),
                (50,50,-12,-24),
                (266,266,-12,-24),

                (50,266,110,110),
                (50,50,106,116),
                (266,266,106,116),
            };
            foreach (var (x1, x2, y1, y2) in listLine)
            {
                Line(x1, x2, y1, y2, Brushes.Black);
            }


        }
        private void Text(string text, double x, double y, Brush color)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = 14,
            };
            Canvas.SetLeft(textBlock, x);
            Canvas.SetTop(textBlock, y);
            tanbutsuryoiki4.Children.Add(textBlock);
        }
        private void RedLine(double j, double i)
        {

            var listLine = new List<(double x1, double x2, double y1, double y2)>()
            {
                (50, 103+j, 60, 60),
                (50, 50, 56, 64),
                (103+j, 103+j, 56, 64),

                (210 - j, 265, 60, 60),
                (210-j, 210-j, 56, 64),
                (265, 265, 56, 64),

                (50, 103 + i, 185, 185),
                (50 , 50, 181, 189),
                (103 + i, 103 + i, 181, 189),

                (210-i, 265, 185, 185),
                (210-i, 210-i, 181, 189),
                (265, 265, 181, 189),


            };
            foreach (var (x1, x2, y1, y2) in listLine)
            {
                Line(x1, x2, y1, y2, Brushes.Red);
            }
        }
        private void Line(double x1, double x2, double y1, double y2, Brush strokeColor)
        {
            Line line = new Line
            {
                X1 = x1,
                X2 = x2,
                Y1 = y1,
                Y2 = y2,
                Stroke = strokeColor,
                StrokeThickness = 1
            };
            tanbutsuryoiki4.Children.Add(line);
        }
        private void DrawBeamDiagram4()
        {
            if (MainCanvas3 == null) return;

            MainCanvas3.Children.Clear();

            // Định nghĩa các cọ vẽ
            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush dimBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Blue);

            double scaleFactor = 1.4;

            // Vẽ hình đa giác đại diện cho bê tông
            Polygon concrete1 = new Polygon
            {
                Fill = concreteBrush,
                Points = new PointCollection
                {
                    ScalePoint(50, 50, scaleFactor), ScalePoint(210, 50, scaleFactor), ScalePoint(210, 60, scaleFactor),
                    ScalePoint(205, 68, scaleFactor), ScalePoint(215, 72, scaleFactor), ScalePoint(210, 80, scaleFactor),
                    ScalePoint(210, 90, scaleFactor), ScalePoint(150, 90, scaleFactor), ScalePoint(150, 140, scaleFactor),
                    ScalePoint(210, 140, scaleFactor), ScalePoint(210, 150, scaleFactor), ScalePoint(205, 158, scaleFactor),
                    ScalePoint(215, 162, scaleFactor), ScalePoint(210, 170, scaleFactor), ScalePoint(210, 180, scaleFactor),
                    ScalePoint(150, 180, scaleFactor), ScalePoint(150, 210, scaleFactor), ScalePoint(110, 210, scaleFactor),
                    ScalePoint(110, 180, scaleFactor), ScalePoint(50, 180, scaleFactor), ScalePoint(50, 170, scaleFactor),
                    ScalePoint(55, 162, scaleFactor), ScalePoint(45, 158, scaleFactor), ScalePoint(50, 150, scaleFactor),
                    ScalePoint(50, 140, scaleFactor), ScalePoint(110, 140, scaleFactor), ScalePoint(110, 90, scaleFactor),
                    ScalePoint(50, 90, scaleFactor), ScalePoint(50, 80, scaleFactor), ScalePoint(55, 72, scaleFactor),
                    ScalePoint(45, 68, scaleFactor), ScalePoint(50, 60, scaleFactor),
                }
            };

            // Vẽ các thanh thép trường hợp 15d
            LineGeometry line1 = new LineGeometry(ScalePoint(50, 55, scaleFactor), ScalePoint(170, 55, scaleFactor));
            LineGeometry line2 = new LineGeometry(ScalePoint(50, 145, scaleFactor), ScalePoint(170, 145, scaleFactor));
            // Vẽ các thanh thép trường hợp 10d　定着寸法
            LineGeometry line3 = new LineGeometry(ScalePoint(50, 55, scaleFactor), ScalePoint(155, 55, scaleFactor));
            LineGeometry line4 = new LineGeometry(ScalePoint(50, 145, scaleFactor), ScalePoint(155, 145, scaleFactor));

            //Vẽ các đường zitzac trái trên
            PointCollection zitzac1 = new PointCollection
            {
                ScalePoint(50, 40, scaleFactor),
                ScalePoint(50, 60, scaleFactor),
                ScalePoint(45, 68, scaleFactor),
                ScalePoint(55, 72, scaleFactor),
                ScalePoint(50, 80, scaleFactor),
                ScalePoint(50, 100, scaleFactor),
            };

            //Vẽ các đường zitzac phải trên
            PointCollection zitzac2 = new PointCollection
            {
                ScalePoint(210, 40, scaleFactor),
                ScalePoint(210, 60, scaleFactor),
                ScalePoint(205, 68, scaleFactor),
                ScalePoint(215, 72, scaleFactor),
                ScalePoint(210, 80, scaleFactor),
                ScalePoint(210, 100, scaleFactor),
            };

            //Vẽ các đường zitzac trái dưới
            PointCollection zitzac3 = new PointCollection
            {
                ScalePoint(50, 130, scaleFactor),
                ScalePoint(50, 150, scaleFactor),
                ScalePoint(45, 158, scaleFactor),
                ScalePoint(55, 162, scaleFactor),
                ScalePoint(50, 170, scaleFactor),
                ScalePoint(50, 190, scaleFactor),
            };

            //Vẽ các đường zitzac phải dưới
            PointCollection zitzac4 = new PointCollection
            {
                ScalePoint(210, 130, scaleFactor),
                ScalePoint(210, 150, scaleFactor),
                ScalePoint(205, 158, scaleFactor),
                ScalePoint(215, 162, scaleFactor),
                ScalePoint(210, 170, scaleFactor),
                ScalePoint(210, 190, scaleFactor),
            };

            Polyline polyline1 = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 0.7 * scaleFactor,
                Points = zitzac1
            };

            Polyline polyline2 = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 0.7 * scaleFactor,
                Points = zitzac2
            };

            Polyline polyline3 = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 0.7 * scaleFactor,
                Points = zitzac3
            };

            Polyline polyline4 = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 0.7 * scaleFactor,
                Points = zitzac4
            };

            // Vẽ các đường kích thước 15d trên (dim1,dim2,dim3)
            Line dim1 = new Line
            {
                X1 = ScalePoint(150, 50, scaleFactor).X,
                Y1 = ScalePoint(150, 50, scaleFactor).Y,
                X2 = ScalePoint(150, 54, scaleFactor).X,
                Y2 = ScalePoint(150, 54, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim2 = new Line
            {
                X1 = ScalePoint(150, 52, scaleFactor).X,
                Y1 = ScalePoint(150, 52, scaleFactor).Y,
                X2 = ScalePoint(170, 52, scaleFactor).X,
                Y2 = ScalePoint(170, 52, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim3 = new Line
            {
                X1 = ScalePoint(170, 50, scaleFactor).X,
                Y1 = ScalePoint(170, 50, scaleFactor).Y,
                X2 = ScalePoint(170, 54, scaleFactor).X,
                Y2 = ScalePoint(170, 54, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            // Vẽ các đường kích thước 15d dưới (dim4,dim5,dim6)
            Line dim4 = new Line
            {
                X1 = ScalePoint(150, 140, scaleFactor).X,
                Y1 = ScalePoint(150, 140, scaleFactor).Y,
                X2 = ScalePoint(150, 144, scaleFactor).X,
                Y2 = ScalePoint(150, 144, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim5 = new Line
            {
                X1 = ScalePoint(150, 142, scaleFactor).X,
                Y1 = ScalePoint(150, 142, scaleFactor).Y,
                X2 = ScalePoint(170, 142, scaleFactor).X,
                Y2 = ScalePoint(170, 142, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim6 = new Line
            {
                X1 = ScalePoint(170, 140, scaleFactor).X,
                Y1 = ScalePoint(170, 140, scaleFactor).Y,
                X2 = ScalePoint(170, 144, scaleFactor).X,
                Y2 = ScalePoint(170, 144, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            // Vẽ các đường kích thước 定着寸法 trên (dim7,dim8,dim9)
            Line dim7 = new Line
            {
                X1 = ScalePoint(110, 56, scaleFactor).X,
                Y1 = ScalePoint(110, 56, scaleFactor).Y,
                X2 = ScalePoint(110, 60, scaleFactor).X,
                Y2 = ScalePoint(110, 60, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim8 = new Line
            {
                X1 = ScalePoint(110, 58, scaleFactor).X,
                Y1 = ScalePoint(110, 58, scaleFactor).Y,
                X2 = ScalePoint(155, 58, scaleFactor).X,
                Y2 = ScalePoint(155, 58, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim9 = new Line
            {
                X1 = ScalePoint(155, 56, scaleFactor).X,
                Y1 = ScalePoint(155, 56, scaleFactor).Y,
                X2 = ScalePoint(155, 60, scaleFactor).X,
                Y2 = ScalePoint(155, 60, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };

            // Vẽ các đường kích thước 定着寸法 dưới (dim10,dim11,dim12)
            Line dim10 = new Line
            {
                X1 = ScalePoint(110, 146, scaleFactor).X,
                Y1 = ScalePoint(110, 146, scaleFactor).Y,
                X2 = ScalePoint(110, 150, scaleFactor).X,
                Y2 = ScalePoint(110, 150, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim11 = new Line
            {
                X1 = ScalePoint(110, 148, scaleFactor).X,
                Y1 = ScalePoint(110, 148, scaleFactor).Y,
                X2 = ScalePoint(155, 148, scaleFactor).X,
                Y2 = ScalePoint(155, 148, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim12 = new Line
            {
                X1 = ScalePoint(155, 146, scaleFactor).X,
                Y1 = ScalePoint(155, 146, scaleFactor).Y,
                X2 = ScalePoint(155, 150, scaleFactor).X,
                Y2 = ScalePoint(155, 150, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };

            // Thêm các phần tử vào canvas
            MainCanvas3.Children.Add(concrete1);
            MainCanvas3.Children.Add(polyline1);
            MainCanvas3.Children.Add(polyline2);
            MainCanvas3.Children.Add(polyline3);
            MainCanvas3.Children.Add(polyline4);

            // Thêm các TextBlock 15d
            TextBlock textBlock = new TextBlock
            {
                Text = "15d",
                Foreground = textBrush,
                FontSize = 8 * scaleFactor
            };
            Canvas.SetLeft(textBlock, ScalePoint(152, 40, scaleFactor).X);
            Canvas.SetTop(textBlock, ScalePoint(152, 40, scaleFactor).Y);

            TextBlock textBlock1 = new TextBlock
            {
                Text = "15d",
                Foreground = textBrush,
                FontSize = 8 * scaleFactor
            };
            Canvas.SetLeft(textBlock1, ScalePoint(152, 130, scaleFactor).X);
            Canvas.SetTop(textBlock1, ScalePoint(152, 130, scaleFactor).Y);

            // Thêm các TextBlock 定着寸法
            TextBlock textBlock2 = new TextBlock
            {
                Text = "定着寸法",
                Foreground = textBrush,
                FontSize = 8 * scaleFactor
            };
            Canvas.SetLeft(textBlock2, ScalePoint(115, 60, scaleFactor).X);
            Canvas.SetTop(textBlock2, ScalePoint(115, 60, scaleFactor).Y);

            TextBlock textBlock3 = new TextBlock
            {
                Text = "定着寸法",
                Foreground = textBrush,
                FontSize = 8 * scaleFactor
            };
            Canvas.SetLeft(textBlock3, ScalePoint(115, 150, scaleFactor).X);
            Canvas.SetTop(textBlock3, ScalePoint(115, 150, scaleFactor).Y);

            // Nhóm các hình học thép
            GeometryGroup steelGeometry = new GeometryGroup();

            // Thêm đường thép vào canvas
            Path steelPath = new Path
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1.5 * scaleFactor,
                Data = steelGeometry
            };


            if (harinainaga1.IsChecked == true)
            {
                steelGeometry.Children.Add(line1);
                steelGeometry.Children.Add(line2);
                MainCanvas3.Children.Add(dim1);
                MainCanvas3.Children.Add(dim2);
                MainCanvas3.Children.Add(dim3);
                MainCanvas3.Children.Add(dim4);
                MainCanvas3.Children.Add(dim5);
                MainCanvas3.Children.Add(dim6);
                MainCanvas3.Children.Add(textBlock);
                MainCanvas3.Children.Add(textBlock1);
            }
            else if (harinainaga2.IsChecked == true)
            {
                steelGeometry.Children.Add(line3);
                steelGeometry.Children.Add(line4);
                MainCanvas3.Children.Add(dim7);
                MainCanvas3.Children.Add(dim8);
                MainCanvas3.Children.Add(dim9);
                MainCanvas3.Children.Add(dim10);
                MainCanvas3.Children.Add(dim11);
                MainCanvas3.Children.Add(dim12);
                MainCanvas3.Children.Add(textBlock2);
                MainCanvas3.Children.Add(textBlock3);
            }

            else if (harinainaga3.IsChecked == true)
            {
                steelGeometry.Children.Add(line1);
                steelGeometry.Children.Add(line4);
                MainCanvas3.Children.Add(dim1);
                MainCanvas3.Children.Add(dim2);
                MainCanvas3.Children.Add(dim3);
                MainCanvas3.Children.Add(dim10);
                MainCanvas3.Children.Add(dim11);
                MainCanvas3.Children.Add(dim12);
                MainCanvas3.Children.Add(textBlock);
                MainCanvas3.Children.Add(textBlock3);
            }

            // Thêm đường nét đứt trái trên          
            Line dashedLine1 = new Line
            {
                X1 = ScalePoint(110, 50, scaleFactor).X,
                Y1 = ScalePoint(110, 50, scaleFactor).Y,
                X2 = ScalePoint(110, 90, scaleFactor).X,
                Y2 = ScalePoint(110, 90, scaleFactor).Y,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5 * scaleFactor,
                StrokeDashArray = new DoubleCollection { 8, 2 }
            };
            // Thêm đường nét đứt phải trên
            Line dashedLine2 = new Line
            {
                X1 = ScalePoint(150, 50, scaleFactor).X,
                Y1 = ScalePoint(150, 50, scaleFactor).Y,
                X2 = ScalePoint(150, 90, scaleFactor).X,
                Y2 = ScalePoint(150, 90, scaleFactor).Y,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5 * scaleFactor,
                StrokeDashArray = new DoubleCollection { 8, 2 }
            };
            // Thêm đường nét đứt trái dưới
            Line dashedLine3 = new Line
            {
                X1 = ScalePoint(110, 140, scaleFactor).X,
                Y1 = ScalePoint(110, 140, scaleFactor).Y,
                X2 = ScalePoint(110, 180, scaleFactor).X,
                Y2 = ScalePoint(110, 180, scaleFactor).Y,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5 * scaleFactor,
                StrokeDashArray = new DoubleCollection { 8, 2 }
            };
            // Thêm đường nét đứt phải dưới
            Line dashedLine4 = new Line
            {
                X1 = ScalePoint(150, 140, scaleFactor).X,
                Y1 = ScalePoint(150, 140, scaleFactor).Y,
                X2 = ScalePoint(150, 180, scaleFactor).X,
                Y2 = ScalePoint(150, 180, scaleFactor).Y,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5 * scaleFactor,
                StrokeDashArray = new DoubleCollection { 8, 2 }
            };

            MainCanvas3.Children.Add(steelPath);
            MainCanvas3.Children.Add(dashedLine1);
            MainCanvas3.Children.Add(dashedLine2);
            MainCanvas3.Children.Add(dashedLine3);
            MainCanvas3.Children.Add(dashedLine4);
        }
        private void DrawBeamDiagram5()
        {
            if (MainCanvas4 == null) return;

            MainCanvas4.Children.Clear();

            // Định nghĩa các cọ vẽ
            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush dimBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Blue);

            double scaleFactor = 1.4;

            // Vẽ hình đa giác đại diện cho bê tông
            Polygon concrete1 = new Polygon
            {
                Fill = concreteBrush,
                Points = new PointCollection
                {
                    ScalePoint(50, 90, scaleFactor), ScalePoint(130, 90, scaleFactor), ScalePoint(130, 50, scaleFactor),
                    ScalePoint(185, 50, scaleFactor), ScalePoint(185, 100, scaleFactor), ScalePoint(250, 100, scaleFactor),
                    ScalePoint(250, 120, scaleFactor), ScalePoint(245, 130, scaleFactor), ScalePoint(255, 140, scaleFactor),
                    ScalePoint(250, 150, scaleFactor), ScalePoint(250, 170, scaleFactor), ScalePoint(185, 170, scaleFactor),
                    ScalePoint(185, 210, scaleFactor), ScalePoint(130, 210, scaleFactor), ScalePoint(130, 160, scaleFactor),
                    ScalePoint(50, 160, scaleFactor), ScalePoint(50, 150, scaleFactor), ScalePoint(55, 140, scaleFactor),
                    ScalePoint(45, 130, scaleFactor), ScalePoint(50, 120, scaleFactor),
                }
            };

            // Vẽ các thanh thép ngang
            LineGeometry line1 = new LineGeometry(ScalePoint(49, 95, scaleFactor), ScalePoint(170, 95, scaleFactor));
            LineGeometry line2 = new LineGeometry(ScalePoint(49, 110, scaleFactor), ScalePoint(230, 110, scaleFactor));
            // Vẽ thanh thép dọc
            LineGeometry line3 = new LineGeometry(ScalePoint(175, 100, scaleFactor), ScalePoint(175, 130, scaleFactor));

            // Vẽ thanh thép cong 
            PathFigure arcFigure1 = new PathFigure { StartPoint = ScalePoint(170, 95, scaleFactor) };
            arcFigure1.Segments.Add(new ArcSegment(ScalePoint(175, 100, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc1 = new PathGeometry();
            arc1.Figures.Add(arcFigure1);


            //Vẽ các đường zitzac trái 
            PointCollection zitzac1 = new PointCollection
            {
                ScalePoint(50, 80, scaleFactor),
                ScalePoint(50, 120, scaleFactor),
                ScalePoint(45, 130, scaleFactor),
                ScalePoint(55, 140, scaleFactor),
                ScalePoint(50, 150, scaleFactor),
                ScalePoint(50, 170, scaleFactor),
            };

            Polyline polyline1 = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 0.7 * scaleFactor,
                Points = zitzac1
            };
            //Vẽ các đường zitzac phải 
            PointCollection zitzac2 = new PointCollection
            {
                ScalePoint(250, 90, scaleFactor),
                ScalePoint(250, 120, scaleFactor),
                ScalePoint(245, 130, scaleFactor),
                ScalePoint(255, 140, scaleFactor),
                ScalePoint(250, 150, scaleFactor),
                ScalePoint(250, 180, scaleFactor),
            };

            Polyline polyline2 = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 0.7 * scaleFactor,
                Points = zitzac2
            };

            // Vẽ các đường kích thước  (dim1,dim2,dim3)
            Line dim1 = new Line
            {
                X1 = ScalePoint(40, 95, scaleFactor).X,
                Y1 = ScalePoint(40, 95, scaleFactor).Y,
                X2 = ScalePoint(44, 95, scaleFactor).X,
                Y2 = ScalePoint(44, 95, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim2 = new Line
            {
                X1 = ScalePoint(42, 95, scaleFactor).X,
                Y1 = ScalePoint(42, 95, scaleFactor).Y,
                X2 = ScalePoint(42, 110, scaleFactor).X,
                Y2 = ScalePoint(42, 110, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim3 = new Line
            {
                X1 = ScalePoint(40, 110, scaleFactor).X,
                Y1 = ScalePoint(40, 110, scaleFactor).Y,
                X2 = ScalePoint(44, 110, scaleFactor).X,
                Y2 = ScalePoint(44, 110, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            // Thêm các phần tử vào canvas

            MainCanvas4.Children.Add(dim1);
            MainCanvas4.Children.Add(dim2);
            MainCanvas4.Children.Add(dim3);

            MainCanvas4.Children.Add(concrete1);
            MainCanvas4.Children.Add(polyline1);
            MainCanvas4.Children.Add(polyline2);

            // Nhóm các hình học thép
            GeometryGroup steelGeometry = new GeometryGroup();

            // Thêm đường thép vào canvas
            Path steelPath = new Path
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1.5 * scaleFactor,
                Data = steelGeometry
            };

            TextBlock textBox = new TextBlock
            {
                Text = ShukinKankakuTextBox.Text + "mm",
                Foreground = textBrush,
                FontSize = 10 * scaleFactor,
                TextAlignment = TextAlignment.Right
            };

            textBox.SizeChanged += (s, e) =>
            {
                Canvas.SetLeft(textBox, ScalePoint(30, 98, scaleFactor).X - textBox.ActualWidth);
            };

            Canvas.SetLeft(textBox, ScalePoint(30, 98, scaleFactor).X - textBox.Width);
            Canvas.SetTop(textBox, ScalePoint(30, 98, scaleFactor).Y);

            //TextBlock textBlock2 = new TextBlock
            //{
            //    Text = "mm",
            //    Foreground = textBrush,
            //    FontSize = 8 * scaleFactor
            //};
            //Canvas.SetLeft(textBlock2, ScalePoint(26.5, 98, scaleFactor).X);
            //Canvas.SetTop(textBlock2, ScalePoint(26.5, 98, scaleFactor).Y);

            MainCanvas4.Children.Add(steelPath);
            steelGeometry.Children.Add(line1);
            steelGeometry.Children.Add(line2);
            steelGeometry.Children.Add(line3);
            steelGeometry.Children.Add(arc1);
            MainCanvas4.Children.Add(textBox);
            //MainCanvas4.Children.Add(textBlock2);

        }
        private void DrawBeamDiagram6()
        {
            if (MainCanvas5 == null) return;

            MainCanvas5.Children.Clear();

            // Định nghĩa các cọ vẽ
            SolidColorBrush LBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush dimBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Blue);

            double scaleFactor = 1.3;

            // Vẽ các thanh thép thẳng (L)
            LineGeometry line1 = new LineGeometry(ScalePoint(50, 50, scaleFactor), ScalePoint(215, 50, scaleFactor));
            LineGeometry line2 = new LineGeometry(ScalePoint(165, 54, scaleFactor), ScalePoint(330, 54, scaleFactor));
            LineGeometry line3 = new LineGeometry(ScalePoint(50, 65, scaleFactor), ScalePoint(235, 65, scaleFactor));
            LineGeometry line4 = new LineGeometry(ScalePoint(190, 69, scaleFactor), ScalePoint(330, 69, scaleFactor));
            // Vẽ các thanh thép thẳng (H)
            LineGeometry line5 = new LineGeometry(ScalePoint(50, 90, scaleFactor), ScalePoint(330, 90, scaleFactor));
            LineGeometry line6 = new LineGeometry(ScalePoint(50, 100, scaleFactor), ScalePoint(330, 100, scaleFactor));
            // Vẽ các thanh thép thẳng (3 chấm đen)
            LineGeometry line7 = new LineGeometry(ScalePoint(50, 130, scaleFactor), ScalePoint(330, 130, scaleFactor));
            LineGeometry line8 = new LineGeometry(ScalePoint(50, 140, scaleFactor), ScalePoint(330, 140, scaleFactor));
            LineGeometry line9 = new LineGeometry(ScalePoint(50, 150, scaleFactor), ScalePoint(330, 150, scaleFactor));

            LineGeometry line10 = new LineGeometry(ScalePoint(50, 180, scaleFactor), ScalePoint(215, 180, scaleFactor));
            LineGeometry line11 = new LineGeometry(ScalePoint(165, 184, scaleFactor), ScalePoint(330, 184, scaleFactor));
            LineGeometry line12 = new LineGeometry(ScalePoint(50, 200, scaleFactor), ScalePoint(235, 200, scaleFactor));
            LineGeometry line13 = new LineGeometry(ScalePoint(185, 204, scaleFactor), ScalePoint(330, 204, scaleFactor));
            LineGeometry line14 = new LineGeometry(ScalePoint(50, 220, scaleFactor), ScalePoint(215, 220, scaleFactor));
            LineGeometry line15 = new LineGeometry(ScalePoint(165, 224, scaleFactor), ScalePoint(330, 224, scaleFactor));
            //全数継手の場合
            LineGeometry line16 = new LineGeometry(ScalePoint(50, 180, scaleFactor), ScalePoint(235, 180, scaleFactor));
            LineGeometry line17 = new LineGeometry(ScalePoint(185, 184, scaleFactor), ScalePoint(330, 184, scaleFactor));
            LineGeometry line18 = new LineGeometry(ScalePoint(50, 220, scaleFactor), ScalePoint(235, 220, scaleFactor));
            LineGeometry line19 = new LineGeometry(ScalePoint(185, 224, scaleFactor), ScalePoint(330, 224, scaleFactor));

            // Define EllipseGeometry objects
            EllipseGeometry ellipseGeometry1 = new EllipseGeometry(ScalePoint(190, 90, scaleFactor), 3, 3);
            EllipseGeometry ellipseGeometry2 = new EllipseGeometry(ScalePoint(215, 100, scaleFactor), 3, 3);
            EllipseGeometry ellipseGeometry3 = new EllipseGeometry(ScalePoint(190, 130, scaleFactor), 3, 3);
            EllipseGeometry ellipseGeometry4 = new EllipseGeometry(ScalePoint(190, 140, scaleFactor), 3, 3);
            EllipseGeometry ellipseGeometry5 = new EllipseGeometry(ScalePoint(190, 150, scaleFactor), 3, 3);
            EllipseGeometry ellipseGeometry6 = new EllipseGeometry(ScalePoint(215, 140, scaleFactor), 3, 3);

            Path path1 = new Path
            {
                Stroke = Brushes.Black,
                Fill = Brushes.Black,
                Data = ellipseGeometry1
            };
            Path path2 = new Path
            {
                Stroke = Brushes.Black,
                Fill = Brushes.Black,
                Data = ellipseGeometry2
            };
            Path path3 = new Path
            {
                Stroke = Brushes.Black,
                Fill = Brushes.Black,
                Data = ellipseGeometry3
            };
            Path path4 = new Path
            {
                Stroke = Brushes.Black,
                Fill = Brushes.Black,
                Data = ellipseGeometry4
            };
            Path path5 = new Path
            {
                Stroke = Brushes.Black,
                Fill = Brushes.Black,
                Data = ellipseGeometry5
            };
            Path path6 = new Path
            {
                Stroke = Brushes.Black,
                Fill = Brushes.Black,
                Data = ellipseGeometry6
            };


            // Vẽ các đường kích thước
            Line dim1 = new Line
            {
                X1 = ScalePoint(165, 45, scaleFactor).X,
                Y1 = ScalePoint(165, 45, scaleFactor).Y,
                X2 = ScalePoint(165, 39, scaleFactor).X,
                Y2 = ScalePoint(165, 39, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1.5 * scaleFactor
            };
            Line dim2 = new Line
            {
                X1 = ScalePoint(165, 42, scaleFactor).X,
                Y1 = ScalePoint(165, 42, scaleFactor).Y,
                X2 = ScalePoint(215, 42, scaleFactor).X,
                Y2 = ScalePoint(215, 42, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1.5 * scaleFactor
            };
            Line dim3 = new Line
            {
                X1 = ScalePoint(215, 45, scaleFactor).X,
                Y1 = ScalePoint(215, 45, scaleFactor).Y,
                X2 = ScalePoint(215, 39, scaleFactor).X,
                Y2 = ScalePoint(215, 39, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1.5 * scaleFactor
            };

            // Thêm các phần tử vào canvas           
            MainCanvas5.Children.Add(dim1);
            MainCanvas5.Children.Add(dim2);
            MainCanvas5.Children.Add(dim3);


            double centerX1 = ScalePoint(188, 24, scaleFactor).X;
            double centerY1 = ScalePoint(188, 24, scaleFactor).Y;
            double centerX2 = ScalePoint(188, 72, scaleFactor).X;
            double centerY2 = ScalePoint(188, 72, scaleFactor).Y;
            double textBlockWidth = 100 * scaleFactor; // Adjust width as needed


            // Thêm các TextBlock
            TextBlock textBlock = new TextBlock
            {
                Text = TsuNaga16TextBox.Text + "d",
                Foreground = LBrush,
                FontSize = 10 * scaleFactor,
                TextAlignment = TextAlignment.Center,
                Width = textBlockWidth
            };
            Canvas.SetLeft(textBlock, centerX1 - textBlockWidth / 2);
            Canvas.SetTop(textBlock, centerY1);

            TextBlock textBlock1 = new TextBlock
            {
                Text = TsuNaga19TextBox.Text + "d",
                Foreground = dimBrush,
                FontSize = 10 * scaleFactor,
                TextAlignment = TextAlignment.Center,
                Width = textBlockWidth
            };
            Canvas.SetLeft(textBlock1, centerX2 - textBlockWidth / 2);
            Canvas.SetTop(textBlock1, centerY2);

            //Vẽ các thnah thép cong (6 cây) theo chiều kim đồng hồ
            PathFigure arcFigure1 = new PathFigure { StartPoint = ScalePoint(210, 170, scaleFactor) };
            arcFigure1.Segments.Add(new ArcSegment(ScalePoint(214, 180, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc1 = new PathGeometry();
            arc1.Figures.Add(arcFigure1);

            PathFigure arcFigure2 = new PathFigure { StartPoint = ScalePoint(166, 184, scaleFactor) };
            arcFigure2.Segments.Add(new ArcSegment(ScalePoint(170, 174, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc2 = new PathGeometry();
            arc2.Figures.Add(arcFigure2);

            PathFigure arcFigure3 = new PathFigure { StartPoint = ScalePoint(230, 190, scaleFactor) };
            arcFigure3.Segments.Add(new ArcSegment(ScalePoint(234, 200, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc3 = new PathGeometry();
            arc3.Figures.Add(arcFigure3);

            PathFigure arcFigure4 = new PathFigure { StartPoint = ScalePoint(186, 204, scaleFactor) };
            arcFigure4.Segments.Add(new ArcSegment(ScalePoint(190, 194, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc4 = new PathGeometry();
            arc4.Figures.Add(arcFigure4);

            PathFigure arcFigure5 = new PathFigure { StartPoint = ScalePoint(210, 210, scaleFactor) };
            arcFigure5.Segments.Add(new ArcSegment(ScalePoint(214, 220, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc5 = new PathGeometry();
            arc5.Figures.Add(arcFigure5);

            PathFigure arcFigure6 = new PathFigure { StartPoint = ScalePoint(166, 224, scaleFactor) };
            arcFigure6.Segments.Add(new ArcSegment(ScalePoint(170, 214, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc6 = new PathGeometry();
            arc6.Figures.Add(arcFigure6);

            PathFigure arcFigure7 = new PathFigure { StartPoint = ScalePoint(230, 170, scaleFactor) };
            arcFigure7.Segments.Add(new ArcSegment(ScalePoint(234, 180, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc7 = new PathGeometry();
            arc7.Figures.Add(arcFigure7);

            PathFigure arcFigure8 = new PathFigure { StartPoint = ScalePoint(186, 184, scaleFactor) };
            arcFigure8.Segments.Add(new ArcSegment(ScalePoint(190, 174, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc8 = new PathGeometry();
            arc8.Figures.Add(arcFigure8);

            PathFigure arcFigure9 = new PathFigure { StartPoint = ScalePoint(230, 210, scaleFactor) };
            arcFigure9.Segments.Add(new ArcSegment(ScalePoint(234, 220, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc9 = new PathGeometry();
            arc9.Figures.Add(arcFigure9);

            PathFigure arcFigure10 = new PathFigure { StartPoint = ScalePoint(186, 224, scaleFactor) };
            arcFigure10.Segments.Add(new ArcSegment(ScalePoint(190, 214, scaleFactor), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc10 = new PathGeometry();
            arc10.Figures.Add(arcFigure10);

            // Thêm các arc với màu đỏ
            Path arcPath1 = new Path
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3 * scaleFactor,
                Data = arc1
            };
            Path arcPath2 = new Path
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3 * scaleFactor,
                Data = arc2
            };
            Path arcPath3 = new Path
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3 * scaleFactor,
                Data = arc3
            };
            Path arcPath4 = new Path
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3 * scaleFactor,
                Data = arc4
            };
            Path arcPath5 = new Path
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3 * scaleFactor,
                Data = arc5
            };
            Path arcPath6 = new Path
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3 * scaleFactor,
                Data = arc6
            };
            Path arcPath7 = new Path
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3 * scaleFactor,
                Data = arc7
            };
            Path arcPath8 = new Path
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3 * scaleFactor,
                Data = arc8
            };
            Path arcPath9 = new Path
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3 * scaleFactor,
                Data = arc9
            };
            Path arcPath10 = new Path
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3 * scaleFactor,
                Data = arc10
            };


            // Nhóm các hình học thép
            GeometryGroup steelGeometry = new GeometryGroup();
            steelGeometry.Children.Add(line1);
            steelGeometry.Children.Add(line2);
            steelGeometry.Children.Add(line3);
            steelGeometry.Children.Add(line4);
            steelGeometry.Children.Add(line5);
            steelGeometry.Children.Add(line6);
            steelGeometry.Children.Add(line7);
            steelGeometry.Children.Add(line8);
            steelGeometry.Children.Add(line9);

            // Thêm đường thép vào canvas
            Path steelPath = new Path
            {
                Stroke = Brushes.Gray,
                StrokeThickness = 3 * scaleFactor,
                Data = steelGeometry
            };

            // Thêm các phần tử vào canvas
            MainCanvas5.Children.Add(steelPath);
            MainCanvas5.Children.Add(textBlock);
            MainCanvas5.Children.Add(textBlock1);
            MainCanvas5.Children.Add(path1);
            MainCanvas5.Children.Add(path2);

            if (tsugite1.IsChecked == true)
            {
                MainCanvas5.Children.Add(path3);
                MainCanvas5.Children.Add(path4);
                MainCanvas5.Children.Add(path5);
                steelGeometry.Children.Add(line12);
                steelGeometry.Children.Add(line13);
                steelGeometry.Children.Add(line16);
                steelGeometry.Children.Add(line17);
                steelGeometry.Children.Add(line18);
                steelGeometry.Children.Add(line19);
                if (shitatsuhook2.IsChecked == true)
                {
                    steelGeometry.Children.Add(arc7);
                    steelGeometry.Children.Add(arc8);
                    steelGeometry.Children.Add(arc9);
                    steelGeometry.Children.Add(arc10);
                    steelGeometry.Children.Add(arc3);
                    steelGeometry.Children.Add(arc4);
                    MainCanvas5.Children.Add(arcPath3);
                    MainCanvas5.Children.Add(arcPath4);
                    MainCanvas5.Children.Add(arcPath7);
                    MainCanvas5.Children.Add(arcPath8);
                    MainCanvas5.Children.Add(arcPath9);
                    MainCanvas5.Children.Add(arcPath10);
                }

            }
            else if (tsugite2.IsChecked == true)
            {
                MainCanvas5.Children.Add(path3);
                MainCanvas5.Children.Add(path5);
                MainCanvas5.Children.Add(path6);
                steelGeometry.Children.Add(line10);
                steelGeometry.Children.Add(line11);
                steelGeometry.Children.Add(line12);
                steelGeometry.Children.Add(line13);
                steelGeometry.Children.Add(line14);
                steelGeometry.Children.Add(line15);
                if (shitatsuhook2.IsChecked == true)
                {
                    steelGeometry.Children.Add(arc1);
                    steelGeometry.Children.Add(arc2);
                    steelGeometry.Children.Add(arc3);
                    steelGeometry.Children.Add(arc4);
                    steelGeometry.Children.Add(arc5);
                    steelGeometry.Children.Add(arc6);
                    MainCanvas5.Children.Add(arcPath1);
                    MainCanvas5.Children.Add(arcPath2);
                    MainCanvas5.Children.Add(arcPath3);
                    MainCanvas5.Children.Add(arcPath4);
                    MainCanvas5.Children.Add(arcPath5);
                    MainCanvas5.Children.Add(arcPath6);
                }
            }
        }
        private void DrawBeamDiagram8()
        {
            if (MainCanvas7 == null) return;

            MainCanvas7.Children.Clear();

            // Định nghĩa các cọ vẽ
            SolidColorBrush LBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush dimBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Blue);

            double scaleFactor = 1.3;
            double offsetX = 20;
            double offsetY = 100;

            // Vẽ các thanh thép thẳng (L)
            LineGeometry line1 = new LineGeometry(ScalePoint(50, 114, scaleFactor, offsetX, offsetY), ScalePoint(215, 114, scaleFactor, offsetX, offsetY));
            LineGeometry line2 = new LineGeometry(ScalePoint(165, 110, scaleFactor, offsetX, offsetY), ScalePoint(330, 110, scaleFactor, offsetX, offsetY));
            LineGeometry line3 = new LineGeometry(ScalePoint(50, 104, scaleFactor, offsetX, offsetY), ScalePoint(235, 104, scaleFactor, offsetX, offsetY));
            LineGeometry line4 = new LineGeometry(ScalePoint(190, 100, scaleFactor, offsetX, offsetY), ScalePoint(330, 100, scaleFactor, offsetX, offsetY));
            // Vẽ các thanh thép thẳng (H)
            LineGeometry line5 = new LineGeometry(ScalePoint(50, 60, scaleFactor, offsetX, offsetY), ScalePoint(330, 60, scaleFactor, offsetX, offsetY));
            LineGeometry line6 = new LineGeometry(ScalePoint(50, 50, scaleFactor, offsetX, offsetY), ScalePoint(330, 50, scaleFactor, offsetX, offsetY));

            // Define EllipseGeometry objects
            EllipseGeometry ellipseGeometry1 = new EllipseGeometry(ScalePoint(215, 50, scaleFactor, offsetX, offsetY), 3, 3);
            EllipseGeometry ellipseGeometry2 = new EllipseGeometry(ScalePoint(190, 60, scaleFactor, offsetX, offsetY), 3, 3);

            Path path1 = new Path
            {
                Stroke = Brushes.Black,
                Fill = Brushes.Black,
                Data = ellipseGeometry1
            };
            Path path2 = new Path
            {
                Stroke = Brushes.Black,
                Fill = Brushes.Black,
                Data = ellipseGeometry2
            };

            // Vẽ đường dim a trên
            Line dim1 = new Line
            {
                X1 = ScalePoint(190, 90, scaleFactor, offsetX, offsetY).X,
                Y1 = ScalePoint(190, 90, scaleFactor, offsetX, offsetY).Y,
                X2 = ScalePoint(190, 94, scaleFactor, offsetX, offsetY).X,
                Y2 = ScalePoint(190, 94, scaleFactor, offsetX, offsetY).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim2 = new Line
            {
                X1 = ScalePoint(190, 92, scaleFactor, offsetX, offsetY).X,
                Y1 = ScalePoint(190, 92, scaleFactor, offsetX, offsetY).Y,
                X2 = ScalePoint(215, 92, scaleFactor, offsetX, offsetY).X,
                Y2 = ScalePoint(215, 92, scaleFactor, offsetX, offsetY).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim3 = new Line
            {
                X1 = ScalePoint(215, 90, scaleFactor, offsetX, offsetY).X,
                Y1 = ScalePoint(215, 90, scaleFactor, offsetX, offsetY).Y,
                X2 = ScalePoint(215, 94, scaleFactor, offsetX, offsetY).X,
                Y2 = ScalePoint(215, 94, scaleFactor, offsetX, offsetY).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            // Vẽ đường dim a dưới
            Line dim4 = new Line
            {
                X1 = ScalePoint(190, 40, scaleFactor, offsetX, offsetY).X,
                Y1 = ScalePoint(190, 40, scaleFactor, offsetX, offsetY).Y,
                X2 = ScalePoint(190, 44, scaleFactor, offsetX, offsetY).X,
                Y2 = ScalePoint(190, 44, scaleFactor, offsetX, offsetY).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim5 = new Line
            {
                X1 = ScalePoint(190, 42, scaleFactor, offsetX, offsetY).X,
                Y1 = ScalePoint(190, 42, scaleFactor, offsetX, offsetY).Y,
                X2 = ScalePoint(215, 42, scaleFactor, offsetX, offsetY).X,
                Y2 = ScalePoint(215, 42, scaleFactor, offsetX, offsetY).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };
            Line dim6 = new Line
            {
                X1 = ScalePoint(215, 40, scaleFactor, offsetX, offsetY).X,
                Y1 = ScalePoint(215, 40, scaleFactor, offsetX, offsetY).Y,
                X2 = ScalePoint(215, 44, scaleFactor, offsetX, offsetY).X,
                Y2 = ScalePoint(215, 44, scaleFactor, offsetX, offsetY).Y,
                Stroke = dimBrush,
                StrokeThickness = 1 * scaleFactor
            };

            // Thêm các phần tử vào canvas           
            MainCanvas7.Children.Add(dim1);
            MainCanvas7.Children.Add(dim2);
            MainCanvas7.Children.Add(dim3);
            MainCanvas7.Children.Add(dim4);
            MainCanvas7.Children.Add(dim5);
            MainCanvas7.Children.Add(dim6);

            TextBlock textBox = new TextBlock
            {
                Text = TonariOoTextBox.Text,
                Foreground = textBrush,
                FontSize = 10 * scaleFactor,
                TextAlignment = TextAlignment.Center
            };
            textBox.SizeChanged += (s, e) =>
            {
                var formattedText = new FormattedText(
                    textBox.Text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(textBox.FontFamily, textBox.FontStyle, textBox.FontWeight, textBox.FontStretch),
                    textBox.FontSize,
                    Brushes.Black,
                    new NumberSubstitution(),
                    1);
                double newWidth = formattedText.Width + 10;
                textBox.Width = newWidth;

                Canvas.SetLeft(textBox, ScalePoint(202, 87, scaleFactor, offsetX, offsetY).X - newWidth / 2);
            };
            Canvas.SetLeft(textBox, ScalePoint(202, 87, scaleFactor, offsetX, offsetY).X - textBox.Width / 2);
            Canvas.SetTop(textBox, ScalePoint(202, 87, scaleFactor, offsetX, offsetY).Y);

            TextBlock textBox1 = new TextBlock
            {
                Text = TonariOoTextBox.Text,
                Foreground = textBrush,
                FontSize = 10 * scaleFactor,
                TextAlignment = TextAlignment.Center
            };
            textBox1.SizeChanged += (s, e) =>
            {
                var formattedText = new FormattedText(
                    textBox1.Text,
                    System.Globalization.CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface(textBox1.FontFamily, textBox1.FontStyle, textBox1.FontWeight, textBox1.FontStretch),
                    textBox1.FontSize,
                    Brushes.Black,
                    new NumberSubstitution(),
                    1);
                double newWidth = formattedText.Width + 10;
                textBox1.Width = newWidth;

                Canvas.SetLeft(textBox1, ScalePoint(202, 37, scaleFactor, offsetX, offsetY).X - newWidth / 2);
            };
            Canvas.SetLeft(textBox1, ScalePoint(202, 37, scaleFactor, offsetX, offsetY).X - textBox1.Width / 2);
            Canvas.SetTop(textBox1, ScalePoint(202, 37, scaleFactor, offsetX, offsetY).Y);

            // Nhóm các hình học thép
            GeometryGroup steelGeometry = new GeometryGroup();
            steelGeometry.Children.Add(line1);
            steelGeometry.Children.Add(line2);
            steelGeometry.Children.Add(line3);
            steelGeometry.Children.Add(line4);
            steelGeometry.Children.Add(line5);
            steelGeometry.Children.Add(line6);

            // Thêm đường thép vào canvas
            Path steelPath = new Path
            {
                Stroke = Brushes.Gray,
                StrokeThickness = 3 * scaleFactor,
                Data = steelGeometry
            };

            // Thêm các phần tử vào canvas
            MainCanvas7.Children.Add(steelPath);
            MainCanvas7.Children.Add(path1);
            MainCanvas7.Children.Add(path2);
            MainCanvas7.Children.Add(textBox);
            MainCanvas7.Children.Add(textBox1);
        }
        //// page 1 _ image 1 - 2025/03/11 - Ton

        private Dictionary<string, List<Path>> lineGroups = new Dictionary<string, List<Path>>
        {
            { "外端端部", new List<Path>() }, // トン Image1 2025/03/11 - DrawBeamDiagram9
            { "內端端部", new List<Path>() },
            { "外端中央部", new List<Path>() },
            { "內端中央部", new List<Path>() },
            { "定着_上筋", new List<Path>() },
            { "定着_下筋", new List<Path>() },
            { "外端中央3", new List<Path>() }, // トン　Image3 2025/03/12 - DrawBeamDiagram12
            { "內端中央3", new List<Path>() },
            { "外端端部3", new List<Path>() },
            { "內端端部3", new List<Path>() },
            { "phat", new List<Path>() },
            { "phat1", new List<Path>() },
        };
        private void DrawBeamDiagramTon9() // 一般梁 全般
        {
            Canvas canvas = new Canvas { Width = 800, Height = 300, Background = Brushes.White };
            page1_1_canvas.Children.Clear();
            page1_1_canvas.Children.Add(canvas);

            // Vẽ các hình chữ nhật
            DrawRectangle1(canvas, 0, 100, 170, 170, Brushes.DarkGray);
            DrawRectangle1(canvas, 60, 40, 70, 80, Brushes.DarkGray);
            DrawRectangle1(canvas, 160, 100, 455, 140, Brushes.DarkGray);
            DrawRectangle1(canvas, 550 + 60, 100, 170, 170, Brushes.DarkGray);
            DrawRectangle1(canvas, 650, 40, 70, 80, Brushes.DarkGray);
            DrawRectangle1(canvas, 718 + 60, 100, 70, 140, Brushes.DarkGray);
            DrawRectangle1(canvas, 765 + 60, 190, 30, 55, Brushes.White);
            DrawRectangle1(canvas, 765 + 60, 99, 30, 40, Brushes.White);

            // Vẽ đường thép trên
            double lineStartX = 110, lineStartY = 120, lineEndX = 130, lineEndY = lineStartY;
            DrawStraightLine(canvas, 130, lineStartY, 825, lineEndY, 3, Brushes.Black);
            lineGroups["定着_上筋"].Add(DrawStraightLine(canvas, lineStartX, lineStartY, lineEndX, lineEndY, 3, Brushes.Black));
            lineGroups["定着_上筋"].Add(DrawQuarterCircle(canvas, lineStartX, lineStartY + 20, 20, 180, 3, Brushes.Black));
            lineGroups["定着_上筋"].Add(DrawVerticalLine(canvas, lineStartX - 20, lineStartY + 20 + 30, lineStartY + 20, 3, Brushes.Black));

            double lineStartX1 = 120, lineStartY1 = 130, lineEndX1 = 305, lineEndY1 = lineStartY1;
            DrawStraightLine(canvas, lineStartX1, lineStartY1, lineEndX1, lineEndY1, 3, Brushes.Black);
            DrawQuarterCircle(canvas, lineStartX1, lineStartY1 + 20, 20, 180, 3, Brushes.Black);
            DrawVerticalLine(canvas, lineStartX1 - 20, lineStartY1 + 20 + 30, lineStartY1 + 20, 3, Brushes.Black);
            DrawStraightLine(canvas, lineStartX1 + 355, lineStartY1, lineEndX1 + 520, lineEndY1, 3, Brushes.Black);

            // Vẽ đường thép dưới
            double lineStartX2 = 100, lineStartY2 = 220, lineEndX2 = 130, lineEndY2 = lineStartY2;
            DrawStraightLine(canvas, 130, lineStartY2, 825, lineEndY2, 3, Brushes.Black);
            lineGroups["定着_下筋"].Add(DrawStraightLine(canvas, lineStartX2, lineStartY2, lineEndX2, lineEndY2, 3, Brushes.Black));
            lineGroups["定着_下筋"].Add(DrawQuarterCircle(canvas, lineStartX2, lineStartY2 - 20, 20, 90, 3, Brushes.Black));
            lineGroups["定着_下筋"].Add(DrawVerticalLine(canvas, lineStartX2 - 20, lineStartY2 - 20 - 50, lineStartY2 - 20, 3, Brushes.Black));
            DrawStraightLine(canvas, lineStartX2 + 115, lineStartY2 - 10, 565, lineEndY2 - 10, 3, Brushes.Black);

            // Vẽ các hình tam giác
            double bonusX = 690 + 60, X1 = 50 + bonusX, X2 = 260 + bonusX, X3 = 150 + bonusX;
            double bonusY = 30, Y1 = 150 + bonusY, Y2 = 260 + bonusY, Y3 = 100 + bonusY;
            DrawTriangle(canvas, X1, Y1, X2, Y2, X3, Y3, Brushes.White);

            double bonusXX = 315 + 60, XX1 = 450 + bonusXX, XX2 = 600 + bonusXX, XX3 = 475 + bonusXX;
            double bonusYY = 0, YY1 = 140 + bonusYY, YY2 = 240 + bonusYY, YY3 = 100 + bonusYY;
            DrawTriangle(canvas, XX1, YY1, XX2, YY2, XX3, YY3, Brushes.White);

            // Đường thẳng dọc đoạn cuối
            DrawVerticalLine(canvas, 765 + 60, 80, 140, 1, Brushes.Black);
            DrawStraightLine(canvas, 765 + 60, 140, 790 + 60, 155, 1, Brushes.Black);
            DrawStraightLine(canvas, 740 + 60, 180, 790 + 60, 155, 1, Brushes.Black);
            DrawStraightLine(canvas, 740 + 60, 180, 765 + 60, 195, 1, Brushes.Black);
            DrawVerticalLine(canvas, 765 + 60, 195, 260, 1, Brushes.Black);

            // Vẽ đường dọc nét đứt
            DrawDashedVerticalLine(canvas, 60, 100, 275, 2, Brushes.Black);
            DrawDashedVerticalLine(canvas, 130, 100, 275, 2, Brushes.Black);
            DrawDashedVerticalLine(canvas, 260, 60, 240, 2, Brushes.Black);
            DrawDashedVerticalLine(canvas, 520, 60, 240, 2, Brushes.Black);
            DrawDashedVerticalLine(canvas, 650, 100, 275, 2, Brushes.Black);

            // Các đường ngang mảnh dài
            DrawStraightLine(canvas, 130, 60, 650, 60, 1, Brushes.Black);
            DrawStraightLine(canvas, 110, 170, 670, 170, 2, Brushes.Black);
            DrawStraightLine(canvas, 730, 170, 820, 170, 2, Brushes.Black);

            // Các đường ngang dày trên (giả định ComboBoxUwa tồn tại)
            if (ComboBoxUwa != null && ComboBoxUwa.SelectedItem?.ToString() == "端部")
            {
                DrawStraightLine(canvas, 130, 108, 260, 108, 10, Brushes.Yellow);
                DrawStraightLine(canvas, 520, 108, 650, 108, 10, Brushes.Yellow);
                DrawCircle(canvas, 220, 120, 5, Brushes.Black);
                DrawCircle(canvas, 600, 120, 5, Brushes.Black);
            }
            else
            {
                DrawStraightLine(canvas, 260, 108, 520, 108, 10, Brushes.Yellow);
                DrawCircle(canvas, 420, 120, 5, Brushes.Black);
            }

            // Đường ký hiệu độ dài
            lineGroups["外端端部"].Add(DrawStraightLine(canvas, 260, 90, 305, 90, 2, Brushes.Black));
            lineGroups["外端端部"].Add(DrawVerticalLine(canvas, 260, 85, 95, 2, Brushes.Black));
            lineGroups["外端端部"].Add(DrawVerticalLine(canvas, 305, 85, 95, 2, Brushes.Black));
            lineGroups["內端端部"].Add(DrawStraightLine(canvas, 475, 90, 520, 90, 2, Brushes.Black));
            lineGroups["內端端部"].Add(DrawVerticalLine(canvas, 475, 85, 95, 2, Brushes.Black));
            lineGroups["內端端部"].Add(DrawVerticalLine(canvas, 520, 85, 95, 2, Brushes.Black));
            lineGroups["外端中央部"].Add(DrawStraightLine(canvas, 215, 250, 260, 250, 2, Brushes.Black));
            lineGroups["外端中央部"].Add(DrawVerticalLine(canvas, 215, 245, 255, 2, Brushes.Black));
            lineGroups["外端中央部"].Add(DrawVerticalLine(canvas, 260, 245, 255, 2, Brushes.Black));
            lineGroups["內端中央部"].Add(DrawStraightLine(canvas, 520, 250, 565, 250, 2, Brushes.Black));
            lineGroups["內端中央部"].Add(DrawVerticalLine(canvas, 520, 245, 255, 2, Brushes.Black));
            lineGroups["內端中央部"].Add(DrawVerticalLine(canvas, 565, 245, 255, 2, Brushes.Black));

            // Đường "にげ"
            DrawStraightLine(canvas, 60, 220, 80, 220, 1, Brushes.Red);
            DrawVerticalLine(canvas, 60, 215, 225, 1, Brushes.Red);
            DrawVerticalLine(canvas, 80, 215, 225, 1, Brushes.Red);

            // Các đường ngang dưới (giả định ComboBoxShita tồn tại)
            if (ComboBoxShita != null && ComboBoxShita.SelectedItem?.ToString() == "端部")
            {
                DrawStraightLine(canvas, 130, 108 + 125, 260, 108 + 125, 10, Brushes.Yellow);
                DrawStraightLine(canvas, 520, 108 + 125, 650, 108 + 125, 10, Brushes.Yellow);
                DrawCircle(canvas, 200, 220, 5, Brushes.Black);
                DrawCircle(canvas, 580, 220, 5, Brushes.Black);
            }
            else
            {
                DrawStraightLine(canvas, 260, 108 + 125, 520, 108 + 125, 10, Brushes.Yellow);
                DrawCircle(canvas, 360, 220, 5, Brushes.Black);
            }

            // Vẽ hình tròn giữa
            DrawCircle(canvas, 390, 170, 3, Brushes.Black);

            // Vẽ 40d thép trên
            double lineStartX40d = 90, lineStartY40d = 108, lineEndX40d = 130, lineEndY40d = lineStartY40d;
            lineGroups["定着_上筋"].Add(DrawStraightLine(canvas, lineStartX40d, lineStartY40d, lineEndX40d, lineEndY40d, 1, Brushes.Black));
            lineGroups["定着_上筋"].Add(DrawQuarterCircle(canvas, lineStartX40d, lineStartY40d + 20, 20, 180, 1, Brushes.Black));
            lineGroups["定着_上筋"].Add(DrawVerticalLine(canvas, lineStartX40d - 20, lineStartY40d + 62, lineStartY40d + 20, 1, Brushes.Black));
            lineGroups["定着_上筋"].Add(DrawVerticalLine(canvas, lineStartX40d + 40, lineStartY40d + 10, lineStartY40d - 5, 1, Brushes.Black));
            lineGroups["定着_上筋"].Add(DrawStraightLine(canvas, lineStartX40d - 25, 170, lineEndX40d - 55, 170, 1, Brushes.Black));

            // Vẽ 40d thép dưới
            double lineStartX40d2 = 65, lineStartY40d2 = 250, lineEndX40d2 = 130, lineEndY40d2 = lineStartY40d2;
            lineGroups["定着_下筋"].Add(DrawStraightLine(canvas, lineStartX40d2, lineStartY40d2, lineEndX40d2, lineEndY40d2, 1, Brushes.Black));
            lineGroups["定着_下筋"].Add(DrawQuarterCircle(canvas, lineStartX40d2, lineStartY40d2 - 20, 20, 90, 1, Brushes.Black));
            lineGroups["定着_下筋"].Add(DrawVerticalLine(canvas, lineStartX40d2 + 65, lineStartY40d2 + 5, lineStartY40d2 - 5, 1, Brushes.Black));
            lineGroups["定着_下筋"].Add(DrawVerticalLine(canvas, lineStartX40d2 - 20, lineStartY40d2 - 100, lineStartY40d2 - 20, 1, Brushes.Black));
            lineGroups["定着_下筋"].Add(DrawStraightLine(canvas, lineStartX40d2 - 25, 150, lineEndX40d2 - 80, 150, 1, Brushes.Black));

            // Thêm text
            DrawText(canvas, "L", 390, 35, Brushes.Black, 16);
            DrawText(canvas, "L/4", 180, 60, Brushes.Black, 16);
            DrawText(canvas, "L/2", 385, 60, Brushes.Black, 16);
            DrawText(canvas, "L/4", 570, 60, Brushes.Black, 16);
            DrawText(canvas, "にげ", 62, 225, Brushes.Blue, 15);
            DrawText(canvas, $"{TeichakuShitaTextBox.Text}d", 40, 200, Brushes.Blue, 15, TextAlignment.Right);
            DrawText(canvas, $"{TeichakuUwaTextBox.Text}d", 115, 88, Brushes.Blue, 15, TextAlignment.Right);
            DrawText(canvas, $"{外端端部_textbox.Text}d", 280, 65, Brushes.Black, 16, TextAlignment.Center);
            DrawText(canvas, $"{內端端部_textbox.Text}d", 495, 65, Brushes.Black, 16, TextAlignment.Center);
            DrawText(canvas, $"{外端中央部_textbox.Text}d", 235, 252, Brushes.Black, 16, TextAlignment.Center);
            DrawText(canvas, $"{內端中央部_textbox.Text}d", 540, 252, Brushes.Black, 16, TextAlignment.Center);

            // Thiết lập hiệu ứng các TextBox
            SetupTextBox(外端端部_textbox, lineGroups["外端端部"]);
            SetupTextBox(內端端部_textbox, lineGroups["內端端部"]);
            SetupTextBox(外端中央部_textbox, lineGroups["外端中央部"]);
            SetupTextBox(內端中央部_textbox, lineGroups["內端中央部"]);
            SetupTextBox(TeichakuUwaTextBox, lineGroups["定着_上筋"]);
            SetupTextBox(TeichakuShitaTextBox, lineGroups["定着_下筋"]);

        }

        // Hàm thiết lập TextBox
        private void SetupTextBox(TextBox textBox, List<Path> lines)
        {
            // Hàm cập nhật màu
            void UpdateColors(Brush background, Brush stroke)
            {
                // Ép buộc cập nhật màu
                textBox.Dispatcher.Invoke(() =>
                {
                    textBox.Background = background;
                    textBox.ClearValue(Control.BackgroundProperty); // Xóa giá trị mặc định nếu có
                    textBox.Background = background; // Áp dụng lại
                    foreach (var line in lines)
                    {
                        line.Stroke = stroke;
                    }
                });
            }

            // Sự kiện khi focus
            textBox.GotFocus += (sender, e) =>
            {
                UpdateColors(Brushes.LightGreen, Brushes.Red);
            };

            // Sự kiện khi mất focus
            textBox.LostFocus += (sender, e) =>
            {
                UpdateColors(Brushes.White, Brushes.Black);
            };

            // Sự kiện khi con trỏ chuột di chuyển vào
            textBox.MouseEnter += (sender, e) =>
            {
                UpdateColors(Brushes.LightGreen, Brushes.Red); // Luôn đổi màu khi hover
            };

            // Sự kiện khi con trỏ chuột rời khỏi
            textBox.MouseLeave += (sender, e) =>
            {
                if (!textBox.IsFocused) // Chỉ trả lại màu nếu không focus
                {
                    UpdateColors(Brushes.White, Brushes.Black);
                }
            };

            // Sự kiện khi nội dung thay đổi
            textBox.TextChanged += (sender, e) =>
            {
                if (textBox.IsFocused) // Giữ màu khi đang nhập
                {
                    UpdateColors(Brushes.LightGreen, Brushes.Red);
                }
            };

            // Sự kiện khi nhập liệu bằng phím
            textBox.PreviewKeyDown += (sender, e) =>
            {
                if (textBox.IsFocused) // Giữ màu khi chỉnh sửa
                {
                    UpdateColors(Brushes.LightGreen, Brushes.Red);
                }
            };

            // Sự kiện khi thả phím (đảm bảo màu sau khi nhập)
            textBox.KeyUp += (sender, e) =>
            {
                if (textBox.IsFocused)
                {
                    UpdateColors(Brushes.LightGreen, Brushes.Red);
                }
            };
        }

        private void DrawRectangle1(Canvas canvas, double x, double y, double width, double height, Brush color)
        {
            Rectangle rect = new Rectangle
            {
                Width = width,
                Height = height,
                Fill = color,
                Stroke = Brushes.Transparent,
                StrokeThickness = 0
            };
            Canvas.SetLeft(rect, x);
            Canvas.SetTop(rect, y);
            canvas.Children.Add(rect);
        }

        private Path DrawStraightLine(Canvas canvas, double startX, double startY, double endX, double endY, int StrokeThickness1, Brush color)
        {
            Path path = new Path
            {
                Stroke = color,
                StrokeThickness = StrokeThickness1,
                StrokeLineJoin = PenLineJoin.Round
            };
            PathGeometry geometry = new PathGeometry();
            PathFigure figure = new PathFigure { StartPoint = new Point(startX, startY) };
            LineSegment lineSegment = new LineSegment { Point = new Point(endX, endY) };
            figure.Segments.Add(lineSegment);
            geometry.Figures.Add(figure);
            path.Data = geometry;
            canvas.Children.Add(path);
            return path;
        }

        private Path DrawQuarterCircle(Canvas canvas, double centerX, double centerY, double radius, double startAngle, int StrokeThickness1, Brush color)
        {
            Path path = new Path
            {
                Stroke = color,
                StrokeThickness = StrokeThickness1
            };
            double startAngleRad = startAngle * Math.PI / 180;
            double endAngleRad = (startAngle + 90) * Math.PI / 180;
            Point startPoint = new Point(centerX + radius * Math.Cos(startAngleRad), centerY + radius * Math.Sin(startAngleRad));
            Point endPoint = new Point(centerX + radius * Math.Cos(endAngleRad), centerY + radius * Math.Sin(endAngleRad));
            PathGeometry geometry = new PathGeometry();
            PathFigure figure = new PathFigure { StartPoint = startPoint };
            ArcSegment arc = new ArcSegment
            {
                Point = endPoint,
                Size = new Size(radius, radius),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = false
            };
            figure.Segments.Add(arc);
            geometry.Figures.Add(figure);
            path.Data = geometry;
            canvas.Children.Add(path);
            return path;
        }

        private void DrawTriangle(Canvas canvas, double x1, double y1, double x2, double y2, double x3, double y3, Brush fillColor)
        {
            Polygon triangle = new Polygon
            {
                Fill = fillColor,
                Stroke = Brushes.Transparent,
                StrokeThickness = 0
            };
            PointCollection points = new PointCollection
            {
                new Point(x1, y1),
                new Point(x2, y2),
                new Point(x3, y3)
            };
            triangle.Points = points;
            canvas.Children.Add(triangle);
        }

        private Path DrawVerticalLine(Canvas canvas, double x, double startY, double endY, double StrokeThickness1, Brush color)
        {
            Path path = new Path
            {
                Stroke = color,
                StrokeThickness = StrokeThickness1
            };
            PathGeometry geometry = new PathGeometry();
            PathFigure figure = new PathFigure { StartPoint = new Point(x, startY) };
            LineSegment lineSegment = new LineSegment { Point = new Point(x, endY) };
            figure.Segments.Add(lineSegment);
            geometry.Figures.Add(figure);
            path.Data = geometry;
            canvas.Children.Add(path);
            return path;
        }

        private void DrawDashedVerticalLine(Canvas canvas, double x, double startY, double endY, int StrokeThickness1, Brush color)
        {
            Path path = new Path
            {
                Stroke = color,
                StrokeThickness = StrokeThickness1,
                StrokeDashArray = new DoubleCollection { 4, 4 }
            };
            PathGeometry geometry = new PathGeometry();
            PathFigure figure = new PathFigure { StartPoint = new Point(x, startY) };
            LineSegment lineSegment = new LineSegment { Point = new Point(x, endY) };
            figure.Segments.Add(lineSegment);
            geometry.Figures.Add(figure);
            path.Data = geometry;
            canvas.Children.Add(path);
        }

        private void DrawCircle(Canvas canvas, double centerX, double centerY, double radius, Brush fillColor)
        {
            Ellipse circle = new Ellipse
            {
                Width = radius * 2,
                Height = radius * 2,
                Fill = fillColor,
                Stroke = Brushes.Transparent,
                StrokeThickness = 0
            };
            Canvas.SetLeft(circle, centerX - radius);
            Canvas.SetTop(circle, centerY - radius);
            canvas.Children.Add(circle);
        }

        private void DrawText(Canvas canvas, string text, double x, double y, Brush color, double fontSize = 12, TextAlignment alignment = TextAlignment.Left)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = fontSize,
                TextAlignment = alignment // Sử dụng tham số alignment để canh lề
            };

            // Đo kích thước của TextBlock để căn chỉnh chính xác
            textBlock.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double textWidth = textBlock.DesiredSize.Width;

            // Điều chỉnh vị trí dựa trên kiểu căn lề
            double leftPosition;
            switch (alignment)
            {
                case TextAlignment.Left:
                    leftPosition = x; // Điểm x là cạnh trái
                    break;
                case TextAlignment.Right:
                    leftPosition = x - textWidth; // Điểm x là cạnh phải
                    break;
                case TextAlignment.Center:
                    leftPosition = x - (textWidth / 2); // Điểm x là trung tâm
                    break;
                default:
                    leftPosition = x; // Mặc định là trái
                    break;
            }
            Canvas.SetLeft(textBlock, leftPosition);
            Canvas.SetTop(textBlock, y);
            canvas.Children.Add(textBlock);
        }
        private void DrawBeamDiagram10()
        {
            drawBeamDiagram10.Children.Clear();
            drawBeamDiagram10.Width = 319;
            drawBeamDiagram10.Height = 200;

            Polygon polygon = new Polygon
            {
                Fill = new SolidColorBrush(Colors.LightGray),
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Points = new PointCollection
                {
                    new Point(0,0),
                    new Point(197,0),
                    new Point(197,27),
                    new Point(295,27),
                    new Point(295,25),
                    new Point(295,64),
                    new Point(283,69),
                    new Point(307,74),
                    new Point(295,79),
                    new Point(295,79),
                    new Point(295,121),
                    new Point(295,117),
                    new Point(197,117),
                    new Point(197,127),
                    new Point(80,127),
                    new Point(80,78),
                    new Point(0,78),
                    new Point(0,83),
                    new Point(0,45),
                    new Point(12,40),
                    new Point(-12,35),
                    new Point(0,30),
                    new Point(0,-5),
                }
            };
            drawBeamDiagram10.Children.Add(polygon);
            PolyLine(0);
            PolyLine(14);
            PolyLine(49);
            PolyLine(61);
            PolyLineDash(0, 0);
            PolyLineDash(117, 27);
            PolyLineDash1();
            Line3(138.5, 138.5, 39, 75, Brushes.Red);
            Line3(134, 143, 75, 75, Brushes.Red);
            Line3(134, 143, 39, 39, Brushes.Red);
            Line3(80, 197, 138, 138, Brushes.Black);
            Line3(80, 80, 133, 142, Brushes.Black);
            Line3(197, 197, 133, 142, Brushes.Black);
            DrawText("L", 138.5, 140, Brushes.Black);
            DrawText("1", 108, 85, Brushes.Black);
            DrawText("6", 135, 100, Brushes.Black);
            DrawText($"{HariHabaZureTextBox.Text}", 150, 50, Brushes.Black);
            Polyline polyline = new Polyline
            {
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                Points = new PointCollection
                {
                    new Point(120,90),
                    new Point(163,102),
                    new Point(120,102),
                    new Point(120,90),
                }
            };
            drawBeamDiagram10.Children.Add(polyline);
        }
        private void DrawText(string text, double x, double y, Brush color)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = 14,
                // FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(textBlock, x);
            Canvas.SetTop(textBlock, y);
            drawBeamDiagram10.Children.Add(textBlock);
        }
        private void Line3(double x1, double x2, double y1, double y2, Brush strokeColor)
        {
            Line line = new Line
            {
                Stroke = strokeColor,
                StrokeThickness = 2,
                X1 = x1,
                X2 = x2,
                Y1 = y1,
                Y2 = y2,
            };
            drawBeamDiagram10.Children.Add(line);
        }
        private void PolyLineDash1()
        {
            Polyline poly = new Polyline
            {
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 10, 5 }
            };
            var point = new List<(double x, double y)>()
            {
                (0, 39),
                (138.5, 39),
                (138.5, 75),
                (295, 75),
            };
            foreach (var (x, y) in point)
            {
                poly.Points.Add(new Point(x, y));
            }
            drawBeamDiagram10.Children.Add(poly);
        }
        private void PolyLineDash(double n, double m)
        {
            Polyline poly = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 10, 5 }
            };
            var point = new List<(double x, double y)>()
            {
                (80, 0),
                (80, 88),
            };
            foreach (var (x, y) in point)
            {
                poly.Points.Add(new Point(x + n, y + m));
            }
            drawBeamDiagram10.Children.Add(poly);
        }
        private void PolyLine(double n)
        {
            Polyline poly = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 2,
            };
            var point = new List<(double x, double y)>()
            {
                (0, 11),
                (80, 11),
                (197, 41),
                (295, 41),
            };
            foreach (var (x, y) in point)
            {
                poly.Points.Add(new Point(x, y + n));
            }
            drawBeamDiagram10.Children.Add(poly);
        }


        private void DrawBeamDiagram11()
        {
            drawBeamDiagram11.Children.Clear();
            drawBeamDiagram11.Width = 319;
            drawBeamDiagram11.Height = 200;

            Polygon polygon = new Polygon
            {
                Fill = new SolidColorBrush(Colors.LightGray),
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Points = new PointCollection
                {
                    new Point(0,0),
                    new Point(197,0),
                    new Point(197,27),
                    new Point(295,27),
                    new Point(295,25),
                    new Point(295,64),
                    new Point(283,69),
                    new Point(307,74),
                    new Point(295,79),
                    new Point(295,79),
                    new Point(295,121),
                    new Point(295,117),
                    new Point(197,117),
                    new Point(197,127),
                    new Point(80,127),
                    new Point(80,78),
                    new Point(0,78),
                    new Point(0,83),
                    new Point(0,45),
                    new Point(12,40),
                    new Point(-12,35),
                    new Point(0,30),
                    new Point(0,-5),
                }
            };
            drawBeamDiagram11.Children.Add(polygon);
            PolyLineDash1(0, 0);
            PolyLineDash1(117, 27);
            PolyLineDash11();
            Line1(0, 180, 11, 11, Brushes.Black);
            Line1(0, 180, 25, 25, Brushes.Black);
            Line1(20, 295, 39, 39, Brushes.Black);
            Line1(20, 295, 53, 53, Brushes.Black);
            Line1(0, 250, 60, 60, Brushes.Black);
            Line1(0, 250, 74, 74, Brushes.Black);
            Line1(95, 295, 90, 90, Brushes.Black);
            Line1(95, 295, 102, 102, Brushes.Black);
            Line1(138.5, 138.5, 39, 72, Brushes.Red);
            Line1(134, 143, 72, 72, Brushes.Red);
            Line1(134, 143, 40, 40, Brushes.Red);
            Line1(80, 197, 138, 138, Brushes.Black);
            Line1(80, 80, 133, 142, Brushes.Black);
            Line1(197, 197, 133, 142, Brushes.Black);
            DrawText1("L", 138.5, 140, Brushes.Black);
            DrawText1("1", 108, 85, Brushes.Blue);
            DrawText1("6", 135, 100, Brushes.Blue);
            DrawText1($"{KakuShukinZureTextBox.Text}", 150, 50, Brushes.Black);
            // hình tam giác đỏ
            Polyline polyline = new Polyline
            {
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                Points = new PointCollection
                {
                    new Point(120,90),
                    new Point(163,102),
                    new Point(120,102),
                    new Point(120,90),
                }
            };
            drawBeamDiagram11.Children.Add(polyline);
        }
        private void DrawText1(string text, double x, double y, Brush color)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = color,
                FontSize = 14,
                //FontWeight = FontWeights.Bold
            };
            Canvas.SetLeft(textBlock, x);
            Canvas.SetTop(textBlock, y);
            drawBeamDiagram11.Children.Add(textBlock);
        }
        private void Line1(double x1, double x2, double y1, double y2, Brush strokeColor)
        {
            Line line = new Line
            {
                Stroke = strokeColor,
                StrokeThickness = 2,
                X1 = x1,
                X2 = x2,
                Y1 = y1,
                Y2 = y2,
            };
            drawBeamDiagram11.Children.Add(line);
        }
        private void PolyLineDash11()
        {
            Polyline poly = new Polyline
            {
                Stroke = Brushes.Red,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 10, 5 }
            };
            var point = new List<(double x, double y)>()
            {
                (0, 41),
                (138.5, 41),
                (138.5, 72),
                (295, 72),
            };
            foreach (var (x, y) in point)
            {
                poly.Points.Add(new Point(x, y));
            }
            drawBeamDiagram11.Children.Add(poly);
        }
        private void PolyLineDash1(double n, double m)
        {
            Polyline poly = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                StrokeDashArray = new DoubleCollection { 10, 5 }
            };
            var point = new List<(double x, double y)>()
            {
                (80, 0),
                (80, 88),
            };
            foreach (var (x, y) in point)
            {
                poly.Points.Add(new Point(x + n, y + m));
            }
            drawBeamDiagram11.Children.Add(poly);
        }
        // page 1 _ image 3 - 2025/03/12 - トン
        private List<Path> topReinforcementLines = new List<Path>();    // 
        private List<Path> bottomReinforcementLines = new List<Path>(); // 
        private void DrawBeamDiagramTon12() // 主筋アンカ長の指定
        {
            Canvas canvas = new Canvas { Width = 800, Height = 300, Background = Brushes.White };
            page1_3_canvas.Children.Clear();
            page1_3_canvas.Children.Add(canvas);

            // Vẽ các hình chữ nhật
            DrawRectangle1(canvas, 0, 100, 170, 170, Brushes.DarkGray);
            DrawRectangle1(canvas, 60, 40, 70, 80, Brushes.DarkGray);
            DrawRectangle1(canvas, 160, 100, 455, 140, Brushes.DarkGray);
            DrawRectangle1(canvas, 550 + 60, 100, 170, 170, Brushes.DarkGray);
            DrawRectangle1(canvas, 650, 40, 70, 80, Brushes.DarkGray);
            DrawRectangle1(canvas, 718 + 60, 100, 70, 140, Brushes.DarkGray);
            DrawRectangle1(canvas, 765 + 60, 190, 30, 55, Brushes.White);
            DrawRectangle1(canvas, 765 + 60, 99, 30, 40, Brushes.White);

            // Vẽ đường thép trên
            double lineStartX = 120, lineStartY = 115, lineEndX = 130, lineEndY = lineStartY;
            DrawStraightLine(canvas, 130, lineStartY, 825, lineEndY, 3, Brushes.Black);
            DrawStraightLine(canvas, lineStartX, lineStartY, lineEndX, lineEndY, 3, Brushes.Black);
            DrawQuarterCircle(canvas, lineStartX, lineStartY + 20, 20, 180, 3, Brushes.Black);
            DrawVerticalLine(canvas, lineStartX - 20, lineStartY + 70, lineStartY + 20, 3, Brushes.Black);
            DrawStraightLine(canvas, 100 + 115, 125, 565, 125, 3, Brushes.Black);

            // Vẽ đường thép dưới
            double lineStartX2 = 105, lineStartY2 = 225, lineEndX2 = 130, lineEndY2 = lineStartY2;
            DrawStraightLine(canvas, 130, lineStartY2, 825, lineEndY2, 3, Brushes.Black);
            DrawStraightLine(canvas, lineStartX2, lineStartY2, lineEndX2, lineEndY2, 3, Brushes.Black);
            DrawQuarterCircle(canvas, lineStartX2, lineStartY2 - 20, 20, 90, 3, Brushes.Black);
            DrawVerticalLine(canvas, lineStartX2 - 20, lineStartY2 - 20 - 50, lineStartY2 - 20, 3, Brushes.Black);

            DrawStraightLine(canvas, 470, lineStartY2 - 10, 825, lineEndY2 - 10, 3, Brushes.Black);
            DrawStraightLine(canvas, lineStartX2 + 5, lineStartY2 - 10, lineEndX2 + 170, lineEndY2 - 10, 3, Brushes.Black);
            DrawQuarterCircle(canvas, lineStartX2 + 5, lineStartY2 - 30, 20, 90, 3, Brushes.Black);
            DrawVerticalLine(canvas, lineStartX2 - 15, lineStartY2 - 80, lineStartY2 - 30, 3, Brushes.Black);

            // Vẽ các hình tam giác
            double bonusX = 690 + 60, X1 = 50 + bonusX, X2 = 260 + bonusX, X3 = 150 + bonusX;
            double bonusY = 30, Y1 = 150 + bonusY, Y2 = 260 + bonusY, Y3 = 100 + bonusY;
            DrawTriangle(canvas, X1, Y1, X2, Y2, X3, Y3, Brushes.White);

            double bonusXX = 315 + 60, XX1 = 450 + bonusXX, XX2 = 600 + bonusXX, XX3 = 475 + bonusXX;
            double bonusYY = 0, YY1 = 140 + bonusYY, YY2 = 240 + bonusYY, YY3 = 100 + bonusYY;
            DrawTriangle(canvas, XX1, YY1, XX2, YY2, XX3, YY3, Brushes.White);

            // Đường thẳng dọc đoạn cuối
            DrawVerticalLine(canvas, 765 + 60, 80, 140, 1, Brushes.Black);
            DrawStraightLine(canvas, 765 + 60, 140, 790 + 60, 155, 1, Brushes.Black);
            DrawStraightLine(canvas, 740 + 60, 180, 790 + 60, 155, 1, Brushes.Black);
            DrawStraightLine(canvas, 740 + 60, 180, 765 + 60, 195, 1, Brushes.Black);
            DrawVerticalLine(canvas, 765 + 60, 195, 260, 1, Brushes.Black);

            // Vẽ đường dọc nét đứt
            DrawDashedVerticalLine(canvas, 60, 100, 275, 2, Brushes.Black);
            DrawDashedVerticalLine(canvas, 130, 100, 275, 2, Brushes.Black);
            DrawDashedVerticalLine(canvas, 260, 60, 240, 2, Brushes.Black);
            DrawDashedVerticalLine(canvas, 520, 60, 240, 2, Brushes.Black);
            DrawDashedVerticalLine(canvas, 650, 100, 275, 2, Brushes.Black);

            // Các đường ngang mảnh dài
            DrawStraightLine(canvas, 130, 60, 650, 60, 1, Brushes.Black);
            DrawStraightLine(canvas, 110, 170, 670, 170, 2, Brushes.Black);
            DrawStraightLine(canvas, 730, 170, 820, 170, 2, Brushes.Black);

            // Đường ký hiệu độ dài
            lineGroups["外端中央3"].Add(DrawStraightLine(canvas, 260, 90, 305, 90, 2, Brushes.Black));
            lineGroups["外端中央3"].Add(DrawVerticalLine(canvas, 260, 85, 95, 2, Brushes.Black));
            lineGroups["外端中央3"].Add(DrawVerticalLine(canvas, 305, 85, 95, 2, Brushes.Black));
            lineGroups["內端中央3"].Add(DrawStraightLine(canvas, 475, 90, 520, 90, 2, Brushes.Black));
            lineGroups["內端中央3"].Add(DrawVerticalLine(canvas, 475, 85, 95, 2, Brushes.Black));
            lineGroups["內端中央3"].Add(DrawVerticalLine(canvas, 520, 85, 95, 2, Brushes.Black));
            lineGroups["外端端部3"].Add(DrawStraightLine(canvas, 215, 250, 260, 250, 2, Brushes.Black));
            lineGroups["外端端部3"].Add(DrawVerticalLine(canvas, 215, 245, 255, 2, Brushes.Black));
            lineGroups["外端端部3"].Add(DrawVerticalLine(canvas, 260, 245, 255, 2, Brushes.Black));
            lineGroups["內端端部3"].Add(DrawStraightLine(canvas, 520, 250, 565, 250, 2, Brushes.Black));
            lineGroups["內端端部3"].Add(DrawVerticalLine(canvas, 520, 245, 255, 2, Brushes.Black));
            lineGroups["內端端部3"].Add(DrawVerticalLine(canvas, 565, 245, 255, 2, Brushes.Black));

            // Thêm text
            DrawText(canvas, "L", 390, 35, Brushes.Black, 16);
            DrawText(canvas, "L/4", 180, 60, Brushes.Black, 16);
            DrawText(canvas, "L/2", 385, 60, Brushes.Black, 16);
            DrawText(canvas, "L/4", 570, 60, Brushes.Black, 16);
            DrawText(canvas, $"{外端中央3_textbox.Text}d", 280, 65, Brushes.Black, 16, TextAlignment.Center);
            DrawText(canvas, $"{內端中央3_textbox.Text}d", 495, 65, Brushes.Black, 16, TextAlignment.Center);
            DrawText(canvas, $"{外端端部3_textbox.Text}d", 235, 250, Brushes.Black, 16, TextAlignment.Center);
            DrawText(canvas, $"{內端端部3_textbox.Text}d", 540, 250, Brushes.Black, 16, TextAlignment.Center);

            // Thiết lập hiệu ứng các TextBox
            SetupTextBox(外端中央3_textbox, lineGroups["外端中央3"]);
            SetupTextBox(內端中央3_textbox, lineGroups["內端中央3"]);
            SetupTextBox(外端端部3_textbox, lineGroups["外端端部3"]);
            SetupTextBox(內端端部3_textbox, lineGroups["內端端部3"]);
            SetupTextBox3(AnkaNagaUwaTextBox);
            SetupTextBox3(AnkaNagaUwaChuTextBox);
            SetupTextBox3(AnkaNagaShitaChuTextBox);
            SetupTextBox3(AnkaNagaShitaTextBox);
        }
        private List<Shape> drawnShapes = new List<Shape>();

        private void ClearOldShapes(Canvas canvas)
        {
            foreach (var shape in drawnShapes)
            {
                canvas.Children.Remove(shape);
            }
            drawnShapes.Clear();
        }
        private void DrawNewShapes(Canvas canvas)
        {
            double lineStartX2 = 105, lineStartY2 = 225, lineEndY2 = lineStartY2;

            // Vẽ lại đường mới với màu đen
            var quarterCircle = DrawQuarterCircle(canvas, lineStartX2 + 5, lineStartY2 - 30, 20, 90, 3, Brushes.Black);
            var verticalLine = DrawVerticalLine(canvas, lineStartX2 - 15, lineStartY2 - 80, lineStartY2 - 30, 3, Brushes.Black);

            // Lưu trữ các đường đã vẽ
            drawnShapes.Add(quarterCircle);
            drawnShapes.Add(verticalLine);
        }
        private void SetupTextBox3(TextBox textBox)
        {

            // Hàm cập nhật màu và vẽ đường (nếu cần)
            void UpdateColorsAndLines(Brush background, bool drawLines = false)
            {
                textBox.Dispatcher.Invoke(() =>
                {
                    textBox.Background = background;
                    textBox.ClearValue(Control.BackgroundProperty); // Xóa giá trị mặc định nếu có
                    textBox.Background = background; // Áp dụng lại
                });

                if (drawLines)
                {
                    Canvas canvas = page1_3_canvas.Children.OfType<Canvas>().FirstOrDefault();
                    if (canvas != null)
                    {
                        // Vẽ 40d thép trên cho アンカ長_上筋 và アンカ長_上宙
                        if (textBox == AnkaNagaUwaTextBox || textBox == AnkaNagaUwaChuTextBox)
                        {
                            topReinforcementLines.Add(DrawStraightLine(canvas, 70, 115, 80, 115, 2, Brushes.Red));
                            topReinforcementLines.Add(DrawVerticalLine(canvas, 75, 115, 185, 2, Brushes.Red));
                            topReinforcementLines.Add(DrawStraightLine(canvas, 70, 185, 80, 185, 2, Brushes.Red));

                        }
                        // Vẽ 40d thép dưới cho アンカ長_下宙 và アンカ長_下筋
                        else if (textBox == AnkaNagaShitaTextBox)
                        {
                            bottomReinforcementLines.Add(DrawStraightLine(canvas, 70, 155, 80, 155, 2, Brushes.Red));
                            bottomReinforcementLines.Add(DrawVerticalLine(canvas, 75, 155, 225, 2, Brushes.Red));
                            bottomReinforcementLines.Add(DrawStraightLine(canvas, 70, 225, 80, 225, 2, Brushes.Red));

                        }
                        else if (textBox == AnkaNagaShitaChuTextBox)
                        {
                            bottomReinforcementLines.Add(DrawStraightLine(canvas, 70, 145, 80, 145, 2, Brushes.Yellow));
                            bottomReinforcementLines.Add(DrawVerticalLine(canvas, 75, 145, 215, 2, Brushes.Yellow));
                            bottomReinforcementLines.Add(DrawStraightLine(canvas, 70, 215, 80, 215, 2, Brushes.Yellow));
                            ClearOldShapes(canvas);
                            double lineStartX2 = 105, lineStartY2 = 225, lineEndY2 = lineStartY2;
                            DrawQuarterCircle(canvas, lineStartX2 + 5, lineStartY2 - 30, 20, 90, 3, Brushes.Yellow);
                            DrawVerticalLine(canvas, lineStartX2 - 15, lineStartY2 - 80, lineStartY2 - 30, 3, Brushes.Yellow);
                        }

                    }
                }

            }


            // Sự kiện khi focus
            textBox.GotFocus += (sender, e) =>
            {
                UpdateColorsAndLines(Brushes.LightGreen, true); // Đổi màu và vẽ đường
                if (textBox == AnkaNagaShitaChuTextBox)
                {
                    double lineStartX2 = 105, lineStartY2 = 225, lineEndY2 = lineStartY2;
                    DrawQuarterCircle(page1_3_canvas, lineStartX2 + 5, lineStartY2 - 30, 20, 90, 3, Brushes.Yellow);
                    DrawVerticalLine(page1_3_canvas, lineStartX2 - 15, lineStartY2 - 80, lineStartY2 - 30, 3, Brushes.Yellow);
                }
            };

            // Sự kiện khi mất focus
            textBox.LostFocus += (sender, e) =>
            {   // Xóa các đường cũ
                ClearOldShapes(page1_3_canvas);
                // Vẽ các đường mới
                DrawNewShapes(page1_3_canvas);
                UpdateColorsAndLines(Brushes.White); // Trả lại màu, không vẽ đường
                Canvas canvas = page1_3_canvas.Children.OfType<Canvas>().FirstOrDefault();
                if (canvas != null)
                {
                    // Xóa các đường thép trên
                    if (textBox == AnkaNagaUwaTextBox || textBox == AnkaNagaUwaChuTextBox)
                    {
                        foreach (var line in topReinforcementLines)
                        {
                            canvas.Children.Remove(line);
                        }
                        topReinforcementLines.Clear();
                    }
                    // Xóa các đường thép dưới
                    else if (textBox == AnkaNagaShitaChuTextBox || textBox == AnkaNagaShitaTextBox)
                    {
                        foreach (var line in bottomReinforcementLines)
                        {
                            canvas.Children.Remove(line);
                        }
                        bottomReinforcementLines.Clear();

                    }
                }
            };

            // Sự kiện khi con trỏ chuột di chuyển vào
            textBox.MouseEnter += (sender, e) =>
            {
                UpdateColorsAndLines(Brushes.LightGreen, true); // Đổi màu và vẽ đường khi hover
                if (textBox == AnkaNagaShitaChuTextBox)
                {
                    double lineStartX2 = 105, lineStartY2 = 225, lineEndY2 = lineStartY2;
                    DrawQuarterCircle(page1_3_canvas, lineStartX2 + 5, lineStartY2 - 30, 20, 90, 3, Brushes.Yellow);
                    DrawVerticalLine(page1_3_canvas, lineStartX2 - 15, lineStartY2 - 80, lineStartY2 - 30, 3, Brushes.Yellow);
                }

            };

            // Sự kiện khi con trỏ chuột rời khỏi
            textBox.MouseLeave += (sender, e) =>
            {                            // Xóa các đường cũ

                if (!textBox.IsFocused) // Chỉ trả lại màu nếu không focus
                {
                    ClearOldShapes(page1_3_canvas);
                    // Vẽ các đường mới
                    DrawNewShapes(page1_3_canvas);
                    UpdateColorsAndLines(Brushes.White); // Trả lại màu, không vẽ đường
                    Canvas canvas = page1_3_canvas.Children.OfType<Canvas>().FirstOrDefault();
                    if (canvas != null)
                    {
                        // Xóa các đường thép trên
                        if (textBox == AnkaNagaUwaTextBox || textBox == AnkaNagaUwaChuTextBox)
                        {
                            foreach (var line in topReinforcementLines)
                            {
                                canvas.Children.Remove(line);
                            }
                            topReinforcementLines.Clear();

                        }
                        // Xóa các đường thép dưới
                        else if (textBox == AnkaNagaShitaChuTextBox || textBox == AnkaNagaShitaTextBox)
                        {
                            foreach (var line in bottomReinforcementLines)
                            {
                                canvas.Children.Remove(line);
                            }
                            bottomReinforcementLines.Clear();

                        }
                    }
                }
            };

            // Sự kiện khi nội dung thay đổi
            textBox.TextChanged += (sender, e) =>
            {
                if (textBox.IsFocused) // Giữ màu khi đang nhập
                {
                    UpdateColorsAndLines(Brushes.LightGreen, true); // Giữ màu và đường
                }
            };

            // Sự kiện khi nhập liệu bằng phím
            textBox.PreviewKeyDown += (sender, e) =>
            {
                if (textBox.IsFocused) // Giữ màu khi chỉnh sửa
                {
                    UpdateColorsAndLines(Brushes.LightGreen, true); // Giữ màu và đường
                }
            };

            // Sự kiện khi thả phím
            textBox.KeyUp += (sender, e) =>
            {
                if (textBox.IsFocused)
                {
                    UpdateColorsAndLines(Brushes.LightGreen, true); // Giữ màu và đường
                }
            };
        }
        //######## end ##############
        private void DrawBeamDiagram13()
        {
            if (MainCanvas13 == null) return;

            MainCanvas13.Children.Clear();

            // Định nghĩa các cọ vẽ
            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush dimBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Blue);

            double scaleFactor = 1.4;

            // Vẽ hình đa giác đại diện cho bê tông
            Polygon concrete1 = new Polygon
            {
                Fill = concreteBrush,
                Points = new PointCollection
                {
                    ScalePoint(140, 50, scaleFactor), ScalePoint(180, 50, scaleFactor), ScalePoint(180, 90, scaleFactor),
                    ScalePoint(270, 120, scaleFactor), ScalePoint(270, 130, scaleFactor), ScalePoint(265, 135, scaleFactor),
                    ScalePoint(275, 145, scaleFactor), ScalePoint(270, 150, scaleFactor), ScalePoint(270, 160, scaleFactor),
                    ScalePoint(180, 130, scaleFactor), ScalePoint(180, 210, scaleFactor), ScalePoint(140, 210, scaleFactor),
                    ScalePoint(140, 130, scaleFactor), ScalePoint(50, 160, scaleFactor), ScalePoint(50, 150, scaleFactor),
                    ScalePoint(55, 145, scaleFactor), ScalePoint(45, 135, scaleFactor), ScalePoint(50, 130, scaleFactor),
                    ScalePoint(50, 120, scaleFactor), ScalePoint(140, 90, scaleFactor),
                }
            };

            // Vẽ các thanh thép thẳng đen cố định
            LineGeometry line1 = new LineGeometry(ScalePoint(50, 125, scaleFactor), ScalePoint(160, 90, scaleFactor));
            LineGeometry line2 = new LineGeometry(ScalePoint(160, 90, scaleFactor), ScalePoint(270, 125, scaleFactor));
            LineGeometry line3 = new LineGeometry(ScalePoint(50, 155, scaleFactor), ScalePoint(140, 125, scaleFactor));
            LineGeometry line4 = new LineGeometry(ScalePoint(180, 125, scaleFactor), ScalePoint(270, 155, scaleFactor));

            //Vẽ các đường zitzac trái

            PointCollection zitzac1 = new PointCollection
            {
                ScalePoint(50, 110, scaleFactor),
                ScalePoint(50, 130, scaleFactor),
                ScalePoint(45, 135, scaleFactor),
                ScalePoint(55, 145, scaleFactor),
                ScalePoint(50, 150, scaleFactor),
                ScalePoint(50, 170, scaleFactor),
            };

            //Vẽ các đường zitzac phải 
            PointCollection zitzac2 = new PointCollection
            {
                ScalePoint(270, 110, scaleFactor),
                ScalePoint(270, 130, scaleFactor),
                ScalePoint(265, 135, scaleFactor),
                ScalePoint(275, 145, scaleFactor),
                ScalePoint(270, 150, scaleFactor),
                ScalePoint(270, 170, scaleFactor),
            };

            Polyline polyline1 = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 0.7 * scaleFactor,
                Points = zitzac1
            };

            Polyline polyline2 = new Polyline
            {
                Stroke = Brushes.Black,
                StrokeThickness = 0.7 * scaleFactor,
                Points = zitzac2
            };

            // Vẽ thanh thép màu xanh dương
            Line dim1 = new Line
            {
                X1 = ScalePoint(140, 125, scaleFactor).X,
                Y1 = ScalePoint(140, 125, scaleFactor).Y,
                X2 = ScalePoint(180, 125, scaleFactor).X,
                Y2 = ScalePoint(180, 125, scaleFactor).Y,
                Stroke = textBrush,
                StrokeThickness = 1.5 * scaleFactor
            };
            // Vẽ 2 thanh thep đường màu đỏ góc nhọn
            Line dim2 = new Line
            {
                X1 = ScalePoint(140, 125, scaleFactor).X,
                Y1 = ScalePoint(140, 125, scaleFactor).Y,
                X2 = ScalePoint(160, 120, scaleFactor).X,
                Y2 = ScalePoint(160, 120, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1.5 * scaleFactor
            };
            Line dim3 = new Line
            {
                X1 = ScalePoint(160, 120, scaleFactor).X,
                Y1 = ScalePoint(160, 120, scaleFactor).Y,
                X2 = ScalePoint(180, 125, scaleFactor).X,
                Y2 = ScalePoint(180, 125, scaleFactor).Y,
                Stroke = dimBrush,
                StrokeThickness = 1.5 * scaleFactor
            };

            // Thêm các phần tử vào canvas
            MainCanvas13.Children.Add(concrete1);
            MainCanvas13.Children.Add(polyline1);
            MainCanvas13.Children.Add(polyline2);
            // Nhóm các hình học thép
            GeometryGroup steelGeometry = new GeometryGroup();
            steelGeometry.Children.Add(line1);
            steelGeometry.Children.Add(line2);
            steelGeometry.Children.Add(line3);
            steelGeometry.Children.Add(line4);
            //Nếu 1 vẽ xanh dương
            if (kobaichouten1.IsChecked == true)
            {
                MainCanvas13.Children.Add(dim1);
            }
            //Nếu 2 vẽ 2 đường màu đỏ
            else if (kobaichouten2.IsChecked == true)
            {
                MainCanvas13.Children.Add(dim2);
                MainCanvas13.Children.Add(dim3);
            }
            // Thêm đường thép vào canvas
            Path steelPath = new Path
            {
                Stroke = Brushes.Black,
                StrokeThickness = 1.5 * scaleFactor,
                Data = steelGeometry
            };
            // Thêm các phần tử vào canvas
            MainCanvas13.Children.Add(steelPath);

        }
        private Point ScalePoint(double x, double y, double scaleFactor, double offsetX, double offsetY)
        {
            double roundedX = Math.Round(x);
            double roundedY = Math.Round(y);
            return new Point(roundedX * scaleFactor + offsetX, -roundedY * scaleFactor + offsetY);
        }
        private void DrawBeamDiagram14()
        {
            if (MainCanvas14 == null) return;

            MainCanvas14.Children.Clear();

            // Định nghĩa các cọ vẽ
            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray); // Khối bê tông
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);   // Thanh thép
            SolidColorBrush concreteLineBrush = new SolidColorBrush(Colors.Black); // Đường viền bê tông

            double scaleFactor = 1.4;
            double offsetX = -500 * scaleFactor; // Dịch chuyển để tâm bản vẽ vào giữa Canvas
            double offsetY = 150 * scaleFactor;
            double concreteLineThickness = 0.25;  // Độ dày đường viền bê tông
            double steelLineThickness = 4.5;     // Độ dày thanh thép

            // Vẽ khối bê tông (chung cho cả hai option)
            DrawConcrete(concreteBrush, concreteLineBrush, scaleFactor, offsetX, offsetY, concreteLineThickness);

            // Vẽ thanh thép dựa trên option được chọn
            if (hanchishitakin1.IsChecked == true)
            {
                DrawSteelOption1(steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);
            }
            else if (hanchishitakin2.IsChecked == true)
            {
                DrawSteelOption2(steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);
            }
        }

        // Hàm vẽ khối bê tông và đường viền (chung)
        private void DrawConcrete(SolidColorBrush concreteBrush, SolidColorBrush lineBrush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            // Vẽ các khối bê tông (HATCH)
            DrawHatch(new double[] {
                589, -69, 589, -51, 589, -48, 589, -46, 589, -30, 589, -28,
                589, -25, 589, 31, 674, 102, 698, 72, 636, 19
            }, 11, concreteBrush, scaleFactor, offsetX, offsetY);

            DrawHatch(new double[] {
                589, 31, 529, 51, 529, -49, 589, -69, 589, -51, 589, -48,
                589, -46, 589, -30, 589, -28, 589, -25
            }, 10, concreteBrush, scaleFactor, offsetX, offsetY);

            DrawHatch(new double[] {
                589, 31, 674, 102, 596, 107, 529, 51
            }, 4, concreteBrush, scaleFactor, offsetX, offsetY);

        }

        // Hàm vẽ thanh thép cho Option1
        private void DrawSteelOption1(SolidColorBrush steelBrush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            // Vẽ các thanh thép (LINE)           
            DrawLine(572, -62, 641, 68, steelBrush, scaleFactor, offsetX, offsetY, thickness);
            DrawLine(630, 29, 691, 80, steelBrush, scaleFactor, offsetX, offsetY, thickness);
            DrawLine(594, 8, 686, 86, steelBrush, scaleFactor, offsetX, offsetY, thickness);
            DrawLine(581, -65, 630, 29.3, steelBrush, scaleFactor, offsetX, offsetY, thickness);
        }

        // Hàm vẽ thanh thép cho Option2
        private void DrawSteelOption2(SolidColorBrush steelBrush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            // Vẽ các thanh thép (LINE)          
            DrawLine(572, -62, 641, 68, steelBrush, scaleFactor, offsetX, offsetY, thickness);
            DrawLine(594, 8, 686, 86, steelBrush, scaleFactor, offsetX, offsetY, thickness);
            DrawLine(581, -65, 650, 66, steelBrush, scaleFactor, offsetX, offsetY, thickness);
            DrawLine(599, 3, 691, 80, steelBrush, scaleFactor, offsetX, offsetY, thickness);
        }

        // Hàm vẽ vùng tô (HATCH) cho khối bê tông
        private void DrawHatch(double[] coords, int pointCount, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY)
        {
            Polygon hatch = new Polygon
            {
                Fill = brush,
                Stroke = Brushes.Black,
                StrokeThickness = 0.5
            };

            PointCollection points = new PointCollection();
            for (int i = 0; i < pointCount * 2; i += 2)
            {
                Point scaledPoint = ScalePoint(coords[i], coords[i + 1], scaleFactor, offsetX, offsetY);
                points.Add(scaledPoint);
            }

            hatch.Points = points;
            MainCanvas14.Children.Add(hatch);
        }

        // Hàm vẽ đường thẳng (LINE) với độ dày tùy chỉnh
        private void DrawLine(double x1, double y1, double x2, double y2, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            Line line = new Line
            {
                Stroke = brush,
                StrokeThickness = thickness
            };

            Point start = ScalePoint(x1, y1, scaleFactor, offsetX, offsetY);
            Point end = ScalePoint(x2, y2, scaleFactor, offsetX, offsetY);

            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;

            MainCanvas14.Children.Add(line);
        }
        // Hình 地中梁の追い出し・追い終い筋の長さの設定 Bắt đầu
        private Dictionary<string, List<Shape>> lineGroupskha = new Dictionary<string, List<Shape>>
        {
            { "上筋 追い出し筋の長さ", new List<Shape>() }, //Hình 地中梁の追い出し・追い終い筋の長さの設定
            { "上筋 追い終い筋の長さ", new List<Shape>() },
            { "下筋 追い出し筋の長さ", new List<Shape>() },
            { "下筋 追い終い筋の長さ", new List<Shape>() },
            { "上筋 中間長さ 最短", new List<Shape>() },
            { "下筋 中間長さ 最短", new List<Shape>() },

            { "上筋 追い出し筋の長さ1", new List<Shape>() },//Hình 一般階大梁の追い出し・追い終い筋の長さの設定 
            { "上筋 追い終い筋の長さ1", new List<Shape>() },
            { "下筋 追い出し筋の長さ1", new List<Shape>() },
            { "下筋 追い終い筋の長さ1", new List<Shape>() },
            { "上筋 中間長さ 最短1", new List<Shape>() },
            { "下筋 中間長さ 最短1", new List<Shape>() },
        };
        private void DrawBeamDiagramKha15()
        {
            if (MainCanvas15 == null) return;

            MainCanvas15.Children.Clear();

            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush concreteLineBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Red);

            double scaleFactor = 4; // Tăng tỷ lệ để bản vẽ rõ hơn vì tọa độ nhỏ
            double offsetX = 100 * scaleFactor; // Dịch chuyển để căn giữa
            double offsetY = 100 * scaleFactor;
            double concreteLineThickness = 2.0;
            double steelLineThickness = 2.5;
            double zitzacLineThickness = 1;
            double circleThickness = 5;

            // Vẽ khối bê tông (chung cho cả hai option)
            DrawConcreteKha(concreteBrush, concreteLineBrush, scaleFactor, offsetX, offsetY, concreteLineThickness);

            // Các đường zitzac
            DrawLineKha(-7, 62, -4, 60, steelBrush, scaleFactor, offsetX, offsetY, zitzacLineThickness);
            DrawLineKha(-5, 63, 0, 60, steelBrush, scaleFactor, offsetX, offsetY, zitzacLineThickness);
            DrawLineKha(-7, 71, -7, 62, steelBrush, scaleFactor, offsetX, offsetY, zitzacLineThickness);
            DrawLineKha(-5, 71, -5, 63, steelBrush, scaleFactor, offsetX, offsetY, zitzacLineThickness);
            DrawLineKha(0, 60, -6, 57, steelBrush, scaleFactor, offsetX, offsetY, zitzacLineThickness);
            DrawLineKha(-4, 60, -10, 58, steelBrush, scaleFactor, offsetX, offsetY, zitzacLineThickness);
            DrawLineKha(-5, 46, -5, 53, steelBrush, scaleFactor, offsetX, offsetY, zitzacLineThickness);
            DrawLineKha(-3, 46, -3, 54, steelBrush, scaleFactor, offsetX, offsetY, zitzacLineThickness);
            DrawLineKha(-3, 54, -6, 57, steelBrush, scaleFactor, offsetX, offsetY, zitzacLineThickness);
            DrawLineKha(-5, 53, -10, 58, steelBrush, scaleFactor, offsetX, offsetY, zitzacLineThickness);

            // Thanh thép ngang/dọc (LINE) và (ARC) bo tròn ở góc  
            lineGroupskha["上筋 追い出し筋の長さ"].Add(DrawLineKha(-68, 66, -43, 66, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness)); //thanh số 1
            lineGroupskha["上筋 追い出し筋の長さ"].Add(DrawLineKha(-71, 56, -71, 63, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// thanh số 14
            lineGroupskha["上筋 追い出し筋の長さ"].Add(DrawArc(-68, 63, 3, 90, 180, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// góc số 1

            lineGroupskha["上筋 中間長さ 最短"].Add(DrawLineKha(-42, 66, -11, 66, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// thanh số 2
                                                                                                                                          // 
            DrawLineKha(-10, 66, -7, 66, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// thanh số 3

            DrawLineKha(-4, 66, 11, 66, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// thanh số 4

            lineGroupskha["上筋 追い終い筋の長さ"].Add(DrawLineKha(12, 66, 30, 66, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));//thanh số 5
            lineGroupskha["上筋 追い終い筋の長さ"].Add(DrawLineKha(33, 56, 33, 63, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));//thanh số 6
            lineGroupskha["上筋 追い終い筋の長さ"].Add(DrawArc(33, 66, 3, 270, 180, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// góc số 2

            lineGroupskha["下筋 追い終い筋の長さ"].Add(DrawLineKha(36, 62, 36, 53, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));//thanh số 7
            lineGroupskha["下筋 追い終い筋の長さ"].Add(DrawLineKha(7, 50, 33, 50, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// thanh số 8 
            lineGroupskha["下筋 追い終い筋の長さ"].Add(DrawArc(33, 53, 3, 269, 359, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// góc số 3

            DrawLineKha(-2, 50, 6, 50, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// thanh số 9
            DrawLineKha(-16, 50, -5, 50, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// thanh số 10

            lineGroupskha["下筋 中間長さ 最短"].Add(DrawLineKha(-49, 50, -17, 50, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// thanh số 11

            lineGroupskha["下筋 追い出し筋の長さ"].Add(DrawLineKha(-69, 50, -50, 50, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// thanh số 12
            lineGroupskha["下筋 追い出し筋の長さ"].Add(DrawLineKha(-72, 61, -72, 53, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness)); //thanh số 13
            lineGroupskha["下筋 追い出し筋の長さ"].Add(DrawArc(-69, 53, 3, 180, 270, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// góc số 4

            // TEXT
            DrawText("(1)", -64, 68, textBrush, scaleFactor, offsetX, offsetY);
            DrawText("(2)", -27, 68, textBrush, scaleFactor, offsetX, offsetY);
            DrawText("(5)", -33, 52, textBrush, scaleFactor, offsetX, offsetY);
            DrawText("(4)", -64, 52, textBrush, scaleFactor, offsetX, offsetY);
            DrawText("(3)", 25, 68, textBrush, scaleFactor, offsetX, offsetY);
            DrawText("(6)", 25, 52, textBrush, scaleFactor, offsetX, offsetY);

            // 円を描画
            DrawCircle(-42.5, 65, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness); // 1つ目
            DrawCircle(-10.5, 65, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness); // 2つ目
            DrawCircle(11.5, 65, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness);  // 3つ目
            DrawCircle(6.5, 49, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness);   // 4つ目
            DrawCircle(-15, 49, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness);   // 5つ目
            DrawCircle(-49.5, 49, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness); // 6つ目

            // Thiết lập hiệu ứng khi focus vào textbox
            SetupPanelKha(ChichuUwaDashiTextBox, chichudashiuwa1, chichudashiuwa2, lineGroupskha["上筋 追い出し筋の長さ"]);//Textbox Trái trên
            SetupPanelKha(ChichuUwaShimaiTextBox, chichushimaiuwa1, chichushimaiuwa2, lineGroupskha["上筋 追い終い筋の長さ"]);//Textbox Phải trên
            SetupPanelKha(ChichuShitaDashiTextBox, chichudashishita1, chichudashishita2, lineGroupskha["下筋 追い出し筋の長さ"]);//Textbox Trái dưới
            SetupPanelKha(ChichuShitaShimaiTextBox, chichushimaishita1, chichushimaishita2, lineGroupskha["下筋 追い終い筋の長さ"]);//Textbox Phải dưới

            SetupTextBoxKha(ChichuUwaNakaTanTextBox, lineGroupskha["上筋 中間長さ 最短"]);//Textbox giữa trên 1
            SetupTextBoxKha(ChichuUwaNakaChoTextBox, lineGroupskha["上筋 中間長さ 最短"]);//Textbox giữa trên 2
            SetupTextBoxKha(ChichuShitaNakaTanTextBox, lineGroupskha["下筋 中間長さ 最短"]);//Textbox giữa dưới 1
            SetupTextBoxKha(ChichuShitaNakaChoTextBox, lineGroupskha["下筋 中間長さ 最短"]);//Textbox giữa dưới 2

        }

        private void DrawConcreteKha(SolidColorBrush concreteBrush, SolidColorBrush lineBrush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            // HATCH 1 bê tông trái
            DrawHatchKha(new double[] {
                -62, 70, -7, 70, -7, 62, -4, 60, -10, 58, -5, 53, -5, 48,
                -52, 48, -52, 40, -77, 40, -77, 48, -82, 48, -82, 70,
                -77, 70, -77, 80, -62, 80, -62, 70
            }, 17, concreteBrush, scaleFactor, offsetX, offsetY);

            // HATCH 2 bê tông phải
            DrawHatchKha(new double[] {
                43, 48, 43, 70, 38, 70, 38, 80, 23, 80, 23, 70, -5, 70,
                -5, 63, 0, 60, -6, 57, -3, 54, -3, 48, 13, 48, 13, 39,
                38, 39, 38, 48
            }, 16, concreteBrush, scaleFactor, offsetX, offsetY);
        }

        private void DrawHatchKha(double[] coords, int pointCount, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY)
        {
            Polygon hatch = new Polygon
            {
                Fill = brush,
                Stroke = Brushes.Black,
                StrokeThickness = 0
            };

            PointCollection points = new PointCollection();
            for (int i = 0; i < pointCount * 2; i += 2)
            {
                Point scaledPoint = ScalePoint(coords[i], coords[i + 1], scaleFactor, offsetX, offsetY);
                points.Add(scaledPoint);
            }

            hatch.Points = points;
            MainCanvas15.Children.Add(hatch);
        }

        private Line DrawLineKha(double x1, double y1, double x2, double y2, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            Line line = new Line
            {
                Stroke = brush,
                StrokeThickness = thickness
            };

            Point start = ScalePoint(x1, y1, scaleFactor, offsetX, offsetY);
            Point end = ScalePoint(x2, y2, scaleFactor, offsetX, offsetY);

            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;

            MainCanvas15.Children.Add(line);
            return line; // Ensure the method returns a Line object
        }

        private Path DrawArc(double centerX, double centerY, double radius, double startAngle, double endAngle, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            // Pathオブジェクトを使用
            Path arc = new Path
            {
                Stroke = brush,
                StrokeThickness = thickness
            };

            // スケーリングと座標変換
            Point center = ScalePoint(centerX, centerY, scaleFactor, offsetX, offsetY);
            double scaledRadius = Math.Round(radius) * scaleFactor;

            // 開始角度と終了角度をラジアンに変換
            double startRad = startAngle * Math.PI / 180;
            double endRad = endAngle * Math.PI / 180;

            // 開始点と終了点を計算
            Point startPoint = new Point(
                center.X + scaledRadius * Math.Cos(startRad),
                center.Y - scaledRadius * Math.Sin(startRad));
            Point endPoint = new Point(
                center.X + scaledRadius * Math.Cos(endRad),
                center.Y - scaledRadius * Math.Sin(endRad));

            // 円弧が180度を超えるかどうかを判定
            bool isLargeArc = Math.Abs(endAngle - startAngle) > 180;

            // PathFigureとArcSegmentを作成
            PathFigure pathFigure = new PathFigure { StartPoint = startPoint };
            ArcSegment arcSegment = new ArcSegment
            {
                Point = endPoint,
                Size = new Size(scaledRadius, scaledRadius),
                SweepDirection = SweepDirection.Counterclockwise, // 必要に応じてClockwiseに変更可能
                IsLargeArc = isLargeArc
            };
            pathFigure.Segments.Add(arcSegment);

            // PathGeometryを設定
            PathGeometry pathGeometry = new PathGeometry();
            pathGeometry.Figures.Add(pathFigure);
            arc.Data = pathGeometry; // Dataプロパティに設定

            // キャンバスに追加
            MainCanvas15.Children.Add(arc);
            return arc; // Path型を返す
        }

        private void DrawText(string text, double x, double y, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 3 * scaleFactor
            };

            Point position = ScalePoint(x, y, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(textBlock, position.X);
            Canvas.SetTop(textBlock, position.Y - textBlock.FontSize); // Dịch lên để căn giữa

            MainCanvas15.Children.Add(textBlock);
        }
        private void DrawCircle(double centerX, double centerY, double radius, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            Ellipse circle = new Ellipse
            {
                Stroke = brush,
                StrokeThickness = thickness,
                Width = radius * 1.5 * scaleFactor,
                Height = radius * 1 * scaleFactor
            };

            Point center = ScalePoint(centerX, centerY, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(circle, center.X - radius * scaleFactor);
            Canvas.SetTop(circle, center.Y - radius * scaleFactor);

            MainCanvas15.Children.Add(circle);
        }
        private void SetupTextBoxKha(TextBox textBox, List<Shape> shapes)
        {
            void UpdateColors(Brush background, Brush stroke)
            {
                textBox.Dispatcher.Invoke(() =>
                {
                    textBox.Background = background;
                    textBox.ClearValue(Control.BackgroundProperty); // Xóa giá trị mặc định nếu có
                    textBox.Background = background; // Áp dụng lại
                    foreach (var shape in shapes)
                    {
                        shape.Stroke = stroke;
                    }
                });
            }
            textBox.GotFocus += (sender, e) =>
            {
                UpdateColors(Brushes.LightGreen, Brushes.Red);
            };
            textBox.LostFocus += (sender, e) =>
            {
                UpdateColors(Brushes.White, Brushes.Black);
            };
            textBox.MouseEnter += (sender, e) =>
            {
                UpdateColors(Brushes.LightGreen, Brushes.Red);
            };
            textBox.MouseLeave += (sender, e) =>
            {
                if (!textBox.IsFocused)
                {
                    UpdateColors(Brushes.White, Brushes.Black);
                }
            };
            textBox.TextChanged += (sender, e) =>
            {
                if (textBox.IsFocused)
                {
                    UpdateColors(Brushes.LightGreen, Brushes.Red);
                }
            };
            textBox.PreviewKeyDown += (sender, e) =>
            {
                if (textBox.IsFocused)
                {
                    UpdateColors(Brushes.LightGreen, Brushes.Red);
                }
            };
            textBox.KeyUp += (sender, e) =>
            {
                if (textBox.IsFocused)
                {
                    UpdateColors(Brushes.LightGreen, Brushes.Red);
                }
            };

        }
        private void tsugite1_Checked(object sender, RoutedEventArgs e)
        {
            DrawBeamDiagram6();
            TonariOoTextBox.IsEnabled = false;
            TonariKoTextBox.IsEnabled = false;
            TonariOoTextBox.Text = "0";
            TonariKoTextBox.Text = "0";
        }
        private void tsugite2_Checked(object sender, RoutedEventArgs e)
        {
            DrawBeamDiagram6();
            TonariOoTextBox.IsEnabled = true;
            TonariKoTextBox.IsEnabled = true;
        }


        private void chichudashiuwa1_Checked(object sender, RoutedEventArgs e)
        {
            ChichuUwaDashiTextBox.IsEnabled = false;
        }
        private void chichudashiuwa2_Checked(object sender, RoutedEventArgs e)
        {
            // Enable the TextBox when chichudashiuwa2 is checked
            ChichuUwaDashiTextBox.IsEnabled = true;
        }
        private void chichudashishita1_Checked(object sender, RoutedEventArgs e)
        {
            ChichuShitaDashiTextBox.IsEnabled = false;
        }
        private void chichudashishita2_Checked(object sender, RoutedEventArgs e)
        {
            // Enable the TextBox when chichudashishita2 is checked
            ChichuShitaDashiTextBox.IsEnabled = true;
        }
        private void chichushimaiuwa1_Checked(object sender, RoutedEventArgs e)
        {
            ChichuUwaShimaiTextBox.IsEnabled = false;
        }
        private void chichushimaiuwa2_Checked(object sender, RoutedEventArgs e)
        {
            // Enable the TextBox when chichushimaiuwa2 is checked
            ChichuUwaShimaiTextBox.IsEnabled = true;
        }
        private void chichushimaishita1_Checked(object sender, RoutedEventArgs e)
        {
            ChichuShitaShimaiTextBox.IsEnabled = false;
        }
        private void chichushimaishita2_Checked(object sender, RoutedEventArgs e)
        {
            // Enable the TextBox when chichushimaishita2 is checked
            ChichuShitaShimaiTextBox.IsEnabled = true;
        }

        private void SetupPanelKha(TextBox textBox, RadioButton radioButton1, RadioButton radioButton2, List<Shape> lines)
        {
            // Hàm cập nhật màu
            void UpdateColors(Brush background, Brush stroke)
            {
                // Ép buộc cập nhật màu
                textBox.Dispatcher.Invoke(() =>
                {
                    textBox.Background = background;
                    textBox.ClearValue(Control.BackgroundProperty); // Xóa giá trị mặc định nếu có
                    textBox.Background = background; // Áp dụng lại
                    foreach (var line in lines)
                    {
                        line.Stroke = stroke;
                    }
                });
            }

            textBox.GotFocus += (sender, e) =>
            {
                UpdateColors(Brushes.LightGreen, Brushes.Red);
            };


            textBox.LostFocus += (sender, e) =>
            {
                UpdateColors(Brushes.White, Brushes.Black);
            };


            textBox.MouseEnter += (sender, e) =>
            {
                UpdateColors(Brushes.LightGreen, Brushes.Red); // Luôn đổi màu khi hover
            };


            textBox.MouseLeave += (sender, e) =>
            {
                if (!textBox.IsFocused) // Chỉ trả lại màu nếu không focus
                {
                    UpdateColors(Brushes.White, Brushes.Black);
                }
            };

            // Sự kiện khi nội dung thay đổi
            textBox.TextChanged += (sender, e) =>
            {
                if (textBox.IsFocused) // Giữ màu khi đang nhập
                {
                    UpdateColors(Brushes.LightGreen, Brushes.Red);
                }
            };

            // Sự kiện khi nhập liệu bằng phím
            textBox.PreviewKeyDown += (sender, e) =>
            {
                if (textBox.IsFocused) // Giữ màu khi chỉnh sửa
                {
                    UpdateColors(Brushes.LightGreen, Brushes.Red);
                }
            };

            // Sự kiện khi thả phím (đảm bảo màu sau khi nhập)
            textBox.KeyUp += (sender, e) =>
            {
                if (textBox.IsFocused)
                {
                    UpdateColors(Brushes.LightGreen, Brushes.Red);
                }
            };

            // Sự kiện khi nhấn radio button chichudashiuwa1
            radioButton1.Unchecked += (sender, e) =>
            {
                UpdateColors(Brushes.White, Brushes.Black);
            };
            radioButton1.MouseLeave += (sender, e) =>
            {               
                UpdateColors(Brushes.White, Brushes.Black);                                
            };
            radioButton1.MouseEnter += (sender, e) =>
            {
                UpdateColors(Brushes.White, Brushes.Red);
            };
            // Sự kiện khi nhấn radio button chichudashiuwa2
            radioButton2.Checked += (sender, e) =>
            {
                UpdateColors(Brushes.LightGreen, Brushes.Red);
            };
            radioButton2.Unchecked += (sender, e) =>
            {
                UpdateColors(Brushes.White, Brushes.Black);
            };
            radioButton2.MouseEnter += (sender, e) =>
            {
                UpdateColors(Brushes.LightGreen, Brushes.Red);
            };
            radioButton2.MouseLeave += (sender, e) =>
            {
                if (!textBox.IsFocused)
                {
                    UpdateColors(Brushes.White, Brushes.Black);
                }
            };


        }
       
        private void DrawBeamDiagramPhat()
        {
            drawBeamDiagramPhat.Children.Clear();
            drawBeamDiagramPhat.Width = 319;
            drawBeamDiagramPhat.Height = 200;

            Polygon polygon = new Polygon
            {
                Fill = new SolidColorBrush(Colors.LightGray),
                Stroke = Brushes.Black,
                StrokeThickness = 1,
                Points = new PointCollection
                 {
                     new Point(0,0),
                     new Point(80,0),
                     new Point(80,-27),
                     new Point(197,-27),
                     new Point(197,-14),
                     new Point(295,-14),
                     new Point(295,-17),
                     new Point(295,20),
                     new Point(283,25),
                     new Point(307,30),
                     new Point(295,35),
                     new Point(295,35),
                     new Point(295,69),
                     new Point(295,64),
                     new Point(197,64),
                     new Point(197,127),
                     new Point(80,127),
                     new Point(80,78),
                     new Point(0,78),
                     new Point(0,83),
                     new Point(0,45),
                     new Point(12,40),
                     new Point(-12,35),
                     new Point(0,30),
                     new Point(0,-5),
                 }
            };
            drawBeamDiagramPhat.Children.Add(polygon);
            PolyLinePhat(0);
            PolyLinePhat(58);
            LinePhat(80, 125, 68, 68, Brushes.Red);
            LinePhat(150, 197, 54, 54, Brushes.Red);
            LinePhat(138, 138, 54, 68, Brushes.Red);
            LinePhat(130, 146, 68, 68, Brushes.Red);
            LinePhat(130, 146, 54, 54, Brushes.Red);

            TextBlock textBlock = new TextBlock
            {
                Text = $"{DansaKoteisaTextBox.Text}",
                FontSize = 14,
                Foreground = Brushes.Black,
            };
            Canvas.SetLeft(textBlock, 150);
            Canvas.SetTop(textBlock, 50);
            drawBeamDiagramPhat.Children.Add(textBlock);
        }
        private void LinePhat(double x1, double x2, double y1, double y2, Brush strokeColor)
        {
            Line line = new Line
            {
                Stroke = strokeColor,
                StrokeThickness = 2,
                X1 = x1,
                X2 = x2,
                Y1 = y1,
                Y2 = y2,
            };
            drawBeamDiagramPhat.Children.Add(line);
        }
        private void PolyLinePhat(double n)
        {
            Polyline poly = new Polyline
            {

                Stroke = Brushes.Black,
                StrokeThickness = 2,
                Points = new PointCollection
                 {
                     new Point(0,10+n),
                     new Point(80,10+n),
                     new Point(80,10+n),
                     new Point(197,-4+n),
                     new Point(295,-4+n),
                 }
            };
            drawBeamDiagramPhat.Children.Add(poly);
        }
        private void DrawBeamDiagram1Phat()
        {
            drawBeamDiagram1Phat.Children.Clear();
            drawBeamDiagram1Phat.Width = 300;
            drawBeamDiagram1Phat.Height = 275;

            double scale = 5.65;
            double xOffset = 31.85;
            double yOffset = 24.46;
            Color black = Colors.Black;
            // Vẽ HATCH
            Polygon hatchPolygon = new Polygon
            {
                Fill = new SolidColorBrush(Colors.LightGray),
                Points = new PointCollection
                {
                    new Point((66.51180199107087 - xOffset) * scale, (yOffset - 24.15800943923977) * scale),
                    new Point((66.51180199107084 - xOffset) * scale, (yOffset - -24.46353041046207) * scale),
                    new Point((31.8521931829448 - xOffset) * scale, (yOffset - -24.46353041046207) * scale),
                    new Point((31.85219318294481 - xOffset) * scale, (yOffset - 24.15800943923976) * scale)
                }
            };
            drawBeamDiagram1Phat.Children.Add(hatchPolygon);

            // Layer 1
            lineGroups["phat"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 49.12343957303784, 17.80774989683441, 49.12343957303784, 23.77660522693072, black));
            lineGroups["phat"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 49.12343957303783, -24.12017359123223, 49.12343957303783, -18.15131826113592, black));
            lineGroups["phat"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 49.12343957303784, 23.77660522693072, 47.1724048374724, 23.77660522693072, black));
            lineGroups["phat"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 51.07447430860329, 23.77660522693072, 49.12343957303784, 23.77660522693072, black));
            lineGroups["phat"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 49.12343957303784, 17.80774989683441, 47.1724048374724, 17.80774989683441, black));
            lineGroups["phat"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 51.07447430860329, 17.80774989683441, 49.12343957303784, 17.80774989683441, black));
            lineGroups["phat"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 49.12343957303783, -18.15131826113592, 47.17240483747238, -18.15131826113592, black));
            lineGroups["phat"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 51.07447430860327, -18.15131826113592, 49.12343957303783, -18.15131826113592, black));
            lineGroups["phat"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 49.12343957303783, -24.07658483994222, 47.17240483747238, -24.07658483994222, black));
            lineGroups["phat"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 51.07447430860327, -24.07658483994222, 49.12343957303783, -24.07658483994222, black));

            // Layer 2
            lineGroups["phat1"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 60.16296239122443, -0.1316574966033217, 66.13181772132074, -0.1316574966033217, black));
            lineGroups["phat1"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 32.23483761403637, -0.1316574966033182, 38.20369294413271, -0.1316574966033182, black));
            lineGroups["phat1"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 60.16296239122443, -0.1316574966033217, 60.16296239122443, 1.819377238962122, black));
            lineGroups["phat1"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 60.16296239122443, -2.082692232168761, 60.16296239122443, -0.1316574966033217, black));
            lineGroups["phat1"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 66.13181772132074, -0.1316574966033253, 66.13181772132074, 1.819377238962118, black));
            lineGroups["phat1"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 66.13181772132074, -2.082692232168765, 66.13181772132074, -0.1316574966033253, black));
            lineGroups["phat1"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 32.23483761403637, -0.1316574966033182, 32.23483761403637, 1.819377238962126, black));
            lineGroups["phat1"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 32.23483761403637, -2.082692232168758, 32.23483761403637, -0.1316574966033182, black));
            lineGroups["phat1"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 38.20369294413271, -0.1316574966033217, 38.20369294413271, 1.819377238962122, black));
            lineGroups["phat1"].Add(AddLine(drawBeamDiagram1Phat, scale, xOffset, yOffset, 38.20369294413271, -2.082692232168761, 38.20369294413271, -0.1316574966033217, black));

            // Vẽ LINE từ mã cũ
            AddLine1(drawBeamDiagram1Phat, scale, xOffset, yOffset, 42.42, 16.81, 46.43, 13.30, Colors.Black);
            AddLine1(drawBeamDiagram1Phat, scale, xOffset, yOffset, 41.20, -17.15, 49.20, -17.15, Colors.Black);
            AddLine1(drawBeamDiagram1Phat, scale, xOffset, yOffset, 59.16, -15.11, 59.16, 14.85, Colors.Black);
            AddLine1(drawBeamDiagram1Phat, scale, xOffset, yOffset, 39.20, -15.19, 39.20, 14.77, Colors.Black);
            AddLine1(drawBeamDiagram1Phat, scale, xOffset, yOffset, 49.20, -17.15, 57.20, -17.15, Colors.Black);
            AddLine1(drawBeamDiagram1Phat, scale, xOffset, yOffset, 41.12, 16.81, 49.12, 16.81, Colors.Black);
            AddLine1(drawBeamDiagram1Phat, scale, xOffset, yOffset, 49.12, 16.81, 57.12, 16.81, Colors.Black);
            AddLine1(drawBeamDiagram1Phat, scale, xOffset, yOffset, 39.31, 13.23, 43.32, 9.72, Colors.Black);

            // Vẽ ARC (giữ nguyên từ mã cũ, điều chỉnh ARC 1 và ARC 4 theo DXF)
            AddArc(drawBeamDiagram1Phat, scale, xOffset, yOffset, 57.16, 14.81, 2.0, 1.15, 91.15, false);  // ARC 1, reversed
            AddArc(drawBeamDiagram1Phat, scale, xOffset, yOffset, 57.16, -15.15, 2.0, 271.15, 1.15, false); // ARC 2
            AddArc(drawBeamDiagram1Phat, scale, xOffset, yOffset, 41.20, 14.77, 2.0, 91.15, 181.15, false); // ARC 3
            AddArc(drawBeamDiagram1Phat, scale, xOffset, yOffset, 41.20, -15.15, 2.0, 181.15, 271.15, false); // ARC 4, reversed

            SetupTextBox(ChichuOoJogeKaburiTextBox, lineGroups["phat"]);
            SetupTextBox(ChichuKataOoJogeKaburiTextBox, lineGroups["phat"]);
            SetupTextBox(IppanOoJogeKaburiTextBox, lineGroups["phat"]);
            SetupTextBox(IppanKataOoJogeKaburiTextBox, lineGroups["phat"]);
            SetupTextBox(KabeChichuJogeKaburiTextBox, lineGroups["phat"]);
            SetupTextBox(ChichuKoJogeKaburiTextBox, lineGroups["phat"]);
            SetupTextBox(ChichuKataKoJogeKaburiTextBox, lineGroups["phat"]);
            SetupTextBox(IppanKoJogeKaburiTextBox, lineGroups["phat"]);
            SetupTextBox(IppanKataKoJogeKaburiTextBox, lineGroups["phat"]);
            SetupTextBox(KabeIppanJogeKaburiTextBox, lineGroups["phat"]);



            SetupTextBox(ChichuOoSayuKaburiTextBox, lineGroups["phat1"]);
            SetupTextBox(ChichuKataOoSayuKaburiTextBox, lineGroups["phat1"]);
            SetupTextBox(IppanOoSayuKaburiTextBox, lineGroups["phat1"]);
            SetupTextBox(IppanKataOoSayuKaburiTextBox, lineGroups["phat1"]);
            SetupTextBox(KabeChichuSayuKaburiTextBox, lineGroups["phat1"]);
            SetupTextBox(ChichuKoSayuKaburiTextBox, lineGroups["phat1"]);
            SetupTextBox(ChichuKataKoSayuKaburiTextBox, lineGroups["phat1"]);
            SetupTextBox(IppanKoSayuKaburiTextBox, lineGroups["phat1"]);
            SetupTextBox(IppanKataKoSayuKaburiTextBox, lineGroups["phat1"]);
            SetupTextBox(KabeIppanSayuKaburiTextBox, lineGroups["phat1"]);
        }

        private Path AddLine(Canvas canvas, double scale, double xOffset, double yOffset,
                             double x1, double y1, double x2, double y2, Color color)
        {
            Line line = new Line
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2, // Đặt lại độ dày từ DXF (370 = 50 -> 0.5mm, điều chỉnh nếu cần)
                X1 = (x1 - xOffset) * scale,
                Y1 = (yOffset - y1) * scale,
                X2 = (x2 - xOffset) * scale,
                Y2 = (yOffset - y2) * scale
            };
            canvas.Children.Add(line);

            // Wrap the Line in a Path to return the correct type
            Path path = new Path
            {
                Data = new LineGeometry(new Point(line.X1, line.Y1), new Point(line.X2, line.Y2)),
                Stroke = line.Stroke,
                StrokeThickness = line.StrokeThickness
            };
            canvas.Children.Add(path);
            return path;
        }
        private void AddLine1(Canvas canvas, double scale, double xOffset, double yOffset,
                             double x1, double y1, double x2, double y2, Color color)
        {
            Line line = new Line
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 5, // Đặt lại độ dày từ DXF (370 = 50 -> 0.5mm, điều chỉnh nếu cần)
                X1 = (x1 - xOffset) * scale,
                Y1 = (yOffset - y1) * scale,
                X2 = (x2 - xOffset) * scale,
                Y2 = (yOffset - y2) * scale
            };
            canvas.Children.Add(line);
        }

        private void AddArc(Canvas canvas, double scale, double xOffset, double yOffset,
                            double centerX, double centerY, double radius, double startAngle, double endAngle, bool isReversed)
        {
            Path arcPath = new Path
            {
                Stroke = new SolidColorBrush(Colors.Black),
                StrokeThickness = 5
            };
            PathGeometry pathGeometry = new PathGeometry();
            PathFigure pathFigure = new PathFigure();

            double startRad = startAngle * Math.PI / 180;
            double endRad = endAngle * Math.PI / 180;

            double xStart = (centerX - xOffset + radius * Math.Cos(startRad)) * scale;
            double yStart = (yOffset - (centerY + radius * Math.Sin(startRad))) * scale;
            double xEnd = (centerX - xOffset + radius * Math.Cos(endRad)) * scale;
            double yEnd = (yOffset - (centerY + radius * Math.Sin(endRad))) * scale;

            pathFigure.StartPoint = new Point(xStart, yStart);

            double sweepAngle = endAngle - startAngle;
            if (!isReversed)
            {
                if (sweepAngle < 0) sweepAngle += 360;
            }
            else
            {
                if (sweepAngle > 0) sweepAngle -= 360;
            }
            bool isLargeArc = Math.Abs(sweepAngle) > 180;
            SweepDirection sweepDirection = isReversed ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

            ArcSegment arcSegment = new ArcSegment
            {
                Point = new Point(xEnd, yEnd),
                Size = new Size(radius * scale, radius * scale),
                SweepDirection = sweepDirection,
                IsLargeArc = isLargeArc
            };
            pathFigure.Segments.Add(arcSegment);
            pathGeometry.Figures.Add(pathFigure);
            arcPath.Data = pathGeometry;
            canvas.Children.Add(arcPath);
        }

        private void DrawBeamDiagramKha16()
        {
            if (MainCanvasKha16 == null) return;

            MainCanvasKha16.Children.Clear();

            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush concreteLineBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Red);

            double scaleFactor = 4; // Tăng tỷ lệ để bản vẽ rõ hơn vì tọa độ nhỏ
            double offsetX = 100 * scaleFactor; // Dịch chuyển thép
            double offsetY = 100 * scaleFactor;
            double offsetX1 = 80.5 * scaleFactor; // Dịch chuyển khối be tông, do không khớp với thép
            double offsetY1 = 81 * scaleFactor;
            double concreteLineThickness = 2.0;
            double steelLineThickness = 2.5;
            double zitzacLineThickness = 1;
            double circleThickness = 5;

            // Vẽ khối bê tông (chung cho cả hai option)
            DrawConcreteKha16(concreteBrush, concreteLineBrush, scaleFactor, offsetX1, offsetY1, concreteLineThickness);


            // Các đường zitzac
            DrawLineKha16(12.66589774001699, 42.56294711979265, 15.80853748624435, 40.78706620415463, steelBrush, scaleFactor, offsetX1, offsetY1, zitzacLineThickness);
            DrawLineKha16(14.66589774001699, 43.56294712012053, 19.80853748624435, 40.78706620415372, steelBrush, scaleFactor, offsetX1, offsetY1, zitzacLineThickness);
            DrawLineKha16(12.66589774001699, 51.78744802441221, 12.66589774001699, 42.56294712012053, steelBrush, scaleFactor, offsetX1, offsetY1, zitzacLineThickness);
            DrawLineKha16(14.66589774001699, 51.78744836122016, 14.66589774001699, 43.56294712012053, steelBrush, scaleFactor, offsetX1, offsetY1, zitzacLineThickness);
            DrawLineKha16(19.80853748624435, 40.78706620415372, 13.20941054089094, 37.67557362379151, steelBrush, scaleFactor, offsetX1, offsetY1, zitzacLineThickness);
            DrawLineKha16(15.80853748624435, 40.78706620415463, 9.837904767182735, 38.04592511583188, steelBrush, scaleFactor, offsetX1, offsetY1, zitzacLineThickness);
            DrawLineKha16(14.69483332118802, 26.7248702061067, 14.69483332118802, 33.43779215712541, steelBrush, scaleFactor, offsetX1, offsetY1, zitzacLineThickness);
            DrawLineKha16(16.69483332118802, 26.72487087972261, 16.69483332118802, 34.43779215712619, steelBrush, scaleFactor, offsetX1, offsetY1, zitzacLineThickness);
            DrawLineKha16(16.69483332118984, 34.43779215712451, 13.20941054089094, 37.67557362379151, steelBrush, scaleFactor, offsetX1, offsetY1, zitzacLineThickness);
            DrawLineKha16(14.69483332118802, 33.43779215712541, 9.837904767182735, 38.04592511583188, steelBrush, scaleFactor, offsetX1, offsetY1, zitzacLineThickness);
            // Thanh thép ngang/dọc (LINE) và (ARC) bo tròn ở góc  
            lineGroupskha["上筋 追い出し筋の長さ1"].Add(DrawLineKha16(-68, 66, -43, 66, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness)); //thanh số 1
            lineGroupskha["上筋 追い出し筋の長さ1"].Add(DrawLineKha16(-71, 56, -71, 63, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// thanh số 14
            lineGroupskha["上筋 追い出し筋の長さ1"].Add(DrawArc16(-68, 63, 3, 90, 180, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// góc số 1

            lineGroupskha["上筋 中間長さ 最短1"].Add(DrawLineKha16(-42, 66, -11, 66, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// thanh số 2
                                                                                                                                             // 
            DrawLineKha16(-10, 66, -7, 66, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// thanh số 3

            DrawLineKha16(-4, 66, 11, 66, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// thanh số 4

            lineGroupskha["上筋 追い終い筋の長さ1"].Add(DrawLineKha16(12, 66, 30, 66, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));//thanh số 5
            lineGroupskha["上筋 追い終い筋の長さ1"].Add(DrawLineKha16(33, 56, 33, 63, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));//thanh số 6
            lineGroupskha["上筋 追い終い筋の長さ1"].Add(DrawArc16(33, 66, 3, 270, 180, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// góc số 2

            lineGroupskha["下筋 追い終い筋の長さ1"].Add(DrawLineKha16(36, 62, 36, 53, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));//thanh số 7
            lineGroupskha["下筋 追い終い筋の長さ1"].Add(DrawLineKha16(7, 50, 33, 50, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// thanh số 8 
            lineGroupskha["下筋 追い終い筋の長さ1"].Add(DrawArc16(33, 53, 3, 269, 359, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// góc số 3

            DrawLineKha16(-2, 50, 6, 50, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// thanh số 9
            DrawLineKha16(-16, 50, -5, 50, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// thanh số 10

            lineGroupskha["下筋 中間長さ 最短1"].Add(DrawLineKha16(-49, 50, -17, 50, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// thanh số 11

            lineGroupskha["下筋 追い出し筋の長さ1"].Add(DrawLineKha16(-69, 50, -50, 50, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// thanh số 12
            lineGroupskha["下筋 追い出し筋の長さ1"].Add(DrawLineKha16(-72, 61, -72, 53, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness)); //thanh số 13
            lineGroupskha["下筋 追い出し筋の長さ1"].Add(DrawArc16(-69, 53, 3, 180, 270, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness));// góc số 4

            // TEXT
            DrawText16("(1)", -64, 68, textBrush, scaleFactor, offsetX, offsetY);
            DrawText16("(2)", -27, 68, textBrush, scaleFactor, offsetX, offsetY);
            DrawText16("(5)", -33, 52, textBrush, scaleFactor, offsetX, offsetY);
            DrawText16("(4)", -64, 52, textBrush, scaleFactor, offsetX, offsetY);
            DrawText16("(3)", 25, 68, textBrush, scaleFactor, offsetX, offsetY);
            DrawText16("(6)", 25, 52, textBrush, scaleFactor, offsetX, offsetY);

            // 円を描画
            DrawCircle16(-42.5, 65, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness); // 1つ目
            DrawCircle16(-10.5, 65, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness); // 2つ目
            DrawCircle16(11.5, 65, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness);  // 3つ目
            DrawCircle16(6.5, 49, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness);   // 4つ目
            DrawCircle16(-15, 49, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness);   // 5つ目
            DrawCircle16(-49.5, 49, 2, steelBrush, scaleFactor, offsetX, offsetY, circleThickness); // 6つ目

            // Thiết lập hiệu ứng khi focus vào textbox
            SetupPanelKha(IppanUwaDashiTextBox, ippandashiuwa1, ippandashiuwa2, lineGroupskha["上筋 追い出し筋の長さ1"]);
            SetupPanelKha(IppanUwaShimaiTextBox, ippanshimaiuwa1, ippanshimaiuwa2, lineGroupskha["上筋 追い終い筋の長さ1"]);
            SetupPanelKha(IppanShitaDashiTextBox, ippandashishita1, ippandashishita2, lineGroupskha["下筋 追い出し筋の長さ1"]);
            SetupPanelKha(IppanShitaShimaiTextBox, ippanshimaishita1, ippanshimaishita2, lineGroupskha["下筋 追い終い筋の長さ1"]);

            SetupTextBoxKha(IppanUwaNakaTanTextBox, lineGroupskha["上筋 中間長さ 最短1"]);
            SetupTextBoxKha(IppanUwaNakaChoTextBox, lineGroupskha["上筋 中間長さ 最短1"]);
            SetupTextBoxKha(IppanShitaNakaTanTextBox, lineGroupskha["下筋 中間長さ 最短1"]);
            SetupTextBoxKha(IppanShitaNakaChoTextBox, lineGroupskha["下筋 中間長さ 最短1"]);

        }
        private void ippandashiuwa1_Checked(object sender, RoutedEventArgs e)
        {
            IppanUwaDashiTextBox.IsEnabled = false;
        }
        private void ippandashiuwa2_Checked(object sender, RoutedEventArgs e)
        {
            // Enable the TextBox when chichudashiuwa2 is checked
            IppanUwaDashiTextBox.IsEnabled = true;
        }
        private void ippandashishita1_Checked(object sender, RoutedEventArgs e)
        {
            IppanShitaDashiTextBox.IsEnabled = false;
        }
        private void ippandashishita2_Checked(object sender, RoutedEventArgs e)
        {
            // Enable the TextBox when chichudashishita2 is checked
            IppanShitaDashiTextBox.IsEnabled = true;
        }
        private void ippanshimaiuwa1_Checked(object sender, RoutedEventArgs e)
        {
            IppanUwaShimaiTextBox.IsEnabled = false;
        }
        private void ippanshimaiuwa2_Checked(object sender, RoutedEventArgs e)
        {
            // Enable the TextBox when chichushimaiuwa2 is checked
            IppanUwaShimaiTextBox.IsEnabled = true;
        }
        private void ippanshimaishita1_Checked(object sender, RoutedEventArgs e)
        {
            IppanShitaShimaiTextBox.IsEnabled = false;
        }
        private void ippanshimaishita2_Checked(object sender, RoutedEventArgs e)
        {
            // Enable the TextBox when chichushimaishita2 is checked
            IppanShitaShimaiTextBox.IsEnabled = true;
        }

        private void DrawConcreteKha16(SolidColorBrush concreteBrush, SolidColorBrush lineBrush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            // HATCH 1（左側のコンクリート）
            DrawHatchKha16(new double[]
            {
                14.69483332118802, 33.43779215712541,
                9.837904767182735, 38.04592511583188,
                15.80853748624435, 40.78706620415463,
                12.66589774001699, 42.56294711979265,
                12.66589774001699, 50.01194892870387,
                -42.54787806895961, 50.01193033226448,
                -42.54787806895961, 60.01193033226448,
                -57.54787806895961, 60.01193033226448,
                -57.54787806895961, 20.0117762514021,
                -42.54787806895962, 20.0117762514021,
                -42.54787806895962, 28.50036963862296,
                14.69483332118802, 28.50036963862297
            }, 12, concreteBrush, scaleFactor, offsetX, offsetY);

            // HATCH 2（右側のコンクリート）
            DrawHatchKha16(new double[]
            {
                42.47605379111584, 28.50036963862297,
                16.69483332118802, 28.50036963862297,
                16.69483332118802, 34.43779215712541,
                13.20941054089094, 37.67557362379151,
                19.80853748624435, 40.78706620415372,
                14.66589774001699, 43.56294712012053,
                14.66589774001699, 50.0119496023198,
                42.47605379111584, 50.01195896900182,
                42.47605379111585, 60.01195896900182,
                57.49038140675005, 60.01193033230857,
                57.49038140675005, 19.98804103099818,
                42.47605379111584, 19.98804103099818
            }, 12, concreteBrush, scaleFactor, offsetX, offsetY);
        }
        private void DrawHatchKha16(double[] coords, int pointCount, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY)
        {
            Polygon hatch = new Polygon
            {
                Fill = brush,
                Stroke = Brushes.Black,
                StrokeThickness = 0
            };

            PointCollection points = new PointCollection();
            for (int i = 0; i < pointCount * 2; i += 2)
            {
                Point scaledPoint = ScalePoint(coords[i], coords[i + 1], scaleFactor, offsetX, offsetY);
                points.Add(scaledPoint);
            }

            hatch.Points = points;
            MainCanvasKha16.Children.Add(hatch);
        }

        private Line DrawLineKha16(double x1, double y1, double x2, double y2, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            Line line = new Line
            {
                Stroke = brush,
                StrokeThickness = thickness
            };

            Point start = ScalePoint(x1, y1, scaleFactor, offsetX, offsetY);
            Point end = ScalePoint(x2, y2, scaleFactor, offsetX, offsetY);

            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;

            MainCanvasKha16.Children.Add(line);
            return line; // Ensure the method returns a Line object
        }

        private Path DrawArc16(double centerX, double centerY, double radius, double startAngle, double endAngle, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            // Pathオブジェクトを使用
            Path arc = new Path
            {
                Stroke = brush,
                StrokeThickness = thickness
            };

            // スケーリングと座標変換
            Point center = ScalePoint(centerX, centerY, scaleFactor, offsetX, offsetY);
            double scaledRadius = Math.Round(radius) * scaleFactor;

            // 開始角度と終了角度をラジアンに変換
            double startRad = startAngle * Math.PI / 180;
            double endRad = endAngle * Math.PI / 180;

            // 開始点と終了点を計算
            Point startPoint = new Point(
                center.X + scaledRadius * Math.Cos(startRad),
                center.Y - scaledRadius * Math.Sin(startRad));
            Point endPoint = new Point(
                center.X + scaledRadius * Math.Cos(endRad),
                center.Y - scaledRadius * Math.Sin(endRad));

            // 円弧が180度を超えるかどうかを判定
            bool isLargeArc = Math.Abs(endAngle - startAngle) > 180;

            // PathFigureとArcSegmentを作成
            PathFigure pathFigure = new PathFigure { StartPoint = startPoint };
            ArcSegment arcSegment = new ArcSegment
            {
                Point = endPoint,
                Size = new Size(scaledRadius, scaledRadius),
                SweepDirection = SweepDirection.Counterclockwise, // 必要に応じてClockwiseに変更可能
                IsLargeArc = isLargeArc
            };
            pathFigure.Segments.Add(arcSegment);

            // PathGeometryを設定
            PathGeometry pathGeometry = new PathGeometry();
            pathGeometry.Figures.Add(pathFigure);
            arc.Data = pathGeometry; // Dataプロパティに設定

            // キャンバスに追加
            MainCanvasKha16.Children.Add(arc);
            return arc; // Path型を返す
        }

        private void DrawText16(string text, double x, double y, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 3 * scaleFactor
            };

            Point position = ScalePoint(x, y, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(textBlock, position.X);
            Canvas.SetTop(textBlock, position.Y - textBlock.FontSize); // Dịch lên để căn giữa

            MainCanvasKha16.Children.Add(textBlock);
        }
        private void DrawCircle16(double centerX, double centerY, double radius, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            Ellipse circle = new Ellipse
            {
                Stroke = brush,
                StrokeThickness = thickness,
                Width = radius * 1.5 * scaleFactor,
                Height = radius * 1 * scaleFactor
            };

            Point center = ScalePoint(centerX, centerY, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(circle, center.X - radius * scaleFactor);
            Canvas.SetTop(circle, center.Y - radius * scaleFactor);

            MainCanvasKha16.Children.Add(circle);
        } // Kết thúc hình 一般階大梁の追い出し・追い終い筋の長さの設定

        // ######## Bắt đầu vẽ hình ハンチ点の補強スタラップ ########
        private Point ScalePointKha17(double x, double y, double scaleFactor, double canvasHeight, double offsetX, double offsetY)
        {
            double scaledX = Math.Round(x * scaleFactor + offsetX);
            double scaledY = Math.Round(canvasHeight - y * scaleFactor + offsetY);
            return new Point(scaledX, scaledY);
        }

        // DXFの色番号をWPFの色に変換
        private SolidColorBrush GetColorFromDXFColor(int dxfColor)
        {
            switch (dxfColor)
            {
                case 1: return new SolidColorBrush(Colors.Red);
                case 5: return new SolidColorBrush(Colors.Blue);
                case 9: return new SolidColorBrush(Colors.LightGray);
                case 18: return new SolidColorBrush(Colors.Black);
                default: return new SolidColorBrush(Colors.Black);
            }
        }

        // 直線を描画するヘルパー関数
        private void DrawLineKha17(Canvas canvas, double x1, double y1, double x2, double y2, int color, double scaleFactor, double canvasHeight, double offsetX, double offsetY, bool isDashed = false)
        {
            Line line = new Line
            {
                X1 = ScalePointKha17(x1, y1, scaleFactor, canvasHeight, offsetX, offsetY).X,
                Y1 = ScalePointKha17(x1, y1, scaleFactor, canvasHeight, offsetX, offsetY).Y,
                X2 = ScalePointKha17(x2, y2, scaleFactor, canvasHeight, offsetX, offsetY).X,
                Y2 = ScalePointKha17(x2, y2, scaleFactor, canvasHeight, offsetX, offsetY).Y,
                Stroke = GetColorFromDXFColor(color),
                StrokeThickness = 2
            };

            // 破線を設定（必要に応じて）
            if (isDashed)
            {
                line.StrokeDashArray = new DoubleCollection { 6, 2 }; // 4単位の実線、2単位の空白
            }

            canvas.Children.Add(line);
        }

        // 円弧を描画する関数（PathFigureとArcSegmentを使用）
        private void DrawArcKha17(Canvas canvas, double startX, double startY, double endX, double endY, double radius, int color, double scaleFactor, double canvasHeight, double offsetX, double offsetY)
        {
            PathFigure arcFigure = new PathFigure
            {
                StartPoint = ScalePointKha17(startX, startY, scaleFactor, canvasHeight, offsetX, offsetY)
            };
            arcFigure.Segments.Add(new ArcSegment(
                ScalePointKha17(endX, endY, scaleFactor, canvasHeight, offsetX, offsetY),
                new Size(radius * scaleFactor, radius * scaleFactor),
                0, // RotationAngle (今回は0で固定)
                false, // IsLargeArc (小さい円弧を使用)
                SweepDirection.Counterclockwise, // 反時計回りで外側カーブ
                true // IsStroked
            ));

            PathGeometry arcGeometry = new PathGeometry();
            arcGeometry.Figures.Add(arcFigure);

            Path path = new Path
            {
                Data = arcGeometry,
                Stroke = GetColorFromDXFColor(color),
                StrokeThickness = 2
            };

            canvas.Children.Add(path);
        }

        // テキストを描画する関数
        private void DrawTextKha17(Canvas canvas, string text, double x, double y, double height, SolidColorBrush brush, double scaleFactor, double canvasHeight, double offsetX, double offsetY)
        {
            var scaledPoint = ScalePointKha17(x, y, scaleFactor, canvasHeight, offsetX, offsetY);
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = height * scaleFactor
            };
            Canvas.SetLeft(textBlock, scaledPoint.X);
            Canvas.SetTop(textBlock, scaledPoint.Y);
            canvas.Children.Add(textBlock);
        }

        // ビューポートを設定する関数
        private void SetupViewportKha17(Canvas canvas, double centerX, double centerY, double width, double height, double scaleFactor, double offsetX, double offsetY)
        {
            canvas.Width = width * scaleFactor;
            canvas.Height = height * scaleFactor;
            Canvas.SetLeft(canvas, centerX * scaleFactor - canvas.Width / 2 + offsetX);
            Canvas.SetTop(canvas, centerY * scaleFactor - canvas.Height / 2 + offsetY);
        }

        // ハッチング、線、円弧、テキストを描画
        private void DrawBeamDiagramKha17()
        {
            if (MainCanvasKha17 == null) return;

            MainCanvasKha17.Children.Clear();

            // 定義されたブラシ
            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush dimBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Blue);

            // スケールファクターとオフセットの設定
            double scaleFactor = 2.5;
            double offsetX = -100 * scaleFactor;
            double offsetY = -150 * scaleFactor;

            double canvasHeight = 500; // Canvasの高さ（仮定）

            // 1. HATCH（ハッチング）
            Polygon concrete1 = new Polygon
            {
                Fill = concreteBrush,
                Points = new PointCollection
                {
                    ScalePointKha17(160, 30, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(130, 30, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(130, 60, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(160, 60, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(210, 60, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(210, 78, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(240, 78, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(240, -7, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(210, -7, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(210, 11, scaleFactor, canvasHeight, offsetX, offsetY)
                }
            };
            MainCanvasKha17.Children.Add(concrete1);

            Polygon concrete2 = new Polygon
            {
                Fill = concreteBrush,
                Points = new PointCollection
                {
                    ScalePointKha17(240, -123, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(210, -123, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(210, -86, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(130, -86, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(130, -56, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(160, -56, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(210, -56, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(210, -38, scaleFactor, canvasHeight, offsetX, offsetY),
                    ScalePointKha17(240, -38, scaleFactor, canvasHeight, offsetX, offsetY)
                }
            };
            MainCanvasKha17.Children.Add(concrete2);

            // 2. LINE（直線）
            (int x1, int y1, int x2, int y2, int color)[] lines = new[]
            {
                (162, 32, 131, 32, 18), // khối trên (thép đen ngắn ngang)
                (232, 57, 131, 57, 18), // khối trên (thép đen dài ngang)
                (162, 32, 233, 5, 18),   // khối trên (thép chéo)
                (138, 57, 138, 32, 5),   // khối trên (dọc 1)
                (150, 56, 150, 32, 5),   // khối trên (dọc 2)
                (162, 56, 162, 32, 1),   // khối trên (dọc 3)
                (210, 57, 210, 14, 5),   // khối trên (dọc 7)
                (174, 57, 174, 28, 5),   // khối trên (dọc 4)
                (186, 57, 186, 23, 5),   // khối trên (dọc 5)
                (198, 57, 198, 19, 5),   // khối trên (dọc 6)
                (235, 54, 235, 44, 18),  // khối trên (móc trên)
                (235, 3, 235, -7, 18),   // khối trên (móc dưới)

                (230, -59, 131, -59, 18), // khối dưới (thanh ngang trên)
                (232, -84, 131, -84, 18), // khối dưới (thanh ngang dưới)
                (233, -62, 233, -72, 18), // khối dưới (móc trên)
                (235, -81, 235, -71, 18), // khối dưới (móc dưới)
                (138, -59, 138, -84, 5),  // khối dưới (dọc 1)
                (150, -59, 150, -84, 5),  // khối dưới (dọc 2)
                (162, -59, 162, -84, 1),  // khối dưới (dọc 3)
                (210, -59, 210, -84, 5),  // khối dưới (dọc 4)
                (174, -59, 174, -84, 5),  // khối dưới (dọc 5)
                (186, -59, 186, -84, 5),  // khối dưới (dọc 6)
                (206, -59, 206, -84, 1),  // khối dưới (dọc 7) - 破線に変更
                (198, -59, 198, -84, 5),  // khối dưới (dọc 8)

                (198, -50, 131, -50, 18), // ビッチ thanh ngang
                (138, -45, 138, -55, 5),  // ビッチ thanh dọc 1
                (174, -45, 174, -55, 5),  // ビッチ thanh dọc 4
                (186, -45, 186, -55, 5),  // ビッチ thanh dọc 5
                (198, -45, 198, -55, 5),  // ビッチ dọc 6
                (150, -45, 150, -55, 5),  // ビッチ dọc 2
                (162, -45, 162, -55, 5),  // ビッチ dọc 3

                (210, -91, 206, -91, 1),  // giữa đường dim
                (206, -91, 206, -89, 1),  // dim trái trên
                (206, -93, 206, -91, 1),  // dim trái dưới
                (210, -91, 210, -89, 1),  // dim phải trên
                (210, -93, 210, -91, 1),  // dim phải dưới
            };

            // 直線をループで描画
            foreach (var line in lines)
            {
                DrawLineKha17(MainCanvasKha17, line.x1, line.y1, line.x2, line.y2, line.color, scaleFactor, canvasHeight, offsetX, offsetY, line.x1 == 206 && line.x2 == 206); // (206, -59, 206, -84) のみを破線に
            }

            // 3. ARC（円弧）
            (int startX, int startY, int endX, int endY, int radius, int color)[] arcs = new[]
            {
                (235, 54, 232, 57, 3, 18), // ARC 1: 接続 (232, 57, 131, 57) と (235, 54, 235, 44)
                (235, 3, 233, 5, 3, 18),   // ARC 2: 接続 (162, 32, 233, 5) と (235, 3, 235, -7)
                (233, -62, 230, -59, 3, 18), // ARC 3: 接続 (230, -59, 131, -59) と (233, -62, 233, -72)
                (232, -84, 235, -81, 3, 18) // ARC 4: 接続 (232, -84, 131, -84) と (235, -81, 235, -71)
            };

            // 円弧をループで描画
            foreach (var arc in arcs)
            {
                DrawArcKha17(MainCanvasKha17, arc.startX, arc.startY, arc.endX, arc.endY, arc.radius, arc.color, scaleFactor, canvasHeight, offsetX, offsetY);
            }
            // 4. TEXT（テキスト）
            DrawTextKha17(MainCanvasKha17, "@ビッチ", 155, -35, 5, textBrush, scaleFactor, canvasHeight, offsetX, offsetY);

            // 5. VIEWPORT（ビューポート）
            SetupViewportKha17(MainCanvasKha17, 129, 98, 314, 222, scaleFactor, offsetX, offsetY);
        }
        // ########## Kết thúc vẽ hình ハンチ点の補強スタラップ ##########

        // ########## Bắt đầu vẽ hình 腹筋の設定 ##########
        private void DrawBeamDiagramKha18()
        {
            if (MainCanvasKha18 == null) return;

            MainCanvasKha18.Children.Clear();

            // 定義されたブラシ
            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray); // ハッチング用
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);       // 線分用
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Blue);         // テキスト用（未使用）

            // スケールファクターとオフセットの設定
            double scaleFactor = 3;
            double offsetX = 0 * scaleFactor;    // X軸のオフセット
            double offsetY = 70 * scaleFactor; // Y軸のオフセット

            // 1. HATCH（ハッチング）の描画
            Polygon hatchPolygon = new Polygon
            {
                Fill = concreteBrush,
                Stroke = steelBrush,
                StrokeThickness = 0
            };

            PointCollection hatchPoints = new PointCollection();
            // DXFデータからハッチングの境界座標を追加
            double[,] hatchCoords = new double[,]
            {
                { 57.37328293414508, 43.14994966026446 },
                { 137.3972147942205, 43.1499782970018 },
                { 137.3972147942205, 53.1499782970018 },
                { 152.4115424098547, 53.14994966030856 },
                { 152.4115424098547, 13.12606035899817 },
                { 137.3972147942205, 13.12606035899816 },
                { 137.3972147942205, 21.63838896662295 },
                { 57.37328293414508, 21.63838896662294 },
                { 57.37328293414508, 13.14979557940208 },
                { 42.37328293414508, 13.14979557940209 },
                { 42.37328293414508, 53.14994966026445 },
                { 57.37328293414508, 53.14994966026446 }
            };

            for (int i = 0; i < hatchCoords.GetLength(0); i++)
            {
                Point scaledPoint = ScalePoint(hatchCoords[i, 0], hatchCoords[i, 1], scaleFactor, offsetX, offsetY);
                hatchPoints.Add(scaledPoint);
            }

            hatchPolygon.Points = hatchPoints;
            MainCanvasKha18.Children.Add(hatchPolygon);

            // 2. LINE（線分）の描画
            double[,] lineCoords = new double[,]
            {
                // 線分1
                { 54.5, 27.0, 100.5, 27.0 },
                // 線分2
                { 90.5050679821863, 25.74419038210308, 141.5050679821863, 25.74419038210308 },
                // 線分3
                { 54.5, 38.0, 100.5, 38.0 },
                // 線分4
                { 90.5050679821863, 36.74419038210308, 141.5050679821863, 36.74419038210308 }
            };

            for (int i = 0; i < lineCoords.GetLength(0); i++)
            {
                Point startPoint = ScalePoint(lineCoords[i, 0], lineCoords[i, 1], scaleFactor, offsetX, offsetY);
                Point endPoint = ScalePoint(lineCoords[i, 2], lineCoords[i, 3], scaleFactor, offsetX, offsetY);

                Line line = new Line
                {
                    X1 = startPoint.X,
                    Y1 = startPoint.Y,
                    X2 = endPoint.X,
                    Y2 = endPoint.Y,
                    Stroke = steelBrush,
                    StrokeThickness = 2
                };

                MainCanvasKha18.Children.Add(line);
            }
        }
        // ########## Kết thúc vẽ hình 腹筋の設定 ##########

        // Ton 2025/03/13 
        private List<Path> change_trai = new List<Path>(); // Đổi thành List<Path>
        private List<Path> change_phai = new List<Path>(); // Đổi thành List<Path>
        private const double Scale = 5;
        private const double XOffset = 127.60 * Scale;
        private const double YOffset = 70 * Scale;

        private void DrawBeamDiagramTon19() // ２つ割りスタラップ
        {
            Canvas canvas = new Canvas { Width = 200 * Scale, Height = 200 * Scale, Background = Brushes.White };
            Canvas19.Children.Clear();
            Canvas19.Children.Add(canvas);

            // Vẽ nền
            canvas.Children.Add(new Rectangle
            {
                Width = Math.Round((168.56 - 127.60) * Scale, 2),
                Height = Math.Round((68.82 - 17.90) * Scale, 2),
                Fill = Brushes.LightGray,
                Margin = new Thickness(Math.Round(127.60 * Scale - XOffset, 2), Math.Round(YOffset - 68.82 * Scale, 2), 0, 0)
            });

            // Tạo GeometryGroup để gộp tất cả đường thẳng và cung tròn
            GeometryGroup geometryGroup = new GeometryGroup();
            AddLinesToGeometry(geometryGroup);
            AddArcsToGeometry(geometryGroup);
            SetupTextBox19(STPTsuTextBox);
            SetupTextBox19(NakagoTsuTextBox);

            // Vẽ toàn bộ bằng một Path duy nhất
            canvas.Children.Add(new Path
            {
                Stroke = Brushes.Black,
                StrokeThickness = 3,
                Data = geometryGroup
            });
        }
        private void AddLinesToGeometry(GeometryGroup group)
        {
            var lines = new (double x1, double y1, double x2, double y2)[]
            {
                (136.64, 23.90, 158.60, 23.90), (144.97, 26.8, 144.96, 59.82),
                (150.15, 57.32, 150.15, 59.82), (150.15, 29.60, 150.15, 26.8),
                (150.15, 46.18, 150.15, 48.68), (150.15, 40.46, 150.15, 37.96),
                (134.60, 37.96, 134.59, 48.68), (139.78, 46.18, 139.78, 48.68),
                (139.78, 40.46, 139.78, 37.96), (155.38, 46.18, 155.38, 48.68),
                (155.37, 40.46, 155.37, 37.96), (160.56, 37.96, 160.56, 48.68),
                (160.56, 25.86, 160.56, 61), (134.60, 25.86, 134.60, 60.78),
                (136.56, 62.82, 158.52, 62.82)
            };

            foreach (var (x1, y1, x2, y2) in lines)
            {
                group.Children.Add(new LineGeometry(
                    new Point(Math.Round(x1 * Scale - XOffset, 2), Math.Round(YOffset - y1 * Scale, 2)),
                    new Point(Math.Round(x2 * Scale - XOffset, 2), Math.Round(YOffset - y2 * Scale, 2))));
            }
        }

        private void AddArcsToGeometry(GeometryGroup group)
        {
            var arcs = new (double cx, double cy, double r, double start, double end, bool rev)[]
            {
                (158.52, 60.82, 2.00, 1.15, 91.15, false), (147.55, 59.82, 2.59, 0.00, 180.00, false),
                (147.56, 26.8, 2.59, 180.00, 0.00, true), (147.55, 48.68, 2.59, 0.00, 180.00, false),
                (147.56, 37.96, 2.59, 180.00, 0.00, true), (137.19, 48.68, 2.59, 0.00, 180.00, false),
                (137.19, 37.96, 2.59, 180.00, 0.00, true), (157.97, 48.68, 2.59, 0.00, 180.00, true),
                (157.96, 37.96, 2.59, 180.00, 0.00, false), (158.56, 25.94, 2.00, 271.15, 1.15, false),
                (136.60, 60.82, 2.00, 91.15, 181.15, false), (136.60, 25.90, 2.00, 181.15, 271.15, true)
            };

            for (int i = 0; i < arcs.Length; i++)
            {
                var (cx, cy, r, start, end, rev) = arcs[i];
                double cxScaled = Math.Round(cx * Scale - XOffset, 2);
                double cyScaled = Math.Round(YOffset - cy * Scale, 2);
                double rScaled = Math.Round(r * Scale, 2);
                double t1 = rev && (i == 2 || i == 4 || i == 6 || i == 7) ? end : start;
                double t2 = rev && (i == 2 || i == 4 || i == 6 || i == 7) ? start : end;

                if (i == 11 && t2 < t1) t2 += 360;
                else if (t2 < t1 && !(i == 2 || i == 4 || i == 6 || i == 7)) t2 += 360;
                else if (t2 > t1 && (i == 2 || i == 4 || i == 6 || i == 7)) t2 -= 360;

                double sx = Math.Round(cxScaled + rScaled * Math.Cos(t1 * Math.PI / 180), 2);
                double sy = Math.Round(cyScaled - rScaled * Math.Sin(t1 * Math.PI / 180), 2);
                double ex = Math.Round(cxScaled + rScaled * Math.Cos(t2 * Math.PI / 180), 2);
                double ey = Math.Round(cyScaled - rScaled * Math.Sin(t2 * Math.PI / 180), 2);

                group.Children.Add(new PathGeometry
                {
                    Figures = { new PathFigure
                    {
                        StartPoint = new Point(sx, sy),
                        Segments = { new ArcSegment
                        {
                            Point = new Point(ex, ey),
                            Size = new Size(rScaled, rScaled),
                            SweepDirection = t2 > t1 ? SweepDirection.Counterclockwise : SweepDirection.Clockwise,
                            IsLargeArc = Math.Abs(t2 - t1) > 180
                        }}
                    }}
                });
            }
        }

        private void SetupTextBox19(TextBox textBox)
        {
            void UpdateColorsAndLines(Brush background, bool drawLines = false)
            {
                textBox.Dispatcher.Invoke(() =>
                {
                    textBox.Background = background;
                    textBox.ClearValue(Control.BackgroundProperty);
                    textBox.Background = background;
                });

                if (drawLines)
                {
                    Canvas canvas = Canvas19.Children.OfType<Canvas>().FirstOrDefault();
                    if (canvas != null)
                    {
                        if (textBox == STPTsuTextBox)
                        {
                            change_trai.Add(DrawStraightLine(canvas, 60, 94, 80, 94, 2, Brushes.Red));
                            change_trai.Add(DrawVerticalLine(canvas, 70, 94, 174, 2, Brushes.Red));
                            change_trai.Add(DrawStraightLine(canvas, 60, 174, 80, 174, 2, Brushes.Red));
                        }
                        else if (textBox == NakagoTsuTextBox)
                        {
                            change_phai.Add(DrawStraightLine(canvas, 170, 94, 190, 94, 2, Brushes.Red));
                            change_phai.Add(DrawVerticalLine(canvas, 180, 94, 174, 2, Brushes.Red));
                            change_phai.Add(DrawStraightLine(canvas, 170, 174, 190, 174, 2, Brushes.Red));
                        }
                    }
                }
            }

            textBox.GotFocus += (sender, e) => UpdateColorsAndLines(Brushes.LightGreen, true);
            textBox.LostFocus += (sender, e) =>
            {
                UpdateColorsAndLines(Brushes.White);
                Canvas canvas = Canvas19.Children.OfType<Canvas>().FirstOrDefault();
                if (canvas != null)
                {
                    if (textBox == STPTsuTextBox)
                    {
                        foreach (var line in change_trai) canvas.Children.Remove(line);
                        change_trai.Clear();
                    }
                    else if (textBox == NakagoTsuTextBox)
                    {
                        foreach (var line in change_phai) canvas.Children.Remove(line);
                        change_phai.Clear();
                    }
                }
            };

            textBox.MouseEnter += (sender, e) => UpdateColorsAndLines(Brushes.LightGreen, true);
            textBox.MouseLeave += (sender, e) =>
            {
                if (!textBox.IsFocused)
                {
                    UpdateColorsAndLines(Brushes.White);
                    Canvas canvas = Canvas19.Children.OfType<Canvas>().FirstOrDefault();
                    if (canvas != null)
                    {
                        if (textBox == STPTsuTextBox)
                        {
                            foreach (var line in change_trai) canvas.Children.Remove(line);
                            change_trai.Clear();
                        }
                        else if (textBox == NakagoTsuTextBox)
                        {
                            foreach (var line in change_phai) canvas.Children.Remove(line);
                            change_phai.Clear();
                        }
                    }
                }
            };

            textBox.TextChanged += (sender, e) =>
            {
                if (textBox.IsFocused) UpdateColorsAndLines(Brushes.LightGreen, true);
            };
            textBox.PreviewKeyDown += (sender, e) =>
            {
                if (textBox.IsFocused) UpdateColorsAndLines(Brushes.LightGreen, true);
            };
            textBox.KeyUp += (sender, e) =>
            {
                if (textBox.IsFocused) UpdateColorsAndLines(Brushes.LightGreen, true);
            };
        }
        // Ton 2025/03/12
        private TextBlock[] numberTextBlocks;
        private void DrawBeamDiagramTon20() // 梁の主筋の位置の設定
        {
            double scale = 0.8; // Biến scale để điều chỉnh tỷ lệ
            double horizontalShift = -150; // Biến điều chỉnh vị trí trái-phải (ngang)
            double verticalShift = -50;  // Biến điều chỉnh vị trí cao-thấp (dọc)

            numberTextBlocks = new TextBlock[4]; // Khởi tạo mảng 4 phần tử
            Canvas canvas = new Canvas { Width = 800 * scale, Height = 200 * scale, Background = Brushes.White };
            Canvas15.Children.Clear();
            Canvas15.Children.Add(canvas);

            double xOffsetStep = 70;

            // Truyền scale, horizontalShift, verticalShift vào DrawSingleDiagram
            numberTextBlocks[0] = DrawSingleDiagram(canvas, 0 * xOffsetStep, "①", 48, -55, 35.30, 32.30, -50, -53, scale, horizontalShift, verticalShift);
            numberTextBlocks[1] = DrawSingleDiagram(canvas, 1 * xOffsetStep, "②", 48, -55, 35.30, 32.30, -43.30, -46.30, scale, horizontalShift, verticalShift);
            numberTextBlocks[2] = DrawSingleDiagram(canvas, 2 * xOffsetStep, "③", 48, -55, 28.30, 25.30, -50, -53, scale, horizontalShift, verticalShift);
            numberTextBlocks[3] = DrawSingleDiagram(canvas, 3 * xOffsetStep, "④", 48, -55, 28.30, 25.30, -43.30, -46.30, scale, horizontalShift, verticalShift);

            // Kiểm tra RadioButton để đổi màu (giả định các RadioButton này tồn tại trong XAML)
            if (梁の主筋の位置1 != null && 梁の主筋の位置1.IsChecked == true) numberTextBlocks[0].Foreground = Brushes.Red;
            else if (梁の主筋の位置2 != null && 梁の主筋の位置2.IsChecked == true) numberTextBlocks[1].Foreground = Brushes.Red;
            else if (梁の主筋の位置3 != null && 梁の主筋の位置3.IsChecked == true) numberTextBlocks[2].Foreground = Brushes.Red;
            else if (梁の主筋の位置4 != null && 梁の主筋の位置4.IsChecked == true) numberTextBlocks[3].Foreground = Brushes.Red;
        }

        private TextBlock DrawSingleDiagram(Canvas canvas, double xOffset, string number, double textTopY, double numberY, double heightY1, double heightY2, double heightY3, double heightY4, double scale, double horizontalShift, double verticalShift)
        {
            double offsetX = Math.Round((double)(400 - (-1.25 * 4) + xOffset), 2);
            double offsetY = Math.Round((double)(150 - (-14 * 4)), 2);

            // Truyền scale, horizontalShift, verticalShift vào các hàm vẽ con
            DrawText(canvas, "X方向主筋", 14 + xOffset, heightY1 + 16, 4, 4, 4, offsetX, offsetY, Brushes.Black, scale, horizontalShift, verticalShift);
            TextBlock numberText = DrawText(canvas, number, -19 + xOffset, numberY, 10, 4, 4, offsetX, offsetY, Brushes.Black, scale, horizontalShift, verticalShift);
            DrawText(canvas, "X方向主筋", 14 + xOffset, heightY3 + 17, 4, 4, 4, offsetX, offsetY, Brushes.Black, scale, horizontalShift, verticalShift);

            // Vẽ các LINE
            DrawLine(canvas, -44 + xOffset, heightY1, 14 + xOffset, heightY1, 4, 4, offsetX, offsetY, Brushes.Black, 4, scale, horizontalShift, verticalShift);
            DrawLine(canvas, -44 + xOffset, heightY2, 14 + xOffset, heightY2, 4, 4, offsetX, offsetY, Brushes.Black, 4, scale, horizontalShift, verticalShift);
            DrawLine(canvas, -44 + xOffset, heightY3, 14 + xOffset, heightY3, 4, 4, offsetX, offsetY, Brushes.Black, 4, scale, horizontalShift, verticalShift);
            DrawLine(canvas, -44 + xOffset, heightY4, 14 + xOffset, heightY4, 4, 4, offsetX, offsetY, Brushes.Black, 4, scale, horizontalShift, verticalShift);

            DrawLine(canvas, 1 + xOffset, 30.43, 1 + xOffset, -48.43, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);
            DrawLine(canvas, -31 + xOffset, -48.43, -31 + xOffset, 30.43, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);
            DrawLine(canvas, -34 + xOffset, 38, 4 + xOffset, 38, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);
            DrawLine(canvas, -34 + xOffset, 38, -34 + xOffset, -56, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);
            DrawLine(canvas, 4 + xOffset, 38, 4 + xOffset, -56, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);
            DrawLine(canvas, 4 + xOffset, -56, -34 + xOffset, -56, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);
            DrawLine(canvas, -0.7 + xOffset, -50, -29.3 + xOffset, -50, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);
            DrawLine(canvas, -0.7 + xOffset, 32, -29.3 + xOffset, 32, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);

            DrawLine(canvas, 6 + xOffset, heightY1 + 2.7, 7 + xOffset, heightY1 + 0.7, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);
            DrawLine(canvas, 7 + xOffset, heightY1 + 0.7, 11.46 + xOffset, heightY1 + 7.5, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);
            DrawLine(canvas, 11.46 + xOffset, heightY1 + 7.5, 44 + xOffset, heightY1 + 7.5, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);

            DrawLine(canvas, 6 + xOffset, heightY3 + 3, 7 + xOffset, heightY3 + 1, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);
            DrawLine(canvas, 7 + xOffset, heightY3 + 1, 11.46 + xOffset, heightY3 + 9, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);
            DrawLine(canvas, 11.46 + xOffset, heightY3 + 9, 44 + xOffset, heightY3 + 9, 4, 4, offsetX, offsetY, Brushes.Black, 1, scale, horizontalShift, verticalShift);

            // Vẽ các CIRCLE
            DrawCircle(canvas, -29.3 + xOffset, 30.3, 1.7, 4, 4, offsetX, offsetY, Brushes.Black, scale, horizontalShift, verticalShift);
            DrawCircle(canvas, -0.7 + xOffset, 30.3, 1.7, 4, 4, offsetX, offsetY, Brushes.Black, scale, horizontalShift, verticalShift);
            DrawCircle(canvas, -29.3 + xOffset, -48.3, 1.7, 4, 4, offsetX, offsetY, Brushes.Black, scale, horizontalShift, verticalShift);
            DrawCircle(canvas, -0.7 + xOffset, -48.3, 1.7, 4, 4, offsetX, offsetY, Brushes.Black, scale, horizontalShift, verticalShift);
            DrawCircle(canvas, -15 + xOffset, -48.3, 1.7, 4, 4, offsetX, offsetY, Brushes.Black, scale, horizontalShift, verticalShift);
            DrawCircle(canvas, -15 + xOffset, 30.30, 1.7, 4, 4, offsetX, offsetY, Brushes.Black, scale, horizontalShift, verticalShift);

            return numberText;
        }

        private TextBlock DrawText(Canvas canvas, string text, double x, double y, double height, double scaleX, double scaleY, double offsetX, double offsetY, Brush color, double scale, double horizontalShift, double verticalShift)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                FontSize = height * scaleY * scale + 6, // Nhân với scale
                Foreground = color
            };
            Canvas.SetLeft(textBlock, x * scaleX * scale + offsetX + horizontalShift); // Thêm horizontalShift
            Canvas.SetTop(textBlock, -y * scaleY * scale + offsetY + verticalShift);   // Thêm verticalShift
            canvas.Children.Add(textBlock);
            return textBlock;
        }
        private void DrawLine(Canvas canvas, double x1, double y1, double x2, double y2, double scaleX, double scaleY, double offsetX, double offsetY, Brush color, double strokeThickness, double scale, double horizontalShift, double verticalShift)
        {
            Line line = new Line
            {
                X1 = x1 * scaleX * scale + offsetX + horizontalShift, // Thêm horizontalShift
                Y1 = -y1 * scaleY * scale + offsetY + verticalShift,  // Thêm verticalShift
                X2 = x2 * scaleX * scale + offsetX + horizontalShift, // Thêm horizontalShift
                Y2 = -y2 * scaleY * scale + offsetY + verticalShift,  // Thêm verticalShift
                Stroke = color,
                StrokeThickness = strokeThickness * scale
            };
            canvas.Children.Add(line);
        }

        private void DrawCircle(Canvas canvas, double centerX, double centerY, double radius, double scaleX, double scaleY, double offsetX, double offsetY, Brush color, double scale, double horizontalShift, double verticalShift)
        {
            Ellipse ellipse = new Ellipse
            {
                Width = radius * 2 * scaleX * scale,
                Height = radius * 2 * scaleY * scale,
                Stroke = color,
                StrokeThickness = 1 * scale
            };
            Canvas.SetLeft(ellipse, centerX * scaleX * scale + offsetX + horizontalShift - radius * scaleX * scale); // Thêm horizontalShift
            Canvas.SetTop(ellipse, -centerY * scaleY * scale + offsetY + verticalShift - radius * scaleY * scale);  // Thêm verticalShift
            canvas.Children.Add(ellipse);
        }
        // Ton 2025/03/14 // トップ筋位置寸法を表示
        private const double Scale21 = 7;
        private double CanvasHeight = 200;
        private const double OffsetX = 35;
        private const double OffsetY = -145;
        private void DrawBeamDiagramTon21()
        {
            Canvas canvas = new Canvas { Width = 200 * Scale21, Height = 200 * Scale21, Background = Brushes.White };
            CanvasHeight = canvas.Height / Scale21;
            Canvas21.Children.Clear();
            Canvas21.Children.Add(canvas);

            DrawLines21(canvas);
            DrawText21(canvas);
        }

        private void DrawLine21(Canvas canvas, double x1, double y1, double x2, double y2, Brush color, double thickness, DoubleCollection dashArray = null)
        {
            double adjustedX1 = x1 - OffsetX;
            double adjustedX2 = x2 - OffsetX;
            double flippedY1 = CanvasHeight - (y1 - OffsetY);
            double flippedY2 = CanvasHeight - (y2 - OffsetY);

            Line line = new Line
            {
                X1 = adjustedX1 * Scale21,
                Y1 = flippedY1 * Scale21,
                X2 = adjustedX2 * Scale21,
                Y2 = flippedY2 * Scale21,
                Stroke = color,
                StrokeThickness = thickness
            };
            if (dashArray != null)
            {
                line.StrokeDashArray = dashArray;
            }
            canvas.Children.Add(line);
        }

        private void DrawText21(Canvas canvas, double x, double y, string text, double fontSize, Brush color)
        {
            double adjustedX = x - OffsetX;
            double flippedY = CanvasHeight - (y - OffsetY);

            TextBlock textBlock = new TextBlock
            {
                Text = text,
                FontSize = fontSize * Scale21,
                Foreground = color,
                //FontFamily = new FontFamily("MS Gothic"),
            };
            Canvas.SetLeft(textBlock, adjustedX * Scale21);
            Canvas.SetTop(textBlock, flippedY * Scale21 - (fontSize * Scale21));
            canvas.Children.Add(textBlock);
        }

        private void DrawLines21(Canvas canvas)
        {
            var lines = new List<(double x1, double y1, double x2, double y2, Brush color)>
            {
                (59.0, 55.0, 59.0, 25.0, Brushes.Red),
                (40.25, 55.0, 40.25, 25.0, Brushes.Green),
                (77.75, 55.0, 77.75, 25.0, Brushes.Green),
                (54.0, 55.0, 54.0, 25.0, Brushes.Black),
                (64.0, 55.0, 64.0, 25.0, Brushes.Black),
                (35.25, 35.0, 83.75, 35.0, Brushes.Black),
                (35.25, 30.0, 83.75, 30.0, Brushes.Black),
                (35.25, 25.0, 83.75, 25.0, Brushes.Black),
                (36.5, 40.0, 81.5, 40.0, Brushes.Black),
                (35.25, 55.0, 35.25, 25.0, Brushes.Black),
                (83.75, 25.0, 83.75, 55.0, Brushes.Black),
                (51.5, 45.0, 81.5, 45.0, Brushes.Black),
                (35.25, 55.0, 83.75, 55.0, Brushes.Black)
            };

            foreach (var (x1, y1, x2, y2, color) in lines)
            {
                if (x1 != x2 || y1 != y2)
                {
                    DrawLine21(canvas, x1, y1, x2, y2, color, 1);
                }
            }
        }

        private void DrawText21(Canvas canvas)
        {
            var texts = new List<(double x, double y, string content, double height, Brush color)>
            {
                (60.1, 45.6735, "D29-3000", 2, Brushes.Black),
                (55, 40.6735, "D29-4000", 2, Brushes.Black),
                (57.8968, 35.6735, "D29-6500", 2, Brushes.Black),
                (52.8968, 30.6735, "D29-6500", 2, Brushes.Black)
            };

            // Chỉ thêm "400" và "1850" nếu topkindimon1 được chọn
            if (topkindimon1.IsChecked == true)
            {
                texts.Add((49, 43, "400", 2, Brushes.Red));
                texts.Add((47, 38, "1850", 2, Brushes.Red));
            }

            foreach (var (x, y, content, height, color) in texts)
            {
                DrawText21(canvas, x, y, content, height, color);
            }
        }
        //#### end ####
        private void DrawBeamDiagramKha19()
        {
            if (MainCanvasKha19 == null) return;

            MainCanvasKha19.Children.Clear();

            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush concreteLineBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush text300 = new SolidColorBrush(Colors.Blue);
            SolidColorBrush lightgreen = new SolidColorBrush(Colors.LightSkyBlue);
            SolidColorBrush purple = new SolidColorBrush(Colors.HotPink);

            double scaleFactor = 0.7; // スケールファクター（座標範囲が広いため小さめに設定）
            double offsetX = -50; // オフセット（図面をキャンバスの中央に配置）
            double offsetY = -10;
            double steelLineThickness = 1;
            double textthickness = 2;

            // DXFデータをシミュレート（実際のアプリケーションではDXFファイルをパース）
            DrawEntitiesKha19(scaleFactor, offsetX, offsetY, concreteLineBrush, steelBrush, textBrush, text300, lightgreen, purple, steelLineThickness, textthickness);
        }

        private void DrawEntitiesKha19(double scaleFactor, double offsetX, double offsetY, SolidColorBrush concreteLineBrush,
                                    SolidColorBrush steelBrush, SolidColorBrush textBrush, SolidColorBrush text300,
                                    SolidColorBrush lightgreen, SolidColorBrush purple, double steelLineThickness, double textthickness)
        {
            // LINEエンティティの描画
            DrawLineKha19(211.289, 0.394, 211.289, -304.773, textBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Red line
            DrawLineKha19(161.289, 0.394, 161.289, -304.773, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // The left of red line
            DrawLineKha19(261.289, 0.394, 261.289, -304.773, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // The right of red line
            DrawLineKha19(8.789, -18.870, 211.289, -18.870, lightgreen, scaleFactor, offsetX, offsetY, steelLineThickness); // Left Blue line
            DrawLineKha19(211.289, -18.870, 537.069, -18.870, lightgreen, scaleFactor, offsetX, offsetY, steelLineThickness); // Right Blue line
            DrawLineKha19(174.289, -68.870, 537.069, -68.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thép ngang 1
            DrawLineKha19(174.289, -118.870, 537.069, -118.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thép ngang 2 
            DrawLineKha19(174.289, -168.870, 537.069, -168.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thép ngang 3 
            DrawLineKha19(174.289, -218.870, 449.289, -218.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép ngang 4
            DrawLineKha19(174.289, -268.870, 449.289, -268.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép ngnag 5
            DrawLineKha19(174.289, -68.870, 174.289, -98.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 1
            DrawLineKha19(174.289, -118.870, 174.289, -148.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 2
            DrawLineKha19(174.289, -168.870, 174.289, -198.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 3
            DrawLineKha19(174.289, -218.870, 174.289, -248.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 4
            DrawLineKha19(174.289, -268.870, 174.289, -298.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 5
            DrawLineKha19(0.0, 0.0, 0.0, -305.167, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // viền trái
            DrawLineKha19(0.0, -305.167, 537.069, -305.167, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // viền dưới
            DrawLineKha19(0.0, 0.0, 537.069, 0.0, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // viền trên
            DrawLineKha19(537.069, 0.0, 537.069, -305.167, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // viền phải


            // hatarakion2がチェックされていない場合のみ以下のテキストを描画
            if (hatarakion2.IsChecked != true)
            {
                DrawTextKha19("(4000)", 342.017, -89.870, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 116           
                DrawTextKha19("(3500)", 317.017, -139.870, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 118
                DrawTextKha19("(4000)", 342.017, -189.870, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 11A
                DrawTextKha19("(2750)", 279.517, -239.870, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 11C
                DrawTextKha19("(2750)", 279.517, -289.870, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 11E
            }
            // TEXTエンティティの描画
            if (ankanagaon2.IsChecked != true)
            {
                DrawTextKha19("300", 112.041, -84.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 107
                DrawTextKha19("300", 112.041, -134.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 109
                DrawTextKha19("300", 112.041, -184.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 10B
                DrawTextKha19("300", 112.041, -234.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 10D
                DrawTextKha19("300", 112.041, -284.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 10F
            }

            DrawTextKha19("D29-4300", 325.257, -62.135, steelBrush, scaleFactor, offsetX, offsetY, textthickness); // 117
            DrawTextKha19("D29-3800", 300.257, -112.135, steelBrush, scaleFactor, offsetX, offsetY, textthickness); // 119           
            DrawTextKha19("D29-4300", 325.257, -162.135, steelBrush, scaleFactor, offsetX, offsetY, textthickness); // 11B           
            DrawTextKha19("D29-3050", 262.757, -212.135, steelBrush, scaleFactor, offsetX, offsetY, textthickness); // 11D           
            DrawTextKha19("D29-3050", 262.757, -262.135, steelBrush, scaleFactor, offsetX, offsetY, textthickness); // 11F

            if (nigeon2.IsChecked != true)
            {
                DrawTextKha19("ﾆｹﾞ130", 17.645, -109.238, purple, scaleFactor, offsetX, offsetY, textthickness); // 25B
            }

            // 固定
            DrawTextKha19("上筋", 17.645, -49.238, steelBrush, scaleFactor, offsetX, offsetY, textthickness); // 135

            // 3つの丸い点
            DrawEllipseKha19(513.289, -68.870, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true); // 110 (円形、HATCHあり)
            DrawEllipseKha19(463.289, -118.870, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true); // 112 (円形、HATCHあり)
            DrawEllipseKha19(513.289, -168.870, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true); // 114 (円形、HATCHあり)
        }

        private Line DrawLineKha19(double x1, double y1, double x2, double y2, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            Line line = new Line
            {
                Stroke = brush,
                StrokeThickness = thickness
            };

            Point start = ScalePoint(x1, y1, scaleFactor, offsetX, offsetY);
            Point end = ScalePoint(x2, y2, scaleFactor, offsetX, offsetY);

            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;

            MainCanvasKha19.Children.Add(line);
            return line;
        }

        private void DrawTextKha19(string text, double x, double y, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double textthickness)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 20 * scaleFactor, // フォントサイズをスケールに合わせて調整
                //FontFamily = new FontFamily("MS Gothic") // 日本語フォントをサポート
            };

            Point position = ScalePoint(x, y, scaleFactor, offsetX, offsetY + 3);
            Canvas.SetLeft(textBlock, position.X);
            Canvas.SetTop(textBlock, position.Y - textBlock.FontSize); // テキストを上に調整

            MainCanvasKha19.Children.Add(textBlock);
        }

        private void DrawEllipseKha19(double centerX, double centerY, double majorRadius, double minorRadius, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, bool isFilled)
        {
            Ellipse ellipse = new Ellipse
            {
                Stroke = brush,
                StrokeThickness = 1,
                Fill = isFilled ? brush : null,
                Width = majorRadius * 3 * scaleFactor,
                Height = minorRadius * 3 * scaleFactor
            };

            Point center = ScalePoint(centerX, centerY, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(ellipse, center.X - ellipse.Width / 2);
            Canvas.SetTop(ellipse, center.Y - ellipse.Height / 2);

            MainCanvasKha19.Children.Add(ellipse);
        }
        //Kết thúc hình 施工図の設定1

        //#### Bắt đầu vẽ hình 施工図の設定2 ####
        private void DrawBeamDiagramKha20()
        {
            if (MainCanvasKha20 == null) return;

            MainCanvasKha20.Children.Clear();

            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush concreteLineBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush text300 = new SolidColorBrush(Colors.Blue);
            SolidColorBrush lightgreen = new SolidColorBrush(Colors.LightGreen);
            SolidColorBrush purple = new SolidColorBrush(Colors.HotPink);

            double scaleFactor = 0.7; // スケールファクター（座標範囲が広いため小さめに設定）
            double offsetX = 95; // オフセット（図面をキャンバスの中央に配置）
            double offsetY = 110;
            double steelLineThickness = 1;
            double textthickness = 2;

            // DXFデータをシミュレート（実際のアプリケーションではDXFファイルをパース）
            DrawEntitiesKha20(scaleFactor, offsetX, offsetY, concreteLineBrush, steelBrush, textBrush, text300, lightgreen, purple, steelLineThickness, textthickness);
        }

        private void DrawEntitiesKha20(double scaleFactor, double offsetX, double offsetY, SolidColorBrush concreteLineBrush,
                          SolidColorBrush steelBrush, SolidColorBrush textBrush, SolidColorBrush text300,
                          SolidColorBrush lightgreen, SolidColorBrush purple, double steelLineThickness, double textthickness)
        {
            if (nakagozuon2.IsChecked != true)
            {

                DrawLineKha20(40.0, 50.0, 100.0, 50.0, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // 上辺
                DrawLineKha20(100.0, 50.0, 100.0, 0.0, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // 右辺
                DrawLineKha20(100.0, 0.0, 40.0, 0.0, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // 下辺
                DrawLineKha20(40.0, 0.0, 40.0, 50.0, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // 左辺
                DrawLineKha20(60.0, 50.0, 60.0, 0.0, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // 内部垂直線1
                DrawLineKha20(80.0, 50.0, 80.0, 0.0, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // 内部垂直線2

            }

            if (nakagozuon1.IsChecked != true)
            {
                DrawEllipseKha20(109.25, 35.0, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true);
                DrawEllipseKha20(59.25, -15.0, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true);

                DrawLineKha20(-203.75, 35.0, 346.25, 35.0, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);
                DrawLineKha20(-203.75, -15.0, 346.25, -15.0, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);

                DrawTextKha20("(4000)", -123.022, 14.00000000000156, steelBrush, scaleFactor, offsetX, offsetY, textthickness);
                DrawTextKha20("D29-4300", -139.782, 41.73454545454612, text300, scaleFactor, offsetX, offsetY, textthickness);
                DrawTextKha20("(3500)", -148.022, -36.00000000000031, steelBrush, scaleFactor, offsetX, offsetY, textthickness);
                DrawTextKha20("D29-3800", -164.782, -8.265454545454126, text300, scaleFactor, offsetX, offsetY, textthickness);

            }
            // LINEエンティティの描画（既存）
            DrawLineKha20(-66.25, 195.0, -66.25, -115.0, lightgreen, scaleFactor, offsetX, offsetY, 2);// Left Green line
            DrawLineKha20(21.25, 195.0, 21.25, 125.0, concreteLineBrush, scaleFactor, offsetX, offsetY, 1);// Left number 4
            DrawLineKha20(21.25, 125.0, 121.25, 125.0, concreteLineBrush, scaleFactor, offsetX, offsetY, 1); //Under number 4
            DrawLineKha20(121.25, 125.0, 121.25, 195.0, concreteLineBrush, scaleFactor, offsetX, offsetY, 1);// Right number 4
            DrawLineKha20(208.75, 195.0, 208.75, -115.0, lightgreen, scaleFactor, offsetX, offsetY, 2);// right Green line
            DrawLineKha20(-203.75, -143.0, 346.25, -143.0, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// đường ngang 6
            DrawLineKha20(-206.25, 195.0, 346.25, 195.0, concreteLineBrush, scaleFactor, offsetX, offsetY, 1); //đường ngang trên cùng
            DrawLineKha20(346.25, 195.0, 346.25, -143.0, concreteLineBrush, scaleFactor, offsetX, offsetY, 1);// dọc phải
            DrawLineKha20(-203.75, -143.0, -203.75, 195.0, concreteLineBrush, scaleFactor, offsetX, offsetY, 1);// dọc trái

            // レイヤー3のLINEエンティティ（既存）

            DrawLineKha20(-203.75, -65.0, 346.25, -65.0, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);
            DrawLineKha20(-203.75, -115.0, 346.25, -115.0, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness);

            // 新しいLINEエンティティの描画（矩形と内部の線）

            // ELLIPSEエンティティの描画（既存）           

            DrawEllipseKha20(59.25, -115.0, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true); // HATCHがあるため塗りつぶし
            DrawEllipseKha20(109.25, -65.0, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true);

            // TEXTエンティティの描画（既存）

            DrawTextKha20("600x900", 27.418, 98.0000000000016, steelBrush, scaleFactor, offsetX, offsetY, textthickness);
            DrawTextKha20("4", 64.792, 139.0, steelBrush, scaleFactor, offsetX, offsetY, textthickness);

            DrawTextKha20("(3500)", -148.022, -135.9999999999998, steelBrush, scaleFactor, offsetX, offsetY, textthickness);
            DrawTextKha20("D29-3800", -164.782, -108.2654545454544, text300, scaleFactor, offsetX, offsetY, textthickness);
            DrawTextKha20("(4000)", -123.022, -85.99999999999996, steelBrush, scaleFactor, offsetX, offsetY, textthickness);
            DrawTextKha20("D29-4300", -139.782, -58.26545454545449, text300, scaleFactor, offsetX, offsetY, textthickness);
        }

        private Line DrawLineKha20(double x1, double y1, double x2, double y2, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            Line line = new Line
            {
                Stroke = brush,
                StrokeThickness = thickness
            };

            Point start = ScalePoint(x1, y1, scaleFactor, offsetX, offsetY);
            Point end = ScalePoint(x2, y2, scaleFactor, offsetX, offsetY);

            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;

            MainCanvasKha20.Children.Add(line);
            return line;
        }

        private void DrawTextKha20(string text, double x, double y, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double textthickness)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 20 * scaleFactor, // フォントサイズをスケールに合わせて調整
                //FontFamily = new FontFamily("MS Gothic") // 日本語フォントをサポート
            };

            Point position = ScalePoint(x, y, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(textBlock, position.X);
            Canvas.SetTop(textBlock, position.Y - textBlock.FontSize); // テキストを上に調整

            MainCanvasKha20.Children.Add(textBlock);
        }
        private void DrawEllipseKha20(double centerX, double centerY, double majorRadius, double minorRadius, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, bool isFilled)
        {
            Ellipse ellipse = new Ellipse
            {
                Stroke = brush,
                StrokeThickness = 1,
                Fill = isFilled ? brush : null,
                Width = majorRadius * 3 * scaleFactor,
                Height = minorRadius * 3 * scaleFactor
            };

            Point center = ScalePoint(centerX, centerY, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(ellipse, center.X - ellipse.Width / 2);
            Canvas.SetTop(ellipse, center.Y - ellipse.Height / 2);

            MainCanvasKha20.Children.Add(ellipse);
        }

        //#### Kết thúc vẽ hình 施工図の設定2 ####

        private void DrawBeamDiagramPhatSaigo()
        {
            drawBeamDiagramPhatSaigo.Children.Clear();

            DrawHatches();
            DrawLines();
            DrawArcs();
            DrawCircles();
            DrawText();
        }
        private void DrawHatches()
        {
            double[,] hatch1Coords = new double[,]
            {
                         {54, -8}, {54, 21}, {54, 25}, {23, 25}, {20, 25}, {20, 30},
                         {-17, 30}, {-17, 25}, {-24, 25}, {-24, -8}, {-17, -8}, {-17, -20},
                         {20, -20}, {20, -8}, {54, -8}
            };
            Polygon hatch1Polygon = new Polygon { Fill = Brushes.LightGray };
            for (int i = 0; i < hatch1Coords.GetLength(0); i++)
            {
                hatch1Polygon.Points.Add(new Point(
                    hatch1Coords[i, 0] * scale + offsetX,
                    -hatch1Coords[i, 1] * scale + offsetY
                ));
            }
            drawBeamDiagramPhatSaigo.Children.Add(hatch1Polygon);

            double[,] hatch2Coords = new double[,]
            {
                         {-90, -15}, {-127, -15}, {-127, -8}, {-134, -8}, {-134, 25}, {-127, 25},
                         {-90, 25}, {-87, 25}, {-56, 25}, {-56, 21}, {-56, -8}, {-90, -8}
            };
            Polygon hatch2Polygon = new Polygon { Fill = Brushes.LightGray };
            for (int i = 0; i < hatch2Coords.GetLength(0); i++)
            {
                hatch2Polygon.Points.Add(new Point(
                    hatch2Coords[i, 0] * scale + offsetX,
                    -hatch2Coords[i, 1] * scale + offsetY
                ));
            }
            drawBeamDiagramPhatSaigo.Children.Add(hatch2Polygon);

            double[][] circularHatchCoords = new double[][]
            {
                         new double[] {-10.8, 17.69862022921933, 1.301379770780674},
                         new double[] {-120.8, 17.69862022921933, 1.301379770780674},
                         new double[] {13.8, 17.69862022921933, 1.301379770780674},
                         new double[] {-96.2, 17.69862022921933, 1.301379770780674},
                         new double[] {13.858518785067, -10.86023132, 1.301379770780674},
                         new double[] {-96.2, -8.860231315790656, 1.301379770780674},
                         new double[] {-10.80443364219048, -10.86023132, 1.301379770780674},
                         new double[] {-120.8, -8.859683022637206, 1.301379770780674}
            };
            foreach (var coords in circularHatchCoords)
            {
                double x = coords[0] * scale + offsetX;
                double y = -coords[1] * scale + offsetY;
                double radius = coords[2] * scale;
                Ellipse hatchEllipse = new Ellipse
                {
                    Width = 2 * radius,
                    Height = 2 * radius,
                    Fill = Brushes.Yellow,
                };
                Canvas.SetLeft(hatchEllipse, x - radius);
                Canvas.SetTop(hatchEllipse, y - radius);
                drawBeamDiagramPhatSaigo.Children.Add(hatchEllipse);
            }
        }

        private void DrawLines()
        {
            List<double[]> lines = new List<double[]>
                     {
                         new double[] {23, 21, 23, 25,1},
                         new double[] {-87, 21, -87, 25, 1 },
                         new double[] {-10.91934405066459, -13.03932153, 14.04052926378799, -13.03932153, 2},
                         new double[] {16, -11.07985079, 16.00036229282939, 17.96183295065667,2},
                         new double[] {-12.95906815797144, -11.07985079, -12.95906815797144, 17.8796462790022,2},
                         new double[] {-10.99922466630687, 19.91934405066345, 13.96067847093438, 19.91934405066345, 2 },
                         new double[] {-23, 15.5, 52.98360703551981, 15.5, 4},
                         new double[] {-23.05060233409723, -3.412623903311431, 53.496849721354, -3.41262390331145, 4},
                         new double[] {21.40795435610736, -3.393855512759384, 21.40795435610736, 15.46337347268068, 3},
                         new double[] {-18.59204564389264, -3.393855512759383, -18.59204564389264, 15.46337347268068, 3},
                         new double[] {48.40795435610736, -3.393855512759383, 48.40795435610736, 15.46337347268068,3},
                         new double[] {28.40795435610736, -3.393855512759384, 28.40795435610736, 15.46337347268068,3},
                         new double[] {35.40795435610736, -3.393855512759384, 35.40795435610736, 15.46337347268068,3},
                         new double[] {42.40795435610736, -3.393855512759384, 42.40795435610736, 15.46337347268068,3},
                         new double[] {-120.92, -11.03932152906629, -96, -11.03972410730685,2},
                         new double[] {-94, -9, -94, 17.96183295065667,2},
                         new double[] { -122.96, -9.079850792854273, -122.96, 17.8796462790022,2},
                         new double[] {-121, 19.91934405066345, -96, 19.91934405066345,2},
                         new double[] {-133, 15.5, -57, 15.5,4},
                         new double[] {-133, -3.412623903311427, -56.5, -3.412623903311449,4},
                         new double[] {-88.59, -3.393855512759387, -88.59, 15.46337347268069,3},
                         new double[] {-128.59, -3.393855512759387, -128.59, 15.46337347268069, 3},
                         new double[] {-61.59, -3.393855512759387, -61.59, 15.46337347268069, 3},
                         new double[] {-81.59, -3.393855512759387,-81.59, 15.46337347268069,3},
                         new double[] {-74.59, -3.393855512759387, -74.59, 15.46337347268069,3},
                         new double[] {-67.59, -3.393855512759387, -67.59, 15.46337347268069, 3 },

                         new double[] {54, 21, 20, 21,5}, // đường gạch chân

                         new double[] {-56, 21, -90, 21,5},// đường gạch chân

                         new double[] {35.4, 16.5, 35.4, 21,1},
                         new double[] {37.35103473556544, 21, 33.44896526443456, 21,1},
                         new double[] {37.35103473556544, 16.5, 33.44896526443456, 16.5,1},


                         new double[] {24.95103473556544, 21, 21, 21,1},
                         new double[] {24.95103473556544, 25, 21, 25,1},

                         new double[] {-74.6, 16.5, -74.6, 21,1},
                         new double[] {-74.6, 21, -76.5, 21,1},
                         new double[] {-72.65, 21, -74.6, 21,1},
                         new double[] {-74.6, 16.5, -76.5, 16.5,1},
                         new double[] {-72.65, 16.5, -74.6, 16.5, 1 },
                         new double[] {-87, 25, -89, 25,1},
                         new double[] {-85, 25, -87, 25, 1 },
                         new double[] {-87, 25, -89, 25,1},
                         new double[] {-85, 25, -87, 25, 1 },
                         new double[] {-87, 21, -89, 21,1},
                         new double[] {-85, 21, -87, 21, 1 }
                     };
            var styleMap = new Dictionary<int, (Brush Stroke, double Thickness, bool IsDashed)>
                    {
                        { 1, (Brushes.Red, 2.0, false) },
                        { 2, (Brushes.Black, 5.0, false) },
                        { 3, (Brushes.Blue, 5.0, false) },
                        { 4, (Brushes.Purple, 5.0, false) },
                        { 5, (Brushes.Black, 1.5, true) }
                    };

            foreach (var line in lines)
            {
                if (!styleMap.TryGetValue((int)line[4], out var style)) continue;

                var x1 = line[0] * scale + offsetX;
                var y1 = -line[1] * scale + offsetY;
                var x2 = line[2] * scale + offsetX;
                var y2 = -line[3] * scale + offsetY;

                if (style.IsDashed)
                {
                    var polyline = new Polyline
                    {
                        Stroke = style.Stroke,
                        StrokeThickness = style.Thickness,
                        StrokeDashArray = new DoubleCollection { 10, 5 }
                    };
                    polyline.Points.Add(new Point(x1, y1));
                    polyline.Points.Add(new Point(x2, y2));
                    drawBeamDiagramPhatSaigo.Children.Add(polyline);
                }
                else
                {
                    drawBeamDiagramPhatSaigo.Children.Add(new Line
                    {
                        X1 = x1,
                        Y1 = y1,
                        X2 = x2,
                        Y2 = y2,
                        Stroke = style.Stroke,
                        StrokeThickness = style.Thickness
                    });
                }
            }
        }

        private void DrawArcs()
        {
            List<double[]> arcs = new List<double[]>
                    {
                        new double[] {14.00080515648112, 17.91974662890402, 2, 270, 360},
                        new double[] {14.00040257824057, -11.04012669, 2, 90, 1.149622},
                        new double[] {-10.95906815797144, 17.8796462790022, 2, 180, 270},
                        new double[] {-10.95947073621201, -11.03972411, 2, 90, 180},
                        new double[] {-96, 17.91974662890402, 2, 270, 360},
                        new double[] {-96, -9.04012668554742, 2, 90, 1.149622},
                        new double[] {-121, 17.8796462790022, 2, 180, 270},
                        new double[] {-121, -9.03972410730686, 2, 90, 180}
                    };

            foreach (var arc in arcs)
            {
                double centerX = arc[0] * scale + offsetX;
                double centerY = -arc[1] * scale + offsetY;
                double radius = arc[2] * scale;
                double startAngle = arc[3] * Math.PI / 180;
                double endAngle = arc[4] * Math.PI / 180;

                Point startPoint = new Point(
                    centerX + radius * Math.Cos(startAngle),
                    centerY + radius * Math.Sin(startAngle)
                );
                Point endPoint = new Point(
                    centerX + radius * Math.Cos(endAngle),
                    centerY + radius * Math.Sin(endAngle)
                );

                PathFigure pathFigure = new PathFigure { StartPoint = startPoint };
                ArcSegment arcSegment = new ArcSegment
                {
                    Point = endPoint,
                    Size = new Size(radius, radius),
                    SweepDirection = (endAngle > startAngle) ? SweepDirection.Clockwise : SweepDirection.Counterclockwise,
                    IsLargeArc = Math.Abs(endAngle - startAngle) > Math.PI
                };
                pathFigure.Segments.Add(arcSegment);
                PathGeometry pathGeometry = new PathGeometry { Figures = { pathFigure } };
                Path arcPath = new Path
                {
                    Stroke = Brushes.Black,
                    StrokeThickness = 5,
                    Data = pathGeometry
                };
                drawBeamDiagramPhatSaigo.Children.Add(arcPath);
            }
        }

        private void DrawCircles()
        {
            double[][] circles = new double[][]
            {
                         new double[] {13.8, 17.69862022921933, 1.301379770780674},
                         new double[] {13.858518785067, -10.86023132, 1.301379770780674},
                         new double[] {-10.8, 17.69862022921933, 1.301379770780674},
                         new double[] {-10.80443364219048, -10.85968302, 1.301379770780674},
                         new double[] {-96.2, 17.69862022921933, 1.301379770780674},
                         new double[] {-96.2, -8.860231315790656, 1.301379770780674},
                         new double[] {-120.8, 17.69862022921933, 1.301379770780674},
                         new double[] {-120.8, -8.859683022637206, 1.301379770780674}
            };

            foreach (var circle in circles)
            {
                double x = circle[0] * scale + offsetX;
                double y = -circle[1] * scale + offsetY;
                double radius = circle[2] * scale;
                Ellipse ellipse = new Ellipse
                {
                    Width = 2 * radius,
                    Height = 2 * radius,
                    Stroke = Brushes.Black,
                    StrokeThickness = 0.5
                };
                Canvas.SetLeft(ellipse, x - radius);
                Canvas.SetTop(ellipse, y - radius);
                drawBeamDiagramPhatSaigo.Children.Add(ellipse);
            }
        }

        private void DrawText()
        {
            List<(double x, double y, double height, string text)> texts = new List<(double, double, double, string)>
            {
                (-3.4525, 10.46531791907515, 3, "梁主筋"),
                (-113.46, 10.5, 3, "梁主筋"),
                (24.709, 25, 2.5, "通常のかぶり寸法"),
                (-85.3,25, 2.5, "通常のかぶり寸法"),
                (37.94090398410747, 20, 2.5, $"{STPSeiHerisunTextBox.Text}"),
                (-71, 20, 2.5, $"{STPHabaHerisunTextBox.Text}")
            };

            foreach (var (x, y, height, text) in texts)
            {
                TextBlock textBlock = new TextBlock
                {
                    Text = text,
                    FontSize = height * scale,
                    Foreground = Brushes.Black
                };
                Canvas.SetLeft(textBlock, x * scale + offsetX);
                Canvas.SetTop(textBlock, -y * scale + offsetY);
                drawBeamDiagramPhatSaigo.Children.Add(textBlock);
            }
        }




        private readonly double offsetX = 500;
        private readonly double offsetY = 100;
        private readonly double scale = 4;


        private List<Line> drawnLines = new List<Line>();
        private List<Path> drawnArcs = new List<Path>();
        private void DrawBeamDiagramPhatSaigo1()
        {
            drawBeamDiagramPhatSaigo1.Children.Clear();
            drawnLines.Clear();
            drawnArcs.Clear();
            // Draw HATCHes
            DrawHatchesPhatSaigo();
            // Draw LINEs
            DrawLinesPhatSaigo();
            // Draw ARCs
            DrawArcsPhatSaigo();
            // Draw CIRCLEs
            DrawCirclesPhatSaigo();
            // Draw TEXTs
            DrawTexts();
            List<(double x1, double y1, double x2, double y2)> linesToRemove = new List<(double, double, double, double)>
            {
                (-0.466078523878565, 23.9592938915827, -2.417113259444079, 23.9592938915827),
                (1.484956211686836, 23.9592938915827, -0.466078523878565, 23.9592938915827),
                 (40.46607852, 23.95918129627695, 38.51504379, 23.95918129627695),
                 (42.41711325944411, 23.95918129627695, 40.46607852387871, 23.95918129627695),
            };
            List<(double x1, double y1, double x2, double y2)> linesToRemove1 = new List<(double, double, double, double)>
            {
                (152.2270298355221, 50.48790517941146, 221.1295431087692, 50.48790659133088),
                (178.8968192, 47.54603933, 178.8968192, 41.54603933),
                (182.4611386301022, 41.24192361742633, 180.5101038945367, 41.24192361742633),
                (182.4611386301022, 47.34192361742633, 180.5101038945367, 47.34192361742633),
                (182.4611386301022, 41.24192361742633, 182.4611386301022, 47.34192361742633),
                (184.4121733656676, 41.24192361742633, 182.4611386301022, 41.24192361742633),
                (184.4121733656676, 47.34192361742633, 182.4611386301022, 47.34192361742633),
                (197.4611386301022, 41.24192361742633, 195.5101038945367, 41.24192361742633),
                (197.4611386301022, 47.34192361742633, 195.5101038945367, 47.34192361742633),
                (197.4611386301022, 41.24192361742633, 197.4611386301022, 47.34192361742633),
                (201.12182979, 47.54660263, 201.12182979, 41.54603969),
                (204.06313284, 50.48790568, 221.12953954, 50.48790568),
                (199.4121733656676, 41.24192361742633, 197.4611386301022, 41.24192361742633),
                (199.4121733656676, 47.34192361742633, 197.4611386301022, 47.34192361742633),
            };
            List<(double x1, double y1, double x2, double y2)> linesToRemove2 = new List<(double, double, double, double)>
            {
                (152.2270298355221, 50.48790517941145, 192.8973860754106, 50.48790604095875),
                (186.1790261450734, 48.97036987039817, 221.0132758158068, 48.97036987039817),
                (152.227026270127, 50.48790517941165, 175.8973825100156, 50.4879056808366),
                (178.8968192, 47.54603933, 178.8968192, 41.54603933),
                (183.2377231, 46.02906682, 183.2377231, 40.02906682),
                (195.89682277, 47.54603969, 195.89682277, 41.54603969),
                (201.12182979, 47.54660263, 201.12182979, 41.54603969),
                (204.06313284, 50.48790568, 221.12953954, 50.48790568),
                (182.4611386301022, 41.24192361742633, 180.5101038945367, 41.24192361742633),
                (182.4611386301022, 47.34192361742633, 180.5101038945367, 47.34192361742633),
                (182.4611386301022, 41.24192361742633, 182.4611386301022, 47.34192361742633),
                (184.4121733656676, 41.24192361742633, 182.4611386301022, 41.24192361742633),
                (184.4121733656676, 47.34192361742633, 182.4611386301022, 47.34192361742633),
                (197.4611386301022, 41.24192361742633, 195.5101038945367, 41.24192361742633),
                (197.4611386301022, 47.34192361742633, 195.5101038945367, 47.34192361742633),
                (197.4611386301022, 41.24192361742633, 197.4611386301022, 47.34192361742633),
                (199.4121733656676, 41.24192361742633, 197.4611386301022, 41.24192361742633),
                (199.4121733656676, 47.34192361742633, 197.4611386301022, 47.34192361742633),
            };
            List<(double x1, double y1, double x2, double y2)> linesToRemove3 = new List<(double, double, double, double)>
            {
                (152.2270298355221, 50.48790517941146, 221.1295431087692, 50.48790659133088),
                (152.2270298355221, 50.48790517941145, 192.8973860754106, 50.48790604095875),
                (186.1790261450734, 48.97036987039817, 221.0132758158068, 48.97036987039817),
                (195.89682277, 47.54603969, 195.89682277, 41.54603969),
                (183.2377231, 46.02906682, 183.2377231, 40.02906682),
            };
            List<(double cx, double cy, double r, double startAngle, double endAngle)> arcsToRemove = new List<(double, double, double, double, double)>
            {
                (204.1212664857637, 47.48846898719781, 3.0, 180, 90),
                (175.8973825100157, 47.48790568083667, 3, 90, 0),
            };
            List<(double cx, double cy, double r, double startAngle, double endAngle)> arcsToRemove1 = new List<(double, double, double, double, double)>
            {
                (175.8973825100157, 47.48790568083667, 3, 90, 0),
                (186.2371597915549, 45.97093317675939, 3.0, 180, 90),
                (192.8973860754107, 47.48790604095882, 3, 90, 0),
                (204.1212664857637, 47.48846898719781, 3.0, 180, 90),
            };
            List<(double cx, double cy, double r, double startAngle, double endAngle)> arcsToRemove2 = new List<(double, double, double, double, double)>
            {

                (186.2371597915549, 45.97093317675939, 3.0, 180, 90),
                (192.8973860754107, 47.48790604095882, 3, 90, 0),

            };
            if (fukashiSTPteichakutenba2.IsChecked == true)
            {
                foreach (var (x1, y1, x2, y2) in linesToRemove)
                {
                    RemoveLine(x1, y1, x2, y2);
                }
            }
            if (fukashishukinteichaku1.IsChecked == true)
            {
                foreach (var (x1, y1, x2, y2) in linesToRemove1)
                {
                    RemoveLine(x1, y1, x2, y2);
                }
                foreach (var (cx, cy, r, startAngle, endAngle) in arcsToRemove)
                {
                    RemoveArc(cx, cy, r, startAngle, endAngle);
                }
            }
            if (fukashishukinteichaku3.IsChecked == true)
            {
                foreach (var (x1, y1, x2, y2) in linesToRemove3)
                {
                    RemoveLine(x1, y1, x2, y2);
                }
                foreach (var (cx, cy, r, startAngle, endAngle) in arcsToRemove2)
                {
                    RemoveArc(cx, cy, r, startAngle, endAngle);
                }
            }
            if (fukashishukinteichaku2.IsChecked == true)
            {
                foreach (var (x1, y1, x2, y2) in linesToRemove2)
                {
                    RemoveLine(x1, y1, x2, y2);
                }
                foreach (var (cx, cy, r, startAngle, endAngle) in arcsToRemove1)
                {
                    RemoveArc(cx, cy, r, startAngle, endAngle);
                }
            }
        }
        private void RemoveLine(double x1, double y1, double x2, double y2)
        {
            double offsetX = 0;
            double offsetY = 250;
            Line lineToRemove = null;
            foreach (var line in drawnLines)
            {
                if (line.X1 == x1 * scale + offsetX &&
                    line.Y1 == -y1 * scale + offsetY &&
                    line.X2 == x2 * scale + offsetX &&
                    line.Y2 == -y2 * scale + offsetY)
                {
                    lineToRemove = line;
                    break;
                }
            }
            if (lineToRemove != null)
            {
                drawBeamDiagramPhatSaigo1.Children.Remove(lineToRemove);
                drawnLines.Remove(lineToRemove);
            }
        }
        private void RemoveArc(double cx, double cy, double r, double startAngle, double endAngle)
        {
            double offsetX = 0;
            double offsetY = 250;
            Path arcToRemove = null;
            foreach (var arc in drawnArcs)
            {
                var startRad = Math.PI / 180 * startAngle;
                var endRad = Math.PI / 180 * endAngle;

                var startPoint = new Point(
                    cx * scale + offsetX + r * scale * Math.Cos(startRad),
                    -cy * scale + offsetY - r * scale * Math.Sin(startRad)
                );
                var endPoint = new Point(
                    cx * scale + offsetX + r * scale * Math.Cos(endRad),
                    -cy * scale + offsetY - r * scale * Math.Sin(endRad)
                );

                var arcSegment = (ArcSegment)((PathGeometry)arc.Data).Figures[0].Segments[0];

                if (arcSegment.Point == endPoint &&
                    arcSegment.Size.Width == r * scale &&
                    arcSegment.Size.Height == r * scale)
                {
                    arcToRemove = arc;
                    break;
                }
            }
            if (arcToRemove != null)
            {
                drawBeamDiagramPhatSaigo1.Children.Remove(arcToRemove);
                drawnArcs.Remove(arcToRemove);
            }
        }
        private void DrawHatchesPhatSaigo()
        {
            // HATCH 197
            double[,] hatch197Coords = new double[,]
            {
                {7.05458868625599, 48.53074845679014}, {33.01325426598689, 48.53074845679013},
                {38.01325426598689, 48.53074845679013}, {38.01325426598686, 15.04070610841728},
                {2.054588686255997, 15.04070610841728}, {2.054588686256004, 48.53074845679014},
                {7.05458868625599, 48.53074845679014}
            };
            DrawPolygonPhatSaigo(hatch197Coords);

            // HATCH 198
            double[,] hatch198Coords = new double[,]
            {
                {7.05458868625599, 48.53074845679014}, {33.01325426598689, 48.53074845679013},
                {38.01325426598689, 48.53074845679012}, {38.01325426598689, 64.95929389158272},
                {2.054588686256004, 64.95929389158272}, {2.054588686256004, 48.53074845679014},
                {7.05458868625599, 48.53074845679014}
            };
            DrawPolygon1PhatSaigo(hatch198Coords);

            // HATCH 4CA
            double[,] hatch4CACoords = new double[,]
            {
                {142.0000021674614, 47.52239414001571}, {150.2270300749635, 47.52239812244872},
                {150.2270300749635, 35.51031067911089}, {153.3696698211909, 33.73442976347287},
                {147.3990371021292, 30.99328867515012}, {152.2559656561345, 26.38515571644365},
                {152.2559656561345, 14.96605978034915}, {112.8969654232045, 14.96605978034914},
                {112.8969654232045, 11.47746639312829}, {95.01325426598689, 11.47746639312828},
                {95.01325426598686, 15.04070610841728}, {89.12954310876921, 15.04070610841728},
                {89.12954310876924, 47.52237952600933}, {95.12954310876924, 47.52237952600933},
                {95.12954310876924, 64.95929389158272}, {113.0132542659869, 64.95929389158272},
                {113.0132545539682, 47.52238010848672}, {142.0000021674614, 47.52239414001571}
            };
            DrawPolygonPhatSaigo(hatch4CACoords);

            // HATCH 4CB
            double[,] hatch4CBCoords = new double[,]
            {
                {113.0132544800176, 52.0}, {142.0, 52.0},
                {142.0000021674614, 47.52239414001571}, {113.0132545539682, 47.52238010848671},
                {113.0132544800176, 52.0}
            };
            DrawPolygon1PhatSaigo(hatch4CBCoords);

            // HATCH 4CC
            double[,] hatch4CCCoords = new double[,]
            {
                {152.2270300749635, 47.52239812244872}, {181.0371886308753, 47.52240509230884},
                {181.0371879876681, 52.00000560769166}, {181.0371861260628, 64.95931871373789},
                {198.92089728328, 64.95931871373787}, {198.92089728328, 47.52240580509093},
                {221.1295431087692, 47.52240580509093}, {221.1295431087692, 16.0404913320717},
                {198.8046084404976, 16.04070610841728}, {198.8046084404976, 11.47746639312829},
                {180.92089728328, 11.47746639312829}, {180.92089728328, 14.96605978034915},
                {154.2559656561345, 14.96605978034915}, {154.2559656561363, 27.38515571644275},
                {150.7705428758374, 30.62293718310975}, {157.3696698211909, 33.73442976347197},
                {152.2270300749635, 36.5},{152.2270300749635, 47.52239812244872},

            };
            DrawPolygonPhatSaigo(hatch4CCCoords);

            // HATCH 4CD
            double[,] hatch4CDCoords = new double[,]
            {
                {152.2270300749635, 47.52239812244872}, {152.2270297134322, 52.00000146911271},
                {181.037187987668, 52.00000560769164}, {181.0371886308753, 47.52240509230884},
                {152.2270300749635, 47.52239812244872}
            };
            DrawPolygon1PhatSaigo(hatch4CDCoords);

            // HATCH 4CE
            double[,] hatch4CECoords = new double[,]
            {
                {198.92089728328, 51.99999999999999}, {221.1295431087692, 52.0018735403596},
                {221.1295431087692, 47.52240580509093}, {198.92089728328, 47.52240580509093},
                {198.92089728328, 51.99999999999999}
            };
            DrawPolygon1PhatSaigo(hatch4CECoords);

            // HATCH 1B9 (Circular)
            DrawEllipsePhatSaigo(9.7694367121724, 42.53074845679014, 1.790744420494063);

            // HATCH 1BA (Circular)
            DrawEllipsePhatSaigo(30.30561851436983, 42.52797883371036, 1.790744420494063);

            // HATCH 4C9 (Invalid, skipped or minimal polygon)
            double[,] hatch4C9Coords = new double[,]
            {
                {181.0371861260623, 37.95932252832005}, {181.0371861260623, 16.44773319794121},
                {181.0371861260623, 37.95932252832005}
            };
            DrawPolygonPhatSaigo(hatch4C9Coords);
        }

        private void DrawLinesPhatSaigo()
        {
            List<(double x1, double y1, double x2, double y2)> lines = new List<(double, double, double, double)>
            {
                (-3.41711326, 59.03124391, 2.05458869, 59.03124391),
                (38.01325427, 59.03124391, 43.41711326, 59.03124391),
            };

            foreach (var (x1, y1, x2, y2) in lines)
            {
                DrawLinePhatSaigo(x1, y1, x2, y2);
            }
            List<(double x1, double y1, double x2, double y2)> lines1 = new List<(double, double, double, double)>
            {
                (134.4611421954972, 41.24192397754848, 132.5101074599317, 41.24192397754848),
                (134.4611421954972, 47.34192397754848, 132.5101074599317, 47.34192397754848),
                (182.4611386301022, 41.24192361742633, 180.5101038945367, 41.24192361742633),
                (182.4611386301022, 47.34192361742633, 180.5101038945367, 47.34192361742633),
                (197.4611386301022, 41.24192361742633, 195.5101038945367, 41.24192361742633),
                (197.4611386301022, 47.34192361742633, 195.5101038945367, 47.34192361742633),
                (134.4611421954972, 41.24192397754848, 134.4611421954972, 47.34192397754848),
                (182.4611386301022, 41.24192361742633, 182.4611386301022, 47.34192361742633),
                (197.4611386301022, 41.24192361742633, 197.4611386301022, 47.34192361742633),
                (136.4121769310626, 41.24192397754848, 134.4611421954972, 41.24192397754848),
                (136.4121769310626, 47.34192397754848, 134.4611421954972, 47.34192397754848),
                (184.4121733656676, 41.24192361742633, 182.4611386301022, 41.24192361742633),
                (184.4121733656676, 47.34192361742633, 182.4611386301022, 47.34192361742633),
                (199.4121733656676, 41.24192361742633, 197.4611386301022, 41.24192361742633),
                (199.4121733656676, 47.34192361742633, 197.4611386301022, 47.34192361742633),

                (40.46607852387871, 23.95918129627695, 40.46607852387868, 48.15918129627695),
                (40.46607852387868, 48.15918129627696, 38.51504378831316, 48.15918129627696),
                (42.41711325944408, 48.15918129627695, 40.46607852387868, 48.15918129627695),
                (42.41711325944408, 48.15918129627695, 40.46607852387868, 48.15918129627695),
                (42.41711325944408, 48.15918129627695, 40.46607852387868, 48.15918129627695),

                (42.41711325944411, 23.95918129627695, 40.46607852387871, 23.95918129627695),
                (40.46607852, 23.95918129627695, 38.51504379, 23.95918129627695),
            };




            List<(double x1, double y1, double x2, double y2)> linesb = new List<(double, double, double, double)>
            {
                (40.46607852387871, 20.49990370292084, 40.46607852387871, 44.39821654787557),
                (40.46607852387871, 20.49999999999989, 38.51504378831319, 20.49999999999989),
                (42.41711325944411, 20.49999999999989, 40.46607852387871, 20.49999999999989),
                (40.46607852, 44, 38.51504379, 44),
                (42.41711326, 44, 40.46607852, 44),
            };

            if (fukashiSTPteichakutenba2.IsChecked == true)
            {

                foreach (var (x1, y1, x2, y2) in linesb)
                {
                    DrawLine1PhatSaigo(x1, y1, x2, y2);
                }
            }
            foreach (var (x1, y1, x2, y2) in lines1)
            {
                DrawLine1PhatSaigo(x1, y1, x2, y2);
            }
            List<(double x1, double y1, double x2, double y2)> lines2 = new List<(double, double, double, double)>
            {

                (40.46607852387869, 61.49999999999989, 40.46607852387869, 64.9592938915826),
                (40.46607852, 64.95929389, 40.46607852387869, 64.95929389),
                (40.46607852, 61.6, 40.46607852387869, 61.6),

                (-0.466078523878565, 23.9592938915827, -0.466078523878565, 61.49999999999998),
                (-0.466078523878565, 61.49999999999999, -2.417113259444079, 61.49999999999999),
                (1.484956211686836, 61.49999999999989, -0.466078523878565, 61.49999999999989),
                (1.484956211686836, 61.49999999999999, -0.466078523878565, 61.49999999999999),

                (-0.466078523878565, 23.9592938915827, -2.417113259444079, 23.9592938915827),

                (1.484956211686836, 23.9592938915827, -0.466078523878565, 23.9592938915827),
                (38.51504379, 61.5, 42.41711326, 61.5),
                (42.41711326, 64.95929389, 38.51504379, 64.95929389),
            };
            List<(double x1, double y1, double x2, double y2)> linesv = new List<(double, double, double, double)>
            {
                (40.46607852, 48.24070611, 38.51504379, 48.24070611),
                (42.41711326, 48.24070611, 40.46607852, 48.24070611),
                (40.46607852, 44.78141222, 40.46607852, 48.24070611),
                (40.46607852387871, 44.39821654787557, 38.51504378831319, 44.39821654787557),
                (42.41711325944411, 44.39821654787557, 40.46607852387871, 44.39821654787557),
                (-0.466078523878565, 20.49999999999989, -0.466078523878565, 61.49999999999989),
                (-0.466078523878565, 20.49999999999989, -2.417113259444079, 20.49999999999989),
                (1.484956211686836, 20.49999999999989, -0.466078523878565, 20.49999999999989),
            };

            if (fukashiSTPteichakutenba2.IsChecked == true)
            {
                foreach (var (x1, y1, x2, y2) in linesv)
                {
                    DrawLine2PhatSaigo(x1, y1, x2, y2);
                }
            }
            foreach (var (x1, y1, x2, y2) in lines2)
            {
                DrawLine2PhatSaigo(x1, y1, x2, y2);
            }
            List<(double x1, double y1, double x2, double y2)> lines3 = new List<(double, double, double, double)>
            {
                (40.46607852387868, 48.49999999999988, 40.46607852387868, 61.29999999999988),
                (42.41711325944408, 61.29999999999988, 40.46607852387868, 61.29999999999988),
                (40.46607852387868, 48.6, 38.51504378831316, 48.6),
                (42.41711325944408, 48.6, 40.46607852387868, 48.6),
                (40.46607852387868, 61.29999999999988, 38.51504378831316, 61.29999999999988),
            };
            foreach (var (x1, y1, x2, y2) in lines3)
            {
                DrawLine3PhatSaigo(x1, y1, x2, y2);
            }
            List<(double x1, double y1, double x2, double y2, double n)> lines4 = new List<(double, double, double, double, double)>
            {
                (9.01405942, 60.95929389, 30.97396935, 60.95929389,1),

                (7.05458869, 58.91956978, 7.05458869, 24.37886368,1),

                (32.97356677, 58.99942058, 32.97356677, 24.37886368,1),
                (135.8973860754106, 50.48790604095875, 100.8973860754106, 50.48790604095875,0),
                (152.2270298355221, 50.48790517941145, 192.8973860754106, 50.48790604095875,0),
                (186.1790261450734, 48.97036987039817, 221.0132758158068, 48.97036987039817,0),
                (152.2270298355221, 50.48790517941146, 221.1295431087692, 50.48790659133088,0),
                (152.227026270127, 50.48790517941165, 175.8973825100156, 50.4879056808366,0),
                (178.8968192, 47.54603933, 178.8968192, 41.54603933,0),
                (183.2377231, 46.02906682, 183.2377231, 40.02906682,0),
                (195.89682277, 47.54603969, 195.89682277, 41.54603969,0),
                (201.12182979, 47.54660263, 201.12182979, 41.54603969,0),
                (138.89682277, 47.54603969, 138.89682277, 41.54603969,0),
                (204.06313284, 50.48790568, 221.12953954, 50.48790568,0),
            };
            List<(double x1, double y1, double x2, double y2, double n)> linesa = new List<(double, double, double, double, double)>
            {
                (7.05458869, 58.91956978, 7.05458869, 20.91956978,1),
                (32.97356677, 58.99942058, 32.97356677, 20.99942058,1),
            };
            if (fukashiSTPteichakutenba2.IsChecked == true)
            {
                foreach (var (x1, y1, x2, y2, n) in linesa)
                {
                    DrawLine4PhatSaigo(x1, y1, x2, y2, n);
                }
            }
            foreach (var (x1, y1, x2, y2, n) in lines4)
            {
                DrawLine4PhatSaigo(x1, y1, x2, y2, n);
            }
            List<(double x1, double y1, double x2, double y2)> lines5 = new List<(double, double, double, double)>
            {
                (152.2559656561345, 56.38515571644364, 152.2559656561345, 36.96605978034915),
                (152.2559656561345, 36.96605978034915,157.3,33.7),
                (150.2270300749635, 35.51031067911089, 153.3696698211909, 33.73442976347287),
                (152.2559656561345, 26.38515571644364, 152.2559656561345, 11.47746639),
                (150.22703007, 56.38515571644364, 150.22703007, 35.51031068),
                (147,31,152,26.5),
                (150.7,30.7,154.2,27.4),
                (154.2,27.4,154.2,11.5),
                (157.3696698211909, 33.73442976347196, 150.7705428758374, 30.62293718310975),
                (153.3696698211909, 33.73442976347287, 147.3990371021292, 30.99328867515012),
                (2.05458869, 48.53074846, 38.01325427, 48.53074846),
                (-3.41711326, 64.95929389, 43.41711326, 64.95929389),
                (2.05458869, 15.04070611, 2.05458869, 64.95929389),
                (38.01325427, 64.95929389, 38.01325427, 15.04070611),
                (38.01325427, 15.04070611, 2.05458869, 15.04070611),
            };
            foreach (var (x1, y1, x2, y2) in lines5)
            {
                DrawLine5PhatSaigo(x1, y1, x2, y2);
            }
            // Danh sách các đường cần xóa

        }

        private void DrawArcsPhatSaigo()
        {
            List<(double cx, double cy, double r, double startAngle, double endAngle, double n)> arcs = new List<(double, double, double, double, double, double)>
            {
                (186.2371597915549, 45.97093317675939, 3.0, 180, 90,0),
                (204.1212664857637, 47.48846898719781, 3.0, 180, 90,0),
                (192.8973860754107, 47.48790604095882, 3, 90, 0,0),
                (135.8973860754107, 47.48790604095882, 3, 90, 0,0),
                (175.8973825100157, 47.48790568083667, 3, 90, 0,0),
                (30.97396934998969, 58.95929389158272, 2.0, 90, 0,1),
                (9.05418611, 58.95969647, 2.0, 180, 90,1)
            };

            foreach (var (cx, cy, r, startAngle, endAngle, n) in arcs)
            {
                DrawArcPhatSaigo(cx, cy, r, startAngle, endAngle, n);
            }
        }

        private void DrawCirclesPhatSaigo()
        {
            DrawCirclePhatSaigo(30.30561851436983, 42.52797883371036, 1.790744420494063);
            DrawCirclePhatSaigo(9.7694367121724, 42.53074845679014, 1.790744420494063);
        }

        private void DrawTexts()
        {
            DrawTextPhatSaigo(47.03394913347243, 65, "-かぶり（本体梁リストより）", 2.5, Brushes.Blue);
            DrawTextPhatSaigo(47.03394913347243, 53.88088932552301, "-ふかし長さ-かぶり", 2.5, Brushes.Orange);
            DrawTextPhatSaigo(47.03394913347243, 35.5, "-定着", 2.5, Brushes.Red);
        }

        private void DrawPolygonPhatSaigo(double[,] coords)
        {
            double offsetX = 0;
            double offsetY = 250;
            Polygon polygon = new Polygon { Fill = Brushes.LightGray };
            for (int i = 0; i < coords.GetLength(0); i++)
            {
                double x = coords[i, 0] * scale + offsetX;
                double y = -coords[i, 1] * scale + offsetY;
                polygon.Points.Add(new Point(x, y));
            }
            drawBeamDiagramPhatSaigo1.Children.Add(polygon);
        }
        private void DrawPolygon1PhatSaigo(double[,] coords)
        {
            double offsetX = 0;
            double offsetY = 250;
            Polygon polygon = new Polygon { Fill = new SolidColorBrush(Color.FromRgb(179, 179, 179)) };
            for (int i = 0; i < coords.GetLength(0); i++)
            {
                double x = coords[i, 0] * scale + offsetX;
                double y = -coords[i, 1] * scale + offsetY;
                polygon.Points.Add(new Point(x, y));
            }
            drawBeamDiagramPhatSaigo1.Children.Add(polygon);
        }

        private void DrawEllipsePhatSaigo(double centerX, double centerY, double radius)
        {
            double offsetX = 0;
            double offsetY = 250;
            Ellipse ellipse = new Ellipse
            {
                Width = radius * 2 * scale,
                Height = radius * 2 * scale,
                Fill = Brushes.Yellow
            };
            Canvas.SetLeft(ellipse, centerX * scale + offsetX - ellipse.Width / 2);
            Canvas.SetTop(ellipse, -centerY * scale + offsetY - ellipse.Height / 2);
            drawBeamDiagramPhatSaigo1.Children.Add(ellipse);
        }

        private void DrawLinePhatSaigo(double x1, double y1, double x2, double y2)
        {
            double offsetX = 0;
            double offsetY = 250;
            Line line = new Line
            {
                X1 = x1 * scale + offsetX,
                Y1 = -y1 * scale + offsetY,
                X2 = x2 * scale + offsetX,
                Y2 = -y2 * scale + offsetY,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            drawBeamDiagramPhatSaigo1.Children.Add(line);
            drawnLines.Add(line);
        }
        private void DrawLine1PhatSaigo(double x1, double y1, double x2, double y2)
        {
            double offsetX = 0;
            double offsetY = 250;
            Line line = new Line
            {
                X1 = x1 * scale + offsetX,
                Y1 = -y1 * scale + offsetY,
                X2 = x2 * scale + offsetX,
                Y2 = -y2 * scale + offsetY,
                Stroke = Brushes.Red,
                StrokeThickness = 2
            };
            drawBeamDiagramPhatSaigo1.Children.Add(line);
            drawnLines.Add(line);
        }
        private void DrawLine2PhatSaigo(double x1, double y1, double x2, double y2)
        {
            double offsetX = 0;
            double offsetY = 250;
            Line line = new Line
            {
                X1 = x1 * scale + offsetX,
                Y1 = -y1 * scale + offsetY,
                X2 = x2 * scale + offsetX,
                Y2 = -y2 * scale + offsetY,
                Stroke = Brushes.Blue,
                StrokeThickness = 3
            };
            drawBeamDiagramPhatSaigo1.Children.Add(line);
            drawnLines.Add(line);
        }
        private void DrawLine3PhatSaigo(double x1, double y1, double x2, double y2)
        {
            double offsetX = 0;
            double offsetY = 250;
            Line line = new Line
            {
                X1 = x1 * scale + offsetX,
                Y1 = -y1 * scale + offsetY,
                X2 = x2 * scale + offsetX,
                Y2 = -y2 * scale + offsetY,
                Stroke = Brushes.Orange,
                StrokeThickness = 2
            };
            drawBeamDiagramPhatSaigo1.Children.Add(line);
            drawnLines.Add(line);
        }
        private void DrawLine4PhatSaigo(double x1, double y1, double x2, double y2, double n)
        {
            double offsetX = 0;
            double offsetY = 250;
            double a;
            if (n == 1) { a = 5; } else { a = 3; }
            Line line = new Line
            {
                X1 = x1 * scale + offsetX,
                Y1 = -y1 * scale + offsetY,
                X2 = x2 * scale + offsetX,
                Y2 = -y2 * scale + offsetY,
                Stroke = Brushes.Black,
                StrokeThickness = a
            };
            drawBeamDiagramPhatSaigo1.Children.Add(line);
            drawnLines.Add(line);
        }
        private void DrawLine5PhatSaigo(double x1, double y1, double x2, double y2)
        {
            double offsetX = 0;
            double offsetY = 250;
            Line line = new Line
            {
                X1 = x1 * scale + offsetX,
                Y1 = -y1 * scale + offsetY,
                X2 = x2 * scale + offsetX,
                Y2 = -y2 * scale + offsetY,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
            drawBeamDiagramPhatSaigo1.Children.Add(line);
            drawnLines.Add(line);
        }

        private void DrawArcPhatSaigo(double centerX, double centerY, double radius, double startAngle, double endAngle, double n)
        {
            double offsetY = 250;
            double a;
            if (n == 1) { a = 5; } else { a = 3; }
            double startRad = Math.PI / 180 * startAngle;
            double endRad = Math.PI / 180 * endAngle;
            double offsetX = 0;
            Point startPoint = new Point(
                centerX * scale + offsetX + radius * scale * Math.Cos(startRad),
                -centerY * scale + offsetY - radius * scale * Math.Sin(startRad)
            );
            Point endPoint = new Point(
                centerX * scale + offsetX + radius * scale * Math.Cos(endRad),
                -centerY * scale + offsetY - radius * scale * Math.Sin(endRad)
            );

            Path path = new Path
            {
                Stroke = Brushes.Black,
                StrokeThickness = a,
                Data = new PathGeometry
                {
                    Figures = new PathFigureCollection
                    {
                        new PathFigure
                        {
                            StartPoint = startPoint,
                            Segments = new PathSegmentCollection
                            {
                                new ArcSegment
                                {
                                    Point = endPoint,
                                    Size = new Size(radius * scale, radius * scale),
                                    SweepDirection = SweepDirection.Clockwise,
                                    IsLargeArc = Math.Abs(endAngle - startAngle) > 180
                                }
                            }
                        }
                    }
                }
            };
            drawBeamDiagramPhatSaigo1.Children.Add(path);
            drawnArcs.Add(path);
        }
        private void DrawCirclePhatSaigo(double centerX, double centerY, double radius)
        {
            double offsetX = 0;
            double offsetY = 250;
            Ellipse ellipse = new Ellipse
            {
                Width = radius * 2 * scale,
                Height = radius * 2 * scale,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };
            Canvas.SetLeft(ellipse, centerX * scale + offsetX - ellipse.Width / 2);
            Canvas.SetTop(ellipse, -centerY * scale + offsetY - ellipse.Height / 2);
            drawBeamDiagramPhatSaigo1.Children.Add(ellipse);
        }

        private void DrawTextPhatSaigo(double x, double y, string text, double height, Brush color)
        {
            double offsetX = 0;
            double offsetY = 250;
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                FontSize = (height * scale) + 3.5,
                Foreground = color
            };
            Canvas.SetLeft(textBlock, x * scale + offsetX);
            Canvas.SetTop(textBlock, -y * scale + offsetY);
            drawBeamDiagramPhatSaigo1.Children.Add(textBlock);
        }

        //#### Bắt đầu vẽ hình 施工図の設定4 ####
        private void DrawBeamDiagramKha21()
        {
            if (MainCanvasKha21 == null) return;

            MainCanvasKha21.Children.Clear();

            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush concreteLineBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush text300 = new SolidColorBrush(Colors.Blue);
            SolidColorBrush lightgreen = new SolidColorBrush(Colors.LightGreen);
            SolidColorBrush purple = new SolidColorBrush(Colors.HotPink);

            double scaleFactor = 0.7; // スケールファクター（座標範囲が広いため小さめに設定）
            double offsetX = 150; // オフセット（図面をキャンバスの中央に配置）
            double offsetY = 110;
            double steelLineThickness = 1;
            double textthickness = 2;
            double vienkhung = 1;

            // DXFデータをシミュレート（実際のアプリケーションではDXFファイルをパース）
            DrawEntitiesKha21(scaleFactor, offsetX, offsetY, concreteLineBrush, steelBrush, textBrush, text300, lightgreen, purple, steelLineThickness, textthickness, vienkhung);
        }

        private void DrawEntitiesKha21(double scaleFactor, double offsetX, double offsetY, SolidColorBrush concreteLineBrush,
                           SolidColorBrush steelBrush, SolidColorBrush textBrush, SolidColorBrush text300,
                           SolidColorBrush lightgreen, SolidColorBrush purple, double steelLineThickness, double textthickness, double vienkhung)
        {

            DrawLineKha21(-136.3741017178741, 194.25, -136.3741017178741, -55.75, lightgreen, scaleFactor, offsetX, offsetY, steelLineThickness);// Left green line
            DrawLineKha21(138.6258982821259, 194.25, 138.6258982821259, -55.75, lightgreen, scaleFactor, offsetX, offsetY, steelLineThickness);// right green line
            DrawLineKha21(-28.87410171787405, 59.25, 31.12589828212595, 59.25, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness);
            DrawLineKha21(31.12589828212595, 59.25, 31.12589828212595, 159.25, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// phải hình chữ nhật
            DrawLineKha21(-28.87410171787405, 159.25, -28.87410171787405, 59.25, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// dưới hình chữ nhật
            DrawLineKha21(-28.87410171787405, 155.0015, -28.87410171787405, 159.25, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness);
            DrawLineKha21(-21.87410171787405, 159.25, -21.87410171787405, 146.25, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness);
            DrawLineKha21(24.12589828212595, 159.25, 24.12589828212595, 146.25, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness);
            DrawLineKha21(24.59629828212619, 148.4718000000001, 31.12589828212595, 155.0015, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness);
            DrawLineKha21(31.12589828212595, 159.25, -28.87410171787405, 159.25, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness);// trên hình chữ nhật
            DrawLineKha21(-276.3741017178741, 194.25, 276.1258982821259, 194.25, concreteLineBrush, scaleFactor, offsetX, offsetY, vienkhung);// trên cùng
            DrawLineKha21(276.1258982821259, 194.25, 276.3741017178741, -169.1585309186263, concreteLineBrush, scaleFactor, offsetX, offsetY, vienkhung);// bên phải
            DrawLineKha21(276.3741017178741, -169.1585309186263, -273.8741017178741, -169.1585309186263, concreteLineBrush, scaleFactor, offsetX, offsetY, vienkhung);// dưới
            DrawLineKha21(-273.8741017178741, -169.1585309186263, -273.8741017178741, 194.25, concreteLineBrush, scaleFactor, offsetX, offsetY, vienkhung);// trái

            // 2. TEXTエンティティの描画
            DrawTextKha21("520x820", -35, 30, steelBrush, scaleFactor, offsetX, offsetY, 2);
            DrawTextKha21("(P56)", -20, -30, steelBrush, scaleFactor, offsetX, offsetY, 2);
            DrawTextKha21("520", -20, -90, steelBrush, scaleFactor, offsetX, offsetY, 2);
            DrawTextKha21("[D10@1000]", -55, -120, steelBrush, scaleFactor, offsetX, offsetY, 2);
            DrawTextKha21("(P7)", -20, -150, steelBrush, scaleFactor, offsetX, offsetY, 2);
            if (STPzaishitsuon1.IsChecked == true)
            {
                DrawTextKha21("D13@100(SD295)", -75, -3, steelBrush, scaleFactor, offsetX, offsetY, textthickness);

            }
            else if (STPzaishitsuon2.IsChecked == true)
            {
                DrawTextKha21("D13@100", -40, -3, steelBrush, scaleFactor, offsetX, offsetY, 10);

            }

        }
        // ブロック参照を仮に円として描画するメソッド

        private Line DrawLineKha21(double x1, double y1, double x2, double y2, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            Line line = new Line
            {
                Stroke = brush,
                StrokeThickness = thickness
            };

            Point start = ScalePoint(x1, y1, scaleFactor, offsetX, offsetY);
            Point end = ScalePoint(x2, y2, scaleFactor, offsetX, offsetY);

            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;

            MainCanvasKha21.Children.Add(line);
            return line;
        }

        private void DrawTextKha21(string text, double x, double y, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double textthickness)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 20 * scaleFactor, // フォントサイズをスケールに合わせて調整
                //FontFamily = new FontFamily("MS Gothic"), // 日本語フォントをサポート

            };

            Point position = ScalePoint(x, y, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(textBlock, position.X);
            Canvas.SetTop(textBlock, position.Y - textBlock.FontSize); // テキストを上に調整

            MainCanvasKha21.Children.Add(textBlock);
        }
        //#### Kết thúc vẽ hình 施工図の設定4 ####

        //#### Bắt đầu vẽ hình 施工図の設定7 ####
        private void DrawBeamDiagramKha22()
        {
            if (MainCanvasKha22 == null) return;

            MainCanvasKha22.Children.Clear();

            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush concreteLineBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush text300 = new SolidColorBrush(Colors.Blue);
            SolidColorBrush lightgreen = new SolidColorBrush(Colors.LightSkyBlue);
            SolidColorBrush purple = new SolidColorBrush(Colors.HotPink);

            double scaleFactor = 0.7; // スケールファクター（座標範囲が広いため小さめに設定）
            double offsetX = 0; // オフセット（図面をキャンバスの中央に配置）
            double offsetY = 0;
            double steelLineThickness = 1;
            double textthickness = 2;
            double vienkhung = 1;

            // DXFデータをシミュレート（実際のアプリケーションではDXFファイルをパース）
            DrawEntitiesKha22(scaleFactor, offsetX, offsetY, concreteLineBrush, steelBrush, textBrush, text300, lightgreen, purple, steelLineThickness, textthickness, vienkhung);
        }

        private void DrawEntitiesKha22(double scaleFactor, double offsetX, double offsetY, SolidColorBrush concreteLineBrush,
                                    SolidColorBrush steelBrush, SolidColorBrush textBrush, SolidColorBrush text300,
                                    SolidColorBrush lightgreen, SolidColorBrush purple, double steelLineThickness, double textthickness, double vienkhung)
        {
            // LINEエンティティの描画
            DrawLineKha22(211.289, 0.394, 211.289, -304.773, textBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Red line
            DrawLineKha22(161.289, 0.394, 161.289, -304.773, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // The left of red line
            DrawLineKha22(261.289, 0.394, 261.289, -304.773, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // The right of red line
            DrawLineKha22(8.789, -18.870, 211.289, -18.870, lightgreen, scaleFactor, offsetX, offsetY, steelLineThickness); // Left Blue line
            DrawLineKha22(211.289, -18.870, 537.069, -18.870, lightgreen, scaleFactor, offsetX, offsetY, steelLineThickness); // Right Blue line

            DrawLineKha22(0.0, 0.0, 0.0, -305.167, concreteLineBrush, scaleFactor, offsetX, offsetY, vienkhung); // viền trái
            DrawLineKha22(0.0, -305.167, 537.069, -305.167, concreteLineBrush, scaleFactor, offsetX, offsetY, vienkhung); // viền dưới
            DrawLineKha22(0.0, 0.0, 537.069, 0.0, concreteLineBrush, scaleFactor, offsetX, offsetY, vienkhung); // viền trên
            DrawLineKha22(537.069, 0.0, 537.069, -305.167, concreteLineBrush, scaleFactor, offsetX, offsetY, vienkhung); // viền phải         

            if (matomeprint1.IsChecked == true)
            {
                DrawTextKha22("300", 112.041, -84.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 107
                DrawTextKha22("300", 112.041, -134.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 109
                DrawTextKha22("300", 112.041, -184.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 10B
                DrawTextKha22("300", 112.041, -234.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 10D
                DrawTextKha22("300", 112.041, -284.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 10F

                DrawTextKha22("D29-4300", 325.257, -62.135, text300, scaleFactor, offsetX, offsetY, textthickness); // 117
                DrawTextKha22("D29-3800", 300.257, -112.135, text300, scaleFactor, offsetX, offsetY, textthickness); // 119           
                DrawTextKha22("D29-4300", 325.257, -162.135, text300, scaleFactor, offsetX, offsetY, textthickness); // 11B           
                DrawTextKha22("D29-3800", 300.257, -212.135, text300, scaleFactor, offsetX, offsetY, textthickness); // 11D           
                DrawTextKha22("D29-4300", 325.257, -262.135, text300, scaleFactor, offsetX, offsetY, textthickness); // 11F

                DrawEllipseKha22(513.289, -68.870, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true); // 110 (円形、HATCHあり)
                DrawEllipseKha22(463.289, -118.870, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true); // 112 (円形、HATCHあり)
                DrawEllipseKha22(513.289, -168.870, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true); // 114 (円形、HATCHあり)
                DrawEllipseKha22(463.289, -218.870, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true); // 112 (円形、HATCHあり)
                DrawEllipseKha22(513.289, -268.870, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true); // 114 (円形、HATCHあり)

                DrawLineKha22(174.289, -68.870, 537.069, -68.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thép ngang 1
                DrawLineKha22(174.289, -118.870, 537.069, -118.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thép ngang 2 
                DrawLineKha22(174.289, -168.870, 537.069, -168.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thép ngang 3 
                DrawLineKha22(174.289, -218.870, 537.069, -218.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép ngang 4
                DrawLineKha22(174.289, -268.870, 537.069, -268.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép ngnag 5
                DrawLineKha22(174.289, -68.870, 174.289, -98.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 1
                DrawLineKha22(174.289, -118.870, 174.289, -148.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 2
                DrawLineKha22(174.289, -168.870, 174.289, -198.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 3
                DrawLineKha22(174.289, -218.870, 174.289, -248.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 4
                DrawLineKha22(174.289, -268.870, 174.289, -298.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 5
            }
            else if (matomeprint2.IsChecked == true)
            {
                DrawTextKha22("300", 112.041, -134.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 109
                DrawTextKha22("300", 112.041, -184.870, text300, scaleFactor, offsetX, offsetY, textthickness); // 10B

                DrawLineKha22(174.289, -118.870, 537.069, -118.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thép ngang 2 
                DrawLineKha22(174.289, -168.870, 537.069, -168.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thép ngang 3 

                DrawLineKha22(174.289, -118.870, 174.289, -148.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 2
                DrawLineKha22(174.289, -168.870, 174.289, -198.870, steelBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // thép dọc 3

                DrawTextKha22("D29-3800 x 2", 300.257, -112.135, text300, scaleFactor, offsetX, offsetY, textthickness); // 119           
                DrawTextKha22("D29-4300 x 3", 325.257, -162.135, text300, scaleFactor, offsetX, offsetY, textthickness); // 11B    

                DrawTextKha22("ﾆｹﾞ130", 17.645, -109.238, purple, scaleFactor, offsetX, offsetY, textthickness); // 25B

                DrawEllipseKha22(463.289, -118.870, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true); // 112 (円形、HATCHあり)
                DrawEllipseKha22(513.289, -168.870, 3.0, 3.0, steelBrush, scaleFactor, offsetX, offsetY, true); // 114 (円形、HATCHあり)
            }

            // 固定
            DrawTextKha22("上筋", 17.645, -49.238, steelBrush, scaleFactor, offsetX, offsetY, textthickness); // 135

        }

        private Line DrawLineKha22(double x1, double y1, double x2, double y2, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            Line line = new Line
            {
                Stroke = brush,
                StrokeThickness = thickness
            };

            Point start = ScalePoint(x1, y1, scaleFactor, offsetX, offsetY);
            Point end = ScalePoint(x2, y2, scaleFactor, offsetX, offsetY);

            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;

            MainCanvasKha22.Children.Add(line);
            return line;
        }

        private void DrawTextKha22(string text, double x, double y, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double textthickness)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 20 * scaleFactor, // フォントサイズをスケールに合わせて調整
                //FontFamily = new FontFamily("MS Gothic") // 日本語フォントをサポート
            };

            Point position = ScalePoint(x, y, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(textBlock, position.X);
            Canvas.SetTop(textBlock, position.Y - textBlock.FontSize); // テキストを上に調整

            MainCanvasKha22.Children.Add(textBlock);
        }

        private void DrawEllipseKha22(double centerX, double centerY, double majorRadius, double minorRadius, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, bool isFilled)
        {
            Ellipse ellipse = new Ellipse
            {
                Stroke = brush,
                StrokeThickness = 1,
                Fill = isFilled ? brush : null,
                Width = majorRadius * 3 * scaleFactor,
                Height = minorRadius * 3 * scaleFactor
            };

            Point center = ScalePoint(centerX, centerY, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(ellipse, center.X - ellipse.Width / 2);
            Canvas.SetTop(ellipse, center.Y - ellipse.Height / 2);

            MainCanvasKha22.Children.Add(ellipse);
        }

        //#### Kết thúc vẽ hình 施工図の設定7 ####

        //#### Bắt đầu vẽ hình 施工図の設定6 ####
        private void DrawBeamDiagramKha23()
        {
            if (MainCanvasKha23 == null) return;

            MainCanvasKha23.Children.Clear();

            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGreen);
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush concreteLineBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Red);
            SolidColorBrush text300 = new SolidColorBrush(Colors.Blue);
            SolidColorBrush lightblue = new SolidColorBrush(Colors.LightSkyBlue);
            SolidColorBrush purple = new SolidColorBrush(Colors.HotPink);

            double scaleFactor = 0.5; // スケールファクター（座標範囲が広いため小さめに設定）
            double offsetX = 200; // オフセット（図面をキャンバスの中央に配置）
            double offsetY = 150;
            double steelLineThickness = 1;
            double textthickness = 2;
            double vienkhung = 1;

            // DXFデータをシミュレート（実際のアプリケーションではDXFファイルをパース）
            DrawEntitiesKha23(scaleFactor, offsetX, offsetY, concreteLineBrush, concreteBrush, steelBrush, textBrush, text300, lightblue, purple, steelLineThickness, textthickness, vienkhung);
        }

        private void DrawEntitiesKha23(double scaleFactor, double offsetX, double offsetY, SolidColorBrush concreteLineBrush, SolidColorBrush concreteBrush,
                              SolidColorBrush steelBrush, SolidColorBrush textBrush, SolidColorBrush text300,
                              SolidColorBrush lightblue, SolidColorBrush purple, double steelLineThickness, double textthickness, double vienkhung)
        {
            // レイヤーごとのブラシを選択するヘルパーメソッド
            SolidColorBrush GetBrushForLayer(string layer, int colorIndex)
            {
                if (layer.Contains("上筋") || layer.Contains("上宙1")) return steelBrush; // 鉄筋
                if (colorIndex == 3) return concreteBrush; // HATCH用のグレー
                if (colorIndex == 4) return lightblue; // 特定の色番号に基づくブラシ
                if (colorIndex == 5) return textBrush; // テキスト用
                if (colorIndex == 6) return text300; // 特定の色番号に基づくブラシ
                if (colorIndex == 7) return steelBrush;
                if (colorIndex == 8) return purple;
                return concreteLineBrush; // デフォルト
            }
            if (tsuryoikion2.IsChecked != true)
            {
                var hatches = new List<(List<(double x1, double y1, double x2, double y2)> boundaries, string layer, int colorIndex)>
                {
                    // 最初のHATCHエンティティ（例）
                    (new List<(double, double, double, double)>
                    {
                        (-66.625, 210.350, 105.875, 210.350),
                        (105.875, 210.350, 111.875, 210.350),
                        (111.875, 210.350, 208.375, 210.350),
                        (208.375, 210.350, 208.375, 160.350),
                        (208.375, 160.350, 208.375, 110.350),
                        (208.375, 110.350, 208.375, 60.350),
                        (208.375, 60.350, 208.375, 10.350),
                        (208.375, 10.350, -66.625, 10.350),
                        (-66.625, 10.350, -66.625, 60.350),
                        (-66.625, 60.350, -66.625, 110.350),
                        (-66.625, 110.350, -66.625, 160.350),
                        (-66.625, 160.350, -66.625, 210.350),
                    }, "1", 3),
                    // 2番目のHATCHエンティティ
                    (new List<(double, double, double, double)>
                    {(-204, 10, -67, 10),
                        (-67, 10, -67, -171),
                        (-67, -171, -204, -171),
                        (-204, -171, -204, 10),
                    }, "1", 3),
                    // 3番目のHATCHエンティティ
                    (new List<(double, double, double, double)>
                    {
                         (208.375, 10, 346, 10),
                        (346, 10, 346, -170.650),
                        (346, -170.650, 208.375, -170.650),
                        (208.375, -170.650, 208.375, 10),
                    }, "1", 3),
                     // 4番目のHATCHエンティティ
                    (new List<(double, double, double, double)>
                    {
                        (446, 10, 517, 10),
                        (517, 10, 517, -171),
                        (517, -171, 446, -171),
                        (446, -171, 446, 10),
                    }, "1", 3),
                };
                foreach (var hatch in hatches)
                {
                    Polygon polygon = new Polygon
                    {
                        Stroke = concreteBrush,
                        StrokeThickness = vienkhung,
                        Fill = GetBrushForLayer(hatch.layer, hatch.colorIndex)
                    };

                    foreach (var boundary in hatch.boundaries)
                    {
                        Point start = ScalePoint(boundary.x1, boundary.y1, scaleFactor, offsetX, offsetY);
                        polygon.Points.Add(start);
                    }

                    MainCanvasKha23.Children.Add(polygon);
                }
            }
            // HATCHエンティティの描画

            // Add missing lines
            DrawLineKha23(-254.125, 343.716, -254.125, -170.650, GetBrushForLayer("1", 5), scaleFactor, offsetX, offsetY, steelLineThickness); // Đỏ trái
            DrawLineKha23(395.875, 343.716, 395.875, -170.650, GetBrushForLayer("1", 5), scaleFactor, offsetX, offsetY, steelLineThickness); // Đỏ phải
            // LINEエンティティの描画
            var lines = new List<(double x1, double y1, double x2, double y2, string layer, int colorIndex)>
            {
                // DXFデータから抽出したすべてのLINEエンティティ

                (-304, 344, -304, -171, "0", 0),// Đen dọc 1 (tính từ trái qua)
                (-204, 344, -204, -171, "0", 0),// đen dọc 2               
                (346, 344, 346, -171, "0", 0),// đen dọc 3
                (446, 344, 446, -171, "0", 0),// đen dọc 4

                (-185, 344, -185, 300, "0", 0),// Chữ u trái (thanh 1)
                (-185, 300, -85, 300, "0", 0),//Chữ u trái (thanh 2)
                (-85, 300, -85, 344, "0", 0),//Chữ u trái (thanh 3)
                (-66.625, 343.716, -66.625, -170.650, "span_wari", 3),// xanh lục trái
                (20.875, 343.716, 20.875, 300.350, "0", 0),//Chữ u giữa (thanh 1)
                (20.875, 300.350, 120.875, 300.350, "0", 0),//Chữ u giữa (thanh 2)
                (120.875, 300.350, 120.875, 343.716, "0", 0),//Chữ u giữa (thanh 3)
                (208.375, 343.716, 208.375, -170.650, "span_wari", 3),// xanh lục phải
                (227.125, 343.716, 227.125, 300.350, "0", 0),//Chữ u phải (thanh 1)
                (227.125, 300.350, 327.125, 300.350, "0", 0),//Chữ u phải (thanh 2)
                (327.125, 300.350, 327.125, 343.716, "0", 0),//Chữ u phải (thanh 3)

                 (467, 344, 467, 300, "0", 0),//Chữ u thứ 4 (thanh 1)
                 (467, 300, 517, 300, "0", 0),//Chữ u thứ 4 (thanh 2)

                (-456.625, 260.350, 517, 260.350, "-ve_ngang_span", 4),//xanh lam trên
                (-457.125, 10.350, 517, 10.350, "-ve_ngang_span", 4),// xanh lam dưới
                (-291.125, 210.350, 517, 210.350, "上筋", 0),// đen ngang 1
                (-291.125, 160.350, 517, 160.350, "上筋", 0),// đen ngang 2
                (-291.125, 110.350, 517, 110.350, "上筋", 0),// đen ngang 3
                (-291.125, 60.350, -16.125, 60.350, "上筋", 0),// đen ngang 4 trái
                (158.375, 60.350, 517, 60.350, "上筋", 0),// đen ngang 4 phải
                (-291.125, 210.350, -291.125, 180.350, "上筋 アンカ左下", 0),//móc ngang 1
                (-291.125, 160.350, -291.125, 130.350, "上筋 アンカ左下", 0),//móc ngang 2
                (-291.125, 110.350, -291.125, 80.350, "上筋 アンカ左下", 0),//móc ngang 3
                (-291.125, 60.350, -291.125, 30.350, "上筋 アンカ左下", 0),//móc ngang 4
                (-291.625, -39.650, 517, -39.650, "上宙1", 0),//đen ngang 5
                (-291.625, -89.650, 517, -89.650, "上宙1", 0),//đen ngang 6
                (-291.625, -139.650, 517, -139.650, "上宙1", 0),// đen ngang 7
                (-291.625, -39.650, -291.625, -9.650, "上宙1 アンカ左下", 0),// móc ngang 5
                (-291.625, -89.650, -291.625, -59.650, "上宙1 アンカ左下", 0),// móc ngang 6
                (-291.625, -139.650, -291.625, -109.650, "上宙1 アンカ左下", 0),//móc ngang 7
            };

            foreach (var line in lines)
            {
                SolidColorBrush brush = GetBrushForLayer(line.layer, line.colorIndex);
                DrawLineKha23(line.x1, line.y1, line.x2, line.y2, brush, scaleFactor, offsetX, offsetY, steelLineThickness);
            }
            if (tsuryoikion2.IsChecked != true)
            {
                if (tsuryoikion2.IsChecked != true)
                {
                    var hatches = new List<(List<(double x1, double y1, double x2, double y2)> boundaries, string layer, int colorIndex)>
                    {
                        // HATCH entities
                    };
                    foreach (var hatch in hatches)
                    {
                        Polygon polygon = new Polygon
                        {
                            Stroke = concreteBrush,
                            StrokeThickness = vienkhung,
                            Fill = GetBrushForLayer(hatch.layer, hatch.colorIndex)
                        };

                        foreach (var boundary in hatch.boundaries)
                        {
                            Point start = ScalePoint(boundary.x1, boundary.y1, scaleFactor, offsetX, offsetY);
                            polygon.Points.Add(start);
                        }

                        MainCanvasKha23.Children.Add(polygon);
                    }


                }

            }
            ;

            // TEXTエンティティの描画
            var texts = new List<(string text, double x, double y, int colorIndex)>
            {
               // DXFデータから抽出したすべてのTEXTエンティティ
                ("300", -345, 190, 6),
                ("300", -345, 140, 6),
                ("300", -345, 90, 6),
                ("300", -345, 40, 6),
                ("300", -345, -30, 6),
                ("300", -345, -80, 6),
                ("300", -345, -130, 6),
                ("(4000)", -91.125, 185, 6),
                ("D29-4300", -91.125, 214, 6),
                ("(3500)", -116.125, 135, 6),
                ("D29-3800", -116.125, 165, 6),
                ("(4000)", -91.125, 85, 6),
                ("D29-4300", -91.125, 115, 6),
                ("D29-3050", -153.625, 65, 6),
                ("(2850)", -150, 35, 6),
                ("(1800)", -184, -67, 6),
                ("D29-2100", -186, -32, 6),
                ("(1300)", -209.625, -117, 6),
                ("D29-1600", -209.625, -84, 6),
                ("(1800)", -186, -171, 6),
                ("D29-2100", -184, -134, 6),
                ("4",-140, 305,7),
                ("4", 65, 305, 7),
                ("4", 270, 305, 7),
                ("600ｘ900", 35, 270, 7),
                ("上筋",-456, 230, 7),
                ("下筋", -456, -20, 7),
                ("にげ　130", -456, 180, 8),
                ("にげ　130", -456, -70, 8),

                ("D29-6800", 380, 212.350, 6),
                ("D29-6800", 360, 162.350, 6),
                ("D29-6800", 380, 112.350, 6),
                ("D29-5800", 340, 62.350, 6),
                ("D29-6800", 280, -38, 6),
                ("D29-6800", 250, -87.650, 6),
                ("D29-6800", 280, -137.650, 6),
            };

            foreach (var text in texts)
            {
                SolidColorBrush brush = GetBrushForLayer(text.text, text.colorIndex);
                DrawTextKha23(text.text, text.x, text.y, brush, scaleFactor, offsetX, offsetY, textthickness);
            }

            // ELLIPSEエンティティの描画
            var ellipses = new List<(double centerX, double centerY, double majorRadius, double minorRadius)>
            {
                // DXFデータから抽出したすべてのELLIPSEエンティティ
                (108.875, 210.350, 3, 3),
                (58.875, 160.350, 3, 3),
                (108.875, 110.350, 3, 3),
                (-111.625, -39.650, 3, 3),
                (-161.625, -89.650, 3, 3),
                (-111.625, -139.650, 3, 3),
            };

            foreach (var ellipse in ellipses)
            {
                DrawEllipseKha23(ellipse.centerX, ellipse.centerY, ellipse.majorRadius, ellipse.minorRadius, steelBrush, scaleFactor, offsetX, offsetY, true);
            }
        }

        private Line DrawLineKha23(double x1, double y1, double x2, double y2, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            Line line = new Line
            {
                Stroke = brush,
                StrokeThickness = thickness
            };

            Point start = ScalePoint(x1, y1, scaleFactor, offsetX, offsetY);
            Point end = ScalePoint(x2, y2, scaleFactor, offsetX, offsetY);

            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;

            MainCanvasKha23.Children.Add(line);
            return line;
        }

        private void DrawTextKha23(string text, double x, double y, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double textthickness)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 20 * scaleFactor, // フォントサイズをスケールに合わせて調整
                //FontFamily = new FontFamily("MS Gothic") // 日本語フォントをサポート
            };

            Point position = ScalePoint(x, y, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(textBlock, position.X);
            Canvas.SetTop(textBlock, position.Y - textBlock.FontSize); // テキストを上に調整

            MainCanvasKha23.Children.Add(textBlock);
        }

        private void DrawEllipseKha23(double centerX, double centerY, double majorRadius, double minorRadius, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, bool isFilled)
        {
            Ellipse ellipse = new Ellipse
            {
                Stroke = brush,
                StrokeThickness = 1,
                Fill = isFilled ? brush : null,
                Width = majorRadius * 3 * scaleFactor,
                Height = minorRadius * 3 * scaleFactor
            };

            Point center = ScalePoint(centerX, centerY, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(ellipse, center.X - ellipse.Width / 2);
            Canvas.SetTop(ellipse, center.Y - ellipse.Height / 2);

            MainCanvasKha23.Children.Add(ellipse);
        }

        //#### Kết thúc vẽ hình 施工図の設定6 ####

        //#### Bắt đầu vẽ hình 施工図の設定5 ####
        private void DrawBeamDiagramKha24()
        {
            if (MainCanvasKha24 == null) return;

            MainCanvasKha24.Children.Clear();

            SolidColorBrush concreteBrush = new SolidColorBrush(Colors.LightGray);
            SolidColorBrush steelBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush concreteLineBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush textBrush = new SolidColorBrush(Colors.Black);
            SolidColorBrush text300 = new SolidColorBrush(Colors.Blue);
            SolidColorBrush lightgreen = new SolidColorBrush(Colors.LightSkyBlue);
            SolidColorBrush purple = new SolidColorBrush(Colors.HotPink);
            SolidColorBrush redBrush = new SolidColorBrush(Colors.Red); // 色番号1用
            SolidColorBrush blueBrush = new SolidColorBrush(Colors.Blue); // 色番号5用

            double scaleFactor = 0.5; // スケールファクター（座標範囲が広いため小さめに設定）
            double offsetX = -150; // オフセット（図面をキャンバスの中央に配置）
            double offsetY = 80; // y座標が負の領域に広がるため、十分なオフセットを設定
            double steelLineThickness = 1;
            double textthickness = 2;

            // DXFデータをシミュレート（実際のアプリケーションではDXFファイルをパース）
            DrawEntitiesKha24(scaleFactor, offsetX, offsetY, concreteLineBrush, steelBrush, textBrush, text300, lightgreen, purple, redBrush, blueBrush, steelLineThickness, textthickness);
            // Vẽ thanh thép cong 
            PathFigure arcFigure1 = new PathFigure { StartPoint = ScalePoint(534.481, 235.052, scaleFactor, offsetX, offsetY) };
            arcFigure1.Segments.Add(new ArcSegment(ScalePoint(541.481, 235.052, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc1 = new PathGeometry();
            arc1.Figures.Add(arcFigure1);
            Path arcPath1 = new Path { Data = arc1, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure2 = new PathFigure { StartPoint = ScalePoint(541.481, 142.052, scaleFactor, offsetX, offsetY) };
            arcFigure2.Segments.Add(new ArcSegment(ScalePoint(534.481, 142.052, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc2 = new PathGeometry();
            arc2.Figures.Add(arcFigure2);
            Path arcPath2 = new Path { Data = arc2, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure3 = new PathFigure { StartPoint = ScalePoint(787.981, 235.052, scaleFactor, offsetX, offsetY) };
            arcFigure3.Segments.Add(new ArcSegment(ScalePoint(794.981, 235.052, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc3 = new PathGeometry();
            arc3.Figures.Add(arcFigure3);
            Path arcPath3 = new Path { Data = arc3, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure4 = new PathFigure { StartPoint = ScalePoint(794.981, 142.052, scaleFactor, offsetX, offsetY) };
            arcFigure4.Segments.Add(new ArcSegment(ScalePoint(787.981, 142.052, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc4 = new PathGeometry();
            arc4.Figures.Add(arcFigure4);
            Path arcPath4 = new Path { Data = arc4, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure5 = new PathFigure { StartPoint = ScalePoint(534.481, -56.948, scaleFactor, offsetX, offsetY) };
            arcFigure5.Segments.Add(new ArcSegment(ScalePoint(541.481, -56.948, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc5 = new PathGeometry();
            arc5.Figures.Add(arcFigure5);
            Path arcPath5 = new Path { Data = arc5, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure6 = new PathFigure { StartPoint = ScalePoint(541.481, -149.948, scaleFactor, offsetX, offsetY) };
            arcFigure6.Segments.Add(new ArcSegment(ScalePoint(534.481, -149.948, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc6 = new PathGeometry();
            arc6.Figures.Add(arcFigure6);
            Path arcPath6 = new Path { Data = arc6, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure7 = new PathFigure { StartPoint = ScalePoint(661.231, -56.948, scaleFactor, offsetX, offsetY) };
            arcFigure7.Segments.Add(new ArcSegment(ScalePoint(668.231, -56.948, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc7 = new PathGeometry();
            arc7.Figures.Add(arcFigure7);
            Path arcPath7 = new Path { Data = arc7, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure8 = new PathFigure { StartPoint = ScalePoint(668.231, -149.948, scaleFactor, offsetX, offsetY) };
            arcFigure8.Segments.Add(new ArcSegment(ScalePoint(661.231, -149.948, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc8 = new PathGeometry();
            arc8.Figures.Add(arcFigure8);
            Path arcPath8 = new Path { Data = arc8, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure9 = new PathFigure { StartPoint = ScalePoint(787.981, -56.948, scaleFactor, offsetX, offsetY) };
            arcFigure9.Segments.Add(new ArcSegment(ScalePoint(794.981, -56.948, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc9 = new PathGeometry();
            arc9.Figures.Add(arcFigure9);
            Path arcPath9 = new Path { Data = arc9, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure10 = new PathFigure { StartPoint = ScalePoint(794.981, -149.948, scaleFactor, offsetX, offsetY) };
            arcFigure10.Segments.Add(new ArcSegment(ScalePoint(787.981, -149.948, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc10 = new PathGeometry();
            arc10.Figures.Add(arcFigure10);
            Path arcPath10 = new Path { Data = arc10, Stroke = steelBrush, StrokeThickness = steelLineThickness };
            //có sự thay đổi arc
            PathFigure arcFigure11 = new PathFigure { StartPoint = ScalePoint(534.481, 85, scaleFactor, offsetX, offsetY) };
            arcFigure11.Segments.Add(new ArcSegment(ScalePoint(541.481, 85, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc11 = new PathGeometry();
            arc11.Figures.Add(arcFigure11);
            Path arcPath11 = new Path { Data = arc11, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure12 = new PathFigure { StartPoint = ScalePoint(541.481, -8, scaleFactor, offsetX, offsetY) };
            arcFigure12.Segments.Add(new ArcSegment(ScalePoint(534.481, -8, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc12 = new PathGeometry();
            arc12.Figures.Add(arcFigure12);
            Path arcPath12 = new Path { Data = arc12, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure13 = new PathFigure { StartPoint = ScalePoint(787.981, 85, scaleFactor, offsetX, offsetY) };
            arcFigure13.Segments.Add(new ArcSegment(ScalePoint(794.981, 85, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc13 = new PathGeometry();
            arc13.Figures.Add(arcFigure13);
            Path arcPath13 = new Path { Data = arc13, Stroke = steelBrush, StrokeThickness = steelLineThickness };

            PathFigure arcFigure14 = new PathFigure { StartPoint = ScalePoint(794.981, -8, scaleFactor, offsetX, offsetY) };
            arcFigure14.Segments.Add(new ArcSegment(ScalePoint(787.981, -8, scaleFactor, offsetX, offsetY), new Size(5 * scaleFactor, 5 * scaleFactor), 0, false, SweepDirection.Clockwise, true));
            PathGeometry arc14 = new PathGeometry();
            arc14.Figures.Add(arcFigure14);
            Path arcPath14 = new Path { Data = arc14, Stroke = steelBrush, StrokeThickness = steelLineThickness };
            if (nakago2on1.IsChecked == true)
            {
                MainCanvasKha24.Children.Add(arcPath1);
                MainCanvasKha24.Children.Add(arcPath2);
                MainCanvasKha24.Children.Add(arcPath3);
                MainCanvasKha24.Children.Add(arcPath4);
                MainCanvasKha24.Children.Add(arcPath5);
                MainCanvasKha24.Children.Add(arcPath6);
                MainCanvasKha24.Children.Add(arcPath7);
                MainCanvasKha24.Children.Add(arcPath8);
                MainCanvasKha24.Children.Add(arcPath9);
                MainCanvasKha24.Children.Add(arcPath10);
            }
            //Có sự thay đổi arc
            else if (nakago2on1.IsChecked != true)
            {
                MainCanvasKha24.Children.Add(arcPath11);
                MainCanvasKha24.Children.Add(arcPath12);
                MainCanvasKha24.Children.Add(arcPath13);
                MainCanvasKha24.Children.Add(arcPath14);
            }


        }

        private void DrawEntitiesKha24(double scaleFactor, double offsetX, double offsetY, SolidColorBrush concreteLineBrush,
                                    SolidColorBrush steelBrush, SolidColorBrush textBrush, SolidColorBrush text300,
                                    SolidColorBrush lightgreen, SolidColorBrush purple, SolidColorBrush redBrush, SolidColorBrush blueBrush, double steelLineThickness, double textthickness)
        {
            // LINEエンティティの描画（色番号62に基づいて色を設定）
            if (nakago2on1.IsChecked == true)
            {
                DrawLineKha24(416.231, 260, 416.231, -261.647, redBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Đỏ trái dài
                DrawLineKha24(914.231, 260, 914.231, -261.647, redBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Đỏ phải dài
                DrawLineKha24(386.231, 260, 386.231, -261.647, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Đen 1 dài (tính từ trái qua)
                DrawLineKha24(884.231, 260, 884.231, -261.647, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Đen 3 dài 
                DrawLineKha24(445.231, 260, 445.231, -261.647, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Đen 2 dài
                DrawLineKha24(943.231, 260, 943.231, -261.647, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // đen 4 dài
                DrawLineKha24(634.731, 138.552, 694.731, 138.552, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Chữ nhật dưới
                DrawLineKha24(694.731, 138.552, 694.731, 238.552, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Chữ nhật phải
                DrawLineKha24(634.731, 238.552, 634.731, 138.552, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Chữ nhật trái
                DrawLineKha24(641.731, 238.552, 641.731, 225.552, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc trái hình chữ nhật
                DrawLineKha24(687.731, 238.552, 687.731, 225.552, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc phải hình chữ nhật1
                DrawLineKha24(688.201, 227.773, 694.731, 234.303, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc phải hình chữ nhật2
                DrawLineKha24(694.731, 238.552, 634.731, 238.552, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Chữ nhật trên
                DrawLineKha24(661.231, -56.948, 661.231, -149.948, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thanh 4

                DrawLineKha24(534.481, 235.052, 534.481, 142.052, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thanh 1
                DrawLineKha24(541.481, 235.052, 541.481, 225.552, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); //  Móc trên thanh 1
                DrawLineKha24(541.481, 142.052, 541.481, 151.552, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc dưới thanh 1

                DrawLineKha24(787.981, 235.052, 787.981, 142.052, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thanh 2
                DrawLineKha24(534.481, -56.948, 534.481, -149.948, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thanh 3
                DrawLineKha24(787.981, -56.948, 787.981, -149.948, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thanh 5
                DrawLineKha24(668.231, -149.948, 668.231, -140.448, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc dưới thanh 4

                DrawLineKha24(794.981, 142.052, 794.981, 151.552, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc dưới thanh 2
                DrawLineKha24(541.481, -149.948, 541.481, -140.448, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc dưới thanh 3
                DrawLineKha24(794.981, -149.948, 794.981, -140.448, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); //Móc dưới thanh 5
                DrawLineKha24(668.231, -56.948, 668.231, -66.448, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc trên thanh 4

                DrawLineKha24(794.981, 235.052, 794.981, 225.552, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); //  Móc trên thanh 2
                DrawLineKha24(541.481, -56.948, 541.481, -66.448, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); //  Móc trên thanh 3
                DrawLineKha24(794.981, -56.948, 794.981, -66.448, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc trên thanh 5

                // TEXTエンティティの描画
                DrawTextKha24("中子2", 283.613, -115, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D3
                DrawTextKha24("中子1", 283.613, 175, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D4

                DrawTextKha24("820", 520, 110, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D3
                DrawTextKha24("820", 773, 110, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D3
                DrawTextKha24("820", 520, -182, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D3
                DrawTextKha24("820", 650, -182, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D3
                DrawTextKha24("820", 773, -182, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D3
                DrawTextKha24("(P56)", 510, 70, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D4
                DrawTextKha24("(P56)", 640, 70, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D4
                DrawTextKha24("(P56)", 766, 70, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D4
                DrawTextKha24("(P56)", 510, -222, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D4
                DrawTextKha24("(P56)", 640, -222, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D4
                DrawTextKha24("(P56)", 766, -222, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D4
                DrawTextKha24("D13@100", 500, 90, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A
                DrawTextKha24("D13@100", 630, 90, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A
                DrawTextKha24("D13@100", 753, 90, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A
                DrawTextKha24("D13@100", 500, -202, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A
                DrawTextKha24("D13@100", 630, -202, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A
                DrawTextKha24("D13@100", 753, -202, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A
                DrawTextKha24("520x820", 630, 110, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A
            }

            if (nakago2on1.IsChecked != true)
            {
                //Có sự thay đổi line
                DrawLineKha24(416.231, 110, 416.231, -110, redBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Đỏ trái dài
                DrawLineKha24(914.231, 110, 914.231, -110, redBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Đỏ phải dài
                DrawLineKha24(386.231, 110, 386.231, -110, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Đen 1 dài (tính từ trái qua)
                DrawLineKha24(884.231, 110, 884.231, -110, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Đen 3 dài 
                DrawLineKha24(445.231, 110, 445.231, -110, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Đen 2 dài
                DrawLineKha24(943.231, 110, 943.231, -110, concreteLineBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // đen 4 dài

                DrawLineKha24(634.731, -12, 694.731, -12, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Chữ nhật dưới
                DrawLineKha24(694.731, -12, 694.731, 88, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Chữ nhật phải
                DrawLineKha24(634.731, 88, 634.731, -12, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Chữ nhật trái
                DrawLineKha24(641.731, 88, 641.731, 75, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc trái hình chữ nhật
                DrawLineKha24(687.731, 88, 687.731, 75, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc phải hình chữ nhật1
                DrawLineKha24(688.201, 77, 694.731, 84, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc phải hình chữ nhật2
                DrawLineKha24(694.731, 88, 634.731, 88, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Chữ nhật trên
                DrawLineKha24(534.481, 85, 534.481, -8, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thanh 1
                DrawLineKha24(541.481, 85, 541.481, 75, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); //  Móc trên thanh 1
                DrawLineKha24(541.481, -8, 541.481, 1, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc dưới thanh 1
                DrawLineKha24(787.981, 85, 787.981, -8, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Thanh 2
                DrawLineKha24(794.981, -8, 794.981, 1, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); // Móc dưới thanh 2
                DrawLineKha24(794.981, 85, 794.981, 75, blueBrush, scaleFactor, offsetX, offsetY, steelLineThickness); //  Móc trên thanh 2

                //Có sự thay đổi Text
                DrawTextKha24("中子", 283.613, 10, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A
                DrawTextKha24("820", 520, -40, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D3
                DrawTextKha24("820", 773, -40, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D3
                DrawTextKha24("(P56)", 510, -80, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D4
                DrawTextKha24("(P56)", 640, -80, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D4
                DrawTextKha24("(P56)", 766, -80, textBrush, scaleFactor, offsetX, offsetY, textthickness); // D4
                //DrawTextKha24("D13@100", 500, -60, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A
                DrawTextKha24("D13@100", 630, -60, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A
                //DrawTextKha24("D13@100", 753, -60, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A
                DrawTextKha24("520x820", 630, -40, textBrush, scaleFactor, offsetX, offsetY, textthickness); // 17A

            }



        }

        private Line DrawLineKha24(double x1, double y1, double x2, double y2, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double thickness)
        {
            Line line = new Line
            {
                Stroke = brush,
                StrokeThickness = thickness
            };

            Point start = ScalePoint(x1, y1, scaleFactor, offsetX, offsetY);
            Point end = ScalePoint(x2, y2, scaleFactor, offsetX, offsetY);

            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;

            MainCanvasKha24.Children.Add(line);
            return line;
        }

        private void DrawTextKha24(string text, double x, double y, SolidColorBrush brush, double scaleFactor, double offsetX, double offsetY, double textthickness)
        {
            TextBlock textBlock = new TextBlock
            {
                Text = text,
                Foreground = brush,
                FontSize = 20 * scaleFactor, // フォントサイズをスケールに合わせて調整
                //FontFamily = new FontFamily("MS Gothic") // 日本語フォントをサポート
            };

            Point position = ScalePoint(x, y, scaleFactor, offsetX, offsetY);
            Canvas.SetLeft(textBlock, position.X);
            Canvas.SetTop(textBlock, position.Y - textBlock.FontSize); // テキストを上に調整

            MainCanvasKha24.Children.Add(textBlock);
        }
        //#### Kết thúc vẽ hình 施工図の設定5 ####

        //###################### Kết thúc trang 3 計算値設定　####################################################

    }
}
