
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;   // [NEW] for click/edit
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Linq;
using Autodesk.Revit.DB;
using Microsoft.Win32;
using static System.Net.Mime.MediaTypeNames;
using Application = System.Windows.Application;
using Color = System.Windows.Media.Color;
using Control = System.Windows.Controls.Control;
using Ellipse = System.Windows.Shapes.Ellipse;
using FormattedText = System.Windows.Media.FormattedText;
using Grid = System.Windows.Controls.Grid;
using Image = System.Windows.Controls.Image;
using Line = System.Windows.Shapes.Line;
using LineSegment = System.Windows.Media.LineSegment;
using MediaColor = System.Windows.Media.Color;
using Panel = System.Windows.Controls.Panel;
using Path = System.Windows.Shapes.Path;
using Point = System.Windows.Point;
using Rectangle = System.Windows.Shapes.Rectangle;

// [KEEP] tránh va chạm với System.IO.Path
using WpfPath = System.Windows.Shapes.Path;

namespace RevitProjectDataAddin
{
    public partial class 梁配筋施工図 : Window
    {
        // ===== Config =====
        const double CanvasPadding = 0.70;     // khung vẽ chiếm 70% Canvas
        const double AxisStrokeThickness = 1.5;
        const double LineHitPx = 18; // kích thước vùng click theo pixel (tùy bạn 14–24)          

        // ===== Offsets cho các nhóm hình vẽ tuỳ chỉnh =====
        // Có thể chỉnh các giá trị này (đơn vị mm) để tịnh tiến nhóm phần tử tương ứng
        // theo hướng X (ngang) và Y (đứng).
        private static (double X, double Y) OffsetAbdominalChainY { get; set; } = (0, 10000);      // Offset cho ////////////////1 chổ////////
        private static (double X, double Y) OffsetAbdominalChainX { get; set; } = (0, 10000);      // Offset cho ///////////////// 2 chổ /////////////////
        private static (double X, double Y) OffsetTanbuText { get; set; } = (0, 10000);            // Offset cho ///////////////// 3 chổ ///////////////////////
        private static (double X, double Y) OffsetCentralStirrupFrame { get; set; } = (0, 10000);  // Offset cho /////////////// 4 chổ /////////////////////
        private static (double X, double Y) OffsetLegendColumn { get; set; } = (0, 10000);         // Offset cho ///////////// 5 chổ //////////////
        private static readonly string[] _standardRebarDiameters = { "10", "13", "16", "19", "22", "25", "29", "32", "35", "38" };
        private static readonly string[] _standardRebarDiameters1 = { "10", "13", "16", "19", "22", "25", "29", "32", "35", "38" };
        private const double UiMarkerDotRadiusMm = 20.0; // marker dot diameter in mm
        private const double UiTopTextOffsetY = 0.0;
        private const double UiBottomTextOffsetY = 0.0;
        private const double DxfTopTextOffsetY = 0;
        private const double DxfBottomTextOffsetY = 50;
        private const double PdfTopTextOffsetY = 30;
        private const double PdfBottomTextOffsetY = 30.0;
        private const double DefaultPdfInnerFrameOffsetMm = 5.0;
        private static readonly Brush PopupMenuBorderBrush = CreateFrozenBrush(166, 174, 184);
        private static readonly Brush PopupMenuDividerBrush = CreateFrozenBrush(218, 223, 229);
        private static readonly Brush PopupMenuEditorBorderBrush = CreateFrozenBrush(180, 187, 196);
        private static readonly Thickness PopupMenuBorderThickness = new Thickness(1.0);

        // DIM hover/base brushes (class scope to avoid missing-variable compile issues)
        private readonly Brush dimBaseFg = Brushes.Black;
        private readonly Brush dimHoverFg = Brushes.Blue;
        private bool _useApproximateCanvasTextLayout = false;


        // ===== Anchor enums cho text =====
        enum HAnchor { Left, Center, Right }
        enum VAnchor { Top, Middle, Bottom }
        enum TextOutputTarget { Ui, Dxf, Pdf }
        enum PdfFitMode { Width, Height }
        const TextOutputTarget PlotRenderTextTarget = TextOutputTarget.Ui;
        const double SharedMmToPx = 96.0 / 25.4;
        const double SharedPxToMm = 25.4 / 96.0;

        // Change the accessibility of WCTransform from private to public
        public struct WCTransform
        {
            public double Ox, Oy, Scale;
            public double FontScale;
            public bool YDown; // true = Canvas Y đi xuống

            public WCTransform(double ox, double oy, double scale, bool yDown = true, double fontScale = 1.0)
            { Ox = ox; Oy = oy; Scale = scale; YDown = yDown; FontScale = fontScale <= 0 ? 1.0 : fontScale; }

            public Point P(double wx, double wy)
                => new Point(Ox + wx * Scale, YDown ? Oy + wy * Scale : Oy - wy * Scale);

            public double S(double lenMm) => lenMm * Scale;
        }

        private static MediaColor ColorFromBrush(Brush brush, MediaColor fallback)
        {
            if (brush is SolidColorBrush solid)
            {
                return solid.Color;
            }
            return fallback;
        }

        private static Brush CreateFrozenBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }

        private static bool Near(double a, double b, double eps = 0.5)
            => Math.Abs(a - b) <= eps;

        private static bool IsDescendantOf(DependencyObject source, DependencyObject ancestor)
        {
            while (source != null)
            {
                if (ReferenceEquals(source, ancestor))
                    return true;

                source = VisualTreeHelper.GetParent(source);
            }

            return false;
        }

        // ===== Helpers: primitives theo tọa độ world (mm) =====
        static Line DrawLineW(Canvas c, WCTransform t,
            double x1, double y1, double x2, double y2,
            Brush stroke = null, double thickness = 1, DoubleCollection dash = null)
        {
            var p1 = t.P(x1, y1);
            var p2 = t.P(x2, y2);
            var line = new Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = stroke ?? Brushes.Black,
                StrokeThickness = thickness
            };
            if (dash != null) line.StrokeDashArray = dash;
            c.Children.Add(line);
            return line;
        }

        static Polyline DrawPolylineW(Canvas c, WCTransform t,
            IEnumerable<Point> worldPts,
            Brush stroke = null, double thickness = 1, Brush fill = null)
        {
            var pl = new Polyline
            {
                Stroke = stroke ?? Brushes.Black,
                StrokeThickness = thickness,
                Fill = fill
            };
            foreach (var wp in worldPts) pl.Points.Add(t.P(wp.X, wp.Y));
            c.Children.Add(pl);
            return pl;
        }



        // Vẽ cung tròn (open arc) và thêm DxfArc vào scene để xuất DXF
        public void DrawArc_Rec(
            Canvas canvas, WCTransform T, GridBotsecozu owner,
            double cx, double cy, double rMm,
            double startDeg, double endDeg,
            Brush stroke, double thickness = 1.2,
            DoubleCollection dash = null, string layer = "MARK")
        {
            if (rMm <= 0) return;

            // --- (A) VẼ WPF ---
            if (canvas != null && !IsLightweightCanvasUi(owner))
            {
                // helper: chuyển mm (world) -> px (canvas)
                double s = T.Scale;                             // giả sử WCTransform có Scale
                bool yDown = true;                              // nếu T có cờ YDown thì dùng T.YDown
                Func<double, double, Point> toCanvas = (wx, wy) =>
                {
                    double x = T.Ox + wx * s;                   // giả sử WCTransform có Ox/Oy
                    double y = T.Oy + (yDown ? wy * s : -wy * s);
                    return new Point(x, y);
                };

                // điểm đầu/cuối của cung
                double sRad = startDeg * Math.PI / 180.0;
                double eRad = endDeg * Math.PI / 180.0;

                Point p0 = toCanvas(cx + rMm * Math.Cos(sRad), cy + rMm * Math.Sin(sRad));
                Point p1 = toCanvas(cx + rMm * Math.Cos(eRad), cy + rMm * Math.Sin(eRad));

                // kích thước cung trong pixel
                double rPx = Math.Abs(rMm * s);
                // sweep CCW theo hệ toạ độ world; với yDown thì trên màn hình sẽ là Clockwise
                var sweepDir = yDown ? SweepDirection.Clockwise : SweepDirection.Counterclockwise;

                // góc quét CCW (0..360)
                double sweepCCW = NormalizeDeltaCCW(startDeg, endDeg);
                bool isLarge = sweepCCW > 180.0 - 1e-6;

                var fig = new PathFigure { StartPoint = p0, IsClosed = false, IsFilled = false };
                fig.Segments.Add(new ArcSegment
                {
                    Point = p1,
                    Size = new Size(rPx, rPx),
                    RotationAngle = 0,
                    IsLargeArc = isLarge,
                    SweepDirection = sweepDir
                });

                var path = new Path
                {
                    Stroke = stroke ?? Brushes.Black,
                    StrokeThickness = thickness,
                    SnapsToDevicePixels = true,
                    Data = new PathGeometry(new[] { fig })
                };
                if (dash != null) path.StrokeDashArray = dash;
                canvas.Children.Add(path);
            }

            // --- (B) THÊM ENTITY DXF ---
            var (sDeg, eDeg, isCircle) = NormalizeArcForDxf(startDeg, endDeg);
            var strokeColor = ColorFromBrush(stroke ?? Brushes.Black, Colors.Black);
            double[] dashArray = dash != null && dash.Count > 0 ? dash.Select(d => (double)d).ToArray() : null;
            if (isCircle)
            {
                // nếu start==end -> coi như full circle (DXF ARC không biểu diễn full vòng)
                SceneFor(owner).Add(new DxfCircle(cx, cy, rMm, layer,
                                                  filled: false,
                                                  strokeColor: strokeColor,
                                                  strokeThicknessPx: thickness,
                                                  dash: dashArray));
            }
            else
            {
                SceneFor(owner).Add(new DxfArc(cx, cy, rMm, sDeg, eDeg, layer, strokeColor, thickness, dashArray));
            }
        }
        public sealed class DxfArc /* : IDxfEntity nếu bạn có interface chung */
        {
            public double X { get; }
            public double Y { get; }
            public double R { get; }
            public double StartDeg { get; }
            public double EndDeg { get; }
            public string Layer { get; }
            public MediaColor StrokeColor { get; }
            public double ThicknessPx { get; }
            public double[] Dash { get; }

            public DxfArc(double x, double y, double r, double startDeg, double endDeg,
                          string layer = "0", MediaColor? strokeColor = null,
                          double thicknessPx = 1.0, double[] dash = null)
            {
                X = x; Y = y; R = r;
                StartDeg = startDeg; EndDeg = endDeg;
                Layer = layer ?? "0";
                StrokeColor = strokeColor ?? Colors.Black;
                ThicknessPx = thicknessPx;
                Dash = dash;
            }
        }


        // Chuẩn hoá hiệu góc CCW (0..360)
        private static double NormalizeDeltaCCW(double startDeg, double endDeg)
        {
            double a = startDeg % 360.0; if (a < 0) a += 360.0;
            double b = endDeg % 360.0; if (b < 0) b += 360.0;
            double d = b - a; if (d < 0) d += 360.0;
            return d;
        }

        // Chuẩn hoá cặp góc cho DXF; trả về (start, end, isFullCircle)
        private static (double s, double e, bool isCircle) NormalizeArcForDxf(double startDeg, double endDeg)
        {
            double s = startDeg % 360.0; if (s < 0) s += 360.0;
            double e = endDeg % 360.0; if (e < 0) e += 360.0;
            double d = e - s; if (d <= 0) d += 360.0;
            bool full = d < 1e-6 || Math.Abs(d - 360.0) < 1e-6;
            return (s, e, full);
        }


        // Chấm tròn kích thước theo pixel (không scale theo mm)
        static Ellipse DrawDotPx(Canvas c, WCTransform t,
            double wx, double wy, double sizePx = 4,
            Brush fill = null, Brush stroke = null, double strokePx = 0.8)
        {
            var p = t.P(wx, wy);
            var el = new Ellipse
            {
                Width = sizePx,
                Height = sizePx,
                Fill = fill ?? Brushes.Black,
                Stroke = stroke,
                StrokeThickness = strokePx
            };
            Canvas.SetLeft(el, p.X - sizePx / 2.0);
            Canvas.SetTop(el, p.Y - sizePx / 2.0);
            c.Children.Add(el);
            return el;
        }

        TextBlock DrawTextW(Canvas c, WCTransform t,
            string text, double wx, double wy,
            double fontPx = 12, Brush color = null,
            HAnchor ha = HAnchor.Center, VAnchor va = VAnchor.Middle,
            bool forcePreciseLayout = false)
        {
            double effectiveFont = fontPx;

            var tb = new TextBlock { Text = text ?? "", FontSize = effectiveFont, Foreground = color ?? Brushes.Black };
            Size sz;
            if (_useApproximateCanvasTextLayout && !forcePreciseLayout)
            {
                double charCount = Math.Max(1, (text ?? string.Empty).Length);
                sz = new Size(
                    Math.Max(effectiveFont * 0.8, charCount * effectiveFont * 0.62),
                    Math.Max(effectiveFont, effectiveFont * 1.35));
            }
            else
            {
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                sz = tb.DesiredSize;
            }

            var p = t.P(wx, wy + GetTextOffsetY(va, TextOutputTarget.Ui));
            double left = p.X, top = p.Y;

            switch (ha)
            {
                case HAnchor.Center: left -= sz.Width / 2.0; break;
                case HAnchor.Right: left -= sz.Width; break;
            }
            switch (va)
            {
                case VAnchor.Middle: top -= sz.Height / 2.0; break;
                case VAnchor.Bottom: top -= sz.Height; break;
            }

            Canvas.SetLeft(tb, left);
            Canvas.SetTop(tb, top);
            c.Children.Add(tb);
            return tb;
        }
        // ===== [SCENE] Wrappers: vẽ ra Canvas đồng thời ghi vào Scene =====
        private Line DrawLine_Rec(Canvas c, WCTransform T, GridBotsecozu owner,
                                  double x1, double y1, double x2, double y2,
                                  Brush stroke = null, double thickness = 1,
                                  DoubleCollection dash = null, string layer = "LINE")
        {
            var strokeColor = ColorFromBrush(stroke ?? Brushes.Black, Colors.Black);
            SceneFor(owner).Add(new SceneLine(x1, y1, x2, y2, layer, thickness, dash, strokeColor));
            if (IsLightweightCanvasUi(owner))
                return null;
            return DrawLineW(c, T, x1, y1, x2, y2, stroke, thickness, dash);
        }

        private TextBlock DrawText_Rec(Canvas c, WCTransform T, GridBotsecozu owner,
                                       string text, double wx, double wy,
                                       double fontPx = 12, Brush color = null,
                                       HAnchor ha = HAnchor.Center, VAnchor va = VAnchor.Bottom,
                                       double heightMm = 200, string layer = "TEXT",
                                       bool forceCanvasVisual = false)
        {
            var (h, v) = ToDxfAlign(ha, va);
            double effectiveHeightMm = heightMm;
            double effectiveFontPx = Math.Max(1.0, effectiveHeightMm * T.Scale);

            var textColor = ColorFromBrush(color ?? Brushes.Black, Colors.Black);
            string fontFamily = this.FontFamily?.Source ?? "Yu Mincho";
            // Scene keeps the source coordinates; exporter-specific offsets are applied later.
            SceneFor(owner).Add(new DxfText(text ?? "", wx, wy, effectiveHeightMm, hAlign: h, vAlign: v, rotDeg: 0,
                                             layer: layer, style: "STANDARD", fontPx: effectiveFontPx,
                                             fontFamily: fontFamily, color: textColor, hAnchor: ha, vAnchor: va,
                                             skipCanvasBitmap: forceCanvasVisual));
            if (IsLightweightCanvasUi(owner) && !forceCanvasVisual)
            {
                return new TextBlock
                {
                    Text = text ?? string.Empty,
                    FontSize = effectiveFontPx,
                    Foreground = color ?? Brushes.Black
                };
            }
            return DrawTextW(c, T, text, wx, wy, effectiveFontPx, color, ha, va, forceCanvasVisual);
        }

        private double ResolveTextCombinedScale(WCTransform T, GridBotsecozu owner)
        {
            double fontScale = T.FontScale > 0 ? T.FontScale : 1.0;
            double axisScale = 1.0;
            double zoomScale = 1.0;

            if (Math.Abs(fontScale - 1.0) < 1e-6)
            {
                var k = _projectData?.Kihon;
                if (k != null && _currentSecoList != null)
                {
                    bool tsuIsX = k.NameX.Any(n => n.Name == _currentSecoList.通を選択);
                    bool tsuIsY = k.NameY.Any(n => n.Name == _currentSecoList.通を選択);

                    var names = tsuIsY ? k.NameX.Select(n => n.Name).ToList()
                                       : tsuIsX ? k.NameY.Select(n => n.Name).ToList()
                                                : new List<string>();

                    int axisCount = Math.Max(names.Count, 1);
                    const double baseDimFont = 10.0;
                    double dimFont = Math.Max(6.0, baseDimFont - 0.25 * Math.Max(0, axisCount - 2));
                    axisScale = dimFont / baseDimFont;
                }

                if (_viewByItem.TryGetValue(owner, out var vs))
                {
                    zoomScale = Clamp(vs.Zoom, 0.05, 20.0);
                }
            }

            return Math.Abs(fontScale - 1.0) < 1e-6
                ? Clamp(fontScale * axisScale * zoomScale, 0.2, 5.0)
                : Clamp(fontScale, 0.2, 5.0);
        }
        // Chấm tròn kích thước theo mm: DXF = CIRCLE (viền) + SOLID-fan (đặc), UI = Ellipse fill
        private Ellipse DrawDotMm_Rec(Canvas c, WCTransform T, GridBotsecozu owner,
                                       double wx, double wy, double rMm = 60,
                                       Brush fill = null, Brush stroke = null, double strokePx = 0.8,
                                       string layer = "MARK", int solidSegments = 32)
        {
            double effectiveRadiusMm = UiMarkerDotRadiusMm;
            // (1) DXF: viền tròn (tuỳ chọn) + phần đặc bằng SOLID tam giác
            var strokeColor = ColorFromBrush(stroke ?? Brushes.Black, Colors.Black);
            var fillColor = ColorFromBrush(fill ?? Brushes.Black, Colors.Black);
            SceneFor(owner).Add(new DxfCircle(wx, wy, effectiveRadiusMm, layer, filled: true,
                                              strokeColor: strokeColor, fillColor: fillColor,
                                              strokeThicknessPx: strokePx));        // viền (giữ để nhìn rõ trên CAD)
            //AddCircleSolidFan(owner, wx, wy, rMm, solidSegments, layer);   // phần đặc ruột

            // (2) UI: Ellipse tròn có fill
            if (IsLightweightCanvasUi(owner))
                return null;
            double sizePx = Math.Max(3.0, 3.0 * effectiveRadiusMm * T.Scale);
            return DrawDotPx(c, T, wx, wy, sizePx, Brushes.Black, Brushes.Black, strokePx);
        }



        // ===== Parse số mm =====
        static double ParseMm(string s)
            => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

        // [NEW] ===== đổi chuỗi/number thành double an toàn
        static double AsDouble(object v)
        {
            if (v == null) return 0;
            if (v is double d) return d;
            if (v is int i) return i;
            var s = v.ToString();
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) return x;
            if (double.TryParse(s, out x)) return x;
            return 0;
        }

        static double ParseDoubleInvariant(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return 0;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
            if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v)) return v;
            if (double.TryParse(s, out v)) return v;
            return 0;
        }


        static (double top, double bottom, double left, double right) GetCentralProtectiveCovers(Z梁の配置 cfg)
        {
            double top = 0, bottom = 0, left = 0, right = 0;

            var dict = cfg?.gridbotdata;
            if (dict != null)
            {
                GridBotData central = null;

                if (dict.TryGetValue("中央", out var directCentral) && directCentral != null)
                {
                    central = directCentral;
                }
                else if (!string.IsNullOrWhiteSpace(cfg?.梁COMBOBOX)
                         && dict.TryGetValue(cfg.梁COMBOBOX, out var selected) && selected != null)
                {
                    central = selected;
                }

                if (central != null)
                {
                    top = ParseDoubleInvariant(central.上TEXTBOX);
                    bottom = ParseDoubleInvariant(central.下TEXTBOX);
                    left = ParseDoubleInvariant(central.左TEXTBOX);
                    right = ParseDoubleInvariant(central.右TEXTBOX);
                }
            }

            return (top, bottom, left, right);
        }

        // [NEW] Lấy offsets cột (Up/Down/Left/Right) cho cặp (階,通) hiện tại để vẽ 4 line dọc khung bù
        // nhớ: using System.Text;
        private (double up, double down, double left, double right) GetOffsetsByPosition(string kai, string tsu, string name)
        {
            double up = 500, down = 500, left = 500, right = 500;
            // key và nhãn vị trí
            string key = $"{kai}::{tsu}";            // ví dụ: "1F::Y1"
            string posLabel = $"{kai} {tsu}-{name}"; // ví dụ: "1F Y1-X1"

            var haichiList = _projectData?.Haichi?.柱配置図;
            if (haichiList == null)
                return (up, down, left, right);

            foreach (var haichi in haichiList)
            {
                if (haichi?.BeamSegmentsMap == null) continue;
                if (!haichi.BeamSegmentsMap.TryGetValue(key, out var segs) || segs == null) continue;

                var seg = segs.FirstOrDefault(s => s.位置表示 == posLabel);
                if (seg != null)
                {
                    // parse string → double an toàn
                    double Parse(string s) =>
                        double.TryParse(s, System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture,
                                        out var v) ? v : 0;

                    up = Parse(seg.上側のズレ);
                    down = Parse(seg.下側のズレ);
                    left = Parse(seg.左側のズレ);
                    right = Parse(seg.右側のズレ);
                    break; // đã tìm thấy thì thoát luôn
                }
            }

            return (up, down, left, right);
        }

        // [NEW] === cập nhật 1 giá trị Span (mm) trong ProjectData.Kihon ===
        // tsuIsY => đang vẽ dãy X nên dùng ListSpanX; tsuIsX => đang vẽ dãy Y nên dùng ListSpanY
        private bool UpdateSpanValue(int spanIndex, double newMm, bool tsuIsX, bool tsuIsY)
        {
            var k = _projectData?.Kihon;
            if (k == null) return false;

            object listObj = tsuIsY ? (object)k.ListSpanX : (object)k.ListSpanY;
            if (listObj == null) return false;

            if (listObj is IList list && spanIndex >= 0 && spanIndex < list.Count)
            {
                var item = list[spanIndex];
                if (item == null) return false;

                var p = item.GetType().GetProperty("Span");
                if (p == null) return false;

                var newValue = newMm.ToString(CultureInfo.InvariantCulture);
                var currentValue = p.GetValue(item)?.ToString();
                if (string.Equals(currentValue, newValue, StringComparison.Ordinal))
                    return false;

                // set "Span" = chuỗi mm invariant
                p.SetValue(item, newValue);
                PropertyChangeTracker.MarkChanged();
                return true;
            }
            return false;
        }

        // [NEW] === Vẽ nhãn span có thể click-để-sửa ===
        private void DrawEditableSpanLabel(Canvas canvas, WCTransform T,
                                           double cx, double cy,
                                           int spanIndex,
                                           string initialText,
                                           bool tsuIsX, bool tsuIsY,
                                           GridBotsecozu item)
        {
            var k = _projectData.Kihon;
            var names = tsuIsY ? k.NameX.Select(n => n.Name).ToList()
                               : tsuIsX ? k.NameY.Select(n => n.Name).ToList()
                                        : new List<string>();
            int previewAxisCount = Math.Max(names.Count, 1);
            double fontScale = T.FontScale > 0 ? T.FontScale : 1.0;
            const double baseDimFont = 10.0;
            double labelFontPx = Math.Max(6.0, baseDimFont - 10 * Math.Max(0, previewAxisCount - 2));
            double effectiveFontPx = 12;

            var p = T.P(cx, cy);

            var tb = DrawText_Rec(canvas, T, item, initialText,
                                 cx, cy, labelFontPx, Brushes.Black,
                                 HAnchor.Center, VAnchor.Bottom, 200, "TEXT",
                                 forceCanvasVisual: true);
            tb.Background = Brushes.Transparent;
            //tb.Padding = new Thickness(6, 2, 6, 2);
            tb.Cursor = Cursors.Hand;
            tb.IsHitTestVisible = true;

            EnableEditableHoverWithBox(tb, canvas,
               hoverForeground: Brushes.Blue,
               hoverFill: new SolidColorBrush(Color.FromArgb(100, 30, 144, 255)),
               hoverStroke: Brushes.Blue);

            Panel.SetZIndex(tb, 1000);

            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var sz = tb.DesiredSize;
            System.Windows.Controls.Primitives.Popup spanPopup = null;

            tb.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;

                string originalText = tb.Text ?? string.Empty;
                try
                {
                    if (spanPopup != null)
                    {
                        spanPopup.IsOpen = false;
                        spanPopup = null;
                    }
                }
                catch { }

                Brush normalBg = Brushes.Transparent;
                Brush selectedBg = new SolidColorBrush(Color.FromRgb(210, 225, 255));
                Brush dividerBrush = PopupMenuDividerBrush;

                UIElement WithRowDivider(UIElement child)
                {
                    return new Border
                    {
                        BorderBrush = dividerBrush,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Child = child
                    };
                }

                ControlTemplate flatBtnTemplate = null;
                ControlTemplate GetFlatBtnTemplate()
                {
                    if (flatBtnTemplate != null) return flatBtnTemplate;

                    var b = new FrameworkElementFactory(typeof(Border));
                    b.SetValue(Border.SnapsToDevicePixelsProperty, true);
                    b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                    b.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                    b.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                    b.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

                    var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                    cp.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
                    cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
                    cp.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Button.VerticalContentAlignmentProperty));
                    b.AppendChild(cp);

                    flatBtnTemplate = new ControlTemplate(typeof(Button)) { VisualTree = b };
                    return flatBtnTemplate;
                }

                Border WrapBox(UIElement child)
                {
                    var host = new ContentControl
                    {
                        Content = child,
                        Width = 100,
                        FontSize = SystemFonts.MessageFontSize,
                        FontFamily = SystemFonts.MessageFontFamily
                    };

                    return new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = PopupMenuBorderBrush,
                        BorderThickness = PopupMenuBorderThickness,
                        CornerRadius = new CornerRadius(2),
                        Child = host
                    };
                }

                var row = new DockPanel { LastChildFill = true, Width = 90 };

                var lbl = new TextBlock
                {
                    Text = "長さ",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Center
                };
                DockPanel.SetDock(lbl, Dock.Left);
                lbl.Text = "長さ";

                var popupEditor = new TextBox
                {
                    Text = originalText,
                    FontSize = effectiveFontPx,
                    Width = 60,
                    MinWidth = 60,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Background = Brushes.Transparent,
                    Visibility = System.Windows.Visibility.Visible
                };
                DockPanel.SetDock(popupEditor, Dock.Right);

                row.Children.Add(lbl);
                row.Children.Add(popupEditor);

                var rowButton = new Button
                {
                    Content = row,
                    HorizontalContentAlignment = HorizontalAlignment.Left,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(10, 6, 10, 6),
                    Background = normalBg,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    MinWidth = 180,
                    OverridesDefaultStyle = true,
                    Template = GetFlatBtnTemplate(),
                    Focusable = false,
                    IsTabStop = false
                };
                rowButton.MouseEnter += (_, __) => rowButton.Background = selectedBg;
                rowButton.MouseLeave += (_, __) =>
                {
                    if (!popupEditor.IsKeyboardFocusWithin)
                        rowButton.Background = normalBg;
                };

                var root = new StackPanel { Orientation = Orientation.Vertical };
                root.Children.Add(WithRowDivider(rowButton));

                spanPopup = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = tb,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                    HorizontalOffset = 6,
                    VerticalOffset = -2,
                    AllowsTransparency = true,
                    StaysOpen = false,
                    Child = WrapBox(root)
                };

                void FocusPopupEditor()
                {
                    popupEditor.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        popupEditor.Focus();
                        popupEditor.SelectAll();
                    }), DispatcherPriority.Input);
                }

                void SyncPopupEditorFromCurrent()
                {
                    popupEditor.Text = tb.Text ?? originalText;
                }

                void SetDisplayMode()
                {
                    popupEditor.IsReadOnly = true;
                    popupEditor.BorderThickness = new Thickness(0);
                    popupEditor.BorderBrush = Brushes.Transparent;
                    popupEditor.Background = Brushes.Transparent;
                }

                void SetEditMode()
                {
                    popupEditor.IsReadOnly = false;
                    popupEditor.BorderThickness = new Thickness(1);
                    popupEditor.BorderBrush = PopupMenuEditorBorderBrush;
                    popupEditor.Background = Brushes.White;
                }

                void BeginPopupEdit()
                {
                    SyncPopupEditorFromCurrent();
                    SetEditMode();
                    rowButton.Background = selectedBg;
                    FocusPopupEditor();
                }

                void EndPopupEdit()
                {
                    SyncPopupEditorFromCurrent();
                    SetDisplayMode();
                    rowButton.Background = rowButton.IsMouseOver ? selectedBg : normalBg;
                }

                void CloseSpanPopup()
                {
                    try
                    {
                        if (spanPopup != null)
                            spanPopup.IsOpen = false;
                    }
                    catch { }
                    spanPopup = null;
                }

                void CommitPopupAndRedraw()
                {
                    if (double.TryParse(popupEditor.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var mm)
                     || double.TryParse(popupEditor.Text, out mm))
                    {
                        if (mm > 0 && UpdateSpanValue(spanIndex, mm, tsuIsX, tsuIsY))
                        {
                            ClearTanbuHookOverridesForSpan(spanIndex);
                            Redraw(canvas, item);
                            CloseSpanPopup();
                            return;
                        }
                    }

                    BeginPopupEdit();
                }

                rowButton.Click += (_, clickArgs) =>
                {
                    clickArgs.Handled = true;
                    if (popupEditor.IsReadOnly)
                        BeginPopupEdit();
                    else
                        FocusPopupEditor();
                };

                popupEditor.GotKeyboardFocus += (_, __) =>
                {
                    rowButton.Background = selectedBg;
                };

                popupEditor.PreviewMouseLeftButtonDown += (_, clickArgs) =>
                {
                    if (!popupEditor.IsReadOnly) return;
                    BeginPopupEdit();
                    clickArgs.Handled = true;
                };

                popupEditor.KeyDown += (_, ke) =>
                {
                    if (ke.Key == Key.Enter)
                    {
                        CommitPopupAndRedraw();
                        ke.Handled = true;
                    }
                    else if (ke.Key == Key.Escape)
                    {
                        EndPopupEdit();
                        CloseSpanPopup();
                        ke.Handled = true;
                    }
                };

                popupEditor.LostKeyboardFocus += (_, __) =>
                {
                    if (!popupEditor.IsReadOnly)
                        EndPopupEdit();
                };

                tb.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (spanPopup != null)
                    {
                        SyncPopupEditorFromCurrent();
                        SetDisplayMode();
                        spanPopup.IsOpen = true;
                        rowButton.Background = normalBg;
                    }
                }), DispatcherPriority.Input);
                return;

                var editor = new TextBox
                {
                    Text = originalText,
                    FontSize = effectiveFontPx,
                    Width = Math.Max(60, sz.Width + 20),
                    Background = Brushes.White
                };

                Canvas.SetLeft(editor, p.X - editor.Width / 2);
                Canvas.SetTop(editor, p.Y - sz.Height - 2);
                Panel.SetZIndex(editor, 2000);
                canvas.Children.Add(editor);

                editor.Focus();
                editor.SelectAll();


                void CommitAndRedraw()
                {
                    if (double.TryParse(editor.Text, NumberStyles.Float,
                                        CultureInfo.InvariantCulture, out var mm)
                     || double.TryParse(editor.Text, out mm))
                    {
                        if (mm > 0)
                        {
                            if (UpdateSpanValue(spanIndex, mm, tsuIsX, tsuIsY))
                            {
                                ClearTanbuHookOverridesForSpan(spanIndex); // <<< reset về default theo eff mới
                                Redraw(canvas, item);
                                return;
                            }

                        }
                    }
                    canvas.Children.Remove(editor);
                }

                editor.KeyDown += (ks, ke) =>
                {
                    if (ke.Key == Key.Enter) CommitAndRedraw();
                    else if (ke.Key == Key.Escape) { canvas.Children.Remove(editor); ke.Handled = true; }
                };

                editor.LostKeyboardFocus += (ls, le) => CommitAndRedraw();
            };
        }

        // hàm chỉnh text cam 
        // =========================
        // [NEW] In-memory overrides cho TEXT CAM (DIM) 2 dòng
        // =========================
        private struct OrangeDimTextKey : IEquatable<OrangeDimTextKey>
        {
            public int RowIndex;
            public bool IsTop;
            public int Wx10; // world-x * 10 (0.1mm)
            public int Wy10; // world-y * 10 (0.1mm)

            public OrangeDimTextKey(int rowIndex, bool isTop, double wx, double wy)
            {
                RowIndex = rowIndex;
                IsTop = isTop;
                Wx10 = Quantize10(wx);
                Wy10 = Quantize10(wy);
            }

            private static int Quantize10(double v)
                => (int)Math.Round(v * 10.0);

            public bool Equals(OrangeDimTextKey other)
                => RowIndex == other.RowIndex
                   && IsTop == other.IsTop
                   && Wx10 == other.Wx10
                   && Wy10 == other.Wy10;

            public override bool Equals(object obj)
                => obj is OrangeDimTextKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + RowIndex;
                    h = h * 31 + (IsTop ? 1 : 0);
                    h = h * 31 + Wx10;
                    h = h * 31 + Wy10;
                    return h;
                }
            }
        }

        private readonly Dictionary<GridBotsecozu, Dictionary<OrangeDimTextKey, string>> _orangeDimTextOverrides
            = new Dictionary<GridBotsecozu, Dictionary<OrangeDimTextKey, string>>();

        private string GetOrangeDimText(GridBotsecozu owner, OrangeDimTextKey key, string fallback)
        {
            if (owner != null
                && _orangeDimTextOverrides.TryGetValue(owner, out var dict)
                && dict != null
                && dict.TryGetValue(key, out var txt)
                && !string.IsNullOrWhiteSpace(txt))
            {
                return txt;
            }
            return fallback ?? string.Empty;
        }
        // =========================
        // [NEW] In-memory overrides cho ANKA (test nhanh, chưa lưu)
        // =========================
        private enum AnkaSide
        {
            Left = 0,
            Right = 1
        }

        private struct AnkaDimKey : IEquatable<AnkaDimKey>
        {
            public OrangeDimTextKey DimKey;
            public AnkaSide Side;

            public AnkaDimKey(OrangeDimTextKey dimKey, AnkaSide side)
            {
                DimKey = dimKey;
                Side = side;
            }

            public bool Equals(AnkaDimKey other)
                => DimKey.Equals(other.DimKey) && Side == other.Side;

            public override bool Equals(object obj)
                => obj is AnkaDimKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + DimKey.GetHashCode();
                    h = h * 31 + (int)Side;
                    return h;
                }
            }
        }

        private struct AnkaSegKey : IEquatable<AnkaSegKey>
        {
            public OrangeSegKey SegKey;
            public AnkaSide Side;

            public AnkaSegKey(OrangeSegKey segKey, AnkaSide side)
            {
                SegKey = segKey;
                Side = side;
            }

            public bool Equals(AnkaSegKey other)
                => SegKey.Equals(other.SegKey) && Side == other.Side;

            public override bool Equals(object obj)
                => obj is AnkaSegKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + SegKey.GetHashCode();
                    h = h * 31 + (int)Side;
                    return h;
                }
            }
        }

        // value: signed length (mm world)
        //  > 0 : vẽ lên (y + len)
        //  < 0 : vẽ xuống (y + len) (len âm)
        private readonly Dictionary<GridBotsecozu, Dictionary<AnkaDimKey, double>> _ankaOverrides
            = new Dictionary<GridBotsecozu, Dictionary<AnkaDimKey, double>>();
        private readonly Dictionary<GridBotsecozu, Dictionary<AnkaSegKey, double>> _ankaSegOverrides
            = new Dictionary<GridBotsecozu, Dictionary<AnkaSegKey, double>>();

        private bool TryGetAnkaOverrideBySegKey(GridBotsecozu owner, OrangeSegKey segKey, AnkaSide side, out double signedLen)
        {
            signedLen = 0.0;
            if (owner == null) return false;

            if (_ankaSegOverrides.TryGetValue(owner, out var segDict)
                && segDict != null
                && segDict.TryGetValue(new AnkaSegKey(segKey, side), out var segVal))
            {
                signedLen = segVal;
                return true;
            }

            if (_ankaOverrides.TryGetValue(owner, out var legacyDict) && legacyDict != null)
            {
                AnkaDimKey? legacyKey = null;
                double legacyValue = 0.0;
                foreach (var entry in legacyDict)
                {
                    if (entry.Key.Side != side) continue;
                    if (TryGetSegKeyForDimKey(owner, entry.Key.DimKey, out var legacySegKey)
                        && legacySegKey.Equals(segKey))
                    {
                        legacyKey = entry.Key;
                        legacyValue = entry.Value;
                        break;
                    }
                }

                if (legacyKey.HasValue)
                {
                    if (!_ankaSegOverrides.TryGetValue(owner, out var targetDict) || targetDict == null)
                    {
                        targetDict = new Dictionary<AnkaSegKey, double>();
                        _ankaSegOverrides[owner] = targetDict;
                    }

                    var segK = new AnkaSegKey(segKey, side);
                    targetDict[segK] = legacyValue;
                    legacyDict.Remove(legacyKey.Value);
                    PersistAnkaSegOverride(owner, segK, legacyValue);
                    PersistAnkaDimOverride(owner, legacyKey.Value, 0.0);
                    signedLen = legacyValue;
                    return true;
                }
            }

            return false;
        }

        private double GetAnkaOverrideBySegKeyOrDefault(GridBotsecozu owner, OrangeSegKey segKey, AnkaSide side, double fallbackSigned)
        {
            return TryGetAnkaOverrideBySegKey(owner, segKey, side, out var signedLen)
                ? signedLen
                : fallbackSigned;
        }

        private bool SetAnkaOverrideBySegKey(GridBotsecozu owner, OrangeSegKey segKey, AnkaSide side, double signedLen)
        {
            if (owner == null) return false;

            if (!_ankaSegOverrides.TryGetValue(owner, out var segDict) || segDict == null)
            {
                segDict = new Dictionary<AnkaSegKey, double>();
                _ankaSegOverrides[owner] = segDict;
            }

            var segK = new AnkaSegKey(segKey, side);

            if (Math.Abs(signedLen) <= 0.0001)
            {
                bool removed = segDict.Remove(segK);
                return PersistAnkaSegOverride(owner, segK, 0.0) || removed;
            }

            bool changed = !segDict.TryGetValue(segK, out var old)
                           || Math.Abs(old - signedLen) > 0.0001;
            segDict[segK] = signedLen;
            return PersistAnkaSegOverride(owner, segK, signedLen) || changed;
        }

        private double GetAnkaOverride(GridBotsecozu owner, OrangeDimTextKey dimKey, AnkaSide side, double fallbackSigned)
        {
            if (owner == null) return fallbackSigned;

            if (TryGetSegKeyForDimKey(owner, dimKey, out var segKey)
                && TryGetAnkaOverrideBySegKey(owner, segKey, side, out var segVal))
            {
                return segVal;
            }

            if (_ankaOverrides.TryGetValue(owner, out var dict)
                && dict != null
                && dict.TryGetValue(new AnkaDimKey(dimKey, side), out var v))
            {
                return v;
            }
            return fallbackSigned;
        }

        private bool SetAnkaOverride(GridBotsecozu owner, OrangeDimTextKey dimKey, AnkaSide side, double signedLen)
        {
            if (owner == null) return false;

            if (TryGetSegKeyForDimKey(owner, dimKey, out var segKey))
            {
                if (!_ankaSegOverrides.TryGetValue(owner, out var segDict) || segDict == null)
                {
                    segDict = new Dictionary<AnkaSegKey, double>();
                    _ankaSegOverrides[owner] = segDict;
                }

                var segK = new AnkaSegKey(segKey, side);

                if (Math.Abs(signedLen) <= 0.0001)
                {
                    bool removed = segDict.Remove(segK);
                    bool persistChanged = PersistAnkaSegOverride(owner, segK, 0.0);
                    persistChanged = PersistAnkaDimOverride(owner, new AnkaDimKey(dimKey, side), 0.0) || persistChanged;
                    return persistChanged || removed;
                }

                bool segChanged = !segDict.TryGetValue(segK, out var segOld)
                                  || Math.Abs(segOld - signedLen) > 0.0001;
                segDict[segK] = signedLen;

                if (_ankaOverrides.TryGetValue(owner, out var legacyDict) && legacyDict != null)
                {
                    legacyDict.Remove(new AnkaDimKey(dimKey, side));
                }

                bool persistSegChanged = PersistAnkaSegOverride(owner, segK, signedLen);
                bool persistLegacyChanged = PersistAnkaDimOverride(owner, new AnkaDimKey(dimKey, side), 0.0);
                return persistLegacyChanged || persistSegChanged || segChanged;
            }

            if (!_ankaOverrides.TryGetValue(owner, out var dict) || dict == null)
            {
                dict = new Dictionary<AnkaDimKey, double>();
                _ankaOverrides[owner] = dict;
            }

            var k = new AnkaDimKey(dimKey, side);

            // signedLen == 0 => remove override (coi như chưa set)
            if (Math.Abs(signedLen) <= 0.0001)
            {
                if (dict.ContainsKey(k))
                {
                    dict.Remove(k);
                    PersistAnkaDimOverride(owner, k, 0.0);
                    return true;
                }
                return PersistAnkaDimOverride(owner, k, 0.0);
            }

            bool changed = !dict.TryGetValue(k, out var old) || Math.Abs(old - signedLen) > 0.0001;
            dict[k] = signedLen;
            return PersistAnkaDimOverride(owner, k, signedLen) || changed;
        }

        private bool SetOrangeDimText(GridBotsecozu owner, OrangeDimTextKey key, string newText)
        {
            if (owner == null) return false;

            string t = (newText ?? string.Empty).Trim();

            if (!_orangeDimTextOverrides.TryGetValue(owner, out var dict) || dict == null)
            {
                dict = new Dictionary<OrangeDimTextKey, string>();
                _orangeDimTextOverrides[owner] = dict;
            }

            // empty => remove override
            if (string.IsNullOrWhiteSpace(t))
            {
                if (dict.ContainsKey(key))
                {
                    dict.Remove(key);
                    PersistOrangeDimTextOverride(owner, key, "");
                    return true;
                }
                return PersistOrangeDimTextOverride(owner, key, "");
            }

            bool changed = !dict.TryGetValue(key, out var old)
                           || !string.Equals(old, t, StringComparison.Ordinal);
            dict[key] = t;
            return PersistOrangeDimTextOverride(owner, key, t) || changed;
        }
        // ===== Helper: tạo vùng preview để vẽ line minh hoa anka
        // =========================
        // [NEW] In-memory delete/suppress cho SEGMENT CAM (line DIM) + ANKA theo row
        // =========================
        private struct OrangeSegKey : IEquatable<OrangeSegKey>
        {
            public int RowIndex;
            public int X1_10;
            public int X2_10;
            public int Y_10;

            public OrangeSegKey(int rowIndex, double x1, double x2, double y)
            {
                RowIndex = rowIndex;
                X1_10 = (int)Math.Round(x1 * 10.0);
                X2_10 = (int)Math.Round(x2 * 10.0);
                Y_10 = (int)Math.Round(y * 10.0);
            }

            public bool Equals(OrangeSegKey other)
                => RowIndex == other.RowIndex
                   && X1_10 == other.X1_10
                   && X2_10 == other.X2_10
                   && Y_10 == other.Y_10;

            public override bool Equals(object obj)
                => obj is OrangeSegKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + RowIndex;
                    h = h * 31 + X1_10;
                    h = h * 31 + X2_10;
                    h = h * 31 + Y_10;
                    return h;
                }
            }
        }

        private struct OrangeSegInfo
        {
            public OrangeSegKey SegKey;
            public OrangeDimTextKey TopKey;

            public bool HasBottomKey;
            public OrangeDimTextKey BottomKey;

            public bool HitLeftAnka;
            public bool HitRightAnka;
            public double DefaultLeftAnkaSigned;
            public double DefaultRightAnkaSigned;
        }

        // map: click DIM (TopKey) -> segment info (x1/x2/y + hitLeft/hitRight + bottomKey)
        private readonly Dictionary<GridBotsecozu, Dictionary<OrangeDimTextKey, OrangeSegInfo>> _orangeDimToSegInfo
            = new Dictionary<GridBotsecozu, Dictionary<OrangeDimTextKey, OrangeSegInfo>>();
        private readonly Dictionary<GridBotsecozu, Dictionary<OrangeSegKey, OrangeSegInfo>> _orangeSegToInfo
            = new Dictionary<GridBotsecozu, Dictionary<OrangeSegKey, OrangeSegInfo>>();

        // set: segment đã bị xoá (skip vẽ line + DIM)
        private readonly Dictionary<GridBotsecozu, HashSet<OrangeSegKey>> _deletedOrangeSegs
            = new Dictionary<GridBotsecozu, HashSet<OrangeSegKey>>();

        private struct OrangeSegOverride
        {
            public double X1;
            public double X2;
        }

        private struct OrangeSegResolved
        {
            public double BaseX1;
            public double BaseX2;
            public double X1;
            public double X2;
            public bool IsEqualCutChild;
        }

        private readonly Dictionary<GridBotsecozu, Dictionary<OrangeSegKey, OrangeSegOverride>> _orangeSegOverrides
            = new Dictionary<GridBotsecozu, Dictionary<OrangeSegKey, OrangeSegOverride>>();

        private readonly Dictionary<GridBotsecozu, Dictionary<OrangeSegKey, List<double>>> _orangeSegEqualCutPoints
            = new Dictionary<GridBotsecozu, Dictionary<OrangeSegKey, List<double>>>();

        private struct OrangeCutPointKey : IEquatable<OrangeCutPointKey>
        {
            public int RowIndex;
            public int X_10;
            public int Y_10;

            public OrangeCutPointKey(int rowIndex, double x, double y)
            {
                RowIndex = rowIndex;
                X_10 = (int)Math.Round(x * 10.0);
                Y_10 = (int)Math.Round(y * 10.0);
            }

            public bool Equals(OrangeCutPointKey other)
                => RowIndex == other.RowIndex && X_10 == other.X_10 && Y_10 == other.Y_10;

            public override bool Equals(object obj)
                => obj is OrangeCutPointKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int h = 17;
                    h = h * 31 + RowIndex;
                    h = h * 31 + X_10;
                    h = h * 31 + Y_10;
                    return h;
                }
            }
        }

        private readonly Dictionary<GridBotsecozu, HashSet<OrangeCutPointKey>> _orangeSegCutMarkers
            = new Dictionary<GridBotsecozu, HashSet<OrangeCutPointKey>>();
        private readonly HashSet<GridBotsecozu> _orangeCutMarkerRedrawInProgress
            = new HashSet<GridBotsecozu>();

        private static string MakeOrangeDimTextPersistKey(OrangeDimTextKey key)
            => $"{key.RowIndex}|{(key.IsTop ? 1 : 0)}|{key.Wx10}|{key.Wy10}";

        private static bool TryParseOrangeDimTextPersistKey(string raw, out OrangeDimTextKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var parts = raw.Split('|');
            if (parts.Length != 4) return false;
            if (!int.TryParse(parts[0], out var rowIndex)) return false;
            if (!int.TryParse(parts[1], out var isTopFlag)) return false;
            if (!int.TryParse(parts[2], out var wx10)) return false;
            if (!int.TryParse(parts[3], out var wy10)) return false;

            key = new OrangeDimTextKey
            {
                RowIndex = rowIndex,
                IsTop = isTopFlag != 0,
                Wx10 = wx10,
                Wy10 = wy10
            };
            return true;
        }

        private static string MakeOrangeSegPersistKey(OrangeSegKey key)
            => $"{key.RowIndex}|{key.X1_10}|{key.X2_10}|{key.Y_10}";

        private static bool TryParseOrangeSegPersistKey(string raw, out OrangeSegKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var parts = raw.Split('|');
            if (parts.Length != 4) return false;
            if (!int.TryParse(parts[0], out var rowIndex)) return false;
            if (!int.TryParse(parts[1], out var x1_10)) return false;
            if (!int.TryParse(parts[2], out var x2_10)) return false;
            if (!int.TryParse(parts[3], out var y_10)) return false;

            key = new OrangeSegKey
            {
                RowIndex = rowIndex,
                X1_10 = x1_10,
                X2_10 = x2_10,
                Y_10 = y_10
            };
            return true;
        }

        private static string MakeAnkaDimPersistKey(AnkaDimKey key)
            => $"{MakeOrangeDimTextPersistKey(key.DimKey)}|{(int)key.Side}";

        private static bool TryParseAnkaDimPersistKey(string raw, out AnkaDimKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var parts = raw.Split('|');
            if (parts.Length != 5) return false;
            if (!TryParseOrangeDimTextPersistKey(string.Join("|", parts.Take(4)), out var dimKey)) return false;
            if (!int.TryParse(parts[4], out var sideValue)) return false;
            if (sideValue < (int)AnkaSide.Left || sideValue > (int)AnkaSide.Right) return false;

            key = new AnkaDimKey(dimKey, (AnkaSide)sideValue);
            return true;
        }

        private static string MakeAnkaSegPersistKey(AnkaSegKey key)
            => $"{MakeOrangeSegPersistKey(key.SegKey)}|{(int)key.Side}";

        private static bool TryParseAnkaSegPersistKey(string raw, out AnkaSegKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var parts = raw.Split('|');
            if (parts.Length != 5) return false;
            if (!TryParseOrangeSegPersistKey(string.Join("|", parts.Take(4)), out var segKey)) return false;
            if (!int.TryParse(parts[4], out var sideValue)) return false;
            if (sideValue < (int)AnkaSide.Left || sideValue > (int)AnkaSide.Right) return false;

            key = new AnkaSegKey(segKey, (AnkaSide)sideValue);
            return true;
        }

        private static List<double> NormalizePersistedCutPoints(IEnumerable<double> cuts)
        {
            if (cuts == null) return new List<double>();

            return cuts
                .Where(c => !double.IsNaN(c) && !double.IsInfinity(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();
        }

        private bool PersistOrangeDimTextOverride(GridBotsecozu owner, OrangeDimTextKey key, string text)
        {
            if (owner == null) return false;
            string persistedKey = MakeOrangeDimTextPersistKey(key);
            bool changed;

            if (string.IsNullOrWhiteSpace(text))
            {
                if (owner.OrangeDimTextOverrides == null) return false;
                changed = owner.OrangeDimTextOverrides.Remove(persistedKey);
            }
            else
            {
                if (owner.OrangeDimTextOverrides == null)
                    owner.OrangeDimTextOverrides = new Dictionary<string, string>();
                string normalized = text.Trim();
                changed = !owner.OrangeDimTextOverrides.TryGetValue(persistedKey, out var existing)
                          || !string.Equals(existing, normalized, StringComparison.Ordinal);
                owner.OrangeDimTextOverrides[persistedKey] = normalized;
            }

            if (changed)
                PropertyChangeTracker.MarkChanged();

            return changed;
        }

        private bool PersistAnkaDimOverride(GridBotsecozu owner, AnkaDimKey key, double signedLen)
        {
            if (owner == null) return false;
            string persistedKey = MakeAnkaDimPersistKey(key);
            bool changed;

            if (Math.Abs(signedLen) <= 0.0001)
            {
                if (owner.AnkaOverrides == null) return false;
                changed = owner.AnkaOverrides.Remove(persistedKey);
            }
            else
            {
                if (owner.AnkaOverrides == null)
                    owner.AnkaOverrides = new Dictionary<string, double>();
                changed = !owner.AnkaOverrides.TryGetValue(persistedKey, out var existing)
                          || Math.Abs(existing - signedLen) > 0.0001;
                owner.AnkaOverrides[persistedKey] = signedLen;
            }

            if (changed)
                PropertyChangeTracker.MarkChanged();

            return changed;
        }

        private bool PersistAnkaSegOverride(GridBotsecozu owner, AnkaSegKey key, double signedLen)
        {
            if (owner == null) return false;
            string persistedKey = MakeAnkaSegPersistKey(key);
            bool changed;

            if (Math.Abs(signedLen) <= 0.0001)
            {
                if (owner.AnkaSegOverrides == null) return false;
                changed = owner.AnkaSegOverrides.Remove(persistedKey);
            }
            else
            {
                if (owner.AnkaSegOverrides == null)
                    owner.AnkaSegOverrides = new Dictionary<string, double>();
                changed = !owner.AnkaSegOverrides.TryGetValue(persistedKey, out var existing)
                          || Math.Abs(existing - signedLen) > 0.0001;
                owner.AnkaSegOverrides[persistedKey] = signedLen;
            }

            if (changed)
                PropertyChangeTracker.MarkChanged();

            return changed;
        }

        private bool PersistDeletedOrangeSeg(GridBotsecozu owner, OrangeSegKey key, bool isDeleted)
        {
            if (owner == null) return false;
            string persistedKey = MakeOrangeSegPersistKey(key);
            bool changed;

            if (isDeleted)
            {
                if (owner.DeletedOrangeSegs == null)
                    owner.DeletedOrangeSegs = new HashSet<string>();
                changed = owner.DeletedOrangeSegs.Add(persistedKey);
            }
            else
            {
                if (owner.DeletedOrangeSegs == null) return false;
                changed = owner.DeletedOrangeSegs.Remove(persistedKey);
            }

            if (changed)
                PropertyChangeTracker.MarkChanged();

            return changed;
        }

        private bool PersistOrangeSegOverride(GridBotsecozu owner, OrangeSegKey key, double x1, double x2)
        {
            if (owner == null) return false;
            double baseX1 = key.X1_10 / 10.0;
            double baseX2 = key.X2_10 / 10.0;
            string persistedKey = MakeOrangeSegPersistKey(key);
            bool isBase = Math.Abs(x1 - baseX1) < 1e-6 && Math.Abs(x2 - baseX2) < 1e-6;
            bool changed;

            if (isBase)
            {
                if (owner.OrangeSegOverrides == null) return false;
                changed = owner.OrangeSegOverrides.Remove(persistedKey);
            }
            else
            {
                if (owner.OrangeSegOverrides == null)
                    owner.OrangeSegOverrides = new Dictionary<string, OrangeSegOverridePersistData>();
                changed = !owner.OrangeSegOverrides.TryGetValue(persistedKey, out var existing)
                          || existing == null
                          || Math.Abs(existing.X1 - x1) > 1e-6
                          || Math.Abs(existing.X2 - x2) > 1e-6;
                owner.OrangeSegOverrides[persistedKey] = new OrangeSegOverridePersistData
                {
                    X1 = x1,
                    X2 = x2
                };
            }

            if (changed)
                PropertyChangeTracker.MarkChanged();

            return changed;
        }

        private bool PersistOrangeSegEqualCutPoints(GridBotsecozu owner, OrangeSegKey key, IEnumerable<double> cuts)
        {
            if (owner == null) return false;
            string persistedKey = MakeOrangeSegPersistKey(key);
            var normalizedCuts = NormalizePersistedCutPoints(cuts);
            bool changed;

            if (normalizedCuts.Count == 0)
            {
                if (owner.OrangeSegEqualCutPoints == null) return false;
                changed = owner.OrangeSegEqualCutPoints.Remove(persistedKey);
            }
            else
            {
                if (owner.OrangeSegEqualCutPoints == null)
                    owner.OrangeSegEqualCutPoints = new Dictionary<string, List<double>>();
                changed = !owner.OrangeSegEqualCutPoints.TryGetValue(persistedKey, out var existing)
                          || existing == null
                          || existing.Count != normalizedCuts.Count
                          || existing.Where((value, index) => Math.Abs(value - normalizedCuts[index]) > 1e-6).Any();
                owner.OrangeSegEqualCutPoints[persistedKey] = normalizedCuts;
            }

            if (changed)
                PropertyChangeTracker.MarkChanged();

            return changed;
        }

        private void SyncRuntimeOverridesFromModel(GridBotsecozu owner)
        {
            if (owner == null) return;

            var orangeTextOverrides = new Dictionary<OrangeDimTextKey, string>();
            if (owner.OrangeDimTextOverrides != null)
            {
                foreach (var entry in owner.OrangeDimTextOverrides)
                {
                    if (TryParseOrangeDimTextPersistKey(entry.Key, out var key)
                        && !string.IsNullOrWhiteSpace(entry.Value))
                    {
                        orangeTextOverrides[key] = entry.Value.Trim();
                    }
                }

            }
            _orangeDimTextOverrides[owner] = orangeTextOverrides;

            var tanbuDiameterOverrides = new Dictionary<(int spanIndex, bool isRightAbdominal), string>();
            if (owner.TanbuDiameterOverrides != null)
            {
                foreach (var entry in owner.TanbuDiameterOverrides)
                {
                    if (TryParseTanbuDiameterKey(entry.Key, out var key)
                        && !string.IsNullOrWhiteSpace(entry.Value))
                    {
                        tanbuDiameterOverrides[key] = entry.Value.Trim();
                    }
                }
            }
            _tanbuDiameterOverrides[owner] = tanbuDiameterOverrides;

            var ankaOverrides = new Dictionary<AnkaDimKey, double>();
            if (owner.AnkaOverrides != null)
            {
                foreach (var entry in owner.AnkaOverrides)
                {
                    if (TryParseAnkaDimPersistKey(entry.Key, out var key)
                        && Math.Abs(entry.Value) > 0.0001)
                    {
                        ankaOverrides[key] = entry.Value;
                    }
                }
            }
            _ankaOverrides[owner] = ankaOverrides;

            var ankaSegOverrides = new Dictionary<AnkaSegKey, double>();
            if (owner.AnkaSegOverrides != null)
            {
                foreach (var entry in owner.AnkaSegOverrides)
                {
                    if (TryParseAnkaSegPersistKey(entry.Key, out var key)
                        && Math.Abs(entry.Value) > 0.0001)
                    {
                        ankaSegOverrides[key] = entry.Value;
                    }
                }
            }
            _ankaSegOverrides[owner] = ankaSegOverrides;

            var deletedOrangeSegs = new HashSet<OrangeSegKey>();
            if (owner.DeletedOrangeSegs != null)
            {
                foreach (var entry in owner.DeletedOrangeSegs)
                {
                    if (TryParseOrangeSegPersistKey(entry, out var key))
                        deletedOrangeSegs.Add(key);
                }
            }
            _deletedOrangeSegs[owner] = deletedOrangeSegs;

            var orangeSegOverrides = new Dictionary<OrangeSegKey, OrangeSegOverride>();
            if (owner.OrangeSegOverrides != null)
            {
                foreach (var entry in owner.OrangeSegOverrides)
                {
                    if (TryParseOrangeSegPersistKey(entry.Key, out var key) && entry.Value != null)
                    {
                        orangeSegOverrides[key] = new OrangeSegOverride
                        {
                            X1 = entry.Value.X1,
                            X2 = entry.Value.X2
                        };
                    }
                }
            }
            _orangeSegOverrides[owner] = orangeSegOverrides;

            var orangeSegEqualCutPoints = new Dictionary<OrangeSegKey, List<double>>();
            if (owner.OrangeSegEqualCutPoints != null)
            {
                foreach (var entry in owner.OrangeSegEqualCutPoints)
                {
                    if (TryParseOrangeSegPersistKey(entry.Key, out var key))
                    {
                        var normalizedCuts = NormalizePersistedCutPoints(entry.Value);
                        if (normalizedCuts.Count > 0)
                            orangeSegEqualCutPoints[key] = normalizedCuts;
                    }
                }
            }
            _orangeSegEqualCutPoints[owner] = orangeSegEqualCutPoints;
        }

        private bool TryGetOrangeSegOverride(GridBotsecozu owner, OrangeSegKey key, out OrangeSegOverride val)
        {
            val = default;
            if (owner == null) return false;
            return _orangeSegOverrides.TryGetValue(owner, out var dict)
                   && dict != null
                   && dict.TryGetValue(key, out val);
        }

        private (double x1, double x2) GetOrangeSegOverride(GridBotsecozu owner, OrangeSegKey key, double baseX1, double baseX2)
        {
            if (TryGetOrangeSegOverride(owner, key, out var val))
            {
                return (val.X1, val.X2);
            }
            return (baseX1, baseX2);
        }

        private bool SetOrangeSegOverride(GridBotsecozu owner, OrangeSegKey key, double x1, double x2)
        {
            if (owner == null) return false;

            double baseX1 = key.X1_10 / 10.0;
            double baseX2 = key.X2_10 / 10.0;
            bool isBase = Math.Abs(x1 - baseX1) < 1e-6 && Math.Abs(x2 - baseX2) < 1e-6;

            if (!_orangeSegOverrides.TryGetValue(owner, out var dict) || dict == null)
            {
                dict = new Dictionary<OrangeSegKey, OrangeSegOverride>();
                _orangeSegOverrides[owner] = dict;
            }

            if (isBase)
            {
                bool removed = dict.Remove(key);
                return PersistOrangeSegOverride(owner, key, x1, x2) || removed;
            }

            bool changed = !dict.TryGetValue(key, out var old)
                           || Math.Abs(old.X1 - x1) > 1e-6
                           || Math.Abs(old.X2 - x2) > 1e-6;
            dict[key] = new OrangeSegOverride { X1 = x1, X2 = x2 };
            return PersistOrangeSegOverride(owner, key, x1, x2) || changed;
        }

        private List<OrangeSegResolved> GetVisibleOrangeSegs(GridBotsecozu owner, int rowIndex, double y, List<(double x1, double x2)> segs)
        {
            var resolved = new List<OrangeSegResolved>(segs.Count);
            foreach (var seg in segs)
            {
                var key = new OrangeSegKey(rowIndex, seg.x1, seg.x2, y);
                var (x1, x2) = GetOrangeSegOverride(owner, key, seg.x1, seg.x2);
                ExpandOrangeSegByEqualCuts(owner, rowIndex, y, seg.x1, seg.x2, x1, x2, resolved, 0);
            }
            return resolved;
        }

        private List<OrangeSegResolved> ClampVisibleOrangeSegsToBounds(
            List<OrangeSegResolved> segs,
            double minX,
            double maxX)
        {
            if (segs == null || segs.Count == 0)
                return segs ?? new List<OrangeSegResolved>();

            bool hasMin = !double.IsNaN(minX) && !double.IsInfinity(minX);
            bool hasMax = !double.IsNaN(maxX) && !double.IsInfinity(maxX);
            if (!hasMin && !hasMax)
                return segs;

            var clamped = new List<OrangeSegResolved>(segs.Count);
            foreach (var seg in segs)
            {
                double x1 = seg.X1;
                double x2 = seg.X2;

                if (hasMin) x1 = Math.Max(x1, minX);
                if (hasMax) x2 = Math.Min(x2, maxX);
                if (x2 <= x1 + 1e-6) continue;

                clamped.Add(new OrangeSegResolved
                {
                    BaseX1 = seg.BaseX1,
                    BaseX2 = seg.BaseX2,
                    X1 = x1,
                    X2 = x2,
                    IsEqualCutChild = seg.IsEqualCutChild
                });
            }

            return clamped;
        }

        private void ExpandOrangeSegByEqualCuts(
            GridBotsecozu owner,
            int rowIndex,
            double y,
            double baseX1,
            double baseX2,
            double x1,
            double x2,
            List<OrangeSegResolved> output,
            int depth)
        {
            if (depth > 16) return;
            if (x2 <= x1 + 1e-6) return;
            if (IsOrangeSegDeleted(owner, rowIndex, baseX1, baseX2, y)) return;

            var key = new OrangeSegKey(rowIndex, baseX1, baseX2, y);
            if (_orangeSegEqualCutPoints.TryGetValue(owner, out var ownerCuts)
                && ownerCuts != null
                && ownerCuts.TryGetValue(key, out var cuts)
                && cuts != null
                && cuts.Count > 0)
            {
                var validCuts = cuts
                    .Where(c => c > x1 + 1e-6 && c < x2 - 1e-6)
                    .Distinct()
                    .OrderBy(c => c)
                    .ToList();

                if (validCuts.Count > 0)
                {
                    double cur = x1;
                    foreach (var cp in validCuts)
                    {
                        var childKey = new OrangeSegKey(rowIndex, cur, cp, y);
                        var (childX1, childX2) = GetOrangeSegOverride(owner, childKey, cur, cp);
                        ExpandOrangeSegByEqualCuts(owner, rowIndex, y, cur, cp, childX1, childX2, output, depth + 1);
                        cur = cp;
                    }

                    if (x2 > cur + 1e-6)
                    {
                        var childKey = new OrangeSegKey(rowIndex, cur, x2, y);
                        var (childX1, childX2) = GetOrangeSegOverride(owner, childKey, cur, x2);
                        ExpandOrangeSegByEqualCuts(owner, rowIndex, y, cur, x2, childX1, childX2, output, depth + 1);
                    }
                    return;
                }
            }

            output.Add(new OrangeSegResolved
            {
                BaseX1 = baseX1,
                BaseX2 = baseX2,
                X1 = x1,
                X2 = x2,
                IsEqualCutChild = depth > 0
            });
        }

        private bool ApplyEqualCutToOrangeSegment(GridBotsecozu owner, OrangeDimTextKey dimKey, int segmentCount, bool distributeRemainderFromLeft = false)
        {
            if (owner == null || segmentCount < 2) return false;
            if (!TryGetSegKeyForDimKey(owner, dimKey, out var segKey)) return false;

            double baseX1 = segKey.X1_10 / 10.0;
            double baseX2 = segKey.X2_10 / 10.0;
            double y = segKey.Y_10 / 10.0;
            var (x1, x2) = GetOrangeSegOverride(owner, segKey, baseX1, baseX2);
            double length = x2 - x1;
            if (length <= 1e-6) return false;

            int totalLengthInt = (int)Math.Round(length, MidpointRounding.AwayFromZero);
            if (totalLengthInt < segmentCount) return false;

            bool hasLeftAnka = TryGetAnkaOverrideBySegKey(owner, segKey, AnkaSide.Left, out var leftAnkaSigned);
            bool hasRightAnka = TryGetAnkaOverrideBySegKey(owner, segKey, AnkaSide.Right, out var rightAnkaSigned);

            if (TryGetOrangeSegInfo(owner, segKey, out var segInfo))
            {
                if (!hasLeftAnka && segInfo.HitLeftAnka)
                {
                    hasLeftAnka = Math.Abs(segInfo.DefaultLeftAnkaSigned) > 0.0001;
                    leftAnkaSigned = segInfo.DefaultLeftAnkaSigned;
                }
                if (!hasRightAnka && segInfo.HitRightAnka)
                {
                    hasRightAnka = Math.Abs(segInfo.DefaultRightAnkaSigned) > 0.0001;
                    rightAnkaSigned = segInfo.DefaultRightAnkaSigned;
                }
            }

            var segmentLengths = new List<int>(segmentCount);
            int baseSegmentLength = totalLengthInt / segmentCount;
            int remainder = totalLengthInt % segmentCount;

            for (int i = 0; i < segmentCount; i++)
                segmentLengths.Add(baseSegmentLength);

            for (int i = 0; i < remainder; i++)
            {
                int targetIndex = distributeRemainderFromLeft
                    ? i
                    : (segmentCount - remainder + i);
                segmentLengths[targetIndex] += 1;
            }

            var cuts = new List<double>(segmentCount - 1);
            double currentX = x1;

            for (int i = 0; i < segmentCount - 1; i++)
            {
                currentX += segmentLengths[i];
                cuts.Add(currentX);
            }

            if (!_orangeSegEqualCutPoints.TryGetValue(owner, out var ownerCuts) || ownerCuts == null)
            {
                ownerCuts = new Dictionary<OrangeSegKey, List<double>>();
                _orangeSegEqualCutPoints[owner] = ownerCuts;
            }
            ownerCuts[segKey] = cuts;
            PersistOrangeSegEqualCutPoints(owner, segKey, cuts);

            RebuildOrangeCutMarkersForRow(owner, segKey.RowIndex, y);

            if (cuts.Count > 0)
            {
                double firstRight = cuts[0];
                double lastLeft = cuts[cuts.Count - 1];

                var firstChild = new OrangeSegKey(segKey.RowIndex, x1, firstRight, y);
                var lastChild = new OrangeSegKey(segKey.RowIndex, lastLeft, x2, y);

                if (hasLeftAnka)
                {
                    SetAnkaOverrideBySegKey(owner, firstChild, AnkaSide.Left, leftAnkaSigned);
                    SetAnkaOverrideBySegKey(owner, segKey, AnkaSide.Left, 0.0);
                }

                if (hasRightAnka)
                {
                    SetAnkaOverrideBySegKey(owner, lastChild, AnkaSide.Right, rightAnkaSigned);
                    SetAnkaOverrideBySegKey(owner, segKey, AnkaSide.Right, 0.0);
                }
            }

            return true;
        }

        private bool ApplyCustomCutToOrangeSegment(
            GridBotsecozu owner,
            OrangeDimTextKey dimKey,
            List<double> userLengths,
            bool applyFromLeft)
        {
            if (owner == null || userLengths == null || userLengths.Count < 2) return false;
            if (!TryGetSegKeyForDimKey(owner, dimKey, out var segKey)) return false;

            double baseX1 = segKey.X1_10 / 10.0;
            double baseX2 = segKey.X2_10 / 10.0;
            double y = segKey.Y_10 / 10.0;
            var (x1, x2) = GetOrangeSegOverride(owner, segKey, baseX1, baseX2);
            double totalLen = x2 - x1;
            if (totalLen <= 1e-6) return false;

            double sum = 0.0;
            for (int i = 0; i < userLengths.Count; i++)
            {
                if (!(userLengths[i] > 1e-6)) return false;
                sum += userLengths[i];
            }
            if (Math.Abs(sum - totalLen) > 1.0) return false;

            bool hasLeftAnka = TryGetAnkaOverrideBySegKey(owner, segKey, AnkaSide.Left, out var leftAnkaSigned);
            bool hasRightAnka = TryGetAnkaOverrideBySegKey(owner, segKey, AnkaSide.Right, out var rightAnkaSigned);

            if (TryGetOrangeSegInfo(owner, segKey, out var segInfo))
            {
                if (!hasLeftAnka && segInfo.HitLeftAnka)
                {
                    hasLeftAnka = Math.Abs(segInfo.DefaultLeftAnkaSigned) > 0.0001;
                    leftAnkaSigned = segInfo.DefaultLeftAnkaSigned;
                }
                if (!hasRightAnka && segInfo.HitRightAnka)
                {
                    hasRightAnka = Math.Abs(segInfo.DefaultRightAnkaSigned) > 0.0001;
                    rightAnkaSigned = segInfo.DefaultRightAnkaSigned;
                }
            }

            var cuts = new List<double>(Math.Max(0, userLengths.Count - 1));
            if (applyFromLeft)
            {
                double cur = x1;
                for (int i = 0; i < userLengths.Count - 1; i++)
                {
                    cur += userLengths[i];
                    if (cur > x1 + 1e-6 && cur < x2 - 1e-6)
                        cuts.Add(cur);
                }
            }
            else
            {
                double cur = x2;
                for (int i = 0; i < userLengths.Count - 1; i++)
                {
                    cur -= userLengths[i];
                    if (cur > x1 + 1e-6 && cur < x2 - 1e-6)
                        cuts.Add(cur);
                }
                cuts.Sort();
            }

            if (!_orangeSegEqualCutPoints.TryGetValue(owner, out var ownerCuts) || ownerCuts == null)
            {
                ownerCuts = new Dictionary<OrangeSegKey, List<double>>();
                _orangeSegEqualCutPoints[owner] = ownerCuts;
            }
            ownerCuts[segKey] = cuts;
            PersistOrangeSegEqualCutPoints(owner, segKey, cuts);

            RebuildOrangeCutMarkersForRow(owner, segKey.RowIndex, y);

            if (cuts.Count > 0)
            {
                double firstRight = cuts[0];
                double lastLeft = cuts[cuts.Count - 1];

                var firstChild = new OrangeSegKey(segKey.RowIndex, x1, firstRight, y);
                var lastChild = new OrangeSegKey(segKey.RowIndex, lastLeft, x2, y);

                if (hasLeftAnka)
                {
                    SetAnkaOverrideBySegKey(owner, firstChild, AnkaSide.Left, leftAnkaSigned);
                    SetAnkaOverrideBySegKey(owner, segKey, AnkaSide.Left, 0.0);
                }

                if (hasRightAnka)
                {
                    SetAnkaOverrideBySegKey(owner, lastChild, AnkaSide.Right, rightAnkaSigned);
                    SetAnkaOverrideBySegKey(owner, segKey, AnkaSide.Right, 0.0);
                }
            }

            return true;
        }

        private bool IsEqualCutMarker(GridBotsecozu owner, int rowIndex, double x, double y)
        {
            if (owner == null) return false;
            return _orangeSegCutMarkers.TryGetValue(owner, out var markers)
                   && markers != null
                   && markers.Contains(new OrangeCutPointKey(rowIndex, x, y));
        }

        private void RebuildOrangeCutMarkersForRow(GridBotsecozu owner, int rowIndex, double y)
        {
            if (owner == null) return;
            if (!_orangeSegToInfo.TryGetValue(owner, out var segDict) || segDict == null) return;

            var baseSegs = segDict.Keys
                .Where(k => k.RowIndex == rowIndex && k.Y_10 == (int)Math.Round(y * 10.0))
                .Select(k => (x1: k.X1_10 / 10.0, x2: k.X2_10 / 10.0))
                .Where(seg => seg.x2 > seg.x1 + 1e-6)
                .Distinct()
                .OrderBy(seg => seg.x1)
                .ToList();

            if (baseSegs.Count == 0) return;

            var visibleSegs = GetVisibleOrangeSegs(owner, rowIndex, y, baseSegs)
                .OrderBy(seg => seg.X1)
                .ToList();

            if (!_orangeSegCutMarkers.TryGetValue(owner, out var markers) || markers == null)
            {
                markers = new HashSet<OrangeCutPointKey>();
                _orangeSegCutMarkers[owner] = markers;
            }

            markers.RemoveWhere(m => m.RowIndex == rowIndex && m.Y_10 == (int)Math.Round(y * 10.0));

            for (int i = 0; i < visibleSegs.Count - 1; i++)
            {
                double boundary = 0.5 * (visibleSegs[i].X2 + visibleSegs[i + 1].X1);
                markers.Add(new OrangeCutPointKey(rowIndex, boundary, y));
            }
        }

        private bool RebuildAllOrangeCutMarkers(GridBotsecozu owner)
        {
            if (owner == null) return false;

            var before = _orangeSegCutMarkers.TryGetValue(owner, out var existingMarkers) && existingMarkers != null
                ? new HashSet<OrangeCutPointKey>(existingMarkers)
                : new HashSet<OrangeCutPointKey>();

            if (!_orangeSegToInfo.TryGetValue(owner, out var segDict) || segDict == null || segDict.Count == 0)
            {
                if (existingMarkers == null || existingMarkers.Count == 0)
                    return false;

                existingMarkers.Clear();
                return true;
            }

            if (existingMarkers == null)
            {
                existingMarkers = new HashSet<OrangeCutPointKey>();
                _orangeSegCutMarkers[owner] = existingMarkers;
            }
            else
            {
                existingMarkers.Clear();
            }

            foreach (var row in segDict.Keys
                .Select(k => new { k.RowIndex, k.Y_10 })
                .Distinct()
                .OrderBy(k => k.RowIndex)
                .ThenBy(k => k.Y_10))
            {
                RebuildOrangeCutMarkersForRow(owner, row.RowIndex, row.Y_10 / 10.0);
            }

            return !before.SetEquals(existingMarkers);
        }

        private void ResetOrangeSegmentRuntimeState(GridBotsecozu owner)
        {
            if (owner == null) return;

            // Segment geometry is rebuilt on every redraw. Only the runtime
            // registration maps are reset here; cut markers must stay alive until the
            // next rebuild finishes, otherwise the redraw pass that should render them
            // will always see an empty marker set.
            _orangeDimToSegInfo[owner] = new Dictionary<OrangeDimTextKey, OrangeSegInfo>();
            _orangeSegToInfo[owner] = new Dictionary<OrangeSegKey, OrangeSegInfo>();
        }

        private bool ApplyOrangeSegLengthDelta(GridBotsecozu owner, OrangeDimTextKey dimKey, bool isLeftMenu, bool pullLeft, double delta)
        {
            if (owner == null || delta <= 0)
                return false;

            if (!_orangeDimToSegInfo.TryGetValue(owner, out var dict) || dict == null
                || !dict.TryGetValue(dimKey, out var info))
            {
                return false;
            }

            var segKey = info.SegKey;
            double baseX1 = segKey.X1_10 / 10.0;
            double baseX2 = segKey.X2_10 / 10.0;
            var (curX1, curX2) = GetOrangeSegOverride(owner, segKey, baseX1, baseX2);

            double newX1 = curX1;
            double newX2 = curX2;
            double deltaVal = Math.Abs(delta);

            if (TryGetOrangeSegLengthDeltaLimit(owner, segKey, isLeftMenu, pullLeft, out var maxAllowed)
                && deltaVal > maxAllowed + 1e-6)
            {
                return false;
            }

            if (isLeftMenu)
            {
                newX1 += pullLeft ? -deltaVal : deltaVal;
            }
            else
            {
                newX2 += pullLeft ? -deltaVal : deltaVal;
            }

            bool adjustedNeighbor = false;
            if (isLeftMenu)
            {
                if (TryGetNeighborSegKey(owner, segKey, findLeft: true, out var leftKey))
                {
                    double leftBaseX1 = leftKey.X1_10 / 10.0;
                    double leftBaseX2 = leftKey.X2_10 / 10.0;
                    var (leftX1, leftX2) = GetOrangeSegOverride(owner, leftKey, leftBaseX1, leftBaseX2);
                    double newBoundary = Math.Max(newX1, leftX1);
                    newX1 = newBoundary;
                    SetOrangeSegOverride(owner, leftKey, leftX1, newBoundary);
                    adjustedNeighbor = true;
                }
            }
            else
            {
                if (TryGetNeighborSegKey(owner, segKey, findLeft: false, out var rightKey))
                {
                    double rightBaseX1 = rightKey.X1_10 / 10.0;
                    double rightBaseX2 = rightKey.X2_10 / 10.0;
                    var (rightX1, rightX2) = GetOrangeSegOverride(owner, rightKey, rightBaseX1, rightBaseX2);
                    double newBoundary = Math.Min(newX2, rightX2);
                    newX2 = newBoundary;
                    SetOrangeSegOverride(owner, rightKey, newBoundary, rightX2);
                    adjustedNeighbor = true;
                }
            }

            if (!adjustedNeighbor && newX2 < newX1)
                newX2 = newX1;

            bool changed = SetOrangeSegOverride(owner, segKey, newX1, newX2);
            if (changed)
                RebuildOrangeCutMarkersForRow(owner, segKey.RowIndex, segKey.Y_10 / 10.0);
            return changed;
        }

        private bool TryGetOrangeSegLengthDeltaLimit(
            GridBotsecozu owner,
            OrangeDimTextKey dimKey,
            bool isLeftMenu,
            bool pullLeft,
            out double maxAllowed)
        {
            maxAllowed = 0.0;
            if (owner == null) return false;

            if (!_orangeDimToSegInfo.TryGetValue(owner, out var dict) || dict == null
                || !dict.TryGetValue(dimKey, out var info))
            {
                return false;
            }

            return TryGetOrangeSegLengthDeltaLimit(owner, info.SegKey, isLeftMenu, pullLeft, out maxAllowed);
        }

        private bool TryGetOrangeSegLengthDeltaLimit(
            GridBotsecozu owner,
            OrangeSegKey segKey,
            bool isLeftMenu,
            bool pullLeft,
            out double maxAllowed)
        {
            maxAllowed = 0.0;
            if (owner == null) return false;

            double baseX1 = segKey.X1_10 / 10.0;
            double baseX2 = segKey.X2_10 / 10.0;
            var (curX1, curX2) = GetOrangeSegOverride(owner, segKey, baseX1, baseX2);
            double curLength = Math.Max(0.0, curX2 - curX1);

            if (isLeftMenu)
            {
                if (pullLeft)
                {
                    if (!TryGetNeighborSegKey(owner, segKey, findLeft: true, out var leftKey))
                        return false;

                    double leftBaseX1 = leftKey.X1_10 / 10.0;
                    double leftBaseX2 = leftKey.X2_10 / 10.0;
                    var (leftX1, _) = GetOrangeSegOverride(owner, leftKey, leftBaseX1, leftBaseX2);
                    maxAllowed = Math.Max(0.0, curX1 - leftX1);
                    return true;
                }

                maxAllowed = curLength;
                return true;
            }

            if (pullLeft)
            {
                maxAllowed = curLength;
                return true;
            }

            if (!TryGetNeighborSegKey(owner, segKey, findLeft: false, out var rightKey))
                return false;

            double rightBaseX1 = rightKey.X1_10 / 10.0;
            double rightBaseX2 = rightKey.X2_10 / 10.0;
            var (_, rightX2) = GetOrangeSegOverride(owner, rightKey, rightBaseX1, rightBaseX2);
            maxAllowed = Math.Max(0.0, rightX2 - curX2);
            return true;
        }


        private void RegisterOrangeSegInfo(GridBotsecozu owner, OrangeDimTextKey topKey, OrangeSegInfo info)
        {
            if (owner == null) return;

            if (!_orangeDimToSegInfo.TryGetValue(owner, out var dict) || dict == null)
            {
                dict = new Dictionary<OrangeDimTextKey, OrangeSegInfo>();
                _orangeDimToSegInfo[owner] = dict;
            }

            dict[topKey] = info;
            if (info.HasBottomKey)
            {
                dict[info.BottomKey] = info;
            }

            if (!_orangeSegToInfo.TryGetValue(owner, out var segDict) || segDict == null)
            {
                segDict = new Dictionary<OrangeSegKey, OrangeSegInfo>();
                _orangeSegToInfo[owner] = segDict;
            }
            segDict[info.SegKey] = info;
        }

        private bool TryGetSegKeyForDimKey(GridBotsecozu owner, OrangeDimTextKey dimKey, out OrangeSegKey segKey)
        {
            segKey = default;
            if (owner == null) return false;

            if (_orangeDimToSegInfo.TryGetValue(owner, out var dict) && dict != null
                && dict.TryGetValue(dimKey, out var info))
            {
                segKey = info.SegKey;
                return true;
            }

            return false;
        }

        private bool TryGetOrangeSegInfo(GridBotsecozu owner, OrangeSegKey segKey, out OrangeSegInfo info)
        {
            info = default;
            if (owner == null) return false;
            return _orangeSegToInfo.TryGetValue(owner, out var segDict)
                   && segDict != null
                   && segDict.TryGetValue(segKey, out info);
        }

        private bool TryGetNeighborSegKey(GridBotsecozu owner, OrangeSegKey segKey, bool findLeft, out OrangeSegKey neighborKey)
        {
            neighborKey = default;
            if (owner == null) return false;
            if (!_orangeSegToInfo.TryGetValue(owner, out var segDict) || segDict == null)
                return false;

            int targetX = findLeft ? segKey.X1_10 : segKey.X2_10;
            foreach (var entry in segDict.Keys)
            {
                if (entry.RowIndex != segKey.RowIndex || entry.Y_10 != segKey.Y_10) continue;
                if (findLeft)
                {
                    if (entry.X2_10 == targetX)
                    {
                        neighborKey = entry;
                        return true;
                    }
                }
                else
                {
                    if (entry.X1_10 == targetX)
                    {
                        neighborKey = entry;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool TryGetSegLength(GridBotsecozu owner, OrangeSegKey segKey, out double length)
        {
            length = 0.0;
            if (owner == null) return false;
            double baseX1 = segKey.X1_10 / 10.0;
            double baseX2 = segKey.X2_10 / 10.0;
            var (x1, x2) = GetOrangeSegOverride(owner, segKey, baseX1, baseX2);
            length = Math.Max(0, x2 - x1);
            return true;
        }

        private bool ApplyOrangeSegTotalLength(GridBotsecozu owner, OrangeSegKey segKey, bool anchorLeft, double newLength)
        {
            if (owner == null) return false;
            double baseX1 = segKey.X1_10 / 10.0;
            double baseX2 = segKey.X2_10 / 10.0;
            var (x1, x2) = GetOrangeSegOverride(owner, segKey, baseX1, baseX2);

            double length = Math.Max(0, newLength);
            double newX1 = x1;
            double newX2 = x2;

            if (anchorLeft)
            {
                newX2 = x1 + length;
                if (TryGetNeighborSegKey(owner, segKey, findLeft: false, out var rightKey))
                {
                    double rightBaseX1 = rightKey.X1_10 / 10.0;
                    double rightBaseX2 = rightKey.X2_10 / 10.0;
                    var (rightX1, rightX2) = GetOrangeSegOverride(owner, rightKey, rightBaseX1, rightBaseX2);
                    double boundary = Math.Min(newX2, rightX2);
                    newX2 = boundary;
                    SetOrangeSegOverride(owner, rightKey, boundary, rightX2);
                }
                if (newX2 < x1) newX2 = x1;
            }
            else
            {
                newX1 = x2 - length;
                if (TryGetNeighborSegKey(owner, segKey, findLeft: true, out var leftKey))
                {
                    double leftBaseX1 = leftKey.X1_10 / 10.0;
                    double leftBaseX2 = leftKey.X2_10 / 10.0;
                    var (leftX1, leftX2) = GetOrangeSegOverride(owner, leftKey, leftBaseX1, leftBaseX2);
                    double boundary = Math.Max(newX1, leftX1);
                    newX1 = boundary;
                    SetOrangeSegOverride(owner, leftKey, leftX1, boundary);
                }
                if (newX1 > x2) newX1 = x2;
            }

            bool changed = SetOrangeSegOverride(owner, segKey, newX1, newX2);
            if (changed)
                RebuildOrangeCutMarkersForRow(owner, segKey.RowIndex, segKey.Y_10 / 10.0);
            return changed;
        }

        private void ResolveAnkaDefaults(
            GridBotsecozu owner,
            OrangeSegKey segKey,
            double segX1,
            double segX2,
            double leftAnkaX,
            double rightAnkaX,
            bool hasLeftAnka,
            bool hasRightAnka,
            double fallbackLeftSigned,
            double fallbackRightSigned,
            out bool hitLeft,
            out bool hitRight,
            out double defaultLeftSigned,
            out double defaultRightSigned)
        {
            hitLeft = false;
            hitRight = false;
            defaultLeftSigned = 0.0;
            defaultRightSigned = 0.0;

            // NOTE:
            // segX1/segX2 có thể đã bị dịch nhẹ (rounding / tonari shift) sau khi 等分切断,
            // nhưng baseX1/baseX2 (segKey) cũng có thể đã là tọa độ sau dịch trong một số luồng merge.
            // Vì vậy cần chấp nhận trúng biên theo CẢ base và seg thực tế để tránh mất ANKA biên.
            double baseX1 = segKey.X1_10 / 10.0;
            double baseX2 = segKey.X2_10 / 10.0;
            bool nearLeftBase = !double.IsNaN(baseX1) && !double.IsInfinity(baseX1) && Near(baseX1, leftAnkaX, 0.5);
            bool nearRightBase = !double.IsNaN(baseX2) && !double.IsInfinity(baseX2) && Near(baseX2, rightAnkaX, 0.5);
            bool nearLeftSeg = !double.IsNaN(segX1) && !double.IsInfinity(segX1) && Near(segX1, leftAnkaX, 0.5);
            bool nearRightSeg = !double.IsNaN(segX2) && !double.IsInfinity(segX2) && Near(segX2, rightAnkaX, 0.5);

            hitLeft = hasLeftAnka && (nearLeftBase || nearLeftSeg);
            hitRight = hasRightAnka && (nearRightBase || nearRightSeg);
            defaultLeftSigned = hitLeft ? fallbackLeftSigned : 0.0;
            defaultRightSigned = hitRight ? fallbackRightSigned : 0.0;
        }

        private bool IsOrangeSegDeleted(GridBotsecozu owner, int rowIndex, double x1, double x2, double y)
        {
            if (owner == null) return false;

            if (_deletedOrangeSegs.TryGetValue(owner, out var set) && set != null)
            {
                return set.Contains(new OrangeSegKey(rowIndex, x1, x2, y));
            }
            return false;
        }

        private bool DeleteOrangeDimSegment(GridBotsecozu owner, OrangeDimTextKey clickedKey)
        {
            if (owner == null) return false;

            bool changed = false;

            // 1) clear text override của DIM đã click
            changed |= SetOrangeDimText(owner, clickedKey, "");

            // 2) tìm segment info để xoá line + anka + bottom
            if (_orangeDimToSegInfo.TryGetValue(owner, out var dict) && dict != null
                && dict.TryGetValue(clickedKey, out var info))
            {
                // 2a) mark segment deleted
                if (!_deletedOrangeSegs.TryGetValue(owner, out var set) || set == null)
                {
                    set = new HashSet<OrangeSegKey>();
                    _deletedOrangeSegs[owner] = set;
                }
                if (set.Add(info.SegKey)) changed = true;
                changed = PersistDeletedOrangeSeg(owner, info.SegKey, true) || changed;

                // 2a-2) Rebuild cut markers for the row so deleted-segment dots are fully synchronized
                RebuildOrangeCutMarkersForRow(owner, info.SegKey.RowIndex, info.SegKey.Y_10 / 10.0);

                // 2b) clear TOP/BOTTOM override (nếu có)
                if (!clickedKey.Equals(info.TopKey))
                    changed |= SetOrangeDimText(owner, info.TopKey, "");
                if (info.HasBottomKey && !clickedKey.Equals(info.BottomKey))
                    changed |= SetOrangeDimText(owner, info.BottomKey, "");

            }

            return changed;
        }
        //hình minh họa anka
        private FrameworkElement CreateAnkaPreviewCanvas(int verticalOffset, AnkaSide side)
        {
            const double previewWidth = 96;
            const double logicalPreviewWidth = 60;
            const double dimGap = 6;

            // chỉnh riêng vị trí hình minh họa trái / phải
            const double leftShapeOffsetX = -5;
            const double rightShapeOffsetX = -10;

            // chỉnh riêng vị trí DIM đỏ trái / phải
            const double leftDimOffsetX = 5;
            const double rightDimOffsetX = -5;
            const double leftDimOffsetY = 0;
            const double rightDimOffsetY = 0;

            var canvas = new Canvas
            {
                Width = previewWidth,
                Height = 20,
                Background = Brushes.Transparent,
                SnapsToDevicePixels = true
            };

            bool isUp = (verticalOffset == -10);
            bool isLeft = side == AnkaSide.Left;

            double shapeOffsetX = (canvas.Width - logicalPreviewWidth) / 2.0
                                + (isLeft ? leftShapeOffsetX : rightShapeOffsetX);

            // =========================
            // CONFIG RIÊNG CHO UP / DOWN
            // =========================
            double hookX;
            double r;
            double yH;
            double yVEnd;
            double horizontalStartX;
            double horizontalEndX;

            // Ký hiệu kích thước "|-|"
            double dimX;
            double dimTop;
            double dimBottom;
            double capLen;

            if (isUp)
            {
                hookX = (isLeft ? 5 : 55) + shapeOffsetX;
                r = 4.0;

                yH = 17;
                yVEnd = -2;

                dimX = isLeft
                    ? (shapeOffsetX - dimGap + leftDimOffsetX)
                    : (shapeOffsetX + logicalPreviewWidth + dimGap + rightDimOffsetX);

                dimTop = -2 + (isLeft ? leftDimOffsetY : rightDimOffsetY);
                dimBottom = 17 + (isLeft ? leftDimOffsetY : rightDimOffsetY);

                capLen = 6;
            }
            else
            {
                hookX = (isLeft ? 5 : 55) + shapeOffsetX;
                r = 4.0;

                yH = 1;
                yVEnd = 19;

                dimX = isLeft
                    ? (shapeOffsetX - dimGap + leftDimOffsetX)
                    : (shapeOffsetX + logicalPreviewWidth + dimGap + rightDimOffsetX);

                dimTop = 1 + (isLeft ? leftDimOffsetY : rightDimOffsetY);
                dimBottom = 19 + (isLeft ? leftDimOffsetY : rightDimOffsetY);

                capLen = 6;
            }

            // ==========================================================
            // 1) VẼ ANKA
            // ==========================================================
            horizontalStartX = (isLeft ? 56 : 4) + shapeOffsetX;
            horizontalEndX = isLeft ? (hookX + r) : (hookX - r);

            Point pHStart = new Point(horizontalStartX, yH);
            Point pHToArc = new Point(horizontalEndX, yH);
            Point pArcToV = isUp ? new Point(hookX, yH - r) : new Point(hookX, yH + r);
            Point pVEnd = new Point(hookX, yVEnd);

            var fig = new PathFigure
            {
                StartPoint = pHStart,
                IsClosed = false,
                IsFilled = false
            };

            fig.Segments.Add(new LineSegment(pHToArc, true));

            fig.Segments.Add(new ArcSegment
            {
                Point = pArcToV,
                Size = new Size(r, r),
                RotationAngle = 0,
                IsLargeArc = false,
                SweepDirection = isUp
                    ? (isLeft ? SweepDirection.Clockwise : SweepDirection.Counterclockwise)
                    : (isLeft ? SweepDirection.Counterclockwise : SweepDirection.Clockwise)
            });

            fig.Segments.Add(new LineSegment(pVEnd, true));

            var path = new Path
            {
                Data = new PathGeometry(new[] { fig }),
                Stroke = Brushes.Black,
                StrokeThickness = 1.5,
                SnapsToDevicePixels = true,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round,

            };

            RenderOptions.SetEdgeMode(path, EdgeMode.Unspecified);
            canvas.Children.Add(path);

            // ==========================================================
            // 2) VẼ DIM "|-|"
            // ==========================================================
            double px = 0.5;
            double x0 = dimX + px;
            double y0 = dimTop + px;
            double y1 = dimBottom + px;
            double xL = (dimX - capLen / 2) + px;
            double xR = (dimX + capLen / 2) + px;

            Line MakeRedLine(double x1_, double y1_, double x2_, double y2_)
            {
                var ln = new Line
                {
                    X1 = x1_,
                    Y1 = y1_,
                    X2 = x2_,
                    Y2 = y2_,
                    Stroke = Brushes.Red,
                    StrokeThickness = 1,
                    SnapsToDevicePixels = true
                };
                RenderOptions.SetEdgeMode(ln, EdgeMode.Aliased);
                return ln;
            }

            canvas.Children.Add(MakeRedLine(x0 - 1, y0 - 1, x0 - 1, y1 - 1));
            canvas.Children.Add(MakeRedLine(xL - 1, y0 - 1, xR - 1, y0 - 1));
            canvas.Children.Add(MakeRedLine(xL - 1, y1 - 1, xR - 1, y1 - 1));

            return canvas;
        }
        private FrameworkElement CreateLengthPreviewCanvas(bool pullLeft)
        {
            var canvas = new Canvas
            {
                Width = 60,
                Height = 20,
                Background = Brushes.Transparent
            };

            double centerY = 10;
            double startX = pullLeft ? 45 : 15;
            double endX = pullLeft ? 10 : 50;

            canvas.Children.Add(new Line
            {
                X1 = startX,
                Y1 = centerY,
                X2 = endX,
                Y2 = centerY,
                Stroke = Brushes.Black,
                StrokeThickness = 1.5,
                SnapsToDevicePixels = true
            });

            var head = new Polygon
            {
                Fill = Brushes.Black,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };

            if (pullLeft)
            {
                head.Points = new PointCollection
                {
                    new Point(endX, centerY),
                    new Point(endX + 6, centerY - 4),
                    new Point(endX + 6, centerY + 4)
                };
            }
            else
            {
                head.Points = new PointCollection
                {
                    new Point(endX, centerY),
                    new Point(endX - 6, centerY - 4),
                    new Point(endX - 6, centerY + 4)
                };
            }

            canvas.Children.Add(head);
            return canvas;
        }

        private void MakeOrangeDimTextEditable(TextBlock tb, Canvas canvas, WCTransform T,
                                               double wx, double wy,
                                               GridBotsecozu owner,
                                               OrangeDimTextKey key)
        {
            if (tb == null || canvas == null)
                return;

            // ===== BLOCK BOTTOM (text đỏ trong ngoặc) =====
            if (!key.IsTop)
            {
                tb.Cursor = Cursors.Arrow;

                if (tb.Background == null)
                    tb.Background = Brushes.Transparent;

                tb.IsHitTestVisible = false;
                return;
            }

            tb.Cursor = Cursors.Hand;
            if (tb.Background == null)
                tb.Background = Brushes.Transparent;
            tb.IsHitTestVisible = true;
            Panel.SetZIndex(tb, 1500);

            bool IsDigitOnlyText(string text, bool allowEmpty = true)
            {
                if (string.IsNullOrEmpty(text))
                    return allowEmpty;

                return text.All(char.IsDigit);
            }

            string BuildCandidateText(TextBox box, string incomingText)
            {
                if (box == null)
                    return incomingText ?? string.Empty;

                string currentText = box.Text ?? string.Empty;
                int selectionStart = Math.Max(0, Math.Min(box.SelectionStart, currentText.Length));
                int selectionLength = Math.Max(0, Math.Min(box.SelectionLength, currentText.Length - selectionStart));

                return currentText.Remove(selectionStart, selectionLength)
                    .Insert(selectionStart, incomingText ?? string.Empty);
            }

            ControlTemplate _dimNumericTextBoxTemplate = null;
            ControlTemplate GetDimNumericTextBoxTemplate()
            {
                if (_dimNumericTextBoxTemplate != null)
                    return _dimNumericTextBoxTemplate;

                var border = new FrameworkElementFactory(typeof(Border));
                border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
                border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
                border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
                border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
                border.SetValue(Border.SnapsToDevicePixelsProperty, true);

                var contentHost = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
                contentHost.SetValue(FrameworkElement.SnapsToDevicePixelsProperty, true);
                border.AppendChild(contentHost);

                _dimNumericTextBoxTemplate = new ControlTemplate(typeof(TextBox))
                {
                    VisualTree = border
                };
                return _dimNumericTextBoxTemplate;
            }

            void ApplyDimNumericTextBoxChrome(TextBox box)
            {
                if (box == null)
                    return;

                if (box.ReadLocalValue(Control.BorderBrushProperty) == DependencyProperty.UnsetValue)
                    box.BorderBrush = PopupMenuEditorBorderBrush;

                if (box.ReadLocalValue(Control.BorderThicknessProperty) == DependencyProperty.UnsetValue)
                    box.BorderThickness = new Thickness(1);

                if (box.ReadLocalValue(Control.BackgroundProperty) == DependencyProperty.UnsetValue)
                    box.Background = Brushes.White;

                box.FocusVisualStyle = null;
                box.Template = GetDimNumericTextBoxTemplate();
            }

            void AttachDimIntegerValidation(
                TextBox box,
                Func<Brush> getValidBackground = null,
                Func<Brush> getValidBorderBrush = null,
                Func<Thickness> getValidBorderThickness = null)
            {
                if (box == null)
                    return;

                ApplyDimNumericTextBoxChrome(box);

                Brush initialBackground = box.Background ?? Brushes.White;
                Brush initialBorderBrush = box.BorderBrush ?? Brushes.Black;
                Thickness initialBorderThickness = box.BorderThickness;

                Brush ResolveValidBackground()
                    => getValidBackground?.Invoke() ?? initialBackground;

                Brush ResolveValidBorderBrush()
                    => getValidBorderBrush?.Invoke() ?? initialBorderBrush;

                Thickness ResolveValidBorderThickness()
                    => getValidBorderThickness?.Invoke() ?? initialBorderThickness;

                Thickness EnsureVisibleBorder(Thickness thickness)
                {
                    if (thickness.Left <= 0 && thickness.Top <= 0 && thickness.Right <= 0 && thickness.Bottom <= 0)
                        return new Thickness(1);

                    return thickness;
                }

                bool hasRejectedInput = false;

                void SetValidityState(bool isValid)
                {
                    box.Background = ResolveValidBackground();
                    box.BorderBrush = isValid ? ResolveValidBorderBrush() : Brushes.Red;
                    box.BorderThickness = isValid
                        ? ResolveValidBorderThickness()
                        : EnsureVisibleBorder(ResolveValidBorderThickness());
                }

                void ShowRejectedInputState()
                {
                    hasRejectedInput = true;

                    // Apply after the input event so the template does not immediately paint over the border.
                    box.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (hasRejectedInput)
                            SetValidityState(false);
                    }), DispatcherPriority.Input);
                }

                void ClearRejectedInputState()
                {
                    hasRejectedInput = false;
                    SetValidityState(IsDigitOnlyText(box.Text));
                }

                box.TextChanged += (_, __) =>
                {
                    hasRejectedInput = false;
                    SetValidityState(IsDigitOnlyText(box.Text));
                };

                box.PreviewTextInput += (_, e) =>
                {
                    string candidate = BuildCandidateText(box, e.Text);
                    bool isValid = IsDigitOnlyText(candidate, allowEmpty: true);
                    if (!isValid)
                    {
                        ShowRejectedInputState();
                        e.Handled = true;
                        return;
                    }

                    ClearRejectedInputState();
                };

                DataObject.AddPastingHandler(box, new DataObjectPastingEventHandler((_, e) =>
                {
                    string pastedText = null;

                    if (e.SourceDataObject != null)
                    {
                        if (e.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
                            pastedText = e.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
                        else if (e.SourceDataObject.GetDataPresent(DataFormats.Text))
                            pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string;
                    }

                    string candidate = BuildCandidateText(box, pastedText ?? string.Empty);
                    bool isValid = IsDigitOnlyText(candidate, allowEmpty: true);
                    if (!isValid)
                    {
                        ShowRejectedInputState();
                        e.CancelCommand();
                        return;
                    }

                    ClearRejectedInputState();
                }));

                box.LostKeyboardFocus += (_, __) =>
                {
                    hasRejectedInput = false;
                    SetValidityState(IsDigitOnlyText(box.Text));
                };

                SetValidityState(IsDigitOnlyText(box.Text));
            }

            bool TrySplitTopParts(string text, out string dPart, out string lenPart)
            {
                dPart = ""; lenPart = "";
                if (string.IsNullOrWhiteSpace(text)) return false;

                string t = text.Trim();
                if (t.StartsWith("D") || t.StartsWith("Ｄ")) t = t.Substring(1);

                int idx = t.IndexOfAny(new[] { '-', '－', '–', '—' });
                if (idx < 0) return false;

                dPart = t.Substring(0, idx).Trim();
                lenPart = t.Substring(idx + 1).Trim();
                return true;
            }

            bool TryParseNumber(string s, out double v)
            {
                s = (s ?? "").Trim();

                if (double.TryParse(s, NumberStyles.Float, CultureInfo.CurrentCulture, out v))
                    return true;

                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                    return true;

                s = s.Replace(',', '.');
                return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v);
            }

            void CloseExistingPopups()
            {
                if (tb.Tag is Tuple<System.Windows.Controls.Primitives.Popup,
                                    System.Windows.Controls.Primitives.Popup,
                                    System.Windows.Controls.Primitives.Popup,
                                    System.Windows.Controls.Primitives.Popup,
                                    System.Windows.Controls.Primitives.Popup> tuple)
                {
                    try { tuple.Item5.IsOpen = false; } catch { }
                    try { tuple.Item4.IsOpen = false; } catch { }
                    try { tuple.Item3.IsOpen = false; } catch { }
                    try { tuple.Item2.IsOpen = false; } catch { }
                    try { tuple.Item1.IsOpen = false; } catch { }
                }
                tb.Tag = null;
            }

            // ===== Helper: inline TextBox editor (trên canvas) =====
            void BeginInlineEdit(string initialText, Action<string> commit)
            {
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var sz = tb.DesiredSize;

                double left = Canvas.GetLeft(tb);
                double top = Canvas.GetTop(tb);
                if (double.IsNaN(left) || double.IsNaN(top))
                {
                    var p = T.P(wx, wy);
                    if (double.IsNaN(left)) left = p.X;
                    if (double.IsNaN(top)) top = p.Y;
                }

                string original = (initialText ?? string.Empty).Trim();

                var editor = new TextBox
                {
                    Text = original,
                    FontSize = tb.FontSize,
                    Width = Math.Max(80, sz.Width + 40),
                    Height = Math.Max(26, sz.Height + 10),
                    Background = Brushes.White
                };
                AttachDimIntegerValidation(editor);

                Canvas.SetLeft(editor, left);
                Canvas.SetTop(editor, top);
                Panel.SetZIndex(editor, 3000);

                tb.Visibility = System.Windows.Visibility.Hidden;
                canvas.Children.Add(editor);

                editor.Focus();
                editor.SelectAll();

                void FocusBack()
                {
                    editor.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        editor.Focus();
                        editor.SelectAll();
                    }), DispatcherPriority.Input);
                }

                void FinishRemove()
                {
                    canvas.Children.Remove(editor);
                    tb.Visibility = System.Windows.Visibility.Hidden;
                }

                void TryCommitOrRefocus()
                {
                    string now = (editor.Text ?? string.Empty).Trim();

                    if (string.Equals(now, original, StringComparison.Ordinal))
                    {
                        FinishRemove();
                        return;
                    }

                    if (!TryParseNumber(now, out var _))
                    {
                        FocusBack();
                        return;
                    }

                    FinishRemove();
                    commit?.Invoke(now);
                }

                editor.KeyDown += (ks, ke) =>
                {
                    if (ke.Key == Key.Enter) { TryCommitOrRefocus(); ke.Handled = true; }
                    else if (ke.Key == Key.Escape) { FinishRemove(); ke.Handled = true; }
                };

                editor.LostKeyboardFocus += (_, __) =>
                {
                    TryCommitOrRefocus();
                };
            }

            if (tb.Foreground == null)
                tb.Foreground = dimBaseFg;

            EnableEditableHoverWithBox(tb, canvas,
                hoverForeground: dimBaseFg,
                hoverFill: new SolidColorBrush(Color.FromArgb(100, 30, 144, 255)),
                hoverStroke: Brushes.Blue);

            // Hover chỉ để highlight, không mở popup.
            tb.MouseEnter += (s, e) =>
            {
                // Nếu còn popup cũ của DIM này, đóng lại để tránh cảm giác "hover là mở menu".
                if (Mouse.LeftButton != MouseButtonState.Pressed)
                    CloseExistingPopups();
            };

            // ===== On click =====
            tb.MouseLeftButtonUp += (s, e) =>
            {
                CloseExistingPopups();

                double GetCurrentFor(AnkaSide side, bool wantTop)
                {
                    if (owner == null
                        || !_orangeDimToSegInfo.TryGetValue(owner, out var dict)
                        || dict == null
                        || !dict.TryGetValue(key, out var info))
                    {
                        return 0.0;
                    }

                    double defSigned = (side == AnkaSide.Left)
                        ? info.DefaultLeftAnkaSigned
                        : info.DefaultRightAnkaSigned;
                    double curSigned = GetAnkaOverride(owner, key, side, defSigned);

                    if (wantTop) return (curSigned > 0) ? Math.Abs(curSigned) : 0.0;
                    return (curSigned < 0) ? Math.Abs(curSigned) : 0.0;
                }

                void CommitFor(AnkaSide side, bool isTop, double newLen)
                {
                    double len = Math.Max(0, newLen);
                    double signed = isTop ? +len : -len;

                    if (owner != null
                        && _orangeDimToSegInfo.TryGetValue(owner, out var dict)
                        && dict != null
                        && dict.TryGetValue(key, out var info)
                        && SetAnkaOverride(owner, key, side, signed))
                        Redraw(canvas, owner);
                }

                Brush normalBg = Brushes.Transparent;
                Brush selectedBg = new SolidColorBrush(Color.FromRgb(210, 225, 255));
                Brush dividerBrush = PopupMenuDividerBrush;

                const double MENU1_MIN_WIDTH = 100;
                const double MENU2_MIN_WIDTH = 80;
                const double MENU3_MIN_WIDTH = 100;

                UIElement WithRowDivider(UIElement child)
                {
                    return new Border
                    {
                        BorderBrush = dividerBrush,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Child = child
                    };
                }

                double anchorX = tb.ActualWidth;
                if (anchorX <= 0)
                {
                    tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    anchorX = tb.DesiredSize.Width;
                }

                var mainPop = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = tb,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                    HorizontalOffset = Math.Max(1.0, anchorX + 1.0),
                    VerticalOffset = 0,
                    AllowsTransparency = true,
                    StaysOpen = true
                };

                System.Windows.Controls.Primitives.Popup ankaPop = null;
                System.Windows.Controls.Primitives.Popup sidePop = null;
                System.Windows.Controls.Primitives.Popup lenPop = null;
                System.Windows.Controls.Primitives.Popup lenDirPop = null;
                System.Windows.Controls.Primitives.Popup cutModePop = null;
                System.Windows.Controls.Primitives.Popup cutInputPop = null;
                System.Windows.Controls.Primitives.Popup cutDetailPop = null;
                System.Windows.Controls.Primitives.Popup dDiaPop = null;

                void CloseSubMenus()
                {
                    if (lenDirPop != null) lenDirPop.IsOpen = false;
                    if (lenPop != null) lenPop.IsOpen = false;
                    if (cutDetailPop != null) cutDetailPop.IsOpen = false;
                    if (cutInputPop != null) cutInputPop.IsOpen = false;
                    if (cutModePop != null) cutModePop.IsOpen = false;
                    if (sidePop != null) sidePop.IsOpen = false;
                    if (ankaPop != null) ankaPop.IsOpen = false;
                    if (dDiaPop != null) dDiaPop.IsOpen = false;
                }

                void CloseSubMenusExcept(params System.Windows.Controls.Primitives.Popup[] keepOpen)
                {
                    bool Keep(System.Windows.Controls.Primitives.Popup p)
                        => p != null && keepOpen != null && keepOpen.Any(k => ReferenceEquals(k, p));

                    if (!Keep(lenDirPop) && lenDirPop != null) lenDirPop.IsOpen = false;
                    if (!Keep(lenPop) && lenPop != null) lenPop.IsOpen = false;
                    if (!Keep(cutDetailPop) && cutDetailPop != null) cutDetailPop.IsOpen = false;
                    if (!Keep(cutInputPop) && cutInputPop != null) cutInputPop.IsOpen = false;
                    if (!Keep(cutModePop) && cutModePop != null) cutModePop.IsOpen = false;
                    if (!Keep(sidePop) && sidePop != null) sidePop.IsOpen = false;
                    if (!Keep(ankaPop) && ankaPop != null) ankaPop.IsOpen = false;
                    if (!Keep(dDiaPop) && dDiaPop != null) dDiaPop.IsOpen = false;
                }

                var win = Window.GetWindow(canvas);
                MouseButtonEventHandler outsideCloser = null;
                KeyEventHandler escCloser = null;

                EventHandler winDeactivatedCloser = null;
                EventHandler appDeactivatedCloser = null;

                HwndSource _hwndSrc = null;
                HwndSourceHook _hwndHook = null;
                bool _isClosing = false;
                int mainPopupOpenRequestId = 0;

                const int WM_NCLBUTTONDOWN = 0x00A1;
                const int WM_NCRBUTTONDOWN = 0x00A4;
                const int WM_NCMBUTTONDOWN = 0x00A7;

                bool IsPointInside(FrameworkElement fe, Point screenPt)
                {
                    if (fe == null || !fe.IsVisible)
                        return false;

                    try
                    {
                        var topLeft = fe.PointToScreen(new Point(0, 0));
                        var rect = new Rect(topLeft, new Size(fe.ActualWidth, fe.ActualHeight));
                        return rect.Contains(screenPt);
                    }
                    catch
                    {
                        return false;
                    }
                }

                TextBox _activeAnkaBox = null;
                Func<bool> _activeAnkaTryCommitOrRefocus = null;
                bool _suppressAnkaCommitOnce = false;

                void CancelActiveAnkaEdit()
                {
                    if (_activeAnkaBox == null)
                        return;

                    if (_activeAnkaBox.Visibility != System.Windows.Visibility.Visible)
                    {
                        _activeAnkaBox = null;
                        _activeAnkaTryCommitOrRefocus = null;
                        return;
                    }

                    _suppressAnkaCommitOnce = true;

                    try
                    {
                        if (_activeAnkaBox.Tag is Tuple<FrameworkElement, string> st && st?.Item1 != null)
                            st.Item1.Visibility = System.Windows.Visibility.Visible;
                    }
                    catch { }

                    try { _activeAnkaBox.Visibility = System.Windows.Visibility.Collapsed; } catch { }

                    _activeAnkaBox = null;
                    _activeAnkaTryCommitOrRefocus = null;
                }

                void CloseAll()
                {
                    if (_isClosing) return;
                    _isClosing = true;
                    InvalidatePendingMainPopupOpen();

                    try
                    {
                        if (lenDirPop != null) lenDirPop.IsOpen = false;
                        if (lenPop != null) lenPop.IsOpen = false;
                        if (cutDetailPop != null) cutDetailPop.IsOpen = false;
                        if (cutInputPop != null) cutInputPop.IsOpen = false;
                        if (cutModePop != null) cutModePop.IsOpen = false;
                        if (sidePop != null) sidePop.IsOpen = false;
                        if (ankaPop != null) ankaPop.IsOpen = false;
                        if (dDiaPop != null) dDiaPop.IsOpen = false;
                        if (mainPop != null) mainPop.IsOpen = false;
                    }
                    catch { }

                    try
                    {
                        if (win != null && outsideCloser != null)
                            win.RemoveHandler(UIElement.PreviewMouseDownEvent, outsideCloser);
                        if (win != null && escCloser != null)
                            win.RemoveHandler(UIElement.PreviewKeyDownEvent, escCloser);
                    }
                    catch { }

                    try
                    {
                        if (win != null && winDeactivatedCloser != null)
                            win.Deactivated -= winDeactivatedCloser;
                    }
                    catch { }

                    try
                    {
                        if (Application.Current != null && appDeactivatedCloser != null)
                            Application.Current.Deactivated -= appDeactivatedCloser;
                    }
                    catch { }

                    try
                    {
                        if (_hwndSrc != null && _hwndHook != null)
                            _hwndSrc.RemoveHook(_hwndHook);
                    }
                    catch { }

                    tb.Tag = null;
                    _isClosing = false;
                }

                IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
                {
                    if (msg == WM_NCLBUTTONDOWN || msg == WM_NCRBUTTONDOWN || msg == WM_NCMBUTTONDOWN)
                    {
                        CloseAll();
                    }
                    return IntPtr.Zero;
                }

                outsideCloser = (ss, ee) =>
                {
                    if (win == null) { CloseAll(); return; }

                    var screenPt = win.PointToScreen(ee.GetPosition(win));

                    bool inside =
                        IsPointInside(tb, screenPt)
                        || (mainPop?.Child is FrameworkElement m && IsPointInside(m, screenPt))
                        || (ankaPop?.Child is FrameworkElement a && IsPointInside(a, screenPt))
                        || (sidePop?.Child is FrameworkElement r && IsPointInside(r, screenPt))
                        || (lenPop?.Child is FrameworkElement l && IsPointInside(l, screenPt))
                        || (lenDirPop?.Child is FrameworkElement ld && IsPointInside(ld, screenPt))
                        || (cutModePop?.Child is FrameworkElement cm && IsPointInside(cm, screenPt))
                        || (cutInputPop?.Child is FrameworkElement ci && IsPointInside(ci, screenPt))
                        || (cutDetailPop?.Child is FrameworkElement cd && IsPointInside(cd, screenPt))
                        || (dDiaPop?.Child is FrameworkElement dd && IsPointInside(dd, screenPt));

                    if (!inside)
                    {
                        if (_activeAnkaBox != null
                            && _activeAnkaBox.Visibility == System.Windows.Visibility.Visible
                            && _activeAnkaTryCommitOrRefocus != null)
                        {
                            bool ok = _activeAnkaTryCommitOrRefocus();
                            if (!ok)
                                return;
                        }

                        CloseAll();
                    }
                };

                escCloser = (ss, ee) =>
                {
                    if (ee.Key == Key.Escape)
                    {
                        CloseAll();
                        ee.Handled = true;
                    }
                };

                winDeactivatedCloser = (ss, ee) => CloseAll();
                appDeactivatedCloser = (ss, ee) => CloseAll();

                if (win != null)
                {
                    win.AddHandler(UIElement.PreviewMouseDownEvent, outsideCloser, true);
                    win.AddHandler(UIElement.PreviewKeyDownEvent, escCloser, true);
                    win.Deactivated += winDeactivatedCloser;
                }

                if (Application.Current != null)
                {
                    Application.Current.Deactivated += appDeactivatedCloser;
                }

                if (win != null)
                {
                    _hwndSrc = PresentationSource.FromVisual(win) as HwndSource;
                    if (_hwndSrc != null)
                    {
                        _hwndHook = new HwndSourceHook(WndProc);
                        _hwndSrc.AddHook(_hwndHook);
                    }
                }

                tb.Tag = Tuple.Create(
                    mainPop,
                    ankaPop ?? new System.Windows.Controls.Primitives.Popup(),
                    sidePop ?? new System.Windows.Controls.Primitives.Popup(),
                    lenPop ?? new System.Windows.Controls.Primitives.Popup(),
                    lenDirPop ?? new System.Windows.Controls.Primitives.Popup());

                Border WrapBox(UIElement child)
                {
                    var host = new ContentControl
                    {
                        Content = child,
                        FontSize = SystemFonts.MessageFontSize,
                        FontFamily = SystemFonts.MessageFontFamily,
                        Foreground = SystemColors.ControlTextBrush
                    };

                    var box = new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = PopupMenuBorderBrush,
                        BorderThickness = PopupMenuBorderThickness,
                        CornerRadius = new CornerRadius(2),
                        Child = host
                    };

                    return box;
                }

                Button selectedMainBtn = null;
                Button selectedAnkaBtn = null;
                Button selectedCutBtn = null;
                FrameworkElement selectedSideRow = null;
                Border selectedDRow = null;

                void SelectDRow(Border row)
                {
                    if (selectedDRow != null) selectedDRow.Background = normalBg;
                    selectedDRow = row;
                    if (selectedDRow != null) selectedDRow.Background = selectedBg;
                }

                void SelectMain(Button btn)
                {
                    if (selectedMainBtn != null) selectedMainBtn.Background = normalBg;
                    selectedMainBtn = btn;
                    if (selectedMainBtn != null) selectedMainBtn.Background = selectedBg;
                }

                void InvalidatePendingMainPopupOpen()
                {
                    unchecked { mainPopupOpenRequestId++; }
                }

                void SelectAnka(Button btn)
                {
                    if (selectedAnkaBtn != null) selectedAnkaBtn.Background = normalBg;
                    selectedAnkaBtn = btn;
                    if (selectedAnkaBtn != null) selectedAnkaBtn.Background = selectedBg;
                }

                void SelectCut(Button btn)
                {
                    if (selectedCutBtn != null) selectedCutBtn.Background = normalBg;
                    selectedCutBtn = btn;
                    if (selectedCutBtn != null) selectedCutBtn.Background = selectedBg;
                }

                void SelectSideRow(FrameworkElement row)
                {
                    if (selectedSideRow != null)
                    {
                        if (selectedSideRow is Border b1) b1.Background = normalBg;
                        else if (selectedSideRow is Panel p1) p1.Background = normalBg;
                    }

                    selectedSideRow = row;

                    if (selectedSideRow != null)
                    {
                        if (selectedSideRow is Border b2) b2.Background = selectedBg;
                        else if (selectedSideRow is Panel p2) p2.Background = selectedBg;
                    }
                }

                ControlTemplate _flatBtnTemplate = null;
                ControlTemplate GetFlatBtnTemplate()
                {
                    if (_flatBtnTemplate != null) return _flatBtnTemplate;

                    var border = new FrameworkElementFactory(typeof(Border));
                    border.SetValue(Border.SnapsToDevicePixelsProperty, true);
                    border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                    border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                    border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                    border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

                    var presenter = new FrameworkElementFactory(typeof(ContentPresenter));
                    presenter.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
                    presenter.SetValue(ContentPresenter.ContentTemplateProperty, new TemplateBindingExtension(Button.ContentTemplateProperty));
                    presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
                    presenter.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Button.VerticalContentAlignmentProperty));

                    border.AppendChild(presenter);

                    _flatBtnTemplate = new ControlTemplate(typeof(Button))
                    {
                        VisualTree = border
                    };
                    return _flatBtnTemplate;
                }

                Button MakeMenuButton(string header, bool hasNext, double minWidth)
                {
                    var dock = new DockPanel { LastChildFill = true };

                    var arrow = new TextBlock
                    {
                        Text = "❯",
                        Margin = new Thickness(0, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Visibility = hasNext ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed
                    };
                    DockPanel.SetDock(arrow, Dock.Right);

                    var label = new TextBlock
                    {
                        Text = header ?? string.Empty,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    dock.Children.Add(arrow);
                    dock.Children.Add(label);

                    return new Button
                    {
                        Content = dock,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(12, 6, 4, 6),
                        Background = normalBg,
                        BorderBrush = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        MinWidth = minWidth,
                        OverridesDefaultStyle = true,
                        Template = GetFlatBtnTemplate(),
                        Focusable = false,
                        IsTabStop = false
                    };
                }

                // ======= ✅ NEW helper: format value hiển thị trên menu (thay 上/下) =======
                string FormatAnka(double v)
                {
                    // tùy bạn: "0.##" hoặc "0.###"
                    return v.ToString("0.##", CultureInfo.InvariantCulture);
                }
                // =======================================================================

                void OpenSidePopup(Button placementBtn, AnkaSide side)
                {
                    CloseSubMenusExcept(ankaPop);
                    if (sidePop != null) sidePop.IsOpen = false;

                    sidePop = new System.Windows.Controls.Primitives.Popup
                    {
                        PlacementTarget = placementBtn,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                        HorizontalOffset = 1,
                        VerticalOffset = -1.5,
                        AllowsTransparency = true,
                        StaysOpen = true
                    };

                    tb.Tag = Tuple.Create(mainPop,
                                          ankaPop ?? new System.Windows.Controls.Primitives.Popup(),
                                          sidePop,
                                          lenPop ?? new System.Windows.Controls.Primitives.Popup(),
                                          lenDirPop ?? new System.Windows.Controls.Primitives.Popup());

                    var root = new StackPanel { Orientation = Orientation.Vertical };

                    TextBox activeBox = null;
                    // Anka
                    Border MakeRow(int previewOffset, bool isTop)
                    {
                        var rowHost = new Border
                        {
                            Background = normalBg,
                            Padding = new Thickness(10, 6, 10, 6),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            MinWidth = Math.Max(MENU3_MIN_WIDTH, 180), // chiều dài của popup anka
                            Cursor = Cursors.Hand
                        };

                        var row = new Grid { HorizontalAlignment = HorizontalAlignment.Stretch };
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        var preview = CreateAnkaPreviewCanvas(previewOffset, side);
                        preview.HorizontalAlignment = HorizontalAlignment.Center;
                        preview.VerticalAlignment = VerticalAlignment.Center;
                        Grid.SetColumn(preview, 1);

                        var box = new TextBox
                        {
                            Width = 40,
                            MinWidth = 40,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = side == AnkaSide.Left ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                            TextAlignment = side == AnkaSide.Left ? TextAlignment.Right : TextAlignment.Left,
                            Margin = side == AnkaSide.Left
                                ? new Thickness(0, 0, 5, 0)
                                : new Thickness(-15, 0, 0, 0),
                            Padding = new Thickness(2, 0, 2, 0),
                            IsReadOnly = true,
                            BorderThickness = new Thickness(0),
                            Background = Brushes.Transparent,
                            FontSize = 15
                        };
                        AttachDimIntegerValidation(
                            box,
                            () => box.IsReadOnly ? Brushes.Transparent : Brushes.White,
                            () => box.IsReadOnly ? Brushes.Transparent : Brushes.Black,
                            () => box.IsReadOnly ? new Thickness(0) : new Thickness(1));
                        Grid.SetColumn(box, side == AnkaSide.Left ? 0 : 2);

                        row.Children.Add(preview);
                        row.Children.Add(box);
                        rowHost.Child = row;

                        void FocusBoxSelectAll()
                        {
                            box.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                box.Focus();
                                box.SelectAll();
                            }), DispatcherPriority.Input);
                        }

                        void RefreshBoxFromCurrent()
                        {
                            double cur = GetCurrentFor(side, wantTop: isTop);
                            box.Text = cur.ToString(CultureInfo.InvariantCulture);
                        }

                        void SetDisplayMode()
                        {
                            box.IsReadOnly = true;
                            box.BorderThickness = new Thickness(0);
                            box.Background = Brushes.Transparent;
                        }

                        void SetEditMode()
                        {
                            box.IsReadOnly = false;
                            box.BorderThickness = new Thickness(1);
                            box.BorderBrush = PopupMenuEditorBorderBrush;
                            box.Background = Brushes.White;
                        }

                        bool TryCommitOrRefocus(bool closeAllAfterValid)
                        {
                            string now = (box.Text ?? "").Trim();
                            if (string.IsNullOrWhiteSpace(now))
                            {
                                RefreshBoxFromCurrent();
                                SetDisplayMode();
                                Keyboard.ClearFocus();
                                return true;
                            }

                            if (!TryParseNumber(now, out var val))
                            {
                                FocusBoxSelectAll();
                                return false;
                            }

                            double cur = GetCurrentFor(side, wantTop: isTop);
                            if (Math.Abs(cur - val) < 1e-6)
                            {
                                RefreshBoxFromCurrent();
                                SetDisplayMode();
                                Keyboard.ClearFocus();
                                return true;
                            }

                            CommitFor(side, isTop, val);

                            // refresh lại theo giá trị thực
                            RefreshBoxFromCurrent();

                            // sau commit: về display mode + bỏ focus để caret tắt
                            SetDisplayMode();
                            if (activeBox == box) activeBox = null;
                            if (_activeAnkaBox == box)
                            {
                                _activeAnkaBox = null;
                                _activeAnkaTryCommitOrRefocus = null;
                            }
                            Keyboard.ClearFocus();

                            if (closeAllAfterValid) CloseAll();
                            return true;
                        }

                        bool CancelEditWithoutCommit()
                        {
                            RefreshBoxFromCurrent();
                            SetDisplayMode();
                            if (activeBox == box) activeBox = null;
                            if (_activeAnkaBox == box)
                            {
                                _activeAnkaBox = null;
                                _activeAnkaTryCommitOrRefocus = null;
                            }
                            Keyboard.ClearFocus();
                            return true;
                        }

                        void ActivateEdit()
                        {
                            // Nếu đang có box khác active, đóng edit cũ mà không commit
                            if (activeBox != null && activeBox != box)
                            {
                                if (_activeAnkaTryCommitOrRefocus != null)
                                {
                                    bool ok = _activeAnkaTryCommitOrRefocus();
                                    if (!ok) return;
                                }
                            }

                            activeBox = box;

                            _activeAnkaBox = box;

                            // Hover/click-outside chỉ hủy edit chưa xác nhận, không commit.
                            _activeAnkaTryCommitOrRefocus = CancelEditWithoutCommit;

                            SetEditMode();
                            FocusBoxSelectAll();
                        }

                        // init text
                        RefreshBoxFromCurrent();
                        SetDisplayMode();

                        rowHost.MouseLeftButtonDown += (ss, ee) =>
                        {
                            ee.Handled = true;
                            SelectSideRow(rowHost);
                            ActivateEdit();
                        };

                        // click trực tiếp vào textbox cũng active edit
                        box.PreviewMouseLeftButtonDown += (ss, ee) =>
                        {
                            SelectSideRow(rowHost);
                            ActivateEdit();
                            ee.Handled = true;
                        };

                        // Hover sang row khác: caret tắt (commit nếu hợp lệ) nhưng KHÔNG đóng menu
                        rowHost.MouseEnter += (_, __) =>
                        {
                            if (activeBox != null && activeBox != box && activeBox.IsKeyboardFocusWithin)
                            {
                                if (_activeAnkaTryCommitOrRefocus != null)
                                {
                                    bool ok = _activeAnkaTryCommitOrRefocus();
                                    if (!ok) return; // invalid => giữ focus ở box cũ
                                }
                            }

                            SelectSideRow(rowHost);
                        };

                        box.KeyDown += (ss, ee) =>
                        {
                            if (ee.Key == Key.Enter)
                            {
                                // ✅ Enter: commit + đóng menu
                                TryCommitOrRefocus(closeAllAfterValid: true);
                                ee.Handled = true;
                            }
                            else if (ee.Key == Key.Escape)
                            {
                                // Esc: revert và tắt caret (không đóng menu)
                                CancelEditWithoutCommit();
                                ee.Handled = true;
                            }
                        };

                        box.LostKeyboardFocus += (_, __) =>
                        {
                            if (_suppressAnkaCommitOnce)
                            {
                                _suppressAnkaCommitOnce = false;
                                return;
                            }

                            if (!box.IsReadOnly)
                                CancelEditWithoutCommit();
                        };

                        return rowHost;
                    }

                    var rowTop = MakeRow(previewOffset: -10, isTop: true);
                    var rowBot = MakeRow(previewOffset: +10, isTop: false);

                    root.Children.Add(WithRowDivider(rowTop));
                    root.Children.Add(WithRowDivider(rowBot));

                    sidePop.Child = WrapBox(root);

                    placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        sidePop.IsOpen = true;
                        SelectSideRow(null);
                    }), DispatcherPriority.Input);
                }

                Button selectedLenBtn = null;
                Border selectedLenDirBtn = null;

                void SelectLen(Button btn)
                {
                    if (selectedLenBtn != null) selectedLenBtn.Background = normalBg;
                    selectedLenBtn = btn;
                    if (selectedLenBtn != null) selectedLenBtn.Background = selectedBg;
                }

                void SelectLenDir(Border btn)
                {
                    if (selectedLenDirBtn != null) selectedLenDirBtn.Background = normalBg;
                    selectedLenDirBtn = btn;
                    if (selectedLenDirBtn != null) selectedLenDirBtn.Background = selectedBg;
                }

                void OpenLenDirPopup(Button placementBtn, bool isLeftMenu)
                {
                    CloseSubMenusExcept(lenPop);
                    if (lenDirPop != null) lenDirPop.IsOpen = false;

                    lenDirPop = new System.Windows.Controls.Primitives.Popup
                    {
                        PlacementTarget = placementBtn,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                        HorizontalOffset = 1,
                        VerticalOffset = -1.5,
                        AllowsTransparency = true,
                        StaysOpen = true
                    };

                    tb.Tag = Tuple.Create(mainPop,
                                          ankaPop ?? new System.Windows.Controls.Primitives.Popup(),
                                          sidePop ?? new System.Windows.Controls.Primitives.Popup(),
                                          lenPop ?? new System.Windows.Controls.Primitives.Popup(),
                                          lenDirPop);

                    var root = new StackPanel { Orientation = Orientation.Vertical };

                    TextBox activeLenBox = null;
                    Border activeLenRow = null;
                    Action activeLenHide = null;

                    void CancelActiveLenEdit()
                    {
                        if (activeLenBox == null) return;
                        activeLenHide?.Invoke();
                    }

                    Border MakeLenDirRow(string label, bool pullLeft)
                    {
                        var rowHost = new Border
                        {
                            Background = normalBg,
                            Padding = new Thickness(10, 6, 10, 6),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            MinWidth = MENU3_MIN_WIDTH,
                            Cursor = Cursors.Hand
                        };

                        var row = new DockPanel { LastChildFill = true };

                        var lbl = new TextBlock
                        {
                            Text = label,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        DockPanel.SetDock(lbl, Dock.Left);

                        var preview = CreateLengthPreviewCanvas(pullLeft);
                        DockPanel.SetDock(preview, Dock.Right);
                        // ô text chiều dài
                        var box = new TextBox
                        {
                            Width = 50,
                            MinWidth = 50,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Visibility = System.Windows.Visibility.Collapsed
                        };
                        AttachDimIntegerValidation(box);
                        Brush normalBorderBrush = box.BorderBrush ?? Brushes.Black;
                        Thickness normalBorderThickness = box.BorderThickness;
                        DockPanel.SetDock(box, Dock.Right);

                        row.Children.Add(lbl);
                        row.Children.Add(box);
                        row.Children.Add(preview);
                        rowHost.Child = row;

                        void FocusBox()
                        {
                            box.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                box.Focus();
                                box.SelectAll();
                            }), DispatcherPriority.Input);
                        }

                        Thickness GetInvalidBorderThickness()
                        {
                            if (normalBorderThickness.Left <= 0 && normalBorderThickness.Top <= 0
                                && normalBorderThickness.Right <= 0 && normalBorderThickness.Bottom <= 0)
                            {
                                return new Thickness(1);
                            }

                            return normalBorderThickness;
                        }

                        void ApplyDeltaLimitValidationVisual()
                        {
                            if (!IsDigitOnlyText(box.Text))
                                return;

                            string now = (box.Text ?? string.Empty).Trim();
                            if (!TryParseNumber(now, out var val) || val <= 0)
                            {
                                box.BorderBrush = normalBorderBrush;
                                box.BorderThickness = normalBorderThickness;
                                return;
                            }

                            bool exceedsLimit = TryGetOrangeSegLengthDeltaLimit(owner, key, isLeftMenu, pullLeft, out var maxAllowed)
                                                && val > maxAllowed + 1e-6;

                            box.BorderBrush = exceedsLimit ? Brushes.Red : normalBorderBrush;
                            box.BorderThickness = exceedsLimit ? GetInvalidBorderThickness() : normalBorderThickness;
                        }

                        void ShowDeltaLimitRejectedState()
                        {
                            box.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                box.BorderBrush = Brushes.Red;
                                box.BorderThickness = GetInvalidBorderThickness();
                            }), DispatcherPriority.Input);
                        }

                        bool WouldExceedDeltaLimit(string candidateText)
                        {
                            if (!IsDigitOnlyText(candidateText, allowEmpty: true))
                                return false;

                            string now = (candidateText ?? string.Empty).Trim();
                            if (!TryParseNumber(now, out var val) || val <= 0)
                                return false;

                            return TryGetOrangeSegLengthDeltaLimit(owner, key, isLeftMenu, pullLeft, out var maxAllowed)
                                   && val > maxAllowed + 1e-6;
                        }

                        void EndEditShowPreview()
                        {
                            box.Visibility = System.Windows.Visibility.Collapsed;
                            if (box.Tag is FrameworkElement prev)
                                prev.Visibility = System.Windows.Visibility.Visible;
                            else
                                preview.Visibility = System.Windows.Visibility.Visible;

                            if (activeLenBox == box) activeLenBox = null;
                            if (activeLenRow == rowHost) activeLenRow = null;
                            if (activeLenHide != null && activeLenBox == null) activeLenHide = null;
                        }

                        void BeginEdit()
                        {
                            if (activeLenBox != null && activeLenBox != box)
                            {
                                if (activeLenBox.Tag is FrameworkElement prev)
                                    prev.Visibility = System.Windows.Visibility.Visible;
                                activeLenBox.Visibility = System.Windows.Visibility.Collapsed;
                            }
                            activeLenBox = box;
                            activeLenRow = rowHost;
                            activeLenHide = EndEditShowPreview;

                            box.Text = (box.Text ?? string.Empty).Trim();
                            box.Tag = preview;
                            preview.Visibility = System.Windows.Visibility.Collapsed;
                            box.Visibility = System.Windows.Visibility.Visible;
                            ApplyDeltaLimitValidationVisual();
                            FocusBox();
                        }

                        bool TryCommitOrRefocus(bool closeAllAfterValid)
                        {
                            string now = (box.Text ?? string.Empty).Trim();
                            if (!TryParseNumber(now, out var val) || val <= 0)
                            {
                                ApplyDeltaLimitValidationVisual();
                                FocusBox();
                                return false;
                            }

                            if (TryGetOrangeSegLengthDeltaLimit(owner, key, isLeftMenu, pullLeft, out var maxAllowed)
                                && val > maxAllowed + 1e-6)
                            {
                                ApplyDeltaLimitValidationVisual();
                                FocusBox();
                                return false;
                            }

                            bool changed = ApplyOrangeSegLengthDelta(owner, key, isLeftMenu, pullLeft, val);
                            if (!changed)
                            {
                                ApplyDeltaLimitValidationVisual();
                                FocusBox();
                                return false;
                            }

                            if (changed)
                                Redraw(canvas, owner);
                            EndEditShowPreview();
                            if (closeAllAfterValid) CloseAll();
                            return true;
                        }

                        rowHost.MouseLeftButtonDown += (ss, ee) =>
                        {
                            ee.Handled = true;
                            SelectLenDir(rowHost);

                            BeginEdit();
                        };

                        rowHost.MouseEnter += (_, __) =>
                        {
                            CancelActiveAnkaEdit();
                            if (activeLenBox != box)
                                CancelActiveLenEdit();
                            SelectLenDir(rowHost);
                        };

                        box.TextChanged += (_, __) => ApplyDeltaLimitValidationVisual();

                        box.PreviewTextInput += (_, ee) =>
                        {
                            if (ee.Handled)
                                return;

                            string candidate = BuildCandidateText(box, ee.Text);
                            if (!WouldExceedDeltaLimit(candidate))
                                return;

                            ShowDeltaLimitRejectedState();
                            ee.Handled = true;
                        };

                        DataObject.AddPastingHandler(box, new DataObjectPastingEventHandler((_, ee) =>
                        {
                            string pastedText = null;

                            if (ee.SourceDataObject != null)
                            {
                                if (ee.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
                                    pastedText = ee.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
                                else if (ee.SourceDataObject.GetDataPresent(DataFormats.Text))
                                    pastedText = ee.SourceDataObject.GetData(DataFormats.Text) as string;
                            }

                            string candidate = BuildCandidateText(box, pastedText ?? string.Empty);
                            if (!WouldExceedDeltaLimit(candidate))
                                return;

                            ShowDeltaLimitRejectedState();
                            ee.CancelCommand();
                        }));
                        rowHost.MouseLeave += (_, __) =>
                        {
                            rowHost.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (rowHost.IsMouseOver) return;
                                EndEditShowPreview();
                            }), DispatcherPriority.Background);
                        };

                        box.KeyDown += (ss, ee) =>
                        {
                            if (ee.Key == Key.Enter)
                            {
                                TryCommitOrRefocus(closeAllAfterValid: true);
                                ee.Handled = true;
                            }
                            else if (ee.Key == Key.Escape)
                            {
                                EndEditShowPreview();
                                ee.Handled = true;
                            }
                        };

                        box.LostKeyboardFocus += (_, __) =>
                        {
                            if (box.Visibility == System.Windows.Visibility.Visible)
                                EndEditShowPreview();
                        };

                        return rowHost;
                    }

                    // ✅ NEW: dòng “chiều dài tổng” dùng cùng cơ chế preview/edit như 2 dòng còn lại
                    Border MakeTotalLenRow(string label)
                    {
                        var rowHost = new Border
                        {
                            Background = normalBg,
                            Padding = new Thickness(10, 6, 10, 6),
                            HorizontalAlignment = HorizontalAlignment.Stretch,
                            MinWidth = MENU3_MIN_WIDTH,
                            Cursor = Cursors.Hand
                        };

                        var row = new DockPanel { LastChildFill = true };

                        var lbl = new TextBlock
                        {
                            Text = label,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        DockPanel.SetDock(lbl, Dock.Left);

                        var preview = new TextBlock
                        {
                            MinWidth = 50,
                            Margin = new Thickness(30, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                            TextAlignment = TextAlignment.Left
                        };
                        DockPanel.SetDock(preview, Dock.Right);

                        var box = new TextBox
                        {
                            Width = 50,
                            MinWidth = 50,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            TextAlignment = TextAlignment.Left,
                            Margin = new Thickness(30, 0, 0, 0),
                            Visibility = System.Windows.Visibility.Collapsed
                        };
                        AttachDimIntegerValidation(box);
                        DockPanel.SetDock(box, Dock.Right);
                        box.Tag = preview;

                        row.Children.Add(lbl);
                        row.Children.Add(box);
                        row.Children.Add(preview);
                        rowHost.Child = row;

                        void RefreshDisplayFromCurrent()
                        {
                            if (TryGetSegKeyForDimKey(owner, key, out var segKey)
                                && TryGetSegLength(owner, segKey, out var segLength))
                            {
                                string text = segLength.ToString(CultureInfo.InvariantCulture);
                                box.Text = text;
                                preview.Text = text;
                            }
                            else
                            {
                                box.Text = string.Empty;
                                preview.Text = string.Empty;
                            }
                        }

                        void SetDisplayMode()
                        {
                            box.IsReadOnly = true;
                            box.BorderThickness = new Thickness(0);
                            box.Background = Brushes.Transparent;
                        }

                        void SetEditMode()
                        {
                            box.IsReadOnly = false;
                            box.BorderThickness = new Thickness(1);
                            box.BorderBrush = PopupMenuEditorBorderBrush;
                            box.Background = Brushes.White;
                        }

                        void FocusBox()
                        {
                            box.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                box.Focus();
                                box.SelectAll();
                            }), DispatcherPriority.Input);
                        }

                        void CancelEditWithoutCommit()
                        {
                            RefreshDisplayFromCurrent();
                            SetDisplayMode();
                            Keyboard.ClearFocus();
                            if (box.Tag is FrameworkElement prev)
                                prev.Visibility = System.Windows.Visibility.Visible;
                            else
                                preview.Visibility = System.Windows.Visibility.Visible;
                            box.Visibility = System.Windows.Visibility.Collapsed;
                            if (activeLenBox == box) activeLenBox = null;
                            if (activeLenRow == rowHost) activeLenRow = null;
                            if (activeLenHide != null && activeLenBox == null) activeLenHide = null;
                        }

                        void EndEdit()
                        {
                            CancelEditWithoutCommit();
                        }

                        void BeginEdit()
                        {
                            CancelActiveLenEdit();
                            RefreshDisplayFromCurrent();
                            SetEditMode();
                            if (activeLenBox != null && activeLenBox != box)
                                CancelActiveLenEdit();

                            activeLenBox = box;
                            activeLenRow = rowHost;
                            activeLenHide = EndEdit;
                            preview.Visibility = System.Windows.Visibility.Collapsed;
                            box.Visibility = System.Windows.Visibility.Visible;
                            FocusBox();
                        }

                        bool TryCommitOrRefocus(bool closeAllAfterValid)
                        {
                            string now = (box.Text ?? string.Empty).Trim();
                            if (!TryParseNumber(now, out var val) || val <= 0)
                            {
                                FocusBox();
                                return false;
                            }

                            bool changed = false;
                            if (owner != null && TryGetSegKeyForDimKey(owner, key, out var segKey))
                            {
                                double newTotal = Math.Max(0, val);
                                changed = ApplyOrangeSegTotalLength(owner, segKey, anchorLeft: !isLeftMenu, newLength: newTotal);
                            }

                            if (changed)
                            {
                                if (closeAllAfterValid) CloseAll();
                                Redraw(canvas, owner);
                                return true;
                            }

                            CancelEditWithoutCommit();
                            if (closeAllAfterValid) CloseAll();
                            return true;
                        }

                        RefreshDisplayFromCurrent();
                        SetDisplayMode();

                        rowHost.MouseLeftButtonDown += (ss, ee) =>
                        {
                            ee.Handled = true;
                            SelectLenDir(rowHost);

                            BeginEdit();
                        };

                        rowHost.MouseEnter += (_, __) =>
                        {
                            CancelActiveAnkaEdit();
                            if (activeLenBox != box)
                                CancelActiveLenEdit();
                            SelectLenDir(rowHost);
                        };

                        box.PreviewMouseLeftButtonDown += (ss, ee) =>
                        {
                            SelectLenDir(rowHost);
                            if (box.IsReadOnly)
                            {
                                BeginEdit();
                                ee.Handled = true;
                            }
                        };

                        rowHost.MouseLeave += (_, __) =>
                        {
                            rowHost.Dispatcher.BeginInvoke(new Action(() =>
                            {
                                if (rowHost.IsMouseOver) return;
                                EndEdit();
                            }), DispatcherPriority.Background);
                        };

                        box.KeyDown += (ss, ee) =>
                        {
                            if (ee.Key == Key.Enter)
                            {
                                // ✅ Enter: commit + đóng menu
                                TryCommitOrRefocus(closeAllAfterValid: true);
                                ee.Handled = true;
                            }
                            else if (ee.Key == Key.Escape)
                            {
                                CancelEditWithoutCommit();
                                ee.Handled = true;
                            }
                        };

                        box.LostKeyboardFocus += (_, __) =>
                        {
                            if (!box.IsReadOnly)
                                CancelEditWithoutCommit();
                        };

                        return rowHost;
                    }

                    root.MouseMove += (_, __) =>
                    {
                        if (activeLenRow == null) return;
                        if (activeLenRow.IsMouseOver) return;
                        activeLenHide?.Invoke();
                    };

                    var rowPullLeft = MakeLenDirRow("左へ引く", pullLeft: true);
                    var rowPullRight = MakeLenDirRow("右へ引く", pullLeft: false);

                    // ✅ thêm option mới
                    var rowTotalLen = MakeTotalLenRow("全部");

                    root.Children.Add(WithRowDivider(rowPullLeft));
                    root.Children.Add(WithRowDivider(rowPullRight));
                    root.Children.Add(WithRowDivider(rowTotalLen));

                    lenDirPop.Child = WrapBox(root);

                    placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        lenDirPop.IsOpen = true;
                        SelectLenDir(null);
                    }), DispatcherPriority.Input);
                }

                void OpenLenPopup(Button placementBtn)
                {
                    int openRequestId = unchecked(++mainPopupOpenRequestId);
                    CloseSubMenus();
                    if (lenPop != null) lenPop.IsOpen = false;

                    lenPop = new System.Windows.Controls.Primitives.Popup
                    {
                        PlacementTarget = placementBtn,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                        HorizontalOffset = 1,
                        VerticalOffset = -1.5,
                        AllowsTransparency = true,
                        StaysOpen = true
                    };

                    tb.Tag = Tuple.Create(mainPop,
                                          ankaPop ?? new System.Windows.Controls.Primitives.Popup(),
                                          sidePop ?? new System.Windows.Controls.Primitives.Popup(),
                                          lenPop,
                                          lenDirPop ?? new System.Windows.Controls.Primitives.Popup());

                    var root = new StackPanel { Orientation = Orientation.Vertical };

                    var btnLeft = MakeMenuButton("左", hasNext: true, minWidth: MENU2_MIN_WIDTH);
                    btnLeft.MouseEnter += (_, __) =>
                    {
                        CancelActiveAnkaEdit();
                        SelectLen(btnLeft);
                        OpenLenDirPopup(btnLeft, true);
                    };
                    btnLeft.Click += (_, __) =>
                    {
                        CancelActiveAnkaEdit();
                        SelectLen(btnLeft);
                        OpenLenDirPopup(btnLeft, true);
                    };

                    var btnRight = MakeMenuButton("右", hasNext: true, minWidth: MENU2_MIN_WIDTH);
                    btnRight.MouseEnter += (_, __) =>
                    {
                        CancelActiveAnkaEdit();
                        SelectLen(btnRight);
                        OpenLenDirPopup(btnRight, false);
                    };
                    btnRight.Click += (_, __) =>
                    {
                        CancelActiveAnkaEdit();
                        SelectLen(btnRight);
                        OpenLenDirPopup(btnRight, false);
                    };

                    root.Children.Add(WithRowDivider(btnLeft));
                    root.Children.Add(WithRowDivider(btnRight));

                    lenPop.Child = WrapBox(root);

                    placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (openRequestId != mainPopupOpenRequestId) return;
                        if (!ReferenceEquals(selectedMainBtn, placementBtn)) return;
                        lenPop.IsOpen = true;
                        SelectLen(null);
                    }), DispatcherPriority.Input);
                }

                bool TryParsePositiveInt(string text, out int n)
                {
                    n = 0;
                    if (!int.TryParse((text ?? string.Empty).Trim(), out n)) return false;
                    return n > 0;
                }

                void OpenCutDetailPopup(FrameworkElement placementTarget, int segments)
                {
                    CloseSubMenusExcept(cutModePop, cutInputPop);
                    if (cutDetailPop != null) cutDetailPop.IsOpen = false;

                    if (segments < 2) return;
                    if (!TryGetSegKeyForDimKey(owner, key, out var segKey)) return;

                    double baseX1 = segKey.X1_10 / 10.0;
                    double baseX2 = segKey.X2_10 / 10.0;
                    var (segX1, segX2) = GetOrangeSegOverride(owner, segKey, baseX1, baseX2);
                    double totalLength = segX2 - segX1;
                    if (totalLength <= 1e-6) return;

                    cutDetailPop = new System.Windows.Controls.Primitives.Popup
                    {
                        PlacementTarget = placementTarget,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                        HorizontalOffset = 16,
                        VerticalOffset = -7.5,
                        AllowsTransparency = true,
                        StaysOpen = true
                    };

                    // =========================
                    // POPUP WIDTH CHÍNH
                    // =========================
                    var root = new StackPanel
                    {
                        Orientation = Orientation.Vertical,
                        Margin = new Thickness(10, 6, 10, 6),
                        MinWidth = 10   // <<< chỉnh chiều ngang popup tại đây
                    };

                    // ===== direction row =====
                    var dirRow = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Margin = new Thickness(10, 6, 10, 6)
                    };

                    dirRow.Children.Add(new TextBlock
                    {
                        Text = "方向",
                        Width = 50,
                        VerticalAlignment = VerticalAlignment.Center
                    });

                    var cmbDirection = new ComboBox
                    {
                        Width = 65,
                        ItemsSource = new[] { "左→右", "右→左" },
                        SelectedIndex = 0
                    };

                    dirRow.Children.Add(cmbDirection);
                    root.Children.Add(WithRowDivider(dirRow));

                    var inputBoxes = new List<TextBox>();
                    var autoBoxes = new List<TextBox>();
                    var autoPreviewBackground = new SolidColorBrush(Color.FromRgb(255, 248, 214));
                    var autoPreviewBorderBrush = new SolidColorBrush(Color.FromRgb(214, 190, 122));
                    var inputBoxBorderBrushes = new Dictionary<TextBox, Brush>();
                    var inputBoxBorderThicknesses = new Dictionary<TextBox, Thickness>();

                    for (int i = 1; i <= segments; i++)
                    {
                        var row = new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Margin = new Thickness(10, 6, 10, 6),
                            MinWidth = 50
                        };

                        row.Children.Add(new TextBlock
                        {
                            Text = $"{i}:",
                            Width = 24,
                            VerticalAlignment = VerticalAlignment.Center
                        });

                        var box = new TextBox
                        {
                            Width = 100,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Padding = new Thickness(2, 0, 2, 0)
                        };

                        if (i < segments)
                        {
                            AttachDimIntegerValidation(box);
                            inputBoxBorderBrushes[box] = box.BorderBrush;
                            inputBoxBorderThicknesses[box] = box.BorderThickness;
                            inputBoxes.Add(box);
                        }
                        else
                        {
                            box.IsReadOnly = true;
                            box.IsTabStop = false;
                            box.Background = autoPreviewBackground;
                            box.BorderBrush = autoPreviewBorderBrush;
                            box.Foreground = Brushes.DarkSlateGray;
                            box.FontWeight = FontWeights.SemiBold;
                            box.ToolTip = "Automatically calculated remaining length";
                            autoBoxes.Add(box);
                        }

                        row.Children.Add(box);
                        root.Children.Add(WithRowDivider(row));
                    }

                    var txtError = new TextBlock
                    {
                        Margin = new Thickness(10, -17, 10, -17),
                        Foreground = Brushes.Red,
                        TextWrapping = TextWrapping.Wrap
                    };
                    var manualInputBackground = Brushes.White;
                    var invalidInputBorderBrush = Brushes.Red;

                    //root.Children.Add(WithRowDivider(txtError));

                    void SetAutoBoxes(double remaining)
                    {
                        double safeRemaining = Math.Max(0.0, remaining);
                        string previewText = Math.Round(safeRemaining, MidpointRounding.AwayFromZero).ToString("0");
                        foreach (var autoBox in autoBoxes)
                            autoBox.Text = previewText;
                    }

                    void ResetInputBoxStates()
                    {
                        foreach (var inputBox in inputBoxes)
                        {
                            inputBox.Background = manualInputBackground;

                            if (inputBoxBorderBrushes.TryGetValue(inputBox, out var normalBorderBrush))
                                inputBox.BorderBrush = normalBorderBrush;

                            if (inputBoxBorderThicknesses.TryGetValue(inputBox, out var normalBorderThickness))
                                inputBox.BorderThickness = normalBorderThickness;
                        }
                    }

                    void MarkInputBoxInvalid(TextBox box)
                    {
                        if (box == null)
                            return;

                        box.Background = manualInputBackground;
                        box.BorderBrush = invalidInputBorderBrush;

                        if (inputBoxBorderThicknesses.TryGetValue(box, out var normalBorderThickness))
                        {
                            if (normalBorderThickness.Left <= 0 && normalBorderThickness.Top <= 0
                                && normalBorderThickness.Right <= 0 && normalBorderThickness.Bottom <= 0)
                            {
                                box.BorderThickness = new Thickness(1);
                            }
                            else
                            {
                                box.BorderThickness = normalBorderThickness;
                            }
                        }
                    }

                    bool TryPreviewCurrentState(out double enteredSum, out TextBox invalidBox, out string validationMessage)
                    {
                        enteredSum = 0.0;
                        invalidBox = null;
                        validationMessage = string.Empty;

                        foreach (var box in inputBoxes)
                        {
                            string raw = (box.Text ?? string.Empty).Trim();
                            if (string.IsNullOrWhiteSpace(raw))
                                continue;

                            if (!int.TryParse(raw, out var value) || value <= 0)
                            {
                                invalidBox = box;
                                validationMessage = "Please enter positive integers.";
                                return false;
                            }

                            enteredSum += value;
                            if (totalLength - enteredSum <= 0.0)
                            {
                                invalidBox = box;
                                validationMessage = "Remaining length must stay positive.";
                                return false;
                            }
                        }

                        return true;
                    }

                    bool ValidateAndPreview(out List<double> lengths)
                    {
                        lengths = new List<double>();
                        ResetInputBoxStates();
                        txtError.Text = string.Empty;

                        double enteredSum = 0.0;
                        foreach (var box in inputBoxes)
                        {
                            string raw = (box.Text ?? string.Empty).Trim();
                            if (string.IsNullOrWhiteSpace(raw))
                            {
                                RefreshValidation();
                                return false;
                            }

                            if (!int.TryParse(raw, out var value) || value <= 0)
                            {
                                MarkInputBoxInvalid(box);
                                SetAutoBoxes(totalLength - enteredSum);
                                txtError.Text = "Please enter positive integers.";
                                return false;
                            }

                            lengths.Add(value);
                            enteredSum += value;
                            if (totalLength - enteredSum <= 0.0)
                            {
                                MarkInputBoxInvalid(box);
                                SetAutoBoxes(0.0);
                                txtError.Text = "Remaining length must stay positive.";
                                return false;
                            }
                        }

                        double remaining = totalLength - enteredSum;
                        double roundedRemaining = Math.Round(remaining, MidpointRounding.AwayFromZero);

                        SetAutoBoxes(remaining);

                        if (roundedRemaining <= 0)
                        {
                            txtError.Text = "Remaining length must stay positive.";
                            return false;
                        }

                        lengths.Add(roundedRemaining);

                        if (Math.Abs(lengths.Sum() - totalLength) > 1.0)
                        {
                            txtError.Text = "Entered lengths do not match the segment length.";
                            return false;
                        }

                        return true;
                    }

                    void RefreshValidation()
                    {
                        ResetInputBoxStates();
                        txtError.Text = string.Empty;

                        if (!TryPreviewCurrentState(out var enteredSum, out var invalidBox, out var validationMessage))
                        {
                            if (invalidBox != null)
                                MarkInputBoxInvalid(invalidBox);

                            SetAutoBoxes(totalLength - enteredSum);
                            txtError.Text = validationMessage;
                            return;
                        }

                        double remaining = totalLength - enteredSum;
                        SetAutoBoxes(remaining);
                    }

                    foreach (var box in inputBoxes)
                    {
                        box.TextChanged += (_, __) => RefreshValidation();
                        box.KeyDown += (_, ee) =>
                        {
                            if (ee.Key == Key.Enter)
                            {
                                if (ValidateAndPreview(out var lengths))
                                {
                                    bool leftToRight = cmbDirection.SelectedIndex != 1;
                                    if (ApplyCustomCutToOrangeSegment(owner, key, lengths, leftToRight))
                                    {
                                        CloseAll();
                                        Redraw(canvas, owner);
                                    }
                                }
                                ee.Handled = true;
                            }
                        };
                    }

                    RefreshValidation();

                    cutDetailPop.Child = WrapBox(root);

                    var focusOwner = placementTarget as TextBox;
                    int focusOwnerSelectionStart = focusOwner?.SelectionStart ?? 0;
                    int focusOwnerSelectionLength = focusOwner?.SelectionLength ?? 0;

                    placementTarget.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        cutDetailPop.IsOpen = true;

                        if (focusOwner == null)
                            return;

                        if (!focusOwner.IsKeyboardFocusWithin)
                        {
                            focusOwner.Focus();
                        }

                        string currentText = focusOwner.Text ?? string.Empty;
                        int selectionStart = Math.Max(0, Math.Min(focusOwnerSelectionStart, currentText.Length));
                        int selectionLength = Math.Max(0, Math.Min(focusOwnerSelectionLength, currentText.Length - selectionStart));
                        focusOwner.Select(selectionStart, selectionLength);
                    }), DispatcherPriority.Input);
                }
                void OpenCutInputPopup(Button placementBtn, bool isCustom)
                {
                    CloseSubMenusExcept(cutModePop);
                    if (cutDetailPop != null) cutDetailPop.IsOpen = false;
                    if (cutInputPop != null) cutInputPop.IsOpen = false;

                    cutInputPop = new System.Windows.Controls.Primitives.Popup
                    {
                        PlacementTarget = placementBtn,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                        HorizontalOffset = 1,
                        VerticalOffset = -1.5,
                        AllowsTransparency = true,
                        StaysOpen = true
                    };

                    var root = new StackPanel { Orientation = Orientation.Vertical };
                    var row = new Grid
                    {
                        Margin = new Thickness(10, 6, 10, 6),
                        MinWidth = 90
                    };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var lblSegments = new TextBlock
                    {
                        Text = "数段",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 10, 0)
                    };
                    Grid.SetColumn(lblSegments, 0);

                    var txtSegments = new TextBox
                    {
                        Width = 50,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(2, 0, 2, 0),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    AttachDimIntegerValidation(txtSegments);
                    Grid.SetColumn(txtSegments, 1);

                    row.Children.Add(lblSegments);
                    row.Children.Add(txtSegments);

                    void TryOpenCustomDetail()
                    {
                        if (!isCustom) return;
                        if (TryParsePositiveInt(txtSegments.Text, out int n) && n >= 2)
                            OpenCutDetailPopup(txtSegments, n);
                        else if (cutDetailPop != null)
                            cutDetailPop.IsOpen = false;
                    }

                    void TryApplyEqualCutAndClose()
                    {
                        if (isCustom) return;
                        if (!TryParsePositiveInt(txtSegments.Text, out int n) || n < 2) return;

                        if (ApplyEqualCutToOrangeSegment(owner, key, n))
                        {
                            CloseAll();
                            Redraw(canvas, owner);
                        }
                    }

                    txtSegments.TextChanged += (_, __) =>
                    {
                        if (!isCustom)
                            return;

                        TryOpenCustomDetail();
                    };
                    txtSegments.KeyDown += (_, ee) =>
                    {
                        if (ee.Key == Key.Enter)
                        {
                            TryOpenCustomDetail();
                            TryApplyEqualCutAndClose();
                            ee.Handled = true;
                        }
                    };

                    root.Children.Add(WithRowDivider(row));

                    cutInputPop.Child = WrapBox(root);

                    placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        cutInputPop.IsOpen = true;
                        txtSegments.Focus();
                        txtSegments.SelectAll();
                    }), DispatcherPriority.Input);
                }

                void OpenCutModePopup(Button placementBtn)
                {
                    int openRequestId = unchecked(++mainPopupOpenRequestId);
                    CloseSubMenus();
                    if (cutModePop != null) cutModePop.IsOpen = false;

                    cutModePop = new System.Windows.Controls.Primitives.Popup
                    {
                        PlacementTarget = placementBtn,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                        HorizontalOffset = 1,
                        VerticalOffset = -1.5,
                        AllowsTransparency = true,
                        StaysOpen = true
                    };

                    var root = new StackPanel { Orientation = Orientation.Vertical };
                    var btnEqual = MakeMenuButton("等分切断", hasNext: true, minWidth: MENU2_MIN_WIDTH);
                    var btnCustom = MakeMenuButton("任意切断", hasNext: true, minWidth: MENU2_MIN_WIDTH);

                    btnEqual.MouseEnter += (_, __) =>
                    {
                        SelectCut(btnEqual);
                        OpenCutInputPopup(btnEqual, isCustom: false);
                    };
                    btnEqual.Click += (_, __) =>
                    {
                        SelectCut(btnEqual);
                        OpenCutInputPopup(btnEqual, isCustom: false);
                    };

                    btnCustom.MouseEnter += (_, __) =>
                    {
                        SelectCut(btnCustom);
                        OpenCutInputPopup(btnCustom, isCustom: true);
                    };
                    btnCustom.Click += (_, __) =>
                    {
                        SelectCut(btnCustom);
                        OpenCutInputPopup(btnCustom, isCustom: true);
                    };

                    root.Children.Add(WithRowDivider(btnEqual));
                    root.Children.Add(WithRowDivider(btnCustom));
                    cutModePop.Child = WrapBox(root);

                    placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (openRequestId != mainPopupOpenRequestId) return;
                        if (!ReferenceEquals(selectedMainBtn, placementBtn)) return;
                        cutModePop.IsOpen = true;
                        SelectCut(null);
                    }), DispatcherPriority.Input);
                }

                void OpenAnkaPopup(Button placementBtn)
                {
                    int openRequestId = unchecked(++mainPopupOpenRequestId);
                    CloseSubMenus();
                    if (ankaPop != null) ankaPop.IsOpen = false;

                    ankaPop = new System.Windows.Controls.Primitives.Popup
                    {
                        PlacementTarget = placementBtn,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                        HorizontalOffset = 1,
                        VerticalOffset = -1.5,
                        AllowsTransparency = true,
                        StaysOpen = true
                    };

                    tb.Tag = Tuple.Create(mainPop,
                                          ankaPop,
                                          sidePop ?? new System.Windows.Controls.Primitives.Popup(),
                                          lenPop ?? new System.Windows.Controls.Primitives.Popup(),
                                          lenDirPop ?? new System.Windows.Controls.Primitives.Popup());

                    var root = new StackPanel { Orientation = Orientation.Vertical };

                    var btnLeft = MakeMenuButton("左", hasNext: true, minWidth: MENU2_MIN_WIDTH);
                    btnLeft.MouseEnter += (_, __) =>
                    {
                        CancelActiveAnkaEdit();
                        SelectAnka(btnLeft);
                        OpenSidePopup(btnLeft, AnkaSide.Left);
                    };
                    btnLeft.Click += (_, __) =>
                    {
                        CancelActiveAnkaEdit();
                        SelectAnka(btnLeft);
                        OpenSidePopup(btnLeft, AnkaSide.Left);
                    };

                    var btnRight = MakeMenuButton("右", hasNext: true, minWidth: MENU2_MIN_WIDTH);
                    btnRight.MouseEnter += (_, __) =>
                    {
                        CancelActiveAnkaEdit();
                        SelectAnka(btnRight);
                        OpenSidePopup(btnRight, AnkaSide.Right);
                    };
                    btnRight.Click += (_, __) =>
                    {
                        CancelActiveAnkaEdit();
                        SelectAnka(btnRight);
                        OpenSidePopup(btnRight, AnkaSide.Right);
                    };

                    root.Children.Add(WithRowDivider(btnLeft));
                    root.Children.Add(WithRowDivider(btnRight));

                    ankaPop.Child = WrapBox(root);

                    placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (openRequestId != mainPopupOpenRequestId) return;
                        if (!ReferenceEquals(selectedMainBtn, placementBtn)) return;
                        ankaPop.IsOpen = true;
                        SelectAnka(null);
                    }), DispatcherPriority.Input);
                }

                var mainRoot = new StackPanel { Orientation = Orientation.Vertical };

                var btnAnka = MakeMenuButton("アンカ", hasNext: true, minWidth: MENU1_MIN_WIDTH);
                btnAnka.MouseEnter += (_, __) =>
                {
                    CancelActiveAnkaEdit();
                    SelectMain(btnAnka);
                    OpenAnkaPopup(btnAnka);
                };
                btnAnka.Click += (_, __) =>
                {
                    CancelActiveAnkaEdit();
                    SelectMain(btnAnka);
                    OpenAnkaPopup(btnAnka);
                };

                void OpenDDiaPopup(Button placementBtn)
                {
                    int openRequestId = unchecked(++mainPopupOpenRequestId);
                    CloseSubMenus();
                    if (dDiaPop != null) dDiaPop.IsOpen = false;

                    dDiaPop = new System.Windows.Controls.Primitives.Popup
                    {
                        PlacementTarget = placementBtn,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                        HorizontalOffset = 1,
                        VerticalOffset = -1.5,
                        AllowsTransparency = true,
                        StaysOpen = true
                    };

                    TrySplitTopParts(tb.Text, out var curD, out var curLen);
                    var root = new StackPanel { Orientation = Orientation.Vertical };

                    foreach (var dia in _standardRebarDiameters)
                    {
                        var d = dia;
                        bool isChecked = string.Equals(d, curD, StringComparison.Ordinal);

                        var rowPanel = new DockPanel { LastChildFill = true };
                        var check = new TextBlock
                        {
                            Text = isChecked ? "✓" : "",
                            Width = 20,
                            Margin = new Thickness(0, 0, 0, 0),
                            VerticalAlignment = VerticalAlignment.Center,
                            HorizontalAlignment = HorizontalAlignment.Left
                        };
                        DockPanel.SetDock(check, Dock.Left);

                        var label = new TextBlock
                        {
                            Text = d,
                            VerticalAlignment = VerticalAlignment.Center
                        };

                        rowPanel.Children.Add(check);
                        rowPanel.Children.Add(label);

                        var rowBtn = new Button
                        {
                            Content = rowPanel,
                            HorizontalContentAlignment = HorizontalAlignment.Stretch,
                            VerticalContentAlignment = VerticalAlignment.Center,
                            Padding = new Thickness(12, 6, 12, 6),
                            Background = normalBg,
                            BorderBrush = Brushes.Transparent,
                            BorderThickness = new Thickness(0),
                            MinWidth = MENU2_MIN_WIDTH,
                            OverridesDefaultStyle = true,
                            Template = GetFlatBtnTemplate(),
                            Focusable = false,
                            IsTabStop = false
                        };

                        var rowHost = new Border { Child = rowBtn, Background = normalBg };

                        rowBtn.MouseEnter += (_, __) => SelectDRow(rowHost);
                        rowBtn.Click += (_, __) =>
                        {
                            SelectDRow(rowHost);
                            string newWhole = $"D{d}-{curLen}";
                            if (SetOrangeDimText(owner, key, newWhole))
                                Redraw(canvas, owner);
                            CloseAll();
                        };

                        root.Children.Add(WithRowDivider(rowHost));
                    }

                    dDiaPop.Child = WrapBox(root);
                    placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (openRequestId != mainPopupOpenRequestId) return;
                        if (!ReferenceEquals(selectedMainBtn, placementBtn)) return;
                        dDiaPop.IsOpen = true;
                        SelectDRow(null);
                    }), DispatcherPriority.Input);
                }

                var btnD = MakeMenuButton("D", hasNext: true, minWidth: MENU1_MIN_WIDTH);
                btnD.MouseEnter += (_, __) =>
                {
                    CancelActiveAnkaEdit();
                    SelectMain(btnD);
                    OpenDDiaPopup(btnD);
                };
                btnD.Click += (_, __) =>
                {
                    CancelActiveAnkaEdit();
                    SelectMain(btnD);
                    OpenDDiaPopup(btnD);
                };

                var btnLen = MakeMenuButton("長さ", hasNext: true, minWidth: MENU1_MIN_WIDTH);
                btnLen.MouseEnter += (_, __) =>
                {
                    CancelActiveAnkaEdit();
                    SelectMain(btnLen);
                    OpenLenPopup(btnLen);
                };
                btnLen.Click += (_, __) =>
                {
                    CancelActiveAnkaEdit();
                    SelectMain(btnLen);
                    OpenLenPopup(btnLen);
                };

                var btnCut = MakeMenuButton("鉄筋を切る", hasNext: true, minWidth: MENU1_MIN_WIDTH);
                btnCut.MouseEnter += (_, __) =>
                {
                    CancelActiveAnkaEdit();
                    SelectMain(btnCut);
                    OpenCutModePopup(btnCut);
                };
                btnCut.Click += (_, __) =>
                {
                    CancelActiveAnkaEdit();
                    SelectMain(btnCut);
                    OpenCutModePopup(btnCut);
                };

                var btnDel = MakeMenuButton("鉄筋を削除", hasNext: false, minWidth: MENU1_MIN_WIDTH);
                btnDel.MouseEnter += (_, __) =>
                {
                    CancelActiveAnkaEdit();
                    InvalidatePendingMainPopupOpen();
                    CloseSubMenus();
                    SelectMain(btnDel);
                };
                btnDel.Click += (_, __) =>
                {
                    CancelActiveAnkaEdit();
                    InvalidatePendingMainPopupOpen();
                    CloseSubMenus();
                    SelectMain(btnDel);
                    CloseAll();

                    if (MessageBox.Show(
                        "この部分を削除してもよろしいでしょうか？", "削除確認",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning) == MessageBoxResult.Yes)
                    {
                        DeleteOrangeDimSegment(owner, key);
                        Redraw(canvas, owner);
                    }
                };

                //var btnReset = MakeMenuButton("リセット", hasNext: false, minWidth: MENU1_MIN_WIDTH);
                //btnReset.MouseEnter += (_, __) =>
                //{
                //    CancelActiveAnkaEdit();
                //    CloseSubMenus();
                //    SelectMain(btnReset);
                //};
                //btnReset.Click += (_, __) =>
                //{
                //    CancelActiveAnkaEdit();
                //    CloseSubMenus();
                //    SelectMain(btnReset);
                //    CloseAll();
                //    if (SetOrangeDimText(owner, key, ""))
                //        Redraw(canvas, owner);
                //};

                mainRoot.Children.Add(WithRowDivider(btnAnka));
                mainRoot.Children.Add(WithRowDivider(btnD));
                mainRoot.Children.Add(WithRowDivider(btnLen));
                mainRoot.Children.Add(WithRowDivider(btnCut));
                mainRoot.Children.Add(WithRowDivider(btnDel));
                //mainRoot.Children.Add(WithRowDivider(btnReset));

                mainPop.Child = WrapBox(mainRoot);

                tb.Dispatcher.BeginInvoke(new Action(() =>
                {
                    mainPop.IsOpen = true;
                    SelectMain(null);

                    tb.Tag = Tuple.Create(mainPop,
                                          ankaPop ?? new System.Windows.Controls.Primitives.Popup(),
                                          sidePop ?? new System.Windows.Controls.Primitives.Popup(),
                                          lenPop ?? new System.Windows.Controls.Primitives.Popup(),
                                          lenDirPop ?? new System.Windows.Controls.Primitives.Popup());
                }), DispatcherPriority.Input);
            };
        }

        /// <summary>
        /// end 
        /// </summary>
        //private readonly Dictionary<GridBotsecozu, Dictionary<int, double>> _tanbuHookOverrides
        //    = new Dictionary<GridBotsecozu, Dictionary<int, double>>();
        private readonly Dictionary<GridBotsecozu, Dictionary<(int spanIndex, bool isRightAbdominal, bool isRightEnd), double>>
        _tanbuHookOverrides = new Dictionary<GridBotsecozu, Dictionary<(int spanIndex, bool isRightAbdominal, bool isRightEnd), double>>();
        private readonly Dictionary<GridBotsecozu, Dictionary<(int spanIndex, bool isRightAbdominal), string>>
        _tanbuDiameterOverrides = new Dictionary<GridBotsecozu, Dictionary<(int spanIndex, bool isRightAbdominal), string>>();

        private static string MakeTanbuHookKey(int spanIndex, bool isRightAbdominal, bool isRightEnd)
            => $"{spanIndex}|{(isRightAbdominal ? 1 : 0)}|{(isRightEnd ? 1 : 0)}";

        private static string MakeTanbuDiameterKey(int spanIndex, bool isRightAbdominal)
            => $"{spanIndex}|{(isRightAbdominal ? 1 : 0)}";

        private static bool TryParseTanbuHookKey(string key, out (int spanIndex, bool isRightAbdominal, bool isRightEnd) parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(key)) return false;

            var parts = key.Split('|');
            if (parts.Length != 3) return false;

            if (!int.TryParse(parts[0], out var spanIndex)) return false;
            if (!int.TryParse(parts[1], out var abdominalFlag)) return false;
            if (!int.TryParse(parts[2], out var endFlag)) return false;

            parsed = (spanIndex, abdominalFlag != 0, endFlag != 0);
            return true;
        }

        private static bool TryParseTanbuDiameterKey(string key, out (int spanIndex, bool isRightAbdominal) parsed)
        {
            parsed = default;
            if (string.IsNullOrWhiteSpace(key)) return false;

            var parts = key.Split('|');
            if (parts.Length != 2) return false;

            if (!int.TryParse(parts[0], out var spanIndex)) return false;
            if (!int.TryParse(parts[1], out var abdominalFlag)) return false;

            parsed = (spanIndex, abdominalFlag != 0);
            return true;
        }

        private string FormatTanbuLabel(string dia, double hookLength)
        {
            dia = string.IsNullOrWhiteSpace(dia) ? string.Empty : dia;
            return $"D{dia}- {hookLength}";
        }

        private string GetTanbuDiameter(GridBotsecozu item, int spanIndex, bool isRightAbdominal, string fallback)
        {
            if (item != null
                && _tanbuDiameterOverrides.TryGetValue(item, out var spanDict)
                && spanDict != null
                && spanDict.TryGetValue((spanIndex, isRightAbdominal), out var val)
                && !string.IsNullOrWhiteSpace(val))
            {
                return val;
            }

            if (item?.TanbuDiameterOverrides != null
                && item.TanbuDiameterOverrides.TryGetValue(MakeTanbuDiameterKey(spanIndex, isRightAbdominal), out var persisted)
                && !string.IsNullOrWhiteSpace(persisted))
            {
                return persisted.Trim();
            }

            return string.IsNullOrWhiteSpace(fallback) ? string.Empty : fallback.Trim();
        }

        private bool SetTanbuDiameter(GridBotsecozu item, int spanIndex, bool isRightAbdominal, string newDiameter)
        {
            if (item == null || spanIndex < 0 || string.IsNullOrWhiteSpace(newDiameter))
                return false;

            string normalized = newDiameter.Trim();

            if (!_tanbuDiameterOverrides.TryGetValue(item, out var spanDict) || spanDict == null)
            {
                spanDict = new Dictionary<(int spanIndex, bool isRightAbdominal), string>();
                _tanbuDiameterOverrides[item] = spanDict;
            }

            bool changed = !spanDict.TryGetValue((spanIndex, isRightAbdominal), out var existing)
                           || !string.Equals(existing, normalized, StringComparison.Ordinal);
            spanDict[(spanIndex, isRightAbdominal)] = normalized;

            if (item.TanbuDiameterOverrides == null)
                item.TanbuDiameterOverrides = new Dictionary<string, string>();

            var persistedKey = MakeTanbuDiameterKey(spanIndex, isRightAbdominal);
            changed = changed
                      || !item.TanbuDiameterOverrides.TryGetValue(persistedKey, out var persistedExisting)
                      || !string.Equals(persistedExisting, normalized, StringComparison.Ordinal);
            item.TanbuDiameterOverrides[persistedKey] = normalized;

            if (changed)
                PropertyChangeTracker.MarkChanged();

            return changed;
        }

        //private double GetTanbuHookLength(GridBotsecozu item, int spanIndex, double fallback)
        private double GetTanbuHookLength(GridBotsecozu item, int spanIndex, bool isRightAbdominal, bool isRightEnd, double fallback)
        {
            if (item != null
                && _tanbuHookOverrides.TryGetValue(item, out var spanDict)
                && spanDict.TryGetValue((spanIndex, isRightAbdominal, isRightEnd), out var val))
            {
                return val;
            }

            if (item?.TanbuHookOverrides != null
                && item.TanbuHookOverrides.TryGetValue(MakeTanbuHookKey(spanIndex, isRightAbdominal, isRightEnd), out var persisted))
            {
                return persisted;
            }

            return fallback;
        }

        //private void SetTanbuHookLength(GridBotsecozu item, int spanIndex, double newLength)
        private bool SetTanbuHookLength(GridBotsecozu item, int spanIndex, bool isRightAbdominal, bool isRightEnd, double newLength)
        {
            if (item == null || spanIndex < 0 || double.IsNaN(newLength) || double.IsInfinity(newLength))
                return false;

            if (!_tanbuHookOverrides.TryGetValue(item, out var spanDict))
            {
                spanDict = new Dictionary<(int spanIndex, bool isRightAbdominal, bool isRightEnd), double>();
                _tanbuHookOverrides[item] = spanDict;
            }

            bool changed = !spanDict.TryGetValue((spanIndex, isRightAbdominal, isRightEnd), out var existing)
                           || Math.Abs(existing - newLength) > 1e-6;

            spanDict[(spanIndex, isRightAbdominal, isRightEnd)] = newLength;

            if (item.TanbuHookOverrides == null)
                item.TanbuHookOverrides = new Dictionary<string, double>();

            var persistedKey = MakeTanbuHookKey(spanIndex, isRightAbdominal, isRightEnd);
            changed = changed
                      || !item.TanbuHookOverrides.TryGetValue(persistedKey, out var persistedExisting)
                      || Math.Abs(persistedExisting - newLength) > 1e-6;
            item.TanbuHookOverrides[persistedKey] = newLength;

            if (changed)
                PropertyChangeTracker.MarkChanged();

            return changed;
        }
        private void ShowComboEditor(Canvas canvas, TextBlock tb, WCTransform T,
                                      double wx, double wy,
                                      HAnchor ha, VAnchor va,
                                      Func<IEnumerable<string>> optionsProvider,
                                      Func<string> currentValueProvider,
                                      Action<string> commitAction)
        {
            if (tb == null || canvas == null)
                return;

            var baseOptions = optionsProvider?.Invoke() ?? Array.Empty<string>();
            var optionList = baseOptions.ToList();
            string current = currentValueProvider?.Invoke();
            if (!string.IsNullOrWhiteSpace(current) && !optionList.Contains(current))
                optionList.Add(current);

            var combo = new ComboBox
            {
                ItemsSource = optionList,
                FontSize = tb.FontSize,
                IsEditable = false
            };

            if (!string.IsNullOrWhiteSpace(current) && optionList.Contains(current))
                combo.SelectedItem = current;

            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double textWidth = tb.DesiredSize.Width;
            double textHeight = tb.DesiredSize.Height;

            combo.MinWidth = Math.Max(80, textWidth + 20);
            combo.Height = Math.Max(24, textHeight + 8);
            combo.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var comboSize = combo.DesiredSize;
            if (double.IsNaN(comboSize.Width) || comboSize.Width <= 0)
                comboSize = new Size(combo.MinWidth, combo.Height);

            var anchor = T.P(wx, wy);
            double left = anchor.X;
            double top = anchor.Y;

            switch (ha)
            {
                case HAnchor.Center: left -= comboSize.Width / 2.0; break;
                case HAnchor.Right: left -= comboSize.Width; break;
            }
            switch (va)
            {
                case VAnchor.Middle: top -= comboSize.Height / 2.0; break;
                case VAnchor.Bottom: top -= comboSize.Height; break;
            }

            Canvas.SetLeft(combo, left);
            Canvas.SetTop(combo, top);
            Panel.SetZIndex(combo, 3000);

            tb.Visibility = System.Windows.Visibility.Hidden;
            canvas.Children.Add(combo);

            combo.Loaded += (_, __) =>
            {
                combo.Focus();
                combo.IsDropDownOpen = true;
            };

            bool finishing = false;
            bool cancelled = false;

            void Finish(bool apply)
            {
                if (finishing) return;
                finishing = true;

                canvas.Children.Remove(combo);
                tb.Visibility = System.Windows.Visibility.Visible;

                if (apply)
                {
                    var selected = combo.SelectedItem as string;
                    if (string.IsNullOrWhiteSpace(selected))
                        selected = combo.Text;

                    if (!string.IsNullOrWhiteSpace(selected))
                        commitAction?.Invoke(selected);
                }
            }

            combo.DropDownClosed += (_, __) =>
            {
                if (!cancelled) Finish(true);
            };

            combo.LostKeyboardFocus += (_, __) =>
            {
                if (combo.IsDropDownOpen)
                    return;

                if (!cancelled)
                    Finish(true);
            };

            combo.KeyDown += (ks, ke) =>
            {
                if (ke.Key == Key.Enter)
                {
                    Finish(true);
                    ke.Handled = true;
                }
                else if (ke.Key == Key.Escape)
                {
                    cancelled = true;
                    Finish(false);
                    ke.Handled = true;
                }
            };
        }

        private void ShowHookLengthEditor(Canvas canvas, TextBlock tb, WCTransform T,
                                           double wx, double wy,
                                           HAnchor ha, VAnchor va,
                                           Func<double> currentValueProvider,
                                           Action<double> commitAction)
        {
            if (tb == null || canvas == null)
                return;

            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var sz = tb.DesiredSize;

            var editor = new TextBox
            {
                Text = currentValueProvider?.Invoke().ToString(CultureInfo.InvariantCulture),
                FontSize = tb.FontSize,
                Width = Math.Max(80, sz.Width + 20),
                Height = Math.Max(26, sz.Height + 6),
                Background = Brushes.White
            };

            var anchor = T.P(wx, wy);
            double left = anchor.X;
            double top = anchor.Y;

            switch (ha)
            {
                case HAnchor.Center: left -= editor.Width / 2.0; break;
                case HAnchor.Right: left -= editor.Width; break;
            }
            switch (va)
            {
                case VAnchor.Middle: top -= editor.Height / 2.0; break;
                case VAnchor.Bottom: top -= editor.Height; break;
            }

            Canvas.SetLeft(editor, left);
            Canvas.SetTop(editor, top);
            Panel.SetZIndex(editor, 3000);

            tb.Visibility = System.Windows.Visibility.Hidden;
            canvas.Children.Add(editor);

            editor.Loaded += (_, __) =>
            {
                editor.Focus();
                editor.SelectAll();
            };

            bool finishing = false;

            void Finish(bool apply)
            {
                if (finishing) return;
                finishing = true;

                canvas.Children.Remove(editor);
                tb.Visibility = System.Windows.Visibility.Visible;

                if (apply)
                {
                    if (double.TryParse(editor.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var len)
                        || double.TryParse(editor.Text, out len))
                    {
                        if (len > 0)
                            commitAction?.Invoke(len);
                    }
                }
            }

            editor.KeyDown += (ks, ke) =>
            {
                if (ke.Key == Key.Enter)
                {
                    Finish(true);
                    ke.Handled = true;
                }
                else if (ke.Key == Key.Escape)
                {
                    Finish(false);
                    ke.Handled = true;
                }
            };

            editor.LostKeyboardFocus += (_, __) => Finish(true);
        }



        private bool ApplyCentralStirrupValues(string kai, string gSym,
                                               string diameter, string pitch, string material)
        {
            var beam = FindBeamBySymbol(kai, gSym);
            if (beam == null)
                return false;

            var layout = beam.梁の配置 ?? (beam.梁の配置 = new Z梁の配置());
            bool changed = false;

            if (!string.IsNullOrWhiteSpace(diameter))
            {
                string dia = diameter.Trim();
                if (!string.Equals(layout.中央スタラップ径, dia, StringComparison.Ordinal))
                {
                    layout.中央スタラップ径 = dia;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(pitch))
            {
                string ptc = pitch.Trim();
                if (!string.Equals(layout.中央ピッチ, ptc, StringComparison.Ordinal))
                {
                    layout.中央ピッチ = ptc;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(material))
            {
                string mat = material.Trim();
                if (!string.Equals(layout.中央スタラップ材質, mat, StringComparison.Ordinal))
                {
                    layout.中央スタラップ材質 = mat;
                    changed = true;
                }
            }

            return changed;
        }

        private IEnumerable<string> GetStirrupMaterialOptions()
            => new[] { "SD295", "SD345", "SD390", "SD490", "ウルボン" };




        private 梁 FindBeamBySymbol(string kai, string gSym)
        {
            if (string.IsNullOrWhiteSpace(gSym))
                gSym = "G0";

            var floorBeamList = _projectData?.リスト?.梁リスト?
                .FirstOrDefault(r => r?.各階 == kai);
            if (floorBeamList == null)
                return null;

            return floorBeamList.梁?.FirstOrDefault(b => b?.Name == gSym)
                   ?? floorBeamList.梁?.FirstOrDefault();
        }

        private bool TryParseBeamDimensions(string text, out double width, out double height)
        {
            width = 0;
            height = 0;

            if (string.IsNullOrWhiteSpace(text))
                return false;

            string cleaned = text.Trim();
            cleaned = cleaned.Trim('(', ')');
            cleaned = cleaned.Replace("×", "x").Replace("X", "x");

            var parts = cleaned.Split(new[] { 'x', '*', ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
                return false;

            bool ParsePart(string s, out double val)
                => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out val)
                || double.TryParse(s, out val);

            return ParsePart(parts[0], out width)
                && ParsePart(parts[1], out height)
                && width > 0
                && height > 0;
        }

        private static string MakeSpanSizeKey(int spanIndex)
            => spanIndex.ToString(CultureInfo.InvariantCulture);

        private (string width, string height) GetSpanBeamDimensions(
            GridBotsecozu item,
            int spanIndex,
            string initialWidth,
            string initialHeight)
        {
            if (item == null || spanIndex < 0)
                return (initialWidth, initialHeight);

            string key = MakeSpanSizeKey(spanIndex);
            string width = initialWidth;
            string height = initialHeight;

            var widthDict = item.SpanWidthOverrides ?? new Dictionary<string, string>();
            if (widthDict.TryGetValue(key, out var savedWidth) && !string.IsNullOrWhiteSpace(savedWidth))
            {
                width = savedWidth;
            }
            else if (!string.IsNullOrWhiteSpace(initialWidth))
            {
                item.SpanWidthOverrides = new Dictionary<string, string>(widthDict)
                {
                    [key] = initialWidth
                };
            }

            var heightDict = item.SpanHeightOverrides ?? new Dictionary<string, string>();
            if (heightDict.TryGetValue(key, out var savedHeight) && !string.IsNullOrWhiteSpace(savedHeight))
            {
                height = savedHeight;
            }
            else if (!string.IsNullOrWhiteSpace(initialHeight))
            {
                item.SpanHeightOverrides = new Dictionary<string, string>(heightDict)
                {
                    [key] = initialHeight
                };
            }

            return (width, height);
        }

        private bool ApplySpanBeamDimensions(GridBotsecozu item, int spanIndex, double width, double height)
        {
            if (item == null || spanIndex < 0)
                return false;

            string key = MakeSpanSizeKey(spanIndex);
            string widthStr = width.ToString(CultureInfo.InvariantCulture);
            string heightStr = height.ToString(CultureInfo.InvariantCulture);
            bool changed = false;

            var widthDict = new Dictionary<string, string>(item.SpanWidthOverrides ?? new Dictionary<string, string>());
            if (!widthDict.TryGetValue(key, out var existingWidth) || existingWidth != widthStr)
            {
                widthDict[key] = widthStr;
                item.SpanWidthOverrides = widthDict;
                changed = true;
            }

            var heightDict = new Dictionary<string, string>(item.SpanHeightOverrides ?? new Dictionary<string, string>());
            if (!heightDict.TryGetValue(key, out var existingHeight) || existingHeight != heightStr)
            {
                heightDict[key] = heightStr;
                item.SpanHeightOverrides = heightDict;
                changed = true;
            }

            return changed;
        }

        private static string GetSpanStringOverride(
            Dictionary<string, string> source,
            string key,
            string fallback)
        {
            if (source != null
                && source.TryGetValue(key, out var saved)
                && !string.IsNullOrWhiteSpace(saved))
            {
                return saved;
            }

            return fallback;
        }

        private static bool SetSpanStringOverride(
            GridBotsecozu item,
            Func<GridBotsecozu, Dictionary<string, string>> getter,
            Action<GridBotsecozu, Dictionary<string, string>> setter,
            string key,
            string value)
        {
            if (item == null || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
                return false;

            string normalized = value.Trim();
            var dict = new Dictionary<string, string>(getter(item) ?? new Dictionary<string, string>());
            if (dict.TryGetValue(key, out var existing)
                && string.Equals(existing, normalized, StringComparison.Ordinal))
            {
                return false;
            }

            dict[key] = normalized;
            setter(item, dict);
            return true;
        }

        private (string diameter, string pitch, string material) GetSpanCentralStirrupValues(
            GridBotsecozu item,
            int spanIndex,
            string initialDiameter,
            string initialPitch,
            string initialMaterial)
        {
            if (item == null || spanIndex < 0)
                return (initialDiameter, initialPitch, initialMaterial);

            string key = MakeSpanSizeKey(spanIndex);

            string diameter = GetSpanStringOverride(item.SpanCentralStirrupDiameterOverrides, key, initialDiameter);
            string pitch = GetSpanStringOverride(item.SpanCentralStirrupPitchOverrides, key, initialPitch);
            string material = GetSpanStringOverride(item.SpanCentralStirrupMaterialOverrides, key, initialMaterial);

            if (!string.IsNullOrWhiteSpace(initialDiameter)
                && (item.SpanCentralStirrupDiameterOverrides == null || !item.SpanCentralStirrupDiameterOverrides.ContainsKey(key)))
            {
                SetSpanStringOverride(item, x => x.SpanCentralStirrupDiameterOverrides, (x, d) => x.SpanCentralStirrupDiameterOverrides = d, key, initialDiameter);
            }

            if (!string.IsNullOrWhiteSpace(initialPitch)
                && (item.SpanCentralStirrupPitchOverrides == null || !item.SpanCentralStirrupPitchOverrides.ContainsKey(key)))
            {
                SetSpanStringOverride(item, x => x.SpanCentralStirrupPitchOverrides, (x, d) => x.SpanCentralStirrupPitchOverrides = d, key, initialPitch);
            }

            if (!string.IsNullOrWhiteSpace(initialMaterial)
                && (item.SpanCentralStirrupMaterialOverrides == null || !item.SpanCentralStirrupMaterialOverrides.ContainsKey(key)))
            {
                SetSpanStringOverride(item, x => x.SpanCentralStirrupMaterialOverrides, (x, d) => x.SpanCentralStirrupMaterialOverrides = d, key, initialMaterial);
            }

            return (diameter, pitch, material);
        }

        private bool ApplySpanCentralStirrupValues(
            GridBotsecozu item,
            int spanIndex,
            string diameter,
            string pitch,
            string material)
        {
            if (item == null || spanIndex < 0)
                return false;

            string key = MakeSpanSizeKey(spanIndex);
            bool changed = false;

            if (!string.IsNullOrWhiteSpace(diameter))
                changed = SetSpanStringOverride(item, x => x.SpanCentralStirrupDiameterOverrides, (x, d) => x.SpanCentralStirrupDiameterOverrides = d, key, diameter) || changed;
            if (!string.IsNullOrWhiteSpace(pitch))
                changed = SetSpanStringOverride(item, x => x.SpanCentralStirrupPitchOverrides, (x, d) => x.SpanCentralStirrupPitchOverrides = d, key, pitch) || changed;
            if (!string.IsNullOrWhiteSpace(material))
                changed = SetSpanStringOverride(item, x => x.SpanCentralStirrupMaterialOverrides, (x, d) => x.SpanCentralStirrupMaterialOverrides = d, key, material) || changed;

            return changed;
        }

        private int GetSpanCentralStirrupShape(
            GridBotsecozu item,
            int spanIndex,
            int initialShape)
        {
            if (item == null || spanIndex < 0)
                return initialShape;

            string key = MakeSpanSizeKey(spanIndex);
            string initialValue = initialShape.ToString(CultureInfo.InvariantCulture);
            string value = GetSpanStringOverride(item.SpanCentralStirrupShapeOverrides, key, initialValue);

            if (item.SpanCentralStirrupShapeOverrides == null || !item.SpanCentralStirrupShapeOverrides.ContainsKey(key))
            {
                SetSpanStringOverride(item, x => x.SpanCentralStirrupShapeOverrides, (x, d) => x.SpanCentralStirrupShapeOverrides = d, key, initialValue);
            }

            int parsedShape;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedShape) || parsedShape < 1 || parsedShape > 9)
                return initialShape;

            return parsedShape;
        }

        private bool ApplySpanCentralStirrupShape(
            GridBotsecozu item,
            int spanIndex,
            int shape)
        {
            if (item == null || spanIndex < 0 || shape < 1 || shape > 9)
                return false;

            string key = MakeSpanSizeKey(spanIndex);
            return SetSpanStringOverride(
                item,
                x => x.SpanCentralStirrupShapeOverrides,
                (x, d) => x.SpanCentralStirrupShapeOverrides = d,
                key,
                shape.ToString(CultureInfo.InvariantCulture));
        }

        private (string diameter, string pitch) GetSpanEndWidthStopValues(
            GridBotsecozu item,
            int spanIndex,
            string initialDiameter,
            string initialPitch)
        {
            if (item == null || spanIndex < 0)
                return (initialDiameter, initialPitch);

            string key = MakeSpanSizeKey(spanIndex);
            string diameter = GetSpanStringOverride(item.SpanEndWidthStopDiameterOverrides, key, initialDiameter);
            string pitch = GetSpanStringOverride(item.SpanEndWidthStopPitchOverrides, key, initialPitch);

            if (!string.IsNullOrWhiteSpace(initialDiameter)
                && (item.SpanEndWidthStopDiameterOverrides == null || !item.SpanEndWidthStopDiameterOverrides.ContainsKey(key)))
            {
                SetSpanStringOverride(item, x => x.SpanEndWidthStopDiameterOverrides, (x, d) => x.SpanEndWidthStopDiameterOverrides = d, key, initialDiameter);
            }

            if (!string.IsNullOrWhiteSpace(initialPitch)
                && (item.SpanEndWidthStopPitchOverrides == null || !item.SpanEndWidthStopPitchOverrides.ContainsKey(key)))
            {
                SetSpanStringOverride(item, x => x.SpanEndWidthStopPitchOverrides, (x, d) => x.SpanEndWidthStopPitchOverrides = d, key, initialPitch);
            }

            return (diameter, pitch);
        }

        private bool ApplySpanEndWidthStopValues(
            GridBotsecozu item,
            int spanIndex,
            string diameter,
            string pitch)
        {
            if (item == null || spanIndex < 0)
                return false;

            string key = MakeSpanSizeKey(spanIndex);
            bool changed = false;

            if (!string.IsNullOrWhiteSpace(diameter))
                changed = SetSpanStringOverride(item, x => x.SpanEndWidthStopDiameterOverrides, (x, d) => x.SpanEndWidthStopDiameterOverrides = d, key, diameter) || changed;
            if (!string.IsNullOrWhiteSpace(pitch))
                changed = SetSpanStringOverride(item, x => x.SpanEndWidthStopPitchOverrides, (x, d) => x.SpanEndWidthStopPitchOverrides = d, key, pitch) || changed;

            return changed;
        }

        private (string diameter, string pitch, string material) GetSpanCentralIntermediateValues(
            GridBotsecozu item,
            int spanIndex,
            string initialDiameter,
            string initialPitch,
            string initialMaterial)
        {
            if (item == null || spanIndex < 0)
                return (initialDiameter, initialPitch, initialMaterial);

            string key = MakeSpanSizeKey(spanIndex);

            string diameter = GetSpanStringOverride(item.SpanCentralIntermediateDiameterOverrides, key, initialDiameter);
            string pitch = GetSpanStringOverride(item.SpanCentralIntermediatePitchOverrides, key, initialPitch);
            string material = GetSpanStringOverride(item.SpanCentralIntermediateMaterialOverrides, key, initialMaterial);

            if (!string.IsNullOrWhiteSpace(initialDiameter)
                && (item.SpanCentralIntermediateDiameterOverrides == null || !item.SpanCentralIntermediateDiameterOverrides.ContainsKey(key)))
            {
                SetSpanStringOverride(item, x => x.SpanCentralIntermediateDiameterOverrides, (x, d) => x.SpanCentralIntermediateDiameterOverrides = d, key, initialDiameter);
            }

            if (!string.IsNullOrWhiteSpace(initialPitch)
                && (item.SpanCentralIntermediatePitchOverrides == null || !item.SpanCentralIntermediatePitchOverrides.ContainsKey(key)))
            {
                SetSpanStringOverride(item, x => x.SpanCentralIntermediatePitchOverrides, (x, d) => x.SpanCentralIntermediatePitchOverrides = d, key, initialPitch);
            }

            if (!string.IsNullOrWhiteSpace(initialMaterial)
                && (item.SpanCentralIntermediateMaterialOverrides == null || !item.SpanCentralIntermediateMaterialOverrides.ContainsKey(key)))
            {
                SetSpanStringOverride(item, x => x.SpanCentralIntermediateMaterialOverrides, (x, d) => x.SpanCentralIntermediateMaterialOverrides = d, key, initialMaterial);
            }

            return (diameter, pitch, material);
        }

        private bool ApplySpanCentralIntermediateValues(
            GridBotsecozu item,
            int spanIndex,
            string diameter,
            string pitch,
            string material)
        {
            if (item == null || spanIndex < 0)
                return false;

            string key = MakeSpanSizeKey(spanIndex);
            bool changed = false;

            if (!string.IsNullOrWhiteSpace(diameter))
                changed = SetSpanStringOverride(item, x => x.SpanCentralIntermediateDiameterOverrides, (x, d) => x.SpanCentralIntermediateDiameterOverrides = d, key, diameter) || changed;
            if (!string.IsNullOrWhiteSpace(pitch))
                changed = SetSpanStringOverride(item, x => x.SpanCentralIntermediatePitchOverrides, (x, d) => x.SpanCentralIntermediatePitchOverrides = d, key, pitch) || changed;
            if (!string.IsNullOrWhiteSpace(material))
                changed = SetSpanStringOverride(item, x => x.SpanCentralIntermediateMaterialOverrides, (x, d) => x.SpanCentralIntermediateMaterialOverrides = d, key, material) || changed;

            return changed;
        }

        private int GetSpanCentralIntermediateShape(
            GridBotsecozu item,
            int spanIndex,
            int initialShape)
        {
            if (item == null || spanIndex < 0)
                return initialShape;

            string key = MakeSpanSizeKey(spanIndex);
            string initialValue = initialShape.ToString(CultureInfo.InvariantCulture);
            string value = GetSpanStringOverride(item.SpanCentralIntermediateShapeOverrides, key, initialValue);

            if (item.SpanCentralIntermediateShapeOverrides == null || !item.SpanCentralIntermediateShapeOverrides.ContainsKey(key))
            {
                SetSpanStringOverride(item, x => x.SpanCentralIntermediateShapeOverrides, (x, d) => x.SpanCentralIntermediateShapeOverrides = d, key, initialValue);
            }

            int parsedShape;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedShape) || parsedShape < 1 || parsedShape > 8)
                return initialShape;

            return parsedShape;
        }

        private bool ApplySpanCentralIntermediateShape(
            GridBotsecozu item,
            int spanIndex,
            int shape)
        {
            if (item == null || spanIndex < 0 || shape < 1 || shape > 8)
                return false;

            string key = MakeSpanSizeKey(spanIndex);
            return SetSpanStringOverride(
                item,
                x => x.SpanCentralIntermediateShapeOverrides,
                (x, d) => x.SpanCentralIntermediateShapeOverrides = d,
                key,
                shape.ToString(CultureInfo.InvariantCulture));
        }

        private bool ApplyBeamDimensions(string kai, string gSym, double width, double height)
        {
            var beam = FindBeamBySymbol(kai, gSym);
            if (beam == null)
                return false;

            var layout = beam.梁の配置 ?? (beam.梁の配置 = new Z梁の配置());
            bool changed = false;

            string widthStr = width.ToString(CultureInfo.InvariantCulture);
            string heightStr = height.ToString(CultureInfo.InvariantCulture);

            if (layout.中央幅 != widthStr)
            {
                layout.中央幅 = widthStr;
                changed = true;
            }

            if (layout.中央成 != heightStr)
            {
                layout.中央成 = heightStr;
                changed = true;
            }

            return changed;
        }

        private void MakeBeamSizeEditable(TextBlock tb, Canvas canvas, WCTransform T,
                                           double wx, double wy,
                                           string kai, string gSym,
                                           GridBotsecozu item,
                                           int spanIndex)
        {
            if (tb == null || canvas == null)
                return;

            tb.Cursor = Cursors.Hand;
            if (tb.Background == null)
                tb.Background = Brushes.Transparent;
            //EnableEditableHover(tb, hoverForeground: Brushes.Brown, underline: true);

            tb.IsHitTestVisible = true;                 // đảm bảo bắt hover/click
                                                        //tb.Padding = new Thickness(6, 2, 6, 2);     // tăng vùng hover cho dễ trúng
            EnableEditableHoverWithBox(tb, canvas,
                hoverForeground: Brushes.Blue,
                hoverFill: new SolidColorBrush(Color.FromArgb(100, 30, 144, 255)),
                hoverStroke: Brushes.Blue);

            System.Windows.Controls.Primitives.Popup beamSizePopup = null;

            tb.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true;

                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                var sz = tb.DesiredSize;
                double popupFontSize = SystemFonts.MessageFontSize;

                double initW = 0;
                double initH = 0;
                TryParseBeamDimensions(tb.Text, out initW, out initH);

                //bool ParseNumber(string text, out double val)
                //    => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out val)
                //    || double.TryParse(text, out val);

                bool ParseNumber(string text, out double val)
                {
                    return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out val)
                           || double.TryParse(text, out val);
                }

                TextBox CreateNumericBox(string value)
                {
                    return new TextBox
                    {
                        Text = value,
                        FontSize = popupFontSize,
                        Width = Math.Max(60, sz.Width / 3.0),
                        MinWidth = 60,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(2, 0, 2, 0),
                        Background = Brushes.Transparent,
                        Foreground = Brushes.Black,
                        Visibility = System.Windows.Visibility.Visible,
                        IsReadOnly = true,
                        BorderThickness = new Thickness(0),
                        BorderBrush = Brushes.Transparent
                    };
                }

                var widthBox = CreateNumericBox(initW > 0 ? initW.ToString(CultureInfo.InvariantCulture) : string.Empty);
                var heightBox = CreateNumericBox(initH > 0 ? initH.ToString(CultureInfo.InvariantCulture) : string.Empty);
                try
                {
                    if (beamSizePopup != null)
                    {
                        beamSizePopup.IsOpen = false;
                        beamSizePopup = null;
                    }
                }
                catch { }

                Brush normalBg = Brushes.Transparent;
                Brush selectedBg = new SolidColorBrush(Color.FromRgb(210, 225, 255));
                Brush dividerBrush = PopupMenuDividerBrush;

                UIElement WithRowDivider(UIElement child)
                {
                    return new Border
                    {
                        BorderBrush = dividerBrush,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Child = child
                    };
                }

                ControlTemplate flatBtnTemplate = null;
                ControlTemplate GetFlatBtnTemplate()
                {
                    if (flatBtnTemplate != null) return flatBtnTemplate;

                    var border = new FrameworkElementFactory(typeof(Border));
                    border.SetValue(Border.SnapsToDevicePixelsProperty, true);
                    border.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                    border.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                    border.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                    border.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

                    var cp = new FrameworkElementFactory(typeof(ContentPresenter));
                    cp.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
                    cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
                    cp.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Button.VerticalContentAlignmentProperty));
                    border.AppendChild(cp);

                    flatBtnTemplate = new ControlTemplate(typeof(Button)) { VisualTree = border };
                    return flatBtnTemplate;
                }

                Border WrapBox(UIElement child)
                {
                    var host = new ContentControl
                    {
                        Content = child,
                        Width = 100,
                        FontSize = SystemFonts.MessageFontSize,
                        FontFamily = SystemFonts.MessageFontFamily
                    };

                    return new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = PopupMenuBorderBrush,
                        BorderThickness = PopupMenuBorderThickness,
                        CornerRadius = new CornerRadius(2),
                        Child = host
                    };
                }

                Button CreateRowButton(string label, TextBox editor)
                {
                    var row = new DockPanel { LastChildFill = true, Width = 90 };

                    var lbl = new TextBlock
                    {
                        Text = label,
                        FontSize = popupFontSize,
                        Foreground = Brushes.Black,
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(lbl, Dock.Left);
                    DockPanel.SetDock(editor, Dock.Right);

                    row.Children.Add(lbl);
                    row.Children.Add(editor);

                    var rowButton = new Button
                    {
                        Content = row,
                        HorizontalContentAlignment = HorizontalAlignment.Left,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(10, 6, 10, 6),
                        Background = normalBg,
                        BorderBrush = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        MinWidth = 150,
                        OverridesDefaultStyle = true,
                        Template = GetFlatBtnTemplate(),
                        Focusable = false,
                        IsTabStop = false
                    };

                    rowButton.MouseEnter += (_, __) => rowButton.Background = selectedBg;
                    rowButton.MouseLeave += (_, __) =>
                    {
                        SetDisplayMode(editor);
                        rowButton.Background = normalBg;
                    };

                    return rowButton;
                }

                Button rowWidth = null;
                Button rowHeight = null;

                void SetDisplayMode(TextBox editor)
                {
                    editor.IsReadOnly = true;
                    editor.BorderThickness = new Thickness(0);
                    editor.BorderBrush = Brushes.Transparent;
                    editor.Background = Brushes.Transparent;
                }

                void SetEditMode(TextBox editor)
                {
                    editor.IsReadOnly = false;
                    editor.BorderThickness = new Thickness(1);
                    editor.BorderBrush = PopupMenuEditorBorderBrush;
                    editor.Background = Brushes.White;
                }

                void HideEditors()
                {
                    SetDisplayMode(widthBox);
                    SetDisplayMode(heightBox);
                    widthBox.Select(widthBox.Text?.Length ?? 0, 0);
                    heightBox.Select(heightBox.Text?.Length ?? 0, 0);
                    Keyboard.ClearFocus();
                    canvas.Focus();
                    if (rowWidth != null) rowWidth.Background = normalBg;
                    if (rowHeight != null) rowHeight.Background = normalBg;
                }

                void FocusEditor(TextBox editor)
                {
                    editor.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        editor.Focus();
                        editor.SelectAll();
                    }), DispatcherPriority.Input);
                }

                void BeginPopupEdit(TextBox editor, Button rowButton)
                {
                    HideEditors();
                    SetEditMode(editor);
                    rowButton.Background = selectedBg;
                    FocusEditor(editor);
                }

                void CloseBeamSizePopup()
                {
                    try
                    {
                        if (beamSizePopup != null)
                            beamSizePopup.IsOpen = false;
                    }
                    catch { }
                    beamSizePopup = null;
                }

                bool TryCommitAndRedraw()
                {
                    double newW = 0;
                    double newH = 0;

                    bool okW = ParseNumber(widthBox.Text, out newW);
                    bool okH = ParseNumber(heightBox.Text, out newH);
                    if (!okW || !okH)
                        return false;

                    if (!ApplySpanBeamDimensions(item, spanIndex, newW, newH))
                        return false;

                    Redraw(canvas, item);
                    CloseBeamSizePopup();
                    return true;
                }

                rowWidth = CreateRowButton("\u5E45", widthBox);
                rowHeight = CreateRowButton("\u6210", heightBox);

                rowWidth.Click += (_, clickArgs) =>
                {
                    clickArgs.Handled = true;
                    BeginPopupEdit(widthBox, rowWidth);
                };
                rowHeight.Click += (_, clickArgs) =>
                {
                    clickArgs.Handled = true;
                    BeginPopupEdit(heightBox, rowHeight);
                };

                void HandleKeyDown(object ks, KeyEventArgs ke)
                {
                    if (ke.Key == Key.Enter)
                    {
                        if (!TryCommitAndRedraw())
                            FocusEditor((TextBox)ks);
                        ke.Handled = true;
                    }
                    else if (ke.Key == Key.Escape)
                    {
                        HideEditors();
                        CloseBeamSizePopup();
                        ke.Handled = true;
                    }
                    else if (ke.Key == Key.Tab)
                    {
                        if (ReferenceEquals(ks, widthBox))
                            BeginPopupEdit(heightBox, rowHeight);
                        else if (ReferenceEquals(ks, heightBox))
                            BeginPopupEdit(widthBox, rowWidth);
                        ke.Handled = true;
                    }
                }

                widthBox.KeyDown += HandleKeyDown;
                heightBox.KeyDown += HandleKeyDown;

                widthBox.PreviewMouseLeftButtonDown += (_, clickArgs) =>
                {
                    if (!widthBox.IsReadOnly) return;
                    BeginPopupEdit(widthBox, rowWidth);
                    clickArgs.Handled = true;
                };
                heightBox.PreviewMouseLeftButtonDown += (_, clickArgs) =>
                {
                    if (!heightBox.IsReadOnly) return;
                    BeginPopupEdit(heightBox, rowHeight);
                    clickArgs.Handled = true;
                };

                widthBox.LostKeyboardFocus += (_, __) =>
                {
                    if (!widthBox.IsReadOnly)
                        HideEditors();
                };
                widthBox.MouseLeave += (_, __) =>
                {
                    if (!widthBox.IsReadOnly)
                        HideEditors();
                };
                heightBox.LostKeyboardFocus += (_, __) =>
                {
                    if (!heightBox.IsReadOnly)
                        HideEditors();
                };
                heightBox.MouseLeave += (_, __) =>
                {
                    if (!heightBox.IsReadOnly)
                        HideEditors();
                };

                var root = new StackPanel { Orientation = Orientation.Vertical };
                root.Children.Add(WithRowDivider(rowWidth));
                root.Children.Add(WithRowDivider(rowHeight));

                beamSizePopup = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = tb,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                    HorizontalOffset = 6,
                    VerticalOffset = -2,
                    AllowsTransparency = true,
                    StaysOpen = false,
                    Child = WrapBox(root)
                };

                tb.Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (beamSizePopup != null)
                    {
                        HideEditors();
                        beamSizePopup.IsOpen = true;
                    }
                }), DispatcherPriority.Input);

            };
        }

        // ======== [NEW] Tương tác kiểu Excel cho LINE ========

        enum LineRole { Chain, AxisCenter, AxisOffset, Other }

        sealed class LineMeta
        {
            public LineRole Role;
            public int? Index;
            public double X1, Y1, X2, Y2; // world-mm
            public GridBotsecozu Owner;
            public Brush OriginalBrush;
        }

        private Line _selectedLine;
        private LineMeta _selectedMeta;
        private readonly Brush _highlight = Brushes.DodgerBlue;

        private readonly Dictionary<GridBotsecozu, List<(double yStart, double yEnd)>> _shitaganeAnkaYByItem
            = new Dictionary<GridBotsecozu, List<(double yStart, double yEnd)>>();
        private readonly Dictionary<GridBotsecozu, List<double>> _shitaganeOrangeYByItem
            = new Dictionary<GridBotsecozu, List<double>>();
        private void SelectVisual(Line visual, LineMeta meta)
        {
            if (_selectedLine != null && _selectedMeta != null)
                _selectedLine.Stroke = _selectedMeta.OriginalBrush;

            _selectedLine = visual;
            _selectedMeta = meta;
            visual.Stroke = _highlight;
        }
        private readonly Dictionary<GridBotsecozu, List<double>> _extraChainsByItem = new Dictionary<GridBotsecozu, List<double>>();


        private List<(double yStart, double yEnd)> ShitaganeAnkaYsFor(GridBotsecozu item)
        {
            if (!_shitaganeAnkaYByItem.TryGetValue(item, out var list))
            {
                list = new List<(double yStart, double yEnd)>();
                _shitaganeAnkaYByItem[item] = list;
            }
            return list;
        }
        private List<double> ShitaganeOrangeYsFor(GridBotsecozu item)
        {
            if (!_shitaganeOrangeYByItem.TryGetValue(item, out var list))
            {
                list = new List<double>();
                _shitaganeOrangeYByItem[item] = list;
            }
            return list;
        }

        private List<double> ExtraChainsFor(GridBotsecozu item)
        {
            if (!_extraChainsByItem.TryGetValue(item, out var list))
            {
                list = new List<double>();
                _extraChainsByItem[item] = list;
            }
            return list;
        }

        private static void ApplyShitaganeOffsets(double targetY)
        {
            OffsetAbdominalChainY = (OffsetAbdominalChainY.X, targetY);
            OffsetAbdominalChainX = (OffsetAbdominalChainX.X, targetY);
            OffsetTanbuText = (OffsetTanbuText.X, targetY);
            OffsetCentralStirrupFrame = (OffsetCentralStirrupFrame.X, targetY);
            OffsetLegendColumn = (OffsetLegendColumn.X, targetY);
        }

        private Line AddLineInteractive(
     Canvas c, WCTransform T,
     double x1, double y1, double x2, double y2,
     Brush stroke, double thickness,
     LineRole role, GridBotsecozu owner, int? index = null)
        {
            var p1 = T.P(x1, y1);
            var p2 = T.P(x2, y2);

            var meta = new LineMeta
            {
                Role = role,
                Index = index,
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Owner = owner,
                OriginalBrush = stroke
            };

            // (A) ĐƯỜNG HIỂN THỊ: mảnh, đúng màu
            var visual = new Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = stroke,
                StrokeThickness = thickness,
                SnapsToDevicePixels = true,
                IsHitTestVisible = false,       // <-- để đường đè (hit) bắt sự kiện
                Tag = meta
            };

            // (B) ĐƯỜNG ĐÈ (HITBOX): dày, trong suốt, chỉ để bắt click
            var hit = new Line
            {
                X1 = p1.X,
                Y1 = p1.Y,
                X2 = p2.X,
                Y2 = p2.Y,
                Stroke = Brushes.Transparent,   // <-- vô hình
                StrokeThickness = Math.Max(thickness, LineHitPx),
                SnapsToDevicePixels = true,
                Cursor = Cursors.Hand,
                IsHitTestVisible = true,
                Tag = meta
            };

            // Sự kiện click đổi màu/chọn
            hit.MouseLeftButtonDown += (s, e) =>
            {
                e.Handled = true;
                SelectVisual(visual, meta);
            };

            // Menu chuột phải (insert/delete chain ...)
            hit.MouseRightButtonUp += (s, e) =>
            {
                e.Handled = true;
                ShowContextMenuForLine(c, visual, meta); // truyền "visual" để đổi màu/refresh
            };

            // Thứ tự add: hit trước, visual sau (visual nằm trên, nhưng IsHitTestVisible=false)
            c.Children.Add(hit);
            c.Children.Add(visual);
            // [SCENE] Ghi thêm line tương tác vào Scene để export dùng chung
            string layer = meta.Role == LineRole.AxisCenter ? "AXIS"
                         : meta.Role == LineRole.Chain ? "CHAIN"
                         : meta.Role == LineRole.AxisOffset ? "OFFSET"
                         : "LINE";
            SceneFor(owner).Add(new SceneLine(x1, y1, x2, y2, layer));

            return visual;
        }

        private void ShowContextMenuForLine(Canvas canvas, Line line, LineMeta meta)
        {
            var cm = new ContextMenu();

            if (meta.Role == LineRole.Chain)
            {
                cm.Items.Add(new Separator());

                const double deltaY = 300; // mm
                var miInsBelow = new MenuItem { Header = "Chèn line mới phía trên" };
                var miInsAbove = new MenuItem { Header = "Chèn line mới phía dưới" };

                var miDelete = new MenuItem { Header = "Xóa line này" };

                miInsAbove.Click += (s, e) =>
                {
                    var list = ExtraChainsFor(meta.Owner);
                    list.Add(meta.Y1 + deltaY);
                    Redraw(canvas, meta.Owner);
                };
                miInsBelow.Click += (s, e) =>
                {
                    var list = ExtraChainsFor(meta.Owner);
                    var y = Math.Max(0, meta.Y1 - deltaY);
                    list.Add(y);
                    Redraw(canvas, meta.Owner);
                };
                miDelete.Click += (s, e) =>
                {
                    var list = ExtraChainsFor(meta.Owner);
                    const double eps = 1e-6;
                    var idx = list.FindIndex(v => Math.Abs(v - meta.Y1) < eps);
                    if (idx >= 0) list.RemoveAt(idx);
                    // nếu là chain mặc định (y=300), menu xoá sẽ không làm gì
                    Redraw(canvas, meta.Owner);
                };

                cm.Items.Add(miInsBelow);
                cm.Items.Add(miInsAbove);
                cm.Items.Add(miDelete);
            }

            line.ContextMenu = cm;
            cm.IsOpen = true;
        }

        // ===== [ZOOM] View state (per-item) =====
        sealed class ViewState
        {
            public double Zoom = 1.0;   // 1.0 = fit
            public double PanXmm = 0.0; // pan theo mm (world)
            public double PanYmm = 0.0;
        }

        private sealed class InteractiveViewportState
        {
            public Canvas Canvas;
            public GridBotsecozu Item;
            public Image Overlay;
            public MatrixTransform OverlayTransform = new MatrixTransform();
            public WCTransform SnapshotTransform;
            public DispatcherTimer CommitTimer;
            public DispatcherTimer SnapshotTimer;
            public BitmapSource CachedSnapshot;
            public WCTransform CachedSnapshotTransform;
            public GridBotsecozu CachedSnapshotItem;
            public double CachedCanvasWidth;
            public double CachedCanvasHeight;
            public bool IsActive;
        }

        private readonly Dictionary<GridBotsecozu, ViewState> _viewByItem = new Dictionary<GridBotsecozu, ViewState>();
        private readonly Dictionary<Canvas, InteractiveViewportState> _interactiveViewportByCanvas
            = new Dictionary<Canvas, InteractiveViewportState>();
        private const int InteractiveViewportCommitDelayMs = 220;
        private const int InteractiveViewportSnapshotRefreshDelayMs = 1;
        private const double InteractiveViewportSnapshotScale = 0.70;
        private const double InteractiveViewportWheelDeltaUnit = 120.0;
        private const double InteractiveViewportWheelStep = 1.10;
        private const double InteractiveViewportCtrlWheelStep = 1.21;
        private const int InteractiveViewportTextlessSceneThreshold = 1800;
        private const int LightweightCanvasUiSpanThreshold = 8;
        private const int DeferredInteractiveCanvasDelayMs = 320;

        private ViewState VS(GridBotsecozu item)
        {
            if (!_viewByItem.TryGetValue(item, out var vs))
            {
                vs = new ViewState();
                _viewByItem[item] = vs;
            }
            return vs;
        }

        private readonly Dictionary<GridBotsecozu, bool> _lightweightCanvasUiByItem
            = new Dictionary<GridBotsecozu, bool>();
        private readonly Dictionary<GridBotsecozu, bool> _forceInteractiveCanvasUiByItem
            = new Dictionary<GridBotsecozu, bool>();

        private sealed class DeferredInteractiveCanvasState
        {
            public Canvas Canvas;
            public GridBotsecozu Item;
            public DispatcherTimer Timer;
        }

        private readonly Dictionary<Canvas, DeferredInteractiveCanvasState> _deferredInteractiveCanvasByCanvas
            = new Dictionary<Canvas, DeferredInteractiveCanvasState>();
        private readonly Dictionary<Canvas, Image> _sceneBitmapLayerByCanvas
            = new Dictionary<Canvas, Image>();

        private bool IsLightweightCanvasUi(GridBotsecozu item)
        {
            return item != null &&
                   _lightweightCanvasUiByItem.TryGetValue(item, out var enabled) &&
                   enabled &&
                   !(_forceInteractiveCanvasUiByItem.TryGetValue(item, out var forced) && forced);
        }

        private void SetLightweightCanvasUi(GridBotsecozu item, bool enabled)
        {
            if (item == null)
                return;

            _lightweightCanvasUiByItem[item] = enabled;
        }

        private void SetForceInteractiveCanvasUi(GridBotsecozu item, bool enabled)
        {
            if (item == null)
                return;

            _forceInteractiveCanvasUiByItem[item] = enabled;
        }

        private DeferredInteractiveCanvasState GetOrCreateDeferredInteractiveCanvasState(Canvas canvas)
        {
            if (canvas == null)
                return null;

            if (!_deferredInteractiveCanvasByCanvas.TryGetValue(canvas, out var state))
            {
                state = new DeferredInteractiveCanvasState
                {
                    Canvas = canvas
                };

                state.Timer = new DispatcherTimer(DispatcherPriority.Background, canvas.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(DeferredInteractiveCanvasDelayMs)
                };
                state.Timer.Tick += (s, e) =>
                {
                    state.Timer.Stop();
                    if (state.Canvas == null || state.Item == null)
                        return;

                    if (_isPanning ||
                        (_interactiveViewportByCanvas.TryGetValue(state.Canvas, out var viewportState) &&
                         viewportState != null &&
                         viewportState.IsActive))
                    {
                        state.Timer.Start();
                        return;
                    }

                    SetForceInteractiveCanvasUi(state.Item, true);
                    try
                    {
                        Redraw(state.Canvas, state.Item);
                    }
                    finally
                    {
                        SetForceInteractiveCanvasUi(state.Item, false);
                    }
                };

                _deferredInteractiveCanvasByCanvas[canvas] = state;
            }

            return state;
        }

        private void CancelDeferredInteractiveCanvasRedraw(Canvas canvas)
        {
            if (canvas == null)
                return;

            if (_deferredInteractiveCanvasByCanvas.TryGetValue(canvas, out var state))
                state.Timer?.Stop();
        }

        private InteractiveViewportState GetOrCreateInteractiveViewportState(Canvas canvas)
        {
            if (canvas == null) return null;

            if (!_interactiveViewportByCanvas.TryGetValue(canvas, out var state))
            {
                state = new InteractiveViewportState
                {
                    Canvas = canvas
                };

                state.CommitTimer = new DispatcherTimer(DispatcherPriority.Background, canvas.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(InteractiveViewportCommitDelayMs)
                };
                state.CommitTimer.Tick += (s, e) =>
                {
                    state.CommitTimer.Stop();
                    if (state.Canvas == null || state.Item == null)
                        return;

                    CommitInteractiveViewport(state.Canvas, state.Item);
                };

                state.SnapshotTimer = new DispatcherTimer(DispatcherPriority.Background, canvas.Dispatcher)
                {
                    Interval = TimeSpan.FromMilliseconds(InteractiveViewportSnapshotRefreshDelayMs)
                };
                state.SnapshotTimer.Tick += (s, e) =>
                {
                    state.SnapshotTimer.Stop();
                    if (state.Canvas == null || state.Item == null || state.IsActive)
                        return;

                    if (!TryMakeTransform(state.Canvas, state.Item, out var snapshotTransform, out _, out _, out _))
                        return;

                    var bitmap = CreateInteractiveViewportSnapshot(state.Canvas, state.Item, snapshotTransform);
                    if (bitmap == null)
                        return;

                    state.CachedSnapshot = bitmap;
                    state.CachedSnapshotTransform = snapshotTransform;
                    state.CachedSnapshotItem = state.Item;
                    state.CachedCanvasWidth = state.Canvas.ActualWidth;
                    state.CachedCanvasHeight = state.Canvas.ActualHeight;
                };

                _interactiveViewportByCanvas[canvas] = state;
            }

            return state;
        }

        private static void CopyOverlayPlacement(FrameworkElement source, FrameworkElement overlay)
        {
            if (source == null || overlay == null) return;

            overlay.HorizontalAlignment = source.HorizontalAlignment;
            overlay.VerticalAlignment = source.VerticalAlignment;
            overlay.Margin = source.Margin;
            overlay.Width = source.ActualWidth > 0 ? source.ActualWidth : source.Width;
            overlay.Height = source.ActualHeight > 0 ? source.ActualHeight : source.Height;
            overlay.MinWidth = source.MinWidth;
            overlay.MinHeight = source.MinHeight;
            overlay.MaxWidth = source.MaxWidth;
            overlay.MaxHeight = source.MaxHeight;

            Grid.SetRow(overlay, Grid.GetRow(source));
            Grid.SetColumn(overlay, Grid.GetColumn(source));
            Grid.SetRowSpan(overlay, Grid.GetRowSpan(source));
            Grid.SetColumnSpan(overlay, Grid.GetColumnSpan(source));
        }

        private bool TryGetCachedInteractiveViewportSnapshot(
            Canvas canvas,
            GridBotsecozu item,
            WCTransform currentTransform,
            out BitmapSource bitmap,
            out WCTransform snapshotTransform)
        {
            bitmap = null;
            snapshotTransform = currentTransform;

            if (canvas == null || item == null)
                return false;

            if (!_interactiveViewportByCanvas.TryGetValue(canvas, out var state) || state == null)
                return false;

            if (state.CachedSnapshot == null || !ReferenceEquals(state.CachedSnapshotItem, item))
                return false;

            if (Math.Abs(state.CachedCanvasWidth - canvas.ActualWidth) > 0.5 ||
                Math.Abs(state.CachedCanvasHeight - canvas.ActualHeight) > 0.5)
            {
                return false;
            }

            var cachedTransform = state.CachedSnapshotTransform;
            if (cachedTransform.YDown != currentTransform.YDown ||
                Math.Abs(cachedTransform.Ox - currentTransform.Ox) > 0.5 ||
                Math.Abs(cachedTransform.Oy - currentTransform.Oy) > 0.5 ||
                Math.Abs(cachedTransform.Scale - currentTransform.Scale) > 1e-6 ||
                Math.Abs(cachedTransform.FontScale - currentTransform.FontScale) > 1e-6)
            {
                return false;
            }

            bitmap = state.CachedSnapshot;
            snapshotTransform = cachedTransform;
            return true;
        }

        private static double GetInteractiveViewportSnapshotScale(Canvas canvas)
        {
            if (canvas == null)
                return InteractiveViewportSnapshotScale;

            int childCount = canvas.Children != null ? canvas.Children.Count : 0;
            if (childCount > 2500)
                return 0.45;
            if (childCount > 1200)
                return 0.55;
            return InteractiveViewportSnapshotScale;
        }

        private static BitmapSource CaptureCanvasSnapshot(Canvas canvas)
        {
            if (canvas == null) return null;

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            if (width < 1 || height < 1)
                return null;

            var dpi = VisualTreeHelper.GetDpi(canvas);
            double snapshotScale = GetInteractiveViewportSnapshotScale(canvas);
            int pixelWidth = Math.Max(1, (int)Math.Ceiling(width * dpi.DpiScaleX * snapshotScale));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(height * dpi.DpiScaleY * snapshotScale));
            var bitmap = new RenderTargetBitmap(
                pixelWidth,
                pixelHeight,
                96.0,
                96.0,
                PixelFormats.Pbgra32);

            bitmap.Render(canvas);
            if (bitmap.CanFreeze) bitmap.Freeze();
            return bitmap;
        }

        private BitmapSource CreateInteractiveViewportSnapshot(Canvas canvas, GridBotsecozu item, WCTransform snapshotTransform)
        {
            if (canvas == null)
                return null;

            if (item != null &&
                _sceneByItem.TryGetValue(item, out var scene) &&
                scene != null &&
                scene.Count > 0)
            {
                var sceneBitmap = CreateSceneInteractiveViewportSnapshot(canvas, scene, snapshotTransform);
                if (sceneBitmap != null)
                    return sceneBitmap;
            }

            return CaptureCanvasSnapshot(canvas);
        }

        private void RenderSceneBitmapToCanvas(Canvas canvas, GridBotsecozu item, WCTransform transform)
        {
            if (canvas == null || item == null)
                return;

            if (!_sceneByItem.TryGetValue(item, out var scene) || scene == null || scene.Count == 0)
                return;

            var bitmap = CreateSceneCanvasBitmap(canvas, scene, transform, renderText: true, scaleFactor: 1.0);
            if (bitmap == null)
                return;

            if (_sceneBitmapLayerByCanvas.TryGetValue(canvas, out var oldImage) && oldImage != null)
            {
                canvas.Children.Remove(oldImage);
            }

            var image = oldImage ?? new Image
            {
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true
            };
            image.Source = bitmap;
            image.Width = canvas.ActualWidth;
            image.Height = canvas.ActualHeight;
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);
            Canvas.SetLeft(image, 0);
            Canvas.SetTop(image, 0);
            Panel.SetZIndex(image, -10000);
            canvas.Children.Insert(0, image);
            _sceneBitmapLayerByCanvas[canvas] = image;
        }

        private BitmapSource CreateSceneCanvasBitmap(Canvas canvas, List<object> scene, WCTransform transform, bool renderText, double scaleFactor)
        {
            if (canvas == null || scene == null || scene.Count == 0)
                return null;

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            if (width < 1 || height < 1)
                return null;

            double effectiveScaleFactor = scaleFactor <= 0 ? 1.0 : scaleFactor;
            int pixelWidth = Math.Max(1, (int)Math.Ceiling(width * effectiveScaleFactor));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(height * effectiveScaleFactor));

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                if (Math.Abs(effectiveScaleFactor - 1.0) > 1e-6)
                    dc.PushTransform(new ScaleTransform(effectiveScaleFactor, effectiveScaleFactor));

                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));

                foreach (var entity in scene)
                {
                    if (entity is SceneLine ln)
                    {
                        var pen = new Pen(new SolidColorBrush(ln.StrokeColor), Math.Max(0.75, ln.Thickness));
                        if (ln.Dash != null && ln.Dash.Length > 0)
                            pen.DashStyle = new DashStyle(ln.Dash, 0);
                        dc.DrawLine(pen, transform.P(ln.X1, ln.Y1), transform.P(ln.X2, ln.Y2));
                    }
                    else if (entity is DxfCircle circle)
                    {
                        double rPx = Math.Max(0.5, Math.Abs(circle.R * transform.Scale));
                        var center = transform.P(circle.X, circle.Y);
                        var pen = new Pen(new SolidColorBrush(circle.StrokeColor), Math.Max(0.75, circle.StrokeThicknessPx));
                        if (circle.Dash != null && circle.Dash.Length > 0)
                            pen.DashStyle = new DashStyle(circle.Dash, 0);
                        Brush fill = circle.Filled ? new SolidColorBrush(circle.FillColor) : null;
                        dc.DrawEllipse(fill, pen, center, rPx, rPx);
                    }
                    else if (entity is DxfArc arc)
                    {
                        DrawDxfArcToInteractiveViewport(dc, arc, transform);
                    }
                    else if (entity is DxfSolid solid)
                    {
                        var geometry = new StreamGeometry();
                        using (var gctx = geometry.Open())
                        {
                            gctx.BeginFigure(transform.P(solid.X1, solid.Y1), true, true);
                            gctx.LineTo(transform.P(solid.X2, solid.Y2), true, false);
                            gctx.LineTo(transform.P(solid.X3, solid.Y3), true, false);
                            gctx.LineTo(transform.P(solid.X4, solid.Y4), true, false);
                        }
                        geometry.Freeze();
                        dc.DrawGeometry(new SolidColorBrush(solid.FillColor), null, geometry);
                    }
                    else if (renderText && entity is DxfText tx && !tx.SkipCanvasBitmap)
                    {
                        DrawDxfTextToInteractiveViewport(dc, tx, transform);
                    }
                }

                if (Math.Abs(effectiveScaleFactor - 1.0) > 1e-6)
                    dc.Pop();
            }

            var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96.0, 96.0, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            if (bitmap.CanFreeze) bitmap.Freeze();
            return bitmap;
        }

        private BitmapSource CreateSceneInteractiveViewportSnapshot(Canvas canvas, List<object> scene, WCTransform snapshotTransform)
        {
            if (canvas == null || scene == null || scene.Count == 0)
                return null;

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            if (width < 1 || height < 1)
                return null;

            bool renderText = scene.Count <= InteractiveViewportTextlessSceneThreshold;
            return CreateSceneCanvasBitmap(canvas, scene, snapshotTransform, renderText, GetInteractiveViewportSnapshotScale(canvas));
        }

        private static void DrawDxfArcToInteractiveViewport(DrawingContext dc, DxfArc arc, WCTransform transform)
        {
            if (dc == null || arc == null || transform.Scale == 0)
                return;

            double startRad = arc.StartDeg * Math.PI / 180.0;
            double endRad = arc.EndDeg * Math.PI / 180.0;
            Point p0 = transform.P(arc.X + arc.R * Math.Cos(startRad), arc.Y + arc.R * Math.Sin(startRad));
            Point p1 = transform.P(arc.X + arc.R * Math.Cos(endRad), arc.Y + arc.R * Math.Sin(endRad));
            double rPx = Math.Max(0.5, Math.Abs(arc.R * transform.Scale));
            double delta = NormalizeDeltaCCW(arc.StartDeg, arc.EndDeg);

            var figure = new PathFigure { StartPoint = p0, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new ArcSegment
            {
                Point = p1,
                Size = new Size(rPx, rPx),
                RotationAngle = 0,
                IsLargeArc = delta > 180.0,
                SweepDirection = transform.YDown ? SweepDirection.Clockwise : SweepDirection.Counterclockwise
            });

            var geometry = new PathGeometry(new[] { figure });
            var pen = new Pen(new SolidColorBrush(arc.StrokeColor), Math.Max(0.75, arc.ThicknessPx));
            if (arc.Dash != null && arc.Dash.Length > 0)
                pen.DashStyle = new DashStyle(arc.Dash, 0);
            dc.DrawGeometry(null, pen, geometry);
        }

        private static void DrawDxfTextToInteractiveViewport(DrawingContext dc, DxfText tx, WCTransform transform)
        {
            if (dc == null || string.IsNullOrEmpty(tx.Value))
                return;

            var metrics = MeasureDxfTextLayout(tx, PlotRenderTextTarget, transform.Scale);
            tx = metrics.Text;
            var brush = new SolidColorBrush(tx.Color);
            var text = new FormattedText(
                tx.Value,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                metrics.Typeface,
                metrics.FontPx,
                brush,
                1.0);

            Point anchor = transform.P(tx.X, tx.Y);
            Point drawPoint = GetDxfTextTopLeft(anchor, metrics.WidthPx, metrics.HeightPx, tx.HAnchor, tx.VAnchor);

            if (Math.Abs(tx.RotationDeg) > 0.01)
            {
                dc.PushTransform(new RotateTransform(-tx.RotationDeg, anchor.X, anchor.Y));
                dc.DrawText(text, drawPoint);
                dc.Pop();
                return;
            }

            dc.DrawText(text, drawPoint);
        }

        private bool TryBeginInteractiveViewport(Canvas canvas, GridBotsecozu item)
        {
            if (canvas == null || item == null)
                return false;

            var state = GetOrCreateInteractiveViewportState(canvas);
            if (state == null)
                return false;

            state.Item = item;
            if (state.IsActive && state.Overlay != null)
            {
                state.CommitTimer.Stop();
                return true;
            }

            if (!(canvas.Parent is Panel parent))
                return false;

            if (!TryMakeTransform(canvas, item, out var snapshotTransform, out _, out _, out _))
                return false;

            BitmapSource bitmap;
            if (!TryGetCachedInteractiveViewportSnapshot(canvas, item, snapshotTransform, out bitmap, out snapshotTransform))
            {
                bitmap = CreateInteractiveViewportSnapshot(canvas, item, snapshotTransform);
                if (bitmap == null)
                    return false;

                state.CachedSnapshot = bitmap;
                state.CachedSnapshotTransform = snapshotTransform;
                state.CachedSnapshotItem = item;
                state.CachedCanvasWidth = canvas.ActualWidth;
                state.CachedCanvasHeight = canvas.ActualHeight;
            }

            var overlay = new Image
            {
                Source = bitmap,
                Stretch = Stretch.Fill,
                IsHitTestVisible = false,
                SnapsToDevicePixels = true,
                CacheMode = new BitmapCache(),
                RenderTransform = state.OverlayTransform,
                Clip = new RectangleGeometry(new Rect(0, 0, canvas.ActualWidth, canvas.ActualHeight))
            };
            RenderOptions.SetBitmapScalingMode(overlay, BitmapScalingMode.LowQuality);

            CopyOverlayPlacement(canvas, overlay);
            Panel.SetZIndex(overlay, Math.Max(Panel.GetZIndex(canvas), 1) + 10000);
            parent.Children.Add(overlay);

            state.Canvas = canvas;
            state.Item = item;
            state.Overlay = overlay;
            state.SnapshotTransform = snapshotTransform;
            state.OverlayTransform.Matrix = Matrix.Identity;
            state.IsActive = true;

            canvas.Opacity = 0.0;
            return true;
        }

        private bool ApplyInteractiveViewport(Canvas canvas, GridBotsecozu item)
        {
            if (canvas == null || item == null)
                return false;

            if (!_interactiveViewportByCanvas.TryGetValue(canvas, out var state) ||
                !state.IsActive ||
                state.Overlay == null)
            {
                return false;
            }

            if (!TryMakeTransform(canvas, item, out var currentTransform, out _, out _, out _))
                return false;

            double snapshotScale = state.SnapshotTransform.Scale;
            if (snapshotScale <= 0)
                return false;

            double scaleRatio = currentTransform.Scale / snapshotScale;
            var matrix = new Matrix(
                scaleRatio, 0,
                0, scaleRatio,
                currentTransform.Ox - state.SnapshotTransform.Ox * scaleRatio,
                currentTransform.Oy - state.SnapshotTransform.Oy * scaleRatio);

            state.OverlayTransform.Matrix = matrix;
            return true;
        }

        private bool ApplyInteractiveViewportPan(Canvas canvas, Vector deltaPx)
        {
            if (canvas == null)
                return false;

            if (!_interactiveViewportByCanvas.TryGetValue(canvas, out var state) ||
                !state.IsActive ||
                state.Overlay == null)
            {
                return false;
            }

            var matrix = _panStartOverlayMatrix;
            matrix.OffsetX += deltaPx.X;
            matrix.OffsetY += deltaPx.Y;
            state.OverlayTransform.Matrix = matrix;
            return true;
        }

        private void ScheduleInteractiveViewportSnapshotRefresh(Canvas canvas, GridBotsecozu item)
        {
            var state = GetOrCreateInteractiveViewportState(canvas);
            if (state == null)
                return;

            state.Canvas = canvas;
            state.Item = item;
            state.SnapshotTimer?.Stop();
            if (state.IsActive)
                return;

            state.SnapshotTimer?.Start();
        }

        private void ScheduleDeferredInteractiveCanvasRedraw(Canvas canvas, GridBotsecozu item)
        {
            var state = GetOrCreateDeferredInteractiveCanvasState(canvas);
            if (state == null)
                return;

            state.Canvas = canvas;
            state.Item = item;
            state.Timer?.Stop();
            state.Timer?.Start();
        }

        private void ScheduleInteractiveViewportCommit(Canvas canvas, GridBotsecozu item)
        {
            var state = GetOrCreateInteractiveViewportState(canvas);
            if (state == null)
                return;

            state.Item = item;
            state.CommitTimer.Stop();
            state.CommitTimer.Start();
        }

        private void EndInteractiveViewport(Canvas canvas)
        {
            if (canvas == null)
                return;

            if (_interactiveViewportByCanvas.TryGetValue(canvas, out var state))
            {
                state.CommitTimer?.Stop();
                state.SnapshotTimer?.Stop();

                if (state.Overlay != null)
                {
                    if (state.Overlay.Parent is Panel parent)
                        parent.Children.Remove(state.Overlay);

                    state.Overlay = null;
                }

                state.Item = null;
                state.IsActive = false;
                state.OverlayTransform.Matrix = Matrix.Identity;
            }

            canvas.Opacity = 1.0;
        }

        private void CommitInteractiveViewport(Canvas canvas, GridBotsecozu item)
        {
            if (canvas == null || item == null)
                return;

            EndInteractiveViewport(canvas);
            Redraw(canvas, item);
        }

        // Trả về transform hiện tại + fitScale (để tính zoom quanh con trỏ)
        private bool TryMakeTransform(Canvas canvas, GridBotsecozu item,
                                      out WCTransform T, out double fitScale,
                                      out double W, out double H)
        {
            T = default; fitScale = 1; W = H = 0;
            var k = _projectData?.Kihon;
            if (k == null || _currentSecoList == null) return false;

            bool tsuIsX = k.NameX.Any(n => n.Name == _currentSecoList.通を選択);
            bool tsuIsY = k.NameY.Any(n => n.Name == _currentSecoList.通を選択);

            var names = tsuIsY ? k.NameX.Select(n => n.Name).ToList()
                               : tsuIsX ? k.NameY.Select(n => n.Name).ToList()
                                        : new List<string>();
            var spans = tsuIsY ? k.ListSpanX.Select(s => ParseMm(s.Span)).ToList()
                               : tsuIsX ? k.ListSpanY.Select(s => ParseMm(s.Span)).ToList()
                                        : new List<double>();
            if (names.Count < 2 || spans.Count < names.Count - 1) return false;

            W = spans.Take(names.Count - 1).Sum();
            if (W <= 0) return false;
            H = Math.Max(1, W * 0.4);

            double maxW = Math.Max(1, canvas.ActualWidth * CanvasPadding);
            double maxH = Math.Max(1, canvas.ActualHeight * CanvasPadding);
            fitScale = Math.Min(maxW / W, maxH / H);

            var vs = VS(item);
            // Replace all usages of Math.Clamp with Clamp in the file
            double scale = fitScale * Clamp(vs.Zoom, 0.05, 20.0);

            double minSpanMm = spans.Take(names.Count - 1).DefaultIfEmpty(W).Min();
            double minSpanPx = minSpanMm * scale;
            double fontScale = Clamp(minSpanPx / 120.0, 0.35, 2.0);

            double baseOx = canvas.ActualWidth / 2.0;
            double baseOy = (canvas.ActualHeight - 800) / 2.0; // [KEEP]
            double ox = baseOx + vs.PanXmm * scale;
            double oy = baseOy + vs.PanYmm * scale;

            T = new WCTransform(ox, oy, scale, yDown: true, fontScale: fontScale);
            return true;
        }

        private static Point ScreenToWorld(WCTransform T, Point sp)
        {
            double wx = (sp.X - T.Ox) / T.Scale;
            double wy = (sp.Y - T.Oy) / T.Scale; // YDown=true
            return new Point(wx, wy);
        }

        // ===== [ZOOM] Trạng thái pan tạm thời khi kéo chuột =====
        bool _isPanning = false;
        Point _panStartPx;         // điểm màn hình lúc bắt đầu pan
        Point _panStartPanMm;      // pan mm lúc bắt đầu pan
        double _panStartScalePxPerMm = 1.0;
        Matrix _panStartOverlayMatrix = Matrix.Identity;


        // ======= PREVIEW: Redraw =======
        private void Redraw(Canvas canvas, GridBotsecozu item)
        {
            var (hata1, hata2, anka1, anka2, nige1, nige2, TsugiteOption1, sugiteOption2) = GetKesanFlags();

            if (canvas == null || _projectData?.Kihon == null || _currentSecoList == null) return;
            EndInteractiveViewport(canvas);
            SyncRuntimeOverridesFromModel(item);
            ResetOrangeSegmentRuntimeState(item);
            canvas.Children.Clear();
            SceneBegin(item);

            var k = _projectData.Kihon;
            bool tsuIsX = k.NameX.Any(n => n.Name == _currentSecoList.通を選択);
            bool tsuIsY = k.NameY.Any(n => n.Name == _currentSecoList.通を選択);

            var names = tsuIsY ? k.NameX.Select(n => n.Name).ToList()
                               : tsuIsX ? k.NameY.Select(n => n.Name).ToList()
                                        : new List<string>();

            var spans = tsuIsY ? k.ListSpanX.Select(s => ParseMm(s.Span)).ToList()
                               : tsuIsX ? k.ListSpanY.Select(s => ParseMm(s.Span)).ToList()
                                        : new List<double>();

            if (names.Count < 2 || spans.Count < names.Count - 1) return;

            int previewSpanCount = Math.Max(0, names.Count - 1);
            SetLightweightCanvasUi(item, previewSpanCount >= LightweightCanvasUiSpanThreshold);
            _useApproximateCanvasTextLayout = IsLightweightCanvasUi(item);

            double W = spans.Take(names.Count - 1).Sum();
            if (W <= 0) return;
            double H = Math.Max(1, W * 0.75);

            double maxW = Math.Max(1, canvas.ActualWidth * CanvasPadding);
            double maxH = Math.Max(1, canvas.ActualHeight * CanvasPadding);
            double fitScale = Math.Min(maxW / W, maxH / H);

            var vs = VS(item);
            double scale = fitScale * Clamp(vs.Zoom, 0.05, 20.0);

            double baseOx = canvas.ActualWidth / 2.0;
            double baseOy = (canvas.ActualHeight - 800) / 2.0;
            double ox = baseOx + vs.PanXmm * scale;
            double oy = baseOy + vs.PanYmm * scale;

            //double fontScale = Clamp(vs.Zoom, 0.05, 20.0);

            double minSpanMm = spans.Take(names.Count - 1).DefaultIfEmpty(W).Min();
            double minSpanPx = minSpanMm * scale;
            int previewAxisCount = Math.Max(names.Count, 1);
            const double baseDimFont = 10.0;
            // Giảm 0.25px cho mỗi trục vượt quá 2: 2 trục → 10.0, 4 trục → 9.5, ...
            double dimFont = Math.Max(6.0, baseDimFont - 10 * Math.Max(0, previewAxisCount - 2));
            double axisFontScale = dimFont / baseDimFont;

            double zoomFontScale = Clamp(vs.Zoom, 0.05, 20.0);
            double fontScale = Clamp(Clamp(minSpanPx / 120.0, 0.35, 2.0) * axisFontScale * zoomFontScale, 0.2, 5.0);

            var T = new WCTransform(ox, oy, scale, yDown: true, fontScale: fontScale);
            var F = 階を選択ComboBox.SelectedItem;

            var pos = new List<double> { -W / 2.0 };
            for (int i = 0; i < names.Count - 1; i++) pos.Add(pos[i] + spans[i]);

            double y0 = 0, yH = H;

            var up = new double[pos.Count];
            var down = new double[pos.Count];
            var left = new double[pos.Count];
            var right = new double[pos.Count];

            string selY = 通を選択ComboBox.SelectedItem?.ToString() ?? "";
            string selF = F?.ToString() ?? "";

            for (int i = 0; i < pos.Count; i++)
            {
                string axisName = (i < names.Count) ? names[i] : "";
                double ui, di, li, ri;
                if (tsuIsY)
                    (ui, di, li, ri) = GetOffsetsByPosition(selF, selY, axisName);
                else
                    (ui, di, li, ri) = GetOffsetsByPosition(selF, axisName, selY);
                up[i] = ui; down[i] = di; left[i] = li; right[i] = ri;
            }

            double yChainMid = 600;
            double yChainBot = 900;
            //double dimFont = 10;
            const double yChainLocalBase = 300.0;
            double axisLineEndY = yChainLocalBase + 13500 + OffsetCentralStirrupFrame.Y;
            Brush dimBrush = Brushes.Black;

            // ==== Cấu hình hiển thị kích thước cho đoạn cam ====
            const bool ShowOrangeDims = true;     // Cho phép tắt nhanh
            const double MinDimLen = 200.0;       // Chỉ hiển thị nếu đoạn ≥ ngưỡng (mm)
            const double OrangeDimPrimaryTextGapMm = -15;
            const double OrangeDimSecondaryTextGapMm = -30;

            // NEW: Bật/tắt chấm đen "tiền xử lý" (cm/cE2...) — MẶC ĐỊNH TẮT
            const bool ShowPreRoundCutMarks = false;
            const double CutDotR = 30.0;
            const double CutTol = 0.01;

            // So gần bằng theo mm
            bool Near(double a, double b, double eps = 0.5) => Math.Abs(a - b) <= eps;

            // === Helper: tìm index span theo x (dùng để lấy φ 中央 đúng span) ===
            int FindSpanIndexByX(double x, double[] spanLeftArrLocal, double[] spanRightArrLocal, int spanCountLocal)
            {
                for (int i = 0; i < spanCountLocal; i++)
                {
                    if (x >= spanLeftArrLocal[i] - 0.01 && x <= spanRightArrLocal[i] + 0.01)
                        return i;
                }
                return -1;
            }

            // =========================
            //  Helpers làm tròn (local)
            // =========================
            double CeilToBase(double v, double step)
                => (v <= 0) ? 0 : Math.Ceiling(v / step) * step;


            void RoundContiguousChainsInPlace(List<(double x1, double x2)> segs, double step, double eps = 1.0)
            {
                if (segs == null || segs.Count == 0) return;

                int i = 0;
                while (i < segs.Count)
                {
                    // tìm một chuỗi liền nhau: seg[k].x2 == seg[k+1].x1 (±eps)
                    int j = i;
                    while (j + 1 < segs.Count && Math.Abs(segs[j].x2 - segs[j + 1].x1) <= eps) j++;

                    if (j > i)
                    {
                        double totalDelta = 0.0;

                        // ceil các đoạn trừ đoạn cuối
                        for (int k2 = i; k2 < j; k2++)
                        {
                            var sk = segs[k2];
                            double len = sk.x2 - sk.x1;
                            double lenRounded = CeilToBase(len, step);
                            double delta = Math.Max(0, lenRounded - len);
                            if (delta <= 0) continue;

                            // dịch toàn bộ các mốc từ k2 trở về sau sang phải 'delta'
                            for (int m = k2; m <= j; m++)
                            {
                                var sm = segs[m];
                                if (m == k2) segs[m] = (sm.x1, sm.x2 + delta);
                                else segs[m] = (sm.x1 + delta, sm.x2 + delta);
                            }
                            totalDelta += delta;
                        }

                        // rút bớt ở đoạn cuối chuỗi để tổng giữ nguyên
                        if (totalDelta > 0)
                        {
                            var last = segs[j];
                            double lastLen = Math.Max(0, last.x2 - last.x1);
                            double newLastLen = Math.Max(0, lastLen - totalDelta);
                            segs[j] = (last.x1, last.x1 + newLastLen);
                        }
                    }

                    i = j + 1;
                }
            }

            void RoundContiguousChainsInPlaceBySpan(
                List<(double x1, double x2)> segs,
                double step,
                IReadOnlyList<double> breakPoints,
                double eps = 1.0)
            {
                if (segs == null || segs.Count == 0) return;
                if (breakPoints == null || breakPoints.Count == 0)
                {
                    RoundContiguousChainsInPlace(segs, step, eps);
                    return;
                }

                bool IsSpanBreak(double x)
                {
                    for (int bi = 0; bi < breakPoints.Count; bi++)
                    {
                        if (Math.Abs(breakPoints[bi] - x) <= eps)
                            return true;
                    }

                    return false;
                }

                int i = 0;
                while (i < segs.Count)
                {
                    int j = i;
                    while (j + 1 < segs.Count)
                    {
                        double joinX = 0.5 * (segs[j].x2 + segs[j + 1].x1);
                        if (Math.Abs(segs[j].x2 - segs[j + 1].x1) > eps || IsSpanBreak(joinX))
                            break;

                        j++;
                    }

                    if (j > i)
                    {
                        double totalDelta = 0.0;

                        for (int k2 = i; k2 < j; k2++)
                        {
                            var sk = segs[k2];
                            double len = sk.x2 - sk.x1;
                            double lenRounded = CeilToBase(len, step);
                            double delta = Math.Max(0, lenRounded - len);
                            if (delta <= 0) continue;

                            for (int m = k2; m <= j; m++)
                            {
                                var sm = segs[m];
                                if (m == k2) segs[m] = (sm.x1, sm.x2 + delta);
                                else segs[m] = (sm.x1 + delta, sm.x2 + delta);
                            }
                            totalDelta += delta;
                        }

                        if (totalDelta > 0)
                        {
                            var last = segs[j];
                            double lastLen = Math.Max(0, last.x2 - last.x1);
                            double newLastLen = Math.Max(0, lastLen - totalDelta);
                            segs[j] = (last.x1, last.x1 + newLastLen);
                        }
                    }

                    i = j + 1;
                }
            }

            // =========================
            // Helper vẽ dot sau làm tròn (không dùng ??= để tương thích C# 7.3)


            void DrawAdjustedHardCutDots(
                Canvas cvs, WCTransform tr, GridBotsecozu owner, int rowIndex,
                List<(double x1, double x2)> segs, List<double> rowCuts,
                double[] spanLeftArrLocal,
                double[] spanRightArrLocal,
                int spanCountLocal,

                //double y, double dotR, Brush brush, double offsetX = 0)

                double y, double dotR, Brush brush, double offsetX = 0,
                Func<double, double> offsetSelector = null,
                Func<double, double, double> targetXSelector = null)

            {
                if (segs == null || segs.Count < 2 || rowCuts == null || rowCuts.Count == 0) return;

                int y10 = (int)Math.Round(y * 10.0);
                if (_orangeSegCutMarkers.TryGetValue(owner, out var syncedMarkers)
                    && syncedMarkers != null
                    && syncedMarkers.Any(m => m.RowIndex == rowIndex && m.Y_10 == y10))
                {
                    return;
                }

                if (brush == null) brush = Brushes.Black;

                const double JoinTol = 0.5;
                // Tập ranh giới thật giữa các đoạn đã merged (chỉ khi 2 đoạn chạm nhau)
                var borders = new List<double>(Math.Max(0, segs.Count - 1));
                for (int i = 0; i < segs.Count - 1; i++)
                {
                    double gap = Math.Abs(segs[i + 1].x1 - segs[i].x2);
                    if (gap <= JoinTol)
                    {
                        // dùng trung điểm để an toàn số học
                        double bx = 0.5 * (segs[i].x2 + segs[i + 1].x1);
                        borders.Add(bx);
                    }
                }
                if (borders.Count == 0) return;

                var used = new bool[borders.Count];
                foreach (var cut in rowCuts)
                {
                    int cutSpanIdx = FindSpanIndexByX(cut, spanLeftArrLocal, spanRightArrLocal, spanCountLocal);
                    int best = -1;
                    double bestD = double.PositiveInfinity;
                    for (int i = 0; i < borders.Count; i++)
                    {
                        if (used[i]) continue;
                        if (cutSpanIdx >= 0)
                        {
                            int borderSpanIdx = FindSpanIndexByX(borders[i], spanLeftArrLocal, spanRightArrLocal, spanCountLocal);
                            if (borderSpanIdx != cutSpanIdx) continue;
                        }

                        double d = Math.Abs(borders[i] - cut);
                        if (d < bestD)
                        {
                            bestD = d;
                            best = i;
                        }
                    }
                    if (best >= 0)
                    {
                        used[best] = true;

                        double leftBaseX = segs[best].x1;
                        double xDot = targetXSelector?.Invoke(cut, leftBaseX)
                                      ?? (borders[best] + (offsetSelector?.Invoke(borders[best]) ?? offsetX));


                        DrawDotMm_Rec(cvs, tr, owner, xDot, y, rMm: dotR, layer: "MARK", fill: brush);
                    }
                }
            }

            void ApplyTonariShiftToMergedCuts(
                List<(double x1, double x2)> merged,
                List<double> rowCuts,
                double[] spanLeftArrLocal,
                double[] spanRightArrLocal,
                int spanCountLocal,
                Func<double, double> offsetSelector,
                Func<double, double, double> targetXSelector = null
)
            {
                if (merged == null || merged.Count < 2) return;
                if (rowCuts == null || rowCuts.Count == 0) return;

                const double JoinTol = 0.5;
                // border giữa các seg (chỉ khi chạm nhau)
                var borders = new List<double>();
                for (int i = 0; i < merged.Count - 1; i++)
                {
                    double gap = Math.Abs(merged[i + 1].x1 - merged[i].x2);
                    if (gap <= JoinTol)
                        borders.Add(0.5 * (merged[i].x2 + merged[i + 1].x1));
                }

                var used = new bool[borders.Count];

                foreach (var cut in rowCuts)
                {
                    int cutSpanIdx = FindSpanIndexByX(cut, spanLeftArrLocal, spanRightArrLocal, spanCountLocal);
                    int best = -1;
                    double bestD = double.MaxValue;

                    for (int i = 0; i < borders.Count; i++)
                    {
                        if (used[i]) continue;
                        if (cutSpanIdx >= 0)
                        {
                            int borderSpanIdx = FindSpanIndexByX(borders[i], spanLeftArrLocal, spanRightArrLocal, spanCountLocal);
                            if (borderSpanIdx != cutSpanIdx) continue;
                        }

                        double d = Math.Abs(borders[i] - cut);
                        if (d < bestD)
                        {
                            bestD = d;
                            best = i;
                        }
                    }

                    if (best < 0) continue;
                    used[best] = true;

                    double leftBaseX = merged[best].x1;
                    double targetX = targetXSelector?.Invoke(cut, leftBaseX)
                                     ?? (borders[best] + (offsetSelector?.Invoke(borders[best]) ?? 0));
                    double delta = targetX - borders[best];
                    if (Math.Abs(delta) < 1e-6) continue;

                    var L = merged[best];
                    var R = merged[best + 1];

                    L.x2 += delta;
                    R.x1 += delta;

                    if (L.x2 < L.x1) L.x2 = L.x1;
                    if (R.x1 > R.x2) R.x1 = R.x2;

                    merged[best] = L;
                    merged[best + 1] = R;
                }
            }


            // === Helper: VẼ TEXT CAM 2 dòng với ANKA
            // DÒNG TRÊN: D{φ}-{(lenRounded + ankaAdd)}
            // DÒNG DƯỚI: {lenRounded}  (luôn hiển thị)
            var (tonariOo, tonariKo) = GetTonariValues();
            void DimOrangeSegmentWithAnkaLabels(
                Canvas cvs, WCTransform tr, GridBotsecozu owner,
                double x1, double x2, double y,
                double dyTop, double dyBottom,
                double[] phiMidArray, double[] spanLeftArrLocal, double[] spanRightArrLocal, int spanCountLocal,
                double defaultLeftAnkaSigned, double defaultRightAnkaSigned,
                double leftAnkaSigned, double rightAnkaSigned,
                bool textAboveLine, // true: chữ nằm phía trên thanh cam
                double baseX1, double baseX2,
                bool suppressAnkaInTopLength = false,
                bool forceShowForCutChild = false,
                int rowIndex = 0    // ✅ optional để không bắt buộc sửa tất cả nơi gọi
            )
            {
                if (!ShowOrangeDims) return;

                double baseLen = (x2 - x1);
                // DIMは短尺でも、セグメントが存在する限り表示する（長さ調整「引く」後に消えないようにする）
                if (baseLen <= 1e-6) return;

                double cx = 0.5 * (x1 + x2);
                int si = FindSpanIndexByX(cx, spanLeftArrLocal, spanRightArrLocal, spanCountLocal);
                double phi = (si >= 0 && si < phiMidArray.Length) ? Math.Max(0, phiMidArray[si]) : 0.0;

                // Segment key (để xoá cả line + dim)
                var segKey = new OrangeSegKey(rowIndex, baseX1, baseX2, y);

                bool hitLeft = Math.Abs(defaultLeftAnkaSigned) > 0.0001;
                bool hitRight = Math.Abs(defaultRightAnkaSigned) > 0.0001;

                bool hasLeftAnka = Math.Abs(leftAnkaSigned) > 0.0001;
                bool hasRightAnka = Math.Abs(rightAnkaSigned) > 0.0001;

                double ankaAdd =
                    (hasLeftAnka ? Math.Abs(leftAnkaSigned) : 0.0) +
                    (hasRightAnka ? Math.Abs(rightAnkaSigned) : 0.0);
                // 等分/任意切断の子セグメントでは通常ANKA加算を抑制するが、
                // 実際にANKAが引き継がれている端部セグメントは上段DIMにANKAを含める。
                if (suppressAnkaInTopLength && !(hasLeftAnka || hasRightAnka)) ankaAdd = 0.0;

                // Text TOP
                double wxTop = cx;
                double wyTop = y + dyTop;
                var topKey = new OrangeDimTextKey(rowIndex, true, wxTop, wyTop);

                // Bottom key (nếu có)
                bool hasBotKey = false;
                OrangeDimTextKey botKey = default;
                if (hata1 == true)
                {
                    double wxBot = cx;
                    double wyBot = y + dyBottom;
                    botKey = new OrangeDimTextKey(rowIndex, false, wxBot, wyBot);
                    hasBotKey = true;
                }

                // Register mapping: DIM TOP -> segment + anka sides + bottom key
                RegisterOrangeSegInfo(owner, topKey, new OrangeSegInfo
                {
                    SegKey = segKey,
                    TopKey = topKey,
                    HasBottomKey = hasBotKey,
                    BottomKey = botKey,
                    HitLeftAnka = hitLeft,
                    HitRightAnka = hitRight,
                    DefaultLeftAnkaSigned = hitLeft ? defaultLeftAnkaSigned : 0.0,
                    DefaultRightAnkaSigned = hitRight ? defaultRightAnkaSigned : 0.0
                });

                // Nếu segment đã bị xoá thì không vẽ text (line sẽ bị skip ở vòng foreach bên ngoài)
                if (IsOrangeSegDeleted(owner, rowIndex, baseX1, baseX2, y))
                    return;

                string topTxt = $"D{phi:0}-{(baseLen + ankaAdd):0}";
                string topTxtDraw = GetOrangeDimText(owner, topKey, topTxt);

                var topTb = DrawText_Rec(
                    cvs, tr, owner,
                    topTxtDraw,
                    wxTop, wyTop,
                    dimFont, Brushes.Black,
                    HAnchor.Center,
                    textAboveLine ? VAnchor.Bottom : VAnchor.Top,
                    200, "DIM",
                    forceCanvasVisual: true
                );
                MakeOrangeDimTextEditable(topTb, cvs, tr, wxTop, wyTop, owner, topKey);

                // Text BOTTOM
                if (hata1 == true)
                {
                    // wxBot/wyBot và botKey đã được chuẩn bị ở trên để phục vụ delete
                    double wxBot = cx;
                    double wyBot = y + dyBottom;

                    string botTxt = $"({baseLen:0})";
                    string botTxtDraw = GetOrangeDimText(owner, botKey, botTxt);

                    var botTb = DrawText_Rec(
                        cvs, tr, owner,
                        botTxtDraw,
                        wxBot, wyBot,
                        dimFont, Brushes.Red,
                        HAnchor.Center,
                        textAboveLine ? VAnchor.Top : VAnchor.Bottom,
                        200, "DIM"
                    );
                    MakeOrangeDimTextEditable(botTb, cvs, tr, wxBot, wyBot, owner, botKey);
                }

                if (IsEqualCutMarker(owner, rowIndex, x1, y))
                {
                    DrawDotMm_Rec(cvs, tr, owner, x1, y, rMm: 30, layer: "DIM", fill: Brushes.Black);
                }

                if (IsEqualCutMarker(owner, rowIndex, x2, y))
                {
                    DrawDotMm_Rec(cvs, tr, owner, x2, y, rMm: 30, layer: "DIM", fill: Brushes.Black);
                }
            }



            void DimOrangeSegmentMidPhi(Canvas cvs, WCTransform tr, GridBotsecozu owner,
                                        double x1, double x2, double y, double dy,
                                        double[] phiMidArray,
                                        double[] spanLeftArrLocal,
                                        double[] spanRightArrLocal,
                                        int spanCountLocal)
            {
                if (!ShowOrangeDims) return;
                double len = (x2 - x1);
                if (len < MinDimLen) return;
                double cx = 0.5 * (x1 + x2);
                int si = FindSpanIndexByX(cx, spanLeftArrLocal, spanRightArrLocal, spanCountLocal);
                double phi = (si >= 0 && si < phiMidArray.Length) ? Math.Max(0, phiMidArray[si]) : 0.0;

                string txt = $"D{phi:0}-{len:0}";
                DrawText_Rec(cvs, tr, owner, txt, cx, y + dy, dimFont, Brushes.Black,
                             HAnchor.Center, VAnchor.Bottom, 200, "DIM");
            }

            // === 1) Trục & offset ===
            for (int i = 0; i < pos.Count; i++)
            {
                //DrawLine_Rec(canvas, T, item, pos[i], y0, pos[i], yH + 5000, Brushes.Red, AxisStrokeThickness, null, "AXIS");

                // Tên trục X và Y
                DrawText_Rec(canvas, T, item, i < names.Count ? names[i] : "", pos[i], y0, dimFont, Brushes.Black,
                                HAnchor.Center, VAnchor.Bottom, 200, "TEXT");

                if (tsuIsY)
                {
                    double xLeft = pos[i] - up[i];
                    double xRight = pos[i] + down[i];
                    // Trục X đỏ
                    DrawLine_Rec(canvas, T, item, pos[i], y0, pos[i], axisLineEndY, Brushes.Red, AxisStrokeThickness, null, "AXIS");

                    // Trục X xám 2 bên
                    DrawLine_Rec(canvas, T, item, xLeft, y0, xLeft, axisLineEndY, Brushes.Black, 1.2, null, "OFFSET");
                    DrawLine_Rec(canvas, T, item, xRight, y0, xRight, axisLineEndY, Brushes.Black, 1.2, null, "OFFSET");

                    if (pos[i] > xLeft)
                    {
                        DrawLine_Rec(canvas, T, item, xLeft, yChainMid, pos[i], yChainMid, Brushes.Black, 1.2, null, "DIM");
                        DrawDotMm_Rec(canvas, T, item, xLeft, yChainMid, rMm: 30, layer: "DIM", fill: dimBrush);
                        DrawDotMm_Rec(canvas, T, item, pos[i], yChainMid, rMm: 30, layer: "DIM", fill: dimBrush);
                        double cxL = (xLeft + pos[i]) / 2.0;
                        DrawText_Rec(canvas, T, item, $"{up[i]:0}", cxL, yChainMid, dimFont, dimBrush, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    }
                    if (xRight > pos[i])
                    {
                        DrawLine_Rec(canvas, T, item, pos[i], yChainMid, xRight, yChainMid, Brushes.Black, 1.2, null, "DIM");
                        DrawDotMm_Rec(canvas, T, item, pos[i], yChainMid, rMm: 30, layer: "DIM", fill: dimBrush);
                        DrawDotMm_Rec(canvas, T, item, xRight, yChainMid, rMm: 30, layer: "DIM", fill: dimBrush);
                        double cxR = (pos[i] + xRight) / 2.0;
                        DrawText_Rec(canvas, T, item, $"{down[i]:0}", cxR, yChainMid, dimFont, dimBrush, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    }
                }
                else if (tsuIsX)
                {
                    double xLeft = pos[i] - left[i];
                    double xRight = pos[i] + right[i];
                    // Trục Y đỏ
                    DrawLine_Rec(canvas, T, item, pos[i], y0, pos[i], axisLineEndY, Brushes.Red, AxisStrokeThickness, null, "AXIS");
                    // Trục Y xám 2 bên
                    DrawLine_Rec(canvas, T, item, xLeft, y0, xLeft, axisLineEndY, Brushes.Black, 1.2, null, "OFFSET");
                    DrawLine_Rec(canvas, T, item, xRight, y0, xRight, axisLineEndY, Brushes.Black, 1.2, null, "OFFSET");

                    if (pos[i] > xLeft)
                    {
                        DrawLine_Rec(canvas, T, item, xLeft, yChainMid, pos[i], yChainMid, Brushes.Black, 1.2, null, "DIM");
                        DrawDotMm_Rec(canvas, T, item, xLeft, yChainMid, rMm: 30, layer: "DIM", fill: dimBrush);
                        DrawDotMm_Rec(canvas, T, item, pos[i], yChainMid, rMm: 30, layer: "DIM", fill: dimBrush);
                        double cxL = (xLeft + pos[i]) / 2.0;
                        DrawText_Rec(canvas, T, item, $"{left[i]:0}", cxL, yChainMid, dimFont, dimBrush, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    }
                    if (xRight > pos[i])
                    {
                        DrawLine_Rec(canvas, T, item, pos[i], yChainMid, xRight, yChainMid, Brushes.Black, 1.2, null, "DIM");
                        DrawDotMm_Rec(canvas, T, item, pos[i], yChainMid, rMm: 30, layer: "DIM", fill: dimBrush);
                        DrawDotMm_Rec(canvas, T, item, xRight, yChainMid, rMm: 30, layer: "DIM", fill: dimBrush);
                        double cxR = (pos[i] + xRight) / 2.0;
                        DrawText_Rec(canvas, T, item, $"{right[i]:0}", cxR, yChainMid, dimFont, dimBrush, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    }
                }
            }

            // === 2) Chuỗi nối hiệu dụng + 1/4–1/2–1/4 ===
            if (tsuIsY)
            {
                for (int j = 0; j < pos.Count - 1; j++)
                {
                    double xA = pos[j] + down[j];
                    double xB = pos[j + 1] - up[j + 1];
                    if (xB <= xA) continue;

                    double eff = (pos[j + 1] - pos[j]) - (down[j] + up[j + 1]);
                    double cx = (xA + xB) / 2.0;

                    DrawLine_Rec(canvas, T, item, xA, yChainMid, xB, yChainMid, Brushes.Black, 1.2, null, "CHAIN");
                    DrawText_Rec(canvas, T, item, $"{eff:0}", cx, yChainMid, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");

                    ////////////////1 chổ////////
                    ////腹筋
                    var offset1 = OffsetAbdominalChainY;
                    const double abdominalLeftFallback = 200.0;
                    const double abdominalRightFallback = 200.0;

                    // 左の腹筋 hooks (độc lập)
                    double leftHook_L = GetTanbuHookLength(item, j, isRightAbdominal: false, isRightEnd: false, abdominalLeftFallback);
                    double rightHook_L = GetTanbuHookLength(item, j, isRightAbdominal: false, isRightEnd: true, abdominalRightFallback);

                    // 右の腹筋 hooks (độc lập)
                    double leftHook_R = GetTanbuHookLength(item, j, isRightAbdominal: true, isRightEnd: false, abdominalLeftFallback);
                    double rightHook_R = GetTanbuHookLength(item, j, isRightAbdominal: true, isRightEnd: true, abdominalRightFallback);

                    double midSpan = xA + ((xB - xA) / 2.0);
                    // 左の腹筋
                    DrawLine_Rec(canvas, T, item,
                        (xA - leftHook_L) + offset1.X, yChainMid + 7500 + offset1.Y,
                        (midSpan + rightHook_L) + offset1.X, yChainMid + 7500 + offset1.Y,
                        Brushes.Red, 1.5, null, "CHAIN");
                    // Chéo của thanh bên trái
                    DrawLine_Rec(canvas, T, item,
                       (midSpan + rightHook_L) + offset1.X, yChainMid + 7500 + offset1.Y,
                       (midSpan + rightHook_L) + offset1.X + 30, yChainMid + 7500 + offset1.Y - 30,
                       Brushes.Blue, 1.5, null, "CHAIN");

                    // 右の腹筋
                    DrawLine_Rec(canvas, T, item,
                         (midSpan - leftHook_R) + offset1.X, yChainMid + 7515 + offset1.Y,
                        (xB + rightHook_R) + offset1.X, yChainMid + 7515 + offset1.Y,
                        Brushes.Black, 1.5, null, "CHAIN");
                    // Chéo của thanh bên phải
                    DrawLine_Rec(canvas, T, item,
                        (midSpan - leftHook_R) + offset1.X, yChainMid + 7515 + offset1.Y,
                        (midSpan - leftHook_R) + offset1.X - 30, yChainMid + 7515 + offset1.Y - 30,
                        Brushes.Black, 1.5, null, "CHAIN");
                    /////////////// hết 1 chổ ////////////

                    double Lspan = xB - xA;
                    double xQ1 = xA + 0.25 * Lspan;
                    double xQ3 = xA + 0.75 * Lspan;

                    DrawLine_Rec(canvas, T, item, xA, yChainBot, xQ1, yChainBot, Brushes.Black, 1.2, null, "CHAIN");
                    DrawLine_Rec(canvas, T, item, xQ1, yChainBot, xQ3, yChainBot, Brushes.Black, 1.2, null, "CHAIN");
                    DrawLine_Rec(canvas, T, item, xQ3, yChainBot, xB, yChainBot, Brushes.Black, 1.2, null, "CHAIN");

                    double v14 = eff * 0.25, v12 = eff * 0.50;
                    DrawText_Rec(canvas, T, item, $"{v14:0}", (xA + xQ1) / 2.0, yChainBot, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, $"{v12:0}", (xQ1 + xQ3) / 2.0, yChainBot, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, $"{v14:0}", (xQ3 + xB) / 2.0, yChainBot, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");

                    DrawDotMm_Rec(canvas, T, item, xQ1, yChainBot, rMm: 25, layer: "MARK", fill: Brushes.Black);
                    DrawDotMm_Rec(canvas, T, item, xQ3, yChainBot, rMm: 25, layer: "MARK");
                }
            }
            else if (tsuIsX)
            {
                for (int j = 0; j < pos.Count - 1; j++)
                {
                    double xA = pos[j] + right[j];
                    double xB = pos[j + 1] - left[j + 1];
                    if (xB <= xA) continue;

                    double eff = (pos[j + 1] - pos[j]) - (right[j] + left[j + 1]);
                    double cx = (xA + xB) / 2.0;

                    DrawLine_Rec(canvas, T, item, xA, yChainMid, xB, yChainMid, Brushes.Black, 1.2, null, "CHAIN");
                    DrawText_Rec(canvas, T, item, $"{eff:0}", cx, yChainMid, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");

                    ///////////////// 2 chổ /////////////////
                    ////腹筋
                    var offset2 = OffsetAbdominalChainX;
                    const double abdominalLeftFallback = 200.0;
                    const double abdominalRightFallback = 200.0;
                    // 左の腹筋 hooks (độc lập)
                    double leftHook_L = GetTanbuHookLength(item, j, isRightAbdominal: false, isRightEnd: false, abdominalLeftFallback);
                    double rightHook_L = GetTanbuHookLength(item, j, isRightAbdominal: false, isRightEnd: true, abdominalRightFallback);

                    // 右の腹筋 hooks (độc lập)
                    double leftHook_R = GetTanbuHookLength(item, j, isRightAbdominal: true, isRightEnd: false, abdominalLeftFallback);
                    double rightHook_R = GetTanbuHookLength(item, j, isRightAbdominal: true, isRightEnd: true, abdominalRightFallback);

                    double midSpanX = xA + ((xB - xA) / 2.0);

                    // 左の腹筋
                    DrawLine_Rec(canvas, T, item,
                                  (xA - leftHook_L) + offset2.X, yChainMid + 7500 + offset2.Y,
                                  midSpanX + rightHook_L + offset2.X, yChainMid + 7500 + offset2.Y,
                                 Brushes.Red, 1.5, null, "CHAIN");
                    // Chéo của thanh bên trái
                    DrawLine_Rec(canvas, T, item,
                                 midSpanX + rightHook_L + offset2.X, yChainMid + 7500 + offset2.Y,
                                 midSpanX + rightHook_L + offset2.X + 30, yChainMid + 7500 + offset2.Y - 30,
                                 Brushes.Blue, 1.5, null, "CHAIN");

                    //右の腹筋
                    DrawLine_Rec(canvas, T, item,
                                 midSpanX - leftHook_R + offset2.X, yChainMid + 7515 + offset2.Y,
                                  (xB + rightHook_R) + offset2.X, yChainMid + 7515 + offset2.Y,
                                Brushes.Black, 1.5, null, "CHAIN");
                    // Chéo của thanh bên phải
                    DrawLine_Rec(canvas, T, item,
                                 midSpanX - leftHook_R + offset2.X, yChainMid + 7515 + offset2.Y,
                                midSpanX - leftHook_R + offset2.X - 30, yChainMid + 7515 + offset2.Y - 30,
                                Brushes.Black, 1.5, null, "CHAIN");
                    //////////////// hết 2 chổ //////////////


                    double Lspan = xB - xA;
                    double xQ1 = xA + 0.25 * Lspan;
                    double xQ3 = xA + 0.75 * Lspan;

                    DrawLine_Rec(canvas, T, item, xA, yChainBot, xQ1, yChainBot, Brushes.Black, 1.2, null, "CHAIN");
                    DrawLine_Rec(canvas, T, item, xQ1, yChainBot, xQ3, yChainBot, Brushes.Black, 1.2, null, "CHAIN");
                    DrawLine_Rec(canvas, T, item, xQ3, yChainBot, xB, yChainBot, Brushes.Black, 1.2, null, "CHAIN");

                    double v14 = eff * 0.25, v12 = eff * 0.50;
                    DrawText_Rec(canvas, T, item, $"{v14:0}", (xA + xQ1) / 2.0, yChainBot, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, $"{v12:0}", (xQ1 + xQ3) / 2.0, yChainBot, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, $"{v14:0}", (xQ3 + xB) / 2.0, yChainBot, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");

                    DrawDotMm_Rec(canvas, T, item, xQ1, yChainBot, rMm: 25, layer: "MARK", fill: Brushes.Black);
                    DrawDotMm_Rec(canvas, T, item, xQ3, yChainBot, rMm: 25, layer: "MARK");
                }
            }

            // === 3) Chuỗi nối mặc định (y=300) ===
            double yChain = 300;
            DrawLine_Rec(canvas, T, item, pos.First(), yChain, pos.Last(), yChain, Brushes.Black, 1.2, null, "CHAIN");

            double leftEdge, rightEdge;
            if (tsuIsY)
            {
                leftEdge = pos[0] - up[0];
                rightEdge = pos[pos.Count - 1] + down[down.Length - 1];
            }
            else
            {
                leftEdge = pos[0] - left[0];
                rightEdge = pos[pos.Count - 1] + right[right.Length - 1];
            }

            // ===== LẤY GIÁ TRỊ NIGE =====
            var (nigeUwa, nigeUwaChu1, nigeUwaChu2, nigeShitaChu2, nigeShitaChu1, nigeShita) = GetNigeValues();

            // ===== Hằng số khoảng cách hiển thị giá trị dưới nhãn =====
            const double LabelDy = 200.0;
            const double ValueDy = 800.0;
            // === 3b) Thu thập dữ liệu span ===
            int spanCount = Math.Max(0, names.Count - 1);
            var qLArr = new double[spanCount];
            var qRArr = new double[spanCount];

            var qR1Arr = new double[spanCount]; // lưu qR1 để dùng cắt cho 下*

            var spanLeftArr = new double[spanCount];
            var spanRightArr = new double[spanCount];
            var midArr = new double[spanCount];
            var nEnd1Arr = new int[spanCount];
            var nMidArr = new int[spanCount];
            var nEnd2Arr = new int[spanCount];

            var diaE1Arr = new double[spanCount];
            var diaE2Arr = new double[spanCount];
            var diaMidArr = new double[spanCount];

            var (gTanbu, nTanbu, gChubu, nChubu) = GetYochouValues();
            var (teiUwa, teiUwaChu, teiShitaChu, teiShitaFlag) = GetTeiFlags();
            bool showStirrupMaterial = !(_projectData?.Kesan?.STPzaishitsuon2 ?? false);

            for (int i = 0; i < spanCount && i < spans.Count; i++)
            {
                string leftNameS = names[i];
                string rightNameS = names[i + 1];
                var (G0, 上側, 下側, 梁の段差1) = GetBeamValuesByPosition(selF, selY, leftNameS, rightNameS);
                var zcfg = GetRebarConfigForSpan(selF, string.IsNullOrWhiteSpace(G0) ? "G0" : G0);
                var (中央幅, 中央成, 中央スタラップ径, ピッチ, スタラップ材質, 端部1幅止筋径, 端部1幅止筋ピッチ, 中央中子筋径,
                                   中央中子筋径ピッチ, 中央中子筋材質, 端部1腹筋径) = GetBeamSize(selF, G0);
                (中央幅, 中央成) = GetSpanBeamDimensions(item, i, 中央幅, 中央成);
                (中央スタラップ径, ピッチ, スタラップ材質) = GetSpanCentralStirrupValues(item, i, 中央スタラップ径, ピッチ, スタラップ材質);
                (端部1幅止筋径, 端部1幅止筋ピッチ) = GetSpanEndWidthStopValues(item, i, 端部1幅止筋径, 端部1幅止筋ピッチ);
                (中央中子筋径, 中央中子筋径ピッチ, 中央中子筋材質) = GetSpanCentralIntermediateValues(item, i, 中央中子筋径, 中央中子筋径ピッチ, 中央中子筋材質);
                var (ankaUwa, ankaUwaChu, ankaShitaChu, ankaShita) = GetAnkaNagaValues();
                double x0 = pos[i];
                double x1 = pos[i + 1];
                double L = x1 - x0;

                double mid = x0 + L / 2.0;
                double qL = (x0 + L / 4.0) + 250;
                double qR = (x1 - L / 4.0) - 250;
                double qL1 = (x0 + L / 8.0) + 250 + 125;
                double qR1 = (x1 - L / 8.0) - 250 - 125;

                qLArr[i] = qL; qRArr[i] = qR; midArr[i] = mid;
                qR1Arr[i] = qR1;
                spanLeftArr[i] = tsuIsY ? (pos[i] - up[i]) : (pos[i] - left[i]);
                spanRightArr[i] = tsuIsY ? (pos[i + 1] + down[i + 1]) : (pos[i + 1] + right[i + 1]);



                double tanbuTextY = yChainMid + 7450;
                string tanbuDisplay = $"D{端部1腹筋径}-";

                if (i < pos.Count - 1)
                {
                    double eff;
                    if (tsuIsY)
                    {
                        double downVal = (i < down.Length) ? down[i] : 0;
                        double upNext = (i + 1 < up.Length) ? up[i + 1] : 0;
                        eff = (pos[i + 1] - pos[i]) - (downVal + upNext);
                    }
                    else
                    {
                        double rightVal = (i < right.Length) ? right[i] : 0;
                        double leftNext = (i + 1 < left.Length) ? left[i + 1] : 0;
                        eff = (pos[i + 1] - pos[i]) - (rightVal + leftNext);
                    }

                    if (eff > 0)
                    {
                        // --- fallback theo eff (KHÔNG đổi) ---
                        //double hookLengthFallback = 200 + (eff / 2.0) + 300;
                        // --- fallback theo eff (giữ logic cho 左/右) ---
                        const double abdominalLeftFallback = 200.0;
                        const double abdominalRightFallback = 200.0;
                        string leftTanbuDiameter = GetTanbuDiameter(item, i, isRightAbdominal: false, 端部1腹筋径);
                        string rightTanbuDiameter = GetTanbuDiameter(item, i, isRightAbdominal: true, 端部1腹筋径);
                        // --- hiển thị: lấy override theo từng bên ---
                        //double hookLengthLeft = GetTanbuHookLength(item, i, isRight: false, hookLengthFallback);
                        //double hookLengthRight = GetTanbuHookLength(item, i, isRight: true, hookLengthFallback);

                        // 左の腹筋 hooks (độc lập)
                        double leftHook_L = GetTanbuHookLength(item, i, isRightAbdominal: false, isRightEnd: false, abdominalLeftFallback);
                        double rightHook_L = GetTanbuHookLength(item, i, isRightAbdominal: false, isRightEnd: true, abdominalRightFallback);

                        // 右の腹筋 hooks (độc lập)
                        double leftHook_R = GetTanbuHookLength(item, i, isRightAbdominal: true, isRightEnd: false, abdominalLeftFallback);
                        double rightHook_R = GetTanbuHookLength(item, i, isRightAbdominal: true, isRightEnd: true, abdominalRightFallback);

                        // Tổng chiều dài 3 đoạn mỗi bên: leftHook + eff/2 + rightHook
                        double hookLengthLeft = (eff / 2.0) + leftHook_L + rightHook_L;   // 左の腹筋 total
                        double hookLengthRight = (eff / 2.0) + leftHook_R + rightHook_R;  // 右の腹筋 total


                        //string leftDisplay = $"D{端部1腹筋径}-{hookLengthLeft:0}";
                        //string rightDisplay = $"D{端部1腹筋径}-{hookLengthRight:0}";

                        string leftDisplayText = $"D{leftTanbuDiameter}-{hookLengthLeft:0}";
                        string rightDisplayText = $"D{rightTanbuDiameter}-{hookLengthRight:0}";
                        double hookCenter = pos[i] + (eff / 2.0);

                        var offset3 = OffsetTanbuText;

                        var leftText = DrawText_Rec(canvas, T, item, leftDisplayText,
                            hookCenter - 700 + offset3.X, tanbuTextY + offset3.Y,
                            dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT",
                            forceCanvasVisual: true);

                        var rightText = DrawText_Rec(canvas, T, item, rightDisplayText,
                            hookCenter + 1800 + offset3.X, tanbuTextY + offset3.Y,
                            dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT",
                            forceCanvasVisual: true);

                        MakeTanbuFukkinEditable(
                            leftText, canvas, T,
                            hookCenter - 700 + offset3.X, tanbuTextY + offset3.Y,
                            item,
                            selF, G0,
                            端部1腹筋径,
                            i,
                            false,
                            eff / 2.0,
                             //() => hookLengthFallback);
                             abdominalLeftFallback,
                            abdominalRightFallback);

                        MakeTanbuFukkinEditable(
                            rightText, canvas, T,
                            hookCenter + 1800 + offset3.X, tanbuTextY + offset3.Y,
                            item,
                            selF, G0,
                            端部1腹筋径,
                            i,
                            true,
                            eff / 2.0,
                            //() => hookLengthFallback);
                            abdominalLeftFallback,
                            abdominalRightFallback);


                        ///////////// hết 3 chổ ///////////////////////

                    }
                }

                int nE1 = 0, nM = 0, nE2 = 0;
                int.TryParse(zcfg?.端部1上筋本数, out nE1);
                int.TryParse(zcfg?.中央上筋本数, out nM);
                int.TryParse(zcfg?.端部2上筋本数, out nE2);
                if (nE1 < 0) nE1 = 0; if (nM < 0) nM = 0; if (nE2 < 0) nE2 = 0;
                nEnd1Arr[i] = nE1; nMidArr[i] = nM; nEnd2Arr[i] = nE2;

                double d1 = 0, d2 = 0, dMid = 0;
                double.TryParse(zcfg?.端部1主筋径, out d1);
                double.TryParse(zcfg?.端部2主筋径, out d2);
                if (!double.TryParse(zcfg?.中央主筋径, out dMid))
                {
                    double temp;
                    if (double.TryParse(zcfg?.中央主筋径, out temp)) dMid = temp;
                }
                if (d1 < 0) d1 = 0; if (d2 < 0) d2 = 0; if (dMid < 0) dMid = 0;
                diaE1Arr[i] = d1;
                diaE2Arr[i] = d2;
                diaMidArr[i] = dMid;

                var offset4 = OffsetCentralStirrupFrame;

                // Nhãn/khung và mốc qL/qR
                double cx = (x0 + x1) / 2.0;
                double yChainLocal = yChainLocalBase;
                // Span chính //
                DrawEditableSpanLabel(canvas, T, cx, yChainLocal, i, spans[i].ToString("0"), tsuIsX, tsuIsY, item);
                DrawLine_Rec(canvas, T, item, qL, yChainLocal + 600, qL, yChainLocal + offset4.Y + 10000, Brushes.Green, 1.2, null, "MARK");
                DrawLine_Rec(canvas, T, item, qR, yChainLocal + 600, qR, yChainLocal + offset4.Y + 10000, Brushes.Green, 1.2, null, "MARK");

                // Khung xanh qL1
                DrawLine_Rec(canvas, T, item, qL1 - 500, yChainLocal + 1300, qL1 + 500, yChainLocal + 1300, Brushes.Blue, 1.2, null, "CHAIN");
                DrawLine_Rec(canvas, T, item, qL1 - 500, yChainLocal + 2800, qL1 + 500, yChainLocal + 2800, Brushes.Blue, 1.2, null, "CHAIN");
                DrawLine_Rec(canvas, T, item, qL1 - 500, yChainLocal + 1300, qL1 - 500, yChainLocal + 2800, Brushes.Blue, 1.2, null, "MARK");
                DrawLine_Rec(canvas, T, item, qL1 + 500, yChainLocal + 1300, qL1 + 500, yChainLocal + 2800, Brushes.Blue, 1.2, null, "MARK");
                // Khung xanh qR1
                DrawLine_Rec(canvas, T, item, qR1 - 500, yChainLocal + 1300, qR1 + 500, yChainLocal + 1300, Brushes.Blue, 1.2, null, "CHAIN");
                DrawLine_Rec(canvas, T, item, qR1 - 500, yChainLocal + 2800, qR1 + 500, yChainLocal + 2800, Brushes.Blue, 1.2, null, "CHAIN");
                DrawLine_Rec(canvas, T, item, qR1 - 500, yChainLocal + 1300, qR1 - 500, yChainLocal + 2800, Brushes.Blue, 1.2, null, "MARK");
                DrawLine_Rec(canvas, T, item, qR1 + 500, yChainLocal + 1300, qR1 + 500, yChainLocal + 2800, Brushes.Blue, 1.2, null, "MARK");

                // Text giữa span
                DrawText_Rec(canvas, T, item, $"{G0}", mid, yChainLocal + 900, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                DrawText_Rec(canvas, T, item, $"({梁の段差1})", mid, yChainLocal + 1200, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                if (zcfg != null)
                {
                    DrawText_Rec(canvas, T, item, zcfg.中央上筋本数, mid, yChainLocal + 1550, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.中央上宙1, mid, yChainLocal + 1750, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.中央上宙2, mid, yChainLocal + 1950, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.中央下宙2, mid, yChainLocal + 2350, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.中央下宙1, mid, yChainLocal + 2550, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.中央下筋本数, mid, yChainLocal + 2750, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");

                    DrawText_Rec(canvas, T, item, zcfg.端部1上筋本数, qL1, yChainLocal + 1550, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.端部1上宙1, qL1, yChainLocal + 1750, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.端部1上宙2, qL1, 12 + yChainLocal + 1950, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.端部1下宙2, qL1, yChainLocal + 2350, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.端部1下宙1, qL1, yChainLocal + 2550, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.端部1下筋本数, qL1, yChainLocal + 2750, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");

                    DrawText_Rec(canvas, T, item, zcfg.端部2上筋本数, qR1, yChainLocal + 1550, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.端部2上宙1, qR1, yChainLocal + 1750, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.端部2上宙2, qR1, yChainLocal + 1950, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.端部2下宙2, qR1, yChainLocal + 2350, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.端部2下宙1, qR1, yChainLocal + 2550, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, zcfg.端部2下筋本数, qR1, yChainLocal + 2750, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                }

                // Khung xanh giữa
                DrawLine_Rec(canvas, T, item, mid - 500, yChainLocal + 1300, mid + 500, yChainLocal + 1300, Brushes.Blue, 1.2, null, "CHAIN");
                DrawLine_Rec(canvas, T, item, mid - 500, yChainLocal + 2800, mid + 500, yChainLocal + 2800, Brushes.Blue, 1.2, null, "CHAIN");
                DrawLine_Rec(canvas, T, item, mid - 500, yChainLocal + 1300, mid - 500, yChainLocal + 2800, Brushes.Blue, 1.2, null, "MARK");
                DrawLine_Rec(canvas, T, item, mid + 500, yChainLocal + 1300, mid + 500, yChainLocal + 2800, Brushes.Blue, 1.2, null, "MARK");

                ///////////////////// chổ này ////////////////////////
                /////////////// 4 chổ /////////////////////

                //9 trường hợp スタラップ筋
                double xR = mid + 350; //Bên phải
                double xL = mid - 350; //Bên trái
                double yT = yChainLocal + 8300;

                double r = 50; // ≤ 400 để nằm gọn trong khung

                double cx1 = xR - r; // tâm ở trong khung phải
                double cx2 = xL + r; //tâm ở trong khung trái
                double cy = yT + r;


                // ----- 1) TÍNH 2 ĐIỂM ĐẦU CUNG (world mm) -----
                double startDeg = -135;
                double endDeg = 45;

                // Cung từ 180° -> 315° (tức -180 -> -45) theo chuẩn CCW DXF
                double startDegL = -180;   // 180°
                double endDegL = -45;    // 315°


                double sr = startDeg * Math.PI / 180.0;
                double er = endDeg * Math.PI / 180.0;

                double srL = startDegL * Math.PI / 180.0;
                double erL = endDegL * Math.PI / 180.0;


                // Điểm đầu & cuối cung
                double sxR = cx1 + r * Math.Cos(sr);
                double sxL = cx2 + r * Math.Cos(srL);

                double sy = cy + r * Math.Sin(sr);
                double syL = cy + r * Math.Sin(srL);

                double exR = cx1 + r * Math.Cos(er);
                double exL = cx2 + r * Math.Cos(erL);

                double ey = cy + r * Math.Sin(er);
                double eyL = cy + r * Math.Sin(erL);



                // ----- 2) VẼ 2 ĐOẠN THẲNG “KÉO DÀI RA” TỪ MỖI ĐIỂM -----
                // a) Nếu muốn kéo theo TIẾP TUYẾN của cung:
                double L1 = 100; // chiều dài đoạn kéo, mm (đổi tuỳ ý)

                // Vector tiếp tuyến đơn vị tại góc θ (chuẩn CCW từ +X)
                double tsxR = -Math.Sin(sr), tsyR = Math.Cos(sr); // Bên phải
                double tsxL = -Math.Sin(srL), tsyL = Math.Cos(srL); // Bên trái

                double tex = -Math.Sin(er), tey = Math.Cos(er);
                double texL = -Math.Sin(erL), teyL = Math.Cos(erL);


                ////Trên
                var stirrupShapes = new List<Shape>();
                using (BeginCollectShapes(stirrupShapes))
                {
                    DrawLine_Rec(canvas, T, item,
                             mid - 350 + offset4.X, yChainLocal + 8300 + offset4.Y,
                             mid + 350 + offset4.X, yChainLocal + 8300 + offset4.Y,
                             Brushes.Blue, 1.2, null, "CHAIN");
                    ////Dưới
                    DrawLine_Rec(canvas, T, item,
                                 mid - 350 + offset4.X, yChainLocal + 9300 + offset4.Y,
                                 mid + 350 + offset4.X, yChainLocal + 9300 + offset4.Y,
                                 Brushes.Blue, 1.2, null, "CHAIN");
                    ////Trái
                    DrawLine_Rec(canvas, T, item,
                                 mid - 350 + offset4.X, yChainLocal + 8300 + offset4.Y,
                                 mid - 350 + offset4.X, yChainLocal + 9300 + offset4.Y,
                                 Brushes.Blue, 1.2, null, "MARK");
                    ////Phải
                    DrawLine_Rec(canvas, T, item,
                                 mid + 350 + offset4.X, yChainLocal + 8300 + offset4.Y,
                                 mid + 350 + offset4.X, yChainLocal + 9300 + offset4.Y,
                                 Brushes.Blue, 1.2, null, "MARK");

                    //Text  rộng dài khung
                    double grossWidth = ParseDoubleInvariant(中央幅);
                    double grossHeight = ParseDoubleInvariant(中央成);
                    var (coverTop, coverBottom, coverLeft, coverRight) = GetCentralProtectiveCovers(zcfg);
                    double netWidth = grossWidth - coverLeft - coverRight;
                    double netHeight = grossHeight - coverTop - coverBottom;
                    double adjustedNetHeight = AdjustCentralNetHeight(netHeight, zcfg?.中央主筋径);


                    //600x900

                    //DrawText_Rec(canvas, T, item,
                    //            $"({(grossWidth)}x{(grossHeight)})",

                    string grossLabel = string.Format(CultureInfo.InvariantCulture, "({0}x{1})", grossWidth, grossHeight);
                    var grossText = DrawText_Rec(canvas, T, item,
                                grossLabel,
                                mid, yChainLocal + 3100, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT",
                                forceCanvasVisual: true);
                    MakeBeamSizeEditable(grossText, canvas, T, mid, yChainLocal + 3000, selF, G0, item, i);

                    //(500x744)
                    DrawText_Rec(canvas, T, item,
                                $"({netWidth}x{adjustedNetHeight})",
                                mid + offset4.X, yChainLocal + 9600 + offset4.Y, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    //D13@120(SD390)

                    string centralStirrupMaterial = showStirrupMaterial ? $"({(スタラップ材質)})" : string.Empty;

                    string diaPart = $"D{(中央スタラップ径)}";
                    string pitchPart = $"@{ピッチ}";
                    string matPart = centralStirrupMaterial;

                    TextBlock tbDia, tbPitch, tbMat;
                    DrawCentralStirrupTripletPx(
                        canvas, T, item,
                        mid + offset4.X, yChainLocal + 9920 + offset4.Y,
                        dimFont, Brushes.Black,
                        diaPart, pitchPart, matPart,
                        out tbDia, out tbPitch, out tbMat);

                    // ✅ từ đây: gắn editable vào tbPitch (pitch), nhưng clamp bằng tbDia / tbMat
                    MakeCentralStirrupEditable(
                         tbDia, tbPitch, tbMat,    // ✅ đúng: Dia trước, Pitch giữa, Mat sau
                         canvas, T,
                         mid + offset4.X, yChainLocal + 9920 + offset4.Y,
                         item, selF, G0,
                         i,
                         中央スタラップ径, ピッチ, スタラップ材質,
                         showStirrupMaterial);



                    //(P56)
                    DrawText_Rec(canvas, T, item,
                                $"(P56)",
                                mid + offset4.X, yChainLocal + 10180 + offset4.Y, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    // 500
                    DrawText_Rec(canvas, T, item,
                                $"{netWidth}",
                                mid + offset4.X, yChainLocal + 10550 + offset4.Y, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");

                    //[D10@200]
                    string endDiaPart = $"[ D{端部1幅止筋径}";
                    string endPitchPart = $"@{端部1幅止筋ピッチ}";
                    string endRightPart = $"]";

                    TextBlock tbEndDia, tbEndPitch, tbEndRight;
                    DrawCentralStirrupTripletPx(
                        canvas, T, item,
                        mid + offset4.X, yChainLocal + 10850 + offset4.Y,
                        dimFont, Brushes.Black,
                        endDiaPart, endPitchPart, endRightPart,
                        out tbEndDia, out tbEndPitch, out tbEndRight);

                    MakeEndWidthStopEditable(
                        tbEndDia, tbEndPitch, tbEndRight,
                        canvas, T,
                        mid + offset4.X, yChainLocal + 10850 + offset4.Y,
                        item, selF, G0,
                        i,
                        端部1幅止筋径, 端部1幅止筋ピッチ);

                    //(P7)
                    DrawText_Rec(canvas, T, item,
                                $"(P7)",
                                mid + offset4.X, yChainLocal + 11100 + offset4.Y, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    // 744
                    DrawText_Rec(canvas, T, item,
                                $"{adjustedNetHeight}",
                                mid + offset4.X, yChainLocal + 12850 + offset4.Y, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    //D13@180(SD390)
                    //string centralIntermediateMaterial = showStirrupMaterial ? $"({(中央中子筋材質)})" : string.Empty;
                    string diaPart2 = $"D{(中央中子筋径)}";
                    string pitchPart2 = $"@{中央中子筋径ピッチ}";
                    string matPart2 = showStirrupMaterial ? $"({(中央中子筋材質)})" : string.Empty;

                    TextBlock tbDia2, tbPitch2, tbMat2;
                    DrawCentralStirrupTripletPx(
                        canvas, T, item,
                        mid + offset4.X, yChainLocal + 13150 + offset4.Y,
                        dimFont, Brushes.Black,
                        diaPart2, pitchPart2, matPart2,
                        out tbDia2, out tbPitch2, out tbMat2);

                    MakeCentralIntermediateEditable(
                        tbDia2, tbPitch2, tbMat2,
                        canvas, T,
                        mid + offset4.X, yChainLocal + 13150 + offset4.Y,
                        item, selF, G0,
                        i,
                        中央中子筋径, 中央中子筋径ピッチ, 中央中子筋材質,
                        showStirrupMaterial);

                    // (P56)
                    DrawText_Rec(canvas, T, item,
                                $"(P56)",
                                mid + offset4.X, yChainLocal + 13400 + offset4.Y, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");

                    int centralStirrupShape = 1;
                    if (!int.TryParse(zcfg?.中央スタラップ形, out centralStirrupShape))
                    {
                        centralStirrupShape = 1;
                    }
                    centralStirrupShape = GetSpanCentralStirrupShape(item, i, centralStirrupShape);

                    if (centralStirrupShape == 1)
                    {
                        // Trường hợp 1 スタラップ筋
                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -135, endDeg: 45,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, layer: "MARK");
                        // Vẽ 2 đoạn tiếp tuyến bắt đầu từ 2 đầu cung
                        DrawLine_Rec(canvas, T, item,
                                     sxR + offset4.X, sy + offset4.Y,
                                     sxR - L1 * tsxR + offset4.X, sy - L1 * tsyR + offset4.Y,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                        DrawLine_Rec(canvas, T, item,
                                     exR + offset4.X, ey + offset4.Y,
                                     exR + L1 * tex + offset4.X, ey + L1 * tey + offset4.Y,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                    }

                    if (centralStirrupShape == 2)
                    {
                        // Trường hợp 2 スタラップ筋
                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -135, endDeg: 0,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, layer: "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     sxR + offset4.X, sy + offset4.Y,
                                     sxR - L1 * tsxR + offset4.X, sy - L1 * tsyR + offset4.Y,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                        DrawLine_Rec(canvas, T, item,
                                     xR + offset4.X, r + yT + offset4.Y,
                                     xR + offset4.X, r + yT + L1 + offset4.Y,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                    }

                    if (centralStirrupShape == 3)
                    {
                        // Trường hợp 3 スタラップ筋
                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -135, endDeg: 0,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     sxR + offset4.X, sy + offset4.Y,
                                     sxR - L1 * tsxR + offset4.X, sy - L1 * tsyR + offset4.Y,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -180, endDeg: -45,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                   exL + offset4.X, eyL + offset4.Y,
                                   exL + L1 * texL + offset4.X, eyL + L1 * teyL + offset4.Y,
                                   Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -90, endDeg: 0,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -180, endDeg: -90,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 350 + r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     mid + 350 - r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     Brushes.Red, 1.2, null, "CHAIN");

                        DrawLine_Rec(canvas, T, item,
                                     mid + 350 + offset4.X, r + yChainLocal + 8300 + offset4.Y,
                                     mid + 350 + offset4.X, r + yChainLocal + 8300 + L1 - 50 + offset4.Y,
                                     Brushes.Red, 1.5, null, "MARK");
                        DrawLine_Rec(canvas, T, item,
                                     mid - 350 + offset4.X, r + yChainLocal + 8300 + offset4.Y,
                                     mid - 350 + offset4.X, r + yChainLocal + 8300 + L1 - 50 + offset4.Y,
                                     Brushes.Red, 1.5, null, "MARK");
                    }

                    if (centralStirrupShape == 4)
                    {
                        // Trường hợp 4 スタラップ筋
                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -135, endDeg: 45,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     sxR + offset4.X, sy + offset4.Y,
                                     sxR - L1 * tsxR + offset4.X, sy - L1 * tsyR + offset4.Y,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                        DrawLine_Rec(canvas, T, item,
                                     exR + offset4.X, ey + offset4.Y,
                                     exR + L1 * tex + offset4.X, ey + L1 * tey + offset4.Y,
                                     Brushes.Red, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -180, endDeg: -45,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                   exL + offset4.X, eyL + offset4.Y,
                                   exL + L1 * texL + offset4.X, eyL + L1 * teyL + offset4.Y,
                                   Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -90, endDeg: 45,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -180, endDeg: -90,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 350 + r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     mid + 350 - r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     Brushes.Red, 1.2, null, "CHAIN");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 350 + offset4.X, r + yChainLocal + 8300 + offset4.Y,
                                     mid - 350 + offset4.X, r + yChainLocal + 8300 + L1 - 50 + offset4.Y,
                                     Brushes.Red, 1.5, null, "MARK");
                    }

                    if (centralStirrupShape == 5)
                    {
                        // Trường hợp 5 スタラップ筋
                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -180, endDeg: 0,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     xR - (r * 2) + offset4.X, yT + r + offset4.Y,
                                     xR - (r * 2) + offset4.X, yT + r + L1 - 50 + offset4.Y,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                        DrawLine_Rec(canvas, T, item,
                                     xL + (r * 2) + offset4.X, yT + r + offset4.Y,
                                     xL + (r * 2) + offset4.X, yT + r + L1 - 50 + offset4.Y,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -180, endDeg: 0,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -90, endDeg: 0,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -180, endDeg: -90,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 350 + r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     mid + 350 - r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     Brushes.Red, 1.2, null, "CHAIN");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 350 + offset4.X, r + yChainLocal + 8300 + offset4.Y,
                                     mid - 350 + offset4.X, r + yChainLocal + 8300 + L1 - 50 + offset4.Y,
                                     Brushes.Red, 1.5, null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid + 350 + offset4.X, r + yChainLocal + 8300 + offset4.Y,
                                     mid + 350 + offset4.X, r + yChainLocal + 8300 + L1 - 50 + offset4.Y,
                                     Brushes.Red, 1.5, null, "MARK");
                    }

                    if (centralStirrupShape == 6)
                    {
                        // Trường hợp 6 スタラップ筋
                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -180, endDeg: 45,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     xR - r * 2 + offset4.X, yT + r + offset4.Y,
                                     xR - r * 2 + offset4.X, yT + r + L1 - 50 + offset4.Y,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                        DrawLine_Rec(canvas, T, item,
                                     exR + offset4.X, ey + offset4.Y,
                                     exR + L1 * tex + offset4.X, ey + L1 * tey + offset4.Y,
                                     Brushes.Red, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -180, endDeg: -0,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                   xL + r * 2 + offset4.X, yT + r + offset4.Y,
                                   xL + r * 2 + offset4.X, yT + r + L1 - 50 + offset4.Y,
                                   Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -90, endDeg: 45,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -180, endDeg: -90,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 350 + r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     mid + 350 - r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     Brushes.Red, 1.2, null, "CHAIN");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 350 + offset4.X, r + yChainLocal + 8300 + offset4.Y,
                                     mid - 350 + offset4.X, r + yChainLocal + 8300 + L1 - 50 + offset4.Y,
                                     Brushes.Red, 1.5, null, "MARK");
                    }

                    double startDegL1 = -225;
                    double srL1 = startDegL1 * Math.PI / 180.0;
                    double sxL1 = cx2 + r * Math.Cos(srL1);
                    double syL1 = cy + r * Math.Sin(srL1);
                    double tsxL1 = -Math.Sin(srL1), tsyL1 = Math.Cos(srL1); // Bên trái

                    if (centralStirrupShape == 7)
                    {
                        // Trường hợp 7 スタラップ筋
                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -135, endDeg: 45,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     sxR + offset4.X, sy + offset4.Y,
                                     sxR - L1 * tsxR, sy - L1 * tsyR + offset4.Y,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                        DrawLine_Rec(canvas, T, item,
                                     exR + offset4.X, ey + offset4.Y,
                                     exR + L1 * tex, ey + L1 * tey + offset4.Y,
                                     Brushes.Red, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -225, endDeg: -45,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                   exL + offset4.X, eyL + offset4.Y,
                                   exL + L1 * texL + offset4.X, eyL + L1 * teyL + offset4.Y,
                                   Brushes.DeepSkyBlue, 1.5, null, "MARK"); // Đoạn chéo từ điểm kết thúc cung tròn trái

                        DrawLine_Rec(canvas, T, item,
                                    sxL1 + offset4.X, syL1 + offset4.Y,
                                    sxL1 - L1 * tsxL1 + offset4.X, syL1 - L1 * tsyL1 + offset4.Y,
                                    Brushes.Red, 1.5, null, "MARK"); // Đoạn chéo từ điểm bắt đầu cung tròn trái

                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -90, endDeg: 45,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -225, endDeg: -90,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 350 + r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     mid + 350 - r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     Brushes.Red, 1.2, null, "CHAIN");
                    }

                    if (centralStirrupShape == 8)
                    {
                        // Trường hợp 8 スタラップ筋
                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -180, endDeg: 45,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     xR - (2 * r) + offset4.X, yT + r + offset4.Y,
                                     xR - (2 * r) + offset4.X, yT + r + 50 + offset4.Y,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                        DrawLine_Rec(canvas, T, item,
                                     exR + offset4.X, ey + offset4.Y,
                                     exR + L1 * tex + offset4.X, ey + L1 * tey + offset4.Y,
                                     Brushes.Red, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -225, endDeg: 0,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                   xL + (2 * r) + offset4.X, yT + r + offset4.Y,
                                   xL + (2 * r) + offset4.X, yT + r + 50 + offset4.Y,
                                   Brushes.DeepSkyBlue, 1.5, null, "MARK"); // Đoạn ngắn biên trái

                        DrawLine_Rec(canvas, T, item,
                                    sxL1 + offset4.X, syL1 + offset4.Y,
                                    sxL1 - L1 * tsxL1 + offset4.X, syL1 - L1 * tsyL1 + offset4.Y,
                                    Brushes.Red, 1.5, null, "MARK"); // Đoạn chéo từ điểm bắt đầu cung tròn trái

                        DrawArc_Rec(canvas, T, item, cx1 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -90, endDeg: 45,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + offset4.X, cy + offset4.Y, r,
                                    startDeg: -225, endDeg: -90,
                                    stroke: Brushes.Red, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 350 + r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     mid + 350 - r + offset4.X, yChainLocal + 8300 + offset4.Y,
                                     Brushes.Red, 1.2, null, "CHAIN");
                    }
                    MakeCentralStirrupShapeEditable(
                                   canvas, T, item,
                                   selF, G0,
                                   i,
                                   centralStirrupShape,
                                   stirrupShapes,
                                   // vùng hit theo đúng khung bạn vẽ (mid±350, 8300..9300)
                                   hitCenterXmm: mid + offset4.X,
                                   hitCenterYmm: (yChainLocal + 8800) + offset4.Y,
                                   hitWidthMm: 900,
                                   hitHeightMm: 1000);
                }

                // ===================== 中子 8 trường hợp =====================
                int centralNakagoShape = 1;
                if (!int.TryParse(zcfg?.中央中子筋形, out centralNakagoShape) || centralNakagoShape < 1 || centralNakagoShape > 8)
                {
                    centralNakagoShape = 1;
                }
                centralNakagoShape = GetSpanCentralIntermediateShape(item, i, centralNakagoShape);

                // Offset riêng cho phần Nakago (giữ nguyên offset4, chỉ cộng thêm ở đúng chỗ)
                double offsetNakagoX = 0;
                double offsetNakagoY = 00;

                // 1) TẠO LIST TRƯỚC KHI VẼ
                var nakagoShapes = new List<Shape>();
                using (BeginCollectShapes(nakagoShapes))
                {

                    if (centralNakagoShape == 1)
                    {
                        DrawLine_Rec(canvas, T, item,
                                     (xL + (r * 2) + 320) + offset4.X + offsetNakagoX, (yT + r + 3000) + offset4.Y + offsetNakagoY,
                                     (xL + (r * 2) + 320) + offset4.X + offsetNakagoX, (yT + r + L1 - 50 + 3000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + 320 + offset4.X + offsetNakagoX, cy + 3000 + offset4.Y + offsetNakagoY, r,
                                    startDeg: -180, endDeg: 0,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 30 + offset4.X + offsetNakagoX, (r + yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                                     mid - 30 + offset4.X + offsetNakagoX, (r + yChainLocal + 8300 + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + 320 + offset4.X + offsetNakagoX, cy + 4000 + r + offset4.Y + offsetNakagoY, r,
                                    startDeg: 0, endDeg: 180,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     (xL + (r * 2) + 320) + offset4.X + offsetNakagoX, (yT + r + 4000) + offset4.Y + offsetNakagoY,
                                     (xL + (r * 2) + 320) + offset4.X + offsetNakagoX, (yT + r + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                    }
                    else if (centralNakagoShape == 2)
                    {
                        DrawLine_Rec(canvas, T, item,
                                     (xL + r + 320) + offset4.X + offsetNakagoX, (yT + 3000) + offset4.Y + offsetNakagoY,
                                     (xL + r + 450) + offset4.X + offsetNakagoX, (yT + 3000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + 320 + offset4.X + offsetNakagoX, cy + 3000 + offset4.Y + offsetNakagoY, r,
                                    startDeg: -180, endDeg: -90,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 30 + offset4.X + offsetNakagoX, (r + yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                                     mid - 30 + offset4.X + offsetNakagoX, (r + yChainLocal + 8300 + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + 320 + offset4.X + offsetNakagoX, cy + 4000 + r + offset4.Y + offsetNakagoY, r,
                                    startDeg: 0, endDeg: 180,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        // Sửa lỗi thiếu offset4.X ở x2 + thêm offsetNakagoX
                        DrawLine_Rec(canvas, T, item,
                                     (xL + (r * 2) + 320) + offset4.X + offsetNakagoX, (yT + r + 4000) + offset4.Y + offsetNakagoY,
                                     (xL + (r * 2) + 320) + offset4.X + offsetNakagoX, (yT + r + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                    }
                    else if (centralNakagoShape == 3)
                    {
                        DrawLine_Rec(canvas, T, item,
                                     (xL + r + 320) + offset4.X + offsetNakagoX, (yT + 3000) + offset4.Y + offsetNakagoY,
                                     (xL + r + 450) + offset4.X + offsetNakagoX, (yT + 3000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + 320 + offset4.X + offsetNakagoX, cy + 3000 + offset4.Y + offsetNakagoY, r,
                                    startDeg: -180, endDeg: -90,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 30 + offset4.X + offsetNakagoX, (r + yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                                     mid - 30 + offset4.X + offsetNakagoX, (r + yChainLocal + 8300 + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + 320 + offset4.X + offsetNakagoX, cy + 4000 + r + offset4.Y + offsetNakagoY, r,
                                    startDeg: 90, endDeg: 180,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     (xL + r + 320) + offset4.X + offsetNakagoX, (r * 2 + yT + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                                     (xL + r + 450) + offset4.X + offsetNakagoX, (r * 2 + yT + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                    }
                    else if (centralNakagoShape == 4)
                    {
                        double startDegLD = 180;
                        double endDegLD = 45;

                        double srLD = startDegLD * Math.PI / 180.0;
                        double erLD = endDegLD * Math.PI / 180.0;

                        double exLD = cx2 + r * Math.Cos(erLD);
                        double eyLD = cy + r * Math.Sin(erLD);

                        double tsxLD = -Math.Sin(srLD), tsyLD = Math.Cos(srLD);
                        double texLD = -Math.Sin(erLD), teyLD = Math.Cos(erLD);

                        DrawLine_Rec(canvas, T, item,
                                     exLD + 320 + offset4.X + offsetNakagoX, eyLD + 2980 + offset4.Y + offsetNakagoY - r,
                                     exLD + 340 + offset4.X + offsetNakagoX + r, eyLD + 2980 + L1 * teyLD + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + 320 + offset4.X + offsetNakagoX, cy + 3000 + offset4.Y + offsetNakagoY, r,
                                    startDeg: -180, endDeg: -45,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 30 + offset4.X + offsetNakagoX, (r + yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                                     mid - 30 + offset4.X + offsetNakagoX, (r + yChainLocal + 8300 + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + 320 + offset4.X + offsetNakagoX, cy + 4000 + r + offset4.Y + offsetNakagoY, r,
                                    startDeg: 45, endDeg: 180,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     exLD + 340 + r + offset4.X + offsetNakagoX, eyLD + 3980 + offset4.Y + offsetNakagoY,
                                     exLD + r + 340 + L1 * texLD + offset4.X + offsetNakagoX, eyLD + 3980 + L1 * teyLD + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                    }
                    else if (centralNakagoShape == 5)
                    {
                        double startDegLD = 180;
                        double endDegLD = 45;

                        double srLD = startDegLD * Math.PI / 180.0;
                        double erLD = endDegLD * Math.PI / 180.0;

                        double exLD = cx2 + r * Math.Cos(erLD);
                        double eyLD = cy + r * Math.Sin(erLD);

                        double tsxLD = -Math.Sin(srLD), tsyLD = Math.Cos(srLD);
                        double texLD = -Math.Sin(erLD), teyLD = Math.Cos(erLD);

                        DrawLine_Rec(canvas, T, item,
                                     (xL + r + 320) + offset4.X + offsetNakagoX, (yT + 3000) + offset4.Y + offsetNakagoY,
                                     (xL + r + 450) + offset4.X + offsetNakagoX, (yT + 3000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + 320 + offset4.X + offsetNakagoX, cy + 3000 + offset4.Y + offsetNakagoY, r,
                                    startDeg: -180, endDeg: -90,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     mid - 30 + offset4.X + offsetNakagoX, (r + yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                                     mid - 30 + offset4.X + offsetNakagoX, (r + yChainLocal + 8300 + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item, cx2 + 320 + offset4.X + offsetNakagoX, cy + 4000 + r + offset4.Y + offsetNakagoY, r,
                                    startDeg: 45, endDeg: 180,
                                    stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                                    dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                                     exLD + 340 + r + offset4.X + offsetNakagoX, eyLD + 3980 + offset4.Y + offsetNakagoY,
                                     exLD + r + 340 + L1 * texLD + offset4.X + offsetNakagoX, eyLD + 3980 + L1 * teyLD + offset4.Y + offsetNakagoY,
                                     Brushes.DeepSkyBlue, 1.5, null, "MARK");
                    }
                    else if (centralNakagoShape == 6)
                    {
                        double offsetX6 = -150;
                        double offsetY6 = 0;

                        double X(double x) => x + offsetX6;
                        double Y(double y) => y + offsetY6;

                        DrawArc_Rec(canvas, T, item,
                            X(cx2 + 320) + offset4.X + offsetNakagoX, Y(cy + 3000) + offset4.Y + offsetNakagoY, r,
                            startDeg: -180, endDeg: -90,
                            stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                            dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                            X(mid - 30) + offset4.X + offsetNakagoX, Y(r + yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                            X(mid - 30) + offset4.X + offsetNakagoX, Y(r + yChainLocal + 8300 + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                            Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                            X(mid + 350) + offset4.X + offsetNakagoX, Y(r + yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                            X(mid + 350) + offset4.X + offsetNakagoX, Y(r + yChainLocal + 8300 + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                            Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item,
                            X(cx2 + 320) + offset4.X + offsetNakagoX, Y(cy + 4000 + r) + offset4.Y + offsetNakagoY, r,
                            startDeg: 90, endDeg: 180,
                            stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                            dash: null, "MARK");

                        DrawArc_Rec(canvas, T, item,
                            X(cx1) + offset4.X + offsetNakagoX, Y(cy + 3000) + offset4.Y + offsetNakagoY, r,
                            startDeg: -135, endDeg: 45,
                            stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                            dash: null, "MARK");

                        DrawArc_Rec(canvas, T, item,
                            X(cx2 + 600) + offset4.X + offsetNakagoX, Y(cy + 4050) + offset4.Y + offsetNakagoY, r,
                            startDeg: 0, endDeg: 90,
                            stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                            dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                            X(sxR) + offset4.X + offsetNakagoX, Y(sy + 3000) + offset4.Y + offsetNakagoY,
                            X(sxR - L1 * tsxR) + offset4.X + offsetNakagoX, Y(sy + 3000 - L1 * tsyR) + offset4.Y + offsetNakagoY,
                            Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                            X(exR) + offset4.X + offsetNakagoX, Y(ey + 3000) + offset4.Y + offsetNakagoY,
                            X(exR + L1 * tex) + offset4.X + offsetNakagoX, Y(ey + 3000 + L1 * tey) + offset4.Y + offsetNakagoY,
                            Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                            X(mid + 20) + offset4.X + offsetNakagoX, Y(yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                            X(mid + 350 - r) + offset4.X + offsetNakagoX, Y(yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                            Brushes.DeepSkyBlue, 1.2, null, "CHAIN");

                        DrawLine_Rec(canvas, T, item,
                            X(mid + 20) + offset4.X + offsetNakagoX, Y(yChainLocal + 8300 + 4150) + offset4.Y + offsetNakagoY,
                            X(mid + 350 - r) + offset4.X + offsetNakagoX, Y(yChainLocal + 8300 + 4150) + offset4.Y + offsetNakagoY,
                            Brushes.DeepSkyBlue, 1.2, null, "CHAIN");
                    }
                    else if (centralNakagoShape == 7)
                    {
                        double offsetX7 = -150;
                        double offsetY7 = 0;

                        double X(double x) => x + offsetX7;
                        double Y(double y) => y + offsetY7;

                        DrawArc_Rec(canvas, T, item,
                            X(cx2 + 320) + offset4.X + offsetNakagoX, Y(cy + 3000) + offset4.Y + offsetNakagoY, r,
                            startDeg: -180, endDeg: -90,
                            stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                            dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                            X(mid - 30) + offset4.X + offsetNakagoX, Y(r + yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                            X(mid - 30) + offset4.X + offsetNakagoX, Y(r + yChainLocal + 8300 + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                            Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                            X(mid + 350) + offset4.X + offsetNakagoX, Y(r + yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                            X(mid + 350) + offset4.X + offsetNakagoX, Y(r + yChainLocal + 8300 + L1 - 50 + 4000) + offset4.Y + offsetNakagoY,
                            Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawArc_Rec(canvas, T, item,
                            X(cx2 + 320) + offset4.X + offsetNakagoX, Y(cy + 4000 + r) + offset4.Y + offsetNakagoY, r,
                            startDeg: 90, endDeg: 180,
                            stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                            dash: null, "MARK");

                        DrawArc_Rec(canvas, T, item,
                            X(cx1) + offset4.X + offsetNakagoX, Y(cy + 3000) + offset4.Y + offsetNakagoY, r,
                            startDeg: -135, endDeg: 0,
                            stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                            dash: null, "MARK");

                        DrawArc_Rec(canvas, T, item,
                            X(cx2 + 600) + offset4.X + offsetNakagoX, Y(cy + 4050) + offset4.Y + offsetNakagoY, r,
                            startDeg: 0, endDeg: 90,
                            stroke: Brushes.DeepSkyBlue, thickness: 1.5,
                            dash: null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                            X(sxR) + offset4.X + offsetNakagoX, Y(sy + 3000) + offset4.Y + offsetNakagoY,
                            X(sxR - L1 * tsxR) + offset4.X + offsetNakagoX, Y(sy + 3000 - L1 * tsyR) + offset4.Y + offsetNakagoY,
                            Brushes.DeepSkyBlue, 1.5, null, "MARK");

                        DrawLine_Rec(canvas, T, item,
                            X(mid + 20) + offset4.X + offsetNakagoX, Y(yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                            X(mid + 350 - r) + offset4.X + offsetNakagoX, Y(yChainLocal + 8300 + 3000) + offset4.Y + offsetNakagoY,
                            Brushes.DeepSkyBlue, 1.2, null, "CHAIN");

                        DrawLine_Rec(canvas, T, item,
                            X(mid + 20) + offset4.X + offsetNakagoX, Y(yChainLocal + 8300 + 4150) + offset4.Y + offsetNakagoY,
                            X(mid + 350 - r) + offset4.X + offsetNakagoX, Y(yChainLocal + 8300 + 4150) + offset4.Y + offsetNakagoY,
                            Brushes.DeepSkyBlue, 1.2, null, "CHAIN");
                    }
                    else if (centralNakagoShape == 8)
                    {
                        // TODO: Thêm logic riêng cho shape 8 (nếu có), nhớ cộng offset4 + offsetNakago như các case trên
                    }
                }
                MakeCentralNakagoShapeEditable(canvas, T, item,
                                                selF, G0,
                                                i,
                                                centralNakagoShape,
                                                nakagoShapes,
                                                /*hit center (world mm)*/ mid + offset4.X + offsetNakagoX,
                                                /*hit center (world mm)*/ (yT + 3555) + offset4.Y + offsetNakagoY,
                                                /*hit size (mm)*/ 600, 1300
                                            );
                //============= hết 4 chỗ =============//

                // ==== Khối minh họa STP ==== (giữ nguyên)
            }

            // === 3b-B) (chuẩn hóa dữ liệu, KHÔNG vẽ) ===
            {
                double yStartTmp = yChainBot + 2850;
                double barSpacingTmp = 500.0;
                double tol = 0.01;
                double minGapTmp = 50;

                int nRowsGlobal = 0;
                for (int i = 0; i < spanCount; i++)
                {
                    int ni = Math.Max(nEnd1Arr[i], Math.Max(nMidArr[i], nEnd2Arr[i]));
                    if (ni > nRowsGlobal) nRowsGlobal = ni;
                }

                for (int barIdx = 0; barIdx < nRowsGlobal; barIdx++)
                {
                    double yLine = yStartTmp + barIdx * barSpacingTmp;

                    var segs = new List<(double x1, double x2)>(spanCount * 3);
                    var rowCuts = new List<double>();

                    for (int i = 0; i < spanCount; i++)
                    {
                        double sxL = spanLeftArr[i];
                        double sxR = spanRightArr[i];
                        double qL = qLArr[i];
                        double qR = qRArr[i];
                        double mid = midArr[i];

                        bool hasE1 = barIdx < nEnd1Arr[i];
                        bool hasMid = barIdx < nMidArr[i];
                        bool hasE2 = barIdx < nEnd2Arr[i];

                        double qL_use = qL;
                        double qR_use = qR;

                        if (i == 0 && hasE1)
                        {
                            double extL = Math.Max(0, gTanbu) * Math.Max(0, diaE1Arr[i]);
                            if (extL > 0)
                            {
                                qL_use = Math.Min(mid - minGapTmp, qL_use + extL);
                                qL_use = Math.Max(qL_use, sxL + minGapTmp);
                            }
                        }
                        if (i == spanCount - 1 && hasE2)
                        {
                            double extR = Math.Max(0, gTanbu) * Math.Max(0, diaE2Arr[i]);
                            if (extR > 0)
                            {
                                qR_use = Math.Max(mid + minGapTmp, qR_use - extR);
                                qR_use = Math.Min(qR_use, sxR - minGapTmp);
                            }
                        }

                        bool cutAtMid = hasE1 && hasMid && hasE2;

                        if (!cutAtMid)
                        {
                            if (hasE1 && i > 0)
                            {
                                double extInnerL = Math.Max(0, nTanbu) * Math.Max(0, diaE1Arr[i]);
                                if (extInnerL > 0)
                                {
                                    qL_use = Math.Max(sxL + minGapTmp, qL_use + extInnerL);
                                    qL_use = Math.Min(qL_use, mid - minGapTmp);
                                }
                            }
                            if (hasE2 && i < spanCount - 1)
                            {
                                double extInnerR = Math.Max(0, nTanbu) * Math.Max(0, diaE2Arr[i]);
                                if (extInnerR > 0)
                                {
                                    qR_use = Math.Min(sxR - minGapTmp, qR_use - extInnerR);
                                    qR_use = Math.Max(qR_use, mid + minGapTmp);
                                }
                            }
                        }

                        if (!cutAtMid && hasMid && !hasE1)
                        {
                            double extMidL = Math.Max(0, gChubu) * Math.Max(0, diaMidArr[i]);
                            if (extMidL > 0)
                            {
                                qL_use = Math.Max(sxL + minGapTmp, Math.Min(mid - minGapTmp, qL_use - extMidL));
                            }
                        }

                        if (!cutAtMid && hasMid && !hasE2)
                        {
                            double extMidR = Math.Max(0, nChubu) * Math.Max(0, diaMidArr[i]);
                            if (extMidR > 0)
                            {
                                qR_use = Math.Min(sxR - minGapTmp, Math.Max(mid + minGapTmp, qR_use + extMidR));
                            }
                        }

                        if (qL_use >= qR_use - 2 * minGapTmp)
                        {
                            double c = (qL_use + qR_use) / 2.0;
                            qL_use = Math.Min(c - minGapTmp, mid - minGapTmp);
                            qR_use = Math.Max(c + minGapTmp, mid + minGapTmp);
                        }

                        if (cutAtMid)
                        {
                            segs.Add((sxL, mid));
                            segs.Add((mid, sxR));
                            rowCuts.Add(mid);
                        }
                        else
                        {
                            if (hasE1 && qL_use > sxL) segs.Add((sxL, qL_use));
                            if (hasMid && qR_use > qL_use) segs.Add((qL_use, qR_use));
                            if (hasE2 && sxR > qR_use) segs.Add((qR_use, sxR));
                        }
                    }

                    if (segs.Count == 0) continue;

                    if (rowCuts.Count > 0)
                    {
                        rowCuts.Sort();
                        var split = new List<(double x1, double x2)>(segs.Count * 2);
                        foreach (var seg in segs)
                        {
                            double a = seg.x1, b = seg.x2;
                            if (b <= a) continue;
                            double cur = a;
                            foreach (double cp in rowCuts)
                            {
                                if (cp > b - tol || cp < cur + tol) continue;
                                if (cur < cp) split.Add((cur, cp));
                                cur = cp;
                            }
                            if (cur < b) split.Add((cur, b));
                        }
                        segs = split;
                    }

                    segs.Sort((a, b) => a.x1.CompareTo(b.x1));
                    var merged = new List<(double x1, double x2)>();
                    (double x1, double x2) curSeg = segs[0];

                    bool IsHardCut(double x) => rowCuts.Any(cp => Math.Abs(cp - x) <= 0.01);

                    for (int s = 1; s < segs.Count; s++)
                    {
                        var nxt = segs[s];
                        if (nxt.x1 <= curSeg.x2 + 0.01)
                        {
                            double joinX = Math.Max(curSeg.x2, nxt.x1);
                            if (IsHardCut(joinX))
                            {
                                merged.Add(curSeg);
                                curSeg = nxt;
                            }
                            else
                            {
                                curSeg.x2 = Math.Max(curSeg.x2, nxt.x2);
                            }
                        }
                        else
                        {
                            merged.Add(curSeg);
                            curSeg = nxt;
                        }
                    }
                    merged.Add(curSeg);
                    // không vẽ gì ở khối chuẩn hóa
                }
            }

            // ====== HẰNG SỐ CỐ ĐỊNH CHO HÌNH CHỮ NHẬT TEI ======
            const double TeiRectW = 90.0;
            const double TeiRectH = 160.0;

            // === 3b-F) VẼ 上筋 ===

            //var (tonariOo, tonariKo) = GetTonariValues();
            bool shouldOffsetTonariDots = sugiteOption2;
            //bool isDairyouOnKoiryouOff = false;

            var isDairyouOnKoiryouOffArr = new bool[spanCount];

            if (shouldOffsetTonariDots && names.Count >= 2)
            {
                //var (G0, _, _, _) = GetBeamValuesByPosition(selF, selY, names[0], names[1]);
                //string gSym = string.IsNullOrWhiteSpace(G0) ? "G0" : G0;
                //isDairyouOnKoiryouOff = GetDairyouOnKoiryouOff(selF, gSym);

                for (int i = 0; i < spanCount && i + 1 < names.Count; i++)
                {
                    var (G0, _, _, _) = GetBeamValuesByPosition(selF, selY, names[i], names[i + 1]);
                    string gSym = string.IsNullOrWhiteSpace(G0) ? "G0" : G0;
                    isDairyouOnKoiryouOffArr[i] = GetDairyouOnKoiryouOff(selF, gSym);
                }
            }

            //double GetTonariDotOffset(int rowIndex)
            double GetTonariDotOffset(int rowIndex, double x)
            {
                if (!shouldOffsetTonariDots) return 0.0;

                bool isEvenRow = ((rowIndex + 1) % 2 == 0);
                if (!isEvenRow) return 0.0;

                //return isDairyouOnKoiryouOff ? tonariOo : tonariKo;

                int spanIdx = -1;
                for (int i = 0; i < spanCount; i++)
                {
                    double spanLeftEdge = spanLeftArr[i];
                    double spanRightEdge = spanRightArr[i];
                    if (x >= spanLeftEdge - 0.5 && x <= spanRightEdge + 0.5)
                    {
                        spanIdx = i;
                        break;
                    }
                }

                bool daiOnKoiOff = spanIdx >= 0 && spanIdx < isDairyouOnKoiryouOffArr.Length
                    ? isDairyouOnKoiryouOffArr[spanIdx]
                    : false;

                return daiOnKoiOff ? tonariOo : tonariKo;
            }

            double GetShitaHardCutAnchorX(int spanIndex)
            {
                if (spanIndex < 0 || spanIndex >= spanCount)
                    return 0.0;

                // Keep the existing sole/tonari spacing between lower-layer cut points,
                // but shift the whole cluster so it is referenced from the green qR line.
                return qRArr[spanIndex];
            }

            double GetUwaHardCutAnchorX(int spanIndex)
            {
                if (spanIndex < 0 || spanIndex >= spanCount)
                    return 0.0;

                // The upper-bar anchor follows the center of the blue box drawn at mid.
                double boxLeft = midArr[spanIndex] - 500.0;
                double boxRight = midArr[spanIndex] + 500.0;
                return 0.5 * (boxLeft + boxRight);
            }

            var shitaSpanBreaks = new List<double>(Math.Max(0, spanCount - 1));
            for (int i = 1; i < spanCount; i++)
                shitaSpanBreaks.Add(spanLeftArr[i]);

            const double visibleLayerGap = 400.0;

            int GetMaxLayerRowCount(int[] end1ArrLocal, int[] midArrLocal, int[] end2ArrLocal)
            {
                int maxRows = 0;
                for (int i = 0; i < spanCount; i++)
                    maxRows = Math.Max(maxRows, Math.Max(end1ArrLocal[i], Math.Max(midArrLocal[i], end2ArrLocal[i])));
                return maxRows;
            }

            TextBlock DrawRebarLayerHeader(double yBase, string label, double nigeValue)
            {
                DrawLine_Rec(canvas, T, item,
                    leftEdge - 1500, yBase,
                    rightEdge + 1500, yBase,
                    Brushes.Aqua, 1.2, null, "CHAIN");

                var labelTb = DrawText_Rec(canvas, T, item, label,
                    leftEdge - 1300, yBase + LabelDy +100, dimFont, Brushes.Red,
                    HAnchor.Center, VAnchor.Bottom, 200, "TEXT");

                if (nige1 == true)
                {
                    DrawText_Rec(canvas, T, item, $"ニゲ {nigeValue:0}", leftEdge - 1100, yBase + ValueDy, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    DrawText_Rec(canvas, T, item, $"ニゲ {nigeValue:0}", rightEdge + 1100, yBase + ValueDy, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                }

                return labelTb;
            }

            double nextRebarLayerBaseY = yChainBot + 2600;

            double lastYOfUwagane = 0.0;
            {
                double yStart = yChainBot + 2850 + 200;
                double barSpacing = 500.0;
                double minGap = 50.0;
                double tol = 0.01;

                var (nigeUwaLocal, _, _, _, _, _) = GetNigeValues();
                var (ankaUwa, _, _, _) = GetAnkaNagaValues();
                var (teiUwaNow, _, _, _) = GetTeiFlags();

                double rectW = TeiRectW;
                double rectH = TeiRectH;

                double dotR = 30.0;

                int nRowsGlobal = GetMaxLayerRowCount(nEnd1Arr, nMidArr, nEnd2Arr);
                bool hasVisibleUwagane = false;

                // Vị trí ANKA biên theo hàng (cố định theo span biên)
                double leftAnkaX_Global = (spanCount > 0) ? spanLeftArr[0] + Math.Max(0, nigeUwaLocal) : double.NaN;
                double rightAnkaX_Global = (spanCount > 0) ? spanRightArr[spanCount - 1] - Math.Max(0, nigeUwaLocal) : double.NaN;
                bool hasLeftAnka_Global = (!teiUwaNow) && (ankaUwa > 0) && (spanCount > 0);
                bool hasRightAnka_Global = (!teiUwaNow) && (ankaUwa > 0) && (spanCount > 0);

                for (int kRow = 0; kRow < nRowsGlobal; kRow++)
                {
                    //double tonariOffset = GetTonariDotOffset(kRow);

                    double y = yStart + kRow * barSpacing;

                    var rowSegs = new List<(double x1, double x2)>();
                    var rowCuts = new List<double>();

                    // --- 端部1 ---
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasE1 = kRow < nEnd1Arr[i];
                        if (!hasE1) continue;

                        bool joinLeft = (i > 0) && (kRow < nEnd2Arr[i - 1]);
                        double phi = Math.Max(0, diaE1Arr[i]);
                        double x1 = spanLeftArr[i];
                        double x2 = qLArr[i];

                        if (i == 0)
                        {
                            x2 += Math.Max(0, gTanbu) * phi;
                            x1 += Math.Max(0, nigeUwaLocal);

                            // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị
                        }
                        else
                        {
                            double ext = Math.Max(0, nTanbu) * phi;
                            if (!joinLeft) x1 -= ext;
                            x2 += ext;
                        }

                        x2 = Math.Min(x2, midArr[i] - minGap);
                        if (x2 > x1) rowSegs.Add((x1, x2));
                    }

                    // --- 中央 (上筋: cắt giữa Mid nếu hasTriple) ---
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasMid = kRow < nMidArr[i];
                        if (!hasMid) continue;

                        bool hasE1 = kRow < nEnd1Arr[i];
                        bool hasE2 = kRow < nEnd2Arr[i];
                        bool hasTriple = hasE1 && hasMid && hasE2;

                        double phi = Math.Max(0, diaMidArr[i]);
                        double x1 = qLArr[i] - Math.Max(0, gChubu) * phi;
                        double x2 = qRArr[i] + Math.Max(0, nChubu) * phi;

                        x1 = Math.Max(x1, spanLeftArr[i] + 50.0);
                        x2 = Math.Min(x2, spanRightArr[i] - 50.0);
                        x2 = Math.Max(x2, midArr[i] + 50.0);

                        if (x2 <= x1) continue;

                        if (hasTriple)
                        {
                            double cm = GetUwaHardCutAnchorX(i);
                            cm = Math.Max(cm, x1 + minGap);
                            cm = Math.Min(cm, x2 - minGap);
                            rowSegs.Add((x1, cm));
                            rowSegs.Add((cm, x2));
                            rowCuts.Add(cm);

                            if (ShowPreRoundCutMarks)
                                DrawDotMm_Rec(canvas, T, item, cm, y, rMm: 30.0, layer: "MARK", fill: Brushes.Black);
                        }
                        else
                        {
                            rowSegs.Add((x1, x2));
                        }
                    }

                    // --- 端部2 ---
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasE2 = kRow < nEnd2Arr[i];
                        if (!hasE2) continue;

                        bool joinRight = (i < spanCount - 1) && (kRow < nEnd1Arr[i + 1]);
                        double phi = Math.Max(0, diaE2Arr[i]);
                        double x1 = qRArr[i];
                        double x2 = spanRightArr[i];

                        if (i == spanCount - 1)
                        {
                            x1 -= Math.Max(0, gTanbu) * phi;
                            x2 -= Math.Max(0, nigeUwaLocal);

                            // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị
                        }
                        else
                        {
                            double ext = Math.Max(0, nTanbu) * phi;
                            x1 -= ext;
                            if (!joinRight) x2 += ext;
                        }

                        x1 = Math.Max(x1, midArr[i] + 50.0);
                        if (x2 > x1) rowSegs.Add((x1, x2));
                    }

                    if (rowSegs.Count == 0) continue;
                    rowSegs.Sort((a, b) => a.x1.CompareTo(b.x1));

                    var merged = new List<(double x1, double x2)>();
                    (double x1m, double x2m) cur = rowSegs[0];

                    bool IsHardCut(double x) => rowCuts.Any(cp => Math.Abs(cp - x) <= tol);

                    for (int s = 1; s < rowSegs.Count; s++)
                    {
                        var nx = rowSegs[s];
                        if (nx.x1 <= cur.x2m + tol)
                        {
                            double joinX = Math.Max(cur.x2m, nx.x1);
                            if (IsHardCut(joinX))
                            {
                                merged.Add(cur);
                                cur = nx;
                            }
                            else
                            {
                                cur.x2m = Math.Max(cur.x2m, nx.x2);
                            }
                        }
                        else
                        {
                            merged.Add(cur);
                            cur = nx;
                        }
                    }
                    merged.Add(cur);

                    // === NEW: Làm tròn theo bội 500 cho các chuỗi liền nhau trước khi vẽ ===
                    RoundContiguousChainsInPlaceBySpan(merged, 500.0, shitaSpanBreaks, 1.0);

                    // ① DỊCH BIÊN CẮT THẬT
                    ApplyTonariShiftToMergedCuts(
                        merged,
                        rowCuts,
                        spanLeftArr,
                        spanRightArr,
                        spanCount,
                        x => GetTonariDotOffset(kRow, x),
                        (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + GetTonariDotOffset(kRow, cut)
                    );

                    var visibleSegs = ClampVisibleOrangeSegsToBounds(
                        GetVisibleOrangeSegs(item, kRow, y, merged),
                        leftAnkaX_Global,
                        rightAnkaX_Global);
                    if (visibleSegs.Count == 0) continue;

                    hasVisibleUwagane = true;
                    lastYOfUwagane = y;

                    // ② VẼ DOT KHÔNG OFFSET NỮA
                    DrawAdjustedHardCutDots(
                        canvas, T, item, kRow,
                        visibleSegs.Select(seg => (seg.X1, seg.X2)).ToList(), rowCuts,
                        spanLeftArr, spanRightArr, spanCount,
                        y, dotR,
                        Brushes.Black,
                        0,
                        null,
                        (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + GetTonariDotOffset(kRow, cut)
                    );

                    foreach (var seg in visibleSegs)
                    {
                        var segKey = new OrangeSegKey(kRow, seg.BaseX1, seg.BaseX2, y);
                        ResolveAnkaDefaults(
                            item,
                            segKey,
                            seg.X1,
                            seg.X2,
                            leftAnkaX_Global,
                            rightAnkaX_Global,
                            hasLeftAnka_Global,
                            hasRightAnka_Global,
                            -ankaUwa,
                            -ankaUwa,
                            out bool hitLeftBoundary,
                            out bool hitRightBoundary,
                            out double defaultLeftSigned,
                            out double defaultRightSigned);
                        double wxTop = 0.5 * (seg.X1 + seg.X2);
                        double wyTop = y - OrangeDimPrimaryTextGapMm;
                        var topKey = new OrangeDimTextKey(kRow, true, wxTop, wyTop);
                        double leftSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Left, defaultLeftSigned);
                        double rightSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Right, defaultRightSigned);

                        if (teiUwaNow)
                        {
                            if (hitLeftBoundary)
                            {
                                DrawRect_Rec(canvas, T, item, seg.X1, y - rectH / 2.0, rectW, rectH,
                                             stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                            }
                            if (hitRightBoundary)
                            {
                                DrawRect_Rec(canvas, T, item, seg.X2, y - rectH / 2.0, rectW, rectH,
                                             stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                            }
                        }
                        else
                        {
                            if (Math.Abs(leftSigned) > 0.0001)
                            {
                                DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X1, y - leftSigned, Brushes.Black, 1.2, null, "MARK");
                                if (anka1 == true)
                                {
                                    DrawText_Rec(canvas, T, item, $"{Math.Abs(leftSigned):0}", seg.X1 - 650, y - leftSigned / 2.0 + 100, dimFont,
                                             Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                }
                            }
                            if (Math.Abs(rightSigned) > 0.0001)
                            {
                                DrawLine_Rec(canvas, T, item, seg.X2, y, seg.X2, y - rightSigned, Brushes.Black, 1.2, null, "MARK");
                                if (anka1 == true)
                                {
                                    DrawText_Rec(canvas, T, item, $"{Math.Abs(rightSigned):0}", seg.X2 + 250, y - rightSigned / 2.0 + 100, dimFont,
                                        Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                }
                            }
                        }

                        DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X2, y, Brushes.Black, 1.2, null, "MARK");

                        // TEXT CAM: dùng chiều dài đã làm tròn (hoặc đoạn cuối không làm tròn)
                        DimOrangeSegmentWithAnkaLabels(
                            canvas, T, item,
                            seg.X1, seg.X2,
                            y, /*dyTop*/ -OrangeDimPrimaryTextGapMm, /*dyBottom*/ OrangeDimSecondaryTextGapMm,
                            diaMidArr, spanLeftArr, spanRightArr, spanCount,
                            defaultLeftSigned, defaultRightSigned,
                            leftSigned, rightSigned,
                            /*textAboveLine*/ true, seg.BaseX1, seg.BaseX2,
                                suppressAnkaInTopLength: seg.IsEqualCutChild,
                            forceShowForCutChild: seg.IsEqualCutChild,
                                rowIndex: kRow
                        );
                    }
                }

                if (hasVisibleUwagane)
                {
                    DrawRebarLayerHeader(nextRebarLayerBaseY, "\u4e0a\u7b4b", nigeUwa);
                    nextRebarLayerBaseY = lastYOfUwagane + visibleLayerGap;
                }

            }

            // === 3c) Chuỗi bổ sung do user chèn ===
            foreach (var y in ExtraChainsFor(item))
            {
                if (y >= 0 && y <= yH)
                {
                    AddLineInteractive(canvas, T, pos.First(), y, pos.Last(), y,
                                        stroke: Brushes.Black, thickness: 1.2,
                                        role: LineRole.Chain, owner: item);
                }
            }

            ///////////// 5 chổ //////////////
            var offset5 = OffsetLegendColumn;
            DrawLine_Rec(canvas, T, item,
                        leftEdge - 1500 + offset5.X, yChainBot + 6600 + offset5.Y,
                        rightEdge + 1500 + offset5.X, yChainBot + 6600 + offset5.Y,
                        Brushes.Aqua, 1.2, null, "CHAIN");
            DrawText_Rec(canvas, T, item, "腹筋",
                         leftEdge - 1400 + offset5.X, yChainBot + 6850 + offset5.Y +100,
                         dimFont, Brushes.Red, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
            DrawText_Rec(canvas, T, item, "STP",
                         leftEdge - 1400 + offset5.X, yChainBot + 7850 + offset5.Y,
                         dimFont, Brushes.Red, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
            DrawText_Rec(canvas, T, item, "腹筋幅止め",
                         leftEdge - 1400 + offset5.X, yChainBot + 9850 + offset5.Y,
                         dimFont, Brushes.Red, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
            DrawText_Rec(canvas, T, item, "中子",
                         leftEdge - 1400 + offset5.X, yChainBot + 10850 + offset5.Y,
                         dimFont, Brushes.Red, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
            /////////// hết 5 chổ ////////////


            // =========================
            // ======  ⬇ 上宙1 ⬇  ======
            // =========================
            (int cnt, double phi) ParseCountPhiChu1(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return (0, 0);
                var txt = s.Trim().Replace("Φ", "φ").Replace("‐", "-").Replace("–", "-").Replace("—", "-");
                int c = 0; double p = 0;
                var mC = System.Text.RegularExpressions.Regex.Match(txt, @"(?<![\d\.])(\d+)");
                if (mC.Success) int.TryParse(mC.Groups[1].Value, out c);
                var mP = System.Text.RegularExpressions.Regex.Match(txt, @"[Ddφ]\s*(\d+(\.\d+)?)");
                if (mP.Success) double.TryParse(mP.Groups[1].Value, out p);
                return (Math.Max(0, c), Math.Max(0, p));
            }

            var nEnd1Chu1Arr = new int[spanCount];
            var nMidChu1Arr = new int[spanCount];
            var nEnd2Chu1Arr = new int[spanCount];
            var diaE1Chu1Arr = new double[spanCount];
            var diaMidChu1Arr = new double[spanCount];
            var diaE2Chu1Arr = new double[spanCount];

            for (int i = 0; i < spanCount && i < spans.Count; i++)
            {
                string leftNameS = names[i];
                string rightNameS = names[i + 1];
                var (G0, _, _, _) = GetBeamValuesByPosition(selF, selY, leftNameS, rightNameS);
                var zcfg = GetRebarConfigForSpan(selF, string.IsNullOrWhiteSpace(G0) ? "G0" : G0);

                var (cE1, pE1) = ParseCountPhiChu1(zcfg?.端部1上宙1);
                var (cMid, pMid) = ParseCountPhiChu1(zcfg?.中央上宙1);
                var (cE2, pE2) = ParseCountPhiChu1(zcfg?.端部2上宙1);

                nEnd1Chu1Arr[i] = cE1;
                nMidChu1Arr[i] = cMid;
                nEnd2Chu1Arr[i] = cE2;

                diaE1Chu1Arr[i] = (pE1 > 0 ? pE1 : diaE1Arr[i]);
                diaMidChu1Arr[i] = (pMid > 0 ? pMid : diaMidArr[i]);
                diaE2Chu1Arr[i] = (pE2 > 0 ? pE2 : diaE2Arr[i]);
            }

            // Vẽ đường Aqua "上宙1"
            double yChu1Base = nextRebarLayerBaseY;
            // 上宙1
            double lastYOfChu1 = 0.0;
            {
                double yStartChu1 = yChu1Base + 500;
                double barSpacing = 500.0;
                double minGap = 50.0;
                double tol = 0.01;

                var (_, nigeUwaChu1Local, _, _, _, _) = GetNigeValues();
                var (_, ankaUwaChu, _, _) = GetAnkaNagaValues();
                var (_, teiUwaChuNow, _, _) = GetTeiFlags();

                double rectW = TeiRectW;
                double rectH = TeiRectH;
                double dotR = 30.0;

                // Vị trí ANKA biên cho 上宙1
                double leftAnkaX_Global = (spanCount > 0) ? spanLeftArr[0] + Math.Max(0, nigeUwaChu1Local) : double.NaN;
                double rightAnkaX_Global = (spanCount > 0) ? spanRightArr[spanCount - 1] - Math.Max(0, nigeUwaChu1Local) : double.NaN;
                bool hasLeftAnka_Global = (!teiUwaChuNow) && (ankaUwaChu > 0) && (spanCount > 0);
                bool hasRightAnka_Global = (!teiUwaChuNow) && (ankaUwaChu > 0) && (spanCount > 0);

                int nRowsGlobalChu1 = GetMaxLayerRowCount(nEnd1Chu1Arr, nMidChu1Arr, nEnd2Chu1Arr);
                bool hasVisibleChu1 = false;

                for (int kRow = 0; kRow < nRowsGlobalChu1; kRow++)
                {
                    //double tonariOffset = GetTonariDotOffset(kRow);

                    double y = yStartChu1 + kRow * barSpacing;

                    var rowSegs = new List<(double x1, double x2)>();
                    var rowCuts = new List<double>();

                    // --- 端部1 (左)
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasE1 = kRow < nEnd1Chu1Arr[i];
                        if (!hasE1) continue;

                        bool joinLeft = (i > 0) && (kRow < nEnd2Chu1Arr[i - 1]);
                        double phi = Math.Max(0, diaE1Chu1Arr[i]);
                        double x1 = spanLeftArr[i];
                        double x2 = qLArr[i];

                        if (i == 0)
                        {
                            x2 += Math.Max(0, gTanbu) * phi;        // gTanbu * Φ
                            x1 += Math.Max(0, nigeUwaChu1Local);    // nige cho 上宙1

                            // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị
                        }
                        else
                        {
                            double ext = Math.Max(0, nTanbu) * phi; // nTanbu * Φ
                            if (!joinLeft) x1 -= ext;
                            x2 += ext;
                        }

                        x2 = Math.Min(x2, midArr[i] - minGap);
                        if (x2 > x1) rowSegs.Add((x1, x2));
                    }

                    // --- 中央 (上宙1)
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasMid = kRow < nMidChu1Arr[i];
                        if (!hasMid) continue;

                        bool hasE1 = kRow < nEnd1Chu1Arr[i];
                        bool hasE2 = kRow < nEnd2Chu1Arr[i];
                        bool hasTriple = hasE1 && hasMid && hasE2;

                        double phi = Math.Max(0, diaMidChu1Arr[i]);
                        double x1 = qLArr[i] - Math.Max(0, gChubu) * phi; // gChubu * Φ
                        double x2 = qRArr[i] + Math.Max(0, nChubu) * phi; // nChubu * Φ

                        x1 = Math.Max(x1, spanLeftArr[i] + minGap);
                        x2 = Math.Min(x2, spanRightArr[i] - minGap);
                        x2 = Math.Max(x2, midArr[i] + minGap);

                        if (x2 <= x1) continue;

                        if (hasTriple)
                        {
                            double cm = GetUwaHardCutAnchorX(i);
                            cm = Math.Max(cm, x1 + minGap);
                            cm = Math.Min(cm, x2 - minGap);
                            rowSegs.Add((x1, cm));
                            rowSegs.Add((cm, x2));
                            rowCuts.Add(cm);
                            //if (ShowPreRoundCutMarks)
                            //    DrawDotMm_Rec(canvas, T, item, cm, y, rMm: 30.0, layer: "MARK", fill: Brushes.Black);
                        }
                        else
                        {
                            rowSegs.Add((x1, x2));
                        }
                    }

                    // --- 端部2 (右)
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasE2 = kRow < nEnd2Chu1Arr[i];
                        if (!hasE2) continue;

                        bool joinRight = (i < spanCount - 1) && (kRow < nEnd1Chu1Arr[i + 1]);
                        double phi = Math.Max(0, diaE2Chu1Arr[i]);
                        double x1 = qRArr[i];
                        double x2 = spanRightArr[i];

                        if (i == spanCount - 1)
                        {
                            x1 -= Math.Max(0, gTanbu) * phi;        // gTanbu * Φ
                            x2 -= Math.Max(0, nigeUwaChu1Local);    // nige 上宙1 ngoài mép

                            // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị
                        }
                        else
                        {
                            double ext = Math.Max(0, nTanbu) * phi; // nTanbu * Φ
                            x1 -= ext;
                            if (!joinRight) x2 += ext;
                        }

                        x1 = Math.Max(x1, midArr[i] + minGap);
                        if (x2 > x1) rowSegs.Add((x1, x2));
                    }

                    if (rowSegs.Count == 0) continue;
                    rowSegs.Sort((a, b) => a.x1.CompareTo(b.x1));

                    var merged = new List<(double x1, double x2)>();
                    (double x1m, double x2m) cur = rowSegs[0];

                    bool IsHardCut(double x) => rowCuts.Any(cp => Math.Abs(cp - x) <= tol);

                    for (int s = 1; s < rowSegs.Count; s++)
                    {
                        var nx = rowSegs[s];
                        if (nx.x1 <= cur.x2m + tol)
                        {
                            double joinX = Math.Max(cur.x2m, nx.x1);
                            if (IsHardCut(joinX))
                            {
                                merged.Add(cur);
                                cur = nx;
                            }
                            else
                            {
                                cur.x2m = Math.Max(cur.x2m, nx.x2);
                            }
                        }
                        else
                        {
                            merged.Add(cur);
                            cur = nx;
                        }
                    }
                    merged.Add(cur);

                    // NEW: Làm tròn bội 500 cho chuỗi liền nhau
                    RoundContiguousChainsInPlaceBySpan(merged, 500.0, shitaSpanBreaks, 1.0);

                    // ① DỊCH BIÊN CẮT THẬT
                    ApplyTonariShiftToMergedCuts(
                        merged,
                        rowCuts,
                        spanLeftArr,
                        spanRightArr,
                        spanCount,
                        x => GetTonariDotOffset(kRow, x),
                        (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + GetTonariDotOffset(kRow, cut)
                    );

                    var visibleSegs = ClampVisibleOrangeSegsToBounds(
                        GetVisibleOrangeSegs(item, kRow, y, merged),
                        leftAnkaX_Global,
                        rightAnkaX_Global);
                    if (visibleSegs.Count == 0) continue;

                    hasVisibleChu1 = true;
                    lastYOfChu1 = y;

                    // ② VẼ DOT KHÔNG OFFSET NỮA
                    DrawAdjustedHardCutDots(
                        canvas, T, item, kRow,
                        visibleSegs.Select(seg => (seg.X1, seg.X2)).ToList(), rowCuts,
                        spanLeftArr, spanRightArr, spanCount,
                        y, dotR,
                        Brushes.Black,
                        0,
                        null,
                        (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + GetTonariDotOffset(kRow, cut)
                    );

                    foreach (var seg in visibleSegs)
                    {
                        var segKey = new OrangeSegKey(kRow, seg.BaseX1, seg.BaseX2, y);
                        ResolveAnkaDefaults(
                            item,
                            segKey,
                            seg.X1,
                            seg.X2,
                            leftAnkaX_Global,
                            rightAnkaX_Global,
                            hasLeftAnka_Global,
                            hasRightAnka_Global,
                            -ankaUwaChu,
                            -ankaUwaChu,
                            out bool hitLeftBoundary,
                            out bool hitRightBoundary,
                            out double defaultLeftSigned,
                            out double defaultRightSigned);
                        double wxTop = 0.5 * (seg.X1 + seg.X2);
                        double wyTop = y - OrangeDimPrimaryTextGapMm;
                        var topKey = new OrangeDimTextKey(kRow, true, wxTop, wyTop);
                        double leftSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Left, defaultLeftSigned);
                        double rightSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Right, defaultRightSigned);

                        if (teiUwaChuNow)
                        {
                            if (hitLeftBoundary)
                            {
                                DrawRect_Rec(canvas, T, item, seg.X1, y - rectH / 2.0, rectW, rectH,
                                             stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                            }
                            if (hitRightBoundary)
                            {
                                DrawRect_Rec(canvas, T, item, seg.X2, y - rectH / 2.0, rectW, rectH,
                                             stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                            }
                        }
                        else
                        {
                            if (Math.Abs(leftSigned) > 0.0001)
                            {
                                DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X1, y - leftSigned, Brushes.Black, 1.2, null, "MARK");
                                if (anka1 == true)
                                {
                                    DrawText_Rec(canvas, T, item, $"{Math.Abs(leftSigned):0}", seg.X1 - 650, y - leftSigned / 2.0 + 100, dimFont,
                                             Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                }
                            }
                            if (Math.Abs(rightSigned) > 0.0001)
                            {
                                DrawLine_Rec(canvas, T, item, seg.X2, y, seg.X2, y - rightSigned, Brushes.Black, 1.2, null, "MARK");
                                if (anka1 == true)
                                {
                                    DrawText_Rec(canvas, T, item, $"{Math.Abs(rightSigned):0}", seg.X2 + 250, y - rightSigned / 2.0 + 100, dimFont,
                                             Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                }
                            }
                        }

                        DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X2, y, Brushes.Black, 1.2, null, "MARK");

                        // TEXT CAM
                        DimOrangeSegmentWithAnkaLabels(
                            canvas, T, item,
                            seg.X1, seg.X2,
                            y, /*dyTop*/ -OrangeDimPrimaryTextGapMm, /*dyBottom*/ OrangeDimSecondaryTextGapMm,
                            diaMidChu1Arr, spanLeftArr, spanRightArr, spanCount,
                            defaultLeftSigned, defaultRightSigned,
                            leftSigned, rightSigned,
                            /*textAboveLine*/ true, seg.BaseX1, seg.BaseX2,
                                suppressAnkaInTopLength: seg.IsEqualCutChild,
                            forceShowForCutChild: seg.IsEqualCutChild,
                                rowIndex: kRow

                        );
                    }
                }

                if (hasVisibleChu1)
                {
                    DrawRebarLayerHeader(yChu1Base, "上宙1", nigeUwaChu1);
                    nextRebarLayerBaseY = lastYOfChu1 + visibleLayerGap;
                }
            }

            // =========================
            // ======  ⬇ 上宙2 ⬇  ======
            // =========================
            var nEnd1Chu2Arr = new int[spanCount];
            var nMidChu2Arr = new int[spanCount];
            var nEnd2Chu2Arr = new int[spanCount];
            var diaE1Chu2Arr = new double[spanCount];
            var diaMidChu2Arr = new double[spanCount];
            var diaE2Chu2Arr = new double[spanCount];

            for (int i = 0; i < spanCount && i < spans.Count; i++)
            {
                string leftNameS = names[i];
                string rightNameS = names[i + 1];
                var (G0, _, _, _) = GetBeamValuesByPosition(selF, selY, leftNameS, rightNameS);
                var zcfg = GetRebarConfigForSpan(selF, string.IsNullOrWhiteSpace(G0) ? "G0" : G0);

                var (cE1, pE1) = ParseCountPhiChu1(zcfg?.端部1上宙2);
                var (cMid, pMid) = ParseCountPhiChu1(zcfg?.中央上宙2);
                var (cE2, pE2) = ParseCountPhiChu1(zcfg?.端部2上宙2);

                nEnd1Chu2Arr[i] = cE1;
                nMidChu2Arr[i] = cMid;
                nEnd2Chu2Arr[i] = cE2;

                diaE1Chu2Arr[i] = (pE1 > 0 ? pE1 : diaE1Arr[i]);
                diaMidChu2Arr[i] = (pMid > 0 ? pMid : diaMidArr[i]);
                diaE2Chu2Arr[i] = (pE2 > 0 ? pE2 : diaE2Arr[i]);
            }

            // Line Aqua + label "上宙2"
            double yChu2Base = nextRebarLayerBaseY;
            // 上宙2 — GIỮ LOGIC giống 上筋/上宙1 (cắt giữa đoạn trung tâm khi hasTriple)
            double lastYOfChu2 = 0.0;
            {
                double yStartChu2 = yChu2Base + 500;
                double barSpacing = 500.0;
                double minGap = 50.0;
                double tol = 0.01;

                var (_, _, nigeUwaChu2Local, _, _, _) = GetNigeValues();
                var (_, ankaUwaChu, _, _) = GetAnkaNagaValues();
                var (_, teiUwaChuNow, _, _) = GetTeiFlags();

                double rectW = TeiRectW;
                double rectH = TeiRectH;
                double dotR = 30.0;

                // Vị trí ANKA biên cho 上宙2
                double leftAnkaX_Global = (spanCount > 0) ? spanLeftArr[0] + Math.Max(0, nigeUwaChu2Local) : double.NaN;
                double rightAnkaX_Global = (spanCount > 0) ? spanRightArr[spanCount - 1] - Math.Max(0, nigeUwaChu2Local) : double.NaN;
                bool hasLeftAnka_Global = (!teiUwaChuNow) && (ankaUwaChu > 0) && (spanCount > 0);
                bool hasRightAnka_Global = (!teiUwaChuNow) && (ankaUwaChu > 0) && (spanCount > 0);

                int nRowsGlobalChu2 = GetMaxLayerRowCount(nEnd1Chu2Arr, nMidChu2Arr, nEnd2Chu2Arr);
                bool hasVisibleChu2 = false;

                for (int kRow = 0; kRow < nRowsGlobalChu2; kRow++)
                {
                    //double tonariOffset = GetTonariDotOffset(kRow);

                    double y = yStartChu2 + kRow * barSpacing;

                    var rowSegs = new List<(double x1, double x2)>();
                    var rowCuts = new List<double>();
                    var cutAtE2 = new double?[spanCount];

                    // --- 端部1 (左)
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasE1 = kRow < nEnd1Chu2Arr[i];
                        if (!hasE1) continue;

                        bool joinLeft = (i > 0) && (kRow < nEnd2Chu2Arr[i - 1]);
                        double phi = Math.Max(0, diaE1Chu2Arr[i]);
                        double x1 = spanLeftArr[i];
                        double x2 = qLArr[i];

                        if (i == 0)
                        {
                            x2 += Math.Max(0, gTanbu) * phi;          // gTanbu * Φ
                            x1 += Math.Max(0, nigeUwaChu2Local);      // nige cho 上宙2

                            // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị
                        }
                        else
                        {
                            double ext = Math.Max(0, nTanbu) * phi;   // nTanbu * Φ
                            if (!joinLeft) x1 -= ext;
                            x2 += ext;
                        }

                        x2 = Math.Min(x2, midArr[i] - minGap);
                        if (x2 > x1) rowSegs.Add((x1, x2));
                    }

                    // --- 中央 (上宙2: giống 上宙1/上筋)
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasMid = kRow < nMidChu2Arr[i];
                        if (!hasMid) continue;

                        bool hasE1 = kRow < nEnd1Chu2Arr[i];
                        bool hasE2 = kRow < nEnd2Chu2Arr[i];
                        bool hasTriple = hasE1 && hasMid && hasE2;

                        double phi = Math.Max(0, diaMidChu2Arr[i]);
                        double x1 = qLArr[i] - Math.Max(0, gChubu) * phi; // gChubu * Φ
                        double x2 = qRArr[i] + Math.Max(0, nChubu) * phi; // nChubu * Φ

                        x1 = Math.Max(x1, spanLeftArr[i] + minGap);
                        x2 = Math.Min(x2, spanRightArr[i] - minGap);
                        x2 = Math.Max(x2, midArr[i] + minGap);
                        if (x2 <= x1) continue;

                        if (hasTriple)
                        {
                            double cm = GetUwaHardCutAnchorX(i);
                            cm = Math.Max(cm, x1 + minGap);
                            cm = Math.Min(cm, x2 - minGap);
                            rowSegs.Add((x1, cm));
                            rowSegs.Add((cm, x2));
                            rowCuts.Add(cm);
                            //if (ShowPreRoundCutMarks)
                            //    DrawDotMm_Rec(canvas, T, item, cm, y, rMm: 30.0, layer: "MARK", fill: Brushes.Black);
                        }
                        else
                        {
                            rowSegs.Add((x1, x2));
                        }
                    }


                    // --- 端部2 (右)
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasE2 = kRow < nEnd2Chu2Arr[i];
                        if (!hasE2) continue;

                        bool joinRight = (i < spanCount - 1) && (kRow < nEnd1Chu2Arr[i + 1]);
                        double phi = Math.Max(0, diaE2Chu2Arr[i]);
                        double x1 = qRArr[i];
                        double x2 = spanRightArr[i];

                        if (i == spanCount - 1)
                        {
                            x1 -= Math.Max(0, gTanbu) * phi;          // gTanbu * Φ
                            x2 -= Math.Max(0, nigeUwaChu2Local);      // nige 上宙2 ngoài mép
                        }
                        else
                        {
                            double ext = Math.Max(0, nTanbu) * phi;   // nTanbu * Φ
                            x1 -= ext;
                            if (!joinRight) x2 += ext;
                        }

                        x1 = Math.Max(x1, midArr[i] + minGap);
                        if (x2 > x1)
                        {
                            // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị

                            rowSegs.Add((x1, x2));
                        }
                    }

                    if (rowSegs.Count == 0) continue;
                    rowSegs.Sort((a, b) => a.x1.CompareTo(b.x1));

                    var merged = new List<(double x1, double x2)>();
                    (double x1m, double x2m) cur = rowSegs[0];

                    bool IsHardCut(double x) => rowCuts.Any(cp => Math.Abs(cp - x) <= tol);

                    for (int s = 1; s < rowSegs.Count; s++)
                    {
                        var nx = rowSegs[s];
                        if (nx.x1 <= cur.x2m + tol)
                        {
                            double joinX = Math.Max(cur.x2m, nx.x1);
                            if (IsHardCut(joinX))
                            {
                                merged.Add(cur);
                                cur = nx;
                            }
                            else
                            {
                                cur.x2m = Math.Max(cur.x2m, nx.x2);
                            }
                        }
                        else
                        {
                            merged.Add(cur);
                            cur = nx;
                        }
                    }
                    merged.Add(cur);

                    // NEW: Làm tròn bội 500
                    RoundContiguousChainsInPlaceBySpan(merged, 500.0, shitaSpanBreaks, 1.0);

                    // ① DỊCH BIÊN CẮT THẬT
                    ApplyTonariShiftToMergedCuts(
                        merged,
                        rowCuts,
                        spanLeftArr,
                        spanRightArr,
                        spanCount,
                        x => GetTonariDotOffset(kRow, x),
                        (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + GetTonariDotOffset(kRow, cut)
                    );

                    var visibleSegs = ClampVisibleOrangeSegsToBounds(
                        GetVisibleOrangeSegs(item, kRow, y, merged),
                        leftAnkaX_Global,
                        rightAnkaX_Global);
                    if (visibleSegs.Count == 0) continue;

                    hasVisibleChu2 = true;
                    lastYOfChu2 = y;

                    // ② VẼ DOT KHÔNG OFFSET NỮA
                    DrawAdjustedHardCutDots(
                        canvas, T, item, kRow,
                        visibleSegs.Select(seg => (seg.X1, seg.X2)).ToList(), rowCuts,
                        spanLeftArr, spanRightArr, spanCount,
                        y, dotR,
                        Brushes.Black,
                        0,
                        null,
                        (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + GetTonariDotOffset(kRow, cut)
                    );

                    foreach (var seg in visibleSegs)
                    {
                        var segKey = new OrangeSegKey(kRow, seg.BaseX1, seg.BaseX2, y);
                        ResolveAnkaDefaults(
                            item,
                            segKey,
                            seg.X1,
                            seg.X2,
                            leftAnkaX_Global,
                            rightAnkaX_Global,
                            hasLeftAnka_Global,
                            hasRightAnka_Global,
                            -ankaUwaChu,
                            -ankaUwaChu,
                            out bool hitLeftBoundary,
                            out bool hitRightBoundary,
                            out double defaultLeftSigned,
                            out double defaultRightSigned);
                        double wxTop = 0.5 * (seg.X1 + seg.X2);
                        double wyTop = y - OrangeDimPrimaryTextGapMm;
                        var topKey = new OrangeDimTextKey(kRow, true, wxTop, wyTop);
                        double leftSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Left, defaultLeftSigned);
                        double rightSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Right, defaultRightSigned);

                        if (teiUwaChuNow)
                        {
                            if (hitLeftBoundary)
                            {
                                DrawRect_Rec(canvas, T, item, seg.X1, y - rectH / 2.0, rectW, rectH,
                                             stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                            }
                            if (hitRightBoundary)
                            {
                                DrawRect_Rec(canvas, T, item, seg.X2, y - rectH / 2.0, rectW, rectH,
                                             stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                            }
                        }
                        else
                        {
                            if (Math.Abs(leftSigned) > 0.0001)
                            {
                                DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X1, y - leftSigned, Brushes.Black, 1.2, null, "MARK");
                                if (anka1 == true)
                                {
                                    DrawText_Rec(canvas, T, item, $"{Math.Abs(leftSigned):0}", seg.X1 - 650, y - leftSigned / 2.0 + 100, dimFont,
                                         Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                }
                            }
                            if (Math.Abs(rightSigned) > 0.0001)
                            {
                                DrawLine_Rec(canvas, T, item, seg.X2, y, seg.X2, y - rightSigned, Brushes.Black, 1.2, null, "MARK");
                                if (anka1 == true)
                                {
                                    DrawText_Rec(canvas, T, item, $"{Math.Abs(rightSigned):0}", seg.X2 + 250, y - rightSigned / 2.0 + 100, dimFont,
                                         Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                }
                            }
                        }

                        DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X2, y, Brushes.Black, 1.2, null, "MARK");

                        // TEXT CAM
                        DimOrangeSegmentWithAnkaLabels(
                            canvas, T, item,
                            seg.X1, seg.X2,
                            y, /*dyTop*/ -OrangeDimPrimaryTextGapMm, /*dyBottom*/ OrangeDimSecondaryTextGapMm,
                            diaMidChu2Arr, spanLeftArr, spanRightArr, spanCount,
                            defaultLeftSigned, defaultRightSigned,
                            leftSigned, rightSigned,
                            /*textAboveLine*/ true, seg.BaseX1, seg.BaseX2,
                                suppressAnkaInTopLength: seg.IsEqualCutChild,
                            forceShowForCutChild: seg.IsEqualCutChild,
                                rowIndex: kRow

                        );
                    }
                }

                if (hasVisibleChu2)
                {
                    DrawRebarLayerHeader(yChu2Base, "上宙2", nigeUwaChu2);
                    nextRebarLayerBaseY = lastYOfChu2 + visibleLayerGap;
                }
            }

            // =========================
            // ======  ⬇ 下宙2 ⬇  ======
            // =========================
            var nEnd1ShitaChu2Arr = new int[spanCount];
            var nMidShitaChu2Arr = new int[spanCount];
            var nEnd2ShitaChu2Arr = new int[spanCount];
            var diaE1ShitaChu2Arr = new double[spanCount];
            var diaMidShitaChu2Arr = new double[spanCount];
            var diaE2ShitaChu2Arr = new double[spanCount];

            for (int i = 0; i < spanCount && i < spans.Count; i++)
            {
                string leftNameS = names[i];
                string rightNameS = names[i + 1];
                var (G0, _, _, _) = GetBeamValuesByPosition(selF, selY, leftNameS, rightNameS);
                var zcfg = GetRebarConfigForSpan(selF, string.IsNullOrWhiteSpace(G0) ? "G0" : G0);

                var (cE1, pE1) = ParseCountPhiChu1(zcfg?.端部1下宙2);
                var (cMid, pMid) = ParseCountPhiChu1(zcfg?.中央下宙2);
                var (cE2, pE2) = ParseCountPhiChu1(zcfg?.端部2下宙2);

                nEnd1ShitaChu2Arr[i] = cE1;
                nMidShitaChu2Arr[i] = cMid;
                nEnd2ShitaChu2Arr[i] = cE2;

                diaE1ShitaChu2Arr[i] = (pE1 > 0 ? pE1 : diaE1Arr[i]);
                diaMidShitaChu2Arr[i] = (pMid > 0 ? pMid : diaMidArr[i]);
                diaE2ShitaChu2Arr[i] = (pE2 > 0 ? pE2 : diaE2Arr[i]);
            }

            // Kẻ line Aqua & nhãn “下宙2”
            double yShitaChu2Base = nextRebarLayerBaseY;
            // 下宙2 — CẮT Ở GIỮA ĐOẠN E2
            double lastYOfShitaChu2 = 0.0;
            {
                double yStartShitaChu2 = yShitaChu2Base + 500;
                double barSpacing = 500.0;
                double minGap = 50.0;
                double tol = 0.01;

                var (_, _, _, nigeShitaChu2Local, _, _) = GetNigeValues();  // nige dưới (Chu2)
                var (_, _, ankaShitaChu, _) = GetAnkaNagaValues();
                var (_, _, teiShitaChuNow, _) = GetTeiFlags();

                double rectW = TeiRectW;
                double rectH = TeiRectH;
                double dotR = 30.0;

                // Vị trí ANKA biên cho 下宙2
                double leftAnkaX_Global = (spanCount > 0) ? spanLeftArr[0] + Math.Max(0, nigeShitaChu2Local) : double.NaN;
                double rightAnkaX_Global = (spanCount > 0) ? spanRightArr[spanCount - 1] - Math.Max(0, nigeShitaChu2Local) : double.NaN;
                bool hasLeftAnka_Global = (!teiShitaChuNow) && (ankaShitaChu > 0) && (spanCount > 0);
                bool hasRightAnka_Global = (!teiShitaChuNow) && (ankaShitaChu > 0) && (spanCount > 0);

                int nRowsGlobalShitaChu2 = GetMaxLayerRowCount(nEnd1ShitaChu2Arr, nMidShitaChu2Arr, nEnd2ShitaChu2Arr);
                bool hasVisibleShitaChu2 = false;

                for (int kRow = 0; kRow < nRowsGlobalShitaChu2; kRow++)
                {

                    //double tonariOffset = GetTonariDotOffset(kRow);

                    double y = yStartShitaChu2 + kRow * barSpacing;

                    var rowSegs = new List<(double x1, double x2)>();
                    var rowCuts = new List<double>();
                    var cutAtE2 = new double?[spanCount];

                    // --- 端部1 (左)
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasE1 = kRow < nEnd1ShitaChu2Arr[i];
                        if (!hasE1) continue;

                        bool joinLeft = (i > 0) && (kRow < nEnd2ShitaChu2Arr[i - 1]);
                        double phi = Math.Max(0, diaE1ShitaChu2Arr[i]);
                        double x1 = spanLeftArr[i];
                        double x2 = qLArr[i];

                        if (i == 0)
                        {
                            x2 += Math.Max(0, gTanbu) * phi;           // gTanbu * Φ
                            x1 += Math.Max(0, nigeShitaChu2Local);     // nige dưới (Chu2)

                            // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị
                        }
                        else
                        {
                            double ext = Math.Max(0, nTanbu) * phi;    // nTanbu * Φ
                            if (!joinLeft) x1 -= ext;
                            x2 += ext;
                        }

                        x2 = Math.Min(x2, midArr[i] - minGap);
                        if (x2 > x1) rowSegs.Add((x1, x2));
                    }

                    // --- 中央 (下宙2: cắt tại qR1, chỉ add nửa trái)
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasMid = kRow < nMidShitaChu2Arr[i];
                        if (!hasMid) continue;

                        bool hasE1 = kRow < nEnd1ShitaChu2Arr[i];
                        bool hasE2 = kRow < nEnd2ShitaChu2Arr[i];
                        bool hasTriple = hasE1 && hasMid && hasE2;

                        double phi = Math.Max(0, diaMidShitaChu2Arr[i]);
                        double x1 = qLArr[i] - Math.Max(0, gChubu) * phi; // gChubu * Φ
                        double x2 = qRArr[i] + Math.Max(0, nChubu) * phi; // nChubu * Φ

                        x1 = Math.Max(x1, spanLeftArr[i] + minGap);
                        x2 = Math.Min(x2, spanRightArr[i] - minGap);
                        x2 = Math.Max(x2, midArr[i] + minGap);

                        if (x2 <= x1) continue;

                        if (hasTriple)
                        {
                            // Anchor cụm cut của lớp dưới theo line xanh qR, rồi giữ nguyên clamp/rule hiện có.
                            double cE2 = GetShitaHardCutAnchorX(i);
                            cE2 = Math.Max(cE2, midArr[i] + minGap);
                            cE2 = Math.Max(cE2, x1 + minGap);
                            cE2 = Math.Min(cE2, spanRightArr[i] - minGap);

                            cutAtE2[i] = cE2;          // NEW
                            rowSegs.Add((x1, cE2));    // chỉ nửa trái
                                                       // rowCuts sẽ được add ở block 端部2 (右) khi trim, giống hiện tại
                        }
                        else
                        {
                            rowSegs.Add((x1, x2));
                        }
                    }

                    // --- 端部2 (右) — TRIM theo cutAtE2
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasE2 = kRow < nEnd2ShitaChu2Arr[i];
                        if (!hasE2) continue;

                        bool joinRight = (i < spanCount - 1) && (kRow < nEnd1ShitaChu2Arr[i + 1]);
                        double phi = Math.Max(0, diaE2ShitaChu2Arr[i]);
                        double x1 = qRArr[i];
                        double x2 = spanRightArr[i];

                        if (i == spanCount - 1)
                        {
                            x1 -= Math.Max(0, gTanbu) * phi;           // gTanbu * Φ
                            x2 -= Math.Max(0, nigeShitaChu2Local);     // nige ngoài mép

                            // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị
                        }
                        else
                        {
                            double ext = Math.Max(0, nTanbu) * phi;    // nTanbu * Φ
                            x1 -= ext;
                            if (!joinRight) x2 += ext;
                        }

                        if (cutAtE2[i].HasValue)
                        {
                            x1 = Math.Max(x1, cutAtE2[i].Value);
                            rowCuts.Add(cutAtE2[i].Value);
                        }

                        x1 = Math.Max(x1, midArr[i] + minGap);
                        if (x2 > x1) rowSegs.Add((x1, x2));
                    }

                    if (rowSegs.Count == 0) continue;
                    rowSegs.Sort((a, b) => a.x1.CompareTo(b.x1));

                    var merged = new List<(double x1, double x2)>();
                    (double x1m, double x2m) cur = rowSegs[0];

                    bool IsHardCut(double x) => rowCuts.Any(cp => Math.Abs(cp - x) <= tol);

                    for (int s = 1; s < rowSegs.Count; s++)
                    {
                        var nx = rowSegs[s];
                        if (nx.x1 <= cur.x2m + tol)
                        {
                            double joinX = Math.Max(cur.x2m, nx.x1);
                            if (IsHardCut(joinX))
                            {
                                merged.Add(cur);
                                cur = nx;
                            }
                            else
                            {
                                cur.x2m = Math.Max(cur.x2m, nx.x2);
                            }
                        }
                        else
                        {
                            merged.Add(cur);
                            cur = nx;
                        }
                    }
                    merged.Add(cur);

                    // NEW: Làm tròn bội 500
                    RoundContiguousChainsInPlaceBySpan(merged, 500.0, shitaSpanBreaks, 1.0);

                    // ① DỊCH BIÊN CẮT THẬT
                    ApplyTonariShiftToMergedCuts(
                        merged,
                        rowCuts,
                        spanLeftArr,
                        spanRightArr,
                        spanCount,
                        x => GetTonariDotOffset(kRow, x),
                        (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + 500.0 + GetTonariDotOffset(kRow, cut)
                    );

                    var visibleSegs = ClampVisibleOrangeSegsToBounds(
                        GetVisibleOrangeSegs(item, kRow, y, merged),
                        leftAnkaX_Global,
                        rightAnkaX_Global);
                    if (visibleSegs.Count == 0) continue;

                    hasVisibleShitaChu2 = true;
                    lastYOfShitaChu2 = y;

                    // ② VẼ DOT KHÔNG OFFSET NỮA
                    DrawAdjustedHardCutDots(
                        canvas, T, item, kRow,
                        visibleSegs.Select(seg => (seg.X1, seg.X2)).ToList(), rowCuts,
                        spanLeftArr, spanRightArr, spanCount,
                        y, dotR,
                        Brushes.Black,
                        0,
                        null,
                        (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + 500.0 + GetTonariDotOffset(kRow, cut)
                    );

                    foreach (var seg in visibleSegs)
                    {
                        var segKey = new OrangeSegKey(kRow, seg.BaseX1, seg.BaseX2, y);
                        ResolveAnkaDefaults(
                            item,
                            segKey,
                            seg.X1,
                            seg.X2,
                            leftAnkaX_Global,
                            rightAnkaX_Global,
                            hasLeftAnka_Global,
                            hasRightAnka_Global,
                            +ankaShitaChu,
                            +ankaShitaChu,
                            out bool hitLeftBoundary,
                            out bool hitRightBoundary,
                            out double defaultLeftSigned,
                            out double defaultRightSigned);
                        double wxTop = 0.5 * (seg.X1 + seg.X2);
                        double wyTop = y - OrangeDimPrimaryTextGapMm;
                        var topKey = new OrangeDimTextKey(kRow, true, wxTop, wyTop);
                        double leftSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Left, defaultLeftSigned);
                        double rightSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Right, defaultRightSigned);

                        if (teiShitaChuNow)
                        {
                            if (hitLeftBoundary)
                            {
                                DrawRect_Rec(canvas, T, item, seg.X1, y - rectH / 2.0, rectW, rectH,
                                             stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                            }
                            if (hitRightBoundary)
                            {
                                DrawRect_Rec(canvas, T, item, seg.X2, y - rectH / 2.0, rectW, rectH,
                                             stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                            }
                        }
                        else
                        {
                            if (Math.Abs(leftSigned) > 0.0001)
                            {
                                DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X1, y - leftSigned, Brushes.Black, 1.2, null, "MARK");
                                if (anka1 == true)
                                {
                                    DrawText_Rec(canvas, T, item, $"{Math.Abs(leftSigned):0}", seg.X1 - 650, y - leftSigned / 2.0 + 100, dimFont,
                                             Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                }
                            }
                            if (Math.Abs(rightSigned) > 0.0001)
                            {
                                DrawLine_Rec(canvas, T, item, seg.X2, y, seg.X2, y - rightSigned, Brushes.Black, 1.2, null, "MARK");
                                if (anka1 == true)
                                {
                                    DrawText_Rec(canvas, T, item, $"{Math.Abs(rightSigned):0}", seg.X2 + 250, y - rightSigned / 2.0 + 100, dimFont,
                                             Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                }
                            }
                        }

                        DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X2, y, Brushes.Black, 1.2, null, "MARK");

                        // TEXT CAM (nhóm dưới): dùng cùng khoảng cách chuẩn như nhóm trên
                        DimOrangeSegmentWithAnkaLabels(
                            canvas, T, item,
                            seg.X1, seg.X2,
                            y, /*dyTop*/ -OrangeDimPrimaryTextGapMm, /*dyBottom*/ OrangeDimSecondaryTextGapMm,
                            diaMidShitaChu2Arr, spanLeftArr, spanRightArr, spanCount,
                            defaultLeftSigned, defaultRightSigned,
                            leftSigned, rightSigned,
                            /*textAboveLine*/ true, seg.BaseX1, seg.BaseX2,
                                suppressAnkaInTopLength: seg.IsEqualCutChild,
                            forceShowForCutChild: seg.IsEqualCutChild,
                                rowIndex: kRow

                        );
                    }
                }

                if (hasVisibleShitaChu2)
                {
                    DrawRebarLayerHeader(yShitaChu2Base, "\u4e0b\u5b99\u0032", nigeShitaChu2);
                    nextRebarLayerBaseY = lastYOfShitaChu2 + visibleLayerGap;
                }
            }

            // =========================
            // ======  ⬇ 下宙1 ⬇  ======
            // =========================
            var nEnd1ShitaChu1Arr = new int[spanCount];
            var nMidShitaChu1Arr = new int[spanCount];
            var nEnd2ShitaChu1Arr = new int[spanCount];
            var diaE1ShitaChu1Arr = new double[spanCount];
            var diaMidShitaChu1Arr = new double[spanCount];
            var diaE2ShitaChu1Arr = new double[spanCount];

            for (int i = 0; i < spanCount && i < spans.Count; i++)
            {
                string leftNameS = names[i];
                string rightNameS = names[i + 1];
                var (G0, _, _, _) = GetBeamValuesByPosition(selF, selY, leftNameS, rightNameS);
                var zcfg = GetRebarConfigForSpan(selF, string.IsNullOrWhiteSpace(G0) ? "G0" : G0);

                var (cE1, pE1) = ParseCountPhiChu1(zcfg?.端部1下宙1);
                var (cMid, pMid) = ParseCountPhiChu1(zcfg?.中央下宙1);
                var (cE2, pE2) = ParseCountPhiChu1(zcfg?.端部2下宙1);

                nEnd1ShitaChu1Arr[i] = cE1;
                nMidShitaChu1Arr[i] = cMid;
                nEnd2ShitaChu1Arr[i] = cE2;

                diaE1ShitaChu1Arr[i] = (pE1 > 0 ? pE1 : diaE1Arr[i]);
                diaMidShitaChu1Arr[i] = (pMid > 0 ? pMid : diaMidArr[i]);
                diaE2ShitaChu1Arr[i] = (pE2 > 0 ? pE2 : diaE2Arr[i]);
            }

            // Kẻ line Aqua & nhãn “下宙1”
            double yShitaChu1Base = nextRebarLayerBaseY;
            double lastYOfShitaChu1 = 0.0;

            // 下宙1 — CẮT Ở GIỮA ĐOẠN E2
            {
                double yStartShitaChu1 = yShitaChu1Base + 500;
                double barSpacing = 500.0;
                double minGap = 50.0;
                double tol = 0.01;

                var (_, _, _, _, nigeShitaChu1Local, _) = GetNigeValues();
                var (_, _, ankaShitaChu, _) = GetAnkaNagaValues();
                var (_, _, teiShitaChuNow, _) = GetTeiFlags();

                double rectW = TeiRectW;
                double rectH = TeiRectH;
                double dotR = 30.0;

                // Vị trí ANKA biên cho 下宙1
                double leftAnkaX_Global = (spanCount > 0) ? spanLeftArr[0] + Math.Max(0, nigeShitaChu1Local) : double.NaN;
                double rightAnkaX_Global = (spanCount > 0) ? spanRightArr[spanCount - 1] - Math.Max(0, nigeShitaChu1Local) : double.NaN;
                bool hasLeftAnka_Global = (!teiShitaChuNow) && (ankaShitaChu > 0) && (spanCount > 0);
                bool hasRightAnka_Global = (!teiShitaChuNow) && (ankaShitaChu > 0) && (spanCount > 0);

                int nRowsGlobalShitaChu1 = GetMaxLayerRowCount(nEnd1ShitaChu1Arr, nMidShitaChu1Arr, nEnd2ShitaChu1Arr);
                bool hasVisibleShitaChu1 = false;

                for (int kRow = 0; kRow < nRowsGlobalShitaChu1; kRow++)
                {
                    //double tonariOffset = GetTonariDotOffset(kRow);

                    double y = yStartShitaChu1 + kRow * barSpacing;
                    // ⭐ Thêm dòng này — giống hệt 下宙2

                    var rowSegs = new List<(double x1, double x2)>();
                    var rowCuts = new List<double>();
                    var cutAtE2 = new double?[spanCount];

                    // --- 端部1 (左)
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasE1 = kRow < nEnd1ShitaChu1Arr[i];
                        if (!hasE1) continue;

                        bool joinLeft = (i > 0) && (kRow < nEnd2ShitaChu1Arr[i - 1]);
                        double phi = Math.Max(0, diaE1ShitaChu1Arr[i]);
                        double x1 = spanLeftArr[i];
                        double x2 = qLArr[i];

                        if (i == 0)
                        {
                            x2 += Math.Max(0, gTanbu) * phi;           // gTanbu * Φ
                            x1 += Math.Max(0, nigeShitaChu1Local);     // nige dưới (Chu1)

                            // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị
                        }
                        else
                        {
                            double ext = Math.Max(0, nTanbu) * phi;    // nTanbu * Φ
                            if (!joinLeft) x1 -= ext;
                            x2 += ext;
                        }

                        x2 = Math.Min(x2, midArr[i] - minGap);
                        if (x2 > x1) rowSegs.Add((x1, x2));
                    }
                    // --- 中央 (下宙1: cắt tại qR1, chỉ add nửa trái)
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasMid = kRow < nMidShitaChu1Arr[i];
                        if (!hasMid) continue;

                        bool hasE1 = kRow < nEnd1ShitaChu1Arr[i];
                        bool hasE2 = kRow < nEnd2ShitaChu1Arr[i];
                        bool hasTriple = hasE1 && hasMid && hasE2;

                        double phi = Math.Max(0, diaMidShitaChu1Arr[i]);
                        double x1 = qLArr[i] - Math.Max(0, gChubu) * phi; // gChubu * Φ
                        double x2 = qRArr[i] + Math.Max(0, nChubu) * phi; // nChubu * Φ

                        x1 = Math.Max(x1, spanLeftArr[i] + minGap);
                        x2 = Math.Min(x2, spanRightArr[i] - minGap);
                        x2 = Math.Max(x2, midArr[i] + minGap);

                        if (x2 <= x1) continue;

                        if (hasTriple)
                        {
                            double cE2 = GetShitaHardCutAnchorX(i);
                            cE2 = Math.Max(cE2, midArr[i] + minGap);
                            cE2 = Math.Max(cE2, x1 + minGap);
                            cE2 = Math.Min(cE2, spanRightArr[i] - minGap);

                            cutAtE2[i] = cE2;          // NEW
                            rowSegs.Add((x1, cE2));    // chỉ nửa trái
                        }
                        else
                        {
                            rowSegs.Add((x1, x2));
                        }
                    }

                    // --- 端部2 (右) — TRIM theo cutAtE2
                    for (int i = 0; i < spanCount; i++)
                    {
                        bool hasE2 = kRow < nEnd2ShitaChu1Arr[i];
                        if (!hasE2) continue;

                        bool joinRight = (i < spanCount - 1) && (kRow < nEnd1ShitaChu1Arr[i + 1]);
                        double phi = Math.Max(0, diaE2ShitaChu1Arr[i]);
                        double x1 = qRArr[i];
                        double x2 = spanRightArr[i];

                        if (i == spanCount - 1)
                        {
                            x1 -= Math.Max(0, gTanbu) * phi;           // gTanbu * Φ
                            x2 -= Math.Max(0, nigeShitaChu1Local);     // nige ngoài mép

                            // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị
                        }
                        else
                        {
                            double ext = Math.Max(0, nTanbu) * phi;    // nTanbu * Φ
                            x1 -= ext;
                            if (!joinRight) x2 += ext;
                        }

                        if (cutAtE2[i].HasValue)
                        {
                            x1 = Math.Max(x1, cutAtE2[i].Value);
                            rowCuts.Add(cutAtE2[i].Value);
                        }

                        x1 = Math.Max(x1, midArr[i] + minGap);
                        if (x2 > x1) rowSegs.Add((x1, x2));
                    }

                    if (rowSegs.Count == 0) continue;
                    rowSegs.Sort((a, b) => a.x1.CompareTo(b.x1));

                    var merged = new List<(double x1, double x2)>();
                    (double x1m, double x2m) cur = rowSegs[0];

                    bool IsHardCut(double x) => rowCuts.Any(cp => Math.Abs(cp - x) <= tol);

                    for (int s = 1; s < rowSegs.Count; s++)
                    {
                        var nx = rowSegs[s];
                        if (nx.x1 <= cur.x2m + tol)
                        {
                            double joinX = Math.Max(cur.x2m, nx.x1);
                            if (IsHardCut(joinX))
                            {
                                merged.Add(cur);
                                cur = nx;
                            }
                            else
                            {
                                cur.x2m = Math.Max(cur.x2m, nx.x2);
                            }
                        }
                        else
                        {
                            merged.Add(cur);
                            cur = nx;
                        }
                    }
                    merged.Add(cur);

                    // NEW: Làm tròn bội 500
                    RoundContiguousChainsInPlaceBySpan(merged, 500.0, shitaSpanBreaks, 1.0);

                    // ① DỊCH BIÊN CẮT THẬT
                    ApplyTonariShiftToMergedCuts(
                        merged,
                        rowCuts,
                        spanLeftArr,
                        spanRightArr,
                        spanCount,
                        x => GetTonariDotOffset(kRow, x),
                        (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + 500.0 + GetTonariDotOffset(kRow, cut)
                    );

                    var visibleSegs = ClampVisibleOrangeSegsToBounds(
                        GetVisibleOrangeSegs(item, kRow, y, merged),
                        leftAnkaX_Global,
                        rightAnkaX_Global);
                    if (visibleSegs.Count == 0) continue;

                    hasVisibleShitaChu1 = true;
                    lastYOfShitaChu1 = y;

                    // ② VẼ DOT KHÔNG OFFSET NỮA
                    DrawAdjustedHardCutDots(
                        canvas, T, item, kRow,
                        visibleSegs.Select(seg => (seg.X1, seg.X2)).ToList(), rowCuts,
                        spanLeftArr, spanRightArr, spanCount,
                        y, dotR,
                        Brushes.Black,
                        0,
                        null,
                        (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + 500.0 + GetTonariDotOffset(kRow, cut)
                    );

                    foreach (var seg in visibleSegs)
                    {
                        var segKey = new OrangeSegKey(kRow, seg.BaseX1, seg.BaseX2, y);
                        ResolveAnkaDefaults(
                            item,
                            segKey,
                            seg.X1,
                            seg.X2,
                            leftAnkaX_Global,
                            rightAnkaX_Global,
                            hasLeftAnka_Global,
                            hasRightAnka_Global,
                            +ankaShitaChu,
                            +ankaShitaChu,
                            out bool hitLeftBoundary,
                            out bool hitRightBoundary,
                            out double defaultLeftSigned,
                            out double defaultRightSigned);
                        double wxTop = 0.5 * (seg.X1 + seg.X2);
                        double wyTop = y - OrangeDimPrimaryTextGapMm;
                        var topKey = new OrangeDimTextKey(kRow, true, wxTop, wyTop);
                        double leftSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Left, defaultLeftSigned);
                        double rightSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Right, defaultRightSigned);

                        if (teiShitaChuNow)
                        {
                            if (hitLeftBoundary)
                            {
                                DrawRect_Rec(canvas, T, item, seg.X1, y - rectH / 2.0, rectW, rectH,
                                             stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                            }
                            if (hitRightBoundary)
                            {
                                DrawRect_Rec(canvas, T, item, seg.X2, y - rectH / 2.0, rectW, rectH,
                                             stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                            }
                        }
                        else
                        {
                            if (Math.Abs(leftSigned) > 0.0001)
                            {
                                DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X1, y - leftSigned, Brushes.Black, 1.2, null, "MARK");
                                if (anka1 == true)
                                {
                                    DrawText_Rec(canvas, T, item, $"{Math.Abs(leftSigned):0}", seg.X1 - 650, y - leftSigned / 2.0 + 100, dimFont,
                                             Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                }
                            }
                            if (Math.Abs(rightSigned) > 0.0001)
                            {
                                DrawLine_Rec(canvas, T, item, seg.X2, y, seg.X2, y - rightSigned, Brushes.Black, 1.2, null, "MARK");
                                if (anka1 == true)
                                {
                                    DrawText_Rec(canvas, T, item, $"{Math.Abs(rightSigned):0}", seg.X2 + 250, y - rightSigned / 2.0 + 100, dimFont,
                                             Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                }
                            }
                        }

                        DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X2, y, Brushes.Black, 1.2, null, "MARK");

                        // TEXT CAM (nhóm dưới)
                        DimOrangeSegmentWithAnkaLabels(
                            canvas, T, item,
                            seg.X1, seg.X2,
                            y, /*dyTop*/ -OrangeDimPrimaryTextGapMm, /*dyBottom*/ OrangeDimSecondaryTextGapMm,
                            diaMidShitaChu1Arr, spanLeftArr, spanRightArr, spanCount,
                            defaultLeftSigned, defaultRightSigned,
                            leftSigned, rightSigned,
                            /*textAboveLine*/ true, seg.BaseX1, seg.BaseX2,
                                suppressAnkaInTopLength: seg.IsEqualCutChild,
                            forceShowForCutChild: seg.IsEqualCutChild,
                                rowIndex: kRow

                        );
                    }
                }

                if (hasVisibleShitaChu1)
                {
                    DrawRebarLayerHeader(yShitaChu1Base, "\u4e0b\u5b99\u0031", nigeShitaChu1);
                    nextRebarLayerBaseY = lastYOfShitaChu1 + visibleLayerGap;
                }
            }

            // =========================
            // ======  ⬇ 下筋 ⬇  ======
            // =========================
            // Dùng: 端部1下筋本数 / 中央下筋本数 / 端部2下筋本数, với **teiShita** + **ankaShita**
            {
                var nEnd1ShitaArr = new int[spanCount];
                var nMidShitaArr = new int[spanCount];
                var nEnd2ShitaArr = new int[spanCount];

                var diaE1ShitaArr = new double[spanCount];
                var diaMidShitaArr = new double[spanCount];
                var diaE2ShitaArr = new double[spanCount];

                for (int i = 0; i < spanCount && i < spans.Count; i++)
                {
                    string leftNameS = names[i];
                    string rightNameS = names[i + 1];
                    var (G0, _, _, _) = GetBeamValuesByPosition(selF, selY, leftNameS, rightNameS);
                    var zcfg = GetRebarConfigForSpan(selF, string.IsNullOrWhiteSpace(G0) ? "G0" : G0);

                    int cE1 = 0, cMid = 0, cE2 = 0;
                    int.TryParse(zcfg?.端部1下筋本数, out cE1);
                    int.TryParse(zcfg?.中央下筋本数, out cMid);
                    int.TryParse(zcfg?.端部2下筋本数, out cE2);

                    nEnd1ShitaArr[i] = Math.Max(0, cE1);
                    nMidShitaArr[i] = Math.Max(0, cMid);
                    nEnd2ShitaArr[i] = Math.Max(0, cE2);

                    diaE1ShitaArr[i] = Math.Max(0, diaE1Arr[i]);
                    diaMidShitaArr[i] = Math.Max(0, diaMidArr[i]);
                    diaE2ShitaArr[i] = Math.Max(0, diaE2Arr[i]);
                }

                // Baseline & nhãn "下筋" – đặt ngay dưới 下宙1
                double yShitaganeBase = nextRebarLayerBaseY;

                var shitaHeaderLine = DrawLine_Rec(canvas, T, item,
                    leftEdge - 1500, yShitaganeBase,
                    rightEdge + 1500, yShitaganeBase,
                    Brushes.Aqua, 1.2, null, "CHAIN");
                //DrawText_Rec(canvas, T, item, "下筋", leftEdge - 1300, yShitaganeBase + LabelDy, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 150, "TEXT");

                var shitaLabel = DrawText_Rec(canvas, T, item, "下筋", leftEdge - 1300, yShitaganeBase + LabelDy +100, dimFont, Brushes.Red, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                // <<< ニゲ TEXT >>>
                TextBlock shitaNigeLeft = null;
                TextBlock shitaNigeRight = null;
                if (nige1 == true)
                {
                    shitaNigeLeft = DrawText_Rec(canvas, T, item, $"ニゲ {nigeShita:0}", leftEdge - 1100, yShitaganeBase + ValueDy, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                    shitaNigeRight = DrawText_Rec(canvas, T, item, $"ニゲ {nigeShita:0}", rightEdge + 1100, yShitaganeBase + ValueDy, dimFont, Brushes.Black, HAnchor.Center, VAnchor.Bottom, 200, "TEXT");
                }
                var shitaAnkaYs = ShitaganeAnkaYsFor(item);
                shitaAnkaYs.Clear();
                var shitaOrangeYs = ShitaganeOrangeYsFor(item);
                shitaOrangeYs.Clear();
                bool hasVisibleShita = false;
                if (shitaLabel != null)
                {
                    shitaLabel.Cursor = Cursors.Hand;
                    if (shitaLabel.Background == null) shitaLabel.Background = Brushes.Transparent;

                    shitaLabel.MouseLeftButtonDown += (s, e) =>
                    {
                        e.Handled = true;
                        //if (shitaAnkaYs.Count == 0)
                        //    return;

                        var sb = new StringBuilder();
                        //for (int i = 0; i < shitaAnkaYs.Count; i++)
                        if (shitaOrangeYs.Count == 0 && shitaAnkaYs.Count == 0)
                        {
                            //var (yStart, yEnd) = shitaAnkaYs[i];
                            //sb.AppendLine($"{i + 1}. Y = {yStart:0.##} → {yEnd:0.##}");
                            sb.AppendLine("オレンジ線がありません");
                        }
                        else
                        {
                            if (shitaOrangeYs.Count > 0)
                            //for (int i = 0; i < shitaAnkaYs.Count; i++)
                            {
                                //var (yStart, yEnd) = shitaAnkaYs[i];
                                //sb.AppendLine($"{i + 1}. Y = {yStart:0.##} → {yEnd:0.##}");

                                sb.AppendLine("下筋ラインY座標:");
                                for (int i = 0; i < shitaOrangeYs.Count; i++)
                                {
                                    sb.AppendLine($"{i + 1}. Y = {shitaOrangeYs[i]:0.##}");
                                }
                            }

                            if (shitaAnkaYs.Count > 0)
                            {
                                if (sb.Length > 0) sb.AppendLine();
                                sb.AppendLine("アンカー線Y座標:");
                                for (int i = 0; i < shitaAnkaYs.Count; i++)
                                {
                                    var (yStart, yEnd) = shitaAnkaYs[i];
                                    sb.AppendLine($"{i + 1}. Y = {yStart:0.##} → {yEnd:0.##}");
                                }
                            }
                        }
                        MessageBox.Show(sb.ToString(), "下筋 アンカーY座標");
                    };
                }

                double lastYOfShitakin1 = 0.0;
                // 下筋 — CẮT Ở GIỮA ĐOẠN E2
                {
                    double yStartShitagane = yShitaganeBase + 500;
                    double barSpacing = 500.0;
                    double minGap = 50.0;
                    double tol = 0.01;

                    //var lowerRebarLineYs = new List<double>();

                    var (_, _, _, ankaShita) = GetAnkaNagaValues();
                    var (_, _, _, teiShitaNow) = GetTeiFlags();

                    double rectW = TeiRectW;
                    double rectH = TeiRectH;
                    double dotR = 30.0;

                    // Vị trí ANKA biên cho 下筋
                    double leftAnkaX_Global = (spanCount > 0) ? spanLeftArr[0] + Math.Max(0, nigeShita) : double.NaN;
                    double rightAnkaX_Global = (spanCount > 0) ? spanRightArr[spanCount - 1] - Math.Max(0, nigeShita) : double.NaN;
                    bool hasLeftAnka_Global = (!teiShitaNow) && (ankaShita > 0) && (spanCount > 0);
                    bool hasRightAnka_Global = (!teiShitaNow) && (ankaShita > 0) && (spanCount > 0);

                    int nRowsGlobalShita = GetMaxLayerRowCount(nEnd1ShitaArr, nMidShitaArr, nEnd2ShitaArr);

                    for (int kRow = 0; kRow < nRowsGlobalShita; kRow++)
                    {
                        //double tonariOffset = GetTonariDotOffset(kRow);

                        double y = yStartShitagane + kRow * barSpacing;
                        //lowerRebarLineYs.Add(y);

                        var rowSegs = new List<(double x1, double x2)>();
                        var rowCuts = new List<double>();
                        var cutAtE2 = new double?[spanCount];

                        // --- 端部1 (左)
                        for (int i = 0; i < spanCount; i++)
                        {
                            bool hasE1 = kRow < nEnd1ShitaArr[i];
                            if (!hasE1) continue;

                            bool joinLeft = (i > 0) && (kRow < nEnd2ShitaArr[i - 1]);
                            double phi = Math.Max(0, diaE1ShitaArr[i]);
                            double x1 = spanLeftArr[i];
                            double x2 = qLArr[i];

                            if (i == 0)
                            {
                                x2 += Math.Max(0, gTanbu) * phi;           // gTanbu * Φ
                                x1 += Math.Max(0, nigeShita);              // nige dưới

                                // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị
                            }
                            else
                            {
                                double ext = Math.Max(0, nTanbu) * phi;    // nTanbu * Φ
                                if (!joinLeft) x1 -= ext;
                                x2 += ext;
                            }

                            x2 = Math.Min(x2, midArr[i] - minGap);
                            if (x2 > x1) rowSegs.Add((x1, x2));
                        }

                        // --- 中央 (下筋: cắt tại qR1, chỉ nửa trái)
                        for (int i = 0; i < spanCount; i++)
                        {
                            bool hasMid = kRow < nMidShitaArr[i];
                            if (!hasMid) continue;

                            bool hasE1 = kRow < nEnd1ShitaArr[i];
                            bool hasE2 = kRow < nEnd2ShitaArr[i];
                            bool hasTriple = hasE1 && hasMid && hasE2;

                            double phi = Math.Max(0, diaMidShitaArr[i]);
                            double x1 = qLArr[i] - Math.Max(0, gChubu) * phi; // gChubu * Φ
                            double x2 = qRArr[i] + Math.Max(0, nChubu) * phi; // nChubu * Φ

                            x1 = Math.Max(x1, spanLeftArr[i] + minGap);
                            x2 = Math.Min(x2, spanRightArr[i] - minGap);
                            x2 = Math.Max(x2, midArr[i] + minGap);

                            if (x2 <= x1) continue;

                            if (hasTriple)
                            {
                                double cE2 = GetShitaHardCutAnchorX(i);  // anchor cụm cut theo line xanh qR
                                cE2 = Math.Max(cE2, midArr[i] + minGap);
                                cE2 = Math.Max(cE2, x1 + minGap);
                                cE2 = Math.Min(cE2, spanRightArr[i] - minGap);

                                cutAtE2[i] = cE2;        // NEW
                                rowSegs.Add((x1, cE2));  // chỉ nửa trái
                            }
                            else
                            {
                                rowSegs.Add((x1, x2));
                            }
                        }

                        // --- 端部2 (右) — TRIM theo cutAtE2
                        for (int i = 0; i < spanCount; i++)
                        {
                            bool hasE2 = kRow < nEnd2ShitaArr[i];
                            if (!hasE2) continue;

                            bool joinRight = (i < spanCount - 1) && (kRow < nEnd1ShitaArr[i + 1]);
                            double phi = Math.Max(0, diaE2ShitaArr[i]);
                            double x1 = qRArr[i];
                            double x2 = spanRightArr[i];

                            if (i == spanCount - 1)
                            {
                                x1 -= Math.Max(0, gTanbu) * phi;           // gTanbu * Φ
                                x2 -= Math.Max(0, nigeShita);              // nige ngoài mép

                                // ANKA/TEI sẽ được vẽ sau khi xác định segment còn hiển thị
                            }
                            else
                            {
                                double ext = Math.Max(0, nTanbu) * phi;    // nTanbu * Φ
                                x1 -= ext;
                                if (!joinRight) x2 += ext;
                            }

                            if (cutAtE2[i].HasValue)
                            {
                                x1 = Math.Max(x1, cutAtE2[i].Value);
                                rowCuts.Add(cutAtE2[i].Value);
                            }

                            x1 = Math.Max(x1, midArr[i] + minGap);
                            if (x2 > x1) rowSegs.Add((x1, x2));
                        }

                        if (rowSegs.Count == 0) continue;
                        rowSegs.Sort((a, b) => a.x1.CompareTo(b.x1));

                        var merged = new List<(double x1, double x2)>();
                        (double x1m, double x2m) cur = rowSegs[0];

                        bool IsHardCut(double x) => rowCuts.Any(cp => Math.Abs(cp - x) <= tol);

                        for (int s = 1; s < rowSegs.Count; s++)
                        {
                            var nx = rowSegs[s];
                            if (nx.x1 <= cur.x2m + tol)
                            {
                                double joinX = Math.Max(cur.x2m, nx.x1);
                                if (IsHardCut(joinX))
                                {
                                    merged.Add(cur);
                                    cur = nx;
                                }
                                else
                                {
                                    cur.x2m = Math.Max(cur.x2m, nx.x2);
                                }
                            }
                            else
                            {
                                merged.Add(cur);
                                cur = nx;
                            }
                        }
                        merged.Add(cur);

                        // NEW: Làm tròn bội 500
                        RoundContiguousChainsInPlaceBySpan(merged, 500.0, shitaSpanBreaks, 1.0);

                        // ① DỊCH BIÊN CẮT THẬT
                        ApplyTonariShiftToMergedCuts(
                            merged,
                            rowCuts,
                            spanLeftArr,
                            spanRightArr,
                            spanCount,
                            x => GetTonariDotOffset(kRow, x),
                            (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + 500.0 + GetTonariDotOffset(kRow, cut)
                        );

                    var visibleSegs = ClampVisibleOrangeSegsToBounds(
                        GetVisibleOrangeSegs(item, kRow, y, merged),
                        leftAnkaX_Global,
                        rightAnkaX_Global);
                    if (visibleSegs.Count == 0) continue;

                        hasVisibleShita = true;
                        lastYOfShitakin1 = y;

                        if (!shitaOrangeYs.Contains(y))
                            shitaOrangeYs.Add(y);

                        // ② VẼ DOT KHÔNG OFFSET NỮA
                        DrawAdjustedHardCutDots(
                            canvas, T, item, kRow,
                            visibleSegs.Select(seg => (seg.X1, seg.X2)).ToList(), rowCuts,
                            spanLeftArr, spanRightArr, spanCount,
                            y, dotR,
                            Brushes.Black,
                            0,
                            null,
                            (cut, leftBaseX) => leftBaseX + CeilToBase(cut - leftBaseX, 500.0) + 500.0 + GetTonariDotOffset(kRow, cut)
                        );

                        foreach (var seg in visibleSegs)
                        {
                            var segKey = new OrangeSegKey(kRow, seg.BaseX1, seg.BaseX2, y);
                            ResolveAnkaDefaults(
                                item,
                                segKey,
                                seg.X1,
                                seg.X2,
                                leftAnkaX_Global,
                                rightAnkaX_Global,
                                hasLeftAnka_Global,
                                hasRightAnka_Global,
                                +ankaShita,
                                +ankaShita,
                                out bool hitLeftBoundary,
                                out bool hitRightBoundary,
                                out double defaultLeftSigned,
                                out double defaultRightSigned);
                            double wxTop = 0.5 * (seg.X1 + seg.X2);
                            double wyTop = y - OrangeDimPrimaryTextGapMm;
                            var topKey = new OrangeDimTextKey(kRow, true, wxTop, wyTop);
                            double leftSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Left, defaultLeftSigned);
                            double rightSigned = GetAnkaOverrideBySegKeyOrDefault(item, segKey, AnkaSide.Right, defaultRightSigned);

                            if (teiShitaNow)
                            {
                                if (hitLeftBoundary)
                                {
                                    DrawRect_Rec(canvas, T, item, seg.X1, y - rectH / 2.0, rectW, rectH,
                                                 stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                                }
                                if (hitRightBoundary)
                                {
                                    DrawRect_Rec(canvas, T, item, seg.X2, y - rectH / 2.0, rectW, rectH,
                                                 stroke: Brushes.Black, strokeThickness: 1.0, fill: Brushes.Black, layer: "MARK");
                                }
                            }
                            else
                            {
                                if (Math.Abs(leftSigned) > 0.0001)
                                {
                                    DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X1, y - leftSigned, Brushes.Black, 1.2, null, "MARK");
                                    shitaAnkaYs.Add((y, y - leftSigned));
                                    if (anka1 == true)
                                    {
                                        DrawText_Rec(canvas, T, item, $"{Math.Abs(leftSigned):0}", seg.X1 - 650, y - leftSigned / 2.0 + 100, dimFont,
                                                 Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                    }
                                }
                                if (Math.Abs(rightSigned) > 0.0001)
                                {
                                    DrawLine_Rec(canvas, T, item, seg.X2, y, seg.X2, y - rightSigned, Brushes.Black, 1.2, null, "MARK");
                                    shitaAnkaYs.Add((y, y - rightSigned));
                                    if (anka1 == true)
                                    {
                                        DrawText_Rec(canvas, T, item, $"{Math.Abs(rightSigned):0}", seg.X2 + 250, y - rightSigned / 2.0 + 100, dimFont,
                                                 Brushes.Black, HAnchor.Left, VAnchor.Bottom, 200, "TEXT");
                                    }
                                }
                            }

                            DrawLine_Rec(canvas, T, item, seg.X1, y, seg.X2, y, Brushes.Black, 1.2, null, "MARK");

                            // TEXT CAM (nhóm dưới)
                            DimOrangeSegmentWithAnkaLabels(
                                canvas, T, item,
                                seg.X1, seg.X2,
                                y, /*dyTop*/ -OrangeDimPrimaryTextGapMm, /*dyBottom*/ OrangeDimSecondaryTextGapMm,
                                diaMidShitaArr, spanLeftArr, spanRightArr, spanCount,
                                defaultLeftSigned, defaultRightSigned,
                                leftSigned, rightSigned,
                                /*textAboveLine*/ true, seg.BaseX1, seg.BaseX2,
                                suppressAnkaInTopLength: seg.IsEqualCutChild,
                            forceShowForCutChild: seg.IsEqualCutChild,
                                rowIndex: kRow

                            );
                        }
                    }
                }

                if (!hasVisibleShita)
                {
                    if (shitaHeaderLine != null) shitaHeaderLine.Visibility = System.Windows.Visibility.Collapsed;
                    if (shitaLabel != null) shitaLabel.Visibility = System.Windows.Visibility.Collapsed;
                    if (shitaNigeLeft != null) shitaNigeLeft.Visibility = System.Windows.Visibility.Collapsed;
                    if (shitaNigeRight != null) shitaNigeRight.Visibility = System.Windows.Visibility.Collapsed;
                }
                else
                {
                    nextRebarLayerBaseY = lastYOfShitakin1 + visibleLayerGap;
                }

                if (shitaOrangeYs.Count > 0)
                {
                    double targetOffsetY = shitaOrangeYs[shitaOrangeYs.Count - 1] - 7000;
                    ApplyShitaganeOffsets(targetOffsetY);
                }

            }

            if (RebuildAllOrangeCutMarkers(item) && _orangeCutMarkerRedrawInProgress.Add(item))
            {
                try
                {
                    Redraw(canvas, item);
                }
                finally
                {
                    _orangeCutMarkerRedrawInProgress.Remove(item);
                }
                return;
            }

            bool useLightweightCanvasUi = IsLightweightCanvasUi(item);

            if (useLightweightCanvasUi)
            {
                RenderSceneBitmapToCanvas(canvas, item, T);
            }

            ScheduleInteractiveViewportSnapshotRefresh(canvas, item);
        }
        // ========================= gần nhất thay đổi ở đây (2025/12/15 10h29) =========================
        // ===== DXF TEXT model =====
        struct DxfText
        {
            public string Value;       // nội dung
            public double X, Y;        // world(mm)
            public double Height;      // chiều cao chữ (mm)
            public int HAlign;         // 0=Left,1=Center,2=Right,3=Aligned,4=Middle,5=Fit
            public int VAlign;         // 0=Baseline,1=Bottom,2=Middle,3=Top
            public double RotationDeg; // góc
            public string Layer;       // layer
            public string Style;       // text style
            public double FontPx;      // kích thước font theo px (WPF)
            public string FontFamily;  // font family name
            public MediaColor Color;        // màu chữ
            public HAnchor HAnchor;    // neo ngang gốc
            public VAnchor VAnchor;    // neo dọc gốc
            public bool SkipCanvasBitmap;

            public DxfText(string value, double x, double y, double heightMm,
                           int hAlign = 1, int vAlign = 1, double rotDeg = 0,
                           string layer = "TEXT", string style = "STANDARD",
                           double fontPx = 12.0, string fontFamily = null,
                           MediaColor? color = null, HAnchor hAnchor = HAnchor.Center,
                           VAnchor vAnchor = VAnchor.Bottom,
                           bool skipCanvasBitmap = false)
            {
                Value = value;
                X = x; Y = y; Height = heightMm;
                HAlign = hAlign; VAlign = vAlign;
                RotationDeg = rotDeg;
                Layer = layer;
                Style = style;
                FontPx = fontPx;
                FontFamily = fontFamily ?? "Yu Mincho";
                Color = color ?? Colors.Black;
                HAnchor = hAnchor;
                VAnchor = vAnchor;
                SkipCanvasBitmap = skipCanvasBitmap;
            }
        }

        // ===== [SCENE] Build DXF từ Scene (fallback qua dữ liệu nếu Scene trống) =====
        private static double GetTextOffsetY(VAnchor anchor, TextOutputTarget target)
        {
            double magnitude;
            switch (target)
            {
                case TextOutputTarget.Ui:
                    magnitude = anchor == VAnchor.Bottom ? UiTopTextOffsetY
                              : anchor == VAnchor.Top ? UiBottomTextOffsetY
                              : 0.0;
                    break;
                case TextOutputTarget.Dxf:
                    magnitude = anchor == VAnchor.Bottom ? DxfTopTextOffsetY
                              : anchor == VAnchor.Top ? DxfBottomTextOffsetY
                              : 0.0;
                    break;
                case TextOutputTarget.Pdf:
                    magnitude = anchor == VAnchor.Bottom ? PdfTopTextOffsetY
                              : anchor == VAnchor.Top ? PdfBottomTextOffsetY
                              : 0.0;
                    break;
                default:
                    magnitude = 0.0;
                    break;
            }

            if (anchor == VAnchor.Bottom)
                return -magnitude;
            if (anchor == VAnchor.Top)
                return magnitude;
            return 0.0;
        }

        private static DxfText ApplyTextOffset(DxfText text, TextOutputTarget target)
        {
            double offsetY = GetTextOffsetY(text.VAnchor, target);
            if (Math.Abs(offsetY) < 1e-9)
                return text;

            text.Y += offsetY;
            return text;
        }

        private readonly struct DxfTextLayoutMetrics
        {
            public DxfTextLayoutMetrics(DxfText text, Typeface typeface, double fontPx, double widthPx, double heightPx, double widthMm, double heightMm)
            {
                Text = text;
                Typeface = typeface;
                FontPx = fontPx;
                WidthPx = widthPx;
                HeightPx = heightPx;
                WidthMm = widthMm;
                HeightMm = heightMm;
            }

            public DxfText Text { get; }
            public Typeface Typeface { get; }
            public double FontPx { get; }
            public double WidthPx { get; }
            public double HeightPx { get; }
            public double WidthMm { get; }
            public double HeightMm { get; }
        }

        private static Typeface CreateDxfTextTypeface(DxfText text)
            => new Typeface(new FontFamily(text.FontFamily ?? "Yu Mincho"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        private static double ResolveDxfTextFontPx(DxfText text, double scalePxPerMm)
        {
            if (text.Height > 0 && scalePxPerMm > 0)
                return Math.Max(1.0, Math.Abs(text.Height * scalePxPerMm));

            if (text.FontPx > 0)
                return Math.Max(1.0, text.FontPx);

            if (text.Height > 0)
                return Math.Max(1.0, text.Height * SharedMmToPx);

            return 12.0;
        }

        private static DxfTextLayoutMetrics MeasureDxfTextLayout(DxfText source, TextOutputTarget target, double scalePxPerMm)
        {
            var text = ApplyTextOffset(source, target);
            var typeface = CreateDxfTextTypeface(text);
            double fontPx = ResolveDxfTextFontPx(text, scalePxPerMm);
            var formatted = new FormattedText(
                text.Value ?? string.Empty,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                fontPx,
                Brushes.Black,
                1.0);

            double widthPx = Math.Max(0.0, formatted.WidthIncludingTrailingWhitespace);
            double heightPx = Math.Max(1.0, formatted.Height);
            double widthMm;
            double heightMm;

            if (scalePxPerMm > 0)
            {
                widthMm = widthPx / scalePxPerMm;
                heightMm = heightPx / scalePxPerMm;
            }
            else
            {
                heightMm = Math.Max(1.0, text.Height > 0 ? text.Height : heightPx * SharedPxToMm);
                widthMm = heightPx > 0
                    ? widthPx * (heightMm / heightPx)
                    : Math.Max(heightMm, (text.Value?.Length ?? 0) * heightMm * 0.6);
            }

            return new DxfTextLayoutMetrics(text, typeface, fontPx, widthPx, heightPx, widthMm, heightMm);
        }

        private static Point GetDxfTextTopLeft(Point anchorPoint, double width, double height, HAnchor hAnchor, VAnchor vAnchor)
        {
            double drawX = anchorPoint.X;
            double drawY = anchorPoint.Y;

            if (hAnchor == HAnchor.Center) drawX -= width / 2.0;
            else if (hAnchor == HAnchor.Right) drawX -= width;

            if (vAnchor == VAnchor.Middle) drawY -= height / 2.0;
            else if (vAnchor == VAnchor.Bottom) drawY -= height;

            return new Point(drawX, drawY);
        }

        private static Vector GetDxfTextAnchorOffset(double width, double height, HAnchor hAnchor, VAnchor vAnchor)
        {
            double offsetX = 0;
            double offsetY = 0;

            if (hAnchor == HAnchor.Center) offsetX = width / 2.0;
            else if (hAnchor == HAnchor.Right) offsetX = width;

            if (vAnchor == VAnchor.Middle) offsetY = height / 2.0;
            else if (vAnchor == VAnchor.Bottom) offsetY = height;

            return new Vector(offsetX, offsetY);
        }

        private static Point GetPlotRenderTextAnchorWorld(DxfText text)
            => new Point(text.X, text.Y +25);

        private static Point GetPlotRenderTextTopLeftWorld(DxfTextLayoutMetrics metrics)
            => GetDxfTextTopLeft(GetPlotRenderTextAnchorWorld(metrics.Text), metrics.WidthMm, metrics.HeightMm, metrics.Text.HAnchor, metrics.Text.VAnchor);

        private static Point GetDxfTextTopLeftWorld(DxfTextLayoutMetrics metrics)
            => GetDxfTextTopLeft(new Point(metrics.Text.X, metrics.Text.Y), metrics.WidthMm, metrics.HeightMm, metrics.Text.HAnchor, metrics.Text.VAnchor);

        private (List<DxfLine> lines, List<DxfText> texts,
             List<DxfCircle> circles, List<DxfArc> arcs,
             List<DxfSolid> solids, string fileKey)
        BuildDxfGeometry(GridBotsecozu item)
        {
            string key = $"{_currentSecoList.階を選択}_{_currentSecoList.通を選択}";
            if (_sceneByItem.TryGetValue(item, out var scene) && scene != null && scene.Count > 0)
            {
                var lines = new List<DxfLine>();
                var texts = new List<DxfText>();
                var circles = new List<DxfCircle>();
                var arcs = new List<DxfArc>();
                var solids = new List<DxfSolid>();

                foreach (var sh in scene)
                {
                    if (sh is SceneLine ln)
                        lines.Add(new DxfLine(ln.X1, ln.Y1, ln.X2, ln.Y2, ln.Layer, ln.Thickness, ln.Dash, ln.StrokeColor));
                    else if (sh is DxfText tx) texts.Add(tx);
                    else if (sh is DxfCircle cc) circles.Add(cc);
                    else if (sh is DxfArc ar) arcs.Add(ar);            // <--- THÊM DÒNG NÀY
                    else if (sh is DxfSolid sd) solids.Add(sd);
                }
                return (lines, texts, circles, arcs, solids, key);
            }
            return (new List<DxfLine>(), new List<DxfText>(),
                    new List<DxfCircle>(), new List<DxfArc>(),
                    new List<DxfSolid>(), key);
        }


        struct DxfLine
        {
            public double X1, Y1, X2, Y2;
            public string Layer;
            public double ThicknessPx;
            public double[] Dash;
            public MediaColor StrokeColor;
            public DxfLine(double x1, double y1, double x2, double y2,
                           string layer = "0", double thicknessPx = 2.5,
                           double[] dash = null, MediaColor? strokeColor = null)
            {
                X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
                Layer = string.IsNullOrWhiteSpace(layer) ? "0" : layer;
                ThicknessPx = thicknessPx;
                Dash = dash;
                StrokeColor = strokeColor ?? Colors.Black;
            }
        }
        struct DxfCircle
        {
            public double X, Y, R;
            public string Layer;
            public bool Filled;
            public MediaColor StrokeColor;
            public MediaColor FillColor;
            public double StrokeThicknessPx;
            public double[] Dash;
            public DxfCircle(double x, double y, double r, string layer = "0", bool filled = false,
                             MediaColor? strokeColor = null, MediaColor? fillColor = null,
                             double strokeThicknessPx = 1.0, double[] dash = null)
            {
                X = x; Y = y; R = r;
                Layer = string.IsNullOrWhiteSpace(layer) ? "0" : layer;
                Filled = filled;
                StrokeColor = strokeColor ?? Colors.Black;
                FillColor = fillColor ?? StrokeColor;
                StrokeThicknessPx = strokeThicknessPx;
                Dash = dash;
            }
        }

        struct DxfSolid
        {
            public double X1, Y1, X2, Y2, X3, Y3, X4, Y4;
            public string Layer;
            public MediaColor FillColor;
            public DxfSolid(double x1, double y1, double x2, double y2,
                            double x3, double y3, double x4, double y4,
                            string layer = "0", MediaColor? fillColor = null)
            {
                X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
                X3 = x3; Y3 = y3; X4 = x4; Y4 = y4;
                Layer = string.IsNullOrWhiteSpace(layer) ? "0" : layer;
                FillColor = fillColor ?? Colors.Black;
            }
        }

        // Vẽ hình chữ nhật (centered theo mm) + ghi 4 cạnh vào Scene để DXF xuất ra 4 LINE.
        // cx: tọa độ X tâm; yTop: đỉnh trên; width/height: kích thước (mm).
        private void DrawRect_Rec(
           Canvas c, WCTransform T, GridBotsecozu owner,
           double cx, double yTop,
           double width, double height,
           Brush stroke = null, double strokeThickness = 1.0,
           Brush fill = null, string layer = "MARK")
        {
            double xL = cx - width / 2.0;
            double xR = cx + width / 2.0;
            double yB = yTop + height;

            // (1) Viền (4 LINE) để xem wireframe
            var strokeColor = ColorFromBrush(stroke ?? Brushes.Black, Colors.Black);
            SceneFor(owner).Add(new SceneLine(xL, yTop, xR, yTop, layer, strokeThickness, null, strokeColor));
            SceneFor(owner).Add(new SceneLine(xR, yTop, xR, yB, layer, strokeThickness, null, strokeColor));
            SceneFor(owner).Add(new SceneLine(xR, yB, xL, yB, layer, strokeThickness, null, strokeColor));
            SceneFor(owner).Add(new SceneLine(xL, yB, xL, yTop, layer, strokeThickness, null, strokeColor));

            // (2) 1 SOLID duy nhất với thứ tự đỉnh CHUẨN: UL, UR, LL, LR
            // (tránh "bow-tie", click vào là 1 entity duy nhất)
            SceneFor(owner).Add(new DxfSolid(
                xL, yTop,   // 1: trên-trái
                xR, yTop,   // 2: trên-phải
                xL, yB,     // 3: dưới-trái
                xR, yB,     // 4: dưới-phải
                layer,
                ColorFromBrush(fill ?? stroke ?? Brushes.Black, Colors.Black)));

            // (3) Vẽ trên Canvas (polyline đóng + fill để xem trên màn hình)
            if (IsLightweightCanvasUi(owner))
                return;
            var pts = new List<Point>
            {
                new Point(xL, yTop),
                new Point(xR, yTop),
                new Point(xR, yB),
                new Point(xL, yB),
                new Point(xL, yTop)
            };
            DrawPolylineW(c, T, pts, stroke ?? Brushes.Black, strokeThickness, fill ?? Brushes.Black);
        }


        // ===== GHI DXF (LINE + TEXT), flipY để CAD nhìn cùng hướng Canvas =====
        private void WriteSimpleDxf(
            string path,
            IEnumerable<DxfLine> lines,
            IEnumerable<DxfText> texts,
            IEnumerable<DxfCircle> circles,
            IEnumerable<DxfArc> arcs,
            IEnumerable<DxfSolid> solids,
            bool flipY = true)
        {
            // Collect all unique layers from input data (default to "0" if empty)
            var allLayers = new HashSet<string> { "0" };
            foreach (var ln in lines)
            {
                allLayers.Add(string.IsNullOrEmpty(ln.Layer) ? "0" : ln.Layer);
            }
            foreach (var t in texts)
            {
                allLayers.Add(string.IsNullOrEmpty(t.Layer) ? "TEXT" : t.Layer);
            }
            foreach (var c in circles)
            {
                allLayers.Add(string.IsNullOrEmpty(c.Layer) ? "0" : c.Layer);
            }
            foreach (var s in solids) allLayers.Add(string.IsNullOrEmpty(s.Layer) ? "0" : s.Layer);
            foreach (var a in arcs) allLayers.Add(string.IsNullOrEmpty(a.Layer) ? "0" : a.Layer);

            using (var sw = new StreamWriter(path, false, System.Text.Encoding.GetEncoding("shift_jis")))
            {
                // Header: older R12 format with Japanese code page
                sw.WriteLine("0"); sw.WriteLine("SECTION");
                sw.WriteLine("2"); sw.WriteLine("HEADER");
                sw.WriteLine("9"); sw.WriteLine("$ACADVER");
                sw.WriteLine("1"); sw.WriteLine("AC1009");
                sw.WriteLine("9"); sw.WriteLine("$DWGCODEPAGE");
                sw.WriteLine("3"); sw.WriteLine("ANSI_932");
                sw.WriteLine("0"); sw.WriteLine("ENDSEC");

                // TABLES section: Define LTYPE, LAYER, and STYLE
                sw.WriteLine("0"); sw.WriteLine("SECTION");
                sw.WriteLine("2"); sw.WriteLine("TABLES");

                // LTYPE table (required for linetypes like CONTINUOUS)
                sw.WriteLine("0"); sw.WriteLine("TABLE");
                sw.WriteLine("2"); sw.WriteLine("LTYPE");
                sw.WriteLine("70"); sw.WriteLine("1"); // Number of ltypes (just one)
                sw.WriteLine("0"); sw.WriteLine("LTYPE");
                sw.WriteLine("2"); sw.WriteLine("CONTINUOUS");
                sw.WriteLine("70"); sw.WriteLine("64"); // Flags
                sw.WriteLine("3"); sw.WriteLine("Solid line");
                sw.WriteLine("72"); sw.WriteLine("65");
                sw.WriteLine("73"); sw.WriteLine("0");
                sw.WriteLine("40"); sw.WriteLine("0.0");
                sw.WriteLine("0"); sw.WriteLine("ENDTAB");

                // LAYER table (dynamically define all used layers)
                sw.WriteLine("0"); sw.WriteLine("TABLE");
                sw.WriteLine("2"); sw.WriteLine("LAYER");
                sw.WriteLine("70"); sw.WriteLine(allLayers.Count.ToString()); // Number of layers
                foreach (var layer in allLayers)
                {
                    sw.WriteLine("0"); sw.WriteLine("LAYER");
                    sw.WriteLine("2"); sw.WriteLine(layer); // Layer name
                    sw.WriteLine("70"); sw.WriteLine("0"); // Flags (0 = enabled)
                    sw.WriteLine("62"); sw.WriteLine("7"); // Color (7 = white/black)
                    sw.WriteLine("6"); sw.WriteLine("CONTINUOUS"); // Linetype
                }
                sw.WriteLine("0"); sw.WriteLine("ENDTAB");

                // STYLE table (unchanged from original)
                sw.WriteLine("0"); sw.WriteLine("TABLE");
                sw.WriteLine("2"); sw.WriteLine("STYLE");
                sw.WriteLine("70"); sw.WriteLine("1"); // Number of styles
                                                       // Default STANDARD style
                sw.WriteLine("0"); sw.WriteLine("STYLE");
                sw.WriteLine("2"); sw.WriteLine("STANDARD");
                sw.WriteLine("70"); sw.WriteLine("0");
                sw.WriteLine("40"); sw.WriteLine("0");
                sw.WriteLine("41"); sw.WriteLine("1");
                sw.WriteLine("50"); sw.WriteLine("0");
                sw.WriteLine("71"); sw.WriteLine("0");
                sw.WriteLine("42"); sw.WriteLine("1");
                sw.WriteLine("3"); sw.WriteLine("MS UI Gothic");
                sw.WriteLine("4"); sw.WriteLine("");
                sw.WriteLine("0"); sw.WriteLine("ENDTAB");

                sw.WriteLine("0"); sw.WriteLine("ENDSEC");

                // BLOCKS section: Define the FILLED_CIRCLE block with SOLID approximations for unit circle fill
                sw.WriteLine("0"); sw.WriteLine("SECTION");
                sw.WriteLine("2"); sw.WriteLine("BLOCKS");
                sw.WriteLine("0"); sw.WriteLine("BLOCK");
                sw.WriteLine("8"); sw.WriteLine("0"); // Layer for block entities (inherits from INSERT layer)
                sw.WriteLine("2"); sw.WriteLine("FILLED_CIRCLE"); // Block name
                sw.WriteLine("70"); sw.WriteLine("0"); // Block type flag
                sw.WriteLine("10"); sw.WriteLine("0"); // Base point X
                sw.WriteLine("20"); sw.WriteLine("0"); // Base point Y
                sw.WriteLine("30"); sw.WriteLine("0"); // Base point Z
                sw.WriteLine("3"); sw.WriteLine("FILLED_CIRCLE"); // Block name again

                // Add 16 SOLID triangles to approximate a unit circle fill (center 0,0; radius 1)
                const int seg = 64; // Number of segments (higher = smoother, but larger file)
                for (int i = 0; i < seg; i++)
                {
                    double a1 = 2 * Math.PI * i / seg;
                    double a2 = 2 * Math.PI * (i + 1) / seg;
                    double x2 = Math.Cos(a1);
                    double y2 = Math.Sin(a1);
                    double x3 = Math.Cos(a2);
                    double y3 = Math.Sin(a2);

                    sw.WriteLine("0"); sw.WriteLine("SOLID");
                    sw.WriteLine("8"); sw.WriteLine("0");
                    sw.WriteLine("10"); sw.WriteLine("0.0"); // Center X
                    sw.WriteLine("20"); sw.WriteLine("0.0"); // Center Y
                    sw.WriteLine("30"); sw.WriteLine("0.0"); // Center Z
                    sw.WriteLine("11"); sw.WriteLine(x2.ToString(CultureInfo.InvariantCulture)); // Vertex 2 X
                    sw.WriteLine("21"); sw.WriteLine(y2.ToString(CultureInfo.InvariantCulture)); // Vertex 2 Y
                    sw.WriteLine("31"); sw.WriteLine("0.0"); // Vertex 2 Z
                    sw.WriteLine("12"); sw.WriteLine(x3.ToString(CultureInfo.InvariantCulture)); // Vertex 3 X
                    sw.WriteLine("22"); sw.WriteLine(y3.ToString(CultureInfo.InvariantCulture)); // Vertex 3 Y
                    sw.WriteLine("32"); sw.WriteLine("0.0"); // Vertex 3 Z
                    sw.WriteLine("13"); sw.WriteLine(x3.ToString(CultureInfo.InvariantCulture)); // Vertex 4 X (same as 3 for triangle)
                    sw.WriteLine("23"); sw.WriteLine(y3.ToString(CultureInfo.InvariantCulture)); // Vertex 4 Y
                    sw.WriteLine("33"); sw.WriteLine("0.0"); // Vertex 4 Z
                }

                sw.WriteLine("0"); sw.WriteLine("ENDBLK"); // Fixed typo: must be ENDBLK (uppercase)
                sw.WriteLine("0"); sw.WriteLine("ENDSEC");

                // ENTITIES section
                sw.WriteLine("0"); sw.WriteLine("SECTION");
                sw.WriteLine("2"); sw.WriteLine("ENTITIES");

                // LINE
                foreach (var ln in lines)
                {
                    var y1 = flipY ? -ln.Y1 : ln.Y1;
                    var y2 = flipY ? -ln.Y2 : ln.Y2;
                    sw.WriteLine("0"); sw.WriteLine("LINE");
                    sw.WriteLine("8"); sw.WriteLine(string.IsNullOrEmpty(ln.Layer) ? "0" : ln.Layer);
                    sw.WriteLine("10"); sw.WriteLine(ln.X1.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("20"); sw.WriteLine(y1.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("30"); sw.WriteLine("0");
                    sw.WriteLine("11"); sw.WriteLine(ln.X2.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("21"); sw.WriteLine(y2.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("31"); sw.WriteLine("0");
                }

                // TEXT entities (unchanged)
                foreach (var t in texts)
                {
                    var exportText = ApplyTextOffset(t, TextOutputTarget.Dxf);
                    var y = flipY ? -exportText.Y : exportText.Y;
                    sw.WriteLine("0"); sw.WriteLine("TEXT");
                    sw.WriteLine("8"); sw.WriteLine(string.IsNullOrEmpty(exportText.Layer) ? "TEXT" : exportText.Layer);
                    //sw.WriteLine("7"); sw.WriteLine(string.IsNullOrEmpty(t.Style) ? "STANDARD" : t.Style);
                    sw.WriteLine("10"); sw.WriteLine(exportText.X.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("20"); sw.WriteLine(y.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("30"); sw.WriteLine("0");
                    sw.WriteLine("40"); sw.WriteLine(exportText.Height.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("1"); sw.WriteLine(exportText.Value ?? "");
                    sw.WriteLine("50"); sw.WriteLine(exportText.RotationDeg.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("72"); sw.WriteLine(exportText.HAlign.ToString());
                    sw.WriteLine("73"); sw.WriteLine(exportText.VAlign.ToString());
                    sw.WriteLine("11"); sw.WriteLine(exportText.X.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("21"); sw.WriteLine(y.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("31"); sw.WriteLine("0");
                }

                // CIRCLE entities with optional fill via BLOCK INSERT
                foreach (var c in circles)
                {
                    var cy = flipY ? -c.Y : c.Y;
                    var layer = string.IsNullOrEmpty(c.Layer) ? "0" : c.Layer;
                    if (c.Filled)
                    {
                        // Insert the BLOCK instance for the filled circle (scaled to radius)
                        sw.WriteLine("0"); sw.WriteLine("INSERT");
                        sw.WriteLine("8"); sw.WriteLine(layer);
                        sw.WriteLine("2"); sw.WriteLine("FILLED_CIRCLE"); // Block name
                        sw.WriteLine("10"); sw.WriteLine(c.X.ToString(CultureInfo.InvariantCulture)); // Insertion point X
                        sw.WriteLine("20"); sw.WriteLine(cy.ToString(CultureInfo.InvariantCulture)); // Insertion point Y
                        sw.WriteLine("30"); sw.WriteLine("0"); // Insertion point Z
                        sw.WriteLine("41"); sw.WriteLine(c.R.ToString(CultureInfo.InvariantCulture)); // X scale (radius)
                        sw.WriteLine("42"); sw.WriteLine(c.R.ToString(CultureInfo.InvariantCulture)); // Y scale (radius)
                        sw.WriteLine("43"); sw.WriteLine("1"); // Z scale
                    }
                    // Write CIRCLE for the boundary
                    sw.WriteLine("0"); sw.WriteLine("CIRCLE");
                    sw.WriteLine("8"); sw.WriteLine(layer);
                    sw.WriteLine("10"); sw.WriteLine(c.X.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("20"); sw.WriteLine(cy.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("30"); sw.WriteLine("0");
                    sw.WriteLine("40"); sw.WriteLine(c.R.ToString(CultureInfo.InvariantCulture));
                }

                // SOLID (khối đặc)
                foreach (var s in solids)
                {
                    var y1 = flipY ? -s.Y1 : s.Y1;
                    var y2 = flipY ? -s.Y2 : s.Y2;
                    var y3 = flipY ? -s.Y3 : s.Y3;
                    var y4 = flipY ? -s.Y4 : s.Y4;

                    sw.WriteLine("0"); sw.WriteLine("SOLID");
                    sw.WriteLine("8"); sw.WriteLine(string.IsNullOrEmpty(s.Layer) ? "0" : s.Layer);

                    sw.WriteLine("10"); sw.WriteLine(s.X1.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("20"); sw.WriteLine(y1.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("30"); sw.WriteLine("0");

                    sw.WriteLine("11"); sw.WriteLine(s.X2.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("21"); sw.WriteLine(y2.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("31"); sw.WriteLine("0");

                    sw.WriteLine("12"); sw.WriteLine(s.X3.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("22"); sw.WriteLine(y3.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("32"); sw.WriteLine("0");

                    sw.WriteLine("13"); sw.WriteLine(s.X4.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("23"); sw.WriteLine(y4.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("33"); sw.WriteLine("0");
                }

                double Norm360(double ang)
                {
                    ang %= 360.0; if (ang < 0) ang += 360.0; return ang;
                }

                foreach (var a in arcs)
                {
                    var ay = flipY ? -a.Y : a.Y;
                    double start = a.StartDeg, end = a.EndDeg;

                    // Khi flip Y: (x, y)->(x, -y) ⇒ góc θ -> -θ và phải ĐỔI THỨ TỰ start/end
                    if (flipY)
                    {
                        start = Norm360(-a.EndDeg);
                        end = Norm360(-a.StartDeg);
                    }

                    sw.WriteLine("0"); sw.WriteLine("ARC");
                    sw.WriteLine("8"); sw.WriteLine(string.IsNullOrEmpty(a.Layer) ? "0" : a.Layer);
                    sw.WriteLine("10"); sw.WriteLine(a.X.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("20"); sw.WriteLine(ay.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("30"); sw.WriteLine("0");
                    sw.WriteLine("40"); sw.WriteLine(a.R.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("50"); sw.WriteLine(start.ToString(CultureInfo.InvariantCulture));
                    sw.WriteLine("51"); sw.WriteLine(end.ToString(CultureInfo.InvariantCulture));
                }


                sw.WriteLine("0"); sw.WriteLine("ENDSEC");
                sw.WriteLine("0"); sw.WriteLine("EOF");
            }
        }


        // ===== Export handlers =====
        private void ExportDxf_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSecoList?.gridbotsecozu == null || _currentSecoList.gridbotsecozu.Count == 0)
            { MessageBox.Show("Không có gì để xuất."); return; }

            var dlg = new SaveFileDialog
            {
                Filter = "AutoCAD DXF (*.dxf)|*.dxf",
                FileName = $"{_currentSecoList.階を選択}_{_currentSecoList.通を選択}.dxf"
            };
            if (dlg.ShowDialog() != true) return;

            var allLines = new List<DxfLine>();
            var allTexts = new List<DxfText>();
            var allCircles = new List<DxfCircle>();
            var allArcs = new List<DxfArc>();
            var allSolids = new List<DxfSolid>();

            foreach (var it in _currentSecoList.gridbotsecozu)
            {
                var (lines, texts, circles, arcs, solids, _) = BuildDxfGeometry(it);
                allLines.AddRange(lines);
                allTexts.AddRange(texts);
                allCircles.AddRange(circles);
                allArcs.AddRange(arcs);
                allSolids.AddRange(solids);
            }

            WriteSimpleDxf(dlg.FileName, allLines, allTexts, allCircles, allArcs, allSolids, flipY: true);
            MessageBox.Show("DXF exported!");
        }


        private void ExportPdf_Click(object sender, RoutedEventArgs e)
        {
            ExportPdfScene_Click(sender, e);
            return;

#if false
            if (_currentSecoList?.gridbotsecozu == null || _currentSecoList.gridbotsecozu.Count == 0)
            { MessageBox.Show("Không có gì để xuất."); return; }

            if (GridList == null)
            { MessageBox.Show("Không tìm thấy canvas để xuất."); return; }

            GridList.UpdateLayout();
            var canvases = EnumerateCanvasVisuals().ToList();
            if (canvases.Count == 0)
            { MessageBox.Show("Không tìm thấy canvas để xuất."); return; }

            var sources = new List<PdfExportSource>();
            foreach (var canvas in canvases)
            {
                if (canvas?.DataContext is GridBotsecozu item)
                {
                    var key = BuildDxfGeometry(item).fileKey;
                    sources.Add(new PdfExportSource(item, canvas, key));
                }
            }

            if (sources.Count == 0)
            { MessageBox.Show("Không có dữ liệu để xuất PDF."); return; }

            var exportOptions = ShowPdfExportOptionsDialog(sources);
            if (exportOptions == null) return;

            var dlg = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"{_currentSecoList.階を選択}_{_currentSecoList.通を選択}.pdf"
            };
            if (dlg.ShowDialog() != true) return;

            var selectedKeys = new HashSet<string>(exportOptions.SelectedKeys ?? new List<string>(), StringComparer.Ordinal);
            var vectorPages = new List<PdfVectorPage>();
            foreach (var src in sources)
            {
                if (selectedKeys.Count > 0 && !selectedKeys.Contains(src.Key))
                    continue;

                if (!_sceneByItem.TryGetValue(src.Item, out var scene) || scene == null || scene.Count == 0)
                {
                    try { Redraw(src.Canvas, src.Item); }
                    catch { }
                }

                var (lines, texts, circles, arcs, solids, key) = BuildDxfGeometry(src.Item);
                var scene = CaptureSceneForPdfExport(src.Item, src.Key);
                var viewportWindows = BuildPdfViewportWindows(exportOptions.PaperSize, exportOptions.Orientation, exportOptions.ScaleDenominator, scene, exportOptions.FitToPage, exportOptions.InnerFrameOffsetMm, exportOptions.FitMode);
                if (viewportWindows.Count == 0)
                {
                    viewportWindows.Add(new PdfViewportWindow
                    {
                        PageIndex = 0,
                        PageCount = 1
                    });
                }

                foreach (var viewportWindow in viewportWindows)
                {
                    var pageSettings = new PdfPlotSettings
                    {
                        PaperSize = exportOptions.PaperSize,
                        Orientation = exportOptions.Orientation,
                        ScaleDenominator = exportOptions.ScaleDenominator,
                        FitMode = exportOptions.FitMode,
                        HorizontalAlignment = exportOptions.HorizontalAlignment,
                        VerticalAlignment = exportOptions.VerticalAlignment,
                        InnerFrameOffsetMm = exportOptions.InnerFrameOffsetMm,
                        TitleText = exportOptions.TitleText,
                        DateText = exportOptions.DateText,
                        SelectedKeys = exportOptions.SelectedKeys != null ? new List<string>(exportOptions.SelectedKeys) : new List<string>(),
                        ViewportWindow = viewportWindow
                    };

                    string pageKey = viewportWindow.PageCount > 1 ? $"{key}_{viewportWindow.PageIndex + 1}" : key;
                    var page = PdfVectorBuilder.Create(pageKey, lines, texts, circles, arcs, solids,
                                                       this.FontFamily?.Source ?? "Yu Mincho",
                                                       pageSettings);
                    if (page != null)
                        vectorPages.Add(page);
                }
            }

            if (vectorPages.Count == 0)
            { MessageBox.Show("Không thể tạo PDF vector từ dữ liệu hiện có."); return; }

            try
            {
                PdfVectorWriter.WritePdf(dlg.FileName, vectorPages);
                MessageBox.Show("PDF exported!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Xuất PDF thất bại: {ex.Message}");
            }
#endif
        }

        private sealed class PdfExportSource
        {
            public PdfExportSource(GridBotsecozu item, Canvas canvas, string key)
            {
                Item = item;
                Canvas = canvas;
                Key = string.IsNullOrWhiteSpace(key) ? "(No name)" : key;
            }

            public GridBotsecozu Item { get; }
            public Canvas Canvas { get; }
            public string Key { get; }
        }

        private sealed class PdfPlotSettings
        {
            public PdfPaperSize PaperSize { get; set; }
            public PdfPaperOrientation Orientation { get; set; } = PdfPaperOrientation.Landscape;
            public double? ScaleDenominator { get; set; }
            public PdfFitMode FitMode { get; set; } = PdfFitMode.Width;
            public PdfHorizontalAlignment HorizontalAlignment { get; set; } = PdfHorizontalAlignment.Center;
            public PdfVerticalAlignment VerticalAlignment { get; set; } = PdfVerticalAlignment.Middle;
            public double InnerFrameOffsetMm { get; set; } = DefaultPdfInnerFrameOffsetMm;
            public string TitleText { get; set; } = "梁配筋図";
            public string DateText { get; set; } = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            public List<string> SelectedKeys { get; set; } = new List<string>();
            public bool FitToPage => !ScaleDenominator.HasValue;
            public bool IsPortrait => Orientation == PdfPaperOrientation.Portrait;
            public PdfViewportWindow ViewportWindow { get; set; }
        }

        private sealed class PdfViewportWindow
        {
            public int PageIndex { get; set; }
            public int PageCount { get; set; }
            public int StartSpanIndex { get; set; }
            public int EndSpanIndex { get; set; }
            public double MinX { get; set; }
            public double MaxX { get; set; }
            public double MinY { get; set; }
            public double MaxY { get; set; }
            public bool IsConnectorPage { get; set; }
            public int ConnectsFromPage { get; set; }
            public int ConnectsToPage { get; set; }
            public string Label
                => IsConnectorPage
                    ? $"Trang nối {ConnectsFromPage}-{ConnectsToPage}"
                    : (PageCount > 1 ? $"Trang {PageIndex + 1}/{PageCount}" : "Trang 1/1");
        }

        private sealed class PdfPaperMargins
        {
            public double LeftMm { get; }
            public double RightMm { get; }
            public double TopMm { get; }
            public double BottomMm { get; }

            public PdfPaperMargins(double leftMm, double rightMm, double topMm, double bottomMm)
            {
                LeftMm = leftMm;
                RightMm = rightMm;
                TopMm = topMm;
                BottomMm = bottomMm;
            }
        }

        private sealed class PdfPageLayoutPlan
        {
            public double PageWidthMm { get; set; }
            public double PageHeightMm { get; set; }
            public double PrintableWidthMm { get; set; }
            public double PrintableHeightMm { get; set; }
            public double ScaleMmPerMm { get; set; }
            public double MarginLeftMm { get; set; }
            public double MarginBottomMm { get; set; }
            public double PaperMarginLeftMm { get; set; }
            public double PaperMarginRightMm { get; set; }
            public double PaperMarginTopMm { get; set; }
            public double PaperMarginBottomMm { get; set; }
            public double FrameLeftMm { get; set; }
            public double FrameBottomMm { get; set; }
            public double FrameWidthMm { get; set; }
            public double FrameHeightMm { get; set; }
            public double InnerFrameOffsetMm { get; set; }
            public double InnerFrameLeftMm { get; set; }
            public double InnerFrameBottomMm { get; set; }
            public double InnerFrameWidthMm { get; set; }
            public double InnerFrameHeightMm { get; set; }
            public double ContentLeftMm { get; set; }
            public double ContentBottomMm { get; set; }
            public double ContentWidthMm { get; set; }
            public double ContentHeightMm { get; set; }
            public double TitleBlockLeftMm { get; set; }
            public double TitleBlockBottomMm { get; set; }
            public double TitleBlockWidthMm { get; set; }
            public double TitleBlockHeightMm { get; set; }
            public double UsedWidthMm { get; set; }
            public double UsedHeightMm { get; set; }
            public bool FitsContent { get; set; }
            public bool IsClipped => !FitsContent;
            public bool IsPortrait { get; set; }
            public string OrientationLabel => IsPortrait ? "Portrait" : "Landscape";
        }

        private static string GetPdfPaperDisplayText(PdfPaperSize paperSize)
            => paperSize == PdfPaperSize.A3 ? "A3" : "A4";

        private static string GetPdfOrientationDisplayText(PdfPaperOrientation orientation)
            => orientation == PdfPaperOrientation.Portrait ? "Portrait" : "Landscape";

        private static string GetPdfScaleDisplayText(double? scaleDenominator)
            => scaleDenominator.HasValue ? $"1:{scaleDenominator.Value:0}" : "Fit to page";

        private static string GetPdfScaleDisplayText(PdfPlotSettings settings)
        {
            if (settings == null)
                return "Fit to page (Width)";

            if (settings.ScaleDenominator.HasValue)
                return $"1:{settings.ScaleDenominator.Value:0}";

            return settings.FitMode == PdfFitMode.Height ? "Fit to page (Height)" : "Fit to page (Width)";
        }

        private static string GetPdfScaleDisplayText(PdfPlotSettings settings, PdfPageLayoutPlan layout)
        {
            if (layout == null || layout.ScaleMmPerMm <= 0 || double.IsNaN(layout.ScaleMmPerMm) || double.IsInfinity(layout.ScaleMmPerMm))
                return GetPdfScaleDisplayText(settings);

            double effectiveDenominator = 1.0 / layout.ScaleMmPerMm;
            if (settings?.FitToPage == true)
                return settings.FitMode == PdfFitMode.Height
                    ? $"Fit to page (Height) (≈ 1:{effectiveDenominator:0.##})"
                    : $"Fit to page (Width) (≈ 1:{effectiveDenominator:0.##})";

            return $"1:{effectiveDenominator:0.##}";
        }

        private static string GetPdfHorizontalAlignmentDisplayText(PdfHorizontalAlignment alignment)
        {
            switch (alignment)
            {
                case PdfHorizontalAlignment.Left:
                    return "Trái";
                case PdfHorizontalAlignment.Right:
                    return "Phải";
                default:
                    return "Giữa";
            }
        }

        private static string GetPdfVerticalAlignmentDisplayText(PdfVerticalAlignment alignment)
        {
            switch (alignment)
            {
                case PdfVerticalAlignment.Top:
                    return "Trên";
                case PdfVerticalAlignment.Bottom:
                    return "Dưới";
                default:
                    return "Giữa";
            }
        }

        private static void ApplyPdfAlignmentToVisibleBounds(PdfPlotSettings settings, ref double minX, ref double minY, ref double maxX, ref double maxY)
        {
            if (settings?.ScaleDenominator == null || settings.ScaleDenominator.Value <= 0)
                return;

            var page = CreateDefaultPdfPageLayout(
                settings.PaperSize,
                settings.Orientation,
                settings.InnerFrameOffsetMm);

            double visibleWidthMm = page.PrintableWidthMm * settings.ScaleDenominator.Value;
            double visibleHeightMm = page.PrintableHeightMm * settings.ScaleDenominator.Value;
            double currentWidthMm = maxX - minX;
            double currentHeightMm = maxY - minY;

            if (currentWidthMm > visibleWidthMm + 1e-6)
            {
                switch (settings.HorizontalAlignment)
                {
                    case PdfHorizontalAlignment.Right:
                        minX = maxX - visibleWidthMm;
                        break;
                    case PdfHorizontalAlignment.Center:
                        minX += (currentWidthMm - visibleWidthMm) / 2.0;
                        break;
                    default:
                        break;
                }

                maxX = minX + visibleWidthMm;
            }

            if (currentHeightMm > visibleHeightMm + 1e-6)
            {
                switch (settings.VerticalAlignment)
                {
                    case PdfVerticalAlignment.Top:
                        maxY = minY + visibleHeightMm;
                        break;
                    case PdfVerticalAlignment.Middle:
                        minY += (currentHeightMm - visibleHeightMm) / 2.0;
                        maxY = minY + visibleHeightMm;
                        break;
                    default:
                        minY = maxY - visibleHeightMm;
                        break;
                }
            }
        }

        private static Rect GetDxfTextBounds(DxfText text, TextOutputTarget target)
        {
            var metrics = MeasureDxfTextLayout(text, target, SharedMmToPx);
            Point topLeft = target == PlotRenderTextTarget
                ? GetPlotRenderTextTopLeftWorld(metrics)
                : GetDxfTextTopLeftWorld(metrics);
            return new Rect(topLeft.X, topLeft.Y, metrics.WidthMm, metrics.HeightMm);
        }

        private static (double WidthMm, double HeightMm) GetPdfPaperBaseSizeMm(PdfPaperSize paperSize)
            => paperSize == PdfPaperSize.A3 ? (420.0, 297.0) : (297.0, 210.0);

        private static PdfPaperMargins GetPdfPaperMargins(PdfPaperSize paperSize, bool isPortrait)
        {
            if (paperSize == PdfPaperSize.A3)
            {
                if (isPortrait)
                {
                    return new PdfPaperMargins(
                        leftMm: 10.0,
                        rightMm: 10.0,
                        topMm: 15.0,
                        bottomMm: 15.0);
                }

                return new PdfPaperMargins(
                    leftMm: 15.0,
                    rightMm: 15.0,
                    topMm: 10.0,
                    bottomMm: 10.0);
            }

            if (isPortrait)
            {
                return new PdfPaperMargins(
                    leftMm: 5.0,
                    rightMm: 5.0,
                    topMm: 10.0,
                    bottomMm: 10.0);
            }

            return new PdfPaperMargins(
                leftMm: 10.0,
                rightMm: 10.0,
                topMm: 5.0,
                bottomMm: 5.0);
        }

        private static PdfPageLayoutPlan CreateDefaultPdfPageLayout(PdfPaperSize paperSize, PdfPaperOrientation orientation, double innerFrameOffsetMm = DefaultPdfInnerFrameOffsetMm)
        {
            var baseSize = GetPdfPaperBaseSizeMm(paperSize);
            bool isPortrait = orientation == PdfPaperOrientation.Portrait;
            double pageWidthMm = isPortrait ? baseSize.HeightMm : baseSize.WidthMm;
            double pageHeightMm = isPortrait ? baseSize.WidthMm : baseSize.HeightMm;
            var margins = GetPdfPaperMargins(paperSize, isPortrait);
            double frameLeft = margins.LeftMm;
            double frameBottom = margins.BottomMm;
            double frameWidth = Math.Max(1.0, pageWidthMm - margins.LeftMm - margins.RightMm);
            double frameHeight = Math.Max(1.0, pageHeightMm - margins.TopMm - margins.BottomMm);
            double safeInnerOffset = Math.Max(0.0, Math.Min(innerFrameOffsetMm, Math.Min(frameWidth, frameHeight) / 2.0 - 0.5));
            double innerFrameLeft = frameLeft + safeInnerOffset;
            double innerFrameBottom = frameBottom + safeInnerOffset;
            double innerFrameWidth = Math.Max(1.0, frameWidth - safeInnerOffset * 2.0);
            double innerFrameHeight = Math.Max(1.0, frameHeight - safeInnerOffset * 2.0);
            double contentLeft = innerFrameLeft;
            double contentBottom = innerFrameBottom;
            double contentWidth = innerFrameWidth;
            double contentHeight = innerFrameHeight;

            return new PdfPageLayoutPlan
            {
                PageWidthMm = pageWidthMm,
                PageHeightMm = pageHeightMm,
                PrintableWidthMm = contentWidth,
                PrintableHeightMm = contentHeight,
                ScaleMmPerMm = 1.0,
                MarginLeftMm = contentLeft,
                MarginBottomMm = contentBottom,
                PaperMarginLeftMm = margins.LeftMm,
                PaperMarginRightMm = margins.RightMm,
                PaperMarginTopMm = margins.TopMm,
                PaperMarginBottomMm = margins.BottomMm,
                FrameLeftMm = frameLeft,
                FrameBottomMm = frameBottom,
                FrameWidthMm = frameWidth,
                FrameHeightMm = frameHeight,
                InnerFrameOffsetMm = safeInnerOffset,
                InnerFrameLeftMm = innerFrameLeft,
                InnerFrameBottomMm = innerFrameBottom,
                InnerFrameWidthMm = innerFrameWidth,
                InnerFrameHeightMm = innerFrameHeight,
                ContentLeftMm = contentLeft,
                ContentBottomMm = contentBottom,
                ContentWidthMm = contentWidth,
                ContentHeightMm = contentHeight,
                TitleBlockLeftMm = frameLeft,
                TitleBlockBottomMm = frameBottom,
                TitleBlockWidthMm = frameWidth,
                TitleBlockHeightMm = 0.0,
                UsedWidthMm = 0.0,
                UsedHeightMm = 0.0,
                FitsContent = true,
                IsPortrait = isPortrait
            };
        }

        private static PdfPageLayoutPlan ResolvePdfPageLayout(
            PdfPaperSize paperSize,
            PdfPaperOrientation orientation,
            double contentWidthMm,
            double contentHeightMm,
            double? scaleDenominator,
            PdfFitMode fitMode = PdfFitMode.Width,
            PdfHorizontalAlignment horizontalAlignment = PdfHorizontalAlignment.Center,
            PdfVerticalAlignment verticalAlignment = PdfVerticalAlignment.Middle,
            double innerFrameOffsetMm = DefaultPdfInnerFrameOffsetMm)
        {
            const double titleBlockHeightMm = 0.0;

            contentWidthMm = Math.Max(1.0, contentWidthMm);
            contentHeightMm = Math.Max(1.0, contentHeightMm);

            PdfPageLayoutPlan Evaluate(double pageWidthMm, double pageHeightMm, bool isPortrait)
            {
                var margins = GetPdfPaperMargins(paperSize, isPortrait);
                double frameLeft = margins.LeftMm;
                double frameBottom = margins.BottomMm;
                double frameWidth = Math.Max(1.0, pageWidthMm - margins.LeftMm - margins.RightMm);
                double frameHeight = Math.Max(1.0, pageHeightMm - margins.TopMm - margins.BottomMm);
                double safeInnerOffset = Math.Max(0.0, Math.Min(innerFrameOffsetMm, Math.Min(frameWidth, frameHeight) / 2.0 - 0.5));
                double innerFrameLeft = frameLeft + safeInnerOffset;
                double innerFrameBottom = frameBottom + safeInnerOffset;
                double innerFrameWidth = Math.Max(1.0, frameWidth - safeInnerOffset * 2.0);
                double innerFrameHeight = Math.Max(1.0, frameHeight - safeInnerOffset * 2.0);

                double contentLeft = innerFrameLeft;
                double contentBottom = innerFrameBottom;
                double printableWidth = innerFrameWidth;
                double printableHeight = innerFrameHeight;

                double scaleMmPerMm;
                bool fitsContent;
                if (scaleDenominator.HasValue && scaleDenominator.Value > 0)
                {
                    scaleMmPerMm = 1.0 / scaleDenominator.Value;
                    fitsContent = contentWidthMm * scaleMmPerMm <= printableWidth + 1e-6
                               && contentHeightMm * scaleMmPerMm <= printableHeight + 1e-6;
                }
                else
                {
                    scaleMmPerMm = fitMode == PdfFitMode.Height
                        ? printableHeight / contentHeightMm
                        : printableWidth / contentWidthMm;
                    fitsContent = true;
                }

                if (scaleMmPerMm <= 0 || double.IsNaN(scaleMmPerMm) || double.IsInfinity(scaleMmPerMm))
                    scaleMmPerMm = 1.0;

                double usedWidth = contentWidthMm * scaleMmPerMm;
                double usedHeight = contentHeightMm * scaleMmPerMm;
                double remainingWidth = Math.Max(0.0, printableWidth - usedWidth);
                double remainingHeight = Math.Max(0.0, printableHeight - usedHeight);

                double marginLeft = contentLeft;
                switch (horizontalAlignment)
                {
                    case PdfHorizontalAlignment.Left:
                        break;
                    case PdfHorizontalAlignment.Right:
                        marginLeft += remainingWidth;
                        break;
                    default:
                        marginLeft += remainingWidth / 2.0;
                        break;
                }

                double marginBottom = contentBottom;
                switch (verticalAlignment)
                {
                    case PdfVerticalAlignment.Bottom:
                        break;
                    case PdfVerticalAlignment.Top:
                        marginBottom += remainingHeight;
                        break;
                    default:
                        marginBottom += remainingHeight / 2.0;
                        break;
                }

                return new PdfPageLayoutPlan
                {
                    PageWidthMm = pageWidthMm,
                    PageHeightMm = pageHeightMm,
                    PrintableWidthMm = printableWidth,
                    PrintableHeightMm = printableHeight,
                    ScaleMmPerMm = scaleMmPerMm,
                    MarginLeftMm = marginLeft,
                    MarginBottomMm = marginBottom,
                    PaperMarginLeftMm = margins.LeftMm,
                    PaperMarginRightMm = margins.RightMm,
                    PaperMarginTopMm = margins.TopMm,
                    PaperMarginBottomMm = margins.BottomMm,
                    FrameLeftMm = frameLeft,
                    FrameBottomMm = frameBottom,
                    FrameWidthMm = frameWidth,
                    FrameHeightMm = frameHeight,
                    InnerFrameOffsetMm = safeInnerOffset,
                    InnerFrameLeftMm = innerFrameLeft,
                    InnerFrameBottomMm = innerFrameBottom,
                    InnerFrameWidthMm = innerFrameWidth,
                    InnerFrameHeightMm = innerFrameHeight,
                    ContentLeftMm = contentLeft,
                    ContentBottomMm = contentBottom,
                    ContentWidthMm = printableWidth,
                    ContentHeightMm = printableHeight,
                    TitleBlockLeftMm = frameLeft,
                    TitleBlockBottomMm = frameBottom,
                    TitleBlockWidthMm = frameWidth,
                    TitleBlockHeightMm = titleBlockHeightMm,
                    UsedWidthMm = usedWidth,
                    UsedHeightMm = usedHeight,
                    FitsContent = fitsContent,
                    IsPortrait = isPortrait
                };
            }

            var baseSize = GetPdfPaperBaseSizeMm(paperSize);
            bool usePortrait = orientation == PdfPaperOrientation.Portrait;
            return usePortrait
                ? Evaluate(baseSize.HeightMm, baseSize.WidthMm, isPortrait: true)
                : Evaluate(baseSize.WidthMm, baseSize.HeightMm, isPortrait: false);
        }

        private static string BuildPdfPlotStatusText(PdfPlotSettings settings, PdfPageLayoutPlan layout)
        {
            if (settings == null || layout == null)
                return string.Empty;

            string summary = $"Khổ {GetPdfPaperDisplayText(settings.PaperSize)} {layout.OrientationLabel} | Scale {GetPdfScaleDisplayText(settings, layout)}";
            if (settings.ViewportWindow != null && settings.ViewportWindow.PageCount > 1)
                summary = $"{summary} | {settings.ViewportWindow.Label}";
            if (!layout.IsClipped)
                return $"{summary}\nNội dung nằm trong vùng in {layout.PrintableWidthMm:0.#} x {layout.PrintableHeightMm:0.#} mm.";

            return $"{summary}\nCảnh báo: nội dung vượt khổ in và sẽ bị cắt, không tự fit lại.";
        }

        private List<PdfViewportWindow> BuildPdfViewportWindows(PdfPaperSize paperSize, PdfPaperOrientation orientation, double? scaleDenominator, IReadOnlyList<object> scene, bool fitToPage = false, double innerFrameOffsetMm = DefaultPdfInnerFrameOffsetMm, PdfFitMode fitMode = PdfFitMode.Width)
        {
            const double pageOverlapRatio = 0.20;

            if (scene == null || scene.Count == 0)
                return new List<PdfViewportWindow>();

            if (!TryGetSceneBounds(scene, PlotRenderTextTarget, out double sceneMinX, out double sceneMinY, out double sceneMaxX, out double sceneMaxY))
                return new List<PdfViewportWindow>();

            var windows = new List<PdfViewportWindow>();
            bool fitToPageByHeight = fitToPage && fitMode == PdfFitMode.Height;
            if ((fitToPage && !fitToPageByHeight) || (!scaleDenominator.HasValue && !fitToPageByHeight) || _projectData?.Kihon == null || _currentSecoList == null)
            {
                windows.Add(new PdfViewportWindow
                {
                    PageIndex = 0,
                    PageCount = 1,
                    StartSpanIndex = 0,
                    EndSpanIndex = 0,
                    MinX = sceneMinX,
                    MaxX = sceneMaxX,
                    MinY = sceneMinY,
                    MaxY = sceneMaxY
                });
                return windows;
            }

            var k = _projectData.Kihon;
            bool tsuIsX = k.NameX.Any(n => n.Name == _currentSecoList.通を選択);
            bool tsuIsY = k.NameY.Any(n => n.Name == _currentSecoList.通を選択);
            var spans = tsuIsY ? k.ListSpanX.Select(s => ParseMm(s.Span)).ToList()
                               : tsuIsX ? k.ListSpanY.Select(s => ParseMm(s.Span)).ToList()
                                        : new List<double>();

            spans = spans.Where(s => s > 0).ToList();
            if (spans.Count == 0)
            {
                windows.Add(new PdfViewportWindow
                {
                    PageIndex = 0,
                    PageCount = 1,
                    StartSpanIndex = 0,
                    EndSpanIndex = Math.Max(0, spans.Count - 1),
                    MinX = sceneMinX,
                    MaxX = sceneMaxX,
                    MinY = sceneMinY,
                    MaxY = sceneMaxY
                });
                return windows;
            }

            var pageLayout = CreateDefaultPdfPageLayout(paperSize, orientation, innerFrameOffsetMm);
            double pageSliceWidthMm;
            if (fitToPageByHeight)
            {
                double contentWidthMm = Math.Max(1.0, sceneMaxX - sceneMinX);
                double contentHeightMm = Math.Max(1.0, sceneMaxY - sceneMinY);
                var fitHeightLayout = ResolvePdfPageLayout(
                    paperSize,
                    orientation,
                    contentWidthMm,
                    contentHeightMm,
                    scaleDenominator: null,
                    fitMode: PdfFitMode.Height,
                    innerFrameOffsetMm: innerFrameOffsetMm);
                double fitScaleMmPerMm = fitHeightLayout?.ScaleMmPerMm ?? 0.0;
                pageSliceWidthMm = fitScaleMmPerMm > 0
                    ? fitHeightLayout.PrintableWidthMm / fitScaleMmPerMm
                    : contentWidthMm;
            }
            else
            {
                pageSliceWidthMm = pageLayout.PrintableWidthMm * scaleDenominator.Value;
            }

            if (pageSliceWidthMm <= 0 || double.IsNaN(pageSliceWidthMm) || double.IsInfinity(pageSliceWidthMm))
                pageSliceWidthMm = spans.Sum();

            double totalWidth = spans.Sum();
            double contentMinX = sceneMinX;
            double contentMaxX = sceneMaxX;
            double contentWidth = contentMaxX - contentMinX;
            if (contentWidth <= pageSliceWidthMm + 1e-6)
            {
                windows.Add(new PdfViewportWindow
                {
                    PageIndex = 0,
                    PageCount = 1,
                    StartSpanIndex = 0,
                    EndSpanIndex = Math.Max(0, spans.Count - 1),
                    MinX = sceneMinX,
                    MaxX = sceneMaxX,
                    MinY = sceneMinY,
                    MaxY = sceneMaxY
                });
                return windows;
            }

            var positions = new List<double> { -totalWidth / 2.0 };
            for (int i = 0; i < spans.Count; i++)
                positions.Add(positions[i] + spans[i]);

            double fullSpanMinX = positions.First();
            double spanOffsetX = contentMinX - fullSpanMinX;
            var scenePositions = positions.Select(p => p + spanOffsetX).ToList();
            double pageOverlapMm = Math.Max(0.0, Math.Min(pageSliceWidthMm * pageOverlapRatio, pageSliceWidthMm - 1.0));
            double pageAdvanceMm = Math.Max(1.0, pageSliceWidthMm - pageOverlapMm);
            double baseStartX = contentMinX;

            while (baseStartX < contentMaxX - 1e-6)
            {
                double groupMinX = Math.Max(contentMinX, baseStartX);
                double groupMaxX = Math.Min(contentMaxX, baseStartX + pageSliceWidthMm);

                int startSpanIndex = 0;
                for (int i = 0; i < spans.Count; i++)
                {
                    if (scenePositions[i + 1] > groupMinX)
                    {
                        startSpanIndex = i;
                        break;
                    }
                }

                int endSpanIndex = Math.Max(0, spans.Count - 1);
                for (int i = 0; i < spans.Count; i++)
                {
                    bool isLastPage = groupMaxX >= contentMaxX - 1e-6;
                    if ((scenePositions[i] < groupMaxX && groupMaxX <= scenePositions[i + 1]) || (isLastPage && scenePositions[i] < groupMaxX))
                    {
                        endSpanIndex = i;
                        break;
                    }
                }

                windows.Add(new PdfViewportWindow
                {
                    StartSpanIndex = startSpanIndex,
                    EndSpanIndex = endSpanIndex,
                    MinX = groupMinX,
                    MaxX = groupMaxX,
                    MinY = sceneMinY,
                    MaxY = sceneMaxY
                });

                if (groupMaxX >= contentMaxX - 1e-6)
                    break;

                baseStartX += pageAdvanceMm;
            }

            for (int i = 0; i < windows.Count; i++)
            {
                windows[i].PageIndex = i;
                windows[i].PageCount = windows.Count;
                windows[i].IsConnectorPage = false;
                windows[i].ConnectsFromPage = 0;
                windows[i].ConnectsToPage = 0;
            }

            return windows;
        }

        private static Rect PdfRectMmToPreviewPx(double leftMm, double bottomMm, double widthMm, double heightMm, double pageHeightMm, double mmToPx)
        {
            return new Rect(
                leftMm * mmToPx,
                (pageHeightMm - bottomMm - heightMm) * mmToPx,
                widthMm * mmToPx,
                heightMm * mmToPx);
        }

        private static void DrawPdfPlotPreviewFrame(DrawingContext dc, PdfPageLayoutPlan layout, PdfPlotSettings settings, string pageNumber, double mmToPx)
        {
            var outerRect = new Rect(0, 0, layout.PageWidthMm * mmToPx, layout.PageHeightMm * mmToPx);
            var frameRect = PdfRectMmToPreviewPx(layout.FrameLeftMm, layout.FrameBottomMm, layout.FrameWidthMm, layout.FrameHeightMm, layout.PageHeightMm, mmToPx);
            var innerFrameRect = PdfRectMmToPreviewPx(layout.InnerFrameLeftMm, layout.InnerFrameBottomMm, layout.InnerFrameWidthMm, layout.InnerFrameHeightMm, layout.PageHeightMm, mmToPx);
            var titleRect = PdfRectMmToPreviewPx(layout.TitleBlockLeftMm, layout.TitleBlockBottomMm, layout.TitleBlockWidthMm, layout.TitleBlockHeightMm, layout.PageHeightMm, mmToPx);

            //dc.DrawRectangle(null, new Pen(Brushes.Black, 5), outerRect);
            dc.DrawRectangle(null, new Pen(Brushes.Black, 1.0), frameRect);
            //dc.DrawRectangle(null, new Pen(new SolidColorBrush(Color.FromRgb(76, 132, 201)), 0.9), innerFrameRect);
            dc.DrawRectangle(null, new Pen(Brushes.Black, 1.0), titleRect);

            double noWidthMm = 28.0;
            double dateWidthMm = 38.0;
            double scaleWidthMm = 28.0;
            double titleWidthMm = Math.Max(40.0, layout.TitleBlockWidthMm - noWidthMm - dateWidthMm - scaleWidthMm);

            double x1 = titleRect.X + titleWidthMm * mmToPx;
            double x2 = x1 + scaleWidthMm * mmToPx;
            double x3 = x2 + dateWidthMm * mmToPx;
            double yMid = titleRect.Y + Math.Min(titleRect.Height * 0.38, 12.0 * mmToPx);

            var gridPen = new Pen(Brushes.Black, 0.8);
            dc.DrawLine(gridPen, new Point(x1, titleRect.Y), new Point(x1, titleRect.Bottom));
            dc.DrawLine(gridPen, new Point(x2, titleRect.Y), new Point(x2, titleRect.Bottom));
            dc.DrawLine(gridPen, new Point(x3, titleRect.Y), new Point(x3, titleRect.Bottom));
            dc.DrawLine(gridPen, new Point(titleRect.X, yMid), new Point(titleRect.Right, yMid));

            DrawPdfPlotPreviewTitleCell(dc, new Rect(titleRect.X, titleRect.Y, x1 - titleRect.X, titleRect.Height), "TITLE", settings?.TitleText ?? "梁配筋図");
            DrawPdfPlotPreviewTitleCell(dc, new Rect(x1, titleRect.Y, x2 - x1, titleRect.Height), "SCALE", GetPdfScaleDisplayText(settings, layout));
            DrawPdfPlotPreviewTitleCell(dc, new Rect(x2, titleRect.Y, x3 - x2, titleRect.Height), "DATE", settings?.DateText ?? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            DrawPdfPlotPreviewTitleCell(dc, new Rect(x3, titleRect.Y, titleRect.Right - x3, titleRect.Height), "NO.", pageNumber ?? "-");
        }

        private static void DrawPdfPlotPreviewTitleCell(DrawingContext dc, Rect rect, string label, string value)
        {
            if (rect.Width <= 2 || rect.Height <= 2)
                return;

            var labelText = new FormattedText(label ?? string.Empty, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Yu Gothic UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal),
                8.0, Brushes.Black, 1.0);
            var valueText = new FormattedText(value ?? string.Empty, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(new FontFamily("Yu Gothic UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal),
                10.0, Brushes.Black, 1.0)
            {
                Trimming = TextTrimming.CharacterEllipsis,
                MaxTextWidth = Math.Max(4.0, rect.Width - 6.0)
            };

            dc.DrawText(labelText, new Point(rect.X + 3.0, rect.Y + 1.5));
            dc.DrawText(valueText, new Point(rect.X + 3.0, rect.Y + rect.Height * 0.42));
        }

        private PdfPlotSettings ShowPdfExportOptionsDialog(IReadOnlyList<PdfExportSource> sources)
        {
            var optionWindow = new Window
            {
                Owner = this,
                Title = "Thiết lập xuất PDF",
                Width = 520,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize,
                Background = Brushes.White
            };

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            optionWindow.Content = root;

            var title = new TextBlock
            {
                Text = "Xuất file PDF",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(title);

            var paperPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(paperPanel, 1);
            paperPanel.Children.Add(new TextBlock { Text = "Khổ giấy:", Width = 100, VerticalAlignment = VerticalAlignment.Center, FontSize = 14 });
            var paperCombo = new ComboBox { Width = 150, FontSize = 14 };
            paperCombo.Items.Add("A4");
            paperCombo.Items.Add("A3");

            var kesan = _projectData?.Kesan;
            if (kesan?.Printsize2 == true)
            {
                paperCombo.SelectedItem = "A3";
            }
            else
            {
                paperCombo.SelectedItem = "A4";
            }

            paperCombo.SelectionChanged += (s, e) =>
            {
                var selectedPaper = (paperCombo.SelectedItem as string) ?? "A4";
                if (_projectData?.Kesan == null) return;

                _projectData.Kesan.Printsize1 = selectedPaper == "A4";
                _projectData.Kesan.Printsize2 = selectedPaper == "A3";
            };

            paperPanel.Children.Add(paperCombo);
            root.Children.Add(paperPanel);

            var rangeTitle = new TextBlock
            {
                Text = "Phạm vi in (theo vị trí chọn):",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            };
            Grid.SetRow(rangeTitle, 2);
            root.Children.Add(rangeTitle);

            var positionList = new ListBox
            {
                SelectionMode = SelectionMode.Multiple,
                BorderBrush = Brushes.Silver,
                BorderThickness = new Thickness(1),
                FontSize = 13
            };
            foreach (var src in sources)
                positionList.Items.Add(src.Key);
            positionList.SelectAll();
            Grid.SetRow(positionList, 3);
            root.Children.Add(positionList);

            var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(footer, 4);
            root.Children.Add(footer);

            var previewText = new TextBlock
            {
                Text = "Chưa preview",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 10, 0),
                TextWrapping = TextWrapping.Wrap
            };
            footer.Children.Add(previewText);

            var previewButton = new Button { Content = "Review", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(previewButton, 1);
            footer.Children.Add(previewButton);

            var okButton = new Button { Content = "Xuất", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(okButton, 2);
            footer.Children.Add(okButton);

            var cancelButton = new Button { Content = "Hủy", Width = 90, Height = 30 };
            Grid.SetColumn(cancelButton, 3);
            footer.Children.Add(cancelButton);

            PdfPlotSettings result = null;

            previewButton.Click += (s, e) =>
            {
                var selected = positionList.SelectedItems.Cast<string>().ToList();
                var paper = (paperCombo.SelectedItem as string) ?? "A4";
                previewText.Text = $"Khổ: {paper} | Số vị trí sẽ in: {selected.Count}/{sources.Count}";

                var selectedSet = new HashSet<string>(selected, StringComparer.Ordinal);
                var selectedSources = sources
                    .Where(src => selectedSet.Contains(src.Key))
                    .ToList();
                ShowPdfExportReviewWindow(optionWindow, selectedSources, paper);
            };

            okButton.Click += (s, e) =>
            {
                var selected = positionList.SelectedItems.Cast<string>().ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show(optionWindow, "Vui lòng chọn ít nhất 1 vị trí để in.");
                    return;
                }

                result = new PdfPlotSettings
                {
                    PaperSize = ((paperCombo.SelectedItem as string) == "A3") ? PdfPaperSize.A3 : PdfPaperSize.A4,
                    SelectedKeys = selected
                };
                // Keep the export options window open after exporting so the current state stays intact.
            };

            cancelButton.Click += (s, e) =>
            {
                optionWindow.DialogResult = false;
                optionWindow.Close();
            };

            var dialogResult = optionWindow.ShowDialog();
            return dialogResult == true ? result : null;
        }

        private void ShowPdfExportReviewWindow(Window owner, IReadOnlyList<PdfExportSource> selectedSources, string paper)
        {
            // Khung review theo tỷ lệ A4 dọc để dễ quan sát đúng bố cục trang in.
            const double a4FrameWidth = 700;
            const double a4FrameHeight = a4FrameWidth * 297.0 / 210.0;

            var reviewWindow = new Window
            {
                Owner = owner,
                Title = "Review nội dung sẽ xuất PDF",
                Width = 1180,
                Height = 1150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.CanResize,
                Background = Brushes.White
            };

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            //root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            //root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            reviewWindow.Content = root;

            var header = new TextBlock
            {
                Text = "Review bản vẽ sẽ xuất ra PDF",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            root.Children.Add(header);

            //var summary = new TextBlock
            //{
            //    Text = $"Khổ giấy xuất: {paper} | Số bản vẽ sẽ xuất: {selectedSources.Count}",
            //    FontSize = 14,
            //    Foreground = Brushes.Black,
            //    Margin = new Thickness(0, 0, 0, 8)
            //};
            //Grid.SetRow(summary, 1);
            //root.Children.Add(summary);

            UIElement reviewBody;
            if (selectedSources.Count == 0)
            {
                reviewBody = new Border
                {
                    BorderBrush = Brushes.Silver,
                    BorderThickness = new Thickness(1),
                    Padding = new Thickness(16),
                    Child = new TextBlock
                    {
                        Text = "(Chưa chọn bản vẽ nào để xuất)",
                        FontSize = 14,
                        Foreground = Brushes.Black
                    }
                };
            }
            else
            {
                var bodyGrid = new Grid();
                bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
                bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var sourceList = new ListBox
                {
                    BorderBrush = Brushes.Silver,
                    BorderThickness = new Thickness(1),
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 12, 0)
                };
                foreach (var src in selectedSources)
                {
                    sourceList.Items.Add(src.Key);
                }
                sourceList.SelectedIndex = 0;
                bodyGrid.Children.Add(sourceList);

                var previewPanel = new StackPanel();
                Grid.SetColumn(previewPanel, 1);
                bodyGrid.Children.Add(previewPanel);

                var pageTitle = new TextBlock
                {
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 8)
                };
                previewPanel.Children.Add(pageTitle);

                var a4Frame = new Border
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(1.5),
                    Width = a4FrameWidth,
                    Height = a4FrameHeight,
                    Background = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    SnapsToDevicePixels = true
                };
                var previewImage = new Image
                {
                    Stretch = Stretch.Uniform,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    SnapsToDevicePixels = true
                };
                a4Frame.Child = previewImage;
                previewPanel.Children.Add(a4Frame);

                var note = new TextBlock
                {
                    Text = "Khung review theo tỷ lệ A4 để dễ đối chiếu khi xuất PDF.",
                    FontSize = 12,
                    Foreground = Brushes.Black,
                    Margin = new Thickness(0, 8, 0, 0)
                };
                previewPanel.Children.Add(note);

                void UpdatePreview(int index)
                {
                    if (index < 0 || index >= selectedSources.Count)
                    {
                        pageTitle.Text = "";
                        previewImage.Source = null;
                        return;
                    }

                    var src = selectedSources[index];
                    pageTitle.Text = $"Xem trước: {src.Key}";

                    // Đồng bộ với dữ liệu đang dùng khi xuất PDF để đảm bảo review phản ánh nội dung xuất.
                    if (!_sceneByItem.TryGetValue(src.Item, out var scene) || scene == null || scene.Count == 0)
                    {
                        try { Redraw(src.Canvas, src.Item); }
                        catch { }
                    }

                    var imageSource = CreatePdfReviewImageSource(src, paper);
                    previewImage.Source = imageSource;
                }

                sourceList.SelectionChanged += (s, e) => UpdatePreview(sourceList.SelectedIndex);
                UpdatePreview(sourceList.SelectedIndex);

                reviewBody = bodyGrid;
            }

            Grid.SetRow(reviewBody, 2);
            root.Children.Add(reviewBody);

            //var closeButton = new Button
            //{
            //    Content = "Đóng",
            //    Width = 90,
            //    Height = 30,
            //    HorizontalAlignment = HorizontalAlignment.Right,
            //    Margin = new Thickness(0, 10, 0, 0)
            //};
            //closeButton.Click += (_, __) => reviewWindow.Close();
            //Grid.SetRow(closeButton, 3);
            //root.Children.Add(closeButton);

            reviewWindow.ShowDialog();
        }

        private ImageSource CreatePdfReviewImageSource(PdfExportSource src, string paper)
        {
            if (src == null)
            {
                return null;
            }

            var canvasImage = CreateCanvasPreviewImageSource(src.Canvas);
            if (canvasImage == null)
                return null;

            const double mmToPx = 96.0 / 25.4;

            var paperSize = string.Equals(paper, "A3", StringComparison.OrdinalIgnoreCase) ? PdfPaperSize.A3 : PdfPaperSize.A4;
            var layout = CreateDefaultPdfPageLayout(paperSize, PdfPaperOrientation.Landscape, DefaultPdfInnerFrameOffsetMm);
            int pixelWidth = Math.Max(1, (int)Math.Round(layout.PageWidthMm * mmToPx));
            int pixelHeight = Math.Max(1, (int)Math.Round(layout.PageHeightMm * mmToPx));

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pixelWidth, pixelHeight));
                DrawPdfPlotPreviewFrame(dc, layout, null, "-", mmToPx);
                var contentRect = PdfRectMmToPreviewPx(
                    layout.ContentLeftMm,
                    layout.ContentBottomMm,
                    layout.ContentWidthMm,
                    layout.ContentHeightMm,
                    layout.PageHeightMm,
                    mmToPx);
                double sourceWidth = canvasImage.Width;
                double sourceHeight = canvasImage.Height;
                if (sourceWidth > 1 && sourceHeight > 1)
                {
                    double fitScale = Math.Min(contentRect.Width / sourceWidth, contentRect.Height / sourceHeight);
                    if (double.IsNaN(fitScale) || double.IsInfinity(fitScale) || fitScale <= 0)
                        fitScale = 1.0;

                    double drawWidth = sourceWidth * fitScale;
                    double drawHeight = sourceHeight * fitScale;
                    var imageRect = new Rect(
                        contentRect.X + (contentRect.Width - drawWidth) / 2.0,
                        contentRect.Y + (contentRect.Height - drawHeight) / 2.0,
                        drawWidth,
                        drawHeight);

                    dc.PushClip(new RectangleGeometry(contentRect));
                    dc.DrawImage(canvasImage, imageRect);
                    dc.Pop();
                }
            }

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
        }

        private PdfPlotSettings ShowPdfPlotDialog(IReadOnlyList<PdfExportSource> sources)
        {
            var optionWindow = new Window
            {
                Owner = this,
                Title = "Print / Plot PDF",
                Width = 1180,
                Height = 1000,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                ResizeMode = ResizeMode.NoResize, // 👈 KHÓA
                Background = Brushes.White
            };

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            optionWindow.Content = root;

            var title = new TextBlock
            {
                Text = "Print / Plot PDF",
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            root.Children.Add(title);

            var controlPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
            Grid.SetRow(controlPanel, 1);

            controlPanel.Children.Add(new TextBlock
            {
                Text = "Khổ giấy:",
                Width = 80,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            });

            var paperCombo = new ComboBox { Width = 120, FontSize = 14, Margin = new Thickness(0, 0, 20, 0) };
            paperCombo.Items.Add("A4");
            paperCombo.Items.Add("A3");
            paperCombo.SelectedItem = _projectData?.Kesan?.Printsize2 == true ? "A3" : "A4";
            controlPanel.Children.Add(paperCombo);
            controlPanel.Children.Add(new TextBlock
            {
                Text = "Hướng:",
                Width = 65,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            });

            var orientationCombo = new ComboBox { Width = 110, FontSize = 14, Margin = new Thickness(0, 0, 20, 0) };
            orientationCombo.Items.Add("Ngang");
            orientationCombo.Items.Add("Dọc");
            orientationCombo.SelectedItem = "Ngang";
            controlPanel.Children.Add(orientationCombo);

            controlPanel.Children.Add(new TextBlock
            {
                Text = "Tỉ lệ:",
                Width = 50,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            });

            var scaleCombo = new ComboBox { Width = 150, FontSize = 14 };
            scaleCombo.Items.Add("Custom...");
            scaleCombo.Items.Add("Fit to page (Width)");
            scaleCombo.Items.Add("Fit to page (Height)");
            scaleCombo.Items.Add("1:1");
            scaleCombo.Items.Add("1:2");
            scaleCombo.Items.Add("1:5");
            scaleCombo.Items.Add("1:10");
            scaleCombo.Items.Add("1:20");
            scaleCombo.Items.Add("1:25");
            scaleCombo.Items.Add("1:50");
            scaleCombo.Items.Add("1:100");
            scaleCombo.Items.Add("1:200");
            scaleCombo.SelectedItem = "Fit to page (Width)";
            controlPanel.Children.Add(scaleCombo);

            controlPanel.Children.Add(new TextBlock
            {
                Text = "Horizontal:",
                Width = 78,
                Margin = new Thickness(20, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            });

            var horizontalCombo = new ComboBox { Width = 95, FontSize = 14, Margin = new Thickness(0, 0, 20, 0) };
            horizontalCombo.Items.Add("Phải");
            horizontalCombo.Items.Add("Giữa");
            horizontalCombo.Items.Add("Trái");
            horizontalCombo.SelectedItem = "Giữa";
            controlPanel.Children.Add(horizontalCombo);

            controlPanel.Children.Add(new TextBlock
            {
                Text = "Vertical:",
                Width = 62,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            });

            var verticalCombo = new ComboBox { Width = 95, FontSize = 14 };
            verticalCombo.Items.Add("Trên");
            verticalCombo.Items.Add("Giữa");
            verticalCombo.Items.Add("Dưới");
            verticalCombo.SelectedItem = "Giữa";
            controlPanel.Children.Add(verticalCombo);

            controlPanel.Children.Add(new TextBlock
            {
                Text = "Offset:",
                Width = 58,
                Margin = new Thickness(16, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                FontSize = 14
            });

            var offsetFrameBox = new TextBox
            {
                Width = 30,
                FontSize = 14,
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Text = DefaultPdfInnerFrameOffsetMm.ToString("0.##", CultureInfo.InvariantCulture), 
            };
            controlPanel.Children.Add(offsetFrameBox);

            controlPanel.Children.Add(new TextBlock
            {
                Text = "mm",
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14
            });

            var customScalePrefix = new TextBlock
            {
                Text = "1:",
                Margin = new Thickness(12, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 14,
                Visibility = System.Windows.Visibility.Collapsed
            };
            controlPanel.Children.Add(customScalePrefix);

            var customScaleBox = new TextBox
            {
                Width = 90,
                FontSize = 14,
                VerticalContentAlignment = VerticalAlignment.Center,
                Visibility = System.Windows.Visibility.Collapsed,
                ToolTip = "Nhập mẫu số tỉ lệ"
            };
            controlPanel.Children.Add(customScaleBox);

            root.Children.Add(controlPanel);

            var bodyGrid = new Grid();
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280) });
            bodyGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(bodyGrid, 2);
            root.Children.Add(bodyGrid);

            var positionPanel = new Grid();
            positionPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            positionPanel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            bodyGrid.Children.Add(positionPanel);

            positionPanel.Children.Add(new TextBlock
            {
                Text = "Phạm vi in (theo vị trí chọn):",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 6)
            });

            var positionList = new ListBox
            {
                SelectionMode = SelectionMode.Extended,
                BorderBrush = Brushes.Silver,
                BorderThickness = new Thickness(1),
                FontSize = 13
            };
            foreach (var src in sources)
                positionList.Items.Add(src.Key);
            positionList.SelectAll();
            positionList.SelectedIndex = positionList.Items.Count > 0 ? 0 : -1;
            Grid.SetRow(positionList, 1);
            positionPanel.Children.Add(positionList);

            var previewPanel = new Grid { Margin = new Thickness(16, 0, 0, 0) };
            previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            previewPanel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Grid.SetColumn(previewPanel, 1);
            bodyGrid.Children.Add(previewPanel);

            var previewTitle = new TextBlock
            {
                Text = "Preview",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 8)
            };
            previewPanel.Children.Add(previewTitle);

            const double previewHostWidth = 810.0;
            const double previewHostHeight = 740.0;
            const double previewHostPadding = 16.0;

            var previewHost = new Border
            {
                BorderBrush = Brushes.Silver,
                BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(245, 247, 249)),
                Width = previewHostWidth,
                Height = previewHostHeight,
                Padding = new Thickness(previewHostPadding),
                HorizontalAlignment = HorizontalAlignment.Left
            };
            Grid.SetRow(previewHost, 1);
            previewPanel.Children.Add(previewHost);

            var previewCanvas = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            previewHost.Child = previewCanvas;

            var previewPageFrame = new Border
            {
                Background = Brushes.White,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(1),
                Margin = new Thickness(2),
                UseLayoutRounding = true,
                SnapsToDevicePixels = true
            };
            var previewImage = new Image
            {
                Stretch = Stretch.Fill,
                SnapsToDevicePixels = true
            };
            previewPageFrame.Child = previewImage;
            previewCanvas.Children.Add(previewPageFrame);
            var previewNavPanel = new Grid
            {
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Visibility = System.Windows.Visibility.Collapsed
            };
            previewNavPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            previewNavPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            previewNavPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(previewNavPanel, 2);
            previewPanel.Children.Add(previewNavPanel);

            var prevPageButton = new Button
            {
                Content = "<",
                Width = 34,
                Height = 28,
                Margin = new Thickness(0, 0, 8, 0),
                IsEnabled = false
            };
            previewNavPanel.Children.Add(prevPageButton);

            var previewPageText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetColumn(previewPageText, 1);
            previewNavPanel.Children.Add(previewPageText);

            var nextPageButton = new Button
            {
                Content = ">",
                Width = 34,
                Height = 28,
                IsEnabled = false
            };
            Grid.SetColumn(nextPageButton, 2);
            previewNavPanel.Children.Add(nextPageButton);

            var previewInfo = new TextBlock
            {
                Text = "Chưa có preview",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0)
            };
            Grid.SetRow(previewInfo, 3);
            previewPanel.Children.Add(previewInfo);

            var footer = new Grid { Margin = new Thickness(0, 12, 0, 0) };
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            var summaryText = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brushes.Black,
                Margin = new Thickness(0, 0, 10, 0),
                TextWrapping = TextWrapping.Wrap
            };
            footer.Children.Add(summaryText);

            var exportButton = new Button { Content = "Xuất", Width = 90, Height = 30, Margin = new Thickness(0, 0, 8, 0) };
            Grid.SetColumn(exportButton, 1);
            footer.Children.Add(exportButton);

            var cancelButton = new Button { Content = "Hủy", Width = 90, Height = 30 };
            Grid.SetColumn(cancelButton, 2);
            footer.Children.Add(cancelButton);

            PdfPlotSettings result = null;
            int currentPreviewPageIndex = 0;
            string previousScaleSelection = (scaleCombo.SelectedItem as string) ?? "Fit to page (Width)";
            bool scaleSelectionChangedWhileOpen = false;

            bool IsAllowedNonNegativeNumberCandidate(string text)
            {
                if (text == null)
                    return false;

                text = text.Trim();
                if (text.IndexOf('-') >= 0)
                    return false;

                bool hasDecimalSeparator = false;
                foreach (char ch in text)
                {
                    if (char.IsDigit(ch))
                        continue;

                    if (ch == '.')
                    {
                        if (hasDecimalSeparator)
                            return false;

                        hasDecimalSeparator = true;
                        continue;
                    }

                    return false;
                }

                return true;
            }

            string BuildCandidateText(TextBox box, string incomingText)
            {
                var currentText = box.Text ?? string.Empty;
                int selectionStart = Math.Max(0, box.SelectionStart);
                int selectionLength = Math.Max(0, box.SelectionLength);
                if (selectionStart > currentText.Length)
                    selectionStart = currentText.Length;
                if (selectionStart + selectionLength > currentText.Length)
                    selectionLength = currentText.Length - selectionStart;

                return currentText.Remove(selectionStart, selectionLength).Insert(selectionStart, incomingText ?? string.Empty);
            }

            void SetTextBoxValidationState(TextBox box, bool isValid, Brush normalBorderBrush, Thickness normalBorderThickness)
            {
                if (box == null)
                    return;

                var effectiveNormalBrush = normalBorderBrush ?? Brushes.Gray;
                var effectiveNormalThickness = new Thickness(
                    Math.Max(1.0, normalBorderThickness.Left),
                    Math.Max(1.0, normalBorderThickness.Top),
                    Math.Max(1.0, normalBorderThickness.Right),
                    Math.Max(1.0, normalBorderThickness.Bottom));

                box.BorderBrush = isValid ? effectiveNormalBrush : Brushes.Red;
                box.BorderThickness = isValid
                    ? effectiveNormalThickness
                    : new Thickness(
                        Math.Max(1.0, effectiveNormalThickness.Left),
                        Math.Max(1.0, effectiveNormalThickness.Top),
                        Math.Max(1.0, effectiveNormalThickness.Right),
                        Math.Max(1.0, effectiveNormalThickness.Bottom));
            }

            void AttachNonNegativeNumberInputGuard(TextBox box, Brush normalBorderBrush, Thickness normalBorderThickness)
            {
                if (box == null)
                    return;

                box.PreviewTextInput += (_, e) =>
                {
                    var candidateText = BuildCandidateText(box, e.Text);
                    bool isAllowed = IsAllowedNonNegativeNumberCandidate(candidateText);
                    if (!isAllowed)
                    {
                        SetTextBoxValidationState(box, false, normalBorderBrush, normalBorderThickness);
                        e.Handled = true;
                        return;
                    }

                    SetTextBoxValidationState(box, true, normalBorderBrush, normalBorderThickness);
                };

                box.PreviewKeyDown += (_, e) =>
                {
                    if (e.Key == Key.Space || e.Key == Key.Subtract || e.Key == Key.OemMinus)
                    {
                        SetTextBoxValidationState(box, false, normalBorderBrush, normalBorderThickness);
                        e.Handled = true;
                    }
                };

                DataObject.AddPastingHandler(box, new DataObjectPastingEventHandler((_, e) =>
                {
                    if (!e.SourceDataObject.GetDataPresent(DataFormats.Text))
                    {
                        SetTextBoxValidationState(box, false, normalBorderBrush, normalBorderThickness);
                        e.CancelCommand();
                        return;
                    }

                    var pastedText = e.SourceDataObject.GetData(DataFormats.Text) as string ?? string.Empty;
                    var candidateText = BuildCandidateText(box, pastedText);
                    if (!IsAllowedNonNegativeNumberCandidate(candidateText))
                    {
                        SetTextBoxValidationState(box, false, normalBorderBrush, normalBorderThickness);
                        e.CancelCommand();
                        return;
                    }

                    SetTextBoxValidationState(box, true, normalBorderBrush, normalBorderThickness);
                }));

                box.TextChanged += (_, __) =>
                {
                    if (IsAllowedNonNegativeNumberCandidate(box.Text ?? string.Empty))
                        SetTextBoxValidationState(box, true, normalBorderBrush, normalBorderThickness);
                };

                box.Loaded += (_, __) => SetTextBoxValidationState(box, true, normalBorderBrush, normalBorderThickness);
                box.GotKeyboardFocus += (_, __) => SetTextBoxValidationState(box, true, normalBorderBrush, normalBorderThickness);
            }

            bool IsCustomScaleSelected()
                => string.Equals(scaleCombo.SelectedItem as string, "Custom...", StringComparison.OrdinalIgnoreCase);

            string GetSuggestedCustomScaleText()
            {
                if (IsCustomScaleSelected() && !string.IsNullOrWhiteSpace(customScaleBox.Text))
                    return customScaleBox.Text.Trim();

                var selectedScaleText = IsCustomScaleSelected()
                    ? previousScaleSelection
                    : ((scaleCombo.SelectedItem as string) ?? previousScaleSelection ?? "Fit to page (Width)");

                if (!string.IsNullOrWhiteSpace(selectedScaleText) &&
                    selectedScaleText.StartsWith("1:", StringComparison.OrdinalIgnoreCase) &&
                    double.TryParse(selectedScaleText.Substring(2).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var presetScale) &&
                    presetScale > 0)
                {
                    return presetScale.ToString("0.##", CultureInfo.InvariantCulture);
                }

                if (string.Equals(selectedScaleText, "Fit to page (Width)", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(selectedScaleText, "Fit to page (Height)", StringComparison.OrdinalIgnoreCase))
                {
                    var currentKey = positionList.SelectedItem as string;
                    if (string.IsNullOrWhiteSpace(currentKey))
                        currentKey = positionList.SelectedItems.Cast<string>().FirstOrDefault() ?? sources.FirstOrDefault()?.Key;

                    var currentSource = sources.FirstOrDefault(src => string.Equals(src.Key, currentKey, StringComparison.Ordinal));
                    if (currentSource != null)
                    {
                        try
                        {
                            var currentScene = CaptureSceneForPdfExport(currentSource.Item, currentSource.Key);
                            if (currentScene != null &&
                                TryGetSceneBounds(currentScene, PlotRenderTextTarget, out double minX, out double minY, out double maxX, out double maxY))
                            {
                                var fitLayout = ResolvePdfPageLayout(
                                    ((paperCombo.SelectedItem as string) == "A3") ? PdfPaperSize.A3 : PdfPaperSize.A4,
                                    ((orientationCombo.SelectedItem as string) == "Dọc") ? PdfPaperOrientation.Portrait : PdfPaperOrientation.Landscape,
                                    maxX - minX,
                                    maxY - minY,
                                    scaleDenominator: null,
                                    fitMode: GetSelectedFitMode(),
                                    ((horizontalCombo.SelectedItem as string) == "Trái") ? PdfHorizontalAlignment.Left : ((horizontalCombo.SelectedItem as string) == "Phải") ? PdfHorizontalAlignment.Right : PdfHorizontalAlignment.Center,
                                    ((verticalCombo.SelectedItem as string) == "Trên") ? PdfVerticalAlignment.Top : ((verticalCombo.SelectedItem as string) == "Dưới") ? PdfVerticalAlignment.Bottom : PdfVerticalAlignment.Middle,
                                    TryGetInnerFrameOffsetMm(out var offsetMm) ? offsetMm : DefaultPdfInnerFrameOffsetMm);

                                if (fitLayout?.ScaleMmPerMm > 0)
                                {
                                    double fitDenominator = 1.0 / fitLayout.ScaleMmPerMm;
                                    if (!double.IsNaN(fitDenominator) && !double.IsInfinity(fitDenominator) && fitDenominator > 0)
                                        return fitDenominator.ToString("0.##", CultureInfo.InvariantCulture);
                                }
                            }
                        }
                        catch
                        {
                        }
                    }
                }

                return string.IsNullOrWhiteSpace(customScaleBox.Text)
                    ? string.Empty
                    : customScaleBox.Text.Trim();
            }

            bool ShowCustomScaleInputDialog(out double customScale)
            {
                customScale = 0;
                double committedScale = 0;

                var dialog = new Window
                {
                    Owner = optionWindow,
                    Title = "Nhập tỉ lệ custom",
                    Width = 200,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    ResizeMode = ResizeMode.NoResize,
                    Background = Brushes.White
                };

                var panel = new Grid { Margin = new Thickness(16) };
                panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                dialog.Content = panel;

                panel.Children.Add(new TextBlock
                {
                    Text = "Nhập mẫu số tỉ lệ",
                    FontSize = 14,
                    Margin = new Thickness(0, 0, 0, 10),
                    HorizontalAlignment = HorizontalAlignment.Center
                });

                var inputPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
                Grid.SetRow(inputPanel, 1);
                inputPanel.Children.Add(new TextBlock
                {
                    Text = "1:",
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 16,
                    Margin = new Thickness(0, 0, 6, 0)
                });

                var inputBox = new TextBox
                {
                    Width = 50,
                    FontSize = 16,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Text = GetSuggestedCustomScaleText()
                };
                var inputBoxNormalBorderBrush = inputBox.BorderBrush;
                var inputBoxNormalBorderThickness = inputBox.BorderThickness;

                bool TryGetPopupScaleValue(out double parsedScale)
                {
                    return double.TryParse((inputBox.Text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out parsedScale)
                        && parsedScale > 0;
                }

                void RefreshPopupScaleValidationState()
                {
                    var rawText = (inputBox.Text ?? string.Empty).Trim();
                    bool isValid = string.IsNullOrWhiteSpace(rawText) || TryGetPopupScaleValue(out _);
                    SetTextBoxValidationState(inputBox, isValid, inputBoxNormalBorderBrush, inputBoxNormalBorderThickness);
                }

                AttachNonNegativeNumberInputGuard(inputBox, inputBoxNormalBorderBrush, inputBoxNormalBorderThickness);
                inputBox.TextChanged += (_, __) => RefreshPopupScaleValidationState();
                inputPanel.Children.Add(inputBox);
                panel.Children.Add(inputPanel);

                var footerPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 12, 0, 0)
                };
                Grid.SetRow(footerPanel, 2);

                var okButton = new Button { Content = "OK", Width = 50, Height = 28, Margin = new Thickness(0, 0, 8, 0) };
                var cancelPopupButton = new Button { Content = "Hủy", Width = 50, Height = 28 };
                footerPanel.Children.Add(okButton);
                footerPanel.Children.Add(cancelPopupButton);
                panel.Children.Add(footerPanel);

                bool committed = false;
                void TryCommitAndClose()
                {
                    if (!TryGetPopupScaleValue(out var parsedScale))
                    {
                        RefreshPopupScaleValidationState();
                        inputBox.Focus();
                        inputBox.SelectAll();
                        return;
                    }

                    committedScale = parsedScale;
                    committed = true;
                    dialog.DialogResult = true;
                    dialog.Close();
                }

                okButton.Click += (_, __) => TryCommitAndClose();
                cancelPopupButton.Click += (_, __) =>
                {
                    dialog.DialogResult = false;
                    dialog.Close();
                };
                inputBox.KeyDown += (_, e) =>
                {
                    if (e.Key != Key.Enter)
                        return;

                    TryCommitAndClose();
                    e.Handled = true;
                };

                dialog.Loaded += (_, __) =>
                {
                    RefreshPopupScaleValidationState();
                    inputBox.Focus();
                    inputBox.SelectAll();
                };

                dialog.ShowDialog();
                if (committed)
                    customScale = committedScale;
                return committed;
            }

            bool TryGetSelectedScaleDenominator(out double? scaleDenominator)
            {
                scaleDenominator = null;
                var selectedScale = (scaleCombo.SelectedItem as string) ?? "Fit to page (Width)";

                if (string.Equals(selectedScale, "Fit to page (Width)", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(selectedScale, "Fit to page (Height)", StringComparison.OrdinalIgnoreCase))
                    return true;

                if (string.Equals(selectedScale, "Custom...", StringComparison.OrdinalIgnoreCase))
                {
                    if (double.TryParse((customScaleBox.Text ?? string.Empty).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var customScale) &&
                        customScale > 0)
                    {
                        scaleDenominator = customScale;
                        return true;
                    }

                    return false;
                }

                if (selectedScale.StartsWith("1:", StringComparison.OrdinalIgnoreCase) &&
                    double.TryParse(selectedScale.Substring(2), NumberStyles.Float, CultureInfo.InvariantCulture, out var scaleValue) &&
                    scaleValue > 0)
                {
                    scaleDenominator = scaleValue;
                    return true;
                }

                return false;
            }

            PdfFitMode GetSelectedFitMode()
            {
                var selectedScale = (scaleCombo.SelectedItem as string) ?? "Fit to page (Width)";
                return string.Equals(selectedScale, "Fit to page (Height)", StringComparison.OrdinalIgnoreCase)
                    ? PdfFitMode.Height
                    : PdfFitMode.Width;
            }

            bool TryGetInnerFrameOffsetMm(out double innerFrameOffsetMm)
            {
                innerFrameOffsetMm = DefaultPdfInnerFrameOffsetMm;
                var rawText = (offsetFrameBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(rawText))
                    return true;

                return double.TryParse(rawText, NumberStyles.Float, CultureInfo.InvariantCulture, out innerFrameOffsetMm)
                    && innerFrameOffsetMm >= 0;
            }

            var offsetFrameNormalBorderBrush = offsetFrameBox.BorderBrush;
            var offsetFrameNormalBorderThickness = offsetFrameBox.BorderThickness;

            void RefreshOffsetFrameValidationState()
            {
                bool isValid = TryGetInnerFrameOffsetMm(out _);
                SetTextBoxValidationState(offsetFrameBox, isValid, offsetFrameNormalBorderBrush, offsetFrameNormalBorderThickness);
            }

            PdfPlotSettings BuildCurrentSettings()
            {
                TryGetSelectedScaleDenominator(out var scaleDenominator);
                TryGetInnerFrameOffsetMm(out var innerFrameOffsetMm);
                string floor = _currentSecoList?.階を選択 ?? string.Empty;
                string axis = _currentSecoList?.通を選択 ?? string.Empty;
                string titleText = string.Join(" ", new[] { floor, axis, "梁配筋図" }.Where(s => !string.IsNullOrWhiteSpace(s)));
                return new PdfPlotSettings
                {
                    PaperSize = ((paperCombo.SelectedItem as string) == "A3") ? PdfPaperSize.A3 : PdfPaperSize.A4,
                    Orientation = ((orientationCombo.SelectedItem as string) == "Dọc") ? PdfPaperOrientation.Portrait : PdfPaperOrientation.Landscape,
                    ScaleDenominator = scaleDenominator,
                    FitMode = GetSelectedFitMode(),
                    HorizontalAlignment = ((horizontalCombo.SelectedItem as string) == "Trái") ? PdfHorizontalAlignment.Left : ((horizontalCombo.SelectedItem as string) == "Phải") ? PdfHorizontalAlignment.Right : PdfHorizontalAlignment.Center,
                    VerticalAlignment = ((verticalCombo.SelectedItem as string) == "Trên") ? PdfVerticalAlignment.Top : ((verticalCombo.SelectedItem as string) == "Dưới") ? PdfVerticalAlignment.Bottom : PdfVerticalAlignment.Middle,
                    InnerFrameOffsetMm = innerFrameOffsetMm,
                    TitleText = string.IsNullOrWhiteSpace(titleText) ? "梁配筋図" : titleText,
                    DateText = DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    SelectedKeys = positionList.SelectedItems.Cast<string>().ToList()
                };
            }

            void UpdatePreview()
            {
                if (_projectData?.Kesan != null)
                {
                    var selectedPaper = (paperCombo.SelectedItem as string) ?? "A4";
                    _projectData.Kesan.Printsize1 = selectedPaper == "A4";
                    _projectData.Kesan.Printsize2 = selectedPaper == "A3";
                }

                customScalePrefix.Visibility = System.Windows.Visibility.Collapsed;
                customScaleBox.Visibility = System.Windows.Visibility.Collapsed;
                RefreshOffsetFrameValidationState();

                var currentSettings = BuildCurrentSettings();
                bool hasValidScale = TryGetSelectedScaleDenominator(out var validatedScale);
                bool hasValidOffset = TryGetInnerFrameOffsetMm(out var validatedOffset);
                currentSettings.ScaleDenominator = validatedScale;
                currentSettings.InnerFrameOffsetMm = validatedOffset;
                exportButton.IsEnabled = hasValidScale && hasValidOffset;
                summaryText.Text = $"Khổ {GetPdfPaperDisplayText(currentSettings.PaperSize)} {GetPdfOrientationDisplayText(currentSettings.Orientation)} | Scale {GetPdfScaleDisplayText(currentSettings)} | H: {GetPdfHorizontalAlignmentDisplayText(currentSettings.HorizontalAlignment)} | V: {GetPdfVerticalAlignmentDisplayText(currentSettings.VerticalAlignment)} | Offset: {currentSettings.InnerFrameOffsetMm:0.##} mm | Chọn {currentSettings.SelectedKeys.Count}/{sources.Count} vị trí";
                var currentKey = positionList.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(currentKey))
                    currentKey = currentSettings.SelectedKeys.FirstOrDefault();

                var currentSource = sources.FirstOrDefault(src => string.Equals(src.Key, currentKey, StringComparison.Ordinal));
                if (currentSource == null)
                {
                    previewTitle.Text = "Preview";
                    previewImage.Source = null;
                    previewNavPanel.Visibility = System.Windows.Visibility.Collapsed;
                    previewPageText.Text = string.Empty;
                    prevPageButton.IsEnabled = false;
                    nextPageButton.IsEnabled = false;
                    previewInfo.Text = "Chọn một vị trí để xem preview.";
                    return;
                }

                if (!hasValidOffset)
                {
                    previewTitle.Text = $"Preview: {currentSource.Key}";
                    previewImage.Source = null;
                    previewNavPanel.Visibility = System.Windows.Visibility.Collapsed;
                    previewPageText.Text = string.Empty;
                    prevPageButton.IsEnabled = false;
                    nextPageButton.IsEnabled = false;
                    previewInfo.Text = "Offset chưa hợp lệ. Nhập số mm >= 0, ví dụ 5.";
                    return;
                }

                if (!hasValidScale)
                {
                    previewTitle.Text = $"Preview: {currentSource.Key}";
                    previewImage.Source = null;
                    previewNavPanel.Visibility = System.Windows.Visibility.Collapsed;
                    previewPageText.Text = string.Empty;
                    prevPageButton.IsEnabled = false;
                    nextPageButton.IsEnabled = false;
                    previewInfo.Text = "Scale custom chưa hợp lệ. Nhập mẫu số dương, ví dụ 75 cho 1:75.";
                    return;
                }

                IReadOnlyList<object> currentScene = null;
                try
                {
                    currentScene = CaptureSceneForPdfExport(currentSource.Item, currentSource.Key);
                }
                catch { }

                var viewportWindows = BuildPdfViewportWindows(currentSettings.PaperSize, currentSettings.Orientation, currentSettings.ScaleDenominator, currentScene, currentSettings.FitToPage, currentSettings.InnerFrameOffsetMm, currentSettings.FitMode).ToList();
                if (viewportWindows.Count == 0)
                {
                    viewportWindows.Add(new PdfViewportWindow
                    {
                        PageIndex = 0,
                        PageCount = 1
                    });
                }

                if (currentPreviewPageIndex >= viewportWindows.Count)
                    currentPreviewPageIndex = viewportWindows.Count - 1;
                if (currentPreviewPageIndex < 0)
                    currentPreviewPageIndex = 0;

                currentSettings.ViewportWindow = viewportWindows[currentPreviewPageIndex];
                previewTitle.Text = $"Preview: {currentSource.Key}";
                var previewSource = CreatePdfPlotPreviewImageSource(currentSource, currentSettings, out var layout);
                previewImage.Source = previewSource;
                previewNavPanel.Visibility = viewportWindows.Count > 1 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
                previewPageText.Text = currentSettings.ViewportWindow?.Label ?? "Trang 1/1";
                prevPageButton.IsEnabled = currentPreviewPageIndex > 0;
                nextPageButton.IsEnabled = currentPreviewPageIndex < viewportWindows.Count - 1;
                summaryText.Text = $"Khổ {GetPdfPaperDisplayText(currentSettings.PaperSize)} {GetPdfOrientationDisplayText(currentSettings.Orientation)} | Scale {GetPdfScaleDisplayText(currentSettings, layout)} | H: {GetPdfHorizontalAlignmentDisplayText(currentSettings.HorizontalAlignment)} | V: {GetPdfVerticalAlignmentDisplayText(currentSettings.VerticalAlignment)} | Offset: {currentSettings.InnerFrameOffsetMm:0.##} mm | Chọn {currentSettings.SelectedKeys.Count}/{sources.Count} vị trí";
                previewInfo.Text = BuildPdfPlotStatusText(currentSettings, layout);

                const double previewFrameSafetyInset = 6.0;
                double maxFrameWidth = previewHostWidth - (previewHostPadding * 2.0) - previewFrameSafetyInset;
                double maxFrameHeight = previewHostHeight - (previewHostPadding * 2.0) - previewFrameSafetyInset;
                double previewScale = Math.Min(maxFrameWidth / Math.Max(1.0, layout.PageWidthMm),
                                               maxFrameHeight / Math.Max(1.0, layout.PageHeightMm));
                if (double.IsNaN(previewScale) || double.IsInfinity(previewScale) || previewScale <= 0)
                    previewScale = 1.0;

                previewPageFrame.Width = layout.PageWidthMm * previewScale;
                previewPageFrame.Height = layout.PageHeightMm * previewScale;
            }

            void CommitOffsetFrameInput(bool moveFocusAway = false)
            {
                if (TryGetInnerFrameOffsetMm(out var committedOffset))
                {
                    string normalizedText = committedOffset.ToString("0.##", CultureInfo.InvariantCulture);
                    if (!string.Equals(offsetFrameBox.Text, normalizedText, StringComparison.Ordinal))
                        offsetFrameBox.Text = normalizedText;
                }

                RefreshOffsetFrameValidationState();

                UpdatePreview();

                if (moveFocusAway)
                    Keyboard.ClearFocus();
            }

            exportButton.Click += (s, e) =>
            {
                var selected = positionList.SelectedItems.Cast<string>().ToList();
                if (selected.Count == 0)
                {
                    MessageBox.Show(optionWindow, "Vui lòng chọn ít nhất 1 vị trí để in.");
                    return;
                }

                result = BuildCurrentSettings();
                if (!TryGetSelectedScaleDenominator(out var validatedScale))
                {
                    MessageBox.Show(optionWindow, "Scale custom chưa hợp lệ. Nhập mẫu số dương, ví dụ 75 cho 1:75.");
                    return;
                }
                if (!TryGetInnerFrameOffsetMm(out var validatedOffset))
                {
                    MessageBox.Show(optionWindow, "Offset khung chưa hợp lệ. Nhập số mm >= 0, ví dụ 5.");
                    return;
                }
                result.ScaleDenominator = validatedScale;
                result.InnerFrameOffsetMm = validatedOffset;

                if (!result.FitToPage)
                {
                    var clippedKeys = new List<string>();
                    foreach (var src in sources.Where(source => selected.Contains(source.Key)))
                    {
                        try
                        {
                            var scene = CaptureSceneForPdfExport(src.Item, src.Key);
                            if (TryGetSceneBounds(scene, PlotRenderTextTarget, out double minX, out double minY, out double maxX, out double maxY))
                            {
                                ApplyPdfAlignmentToVisibleBounds(result, ref minX, ref minY, ref maxX, ref maxY);
                                var layout = ResolvePdfPageLayout(result.PaperSize, result.Orientation, maxX - minX, maxY - minY, result.ScaleDenominator, result.FitMode, result.HorizontalAlignment, result.VerticalAlignment, result.InnerFrameOffsetMm);
                                if (layout.IsClipped)
                                    clippedKeys.Add(src.Key);
                            }
                        }
                        catch
                        {
                            clippedKeys.Add(src.Key);
                        }
                    }

                    if (clippedKeys.Count > 0)
                    {
                        var warning = "Một số bản vẽ không vừa khổ giấy ở scale đã chọn và sẽ bị cắt:\n- "
                                      + string.Join("\n- ", clippedKeys.Take(5))
                                      + (clippedKeys.Count > 5 ? $"\n... và {clippedKeys.Count - 5} bản vẽ khác" : "")
                                      + "\n\nTiếp tục xuất PDF?";
                        if (MessageBox.Show(optionWindow, warning, "Cảnh báo plot", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                            return;
                    }
                }

                var dlg = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    FileName = $"{_currentSecoList.階を選択}_{_currentSecoList.通を選択}.pdf"
                };
                if (dlg.ShowDialog() != true)
                    return;

                try
                {
                    var scenePages = BuildPdfScenePages(sources, result);
                    PdfExporter.Export(
                        dlg.FileName,
                        scenePages,
                        FontFamily?.Source ?? "Yu Mincho",
                        result);

                    MessageBox.Show(optionWindow, "PDF exported!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(optionWindow, $"Xuất PDF thất bại: {ex.Message}");
                }
            };

            cancelButton.Click += (s, e) =>
            {
                optionWindow.DialogResult = false;
                optionWindow.Close();
            };

            prevPageButton.Click += (_, __) =>
            {
                if (currentPreviewPageIndex <= 0)
                    return;

                currentPreviewPageIndex--;
                UpdatePreview();
            };

            nextPageButton.Click += (_, __) =>
            {
                currentPreviewPageIndex++;
                UpdatePreview();
            };

            paperCombo.SelectionChanged += (_, __) =>
            {
                currentPreviewPageIndex = 0;
                UpdatePreview();
            };
            orientationCombo.SelectionChanged += (_, __) =>
            {
                currentPreviewPageIndex = 0;
                UpdatePreview();
            };
            scaleCombo.DropDownOpened += (_, __) => scaleSelectionChangedWhileOpen = false;
            scaleCombo.SelectionChanged += (_, __) =>
            {
                if (scaleCombo.IsDropDownOpen)
                    scaleSelectionChangedWhileOpen = true;

                if (IsCustomScaleSelected())
                    return;

                if (!IsCustomScaleSelected())
                    previousScaleSelection = (scaleCombo.SelectedItem as string) ?? "Fit to page (Width)";

                UpdatePreview();
            };
            scaleCombo.DropDownClosed += (_, __) =>
            {
                if (!IsCustomScaleSelected())
                    return;

                if (ShowCustomScaleInputDialog(out var customScale))
                {
                    customScaleBox.Text = customScale.ToString("0.##", CultureInfo.InvariantCulture);
                }
                else if (scaleSelectionChangedWhileOpen && !string.Equals(previousScaleSelection, "Custom...", StringComparison.OrdinalIgnoreCase))
                {
                    scaleCombo.SelectedItem = previousScaleSelection;
                    return;
                }

                UpdatePreview();
            };
            horizontalCombo.SelectionChanged += (_, __) => UpdatePreview();
            verticalCombo.SelectionChanged += (_, __) => UpdatePreview();
            AttachNonNegativeNumberInputGuard(offsetFrameBox, offsetFrameNormalBorderBrush, offsetFrameNormalBorderThickness);
            offsetFrameBox.TextChanged += (_, __) => RefreshOffsetFrameValidationState();
            offsetFrameBox.KeyDown += (_, e) =>
            {
                if (e.Key != Key.Enter)
                    return;

                CommitOffsetFrameInput(moveFocusAway: true);
                e.Handled = true;
            };
            offsetFrameBox.LostKeyboardFocus += (_, __) => CommitOffsetFrameInput();
            optionWindow.PreviewMouseDown += (_, e) =>
            {
                if (!offsetFrameBox.IsKeyboardFocusWithin)
                    return;

                if (e.OriginalSource is DependencyObject source && !IsDescendantOf(source, offsetFrameBox))
                    Keyboard.ClearFocus();
            };
            positionList.SelectionChanged += (_, __) =>
            {
                currentPreviewPageIndex = 0;
                UpdatePreview();
            };

            UpdatePreview();

            var dialogResult = optionWindow.ShowDialog();
            return dialogResult == true ? result : null;
        }

        private ImageSource CreatePdfPlotPreviewImageSource(PdfExportSource src, PdfPlotSettings settings, out PdfPageLayoutPlan layout)
        {
            layout = CreateDefaultPdfPageLayout(
                settings?.PaperSize ?? PdfPaperSize.A4,
                settings?.Orientation ?? PdfPaperOrientation.Landscape,
                settings?.InnerFrameOffsetMm ?? DefaultPdfInnerFrameOffsetMm);

            if (src == null || settings == null)
                return null;

            IReadOnlyList<object> scene;
            try
            {
                scene = CaptureSceneForPdfExport(src.Item, src.Key);
            }
            catch
            {
                return CreateCanvasPreviewImageSource(src.Canvas);
            }

            if (scene == null || !TryGetSceneBounds(scene, PlotRenderTextTarget, out double minX, out double minY, out double maxX, out double maxY))
                return CreateCanvasPreviewImageSource(src.Canvas);

            var viewportWindow = settings.ViewportWindow;
            if (viewportWindow != null && viewportWindow.MaxX > viewportWindow.MinX && viewportWindow.MaxY > viewportWindow.MinY)
            {
                minX = viewportWindow.MinX;
                maxX = viewportWindow.MaxX;
                minY = viewportWindow.MinY;
                maxY = viewportWindow.MaxY;
            }

            ApplyPdfAlignmentToVisibleBounds(settings, ref minX, ref minY, ref maxX, ref maxY);

            layout = ResolvePdfPageLayout(settings.PaperSize, settings.Orientation, maxX - minX, maxY - minY, settings.ScaleDenominator, settings.FitMode, settings.HorizontalAlignment, settings.VerticalAlignment, settings.InnerFrameOffsetMm);

            const double mmToPx = 96.0 / 25.4;
            int pixelWidth = Math.Max(1, (int)Math.Round(layout.PageWidthMm * mmToPx));
            int pixelHeight = Math.Max(1, (int)Math.Round(layout.PageHeightMm * mmToPx));

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, pixelWidth, pixelHeight));
                DrawPdfPlotPreviewFrame(dc, layout, settings, settings.ViewportWindow != null ? (settings.ViewportWindow.PageIndex + 1).ToString(CultureInfo.InvariantCulture) : "1", mmToPx);
                var contentRect = PdfRectMmToPreviewPx(
                    layout.ContentLeftMm,
                    layout.ContentBottomMm,
                    layout.ContentWidthMm,
                    layout.ContentHeightMm,
                    layout.PageHeightMm,
                    mmToPx);

                dc.PushClip(new RectangleGeometry(contentRect));
                foreach (var entity in scene)
                {
                    if (entity is SceneLine ln)
                    {
                        var pen = new Pen(new SolidColorBrush(ln.StrokeColor), Math.Max(1.0, ln.Thickness));
                        dc.DrawLine(pen,
                            WorldToPdfPlotPreviewPoint(ln.X1, ln.Y1, minX, maxY, layout.PageHeightMm, layout.ScaleMmPerMm, layout.MarginLeftMm, layout.MarginBottomMm, mmToPx),
                            WorldToPdfPlotPreviewPoint(ln.X2, ln.Y2, minX, maxY, layout.PageHeightMm, layout.ScaleMmPerMm, layout.MarginLeftMm, layout.MarginBottomMm, mmToPx));
                    }
                    else if (entity is DxfCircle c)
                    {
                        var center = WorldToPdfPlotPreviewPoint(c.X, c.Y, minX, maxY, layout.PageHeightMm, layout.ScaleMmPerMm, layout.MarginLeftMm, layout.MarginBottomMm, mmToPx);
                        double rPx = Math.Max(0.5, c.R * layout.ScaleMmPerMm * mmToPx);
                        var strokePen = new Pen(new SolidColorBrush(c.StrokeColor), Math.Max(1.0, c.StrokeThicknessPx));
                        var fillBrush = c.Filled ? new SolidColorBrush(c.FillColor) : null;
                        dc.DrawEllipse(fillBrush, strokePen, center, rPx, rPx);
                    }
                    else if (entity is DxfArc arc)
                    {
                        DrawDxfArcToPdfPlotPreview(dc, arc, minX, maxY, layout.PageHeightMm, layout.ScaleMmPerMm, layout.MarginLeftMm, layout.MarginBottomMm, mmToPx);
                    }
                    else if (entity is DxfSolid solid)
                    {
                        var geo = new StreamGeometry();
                        using (var gctx = geo.Open())
                        {
                            gctx.BeginFigure(WorldToPdfPlotPreviewPoint(solid.X1, solid.Y1, minX, maxY, layout.PageHeightMm, layout.ScaleMmPerMm, layout.MarginLeftMm, layout.MarginBottomMm, mmToPx), true, true);
                            gctx.LineTo(WorldToPdfPlotPreviewPoint(solid.X2, solid.Y2, minX, maxY, layout.PageHeightMm, layout.ScaleMmPerMm, layout.MarginLeftMm, layout.MarginBottomMm, mmToPx), true, false);
                            gctx.LineTo(WorldToPdfPlotPreviewPoint(solid.X3, solid.Y3, minX, maxY, layout.PageHeightMm, layout.ScaleMmPerMm, layout.MarginLeftMm, layout.MarginBottomMm, mmToPx), true, false);
                            gctx.LineTo(WorldToPdfPlotPreviewPoint(solid.X4, solid.Y4, minX, maxY, layout.PageHeightMm, layout.ScaleMmPerMm, layout.MarginLeftMm, layout.MarginBottomMm, mmToPx), true, false);
                        }
                        geo.Freeze();
                        dc.DrawGeometry(new SolidColorBrush(solid.FillColor), null, geo);
                    }
                    else if (entity is DxfText tx)
                    {
                        DrawDxfTextToPdfPlotPreview(dc, tx, minX, maxY, layout.PageHeightMm, layout.ScaleMmPerMm, layout.MarginLeftMm, layout.MarginBottomMm, mmToPx);
                    }
                }
                dc.Pop();
            }

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(visual);
            return rtb;
        }

        private static Point WorldToPdfPlotPreviewPoint(double x, double y, double minX, double maxY, double pageHeightMm,
                                                        double scaleMmToMm, double marginLeftMm, double marginBottomMm, double mmToPx)
        {
            double pageMmX = marginLeftMm + (x - minX) * scaleMmToMm;
            double pageMmY = marginBottomMm + (maxY - y) * scaleMmToMm;
            return new Point(pageMmX * mmToPx, (pageHeightMm - pageMmY) * mmToPx);
        }

        private static void DrawDxfArcToPdfPlotPreview(DrawingContext dc, DxfArc arc,
                                                       double minX, double maxY, double pageHeightMm, double scale, double marginLeftMm, double marginBottomMm, double mmToPx)
        {
            double startRad = arc.StartDeg * Math.PI / 180.0;
            double endRad = arc.EndDeg * Math.PI / 180.0;

            Point p0 = WorldToPdfPlotPreviewPoint(arc.X + arc.R * Math.Cos(startRad), arc.Y + arc.R * Math.Sin(startRad), minX, maxY, pageHeightMm, scale, marginLeftMm, marginBottomMm, mmToPx);
            Point p1 = WorldToPdfPlotPreviewPoint(arc.X + arc.R * Math.Cos(endRad), arc.Y + arc.R * Math.Sin(endRad), minX, maxY, pageHeightMm, scale, marginLeftMm, marginBottomMm, mmToPx);

            double delta = NormalizeDeltaCCW(arc.StartDeg, arc.EndDeg);
            bool isLarge = delta > 180.0;
            double rPx = Math.Max(0.5, arc.R * scale * mmToPx);

            var figure = new PathFigure { StartPoint = p0, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new ArcSegment
            {
                Point = p1,
                Size = new Size(rPx, rPx),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = isLarge
            });

            var geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(arc.StrokeColor), Math.Max(1.0, arc.ThicknessPx)), geometry);
        }

        private static void DrawDxfTextToPdfPlotPreview(DrawingContext dc, DxfText tx,
                                                        double minX, double maxY, double pageHeightMm, double scale, double marginLeftMm, double marginBottomMm, double mmToPx)
        {
            if (string.IsNullOrEmpty(tx.Value)) return;

            var metrics = MeasureDxfTextLayout(tx, PlotRenderTextTarget, scale * mmToPx);
            tx = metrics.Text;
            var ft = new FormattedText(tx.Value,
                                       CultureInfo.CurrentCulture,
                                       FlowDirection.LeftToRight,
                                       metrics.Typeface,
                                       metrics.FontPx,
                                       new SolidColorBrush(tx.Color),
                                       1.0);
            var anchorWorld = GetPlotRenderTextAnchorWorld(tx);
            var topLeftWorld = GetPlotRenderTextTopLeftWorld(metrics);
            var anchor = WorldToPdfPlotPreviewPoint(anchorWorld.X, anchorWorld.Y, minX, maxY, pageHeightMm, scale, marginLeftMm, marginBottomMm, mmToPx);
            Point drawPoint = WorldToPdfPlotPreviewPoint(topLeftWorld.X, topLeftWorld.Y, minX, maxY, pageHeightMm, scale, marginLeftMm, marginBottomMm, mmToPx);
            if (Math.Abs(tx.RotationDeg) > 0.01)
            {
                dc.PushTransform(new RotateTransform(-tx.RotationDeg, anchor.X, anchor.Y));
                dc.DrawText(ft, drawPoint);
                dc.Pop();
                return;
            }

            dc.DrawText(ft, drawPoint);
        }

        private static Point WorldToReviewPoint(double x, double y, double maxX, double minY,
                                                 double scaleMmToMm, double offsetMmX, double offsetMmY, double mmToPx)
        {
            // Map theo hệ trục preview cần hiển thị cùng chiều với canvas review,
            // tránh bị đảo 180 độ khi scene dùng quy ước trục ngược.
            double px = ((maxX - x) * scaleMmToMm + offsetMmX) * mmToPx;
            double py = ((y - minY) * scaleMmToMm + offsetMmY) * mmToPx;
            return new Point(px, py);
        }

        private static bool TryGetSceneBounds(IEnumerable<object> scene, TextOutputTarget textTarget, out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = double.PositiveInfinity;
            minY = double.PositiveInfinity;
            maxX = double.NegativeInfinity;
            maxY = double.NegativeInfinity;

            if (scene == null) return false;

            foreach (var entity in scene)
            {
                if (entity is SceneLine ln)
                {
                    if (ln.X1 < minX) minX = ln.X1;
                    if (ln.Y1 < minY) minY = ln.Y1;
                    if (ln.X1 > maxX) maxX = ln.X1;
                    if (ln.Y1 > maxY) maxY = ln.Y1;

                    if (ln.X2 < minX) minX = ln.X2;
                    if (ln.Y2 < minY) minY = ln.Y2;
                    if (ln.X2 > maxX) maxX = ln.X2;
                    if (ln.Y2 > maxY) maxY = ln.Y2;
                }
                else if (entity is DxfText tx)
                {
                    var textBounds = GetDxfTextBounds(tx, textTarget);
                    if (textBounds.Left < minX) minX = textBounds.Left;
                    if (textBounds.Top < minY) minY = textBounds.Top;
                    if (textBounds.Right > maxX) maxX = textBounds.Right;
                    if (textBounds.Bottom > maxY) maxY = textBounds.Bottom;
                }
                else if (entity is DxfCircle c)
                {
                    double x1 = c.X - c.R;
                    double y1 = c.Y - c.R;
                    double x2 = c.X + c.R;
                    double y2 = c.Y + c.R;

                    if (x1 < minX) minX = x1;
                    if (y1 < minY) minY = y1;
                    if (x1 > maxX) maxX = x1;
                    if (y1 > maxY) maxY = y1;

                    if (x2 < minX) minX = x2;
                    if (y2 < minY) minY = y2;
                    if (x2 > maxX) maxX = x2;
                    if (y2 > maxY) maxY = y2;
                }
                else if (entity is DxfArc arc)
                {
                    double x1 = arc.X - arc.R;
                    double y1 = arc.Y - arc.R;
                    double x2 = arc.X + arc.R;
                    double y2 = arc.Y + arc.R;

                    if (x1 < minX) minX = x1;
                    if (y1 < minY) minY = y1;
                    if (x1 > maxX) maxX = x1;
                    if (y1 > maxY) maxY = y1;

                    if (x2 < minX) minX = x2;
                    if (y2 < minY) minY = y2;
                    if (x2 > maxX) maxX = x2;
                    if (y2 > maxY) maxY = y2;
                }
                else if (entity is DxfSolid solid)
                {
                    if (solid.X1 < minX) minX = solid.X1;
                    if (solid.Y1 < minY) minY = solid.Y1;
                    if (solid.X1 > maxX) maxX = solid.X1;
                    if (solid.Y1 > maxY) maxY = solid.Y1;

                    if (solid.X2 < minX) minX = solid.X2;
                    if (solid.Y2 < minY) minY = solid.Y2;
                    if (solid.X2 > maxX) maxX = solid.X2;
                    if (solid.Y2 > maxY) maxY = solid.Y2;

                    if (solid.X3 < minX) minX = solid.X3;
                    if (solid.Y3 < minY) minY = solid.Y3;
                    if (solid.X3 > maxX) maxX = solid.X3;
                    if (solid.Y3 > maxY) maxY = solid.Y3;

                    if (solid.X4 < minX) minX = solid.X4;
                    if (solid.Y4 < minY) minY = solid.Y4;
                    if (solid.X4 > maxX) maxX = solid.X4;
                    if (solid.Y4 > maxY) maxY = solid.Y4;
                }
            }

            return !(double.IsInfinity(minX) || double.IsInfinity(minY) || double.IsInfinity(maxX) || double.IsInfinity(maxY));
        }

        private static void DrawDxfArcToReview(DrawingContext dc, DxfArc arc,
                                               double maxX, double minY, double scale, double offsetX, double offsetY, double mmToPx)
        {
            double startRad = arc.StartDeg * Math.PI / 180.0;
            double endRad = arc.EndDeg * Math.PI / 180.0;

            Point p0 = WorldToReviewPoint(arc.X + arc.R * Math.Cos(startRad), arc.Y + arc.R * Math.Sin(startRad), maxX, minY, scale, offsetX, offsetY, mmToPx);
            Point p1 = WorldToReviewPoint(arc.X + arc.R * Math.Cos(endRad), arc.Y + arc.R * Math.Sin(endRad), maxX, minY, scale, offsetX, offsetY, mmToPx);

            double delta = NormalizeDeltaCCW(arc.StartDeg, arc.EndDeg);
            bool isLarge = delta > 180.0;
            double rPx = Math.Max(0.5, arc.R * scale * mmToPx);

            var figure = new PathFigure { StartPoint = p0, IsClosed = false, IsFilled = false };
            figure.Segments.Add(new ArcSegment
            {
                Point = p1,
                Size = new Size(rPx, rPx),
                RotationAngle = 0,
                IsLargeArc = isLarge,
                SweepDirection = SweepDirection.Clockwise
            });

            var geometry = new PathGeometry(new[] { figure });
            var pen = new Pen(new SolidColorBrush(arc.StrokeColor), Math.Max(1.0, arc.ThicknessPx));
            dc.DrawGeometry(null, pen, geometry);
        }

        private static void DrawDxfTextToReview(DrawingContext dc, DxfText tx,
                                                double maxX, double minY, double scale, double offsetX, double offsetY, double mmToPx,
                                                TextOutputTarget textTarget)
        {
            if (string.IsNullOrEmpty(tx.Value)) return;

            var metrics = MeasureDxfTextLayout(tx, textTarget, scale * mmToPx);
            tx = metrics.Text;
            var ft = new FormattedText(tx.Value,
                                       CultureInfo.CurrentCulture,
                                       FlowDirection.LeftToRight,
                                       metrics.Typeface,
                                       metrics.FontPx,
                                       new SolidColorBrush(tx.Color),
                                       1.0);

            var anchor = WorldToReviewPoint(tx.X, tx.Y, maxX, minY, scale, offsetX, offsetY, mmToPx);
            Point drawPoint = GetDxfTextTopLeft(anchor, metrics.WidthPx, metrics.HeightPx, tx.HAnchor, tx.VAnchor);

            dc.DrawText(ft, drawPoint);
        }

        private ImageSource CreateCanvasPreviewImageSource(Canvas canvas)
        {
            if (canvas == null)
            {
                return null;
            }

            canvas.UpdateLayout();

            double width = canvas.ActualWidth;
            double height = canvas.ActualHeight;
            if (width <= 1 || height <= 1)
            {
                width = canvas.Width > 1 ? canvas.Width : 1200;
                height = canvas.Height > 1 ? canvas.Height : 320;
            }

            int pixelWidth = Math.Max(1, (int)Math.Ceiling(width));
            int pixelHeight = Math.Max(1, (int)Math.Ceiling(height));

            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(canvas);
            return rtb;
        }

        private void ExportItemDxf_Click(object sender, RoutedEventArgs e)
        {
            var fe = sender as FrameworkElement;
            var item = fe != null ? fe.DataContext as GridBotsecozu : null;
            if (item == null) return;

            var (lines, texts, circles, arcs, solids, key) = BuildDxfGeometry(item);
            var dlg = new SaveFileDialog
            {
                Filter = "AutoCAD DXF (*.dxf)|*.dxf",
                FileName = $"{key}.dxf"
            };
            if (dlg.ShowDialog() == true)
            {
                WriteSimpleDxf(dlg.FileName, lines, texts, circles, arcs, solids, flipY: true);
                MessageBox.Show("DXF exported!");
            }
        }


        private IEnumerable<Canvas> EnumerateCanvasVisuals()
        {
            if (GridList == null) yield break;

            for (int i = 0; i < GridList.Items.Count; i++)
            {
                var container = GridList.ItemContainerGenerator.ContainerFromIndex(i) as DependencyObject;
                if (container == null)
                {
                    GridList.UpdateLayout();
                    container = GridList.ItemContainerGenerator.ContainerFromIndex(i) as DependencyObject;
                }

                if (container == null) continue;

                var canvas = FindVisualChild<Canvas>(container);
                if (canvas != null)
                {
                    yield return canvas;
                }
            }
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match)
                    return match;

                var nested = FindVisualChild<T>(child);
                if (nested != null)
                    return nested;
            }

            return null;
        }

        private sealed class PdfPageImage
        {
            public PdfPageImage(byte[] imageData, int pixelWidth, int pixelHeight, double dpiX, double dpiY)
            {
                ImageData = imageData ?? throw new ArgumentNullException(nameof(imageData));
                PixelWidth = pixelWidth;
                PixelHeight = pixelHeight;
                DpiX = dpiX <= 0 ? 96.0 : dpiX;
                DpiY = dpiY <= 0 ? 96.0 : dpiY;
            }

            public byte[] ImageData { get; }
            public int PixelWidth { get; }
            public int PixelHeight { get; }
            public double DpiX { get; }
            public double DpiY { get; }

            public double WidthPoints => PixelWidth / DpiX * 72.0;
            public double HeightPoints => PixelHeight / DpiY * 72.0;
        }

        private static class SimplePdfWriter
        {
            public static void WritePdf(string path, IReadOnlyList<PdfPageImage> pages)
            {
                if (pages == null || pages.Count == 0)
                    throw new ArgumentException("No pages to write.", nameof(pages));

                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    var offsets = new List<long> { 0L };
                    int nextObj = 1;
                    var culture = CultureInfo.InvariantCulture;

                    void WriteLine(string text)
                    {
                        var bytes = Encoding.ASCII.GetBytes(text + "\n");
                        fs.Write(bytes, 0, bytes.Length);
                    }

                    void EnsureOffsetCapacity(int index)
                    {
                        while (offsets.Count <= index)
                            offsets.Add(0L);
                    }

                    void BeginObject(int index)
                    {
                        EnsureOffsetCapacity(index);
                        offsets[index] = fs.Position;
                        WriteLine($"{index} 0 obj");
                    }

                    void EndObject()
                    {
                        WriteLine("endobj");
                    }

                    WriteLine("%PDF-1.4");

                    int catalogObj = nextObj++;
                    int pagesObj = nextObj++;

                    var pageObjs = new List<int>();
                    var contentObjs = new List<int>();
                    var imageObjs = new List<int>();

                    foreach (var _ in pages)
                    {
                        pageObjs.Add(nextObj++);
                        contentObjs.Add(nextObj++);
                        imageObjs.Add(nextObj++);
                    }

                    BeginObject(catalogObj);
                    WriteLine($"<< /Type /Catalog /Pages {pagesObj} 0 R >>");
                    EndObject();

                    BeginObject(pagesObj);
                    var kids = string.Join(" ", pageObjs.Select(id => $"{id} 0 R"));
                    WriteLine("<< /Type /Pages");
                    WriteLine($"   /Kids [{kids}]");
                    WriteLine($"   /Count {pages.Count} >>");
                    EndObject();

                    for (int i = 0; i < pages.Count; i++)
                    {
                        var page = pages[i];
                        int pageObj = pageObjs[i];
                        int contentObj = contentObjs[i];
                        int imageObj = imageObjs[i];

                        BeginObject(pageObj);
                        WriteLine("<< /Type /Page");
                        WriteLine($"   /Parent {pagesObj} 0 R");
                        WriteLine($"   /MediaBox [0 0 {page.WidthPoints.ToString(culture)} {page.HeightPoints.ToString(culture)}]");
                        WriteLine($"   /Contents {contentObj} 0 R");
                        WriteLine($"   /Resources << /XObject << /Im0 {imageObj} 0 R >> >> >>");
                        EndObject();

                        var contentStream = Encoding.ASCII.GetBytes(
                            $"q\n{page.WidthPoints.ToString(culture)} 0 0 {page.HeightPoints.ToString(culture)} 0 0 cm\n/Im0 Do\nQ\n");

                        BeginObject(contentObj);
                        WriteLine($"<< /Length {contentStream.Length} >>");
                        WriteLine("stream");
                        fs.Write(contentStream, 0, contentStream.Length);
                        fs.WriteByte((byte)'\n');
                        WriteLine("endstream");
                        EndObject();

                        BeginObject(imageObj);
                        WriteLine($"<< /Type /XObject /Subtype /Image /Width {page.PixelWidth} /Height {page.PixelHeight} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {page.ImageData.Length} >>");
                        WriteLine("stream");
                        fs.Write(page.ImageData, 0, page.ImageData.Length);
                        fs.WriteByte((byte)'\n');
                        WriteLine("endstream");
                        EndObject();
                    }

                    long xrefOffset = fs.Position;
                    WriteLine("xref");
                    WriteLine($"0 {nextObj}");
                    WriteLine("0000000000 65535 f ");

                    for (int i = 1; i < nextObj; i++)
                    {
                        long offset = i < offsets.Count ? offsets[i] : 0L;
                        WriteLine(offset.ToString("D10", culture) + " 00000 n ");
                    }

                    WriteLine("trailer");
                    WriteLine($"<< /Size {nextObj} /Root {catalogObj} 0 R >>");
                    WriteLine("startxref");
                    WriteLine(xrefOffset.ToString(culture));
                    WriteLine("%%EOF");
                }
            }
        }

        private sealed class PdfVectorPage
        {
            public PdfVectorPage(string key, double widthPoints, double heightPoints, byte[] content)
            {
                Key = string.IsNullOrWhiteSpace(key) ? "page" : key;
                WidthPoints = widthPoints;
                HeightPoints = heightPoints;
                Content = content ?? Array.Empty<byte>();
            }

            public string Key { get; }
            public double WidthPoints { get; }
            public double HeightPoints { get; }
            public byte[] Content { get; }
        }

        private static class PdfVectorWriter
        {
            public static void WritePdf(string path, IReadOnlyList<PdfVectorPage> pages)
            {
                if (pages == null || pages.Count == 0)
                    throw new ArgumentException("No pages to write.", nameof(pages));

                using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
                {
                    var offsets = new List<long> { 0L };
                    int nextObj = 1;
                    var culture = CultureInfo.InvariantCulture;

                    void WriteLine(string text)
                    {
                        var bytes = Encoding.ASCII.GetBytes(text + "\n");
                        fs.Write(bytes, 0, bytes.Length);
                    }

                    void EnsureOffsetCapacity(int index)
                    {
                        while (offsets.Count <= index)
                            offsets.Add(0L);
                    }

                    void BeginObject(int index)
                    {
                        EnsureOffsetCapacity(index);
                        offsets[index] = fs.Position;
                        WriteLine($"{index} 0 obj");
                    }

                    void EndObject()
                    {
                        WriteLine("endobj");
                    }

                    WriteLine("%PDF-1.4");

                    int catalogObj = nextObj++;
                    int pagesObj = nextObj++;

                    var pageObjs = new List<int>();
                    var contentObjs = new List<int>();
                    foreach (var _ in pages)
                    {
                        pageObjs.Add(nextObj++);
                        contentObjs.Add(nextObj++);
                    }

                    BeginObject(catalogObj);
                    WriteLine($"<< /Type /Catalog /Pages {pagesObj} 0 R >>");
                    EndObject();

                    BeginObject(pagesObj);
                    var kids = string.Join(" ", pageObjs.Select(id => $"{id} 0 R"));
                    WriteLine("<< /Type /Pages");
                    WriteLine($"   /Kids [{kids}]");
                    WriteLine($"   /Count {pages.Count} >>");
                    EndObject();

                    for (int i = 0; i < pages.Count; i++)
                    {
                        var page = pages[i];
                        int pageObj = pageObjs[i];
                        int contentObj = contentObjs[i];

                        BeginObject(pageObj);
                        WriteLine("<< /Type /Page");
                        WriteLine($"   /Parent {pagesObj} 0 R");
                        WriteLine($"   /MediaBox [0 0 {page.WidthPoints.ToString(culture)} {page.HeightPoints.ToString(culture)}]");
                        WriteLine($"   /Contents {contentObj} 0 R");
                        WriteLine("   /Resources << >> >>");
                        EndObject();

                        BeginObject(contentObj);
                        WriteLine($"<< /Length {page.Content.Length} >>");
                        WriteLine("stream");
                        fs.Write(page.Content, 0, page.Content.Length);
                        fs.WriteByte((byte)'\n');
                        WriteLine("endstream");
                        EndObject();
                    }

                    long xrefOffset = fs.Position;
                    WriteLine("xref");
                    WriteLine($"0 {nextObj}");
                    WriteLine("0000000000 65535 f ");

                    for (int i = 1; i < nextObj; i++)
                    {
                        long offset = i < offsets.Count ? offsets[i] : 0L;
                        WriteLine(offset.ToString("D10", culture) + " 00000 n ");
                    }

                    WriteLine("trailer");
                    WriteLine($"<< /Size {nextObj} /Root {catalogObj} 0 R >>");
                    WriteLine("startxref");
                    WriteLine(xrefOffset.ToString(culture));
                    WriteLine("%%EOF");
                }
            }
        }

        private enum PdfPaperSize
        {
            A4,
            A3
        }

        private enum PdfPaperOrientation
        {
            Landscape,
            Portrait
        }

        private enum PdfHorizontalAlignment
        {
            Left,
            Center,
            Right
        }

        private enum PdfVerticalAlignment
        {
            Top,
            Middle,
            Bottom
        }

        private static class PdfVectorBuilder
        {
            private const double MmToPt = 72.0 / 25.4;
            private const double PxToMm = 25.4 / 96.0;
            private const double MmToPx = 96.0 / 25.4;
            private const double PageMarginMm = 10.0;
            private const double A4WidthMm = 297.0;
            private const double A4HeightMm = 210.0;
            private const double A3WidthMm = 420.0;
            private const double A3HeightMm = 297.0;
            private const double LineWidthScale = 2.4;
            private const double MinLineWidthMm = 0.35;
            public static PdfVectorPage Create(string key,
                                               IEnumerable<DxfLine> lines,
                                               IEnumerable<DxfText> texts,
                                               IEnumerable<DxfCircle> circles,
                                               IEnumerable<DxfArc> arcs,
                                               IEnumerable<DxfSolid> solids,
                                               string fallbackFont,
                                               PdfPlotSettings plotSettings)
            {
                var builder = new PdfVectorContentBuilder(fallbackFont, plotSettings);
                builder.AddLines(lines);
                builder.AddCircles(circles);
                builder.AddArcs(arcs);
                builder.AddSolids(solids);
                builder.AddTexts(texts);
                return builder.Build(key);
            }

            private sealed class PdfVectorContentBuilder
            {
                private readonly List<Action<StringBuilder, PdfDrawState>> _actions = new List<Action<StringBuilder, PdfDrawState>>();
                private readonly string _fallbackFont;
                private readonly PdfPlotSettings _plotSettings;
                private double _minX = double.PositiveInfinity;
                private double _minY = double.PositiveInfinity;
                private double _maxX = double.NegativeInfinity;
                private double _maxY = double.NegativeInfinity;

                public PdfVectorContentBuilder(string fallbackFont, PdfPlotSettings plotSettings)
                {
                    _fallbackFont = string.IsNullOrWhiteSpace(fallbackFont) ? "Yu Mincho" : fallbackFont;
                    _plotSettings = plotSettings ?? throw new ArgumentNullException(nameof(plotSettings));
                }

                public void AddLines(IEnumerable<DxfLine> lines)
                {
                    if (lines == null) return;
                    foreach (var ln in lines)
                    {
                        Extend(ln.X1, ln.Y1);
                        Extend(ln.X2, ln.Y2);

                        var command = new StringBuilder();
                        command.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} m\n", FormatDouble(ln.X1), FormatDouble(ln.Y1));
                        command.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatDouble(ln.X2), FormatDouble(ln.Y2));

                        double lineWidth = ToLineWidthMm(ln.ThicknessPx);
                        var strokeColor = ln.StrokeColor;

                        _actions.Add((sb, state) =>
                        {
                            SetStrokeColor(sb, state, strokeColor);
                            SetLineWidth(sb, state, lineWidth);
                            sb.Append(command.ToString());
                            sb.AppendLine("S");
                        });
                    }
                }

                public void AddCircles(IEnumerable<DxfCircle> circles)
                {
                    if (circles == null) return;
                    foreach (var c in circles)
                    {
                        Extend(c.X - c.R, c.Y - c.R);
                        Extend(c.X + c.R, c.Y + c.R);

                        var path = BuildCirclePath(c.X, c.Y, c.R);
                        double lineWidth = ToLineWidthMm(c.StrokeThicknessPx);
                        var strokeColor = c.StrokeColor;
                        var fillColor = c.FillColor;

                        _actions.Add((sb, state) =>
                        {
                            SetStrokeColor(sb, state, strokeColor);
                            SetLineWidth(sb, state, lineWidth);
                            sb.Append(path);
                            if (c.Filled)
                            {
                                SetFillColor(sb, state, fillColor);
                                sb.AppendLine("B");
                            }
                            else
                            {
                                sb.AppendLine("S");
                            }
                        });
                    }
                }

                public void AddArcs(IEnumerable<DxfArc> arcs)
                {
                    if (arcs == null) return;
                    foreach (var arc in arcs)
                    {
                        var points = SampleArc(arc);
                        if (points.Count < 2) continue;

                        foreach (var pt in points)
                            Extend(pt.X, pt.Y);
                        ExtendArcBounds(arc);

                        var path = BuildPathFromPoints(points, closePath: false);
                        double lineWidth = ToLineWidthMm(arc.ThicknessPx);
                        var strokeColor = arc.StrokeColor;

                        _actions.Add((sb, state) =>
                        {
                            SetStrokeColor(sb, state, strokeColor);
                            SetLineWidth(sb, state, lineWidth);
                            sb.Append(path);
                            sb.AppendLine("S");
                        });
                    }
                }

                public void AddSolids(IEnumerable<DxfSolid> solids)
                {
                    if (solids == null) return;
                    foreach (var s in solids)
                    {
                        var points = new List<Point>
                        {
                            new Point(s.X1, s.Y1),
                            new Point(s.X2, s.Y2),
                            new Point(s.X4, s.Y4),
                            new Point(s.X3, s.Y3)
                        };

                        foreach (var pt in points)
                            Extend(pt.X, pt.Y);

                        var path = BuildPathFromPoints(points, closePath: true);
                        var fillColor = s.FillColor;

                        _actions.Add((sb, state) =>
                        {
                            SetFillColor(sb, state, fillColor);
                            sb.Append(path);
                            sb.AppendLine("f");
                        });
                    }
                }

                public void AddTexts(IEnumerable<DxfText> texts)
                {
                    if (texts == null) return;
                    foreach (var txt in texts)
                    {
                        if (string.IsNullOrWhiteSpace(txt.Value))
                            continue;

                        var metrics = MeasureDxfTextLayout(txt, PlotRenderTextTarget, MmToPx);
                        var exportText = metrics.Text;
                        var formatted = new FormattedText(
                            exportText.Value,
                            CultureInfo.CurrentCulture,
                            FlowDirection.LeftToRight,
                            metrics.Typeface,
                            metrics.FontPx,
                            Brushes.Black,
                            1.0);
                        var geometryPx = formatted.BuildGeometry(new Point(0, 0));
                        if (geometryPx == null)
                            continue;

                        geometryPx = geometryPx.CloneCurrentValue();
                        var boundsPx = geometryPx.Bounds;
                        if (boundsPx.IsEmpty || boundsPx.Width <= 0 || boundsPx.Height <= 0)
                            continue;

                        Point topLeftWorld = GetPlotRenderTextTopLeftWorld(metrics);
                        Vector anchorOffsetMm = GetDxfTextAnchorOffset(metrics.WidthMm, metrics.HeightMm, exportText.HAnchor, exportText.VAnchor);

                        var transform = new TransformGroup();
                        transform.Children.Add(new ScaleTransform(PxToMm, PxToMm));
                        if (Math.Abs(exportText.RotationDeg) > 0.001)
                        {
                            transform.Children.Add(new RotateTransform(-exportText.RotationDeg, anchorOffsetMm.X, anchorOffsetMm.Y));
                        }
                        transform.Children.Add(new TranslateTransform(topLeftWorld.X, topLeftWorld.Y));

                        geometryPx.Transform = transform;
                        var pathGeometry = PathGeometry.CreateFromGeometry(geometryPx);
                        var finalBounds = pathGeometry.Bounds;
                        if (!finalBounds.IsEmpty)
                            Extend(finalBounds);

                        var path = BuildGeometryPath(pathGeometry);
                        if (string.IsNullOrEmpty(path))
                            continue;

                        var fillColor = exportText.Color;
                        _actions.Add((sb, state) =>
                        {
                            SetFillColor(sb, state, fillColor);
                            sb.Append(path);
                            sb.AppendLine("f*");
                        });
                    }
                }

                private static GlyphTypeface ResolveGlyphTypeface(string fontFamilyName)
                {
                    if (string.IsNullOrWhiteSpace(fontFamilyName))
                        throw new InvalidOperationException("PDF outline export requires a concrete font family name.");

                    var requested = fontFamilyName.Trim();
                    var typeface = new Typeface(new FontFamily(requested), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                    if (typeface.TryGetGlyphTypeface(out var glyphTypeface) &&
                        GlyphTypefaceMatchesFamily(glyphTypeface, requested))
                    {
                        return glyphTypeface;
                    }

                    foreach (var family in Fonts.SystemFontFamilies)
                    {
                        if (!string.Equals(family.Source, requested, StringComparison.OrdinalIgnoreCase))
                            continue;

                        var exactTypeface = new Typeface(family, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
                        if (exactTypeface.TryGetGlyphTypeface(out glyphTypeface) &&
                            GlyphTypefaceMatchesFamily(glyphTypeface, requested))
                        {
                            return glyphTypeface;
                        }
                    }

                    throw new InvalidOperationException($"PDF outline export requires installed font '{requested}'.");
                }

                private static bool GlyphTypefaceMatchesFamily(GlyphTypeface glyphTypeface, string requestedFamily)
                {
                    if (glyphTypeface == null || string.IsNullOrWhiteSpace(requestedFamily))
                        return false;

                    var requested = requestedFamily.Trim();

                    bool Match(IDictionary<CultureInfo, string> names)
                    {
                        if (names == null) return false;
                        foreach (var entry in names.Values)
                        {
                            if (string.Equals(entry, requested, StringComparison.OrdinalIgnoreCase))
                                return true;
                        }
                        return false;
                    }

                    return Match(glyphTypeface.FamilyNames) || Match(glyphTypeface.Win32FamilyNames);
                }

                private static Geometry BuildTextGeometryPixels(string text, GlyphTypeface glyphTypeface, double fontPx, string fontFamilyName)
                {
                    var geometry = BuildGlyphGeometryFromTypeface(text, glyphTypeface, fontPx, fontFamilyName);
                    if (geometry != null && !geometry.Bounds.IsEmpty && geometry.Bounds.Width > 0 && geometry.Bounds.Height > 0)
                        return geometry;

                    return null;
                }

                private static int CountTextLines(string text)
                {
                    if (string.IsNullOrEmpty(text))
                        return 1;

                    return text.Replace("\r\n", "\n").Split('\n').Length;
                }

                private static Geometry BuildGlyphGeometryFromTypeface(string text, GlyphTypeface glyphTypeface, double fontPx, string fontFamilyName)
                {
                    if (glyphTypeface == null || string.IsNullOrEmpty(text) || fontPx <= 0)
                        return null;

                    string normalized = text.Replace("\r\n", "\n");
                    var lines = normalized.Split('\n');
                    var group = new GeometryGroup { FillRule = FillRule.EvenOdd };
                    double baselinePx = glyphTypeface.Baseline * fontPx;
                    double lineHeightPx = glyphTypeface.Height * fontPx;
                    double y = baselinePx;
                    bool hasGeometry = false;

                    foreach (var line in lines)
                    {
                        if (line.Length == 0)
                        {
                            y += lineHeightPx;
                            continue;
                        }

                        var glyphIndices = new List<ushort>();
                        var advanceWidths = new List<double>();

                        foreach (var ch in line)
                        {
                            if (!glyphTypeface.CharacterToGlyphMap.TryGetValue(ch, out var glyphIndex))
                            {
                                throw new InvalidOperationException(
                                    $"Font '{fontFamilyName}' does not contain glyph for '{ch}' (U+{((int)ch):X4}).");
                            }

                            glyphIndices.Add(glyphIndex);
                            double width;
                            if (!glyphTypeface.AdvanceWidths.TryGetValue(glyphIndex, out width))
                                width = 0.5; // fallback advance in em units
                            advanceWidths.Add(width * fontPx);
                        }

                        if (glyphIndices.Count > 0)
                        {
                            Geometry glyphRunGeometry = null;
                            try
                            {
                                glyphRunGeometry = new GlyphRun(glyphTypeface, 0, false, fontPx, glyphIndices,
                                    new Point(0, y), advanceWidths, null, null, null, null, null, null).BuildGeometry();
                            }
                            catch
                            {
                                glyphRunGeometry = null;
                            }

                            if (glyphRunGeometry != null)
                            {
                                group.Children.Add(glyphRunGeometry);
                                hasGeometry = true;
                            }
                            else
                            {
                                double advance = 0;
                                for (int i = 0; i < glyphIndices.Count; i++)
                                {
                                    Geometry outline = null;
                                    try
                                    {
                                        outline = glyphTypeface.GetGlyphOutline(glyphIndices[i], fontPx, fontPx);
                                    }
                                    catch
                                    {
                                        outline = null;
                                    }

                                    if (outline != null)
                                    {
                                        var glyphGeometry = outline.CloneCurrentValue();
                                        glyphGeometry.Transform = new TranslateTransform(advance, y);
                                        group.Children.Add(glyphGeometry);
                                        hasGeometry = true;
                                    }

                                    advance += advanceWidths[i];
                                }
                            }
                        }

                        y += lineHeightPx;
                    }

                    return hasGeometry ? group : null;
                }

                private void DrawPageLayout(StringBuilder sb, PdfDrawState state, PdfPageLayoutPlan page, string key)
                {
                    var black = MediaColor.FromRgb(0, 0, 0);
                    double pageWidthPt = page.PageWidthMm * MmToPt;
                    double pageHeightPt = page.PageHeightMm * MmToPt;
                    double frameLeftPt = page.FrameLeftMm * MmToPt;
                    double frameBottomPt = page.FrameBottomMm * MmToPt;
                    double frameWidthPt = page.FrameWidthMm * MmToPt;
                    double frameHeightPt = page.FrameHeightMm * MmToPt;
                    SetStrokeColor(sb, state, black);
                    SetLineWidth(sb, state, 0.2 * MmToPt);
                    sb.AppendFormat(CultureInfo.InvariantCulture, "0 0 {0} {1} re\nS\n", FormatDouble(pageWidthPt), FormatDouble(pageHeightPt));
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} re\nS\n",
                        FormatDouble(frameLeftPt),
                        FormatDouble(frameBottomPt),
                        FormatDouble(frameWidthPt),
                        FormatDouble(frameHeightPt));
                    SetStrokeColor(sb, state, black);
                    SetLineWidth(sb, state, 0.2 * MmToPt);
                }

                private void AppendPageTextOutline(StringBuilder sb, PdfDrawState state, string text, double targetHeightMm, double leftMm, double topMm, double maxWidthMm)
                {
                    if (string.IsNullOrWhiteSpace(text) || targetHeightMm <= 0)
                        return;

                    string fontFamilyName = string.IsNullOrWhiteSpace(_fallbackFont) ? "Yu Mincho" : _fallbackFont;
                    var glyphTypeface = ResolveGlyphTypeface(fontFamilyName);
                    const double fontPx = 100.0;
                    var geometryPx = BuildTextGeometryPixels(text, glyphTypeface, fontPx, fontFamilyName);
                    if (geometryPx == null)
                        return;

                    geometryPx = geometryPx.CloneCurrentValue();
                    var boundsPx = geometryPx.Bounds;
                    if (boundsPx.IsEmpty || boundsPx.Width <= 0 || boundsPx.Height <= 0)
                        return;

                    double metricHeightPx = Math.Max(glyphTypeface.Height * fontPx, 1.0);
                    double scaleMmPerPx = targetHeightMm / metricHeightPx;
                    double widthMm = boundsPx.Width * scaleMmPerPx;
                    if (maxWidthMm > 0 && widthMm > maxWidthMm)
                    {
                        scaleMmPerPx *= maxWidthMm / widthMm;
                    }

                    var transform = new TransformGroup();
                    transform.Children.Add(new TranslateTransform(-boundsPx.X, -boundsPx.Y));
                    transform.Children.Add(new ScaleTransform(scaleMmPerPx * MmToPt, scaleMmPerPx * MmToPt));
                    transform.Children.Add(new TranslateTransform(leftMm * MmToPt, topMm * MmToPt));
                    geometryPx.Transform = transform;

                    var path = BuildGeometryPath(PathGeometry.CreateFromGeometry(geometryPx));
                    if (string.IsNullOrEmpty(path))
                        return;

                    SetFillColor(sb, state, MediaColor.FromRgb(0, 0, 0));
                    sb.Append(path);
                    sb.AppendLine("f*");
                }

                public PdfVectorPage Build(string key)
                {
                    if (_actions.Count == 0 || double.IsInfinity(_minX) || double.IsInfinity(_minY) ||
                        double.IsInfinity(_maxX) || double.IsInfinity(_maxY))
                    {
                        return null;
                    }

                    double minX = _minX;
                    double minY = _minY;
                    double maxX = _maxX;
                    double maxY = _maxY;

                    var viewportWindow = _plotSettings.ViewportWindow;
                    if (viewportWindow != null
                        && viewportWindow.MaxX > viewportWindow.MinX
                        && viewportWindow.MaxY > viewportWindow.MinY)
                    {
                        minX = viewportWindow.MinX;
                        maxX = viewportWindow.MaxX;
                        minY = viewportWindow.MinY;
                        maxY = viewportWindow.MaxY;
                    }

                    if (maxX <= minX) maxX = minX + 1;
                    if (maxY <= minY) maxY = minY + 1;

                    ApplyPdfAlignmentToVisibleBounds(_plotSettings, ref minX, ref minY, ref maxX, ref maxY);

                    double contentWidth = maxX - minX;
                    double contentHeight = maxY - minY;

                    var page = ResolvePdfPageLayout(_plotSettings.PaperSize, _plotSettings.Orientation, contentWidth, contentHeight, _plotSettings.ScaleDenominator, _plotSettings.FitMode, _plotSettings.HorizontalAlignment, _plotSettings.VerticalAlignment, _plotSettings.InnerFrameOffsetMm);

                    double pageWidthPoints = page.PageWidthMm * MmToPt;
                    double pageHeightPoints = page.PageHeightMm * MmToPt;
                    double contentLeftPoints = page.ContentLeftMm * MmToPt;
                    double contentBottomPoints = page.ContentBottomMm * MmToPt;
                    double contentWidthPoints = page.ContentWidthMm * MmToPt;
                    double contentHeightPoints = page.ContentHeightMm * MmToPt;
                    double scalePt = page.ScaleMmPerMm * MmToPt;
                    double translateXPt = (page.MarginLeftMm - minX * page.ScaleMmPerMm) * MmToPt;
                    double translateYPt = (page.MarginBottomMm + maxY * page.ScaleMmPerMm) * MmToPt;

                    var sb = new StringBuilder();
                    sb.AppendLine("q");
                    var state = new PdfDrawState { LineWidth = double.NaN };
                    DrawPageLayout(sb, state, page, key);
                    SetStrokeColor(sb, state, MediaColor.FromRgb(120, 120, 120));
                    SetLineWidth(sb, state, 0.18 * MmToPt);
                    //sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} re\nS\n",
                    //                FormatDouble(contentLeftPoints),
                    //                FormatDouble(contentBottomPoints),
                    //                FormatDouble(contentWidthPoints),
                    //                FormatDouble(contentHeightPoints));
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} re W n\n",
                                    FormatDouble(contentLeftPoints),
                                    FormatDouble(contentBottomPoints),
                                    FormatDouble(contentWidthPoints),
                                    FormatDouble(contentHeightPoints));
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} 0 0 {1} {2} {3} cm\n",
                                    FormatDouble(scalePt),
                                    FormatDouble(-scalePt),
                                    FormatDouble(translateXPt),
                                    FormatDouble(translateYPt));
                    sb.AppendLine("1 J");
                    sb.AppendLine("1 j");
                    sb.AppendLine("[] 0 d");
                    foreach (var action in _actions)
                    {
                        action(sb, state);
                    }
                    sb.AppendLine("Q");

                    var content = Encoding.ASCII.GetBytes(sb.ToString());
                    return new PdfVectorPage(key, pageWidthPoints, pageHeightPoints, content);
                }

                private void Extend(double x, double y)
                {
                    if (x < _minX) _minX = x;
                    if (y < _minY) _minY = y;
                    if (x > _maxX) _maxX = x;
                    if (y > _maxY) _maxY = y;
                }

                private void Extend(Rect rect)
                {
                    if (rect.IsEmpty) return;
                    Extend(rect.Left, rect.Top);
                    Extend(rect.Left, rect.Bottom);
                    Extend(rect.Right, rect.Top);
                    Extend(rect.Right, rect.Bottom);
                }

                private static double ToLineWidthMm(double thicknessPx)
                {
                    double mm = thicknessPx * PxToMm * LineWidthScale;
                    if (mm <= 0)
                    {
                        mm = MinLineWidthMm;
                    }
                    else if (mm < MinLineWidthMm)
                    {
                        mm = MinLineWidthMm;
                    }
                    return mm;
                }

                private static string FormatDouble(double value)
                {
                    if (Math.Abs(value) < 1e-9) value = 0;
                    return value.ToString("0.######", CultureInfo.InvariantCulture);
                }

                private static MediaColor NormalizePdfPrintColor(MediaColor color)
                {
                    double luminance = (0.299 * color.R) + (0.587 * color.G) + (0.114 * color.B);
                    if (luminance <= 0.0)
                        return color;

                    double factor = 100.0 / luminance;
                    byte r = (byte)Math.Max(0, Math.Min(255, Math.Round(color.R * factor)));
                    byte g = (byte)Math.Max(0, Math.Min(255, Math.Round(color.G * factor)));
                    byte b = (byte)Math.Max(0, Math.Min(255, Math.Round(color.B * factor)));
                    return MediaColor.FromRgb(r, g, b);
                }

                private static void SetStrokeColor(StringBuilder sb, PdfDrawState state, MediaColor color)
                {
                    color = NormalizePdfPrintColor(color);
                    if (!state.StrokeColor.HasValue || state.StrokeColor.Value != color)
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} RG\n",
                            (color.R / 255.0).ToString("0.######", CultureInfo.InvariantCulture),
                            (color.G / 255.0).ToString("0.######", CultureInfo.InvariantCulture),
                            (color.B / 255.0).ToString("0.######", CultureInfo.InvariantCulture));
                        state.StrokeColor = color;
                    }
                }

                private static void SetFillColor(StringBuilder sb, PdfDrawState state, MediaColor color)
                {
                    color = NormalizePdfPrintColor(color);
                    if (!state.FillColor.HasValue || state.FillColor.Value != color)
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} rg\n",
                            (color.R / 255.0).ToString("0.######", CultureInfo.InvariantCulture),
                            (color.G / 255.0).ToString("0.######", CultureInfo.InvariantCulture),
                            (color.B / 255.0).ToString("0.######", CultureInfo.InvariantCulture));
                        state.FillColor = color;
                    }
                }

                private static void SetLineWidth(StringBuilder sb, PdfDrawState state, double widthMm)
                {
                    if (double.IsNaN(state.LineWidth) || Math.Abs(state.LineWidth - widthMm) > 1e-6)
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} w\n", FormatDouble(widthMm));
                        state.LineWidth = widthMm;
                    }
                }

                private static string BuildCirclePath(double cx, double cy, double r)
                {
                    const double k = 0.5522847498307935;
                    double c = r * k;
                    var sb = new StringBuilder();
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} m\n", FormatDouble(cx + r), FormatDouble(cy));
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} c\n",
                        FormatDouble(cx + r), FormatDouble(cy + c),
                        FormatDouble(cx + c), FormatDouble(cy + r),
                        FormatDouble(cx), FormatDouble(cy + r));
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} c\n",
                        FormatDouble(cx - c), FormatDouble(cy + r),
                        FormatDouble(cx - r), FormatDouble(cy + c),
                        FormatDouble(cx - r), FormatDouble(cy));
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} c\n",
                        FormatDouble(cx - r), FormatDouble(cy - c),
                        FormatDouble(cx - c), FormatDouble(cy - r),
                        FormatDouble(cx), FormatDouble(cy - r));
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} c\n",
                        FormatDouble(cx + c), FormatDouble(cy - r),
                        FormatDouble(cx + r), FormatDouble(cy - c),
                        FormatDouble(cx + r), FormatDouble(cy));
                    sb.AppendLine("h");
                    return sb.ToString();
                }

                private static string BuildPathFromPoints(IList<Point> points, bool closePath)
                {
                    if (points == null || points.Count == 0) return string.Empty;
                    var sb = new StringBuilder();
                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} m\n", FormatDouble(points[0].X), FormatDouble(points[0].Y));
                    for (int i = 1; i < points.Count; i++)
                    {
                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatDouble(points[i].X), FormatDouble(points[i].Y));
                    }
                    if (closePath)
                    {
                        sb.AppendLine("h");
                    }
                    return sb.ToString();
                }

                private static string BuildGeometryPath(PathGeometry geometry)
                {
                    if (geometry == null) return string.Empty;
                    var sb = new StringBuilder();
                    foreach (var fig in geometry.Figures)
                    {
                        if (fig == null || fig.Segments.Count == 0) continue;
                        Point currentPoint = fig.StartPoint;
                        sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} m\n", FormatDouble(fig.StartPoint.X), FormatDouble(fig.StartPoint.Y));
                        foreach (var seg in fig.Segments)
                        {
                            if (seg is System.Windows.Media.PolyLineSegment poly)
                            {
                                foreach (var pt in poly.Points)
                                {
                                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatDouble(pt.X), FormatDouble(pt.Y));
                                    currentPoint = pt;
                                }
                            }
                            else if (seg is System.Windows.Media.LineSegment line)
                            {
                                sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} l\n", FormatDouble(line.Point.X), FormatDouble(line.Point.Y));
                                currentPoint = line.Point;
                            }
                            else if (seg is BezierSegment bezier)
                            {
                                sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} c\n",
                                    FormatDouble(bezier.Point1.X), FormatDouble(bezier.Point1.Y),
                                    FormatDouble(bezier.Point2.X), FormatDouble(bezier.Point2.Y),
                                    FormatDouble(bezier.Point3.X), FormatDouble(bezier.Point3.Y));
                                currentPoint = bezier.Point3;
                            }
                            else if (seg is PolyBezierSegment polyBezier)
                            {
                                for (int i = 0; i + 2 < polyBezier.Points.Count; i += 3)
                                {
                                    var p1 = polyBezier.Points[i];
                                    var p2 = polyBezier.Points[i + 1];
                                    var p3 = polyBezier.Points[i + 2];
                                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} c\n",
                                        FormatDouble(p1.X), FormatDouble(p1.Y),
                                        FormatDouble(p2.X), FormatDouble(p2.Y),
                                        FormatDouble(p3.X), FormatDouble(p3.Y));
                                    currentPoint = p3;
                                }
                            }
                            else if (seg is QuadraticBezierSegment quadratic)
                            {
                                AppendQuadraticAsCubic(sb, currentPoint, quadratic.Point1, quadratic.Point2);
                                currentPoint = quadratic.Point2;
                            }
                            else if (seg is PolyQuadraticBezierSegment polyQuadratic)
                            {
                                for (int i = 0; i + 1 < polyQuadratic.Points.Count; i += 2)
                                {
                                    var control = polyQuadratic.Points[i];
                                    var end = polyQuadratic.Points[i + 1];
                                    AppendQuadraticAsCubic(sb, currentPoint, control, end);
                                    currentPoint = end;
                                }
                            }
                        }
                        sb.AppendLine("h");
                    }
                    return sb.ToString();
                }

                private static void AppendQuadraticAsCubic(StringBuilder sb, Point start, Point control, Point end)
                {
                    var cubic1 = new Point(
                        start.X + ((control.X - start.X) * 2.0 / 3.0),
                        start.Y + ((control.Y - start.Y) * 2.0 / 3.0));
                    var cubic2 = new Point(
                        end.X + ((control.X - end.X) * 2.0 / 3.0),
                        end.Y + ((control.Y - end.Y) * 2.0 / 3.0));

                    sb.AppendFormat(CultureInfo.InvariantCulture, "{0} {1} {2} {3} {4} {5} c\n",
                        FormatDouble(cubic1.X), FormatDouble(cubic1.Y),
                        FormatDouble(cubic2.X), FormatDouble(cubic2.Y),
                        FormatDouble(end.X), FormatDouble(end.Y));
                }

                private static List<Point> SampleArc(DxfArc arc)
                {
                    var points = new List<Point>();
                    if (arc == null || arc.R <= 0) return points;

                    double start = DegreesToRadians(arc.StartDeg);
                    double end = DegreesToRadians(arc.EndDeg);
                    double sweep = end - start;
                    if (sweep <= 0) sweep += Math.PI * 2.0;

                    int steps = Math.Max(2, (int)Math.Ceiling(sweep / (Math.PI / 18.0))); // khoảng 10° mỗi đoạn
                    double step = sweep / steps;
                    for (int i = 0; i <= steps; i++)
                    {
                        double angle = start + step * i;
                        double x = arc.X + arc.R * Math.Cos(angle);
                        double y = arc.Y + arc.R * Math.Sin(angle);
                        points.Add(new Point(x, y));
                    }
                    return points;
                }

                private void ExtendArcBounds(DxfArc arc)
                {
                    if (arc == null || arc.R <= 0) return;
                    double start = DegreesToRadians(arc.StartDeg);
                    double end = DegreesToRadians(arc.EndDeg);
                    double sweep = end - start;
                    if (sweep <= 0) sweep += Math.PI * 2.0;

                    foreach (var angle in new[] { 0.0, Math.PI / 2.0, Math.PI, 3.0 * Math.PI / 2.0 })
                    {
                        if (AngleWithinSweep(start, sweep, angle))
                        {
                            double x = arc.X + arc.R * Math.Cos(angle);
                            double y = arc.Y + arc.R * Math.Sin(angle);
                            Extend(x, y);
                        }
                    }
                }

                private static double DegreesToRadians(double degrees)
                    => degrees * Math.PI / 180.0;

                private static bool AngleWithinSweep(double start, double sweep, double angle)
                {
                    double twoPi = Math.PI * 2.0;
                    double s = NormalizeAngle(start);
                    double e = NormalizeAngle(start + sweep);
                    double a = NormalizeAngle(angle);
                    if (sweep >= twoPi - 1e-6) return true;
                    if (s <= e)
                        return a >= s - 1e-6 && a <= e + 1e-6;
                    return a >= s - 1e-6 || a <= e + 1e-6;
                }

                private static double NormalizeAngle(double angle)
                {
                    double twoPi = Math.PI * 2.0;
                    angle %= twoPi;
                    if (angle < 0) angle += twoPi;
                    return angle;
                }
            }

            private sealed class PdfDrawState
            {
                public MediaColor? StrokeColor;
                public MediaColor? FillColor;
                public double LineWidth;
            }
        }

        // Replace all usages of "is not" pattern with equivalent C# 7.3 compatible code

        // Example replacement in Canvas_MouseWheel:
        private void Canvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var canvas = sender as Canvas;
            var item = canvas != null ? canvas.DataContext as GridBotsecozu : null;
            if (canvas == null || item == null) return;
            CancelDeferredInteractiveCanvasRedraw(canvas);
            if (!TryMakeTransform(canvas, item, out var T, out var fitScale, out _, out _)) return;
            bool canUseInteractiveViewport = TryBeginInteractiveViewport(canvas, item);

            var vs = VS(item);
            var sp = e.GetPosition(canvas);
            var w = ScreenToWorld(T, sp);
            double interactiveWheelSteps = e.Delta / InteractiveViewportWheelDeltaUnit;
            double interactiveStepBase =
                Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)
                ? InteractiveViewportCtrlWheelStep
                : InteractiveViewportWheelStep;
            double interactiveStep = Math.Pow(interactiveStepBase, interactiveWheelSteps);

            double step = e.Delta > 0 ? 1.1 : 0.9;                // ±10%
            if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
                step = Math.Pow(step, 2);                         // nhanh hơn khi giữ Ctrl

            double newZoom = Clamp(vs.Zoom * interactiveStep, 0.05, 20.0);
            double baseOx = canvas.ActualWidth / 2.0;
            double baseOy = (canvas.ActualHeight - 800) / 2.0;
            double Sprime = fitScale * newZoom;

            vs.PanXmm = (sp.X - baseOx) / Sprime - w.X;
            vs.PanYmm = (sp.Y - baseOy) / Sprime - w.Y;
            vs.Zoom = newZoom;

            if (canUseInteractiveViewport && ApplyInteractiveViewport(canvas, item))
                ScheduleInteractiveViewportCommit(canvas, item);
            else
                Redraw(canvas, item);
            e.Handled = true;
        }

        // Example replacement in Canvas_MouseDown:
        private void Canvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var canvas = sender as Canvas;
            var item = canvas != null ? canvas.DataContext as GridBotsecozu : null;
            if (canvas == null || item == null) return;
            bool startPan = e.ChangedButton == MouseButton.Middle ||
                            (e.LeftButton == MouseButtonState.Pressed && Keyboard.IsKeyDown(Key.Space));
            if (!startPan) return;

            if (e.ChangedButton == MouseButton.Middle && e.ClickCount == 2)
            {
                var fitState = VS(item);
                fitState.Zoom = 1.0;
                fitState.PanXmm = 0;
                fitState.PanYmm = 0;
                Redraw(canvas, item);
                e.Handled = true;
                return;
            }

            var vs = VS(item);
            _isPanning = true;
            CancelDeferredInteractiveCanvasRedraw(canvas);
            _panStartPx = e.GetPosition(canvas);
            _panStartPanMm = new Point(vs.PanXmm, vs.PanYmm);
            if (TryMakeTransform(canvas, item, out var panStartTransform, out _, out _, out _))
                _panStartScalePxPerMm = Math.Abs(panStartTransform.Scale) < 1e-9 ? 1.0 : panStartTransform.Scale;
            else
                _panStartScalePxPerMm = 1.0;
            canvas.CaptureMouse();
            TryBeginInteractiveViewport(canvas, item);
            if (_interactiveViewportByCanvas.TryGetValue(canvas, out var panState) && panState != null)
                _panStartOverlayMatrix = panState.OverlayTransform.Matrix;
            else
                _panStartOverlayMatrix = Matrix.Identity;
            e.Handled = true;
        }

        // Example replacement in Canvas_MouseMove:
        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isPanning) return;
            var canvas = sender as Canvas;
            var item = canvas != null ? canvas.DataContext as GridBotsecozu : null;
            if (canvas == null || item == null) return;

            var vs = VS(item);
            var sp = e.GetPosition(canvas);
            double S = Math.Abs(_panStartScalePxPerMm) < 1e-9 ? 1.0 : _panStartScalePxPerMm;
            var delta = sp - _panStartPx;

            vs.PanXmm = _panStartPanMm.X + delta.X / S;
            vs.PanYmm = _panStartPanMm.Y + delta.Y / S;

            if (!ApplyInteractiveViewportPan(canvas, delta))
                Redraw(canvas, item);
        }

        private void Canvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isPanning) return;
            _isPanning = false;
            if (sender is Canvas c)
            {
                var item = c.DataContext as GridBotsecozu;
                c.ReleaseMouseCapture();

                if (item != null)
                {
                    if (_interactiveViewportByCanvas.TryGetValue(c, out var state) && state.IsActive)
                        CommitInteractiveViewport(c, item);
                    else
                        Redraw(c, item);
                }
            }
        }

        // Example replacement in Canvas_KeyDown:
        private void Canvas_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.F) return;
            var canvas = sender as Canvas;
            var item = canvas != null ? canvas.DataContext as GridBotsecozu : null;
            if (canvas == null || item == null) return;
            var vs = VS(item);
            vs.Zoom = 1.0; vs.PanXmm = 0; vs.PanYmm = 0;
            Redraw(canvas, item);
            e.Handled = true;
        }
        // Add this helper method to the 梁配筋施工図 class (or as a static method in a suitable location)
        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
        // ===== [SCENE] Một nguồn dữ liệu cho Preview + DXF =====

        // Line trong thế giới mm + Layer
        struct SceneLine
        {
            public double X1, Y1, X2, Y2;
            public string Layer;
            public double Thickness;
            public double[] Dash;
            public MediaColor StrokeColor;
            public SceneLine(double x1, double y1, double x2, double y2,
                              string layer = "LINE", double thickness = 1.0,
                              DoubleCollection dash = null, MediaColor? strokeColor = null)
            {
                X1 = x1; Y1 = y1; X2 = x2; Y2 = y2;
                Layer = layer ?? "LINE";
                Thickness = thickness;
                Dash = dash != null && dash.Count > 0 ? dash.Cast<double>().ToArray() : null;
                StrokeColor = strokeColor ?? Colors.Black;
            }
        }

        // Tái dùng DxfText làm shape text (đã có ở dưới) — không cần thêm type mới.

        // Kho scene theo mỗi GridBotsecozu
        private readonly Dictionary<GridBotsecozu, List<object>> _sceneByItem = new Dictionary<GridBotsecozu, List<object>>();
        private List<object> SceneFor(GridBotsecozu item)
        {
            if (!_sceneByItem.TryGetValue(item, out var list))
            {
                list = new List<object>();
                _sceneByItem[item] = list;
            }
            return list;
        }

        // Mỗi lần vẽ lại thì làm mới scene cho item đó
        private void SceneBegin(GridBotsecozu item) => _sceneByItem[item] = new List<object>();

        // Anchor → DXF align helper
        private static (int h, int v) ToDxfAlign(HAnchor ha, VAnchor va)
        {
            int h = ha == HAnchor.Left ? 0 : ha == HAnchor.Right ? 2 : 1;
            int v = va == VAnchor.Top ? 3 : va == VAnchor.Middle ? 2 : 1;
            return (h, v);
        }
        private (string G符号, double 上側, double 下側, double 梁の段差1)
        GetBeamValuesByPosition(string kai, string tsu, string left, string right)
        {
            double ParseNum(string s)
                => double.TryParse(s, System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture,
                                   out var v) ? v : 0.0;

            string key = $"{kai}::{tsu}";
            string title = $"{kai} {tsu}の{left}-{right}";

            var haichiList = _projectData?.Haichi?.梁配置図;
            if (haichiList == null) return ("", 0, 0, 0);

            foreach (var haichi in haichiList)
            {
                if (haichi?.BeamSegmentsMap == null) continue;
                if (!haichi.BeamSegmentsMap.TryGetValue(key, out var segs) || segs == null) continue;

                var seg = segs.FirstOrDefault(s => s.左側 == left && s.右側 == right)
                       ?? segs.FirstOrDefault(s => s.タイトル == title);

                if (seg != null)
                {
                    string gSym = seg.梁の符号 ?? "G0";           // <-- GIỮ CHUỖI NGUYÊN VẸN
                    double up = ParseNum(seg.上側のズレ寸法);
                    double down = ParseNum(seg.下側のズレ寸法);
                    double step = ParseNum(seg.梁の段差);
                    return (gSym, up, down, step);
                }
            }
            return ("G0", 300, 300, -200);
        }
        // ===== Helper: lấy Z梁の配置 (配筋) theo G符号 của nhịp trên 1 tầng =====
        private Z梁の配置 GetRebarConfigForSpan(string kai, string gSym)
        {
            if (string.IsNullOrWhiteSpace(gSym)) gSym = "G0";

            // 1) tìm danh sách 梁 của tầng 'kai'
            var floorBeamList = _projectData?.リスト?.梁リスト?
                .FirstOrDefault(r => r?.各階 == kai);
            if (floorBeamList == null) return null;

            // 2) tìm đúng beam theo Name == gSym; nếu không thì rơi về beam đầu
            var beam = floorBeamList.梁?.FirstOrDefault(b => b?.Name == gSym)
                    ?? floorBeamList.梁?.FirstOrDefault();
            if (beam == null) return null;

            // 3) đảm bảo có cấu hình 配置
            if (beam.梁の配置 == null)
                beam.梁の配置 = new Z梁の配置();

            // 4) fallback "1" để hiển thị không bị rỗng
            string F(string s) => string.IsNullOrWhiteSpace(s) ? "1" : s;
            var z = beam.梁の配置;

            z.端部1上筋本数 = F(z.端部1上筋本数);
            z.端部1上宙1 = F(z.端部1上宙1);
            z.端部1上宙2 = F(z.端部1上宙2);
            z.端部1下宙1 = F(z.端部1下宙1);
            z.端部1下宙2 = F(z.端部1下宙2);
            z.端部1下筋本数 = F(z.端部1下筋本数);
            z.端部1主筋径 = F(z.端部1主筋径);

            z.中央上筋本数 = F(z.中央上筋本数);
            z.中央上宙1 = F(z.中央上宙1);
            z.中央上宙2 = F(z.中央上宙2);
            z.中央下宙1 = F(z.中央下宙1);
            z.中央下宙2 = F(z.中央下宙2);
            z.中央下筋本数 = F(z.中央下筋本数);
            z.中央主筋径 = F(z.中央主筋径);

            z.端部2上筋本数 = F(z.端部2上筋本数);
            z.端部2上宙1 = F(z.端部2上宙1);
            z.端部2上宙2 = F(z.端部2上宙2);
            z.端部2下宙1 = F(z.端部2下宙1);
            z.端部2下宙2 = F(z.端部2下宙2);
            z.端部2下筋本数 = F(z.端部2下筋本数);
            z.端部2主筋径 = F(z.端部2主筋径);

            return z;
        }
        // Hàm lấy thông tin kích thước (中央幅, 中央成) từ beam theo G符号
        private (string 中央幅, string 中央成, string 中央スタラップ径, string ピッチ, string スタラップ材質,
                string 端部1幅止筋径, string 端部1幅止筋ピッチ, string 中央中子筋径, string 中央中子筋径ピッチ, string 中央中子筋材質, string 端部1腹筋径) GetBeamSize(string kai, string gSym)
        {
            if (string.IsNullOrWhiteSpace(gSym)) gSym = "G0";

            // 1) tìm danh sách梁 theo tầng kai
            var floorBeamList = _projectData?.リスト?.梁リスト?
                .FirstOrDefault(r => r?.各階 == kai);
            if (floorBeamList == null) return ("", "", "", "", "", "", "", "", "", "", "");

            // 2) lấy đúng beam theo tên G符号
            var beam = floorBeamList.梁?.FirstOrDefault(b => b?.Name == gSym)
                    ?? floorBeamList.梁?.FirstOrDefault();
            if (beam == null) return ("", "", "", "", "", "", "", "", "", "", "");

            // 3) đảm bảo beam có 配置
            if (beam.梁の配置 == null)
                beam.梁の配置 = new Z梁の配置();

            // 4) fallback: nếu trống → trả về mặc định
            string F(string s, string def) => string.IsNullOrWhiteSpace(s) ? def : s;

            string 中央幅 = F(beam.梁の配置.中央幅, "900");
            string 中央成 = F(beam.梁の配置.中央成, "600");

            string 中央スタラップ径 = F(beam.梁の配置.中央スタラップ径, "10");
            string ピッチ = F(beam.梁の配置.中央ピッチ, "100");
            string スタラップ材質 = F(beam.梁の配置.中央スタラップ材質, "SD390");
            string 端部1幅止筋径 = F(beam.梁の配置.端部1幅止筋径, "10");
            string 端部1幅止筋ピッチ = F(beam.梁の配置.端部1幅止筋ピッチ, "100");
            string 中央中子筋径 = F(beam.梁の配置.中央中子筋径, "10");
            string 中央中子筋径ピッチ = F(beam.梁の配置.中央中子筋径ピッチ, "200");
            string 中央中子筋材質 = F(beam.梁の配置.中央中子筋材質, "SD390");
            string 端部1腹筋径 = F(beam.梁の配置.端部1腹筋径, "13");


            return (中央幅, 中央成, 中央スタラップ径, ピッチ, スタラップ材質, 端部1幅止筋径, 端部1幅止筋ピッチ, 中央中子筋径, 中央中子筋径ピッチ, 中央中子筋材質, 端部1腹筋径);
        }

        private (double gTanbu, double nTanbu, double gChubu, double nChubu) GetYochouValues()
        {
            var kesan = _projectData?.Kesan;
            if (kesan == null)
                return (0, 0, 0, 0);

            double gTanbu = AsDouble(kesan.GaitanTanbuTopYochou);
            double nTanbu = AsDouble(kesan.NaitanTanbuTopYochou);
            double gChubu = AsDouble(kesan.GaitanChubuTopYochou);
            double nChubu = AsDouble(kesan.NaitanChubuTopYochou);

            return (gTanbu, nTanbu, gChubu, nChubu);
        }
        // ニゲ
        // Trong class 梁配筋施工図 (梁配筋施工図.Canvas.cs)
        private (double nigeUwa,
                 double nigeUwaChu1,
                 double nigeUwaChu2,
                 double nigeShitaChu1,
                 double nigeShitaChu2,
                 double nigeShita)
        GetNigeValues()
        {
            var kesan = _projectData?.Kesan; // ProjectData.Kesan【turn13file14†L10-L13】
            if (kesan == null)
                return (0, 0, 0, 0, 0, 0);

            // Dự phòng: tự TryParse an toàn
            double A(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return 0;
                if (double.TryParse(s.Trim(), out var v)) return v;
                return 0;
            }

            // Các thuộc tính string trong KesanData【turn13file1†L24-L29】
            double uwa = A(kesan.NigeUwa);
            double uwaChu1 = A(kesan.NigeUwaChu1);
            double uwaChu2 = A(kesan.NigeUwaChu2);
            double shitaChu1 = A(kesan.NigeShitaChu1);
            double shitaChu2 = A(kesan.NigeShitaChu2);
            double shita = A(kesan.NigeShita);

            return (uwa, uwaChu1, uwaChu2, shitaChu1, shitaChu2, shita);
        }
        // Trong class 梁配筋施工図 (cùng file 梁配筋施工図.Canvas.cs, đặt cạnh GetNigeValues)
        private (double ankaUwa,
                 double ankaUwaChu,
                 double ankaShitaChu,
                 double ankaShita)
        GetAnkaNagaValues()
        {
            var kesan = _projectData?.Kesan;
            if (kesan == null)
                return (0, 0, 0, 0);

            // Parse an toàn từ string -> double (trống/không hợp lệ trả 0)
            double A(string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return 0;
                return double.TryParse(s.Trim(), out var v) ? v : 0;
            }

            double uwa = A(kesan.AnkaNagaUwa);
            double uwaChu = A(kesan.AnkaNagaUwaChu);
            double shitaChu = A(kesan.AnkaNagaShitaChu);
            double shita = A(kesan.AnkaNagaShita);

            return (uwa, uwaChu, shitaChu, shita);
        }
        // 梁配筋施工図.Canvas.cs (đặt trong partial class 梁配筋施工図)
        private (bool teiUwa, bool teiUwaChu, bool teiShitaChu, bool teiShita) GetTeiFlags()
        {
            var kesan = _projectData?.Kesan;
            // Nếu chưa có Kesan, trả về đúng default đang được set trong KesanData (đều true)
            if (kesan == null) return (true, true, true, true);

            return (kesan.Teiuwa, kesan.Teiuwachu, kesan.Teishitachu, kesan.Teishita);
        }
        private double AdjustCentralNetHeight(double netHeight, string centralMainBarDiameterText)
        {
            double adjustedHeight = netHeight;

            if (adjustedHeight <= 0)
                return 0;

            double mainBarDiameter = ParseDoubleInvariant(centralMainBarDiameterText);
            if (mainBarDiameter <= 0)
                return Math.Max(adjustedHeight, 0);

            if (ToActualDiameter.TryGetValue(mainBarDiameter, out double actualDiameter))
                mainBarDiameter = actualDiameter;

            var kesan = _projectData?.Kesan;
            if (kesan == null)
                return Math.Max(adjustedHeight, 0);

            if (kesan.梁の主筋の位置1)
                adjustedHeight -= 2 * mainBarDiameter;
            else if (kesan.梁の主筋の位置2 || kesan.梁の主筋の位置3)
                adjustedHeight -= mainBarDiameter;

            return Math.Max(adjustedHeight, 0);
        }
        private readonly Dictionary<double, double> ToActualDiameter = new Dictionary<double, double>
        {
            { 10, 11 },
            { 13, 14 },
            { 16, 18 },
            { 19, 21 },
            { 22, 25 },
            { 25, 28 },
            { 29, 33 },
            { 32, 36 },
            { 35, 40 },
            { 38, 43 },
        };
        // === LẤY GIÁ TRỊ HATARAKION, ANKANAGAON, NIGEON từ ProjectData.Kesan ===
        private (bool Hatarakion1, bool Hatarakion2,
                 bool Ankanagaon1, bool Ankanagaon2,
                 bool Nigeon1, bool Nigeon2,
                 bool TsugiteOption1, bool TsugiteOption2) GetKesanFlags()
        {
            var kesan = _projectData?.Kesan;
            if (kesan == null)
                // Trả default (false hết) nếu chưa khởi tạo KesanData
                return (false, false, false, false, false, false, false, false);

            return (
                kesan.Hatarakion1, kesan.Hatarakion2,
                kesan.Ankanagaon1, kesan.Ankanagaon2,
                kesan.Nigeon1, kesan.Nigeon2,
                kesan.TsugiteOption1, kesan.TsugiteOption2
            );
        }
        private (double TonariOo, double TonariKo) GetTonariValues()
        {
            var kesan = _projectData?.Kesan;
            if (kesan == null)
                return (0, 0);

            double A(string s)
                => double.TryParse(s, out var v) ? v : 0;

            double oo = A(kesan.TonariOo);
            double ko = A(kesan.TonariKo);

            return (oo, ko);
        }
        private bool GetDairyouOnKoiryouOff(string kai, string gSym)
        {
            var beam = FindBeamBySymbol(kai, gSym);
            if (beam?.梁の配置 == null) return false;

            return beam.梁の配置.大梁ON_小梁OFF;
        }

        //bool flag = GetDairyouOnKoiryouOff(selF, G0);
        //var(tonariOo, tonariKo) = GetTonariValues();

        // Kết hợp code 2 người : Chỉnh sửa ngày 2026.01.30 16h14
        // File: 梁配筋施工図.cs

        private void MakeTanbuFukkinEditable(
            TextBlock tb,
            Canvas canvas,
            WCTransform T,
            double wx,
            double wy,
            GridBotsecozu item,
            string kai,
            string gSym,
            string diameter,
            int spanIndex,
            bool isRight,
            double bodyBaseLength,
            double fallbackLeft,
            double fallbackRight)
        {
            if (tb == null || canvas == null) return;

            tb.Cursor = Cursors.Hand;
            if (tb.Background == null) tb.Background = Brushes.Transparent;
            tb.IsHitTestVisible = true;
            Panel.SetZIndex(tb, 1500);

            EnableEditableHoverWithBox(tb, canvas,
                hoverForeground: Brushes.Blue,
                hoverFill: new SolidColorBrush(Color.FromArgb(100, 30, 144, 255)),
                hoverStroke: Brushes.Blue);

            bool isRightAbdominal = isRight;
            string currentDiameter = GetTanbuDiameter(item, spanIndex, isRightAbdominal, diameter);

            Action<bool> redrawIfChanged = changed =>
            {
                if (changed) Redraw(canvas, item);
            };

            Action<bool> openHookEditor = endIsRight =>
            {
                ShowHookLengthEditor(
                    canvas, tb, T, wx, wy, HAnchor.Center, VAnchor.Bottom,
                    () =>
                    {
                        double fb = endIsRight ? fallbackRight : fallbackLeft;
                        return GetTanbuHookLength(item, spanIndex, isRightAbdominal, endIsRight, fb);
                    },
                    newLen =>
                    {
                        bool changed = ApplyTanbuHookLength(item, spanIndex, isRightAbdominal, endIsRight, newLen);
                        redrawIfChanged(changed);
                    });
            };

            var oldHandler = PopupMenuHandlerStore.Get(tb);
            if (oldHandler != null) tb.MouseLeftButtonDown -= oldHandler;

            double GetLeft(FrameworkElement fe)
            {
                double v = Canvas.GetLeft(fe);
                return double.IsNaN(v) ? 0 : v;
            }

            double GetTop(FrameworkElement fe)
            {
                double v = Canvas.GetTop(fe);
                return double.IsNaN(v) ? 0 : v;
            }

            double GetWidth(FrameworkElement fe)
            {
                if (fe.ActualWidth > 0) return fe.ActualWidth;
                if (!double.IsNaN(fe.Width) && fe.Width > 0) return fe.Width;
                return 0;
            }

            // ✅ fixed anchor = right-top of tb
            void GetTbRightTop(TextBlock owner, double gapPx, out double x, out double y)
            {
                x = GetLeft(owner) + Math.Max(1, GetWidth(owner)) + gapPx;
                y = GetTop(owner);
            }

            MouseButtonEventHandler handler = (s, e) =>
            {
                e.Handled = true;

                CloseTaggedPopup(tb);
                CloseGlobalActivePopup();

                Brush normalBg = Brushes.Transparent;
                Brush selectedBg = new SolidColorBrush(Color.FromRgb(210, 225, 255));
                Brush dividerBrush = PopupMenuDividerBrush;

                UIElement WithRowDivider(UIElement child)
                {
                    return new Border
                    {
                        BorderBrush = dividerBrush,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Child = child
                    };
                }

                ControlTemplate flatBtnTemplate = null;
                ControlTemplate GetFlatBtnTemplate()
                {
                    if (flatBtnTemplate != null) return flatBtnTemplate;

                    var b = new FrameworkElementFactory(typeof(Border));
                    b.SetValue(Border.SnapsToDevicePixelsProperty, true);
                    b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                    b.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                    b.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                    b.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

                    var p = new FrameworkElementFactory(typeof(ContentPresenter));
                    p.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
                    p.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
                    p.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Button.VerticalContentAlignmentProperty));

                    b.AppendChild(p);

                    flatBtnTemplate = new ControlTemplate(typeof(Button)) { VisualTree = b };
                    return flatBtnTemplate;
                }

                Border WrapBox(UIElement child)
                {
                    var host = new ContentControl
                    {
                        Content = child,
                        FontSize = SystemFonts.MessageFontSize,
                        FontFamily = SystemFonts.MessageFontFamily
                    };

                    return new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = PopupMenuBorderBrush,
                        BorderThickness = PopupMenuBorderThickness,
                        CornerRadius = new CornerRadius(2),
                        Child = host
                    };
                }

                var win = Window.GetWindow(canvas);

                bool IsPointInside(FrameworkElement fe, Point screenPt)
                {
                    if (fe == null || !fe.IsVisible) return false;
                    try
                    {
                        var topLeft = fe.PointToScreen(new Point(0, 0));
                        var rect = new Rect(topLeft, new Size(fe.ActualWidth, fe.ActualHeight));
                        return rect.Contains(screenPt);
                    }
                    catch { return false; }
                }

                Button selectedMainBtn = null;
                Button pinnedMainBtn = null;
                void ClearPinnedMain()
                {
                    if (pinnedMainBtn != null && !ReferenceEquals(pinnedMainBtn, selectedMainBtn))
                        pinnedMainBtn.Background = normalBg;
                    pinnedMainBtn = null;
                }
                void PinMain(Button btn)
                {
                    if (ReferenceEquals(pinnedMainBtn, btn))
                    {
                        if (pinnedMainBtn != null)
                            pinnedMainBtn.Background = selectedBg;
                        return;
                    }

                    if (pinnedMainBtn != null && !ReferenceEquals(pinnedMainBtn, selectedMainBtn))
                        pinnedMainBtn.Background = normalBg;

                    pinnedMainBtn = btn;
                    if (pinnedMainBtn != null)
                        pinnedMainBtn.Background = selectedBg;
                }
                void SelectMain(Button btn)
                {
                    if (selectedMainBtn != null && !ReferenceEquals(selectedMainBtn, pinnedMainBtn))
                        selectedMainBtn.Background = normalBg;
                    selectedMainBtn = btn;
                    if (selectedMainBtn != null)
                        selectedMainBtn.Background = selectedBg;
                }

                Button selectedSubBtn = null;
                Button pinnedSubBtn = null;
                void ClearPinnedSub()
                {
                    if (pinnedSubBtn != null && !ReferenceEquals(pinnedSubBtn, selectedSubBtn))
                        pinnedSubBtn.Background = normalBg;
                    pinnedSubBtn = null;
                }
                void PinSub(Button btn)
                {
                    if (ReferenceEquals(pinnedSubBtn, btn))
                    {
                        if (pinnedSubBtn != null)
                            pinnedSubBtn.Background = selectedBg;
                        return;
                    }

                    if (pinnedSubBtn != null && !ReferenceEquals(pinnedSubBtn, selectedSubBtn))
                        pinnedSubBtn.Background = normalBg;

                    pinnedSubBtn = btn;
                    if (pinnedSubBtn != null)
                        pinnedSubBtn.Background = selectedBg;
                }
                void SelectSub(Button btn)
                {
                    if (selectedSubBtn != null && !ReferenceEquals(selectedSubBtn, pinnedSubBtn))
                        selectedSubBtn.Background = normalBg;
                    selectedSubBtn = btn;
                    if (selectedSubBtn != null)
                        selectedSubBtn.Background = selectedBg;
                }

                // ✅ FIXED placement for MAIN popup (Relative to canvas)
                double menuX, menuY;
                GetTbRightTop(tb, 0, out menuX, out menuY);

                var mainPop = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = canvas,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                    HorizontalOffset = menuX,
                    VerticalOffset = menuY,
                    AllowsTransparency = true,
                    StaysOpen = true
                };
                _activePopupMenu = mainPop;

                var handle = new StyledPopupHandle { Popup = mainPop };
                tb.Tag = handle;

                System.Windows.Controls.Primitives.Popup subPop = null;
                System.Windows.Controls.Primitives.Popup subSubPop = null;

                MouseButtonEventHandler outsideCloser = null;
                KeyEventHandler escCloser = null;
                EventHandler winDeactivatedCloser = null;
                EventHandler appDeactivatedCloser = null;
                bool isClosing = false;

                void CloseAll()
                {
                    try { if (ReferenceEquals(tb.Tag, handle)) tb.Tag = null; } catch { }
                    try { if (ReferenceEquals(_activePopupMenu, mainPop)) _activePopupMenu = null; } catch { }
                    ClearPinnedMain();
                    ClearPinnedSub();

                    if (isClosing) return;
                    isClosing = true;

                    try { if (subSubPop != null) subSubPop.IsOpen = false; } catch { }
                    try { if (subPop != null) subPop.IsOpen = false; } catch { }
                    try { mainPop.IsOpen = false; } catch { }

                    try
                    {
                        if (win != null && outsideCloser != null) win.RemoveHandler(UIElement.PreviewMouseDownEvent, outsideCloser);
                        if (win != null && escCloser != null) win.RemoveHandler(UIElement.PreviewKeyDownEvent, escCloser);
                    }
                    catch { }

                    try { if (win != null && winDeactivatedCloser != null) win.Deactivated -= winDeactivatedCloser; } catch { }
                    try { if (Application.Current != null && appDeactivatedCloser != null) Application.Current.Deactivated -= appDeactivatedCloser; } catch { }

                    isClosing = false;
                }

                void CloseSub()
                {
                    ClearPinnedMain();
                    ClearPinnedSub();
                    try { if (subSubPop != null) subSubPop.IsOpen = false; } catch { }
                    try { if (subPop != null) subPop.IsOpen = false; } catch { }
                }

                outsideCloser = (ss, ee2) =>
                {
                    if (win == null) { CloseAll(); return; }
                    var screenPt = win.PointToScreen(ee2.GetPosition(win));

                    bool inside =
                        IsPointInside(tb, screenPt) ||
                        (mainPop.Child is FrameworkElement m && IsPointInside(m, screenPt)) ||
                        (subPop != null && subPop.Child is FrameworkElement s2 && IsPointInside(s2, screenPt)) ||
                        (subSubPop != null && subSubPop.Child is FrameworkElement s3 && IsPointInside(s3, screenPt));

                    if (!inside) CloseAll();
                };

                escCloser = (ss, ke) =>
                {
                    if (ke.Key == Key.Escape)
                    {
                        CloseAll();
                        ke.Handled = true;
                    }
                };

                winDeactivatedCloser = (_, __) => CloseAll();
                appDeactivatedCloser = (_, __) => CloseAll();

                if (win != null)
                {
                    win.AddHandler(UIElement.PreviewMouseDownEvent, outsideCloser, true);
                    win.AddHandler(UIElement.PreviewKeyDownEvent, escCloser, true);
                    win.Deactivated += winDeactivatedCloser;
                }
                if (Application.Current != null)
                    Application.Current.Deactivated += appDeactivatedCloser;

                // ---- UI builders (each row has ❯) ----
                Button MakeMainBtn(string header)
                {
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var txt = new TextBlock
                    {
                        Text = header ?? "",
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(txt, 0);
                    grid.Children.Add(txt);

                    var arrow = new TextBlock
                    {
                        Text = "❯",
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(10, 0, 0, 0)
                    };
                    Grid.SetColumn(arrow, 1);
                    grid.Children.Add(arrow);

                    var b = new Button
                    {
                        Content = grid,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(10, 6, 10, 6),
                        Background = normalBg,
                        BorderBrush = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        MinWidth = 50,
                        OverridesDefaultStyle = true,
                        Template = GetFlatBtnTemplate(),
                        Focusable = false,
                        IsTabStop = false
                    };

                    b.MouseEnter += (_, __) => SelectMain(b);
                    return b;
                }

                Button MakeSubBtn(string header, bool isChecked, Action onClick)
                {
                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(18) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var mark = new TextBlock
                    {
                        Text = "✓",
                        VerticalAlignment = VerticalAlignment.Center,
                        Opacity = isChecked ? 1.0 : 0.0
                    };
                    Grid.SetColumn(mark, 0);

                    var txt = new TextBlock
                    {
                        Text = header ?? "",
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(txt, 1);

                    var arrow = new TextBlock
                    {
                        Text = "❯",
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(10, 0, 0, 0),
                        Opacity = 0.0 // submenu items don't need arrow, but user asked "each row": keep layout consistent
                    };
                    Grid.SetColumn(arrow, 2);

                    grid.Children.Add(mark);
                    grid.Children.Add(txt);
                    grid.Children.Add(arrow);

                    var b = new Button
                    {
                        Content = grid,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(10, 6, 10, 6),
                        Background = normalBg,
                        BorderBrush = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        MinWidth = 50,
                        OverridesDefaultStyle = true,
                        Template = GetFlatBtnTemplate(),
                        Focusable = false,
                        IsTabStop = false
                    };

                    b.MouseEnter += (_, __) => SelectSub(b);
                    b.Click += (_, __) =>
                    {
                        SelectSub(b);
                        onClick?.Invoke();
                        CloseAll();
                    };

                    return b;
                }

                // ---- Submenu openers ----
                void OpenLenSubmenu(Button placementBtn)
                {
                    CloseSub();

                    subPop = new System.Windows.Controls.Primitives.Popup
                    {
                        PlacementTarget = placementBtn,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                        HorizontalOffset = 1,
                        VerticalOffset = -1.5,
                        AllowsTransparency = true,
                        StaysOpen = true
                    };

                    var subRoot = new StackPanel { Orientation = Orientation.Vertical };

                    // left inline editor
                    subRoot.Children.Add(WithRowDivider(
                        MakeLenNavBtn(
                            sideLabel: "左",
                            endIsRight: false,
                            closeAll: CloseAll,
                            selectSub: SelectSub,
                            getFlatBtnTemplate: GetFlatBtnTemplate)));

                    // right inline editor
                    subRoot.Children.Add(WithRowDivider(
                        MakeLenNavBtn(
                            sideLabel: "右",
                            endIsRight: true,
                            closeAll: CloseAll,
                            selectSub: SelectSub,
                            getFlatBtnTemplate: GetFlatBtnTemplate)));

                    subPop.Child = WrapBox(subRoot);

                    placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        PinMain(placementBtn);
                        subPop.IsOpen = true;
                        ClearPinnedSub();
                        SelectSub(null);
                    }), DispatcherPriority.Input);
                }

                Func<string, Tuple<bool, double>> tryParseLen = text =>
                {
                    if (string.IsNullOrWhiteSpace(text))
                        return Tuple.Create(false, 0.0);

                    double v;
                    if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                        return Tuple.Create(false, 0.0);

                    return Tuple.Create(true, v);
                };

                bool ApplyTanbuHookLengthDelta(bool endIsRight, bool pullLeft, double delta)
                {
                    if (delta <= 0)
                        return false;

                    double fallback = endIsRight ? fallbackRight : fallbackLeft;
                    double currentLen = GetTanbuHookLength(item, spanIndex, isRightAbdominal, endIsRight, fallback);

                    double signedDelta;
                    if (endIsRight)
                    {
                        signedDelta = pullLeft ? -delta : delta;
                    }
                    else
                    {
                        signedDelta = pullLeft ? delta : -delta;
                    }

                    double newLen = currentLen + signedDelta;
                    return ApplyTanbuHookLength(item, spanIndex, isRightAbdominal, endIsRight, newLen);
                }

                Action<bool, bool, string> applyHookLenFromText = (endIsRight, pullLeft, text) =>
                {
                    var parsed = tryParseLen(text);
                    if (!parsed.Item1) return;

                    bool changed = ApplyTanbuHookLengthDelta(endIsRight, pullLeft, parsed.Item2);
                    if (changed) Redraw(canvas, item);
                };

                //Func<bool, string> getHookLenText = endIsRight =>
                //{
                //    double fallback = endIsRight ? fallbackRight : fallbackLeft;
                //    double currentLen = GetTanbuHookLength(item, spanIndex, isRightAbdominal, endIsRight, fallback);
                //    return currentLen.ToString(CultureInfo.InvariantCulture);
                //};

                Button MakeInlineLenRow(string sideLabel, bool endIsRight, bool pullLeft, Action closeAll, Action<Button> selectSub, Func<ControlTemplate> getFlatBtnTemplate)
                {
                    var row = new DockPanel { LastChildFill = true };

                    var lbl = new TextBlock
                    {
                        Text = sideLabel ?? "",
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    DockPanel.SetDock(lbl, Dock.Left);

                    var preview = CreateLengthPreviewCanvas(pullLeft);
                    DockPanel.SetDock(preview, Dock.Right);

                    var tbx = new TextBox
                    {
                        Text = string.Empty,
                        Width = 50,
                        MinWidth = 50,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Visibility = System.Windows.Visibility.Collapsed
                    };

                    var lenBorder = new FrameworkElementFactory(typeof(Border));
                    lenBorder.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Control.BackgroundProperty));
                    lenBorder.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Control.BorderBrushProperty));
                    lenBorder.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Control.BorderThicknessProperty));
                    lenBorder.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Control.PaddingProperty));
                    lenBorder.SetValue(Border.SnapsToDevicePixelsProperty, true);

                    var lenContentHost = new FrameworkElementFactory(typeof(ScrollViewer), "PART_ContentHost");
                    lenContentHost.SetValue(FrameworkElement.SnapsToDevicePixelsProperty, true);
                    lenBorder.AppendChild(lenContentHost);

                    tbx.Template = new ControlTemplate(typeof(TextBox))
                    {
                        VisualTree = lenBorder
                    };

                    if (tbx.ReadLocalValue(Control.BorderBrushProperty) == DependencyProperty.UnsetValue)
                        tbx.BorderBrush = PopupMenuEditorBorderBrush;

                    if (tbx.ReadLocalValue(Control.BorderThicknessProperty) == DependencyProperty.UnsetValue)
                        tbx.BorderThickness = new Thickness(1);

                    if (tbx.ReadLocalValue(Control.BackgroundProperty) == DependencyProperty.UnsetValue)
                        tbx.Background = Brushes.White;

                    tbx.FocusVisualStyle = null;

                    Brush initialBackground = tbx.Background ?? Brushes.White;
                    Brush initialBorderBrush = tbx.BorderBrush ?? Brushes.Black;
                    Thickness initialBorderThickness = tbx.BorderThickness;
                    bool hasRejectedLenInput = false;

                    bool IsDigitOnlyLenText(string text, bool allowEmpty = true)
                    {
                        if (string.IsNullOrEmpty(text))
                            return allowEmpty;

                        return text.All(char.IsDigit);
                    }

                    bool ShrinksCurrentTotal()
                    {
                        return pullLeft == endIsRight;
                    }

                    bool IsLenAllowedForCurrentAction(string text, bool allowEmpty = true)
                    {
                        if (string.IsNullOrEmpty(text))
                            return allowEmpty;

                        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                            return false;

                        if (!ShrinksCurrentTotal())
                            return true;

                        return parsed < GetCurrentTanbuTotalLength();
                    }

                    string BuildLenCandidateText(string incomingText)
                    {
                        string currentText = tbx.Text ?? string.Empty;
                        int selectionStart = Math.Max(0, Math.Min(tbx.SelectionStart, currentText.Length));
                        int selectionLength = Math.Max(0, Math.Min(tbx.SelectionLength, currentText.Length - selectionStart));

                        return currentText.Remove(selectionStart, selectionLength)
                            .Insert(selectionStart, incomingText ?? string.Empty);
                    }

                    Thickness EnsureVisibleLenBorder(Thickness thickness)
                    {
                        if (thickness.Left <= 0 && thickness.Top <= 0 && thickness.Right <= 0 && thickness.Bottom <= 0)
                            return new Thickness(1);

                        return thickness;
                    }

                    void SetLenValidityState(bool isValid)
                    {
                        tbx.Background = initialBackground;
                        tbx.BorderBrush = isValid ? initialBorderBrush : Brushes.Red;
                        tbx.BorderThickness = isValid
                            ? initialBorderThickness
                            : EnsureVisibleLenBorder(initialBorderThickness);
                    }

                    void ShowRejectedLenInputState()
                    {
                        hasRejectedLenInput = true;
                        tbx.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (hasRejectedLenInput)
                                SetLenValidityState(false);
                        }), DispatcherPriority.Input);
                    }

                    void ClearRejectedLenInputState()
                    {
                        hasRejectedLenInput = false;
                        SetLenValidityState(IsDigitOnlyLenText(tbx.Text) && IsLenAllowedForCurrentAction(tbx.Text));
                    }

                    tbx.TextChanged += (_, __) =>
                    {
                        hasRejectedLenInput = false;
                        SetLenValidityState(IsDigitOnlyLenText(tbx.Text) && IsLenAllowedForCurrentAction(tbx.Text));
                    };

                    tbx.PreviewTextInput += (_, inputArgs) =>
                    {
                        if (!IsDigitOnlyLenText(BuildLenCandidateText(inputArgs.Text), allowEmpty: true))
                        {
                            ShowRejectedLenInputState();
                            inputArgs.Handled = true;
                            return;
                        }

                        if (!IsLenAllowedForCurrentAction(BuildLenCandidateText(inputArgs.Text), allowEmpty: true))
                        {
                            ShowRejectedLenInputState();
                            inputArgs.Handled = true;
                            return;
                        }

                        ClearRejectedLenInputState();
                    };

                    DataObject.AddPastingHandler(tbx, new DataObjectPastingEventHandler((_, pasteArgs) =>
                    {
                        string pastedText = null;

                        if (pasteArgs.SourceDataObject != null)
                        {
                            if (pasteArgs.SourceDataObject.GetDataPresent(DataFormats.UnicodeText))
                                pastedText = pasteArgs.SourceDataObject.GetData(DataFormats.UnicodeText) as string;
                            else if (pasteArgs.SourceDataObject.GetDataPresent(DataFormats.Text))
                                pastedText = pasteArgs.SourceDataObject.GetData(DataFormats.Text) as string;
                        }

                        if (!IsDigitOnlyLenText(BuildLenCandidateText(pastedText ?? string.Empty), allowEmpty: true))
                        {
                            ShowRejectedLenInputState();
                            pasteArgs.CancelCommand();
                            return;
                        }

                        if (!IsLenAllowedForCurrentAction(BuildLenCandidateText(pastedText ?? string.Empty), allowEmpty: true))
                        {
                            ShowRejectedLenInputState();
                            pasteArgs.CancelCommand();
                            return;
                        }

                        ClearRejectedLenInputState();
                    }));

                    tbx.LostKeyboardFocus += (_, __) =>
                    {
                        hasRejectedLenInput = false;
                        SetLenValidityState(IsDigitOnlyLenText(tbx.Text) && IsLenAllowedForCurrentAction(tbx.Text));
                    };

                    SetLenValidityState(IsDigitOnlyLenText(tbx.Text) && IsLenAllowedForCurrentAction(tbx.Text));
                    DockPanel.SetDock(tbx, Dock.Right);

                    row.Children.Add(lbl);
                    row.Children.Add(tbx);
                    row.Children.Add(preview);

                    var btn = new Button
                    {
                        Content = row,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(12, 6, 10, 6),
                        Background = Brushes.Transparent,
                        BorderBrush = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        MinWidth = 120,
                        OverridesDefaultStyle = true,
                        Template = getFlatBtnTemplate(),
                        Focusable = false,
                        IsTabStop = false
                    };

                    void FocusBox()
                    {
                        tbx.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            tbx.Focus();
                            tbx.SelectAll();
                        }), DispatcherPriority.Input);
                    }

                    void EndEditShowPreview()
                    {
                        tbx.Visibility = System.Windows.Visibility.Collapsed;
                        preview.Visibility = System.Windows.Visibility.Visible;
                    }

                    void BeginEdit()
                    {
                        preview.Visibility = System.Windows.Visibility.Collapsed;
                        tbx.Visibility = System.Windows.Visibility.Visible;
                        FocusBox();
                    }

                    btn.MouseEnter += (_, __) => selectSub(btn);
                    btn.MouseLeave += (_, __) =>
                    {
                        EndEditShowPreview();
                        if (ReferenceEquals(selectedSubBtn, btn))
                            selectSub(null);
                    };
                    btn.Click += (_, ee) =>
                    {
                        ee.Handled = true;
                        selectSub(btn);
                        if (tbx.Visibility != System.Windows.Visibility.Visible)
                            BeginEdit();
                        else
                            FocusBox();
                    };

                    tbx.PreviewKeyDown += (_, k) =>
                    {
                        if (k.Key == Key.Enter)
                        {
                            if (!IsDigitOnlyLenText(tbx.Text) || !IsLenAllowedForCurrentAction(tbx.Text))
                            {
                                ShowRejectedLenInputState();
                                k.Handled = true;
                                return;
                            }

                            applyHookLenFromText(endIsRight, pullLeft, tbx.Text);
                            EndEditShowPreview();
                            closeAll();
                            k.Handled = true;
                        }
                        else if (k.Key == Key.Escape)
                        {
                            EndEditShowPreview();
                            k.Handled = true;
                        }
                    };

                    tbx.LostKeyboardFocus += (_, __) =>
                    {
                        if (tbx.Visibility == System.Windows.Visibility.Visible)
                            EndEditShowPreview();
                    };

                    return btn;
                }

                double GetCurrentTanbuTotalLength()
                {
                    string raw = (tb?.Text ?? string.Empty).Trim();
                    int dashIndex = raw.LastIndexOf('-');
                    if (dashIndex >= 0)
                    {
                        string lenText = raw.Substring(dashIndex + 1).Trim();
                        if (double.TryParse(lenText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                            return parsed;
                    }

                    double leftLen = GetTanbuHookLength(item, spanIndex, isRightAbdominal, false, fallbackLeft);
                    double rightLen = GetTanbuHookLength(item, spanIndex, isRightAbdominal, true, fallbackRight);
                    return bodyBaseLength + leftLen + rightLen;
                }

                bool ApplyTanbuTotalLength(bool anchorLeft, double newTotal)
                {
                    if (newTotal <= 0) return false;

                    double leftLen = GetTanbuHookLength(item, spanIndex, isRightAbdominal, false, fallbackLeft);
                    double rightLen = GetTanbuHookLength(item, spanIndex, isRightAbdominal, true, fallbackRight);
                    double bodyLen = Math.Max(0.0, bodyBaseLength);

                    if (anchorLeft)
                    {
                        double newRight = newTotal - bodyLen - leftLen;
                        return ApplyTanbuHookLength(item, spanIndex, isRightAbdominal, true, newRight);
                    }

                    double newLeft = newTotal - bodyLen - rightLen;
                    return ApplyTanbuHookLength(item, spanIndex, isRightAbdominal, false, newLeft);
                }

                Button MakeLenNavBtn(string sideLabel, bool endIsRight, Action closeAll, Action<Button> selectSub, Func<ControlTemplate> getFlatBtnTemplate)
                {
                    var btn = MakeMainBtn(sideLabel);

                    void OpenLenDetail()
                    {
                        try { if (subSubPop != null) subSubPop.IsOpen = false; } catch { }

                        subSubPop = new System.Windows.Controls.Primitives.Popup
                        {
                            PlacementTarget = btn,
                            Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                            HorizontalOffset = 1,
                            VerticalOffset = -1.5,
                            AllowsTransparency = true,
                            StaysOpen = true
                        };

                        var root = new StackPanel { Orientation = Orientation.Vertical, Width = 120 };
                        root.Children.Add(WithRowDivider(
                            MakeInlineLenRow("左へ引く", endIsRight, true, closeAll, selectSub, getFlatBtnTemplate)));
                        root.Children.Add(WithRowDivider(
                            MakeInlineLenRow("右へ引く", endIsRight, false, closeAll, selectSub, getFlatBtnTemplate)));
                        {
                            var row = new DockPanel { LastChildFill = true };

                            var lbl = new TextBlock
                            {
                                Text = "全部",
                                VerticalAlignment = VerticalAlignment.Center
                            };
                            DockPanel.SetDock(lbl, Dock.Left);

                            var totalBox = new TextBox
                            {
                                Text = GetCurrentTanbuTotalLength().ToString(CultureInfo.InvariantCulture),
                                Width = 50,
                                MinWidth = 50,
                                VerticalContentAlignment = VerticalAlignment.Center,
                                Background = Brushes.Transparent,
                                Visibility = System.Windows.Visibility.Visible,
                                IsReadOnly = true,
                                BorderThickness = new Thickness(0),
                                BorderBrush = Brushes.Transparent
                            };
                            DockPanel.SetDock(totalBox, Dock.Right);

                            row.Children.Add(lbl);
                            row.Children.Add(totalBox);
                            //row.Children.Add(preview);

                            var totalButton = new Button
                            {
                                Content = row,
                                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                                VerticalContentAlignment = VerticalAlignment.Center,
                                Padding = new Thickness(10, 6, 10, 6),
                                Background = Brushes.Transparent,
                                BorderBrush = Brushes.Transparent,
                                BorderThickness = new Thickness(0),
                                MinWidth = 150,
                                OverridesDefaultStyle = true,
                                Template = getFlatBtnTemplate(),
                                Focusable = false,
                                IsTabStop = false
                            };

                            void FocusTotalBox()
                            {
                                totalBox.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    totalBox.Focus();
                                    totalBox.SelectAll();
                                }), DispatcherPriority.Input);
                            }

                            void EndTotalEdit()
                            {
                                totalBox.Text = GetCurrentTanbuTotalLength().ToString(CultureInfo.InvariantCulture);
                                totalBox.IsReadOnly = true;
                                totalBox.BorderThickness = new Thickness(0);
                                totalBox.BorderBrush = Brushes.Transparent;
                                totalBox.Background = Brushes.Transparent;
                                totalBox.Select(totalBox.Text?.Length ?? 0, 0);
                                Keyboard.ClearFocus();
                                canvas.Focus();
                            }

                            void BeginTotalEdit()
                            {
                                totalBox.Text = GetCurrentTanbuTotalLength().ToString(CultureInfo.InvariantCulture);
                                totalBox.IsReadOnly = false;
                                totalBox.BorderThickness = new Thickness(1);
                                totalBox.BorderBrush = PopupMenuEditorBorderBrush;
                                totalBox.Background = Brushes.White;
                                FocusTotalBox();
                            }

                            totalButton.MouseEnter += (_, __) => selectSub(totalButton);
                            totalButton.MouseLeave += (_, __) =>
                            {
                                EndTotalEdit();
                                totalButton.Background = normalBg;
                                if (ReferenceEquals(selectedSubBtn, totalButton))
                                    selectSub(null);
                            };
                            totalButton.Click += (_, ee) =>
                            {
                                ee.Handled = true;
                                selectSub(totalButton);
                                if (totalBox.IsReadOnly)
                                    BeginTotalEdit();
                                else
                                    FocusTotalBox();
                            };
                            totalBox.PreviewMouseLeftButtonDown += (_, ee) =>
                            {
                                if (!totalBox.IsReadOnly) return;
                                BeginTotalEdit();
                                ee.Handled = true;
                            };
                            totalBox.PreviewKeyDown += (_, ke) =>
                            {
                                if (ke.Key == Key.Enter)
                                {
                                    if (double.TryParse(totalBox.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var len)
                                        && ApplyTanbuTotalLength(anchorLeft: endIsRight, newTotal: len))
                                    {
                                        Redraw(canvas, item);
                                    }
                                    EndTotalEdit();
                                    closeAll();
                                    ke.Handled = true;
                                }
                                else if (ke.Key == Key.Escape)
                                {
                                    EndTotalEdit();
                                    ke.Handled = true;
                                }
                            };
                            totalBox.LostKeyboardFocus += (_, __) =>
                            {
                                if (!totalBox.IsReadOnly)
                                    EndTotalEdit();
                            };
                            totalBox.MouseLeave += (_, __) =>
                            {
                                EndTotalEdit();
                                totalButton.Background = normalBg;
                                if (ReferenceEquals(selectedSubBtn, totalButton))
                                    selectSub(null);
                            };

                            root.Children.Add(WithRowDivider(totalButton));
                        }

                        subSubPop.Child = WrapBox(root);
                        btn.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            subSubPop.IsOpen = true;
                            PinSub(btn);
                        }), DispatcherPriority.Input);
                    }

                    btn.MouseEnter += (_, __) =>
                    {
                        PinSub(btn);
                        selectSub(btn);
                        OpenLenDetail();
                    };
                    btn.Click += (_, __) =>
                    {
                        PinSub(btn);
                        selectSub(btn);
                        OpenLenDetail();
                    };

                    return btn;
                }


                void OpenDiaSubmenu(Button placementBtn)
                {
                    CloseSub();

                    subPop = new System.Windows.Controls.Primitives.Popup
                    {
                        PlacementTarget = placementBtn,
                        Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                        HorizontalOffset = 1,
                        VerticalOffset = -1.5,
                        AllowsTransparency = true,
                        StaysOpen = true
                    };

                    var subRoot = new StackPanel { Orientation = Orientation.Vertical };

                    foreach (var dia in _standardRebarDiameters)
                    {
                        var d = dia;
                        bool checkedNow = string.Equals(d, currentDiameter, StringComparison.Ordinal);
                        subRoot.Children.Add(WithRowDivider(
                            MakeSubBtn(d, checkedNow, () =>
                            {
                                bool changed = SetTanbuDiameter(item, spanIndex, isRightAbdominal, d);
                                redrawIfChanged(changed);
                            })));
                    }

                    subPop.Child = WrapBox(subRoot);

                    placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        subPop.IsOpen = true;
                        SelectSub(null);
                    }), DispatcherPriority.Input);
                }

                // ---- Main menu ----
                var mainRoot = new StackPanel { Orientation = Orientation.Vertical };

                var btnDia = MakeMainBtn("腹筋の径");
                btnDia.MouseEnter += (_, __) =>
                {
                    SelectMain(btnDia);
                    OpenDiaSubmenu(btnDia); // ✅ hover opens submenu
                };
                btnDia.Click += (_, __) =>
                {
                    SelectMain(btnDia);
                    OpenDiaSubmenu(btnDia);
                };

                var btnLen = MakeMainBtn("腹筋の長さ");
                btnLen.MouseEnter += (_, __) =>
                {
                    PinMain(btnLen);
                    SelectMain(btnLen);
                    OpenLenSubmenu(btnLen); // ✅ hover opens submenu
                };
                btnLen.Click += (_, __) =>
                {
                    PinMain(btnLen);
                    SelectMain(btnLen);
                    OpenLenSubmenu(btnLen);
                };

                mainRoot.Children.Add(WithRowDivider(btnDia));
                mainRoot.Children.Add(WithRowDivider(btnLen));

                mainPop.Child = WrapBox(mainRoot);

                tb.Dispatcher.BeginInvoke(new Action(() =>
                {
                    mainPop.IsOpen = true;
                    SelectMain(null);
                }), DispatcherPriority.Input);
            };

            tb.MouseLeftButtonDown += handler;
            PopupMenuHandlerStore.Set(tb, handler);
        }
        private bool ApplyTanbuHookLength(GridBotsecozu item, int spanIndex, bool isRightAbdominal, bool isRightEnd, double newLength)
        {
            if (double.IsNaN(newLength) || double.IsInfinity(newLength))
                return false;

            return SetTanbuHookLength(item, spanIndex, isRightAbdominal, isRightEnd, newLength);
        }
        private Popup _tanbuLenPopup;



        private MenuItem _tanbuLenOwnerItem;
        // Pin hover state for a single MenuItem (no checkmark)
        private static class MenuItemHoverPin
        {
            public static readonly DependencyProperty IsPinnedProperty =
                DependencyProperty.RegisterAttached(
                    "IsPinned",
                    typeof(bool),
                    typeof(MenuItemHoverPin),
                    new FrameworkPropertyMetadata(false));

            public static void SetIsPinned(DependencyObject obj, bool value)
                => obj.SetValue(IsPinnedProperty, value);

            public static bool GetIsPinned(DependencyObject obj)
                => (bool)obj.GetValue(IsPinnedProperty);
        }

        private bool ApplyTanbuDiameter(string kai, string gSym, string newDia)
        {
            var beam = FindBeamBySymbol(kai, gSym);
            if (beam == null || string.IsNullOrWhiteSpace(newDia))
                return false;

            var layout = beam.梁の配置 ?? (beam.梁の配置 = new Z梁の配置());
            string dia = newDia.Trim();

            if (string.Equals(layout.端部1腹筋径, dia, StringComparison.Ordinal))
                return false;

            layout.端部1腹筋径 = dia;
            return true;
        }

        private void MakeEndWidthStopEditable(
            TextBlock tbDia, TextBlock tbPitch, TextBlock tbRight,
            Canvas canvas, WCTransform T,
            double wx, double wy,
            GridBotsecozu item,
            string kai, string gSym,
            int spanIndex,
            string diameter, string pitch)
        {
            if (tbDia == null || tbPitch == null || tbRight == null || canvas == null)
                return;

            EnableEditableHoverWithBox_Group3(
                tbDia, tbPitch, tbRight, canvas,
                hoverForeground: Brushes.Blue,
                hoverFill: new SolidColorBrush(Color.FromArgb(100, 30, 144, 255)),
                hoverStroke: Brushes.Blue);

            foreach (var tb in new[] { tbDia, tbPitch, tbRight })
            {
                tb.Cursor = Cursors.Hand;
                if (tb.Background == null) tb.Background = Brushes.Transparent;
                tb.IsHitTestVisible = true;
                Panel.SetZIndex(tb, 1500);
            }

            //void RedrawIfChanged(bool changed)
            //{
            //    if (changed)
            //        Redraw(canvas, item);
            //}

            // tránh attach nhiều lần (same pattern group 3 như CentralIntermediate)
            var old = GroupClickHandlerStore.Get(tbPitch);
            if (old != null)
            {
                tbDia.MouseLeftButtonDown -= old;
                tbPitch.MouseLeftButtonDown -= old;
                tbRight.MouseLeftButtonDown -= old;
                GroupClickHandlerStore.Set(tbPitch, null);
            }
            MouseButtonEventHandler handler = (s, e) =>
            {
                e.Handled = true;

                //var target = s as FrameworkElement ?? tbPitch;
                var (menuX, menuY) = GetElementGroupRightTopOnCanvas(canvas, gapPx: 0, tbDia, tbPitch, tbRight);
                ShowCentralStirrupPopupWithHoverSubmenushabadome(
                canvas,
                tbPitch,
                menuX,
                menuY,
                currentDiameter: diameter,
                onSelectDiameter: newDia =>
                {
                    if (ApplySpanEndWidthStopValues(item, spanIndex, newDia, pitch))
                        Redraw(canvas, item);
                },
                onEditPitch: () =>
                {
                    BeginInlinePitchEditPushNeighbors(
                        canvas, tbPitch, tbDia, tbRight,
                        getCurrentText: () => pitch,
                        commitText: newText =>
                        {
                            if (!double.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                                return;

                            if (ApplySpanEndWidthStopValues(item, spanIndex, diameter, v.ToString(CultureInfo.InvariantCulture)))
                                Redraw(canvas, item);
                        });
                });
            };

            tbDia.MouseLeftButtonDown += handler;
            tbPitch.MouseLeftButtonDown += handler;
            tbRight.MouseLeftButtonDown += handler;

            GroupClickHandlerStore.Set(tbPitch, handler);
        }
        private bool ApplyEndWidthStopValues(string kai, string gSym, string diameter, string pitch)
        {
            var beam = FindBeamBySymbol(kai, gSym);
            if (beam == null)
                return false;

            var layout = beam.梁の配置 ?? (beam.梁の配置 = new Z梁の配置());
            bool changed = false;

            if (!string.IsNullOrWhiteSpace(diameter))
            {
                string dia = diameter.Trim();
                if (!string.Equals(layout.端部1幅止筋径, dia, StringComparison.Ordinal))
                {
                    layout.端部1幅止筋径 = dia;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(pitch))
            {
                string ptc = pitch.Trim();
                if (!string.Equals(layout.端部1幅止筋ピッチ, ptc, StringComparison.Ordinal))
                {
                    layout.端部1幅止筋ピッチ = ptc;
                    changed = true;
                }
            }

            return changed;
        }
        private void MakeCentralIntermediateEditable(
                    TextBlock tbDia, TextBlock tbPitch, TextBlock tbMat,
                    Canvas canvas, WCTransform T,
                    double wx, double wy,
                    GridBotsecozu item,
                    string kai, string gSym,
                    int spanIndex,
                    string diameter, string pitch, string material,
                    bool showMaterial)
        {
            if (tbDia == null || tbPitch == null || tbMat == null || canvas == null) return;

            EnableEditableHoverWithBox_Group3(
                tbDia, tbPitch, tbMat, canvas,
                hoverForeground: Brushes.Blue,
                hoverFill: new SolidColorBrush(Color.FromArgb(100, 30, 144, 255)),
                hoverStroke: Brushes.Blue);

            TextBlock[] all = { tbDia, tbPitch, tbMat };
            foreach (var tb in all)
            {
                tb.Cursor = Cursors.Hand;
                if (tb.Background == null) tb.Background = Brushes.Transparent;
                tb.IsHitTestVisible = true;
                Panel.SetZIndex(tb, 1500);
            }


            // ✅ gỡ handler cũ theo reference thật (KHÔNG dùng "-= handler" với handler mới)
            var old = GroupClickHandlerStore.Get(tbPitch);
            if (old != null)
            {
                tbDia.MouseLeftButtonDown -= old;
                tbPitch.MouseLeftButtonDown -= old;
                tbMat.MouseLeftButtonDown -= old;
                GroupClickHandlerStore.Set(tbPitch, null);
            }

            MouseButtonEventHandler handler = (s, e) =>
            {
                e.Handled = true;

                //var target = s as FrameworkElement ?? tbPitch;
                var (menuX, menuY) = GetElementGroupRightTopOnCanvas(canvas, gapPx: 0, tbDia, tbPitch, tbMat);

                ShowCentralStirrupPopupWithHoverSubmenusnakago(
                    canvas,
                    tbPitch,
                    menuX,
                    menuY,
                    currentDiameter: diameter,
                    currentMaterial: material,
                    showMaterial: showMaterial,
                    onSelectDiameter: newDia =>
                    {
                        if (ApplySpanCentralIntermediateValues(item, spanIndex, newDia, pitch, material))
                            Redraw(canvas, item);
                    },
                    onEditPitch: () =>
                    {
                        BeginInlinePitchEditPushNeighbors(
                            canvas, tbPitch, tbDia, tbMat,
                            getCurrentText: () => pitch,
                            commitText: newText =>
                            {
                                double v;
                                if (!double.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                                    return;

                                if (ApplySpanCentralIntermediateValues(item, spanIndex, diameter, v.ToString(CultureInfo.InvariantCulture), material))
                                    Redraw(canvas, item);
                            });
                    },
                    onSelectMaterial: newMat =>
                    {
                        if (ApplySpanCentralIntermediateValues(item, spanIndex, diameter, pitch, newMat))
                            Redraw(canvas, item);
                    });
            };

            tbDia.MouseLeftButtonDown += handler;
            tbPitch.MouseLeftButtonDown += handler;
            tbMat.MouseLeftButtonDown += handler;

            GroupClickHandlerStore.Set(tbPitch, handler);
        }
        private bool ApplyCentralIntermediateValues(string kai, string gSym,
                                                  string diameter, string pitch, string material)
        {
            var beam = FindBeamBySymbol(kai, gSym);
            if (beam == null)
                return false;

            var layout = beam.梁の配置 ?? (beam.梁の配置 = new Z梁の配置());
            bool changed = false;

            if (!string.IsNullOrWhiteSpace(diameter))
            {
                string dia = diameter.Trim();
                if (!string.Equals(layout.中央中子筋径, dia, StringComparison.Ordinal))
                {
                    layout.中央中子筋径 = dia;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(pitch))
            {
                string ptc = pitch.Trim();
                if (!string.Equals(layout.中央中子筋径ピッチ, ptc, StringComparison.Ordinal))
                {
                    layout.中央中子筋径ピッチ = ptc;
                    changed = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(material))
            {
                string mat = material.Trim();
                if (!string.Equals(layout.中央中子筋材質, mat, StringComparison.Ordinal))
                {
                    layout.中央中子筋材質 = mat;
                    changed = true;
                }
            }

            return changed;
        }
        // ===================== [PATCH] Add preview tooltip into 中子 menu =====================
        // File: 梁配筋施工図.cs

        // File: 梁配筋施工図.cs

        private void MakeCentralNakagoShapeEditable(
            Canvas canvas,
            WCTransform T,
            GridBotsecozu item,
            string kai,
            string gSym,
            int spanIndex,
            int currentShape,
            IList<Shape> nakagoShapes,
            double hitCenterXmm,
            double hitCenterYmm,
            double hitWidthMm,
            double hitHeightMm)
        {
            if (canvas == null) return;
            if (nakagoShapes == null) nakagoShapes = new List<Shape>();

            var hit = CreateWorldHitRect(
                canvas, T,
                hitCenterXmm, hitCenterYmm,
                hitWidthMm, hitHeightMm,
                2500,
                padMm: 60,
                radiusPx: 0);
            if (hit == null) return;

            EnableHoverFrame(
                hit,
                hoverStroke: Brushes.MediumBlue,
                hoverFill: new SolidColorBrush(Color.FromArgb(50, 30, 144, 255)),
                hoverThickness: 2.0);

            hit.MouseEnter += delegate
            {
                if (nakagoShapes.Count > 0)
                    SetShapeGroupHover(nakagoShapes, true, Brushes.MediumBlue, 0.6);
            };

            hit.MouseLeave += delegate
            {
                if (nakagoShapes.Count > 0)
                    SetShapeGroupHover(nakagoShapes, false, null, 0.0);
            };

            // tránh attach nhiều lần
            var oldHandler = PopupMenuHandlerStore.Get(hit);
            if (oldHandler != null) hit.MouseLeftButtonDown -= oldHandler;

            Func<FrameworkElement, double> getCanvasLeft = fe =>
            {
                var v = Canvas.GetLeft(fe);
                return double.IsNaN(v) ? 0 : v;
            };

            Func<FrameworkElement, double> getCanvasTop = fe =>
            {
                var v = Canvas.GetTop(fe);
                return double.IsNaN(v) ? 0 : v;
            };

            Func<FrameworkElement, double> getWidth = fe =>
            {
                if (fe.ActualWidth > 0) return fe.ActualWidth;
                if (!double.IsNaN(fe.Width) && fe.Width > 0) return fe.Width;
                return 0;
            };

            Func<FrameworkElement, double> getHeight = fe =>
            {
                if (fe.ActualHeight > 0) return fe.ActualHeight;
                if (!double.IsNaN(fe.Height) && fe.Height > 0) return fe.Height;
                return 0;
            };

            Func<FrameworkElement, double, Tuple<double, double>> getFixedPopupAnchorRightTop = (anchor, gapPx) =>
            {
                var x = getCanvasLeft(anchor) + Math.Max(1, getWidth(anchor)) + gapPx;
                var y = getCanvasTop(anchor);
                return Tuple.Create(x, y);
            };

            MouseButtonEventHandler handler = (s, e) =>
            {
                e.Handled = true;

                HideMenuPreview();
                CloseTaggedPopup(hit);
                CloseGlobalActivePopup();

                Brush normalBg = Brushes.Transparent;
                Brush selectedBg = new SolidColorBrush(Color.FromRgb(210, 225, 255));
                Brush dividerBrush = PopupMenuDividerBrush;

                Func<UIElement, UIElement> withRowDivider = child =>
                    new Border
                    {
                        BorderBrush = dividerBrush,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Child = child
                    };

                ControlTemplate flatBtnTemplate = null;
                Func<ControlTemplate> getFlatBtnTemplate = () =>
                {
                    if (flatBtnTemplate != null) return flatBtnTemplate;

                    var b = new FrameworkElementFactory(typeof(Border));
                    b.SetValue(Border.SnapsToDevicePixelsProperty, true);
                    b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                    b.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                    b.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                    b.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

                    var p = new FrameworkElementFactory(typeof(ContentPresenter));
                    p.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
                    p.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
                    p.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Button.VerticalContentAlignmentProperty));

                    b.AppendChild(p);
                    flatBtnTemplate = new ControlTemplate(typeof(Button)) { VisualTree = b };
                    return flatBtnTemplate;
                };

                Func<UIElement, Border> wrapBox = child =>
                    new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = PopupMenuBorderBrush,
                        BorderThickness = PopupMenuBorderThickness,
                        CornerRadius = new CornerRadius(2),
                        Child = new ContentControl
                        {
                            Content = child,
                            FontSize = SystemFonts.MessageFontSize,
                            FontFamily = SystemFonts.MessageFontFamily
                        }
                    };

                var win = Window.GetWindow(canvas);

                Func<FrameworkElement, Point, bool> isPointInside = (fe, screenPt) =>
                {
                    if (fe == null || !fe.IsVisible) return false;
                    try
                    {
                        var topLeft = fe.PointToScreen(new Point(0, 0));
                        var rect = new Rect(topLeft, new Size(fe.ActualWidth, fe.ActualHeight));
                        return rect.Contains(screenPt);
                    }
                    catch { return false; }
                };

                // ✅ Fixed anchor (right-top of hit)
                var anchor = getFixedPopupAnchorRightTop(hit, 0);
                var menuX = anchor.Item1;
                var menuY = anchor.Item2;

                var mainPop = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = canvas,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                    HorizontalOffset = menuX,
                    VerticalOffset = menuY,
                    AllowsTransparency = true,
                    StaysOpen = true
                };
                _activePopupMenu = mainPop;

                var handle = new StyledPopupHandle { Popup = mainPop };
                hit.Tag = handle;

                MouseButtonEventHandler outsideCloser = null;
                KeyEventHandler escCloser = null;
                EventHandler winDeactivatedCloser = null;
                EventHandler appDeactivatedCloser = null;

                bool isClosing = false;
                Action closeAll = () =>
                {
                    if (isClosing) return;
                    isClosing = true;

                    try { if (ReferenceEquals(hit.Tag, handle)) hit.Tag = null; } catch { }
                    try { if (ReferenceEquals(_activePopupMenu, mainPop)) _activePopupMenu = null; } catch { }

                    HideMenuPreview();

                    try { mainPop.IsOpen = false; } catch { }

                    try
                    {
                        if (win != null && outsideCloser != null) win.RemoveHandler(UIElement.PreviewMouseDownEvent, outsideCloser);
                        if (win != null && escCloser != null) win.RemoveHandler(UIElement.PreviewKeyDownEvent, escCloser);
                    }
                    catch { }

                    try { if (win != null && winDeactivatedCloser != null) win.Deactivated -= winDeactivatedCloser; } catch { }
                    try { if (Application.Current != null && appDeactivatedCloser != null) Application.Current.Deactivated -= appDeactivatedCloser; } catch { }

                    isClosing = false;
                };

                outsideCloser = (ss, ee2) =>
                {
                    if (win == null) { closeAll(); return; }

                    var screenPt = win.PointToScreen(ee2.GetPosition(win));
                    var m = mainPop.Child as FrameworkElement;

                    bool inside =
                        isPointInside(hit, screenPt) ||
                        (m != null && isPointInside(m, screenPt));

                    if (!inside) closeAll();
                };

                escCloser = (ss, ke) =>
                {
                    if (ke.Key == Key.Escape)
                    {
                        closeAll();
                        ke.Handled = true;
                    }
                };

                winDeactivatedCloser = (a, b) => closeAll();
                appDeactivatedCloser = (a, b) => closeAll();

                if (win != null)
                {
                    win.AddHandler(UIElement.PreviewMouseDownEvent, outsideCloser, true);
                    win.AddHandler(UIElement.PreviewKeyDownEvent, escCloser, true);
                    win.Deactivated += winDeactivatedCloser;
                }
                if (Application.Current != null)
                    Application.Current.Deactivated += appDeactivatedCloser;

                Button selectedBtn = null;
                Action<Button> selectBtn = b =>
                {
                    if (selectedBtn != null) selectedBtn.Background = normalBg;
                    selectedBtn = b;
                    if (selectedBtn != null) selectedBtn.Background = selectedBg;
                };

                Func<string, bool, Action, int, Button> makeRowButton = (header, isChecked, onClick, vForPreview) =>
                {
                    var check = new TextBlock
                    {
                        Text = isChecked ? "✓" : "",
                        Width = 18,
                        Foreground = Brushes.Black,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var label = new TextBlock
                    {
                        Text = header ?? "",
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var row = new DockPanel { LastChildFill = true };
                    DockPanel.SetDock(check, Dock.Left);
                    row.Children.Add(check);
                    row.Children.Add(label);

                    var btn = new Button
                    {
                        Content = row,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(12, 6, 12, 6),
                        Background = normalBg,
                        BorderBrush = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        MinWidth = 60,
                        OverridesDefaultStyle = true,
                        Template = getFlatBtnTemplate(),
                        Focusable = false,
                        IsTabStop = false
                    };

                    btn.MouseEnter += (aa, bb) =>
                    {
                        selectBtn(btn);
                        var preview = BuildNakagoPreview(vForPreview);
                        ShowMenuPreviewAlignedToPopupTopRight(mainPop, preview, gapPx: 0);
                    };

                    btn.Click += (aa, bb) =>
                    {
                        HideMenuPreview();
                        if (onClick != null) onClick();
                        closeAll();
                    };

                    return btn;
                };

                var root = new StackPanel { Orientation = Orientation.Vertical };

                for (int i = 1; i <= 8; i++)
                {
                    int v = i;
                    root.Children.Add(withRowDivider(
                        makeRowButton(
                            "形状" + v.ToString(CultureInfo.InvariantCulture),
                            (v == currentShape),
                            () =>
                            {
                                if (ApplySpanCentralIntermediateShape(item, spanIndex, v))
                                    Redraw(canvas, item);
                            },
                            v
                        )));
                }

                mainPop.Child = wrapBox(root);

                hit.Dispatcher.BeginInvoke(new Action(() =>
                {
                    mainPop.IsOpen = true;
                    selectBtn(null);
                }), DispatcherPriority.Input);
            };

            hit.MouseLeftButtonDown += handler;
            PopupMenuHandlerStore.Set(hit, handler);
        }
        // Chỉnh sửa vào lúc 9:37 ngày 13/1/2026
        // ===== [NEW] Hitbox theo tọa độ world (mm) =====
        private Rectangle CreateWorldHitRect(Canvas canvas, WCTransform T,
                                             double centerXmm, double centerYmm,
                                             double widthMm, double heightMm,
                                             int zIndex,
                                             double padMm,
                                             double radiusPx)
        {
            if (canvas == null) return null;

            double wMm = widthMm + padMm * 2.0;
            double hMm = heightMm + padMm * 2.0;

            var p0 = T.P(centerXmm - wMm / 2.0, centerYmm - hMm / 2.0);
            var p1 = T.P(centerXmm + wMm / 2.0, centerYmm + hMm / 2.0);

            double left = Math.Min(p0.X, p1.X);
            double top = Math.Min(p0.Y, p1.Y);
            double w = Math.Abs(p1.X - p0.X);
            double h = Math.Abs(p1.Y - p0.Y);

            var r = new Rectangle
            {
                Width = Math.Max(2, w),
                Height = Math.Max(2, h),
                Fill = Brushes.Transparent,
                Stroke = Brushes.Transparent,
                StrokeThickness = 1.0,
                RadiusX = Math.Max(0, radiusPx),
                RadiusY = Math.Max(0, radiusPx),
                IsHitTestVisible = true,
                Cursor = Cursors.Hand
            };

            Canvas.SetLeft(r, left);
            Canvas.SetTop(r, top);
            Panel.SetZIndex(r, zIndex);
            canvas.Children.Add(r);
            return r;
        }
        private sealed class HoverFrameState
        {
            public Brush Stroke;
            public Brush Fill;
            public double Thickness;
        }
        private void EnableHoverFrame(Rectangle hit,
                                      Brush hoverStroke,
                                      Brush hoverFill,
                                      double hoverThickness)
        {
            if (hit == null) return;

            var baseState = new HoverFrameState
            {
                Stroke = hit.Stroke,
                Fill = hit.Fill,
                Thickness = hit.StrokeThickness
            };

            if (hoverStroke == null) hoverStroke = Brushes.MediumBlue;
            if (hoverFill == null) hoverFill = new SolidColorBrush(Color.FromArgb(25, 255, 255, 0));
            if (hoverThickness <= 0) hoverThickness = 2.0;

            hit.MouseEnter += (s, e) =>
            {
                hit.Stroke = hoverStroke;
                hit.Fill = hoverFill;
                hit.StrokeThickness = hoverThickness;
            };

            hit.MouseLeave += (s, e) =>
            {
                hit.Stroke = baseState.Stroke;
                hit.Fill = baseState.Fill;
                hit.StrokeThickness = baseState.Thickness;
            };
        }
        private void SetShapeGroupHover(IEnumerable<Shape> shapes, bool hoverOn, Brush hoverStroke, double thicknessDelta)
        {
            if (shapes == null) return;
            if (hoverStroke == null) hoverStroke = Brushes.OrangeRed;

            foreach (var s in shapes)
            {
                if (s == null) continue;
                EnsureShapeHoverState(s);

                ShapeHoverState st;
                if (!_shapeHoverStates.TryGetValue(s, out st)) continue;

                if (hoverOn)
                {
                    s.Stroke = hoverStroke;
                    s.StrokeThickness = Math.Max(0.2, st.Thickness + thicknessDelta);
                }
                else
                {
                    s.Stroke = st.Stroke;
                    s.StrokeThickness = st.Thickness;
                    s.StrokeDashArray = st.Dash;
                }
            }
        }
        private Popup _menuPreviewPopup;
        private void HideMenuPreview()
        {
            if (_menuPreviewPopup == null) return;
            _menuPreviewPopup.IsOpen = false;
            _menuPreviewPopup.Child = null;
        }
        // Chỉnh ngày 12/1/2026 lúc 11:06
        // ===================== [ADD] Nakago preview builders =====================
        private FrameworkElement BuildNakagoPreview(int shape)
        {
            // Preview size (px)
            const double w = 60;
            const double h = 182;

            var border = new Border
            {
                Width = w,
                Height = h,
                Background = Brushes.White,
                BorderBrush = PopupMenuBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(6)
            };

            var canvas = new Canvas
            {
                Width = w + 10,
                Height = h - 12,
                Background = Brushes.Transparent
            };

            border.Child = canvas;

            // Light title
            var title = new TextBlock
            {
                Text = "中子 " + shape.ToString(CultureInfo.InvariantCulture),
                FontSize = 11,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(title, 2);
            Canvas.SetTop(title, 2);
            canvas.Children.Add(title);

            // Draw area
            double cx = (canvas.Width) / 2.0;
            double top = 22 + 15;
            double bottom = canvas.Height - 10;
            double midY = (top + bottom) / 2.0;

            // Common params
            double r = 4;
            double leftX = cx - 20;
            double rightX = cx + 40;

            double offset67 = -5;

            // stroke for preview
            Brush stroke = Brushes.DeepSkyBlue;
            double th = 2.0;

            // Helper local
            void L(double x1, double y1, double x2, double y2) => PreviewLine(canvas, x1, y1, x2, y2, stroke, th);
            void A(double acx, double acy, double ar, double sDeg, double eDeg) => PreviewArc(canvas, acx, acy, ar, sDeg, eDeg, stroke, th);

            // NOTE:
            // Đây là preview mô phỏng rõ ràng (không cần đúng tuyệt đối mm),
            // mục tiêu là người dùng nhìn ra “dáng” để chọn nhanh.
            switch (shape)
            {
                case 1:
                    // 2 cột + 2 arc đối xứng
                    L(leftX, top + 10, leftX, midY + 25);
                    A(leftX + r, top + 10, r, 180, 0);
                    L(cx - 12, top + 10, cx - 12, top + 10 + r);
                    A(leftX + r, midY + 25, r, 0, 180);
                    L(leftX + r * 2, midY + 25, leftX + r * 2, midY + 25 - r);
                    break;

                case 2:
                    // thanh ngang ngắn + arc 1/4 + cột + arc nửa + cột
                    L(leftX, top + 10, leftX, midY + 25);
                    A(leftX + r, top + 10, r, 180, -90);

                    L(leftX + r, top + 10 - r, leftX + r + 10, top + 10 - r);

                    A(leftX + r, midY + 25, r, 0, 180);
                    L(leftX + r * 2, midY + 25, leftX + r * 2, midY + 25 - r);
                    break;

                case 3:
                    // giống 2 nhưng arc dưới là 1/4
                    L(leftX, top + 10, leftX, midY + 25);
                    A(leftX + r, top + 10, r, 180, -90);

                    L(leftX + r, top + 10 - r, leftX + r + 10, top + 10 - r);

                    A(leftX + r, midY + 25, r, 90, 180);
                    L(leftX + r, midY + 25 + r, leftX + r + 10, midY + 25 + r);

                    break;

                case 4:
                    // 2 đoạn chéo đối xứng + arc trên 3/4 + arc dưới 3/4
                    L(leftX + r + r / 2, top + 10 - r, leftX + 12, top + 18 - r);
                    A(leftX + r, top + 10, r, 180, 315);

                    L(leftX, top + 10, leftX, midY + 25);

                    A(leftX + r, midY + 25, r, 45, 180);
                    L(leftX + r + r / 2, midY + 25 + r, leftX + 12, midY + 17 + r);
                    break;

                case 5:
                    // thanh ngang + arc 1/4 trên + cột + arc 3/4 dưới + đoạn chéo
                    //L(leftX + r + r / 2, top + 10 - r, leftX + 12, top + 18 - r);
                    L(leftX + r, top + 10 - r, leftX + r + 10, top + 10 - r);
                    A(leftX + r, top + 10, r, 180, -90);

                    L(leftX, top + 10, leftX, midY + 25);

                    A(leftX + r, midY + 25, r, 45, 180);
                    L(leftX + r + r / 2, midY + 25 + r, leftX + 12, midY + 17 + r);
                    break;

                case 6:
                    // phức tạp: 2 cột + nhiều arc + 2 line chain (mô phỏng)
                    A(leftX + r + offset67, top + 10, r, 180, 270);
                    L(leftX + offset67, top + 10, leftX + offset67, midY + 25);
                    L(leftX + 25 + offset67, top + 10, leftX + 25 + offset67, midY + 25);
                    A(leftX + r + offset67, midY + 25, r, 90, 180);
                    A(leftX + 25 - r + offset67, top + 10, r, 225, 45);
                    A(leftX + 25 - r + offset67, midY + 25, r, 0, 90);
                    L(leftX + 25 - 8 - r + offset67, top + 16 - r, leftX + 25 - r - r / 2 + offset67, top + 10 - r);
                    L(leftX + 25 - 4 - r + offset67, top + 16 + 2, leftX + 25 - 1 + offset67, top + 12);
                    //// chain hints
                    PreviewLine(canvas, leftX + r + offset67, top + 10 - r, leftX + 25 - r + offset67, top + 10 - r, stroke, 1.4);
                    PreviewLine(canvas, leftX + r + offset67, midY + 25 + r, leftX + 25 - r + offset67, midY + 25 + r, stroke, 1.4);
                    break;

                case 7:
                    // giống 6 nhưng bớt vài nét
                    A(leftX + r + offset67, top + 10, r, 180, 270);
                    L(leftX + offset67, top + 10, leftX + offset67, midY + 25);
                    L(leftX + 25 + offset67, top + 10, leftX + 25 + offset67, midY + 25);
                    A(leftX + r + offset67, midY + 25, r, 90, 180);
                    A(leftX + 25 - r + offset67, top + 10, r, 225, 0);
                    A(leftX + 25 - r + offset67, midY + 25, r, 0, 90);
                    L(leftX + 25 - 8 - r + offset67, top + 16 - r, leftX + 25 - r - r / 2 + offset67, top + 10 - r);
                    //L(leftX + 25 - 4 - r + offset67, top + 16 + 2, leftX + 25 - 1 + offset67, top + 12);
                    //// chain hints
                    PreviewLine(canvas, leftX + r + offset67, top + 10 - r, leftX + 25 - r + offset67, top + 10 - r, stroke, 1.4);
                    PreviewLine(canvas, leftX + r + offset67, midY + 25 + r, leftX + 25 - r + offset67, midY + 25 + r, stroke, 1.4);
                    break;

                case 8:
                    // case 8: không vẽ gì (theo yêu cầu hiện tại) => để trống, chỉ có label
                    break;
            }

            return border;
        }

        private void ShowMenuPreviewAlignedToMenuTopRight(ContextMenu menu, UIElement content, double gapPx)
        {
            if (menu == null || content == null) return;

            EnsureMenuPreviewPopup();

            _menuPreviewPopup.PlacementTarget = menu;
            _menuPreviewPopup.Child = content;

            _menuPreviewPopup.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) =>
                {
                    // x = cạnh phải menu, y = cạnh trên menu
                    var p = new Point(targetSize.Width + gapPx, 0);
                    return new[] { new CustomPopupPlacement(p, PopupPrimaryAxis.None) };
                };

            _menuPreviewPopup.IsOpen = true;
        }
        // ===== [NEW] Apply shape 中子 (1..8) =====
        // pattern giống ApplyEndWidthStopValues / ApplyCentralIntermediateValues 【turn48file4†L12-L20】【turn48file14†L27-L39】
        private bool ApplyCentralNakagoShape(string kai, string gSym, int shape)
        {
            var beam = FindBeamBySymbol(kai, gSym);
            if (beam == null) return false;

            var layout = beam.梁の配置 ?? (beam.梁の配置 = new Z梁の配置());
            string v = shape.ToString(CultureInfo.InvariantCulture);

            // property này bạn đang dùng: zcfg?.中央中子筋形
            if (!string.Equals(layout.中央中子筋形, v, StringComparison.Ordinal))
            {
                layout.中央中子筋形 = v;
                return true;
            }
            return false;
        }
        // ===== [NEW] Hover state cho Shape (Line/Path/Polyline/...) =====
        private sealed class ShapeHoverState
        {
            public Brush Stroke;
            public double Thickness;
            public DoubleCollection Dash;
        }

        private readonly ConditionalWeakTable<Shape, ShapeHoverState> _shapeHoverStates
            = new ConditionalWeakTable<Shape, ShapeHoverState>();

        private void EnsureShapeHoverState(Shape s)
        {
            if (s == null) return;

            ShapeHoverState st;
            if (_shapeHoverStates.TryGetValue(s, out st)) return;

            _shapeHoverStates.Add(s, new ShapeHoverState
            {
                Stroke = s.Stroke,
                Thickness = s.StrokeThickness,
                Dash = s.StrokeDashArray
            });
        }
        private static void PreviewLine(Canvas canvas, double x1, double y1, double x2, double y2, Brush stroke, double thickness)
        {
            var ln = new Line
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                Stroke = stroke ?? Brushes.Black,
                StrokeThickness = thickness
            };
            canvas.Children.Add(ln);
        }
        private static void PreviewArc(Canvas canvas, double cx, double cy, double r, double startDeg, double endDeg, Brush stroke, double thickness)
        {
            double a0 = startDeg * Math.PI / 180.0;
            double a1 = endDeg * Math.PI / 180.0;

            var p0 = new Point(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0));
            var p1 = new Point(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1));

            double delta = endDeg - startDeg;
            while (delta < 0) delta += 360;
            while (delta >= 360) delta -= 360;

            bool large = delta > 180.0;

            var fig = new PathFigure { StartPoint = p0, IsClosed = false, IsFilled = false };
            fig.Segments.Add(new ArcSegment
            {
                Point = p1,
                Size = new Size(r, r),
                IsLargeArc = large,
                SweepDirection = SweepDirection.Clockwise
            });

            var geo = new PathGeometry();
            geo.Figures.Add(fig);

            var path = new Path
            {
                Data = geo,
                Stroke = stroke ?? Brushes.Black,
                StrokeThickness = thickness,
                Fill = Brushes.Transparent
            };

            canvas.Children.Add(path);
        }
        // Preview popup dùng chung cho menu
        private void EnsureMenuPreviewPopup()
        {
            if (_menuPreviewPopup != null) return;

            _menuPreviewPopup = new Popup
            {
                AllowsTransparency = true,
                StaysOpen = true,
                Placement = PlacementMode.Custom
            };
        }

        // ===== [ADD] Apply 中央スタラップ形 (1..9) =====
        private bool ApplyCentralStirrupShape(string kai, string gSym, int shape)
        {
            if (shape < 1 || shape > 9) return false;

            var beam = FindBeamBySymbol(kai, gSym);
            if (beam == null) return false;

            var layout = beam.梁の配置 ?? (beam.梁の配置 = new Z梁の配置());
            string v = shape.ToString(CultureInfo.InvariantCulture);

            if (string.Equals(layout.中央スタラップ形, v, StringComparison.Ordinal))
                return false;

            layout.中央スタラップ形 = v;
            return true;
        }
        // ===== [ADD] Hover + Click menu cho nhóm スタラップ筋 =====　1
        // File: 梁配筋施工図.cs

        private void MakeCentralStirrupShapeEditable(
            Canvas canvas,
            WCTransform T,
            GridBotsecozu item,
            string kai,
            string gSym,
            int spanIndex,
            int currentShape,
            IList<Shape> shapes,
            double hitCenterXmm,
            double hitCenterYmm,
            double hitWidthMm,
            double hitHeightMm)
        {
            if (canvas == null) return;
            if (shapes == null) shapes = new List<Shape>();

            var hit = CreateWorldHitRect(
                canvas, T,
                hitCenterXmm, hitCenterYmm,
                hitWidthMm, hitHeightMm,
                2500,
                padMm: 60,
                radiusPx: 0);
            if (hit == null) return;

            var baseStroke = hit.Stroke;
            var baseFill = hit.Fill;
            var baseThickness = hit.StrokeThickness;

            Brush hoverStroke = Brushes.Blue;
            var hoverFill = new SolidColorBrush(Color.FromArgb(100, 30, 144, 255));

            EnableHoverFrame(
                hit,
                hoverStroke: hoverStroke,
                hoverFill: hoverFill,
                hoverThickness: 2.0);

            hit.MouseEnter += (s, e) =>
            {
                if (shapes.Count > 0)
                    SetShapeGroupHover(shapes, true, hoverStroke, 0.6);
                else
                {
                    hit.Stroke = hoverStroke;
                    hit.StrokeThickness = Math.Max(1.0, baseThickness + 1.0);
                    hit.Fill = new SolidColorBrush(Color.FromArgb(50, 30, 144, 255));
                }
            };

            hit.MouseLeave += (s, e) =>
            {
                if (shapes.Count > 0)
                    SetShapeGroupHover(shapes, false, null, 0.0);
                else
                {
                    hit.Stroke = baseStroke;
                    hit.StrokeThickness = baseThickness;
                    hit.Fill = baseFill;
                }
            };

            var oldHandler = PopupMenuHandlerStore.Get(hit);
            if (oldHandler != null) hit.MouseLeftButtonDown -= oldHandler;

            Func<FrameworkElement, double> getCanvasLeft = fe =>
            {
                var v = Canvas.GetLeft(fe);
                return double.IsNaN(v) ? 0 : v;
            };

            Func<FrameworkElement, double> getCanvasTop = fe =>
            {
                var v = Canvas.GetTop(fe);
                return double.IsNaN(v) ? 0 : v;
            };

            Func<FrameworkElement, double> getWidth = fe =>
            {
                if (fe.ActualWidth > 0) return fe.ActualWidth;
                if (!double.IsNaN(fe.Width) && fe.Width > 0) return fe.Width;
                return 0;
            };

            Func<FrameworkElement, Tuple<double, double>> getFixedAnchorRightTop = anchor =>
            {
                var x = getCanvasLeft(anchor) + Math.Max(1, getWidth(anchor));
                var y = getCanvasTop(anchor);
                return Tuple.Create(x, y);
            };

            MouseButtonEventHandler handler = (s, e) =>
            {
                e.Handled = true;

                HideMenuPreview();
                CloseTaggedPopup(hit);
                CloseGlobalActivePopup();

                Brush normalBg = Brushes.Transparent;
                Brush selectedBg = new SolidColorBrush(Color.FromRgb(210, 225, 255));
                Brush dividerBrush = PopupMenuDividerBrush;

                Func<UIElement, UIElement> withRowDivider = child =>
                    new Border
                    {
                        BorderBrush = dividerBrush,
                        BorderThickness = new Thickness(0, 0, 0, 1),
                        Child = child
                    };

                ControlTemplate flatBtnTemplate = null;
                Func<ControlTemplate> getFlatBtnTemplate = () =>
                {
                    if (flatBtnTemplate != null) return flatBtnTemplate;

                    var b = new FrameworkElementFactory(typeof(Border));
                    b.SetValue(Border.SnapsToDevicePixelsProperty, true);
                    b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                    b.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                    b.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                    b.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

                    var p = new FrameworkElementFactory(typeof(ContentPresenter));
                    p.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
                    p.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
                    p.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Button.VerticalContentAlignmentProperty));

                    b.AppendChild(p);
                    flatBtnTemplate = new ControlTemplate(typeof(Button)) { VisualTree = b };
                    return flatBtnTemplate;
                };

                Func<UIElement, Border> wrapBox = child =>
                    new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = PopupMenuBorderBrush,
                        BorderThickness = PopupMenuBorderThickness,
                        CornerRadius = new CornerRadius(2),
                        Child = new ContentControl
                        {
                            Content = child,
                            FontSize = SystemFonts.MessageFontSize,
                            FontFamily = SystemFonts.MessageFontFamily
                        }
                    };

                var win = Window.GetWindow(canvas);

                Func<FrameworkElement, Point, bool> isPointInside = (fe, screenPt) =>
                {
                    if (fe == null || !fe.IsVisible) return false;
                    try
                    {
                        var topLeft = fe.PointToScreen(new Point(0, 0));
                        var rect = new Rect(topLeft, new Size(fe.ActualWidth, fe.ActualHeight));
                        return rect.Contains(screenPt);
                    }
                    catch { return false; }
                };

                // ✅ Fixed position (right-top of hit)
                var anchor = getFixedAnchorRightTop(hit);
                var menuX = anchor.Item1;
                var menuY = anchor.Item2;

                // ✅ CHANGED: Relative to canvas, not MousePoint
                var mainPop = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = canvas,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                    HorizontalOffset = menuX,
                    VerticalOffset = menuY,
                    AllowsTransparency = true,
                    StaysOpen = true
                };
                _activePopupMenu = mainPop;

                var handle = new StyledPopupHandle { Popup = mainPop };
                hit.Tag = handle;

                MouseButtonEventHandler outsideCloser = null;
                KeyEventHandler escCloser = null;
                EventHandler winDeactivatedCloser = null;
                EventHandler appDeactivatedCloser = null;

                bool isClosing = false;
                Action closeAll = () =>
                {
                    if (isClosing) return;
                    isClosing = true;

                    try { if (ReferenceEquals(hit.Tag, handle)) hit.Tag = null; } catch { }
                    try { if (ReferenceEquals(_activePopupMenu, mainPop)) _activePopupMenu = null; } catch { }

                    HideMenuPreview();

                    try { mainPop.IsOpen = false; } catch { }

                    try
                    {
                        if (win != null && outsideCloser != null) win.RemoveHandler(UIElement.PreviewMouseDownEvent, outsideCloser);
                        if (win != null && escCloser != null) win.RemoveHandler(UIElement.PreviewKeyDownEvent, escCloser);
                    }
                    catch { }

                    try { if (win != null && winDeactivatedCloser != null) win.Deactivated -= winDeactivatedCloser; } catch { }
                    try { if (Application.Current != null && appDeactivatedCloser != null) Application.Current.Deactivated -= appDeactivatedCloser; } catch { }

                    isClosing = false;
                };

                outsideCloser = (ss, ee2) =>
                {
                    if (win == null) { closeAll(); return; }
                    var screenPt = win.PointToScreen(ee2.GetPosition(win));

                    bool inside =
                        isPointInside(hit, screenPt) ||
                        (mainPop.Child is FrameworkElement m && isPointInside(m, screenPt));

                    if (!inside) closeAll();
                };

                escCloser = (ss, ke) =>
                {
                    if (ke.Key == Key.Escape)
                    {
                        closeAll();
                        ke.Handled = true;
                    }
                };

                winDeactivatedCloser = (a, b) => closeAll();
                appDeactivatedCloser = (a, b) => closeAll();

                if (win != null)
                {
                    win.AddHandler(UIElement.PreviewMouseDownEvent, outsideCloser, true);
                    win.AddHandler(UIElement.PreviewKeyDownEvent, escCloser, true);
                    win.Deactivated += winDeactivatedCloser;
                }
                if (Application.Current != null)
                    Application.Current.Deactivated += appDeactivatedCloser;

                Button selectedBtn = null;
                Action<Button> selectBtn = b =>
                {
                    if (selectedBtn != null) selectedBtn.Background = normalBg;
                    selectedBtn = b;
                    if (selectedBtn != null) selectedBtn.Background = selectedBg;
                };

                Func<string, bool, Action, int, Button> makeRowButton = (header, isChecked, onClick, vForPreview) =>
                {
                    var check = new TextBlock
                    {
                        Text = isChecked ? "✓" : "",
                        Width = 18,
                        Foreground = Brushes.Black,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var label = new TextBlock
                    {
                        Text = header ?? "",
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var row = new DockPanel { LastChildFill = true };
                    DockPanel.SetDock(check, Dock.Left);
                    row.Children.Add(check);
                    row.Children.Add(label);

                    var btn = new Button
                    {
                        Content = row,
                        HorizontalContentAlignment = HorizontalAlignment.Stretch,
                        VerticalContentAlignment = VerticalAlignment.Center,
                        Padding = new Thickness(12, 6, 12, 6),
                        Background = normalBg,
                        BorderBrush = Brushes.Transparent,
                        BorderThickness = new Thickness(0),
                        MinWidth = 60,
                        OverridesDefaultStyle = true,
                        Template = getFlatBtnTemplate(),
                        Focusable = false,
                        IsTabStop = false
                    };

                    btn.MouseEnter += (aa, bb) =>
                    {
                        selectBtn(btn);
                        var preview = BuildStirrupPreview(vForPreview);
                        ShowMenuPreviewAlignedToPopupTopRight(mainPop, preview, gapPx: 0);
                    };

                    btn.Click += (aa, bb) =>
                    {
                        HideMenuPreview();
                        if (onClick != null) onClick();
                        closeAll();
                    };

                    return btn;
                };

                var root = new StackPanel { Orientation = Orientation.Vertical };

                for (int i = 1; i <= 9; i++)
                {
                    int v = i;
                    root.Children.Add(withRowDivider(
                        makeRowButton(
                            "形状" + v.ToString(CultureInfo.InvariantCulture),
                            (v == currentShape),
                            () =>
                            {
                                if (ApplySpanCentralStirrupShape(item, spanIndex, v))
                                    Redraw(canvas, item);
                            },
                            v
                        )));
                }

                mainPop.Child = wrapBox(root);

                hit.Dispatcher.BeginInvoke(new Action(() =>
                {
                    mainPop.IsOpen = true;
                    selectBtn(null);
                }), DispatcherPriority.Input);
            };

            hit.MouseLeftButtonDown += handler;
            PopupMenuHandlerStore.Set(hit, handler);
        }
        private void ShowMenuPreviewAlignedToPopupTopRight(Popup popupMenu, UIElement content, double gapPx)
        {
            if (popupMenu == null || content == null) return;

            var target = popupMenu.Child as FrameworkElement;
            if (target == null) return;

            EnsureMenuPreviewPopup();

            _menuPreviewPopup.PlacementTarget = target;
            _menuPreviewPopup.Child = content;

            _menuPreviewPopup.CustomPopupPlacementCallback =
                (popupSize, targetSize, offset) =>
                {
                    var p = new Point(targetSize.Width + gapPx, 0);
                    return new[] { new CustomPopupPlacement(p, PopupPrimaryAxis.None) };
                };

            _menuPreviewPopup.IsOpen = true;
        }

        // Chỉnh sửa vào lúc 9:37 ngày 13/1/2026

        // ===== [ADD] Stirrup preview builders (hover menu item -> show picture) =====

        private FrameworkElement BuildStirrupPreview(int shape)
        {
            const double w = 150;
            const double h = 204;

            var border = new Border
            {
                Width = w,
                Height = h,
                Background = Brushes.White,
                BorderBrush = PopupMenuBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(0),
                Padding = new Thickness(6)
            };

            var canvas = new Canvas
            {
                Width = w - 12,
                Height = h - 12,
                Background = Brushes.Transparent
            };
            border.Child = canvas;

            var title = new TextBlock
            {
                Text = "スタラップ " + shape.ToString(CultureInfo.InvariantCulture),
                FontSize = 11,
                Foreground = Brushes.Black
            };
            Canvas.SetLeft(title, 4);
            Canvas.SetTop(title, 2);
            canvas.Children.Add(title);

            // draw area
            double left = 18;
            double top = 26;
            double right = canvas.Width - 18;
            double bottom = canvas.Height - 18;

            Brush stroke = Brushes.MediumBlue;
            Brush strokeDeepSkyBlue = Brushes.DeepSkyBlue;
            Brush strokeRed = Brushes.Red;
            double th = 2.0;
            double r = 8;

            void L(double x1, double y1, double x2, double y2) => PreviewLine(canvas, x1, y1, x2, y2, stroke, th);
            void A(double cx, double cy, double ar, double sDeg, double eDeg) => PreviewArc(canvas, cx, cy, ar, sDeg, eDeg, stroke, th);

            void LDeepSkyBlue(double x1, double y1, double x2, double y2) => PreviewLine(canvas, x1, y1, x2, y2, strokeDeepSkyBlue, th);
            void ADeepSkyBlue(double cx, double cy, double ar, double sDeg, double eDeg) => PreviewArc(canvas, cx, cy, ar, sDeg, eDeg, strokeDeepSkyBlue, th);


            void LRed(double x1, double y1, double x2, double y2) => PreviewLine(canvas, x1, y1, x2, y2, strokeRed, th);
            void ARed(double cx, double cy, double ar, double sDeg, double eDeg) => PreviewArc(canvas, cx, cy, ar, sDeg, eDeg, strokeRed, th);


            // NOTE: đây là icon minh họa (không nhất thiết đúng 100% mm),
            // mục tiêu là “nhìn dáng” để chọn nhanh trước khi click.
            double rx = right - r * 2;
            double ry = top + r * 2 - 1;

            // common “stirrup rectangle”
            void Rect(double insetX, double insetY)
            {
                double x1 = left + insetX;
                double y1 = top + insetY;
                double x2 = right - insetX;
                double y2 = bottom - insetY;
                L(x1, y1, x2, y1);
                L(x2, y1, x2, y2);
                L(x2, y2, x1, y2);
                L(x1, y2, x1, y1);
            }

            switch (shape)
            {
                case 1:
                    Rect(8, 6);
                    ADeepSkyBlue(rx, ry - 1, r, -135, 45);
                    LDeepSkyBlue(rx - 16, top + 17, rx - 4, top + 7);
                    LDeepSkyBlue(rx - 6, top + 21 + r, rx + 6, top + 11 + r);

                    break;

                case 2:
                    Rect(8, 6);
                    ADeepSkyBlue(rx, ry - 1, r, -135, 0);
                    LDeepSkyBlue(rx - 16, top + 17, rx - 4, top + 7);
                    LDeepSkyBlue(right - r, top + r + 6, right - r, top + r + 19);
                    break;

                case 3:
                    Rect(8, 6);
                    // Trái hooks
                    ARed(left + r * 2, ry - 1, r, -180, -90);
                    LRed(left + r, top + r * 2 - 2, left + r, top + r * 2 + 6);
                    LDeepSkyBlue(left + r * 2 + 16, top + 17, left + r * 2 + 4, top + 7);


                    // Giữa
                    LRed(left + r * 2, top + 6, right - r * 2, top + 6);


                    // Phải
                    ARed(rx, ry - 1, r, -90, 0);
                    LDeepSkyBlue(rx - 16, top + 17, rx - 4, top + 7);
                    LRed(right - r, top + r * 2 - 2, right - r, top + r * 2 + 6);
                    break;

                case 4:
                    Rect(8, 6);
                    // Trái hooks
                    ARed(left + r * 2, ry - 1, r, -180, -90);
                    LRed(left + r, top + r * 2 - 2, left + r, top + r * 2 + 6);
                    LDeepSkyBlue(left + r * 2 + 16, top + 17, left + r * 2 + 4, top + 7);


                    // Giữa
                    LRed(left + r * 2, top + 6, right - r * 2, top + 6);


                    // Phải
                    ARed(rx, ry - 1, r, -90, 45);
                    LDeepSkyBlue(rx - 16, top + 17, rx - 4, top + 7);
                    LRed(rx - 6, top + 21 + r, rx + 6, top + 11 + r);

                    //LRed(right - r, top + r * 2 - 2, right - r, top + r * 2 + 6);
                    break;

                case 5:
                    Rect(8, 6);
                    // Trái hooks
                    ARed(left + r * 2, ry - 1, r, -180, -90);
                    ADeepSkyBlue(left + r * 2, ry - 1, r, -90, 0);
                    LRed(left + r, top + r * 2 - 2, left + r, top + r * 2 + 6);
                    LDeepSkyBlue(left + r + r * 2, top + r * 2 - 2, left + r + r * 2, top + r * 2 + 6);

                    //LDeepSkyBlue(left + r * 2 + 16, top + 17, left + r * 2 + 4, top + 7);


                    // Giữa
                    LRed(left + r * 2, top + 6, right - r * 2, top + 6);


                    // Phải
                    ARed(rx, ry - 1, r, -90, 0);
                    ADeepSkyBlue(rx, ry - 1, r, -180, -115);
                    //LDeepSkyBlue(rx - 16, top + 17, rx - 4, top + 7);
                    LRed(right - r, top + r * 2 - 2, right - r, top + r * 2 + 6);
                    LDeepSkyBlue(right - r - r * 2, top + r * 2 - 2, right - r - r * 2, top + r * 2 + 6);

                    break;

                case 6:
                    Rect(8, 6);
                    // Trái hooks
                    ARed(left + r * 2, ry - 1, r, -180, -90);
                    ADeepSkyBlue(left + r * 2, ry - 1, r, -90, 0);
                    LRed(left + r, top + r * 2 - 2, left + r, top + r * 2 + 6);
                    LDeepSkyBlue(left + r + r * 2, top + r * 2 - 2, left + r + r * 2, top + r * 2 + 6);

                    //LDeepSkyBlue(left + r * 2 + 16, top + 17, left + r * 2 + 4, top + 7);


                    // Giữa
                    LRed(left + r * 2, top + 6, right - r * 2, top + 6);


                    // Phải
                    ADeepSkyBlue(rx, ry - 1, r, -180, -115);
                    LDeepSkyBlue(right - r - r * 2, top + r * 2 - 2, right - r - r * 2, top + r * 2 + 6);
                    ARed(rx, ry - 1, r, -90, 45);
                    LRed(rx - 6, top + 21 + r, rx + 6, top + 11 + r);

                    break;

                case 7:
                    Rect(8, 6);
                    // Trái hooks
                    ARed(left + r * 2, ry - 1, r, -225, -90);
                    LRed(left + r * 2 + 6, top + 21 + r, left + r * 2 - 6, top + 11 + r);
                    LDeepSkyBlue(left + r * 2 + 16, top + 17, left + r * 2 + 4, top + 7);


                    // Giữa
                    LRed(left + r * 2, top + 6, right - r * 2, top + 6);


                    // Phải
                    ARed(rx, ry - 1, r, -90, 45);
                    LDeepSkyBlue(rx - 16, top + 17, rx - 4, top + 7);
                    LRed(rx - 6, top + 21 + r, rx + 6, top + 11 + r);

                    //LRed(right - r, top + r * 2 - 2, right - r, top + r * 2 + 6);
                    break;


                case 8:
                    Rect(8, 6);
                    // Trái hooks
                    ARed(left + r * 2, ry - 1, r, -225, -90);
                    LRed(left + r * 2 + 6, top + 21 + r, left + r * 2 - 6, top + 11 + r);
                    ADeepSkyBlue(left + r * 2, ry - 1, r, -90, 0);
                    LDeepSkyBlue(left + r + r * 2, top + r * 2 - 2, left + r + r * 2, top + r * 2 + 6);


                    //LDeepSkyBlue(left + r * 2 + 16, top + 17, left + r * 2 + 4, top + 7);


                    // Giữa
                    LRed(left + r * 2, top + 6, right - r * 2, top + 6);


                    // Phải
                    ARed(rx, ry - 1, r, -90, 45);
                    //LDeepSkyBlue(rx - 16, top + 17, rx - 4, top + 7);
                    LRed(rx - 6, top + 21 + r, rx + 6, top + 11 + r);
                    ADeepSkyBlue(rx, ry - 1, r, -180, -115);
                    LDeepSkyBlue(right - r - r * 2, top + r * 2 - 2, right - r - r * 2, top + r * 2 + 6);



                    //LRed(right - r, top + r * 2 - 2, right - r, top + r * 2 + 6);
                    break;


                case 9:
                    Rect(8, 6);

                    // case 9: không vẽ gì (theo logic của bạn) -> chỉ title
                    break;
            }

            return border;
        }

        #region PopupMenu (ContextMenu replacement)

        private Popup _activePopupMenu;

        private interface IPopupMenuEntry { }

        private sealed class PopupMenuSeparator : IPopupMenuEntry { }

        private sealed class PopupMenuItemEntry : IPopupMenuEntry
        {
            public string Header { get; }
            public bool IsEnabled { get; }
            public bool IsChecked { get; }
            public Action OnClick { get; }
            public Action OnHover { get; }

            public PopupMenuItemEntry(
                string header,
                Action onClick,
                bool isEnabled = true,
                bool isChecked = false,
                Action onHover = null)
            {
                Header = header ?? "";
                OnClick = onClick ?? (() => { });
                IsEnabled = isEnabled;
                IsChecked = isChecked;
                OnHover = onHover;
            }
        }

        private static class PopupMenuHandlerStore
        {
            // Owner type is this static helper => no "YourCanvasClassNameHere" needed.
            public static readonly DependencyProperty ClickHandlerProperty =
                DependencyProperty.RegisterAttached(
                    "ClickHandler",
                    typeof(MouseButtonEventHandler),
                    typeof(PopupMenuHandlerStore),
                    new PropertyMetadata(null));

            public static void Set(DependencyObject obj, MouseButtonEventHandler value) =>
                obj.SetValue(ClickHandlerProperty, value);

            public static MouseButtonEventHandler Get(DependencyObject obj) =>
                (MouseButtonEventHandler)obj.GetValue(ClickHandlerProperty);
        }


        #endregion

        // ===== 2) SỬA HÀM: thêm 2 param biên (tbDia, tbMat) và dùng inline editor cho ピッチ =====
        private void MakeCentralStirrupEditable(
            TextBlock tbDia, TextBlock tbPitch, TextBlock tbMat,
            Canvas canvas, WCTransform T,
            double wx, double wy,
            GridBotsecozu item,
            string kai, string gSym,
            int spanIndex,
            string diameter, string pitch, string material,
            bool showMaterial)
        {
            if (tbDia == null || tbPitch == null || tbMat == null || canvas == null) return;

            // hover box chung (giữ style giống hiện tại)
            EnableEditableHoverWithBox_Group3(
                tbDia, tbPitch, tbMat, canvas,
                hoverForeground: Brushes.Blue,
                hoverFill: new SolidColorBrush(Color.FromArgb(100, 30, 144, 255)),
                hoverStroke: Brushes.Blue);

            TextBlock[] all = { tbDia, tbPitch, tbMat };
            foreach (var tb in all)
            {
                tb.Cursor = Cursors.Hand;
                if (tb.Background == null) tb.Background = Brushes.Transparent;
                tb.IsHitTestVisible = true;
                Panel.SetZIndex(tb, 1500);
            }

            // ✅ gỡ handler cũ theo reference thật (KHÔNG dùng "-= handler" với handler mới)
            var old = GroupClickHandlerStore.Get(tbPitch);
            if (old != null)
            {
                tbDia.MouseLeftButtonDown -= old;
                tbPitch.MouseLeftButtonDown -= old;
                tbMat.MouseLeftButtonDown -= old;
                GroupClickHandlerStore.Set(tbPitch, null);
            }

            MouseButtonEventHandler handler = (s, e) =>
            {
                e.Handled = true;

                var (menuX, menuY) = GetElementGroupRightTopOnCanvas(canvas, gapPx: 0, tbDia, tbPitch, tbMat);

                ShowCentralStirrupPopupWithHoverSubmenus(
                    canvas,
                    tbPitch,
                    menuX,
                    menuY,
                    currentDiameter: diameter,
                    currentMaterial: material,
                    showMaterial: showMaterial,
                    onSelectDiameter: newDia =>
                    {
                        if (ApplySpanCentralStirrupValues(item, spanIndex, newDia, pitch, material))
                            Redraw(canvas, item);
                    },
                    onEditPitch: () =>
                    {
                        BeginInlinePitchEditPushNeighbors(
                            canvas, tbPitch, tbDia, tbMat,
                            getCurrentText: () => pitch,
                            commitText: newText =>
                            {
                                double v;
                                if (!double.TryParse(newText, NumberStyles.Float, CultureInfo.InvariantCulture, out v))
                                    return;

                                if (ApplySpanCentralStirrupValues(item, spanIndex, diameter, v.ToString(CultureInfo.InvariantCulture), material))
                                    Redraw(canvas, item);
                            });
                    },
                    onSelectMaterial: newMat =>
                    {
                        if (ApplySpanCentralStirrupValues(item, spanIndex, diameter, pitch, newMat))
                            Redraw(canvas, item);
                    });
            };

            tbDia.MouseLeftButtonDown += handler;
            tbPitch.MouseLeftButtonDown += handler;
            tbMat.MouseLeftButtonDown += handler;

            GroupClickHandlerStore.Set(tbPitch, handler);
        }

        private void ShowCentralStirrupPopupWithHoverSubmenus(
            Canvas canvas,
            TextBlock owner,
            double menuX,
            double menuY,
            string currentDiameter,
            string currentMaterial,
            bool showMaterial,
            Action<string> onSelectDiameter,
            Action onEditPitch,
            Action<string> onSelectMaterial)
        {
            if (canvas == null || owner == null) return;

            try
            {
                if (_activePopupMenu != null && _activePopupMenu.IsOpen)
                    _activePopupMenu.IsOpen = false;
            }
            catch { }

            var normalBg = Brushes.Transparent;
            var selectedBg = new SolidColorBrush(Color.FromRgb(210, 225, 255));
            var dividerBrush = PopupMenuDividerBrush;
            var win = Window.GetWindow(canvas);

            UIElement WithRowDivider(UIElement child)
            {
                return new Border
                {
                    BorderBrush = dividerBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = child
                };
            }

            ControlTemplate flatBtnTemplate = null;
            ControlTemplate GetFlatBtnTemplate()
            {
                if (flatBtnTemplate != null) return flatBtnTemplate;

                var b = new FrameworkElementFactory(typeof(Border));
                b.SetValue(Border.SnapsToDevicePixelsProperty, true);
                b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                b.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                b.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                b.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

                var p = new FrameworkElementFactory(typeof(ContentPresenter));
                p.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
                p.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
                p.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Button.VerticalContentAlignmentProperty));

                b.AppendChild(p);

                flatBtnTemplate = new ControlTemplate(typeof(Button)) { VisualTree = b };
                return flatBtnTemplate;
            }

            Border WrapBox(UIElement child)
            {
                var host = new ContentControl
                {
                    Content = child,
                    FontSize = SystemFonts.MessageFontSize,
                    FontFamily = SystemFonts.MessageFontFamily
                };

                return new Border
                {
                    Background = Brushes.White,
                    BorderBrush = PopupMenuBorderBrush,
                    BorderThickness = PopupMenuBorderThickness,
                    CornerRadius = new CornerRadius(2),
                    Child = host
                };
            }

            bool IsPointInside(FrameworkElement fe, Point screenPt)
            {
                if (fe == null || !fe.IsVisible) return false;
                try
                {
                    var topLeft = fe.PointToScreen(new Point(0, 0));
                    var rect = new Rect(topLeft, new Size(fe.ActualWidth, fe.ActualHeight));
                    return rect.Contains(screenPt);
                }
                catch { return false; }
            }

            Button selectedMainBtn = null;
            void SelectMain(Button btn)
            {
                if (selectedMainBtn != null) selectedMainBtn.Background = normalBg;
                selectedMainBtn = btn;
                if (selectedMainBtn != null) selectedMainBtn.Background = selectedBg;
            }

            Button selectedSubBtn = null;
            void SelectSub(Button btn)
            {
                if (selectedSubBtn != null) selectedSubBtn.Background = normalBg;
                selectedSubBtn = btn;
                if (selectedSubBtn != null) selectedSubBtn.Background = selectedBg;
            }

            var mainPop = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = canvas,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                HorizontalOffset = menuX,
                VerticalOffset = menuY,
                AllowsTransparency = true,
                StaysOpen = true
            };

            _activePopupMenu = mainPop;
            var handle = new StyledPopupHandle { Popup = mainPop };
            owner.Tag = handle;

            System.Windows.Controls.Primitives.Popup diaSubPop = null;
            System.Windows.Controls.Primitives.Popup matSubPop = null;

            MouseButtonEventHandler outsideCloser = null;
            KeyEventHandler escCloser = null;
            EventHandler winDeactivatedCloser = null;
            EventHandler appDeactivatedCloser = null;
            bool isClosing = false;

            void CloseSubMenus()
            {
                try { if (diaSubPop != null) diaSubPop.IsOpen = false; } catch { }
                try { if (matSubPop != null) matSubPop.IsOpen = false; } catch { }
            }

            void CloseAll()
            {
                if (isClosing) return;
                isClosing = true;

                try { if (ReferenceEquals(owner.Tag, handle)) owner.Tag = null; } catch { }
                try { if (ReferenceEquals(_activePopupMenu, mainPop)) _activePopupMenu = null; } catch { }

                CloseSubMenus();
                try { mainPop.IsOpen = false; } catch { }

                try
                {
                    if (win != null && outsideCloser != null) win.RemoveHandler(UIElement.PreviewMouseDownEvent, outsideCloser);
                    if (win != null && escCloser != null) win.RemoveHandler(UIElement.PreviewKeyDownEvent, escCloser);
                }
                catch { }

                try { if (win != null && winDeactivatedCloser != null) win.Deactivated -= winDeactivatedCloser; } catch { }
                try { if (Application.Current != null && appDeactivatedCloser != null) Application.Current.Deactivated -= appDeactivatedCloser; } catch { }

                isClosing = false;
            }

            outsideCloser = (ss, ee) =>
            {
                if (win == null) { CloseAll(); return; }
                var screenPt = win.PointToScreen(ee.GetPosition(win));

                bool inside =
                    (mainPop.Child is FrameworkElement m && IsPointInside(m, screenPt))
                    || (diaSubPop != null && diaSubPop.Child is FrameworkElement d && IsPointInside(d, screenPt))
                    || (matSubPop != null && matSubPop.Child is FrameworkElement mt && IsPointInside(mt, screenPt));

                if (!inside) CloseAll();
            };

            escCloser = (ss, ee) =>
            {
                if (ee.Key == Key.Escape)
                {
                    CloseAll();
                    ee.Handled = true;
                }
            };

            winDeactivatedCloser = (_, __) => CloseAll();
            appDeactivatedCloser = (_, __) => CloseAll();

            if (win != null)
            {
                win.AddHandler(UIElement.PreviewMouseDownEvent, outsideCloser, true);
                win.AddHandler(UIElement.PreviewKeyDownEvent, escCloser, true);
                win.Deactivated += winDeactivatedCloser;
            }
            if (Application.Current != null)
                Application.Current.Deactivated += appDeactivatedCloser;

            Button MakeSubBtn(string header, bool isChecked, Action onClick)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) }); // ✅ cột ✓ cố định
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var mark = new TextBlock
                {
                    Text = "✓",
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Opacity = isChecked ? 1.0 : 0.0 // ✅ không chọn vẫn giữ chỗ
                };
                Grid.SetColumn(mark, 0);

                var txt = new TextBlock
                {
                    Text = header ?? string.Empty,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(txt, 1);

                grid.Children.Add(mark);
                grid.Children.Add(txt);

                var b = new Button
                {
                    Content = grid,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = normalBg,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    MinWidth = 80,
                    OverridesDefaultStyle = true,
                    Template = GetFlatBtnTemplate(),
                    Focusable = false,
                    IsTabStop = false
                };

                b.MouseEnter += (_, __) => SelectSub(b);
                b.Click += (_, __) =>
                {
                    SelectSub(b);
                    onClick?.Invoke();
                    CloseAll();
                };

                return b;
            }

            void OpenDiameterSubmenu(Button placementBtn)
            {
                try { if (matSubPop != null) matSubPop.IsOpen = false; } catch { }
                try { if (diaSubPop != null) diaSubPop.IsOpen = false; } catch { }

                diaSubPop = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = placementBtn,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                    HorizontalOffset = 1,
                    VerticalOffset = -1.5,
                    AllowsTransparency = true,
                    StaysOpen = true
                };

                var root = new StackPanel { Orientation = Orientation.Vertical };
                foreach (var dia in _standardRebarDiameters1)
                    root.Children.Add(WithRowDivider(MakeSubBtn(dia, string.Equals(dia, currentDiameter, StringComparison.Ordinal), () => onSelectDiameter?.Invoke(dia))));

                diaSubPop.Child = WrapBox(root);
                placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                {
                    diaSubPop.IsOpen = true;
                    SelectSub(null);
                }), DispatcherPriority.Input);
            }

            void OpenMaterialSubmenu(Button placementBtn)
            {
                if (!showMaterial && string.IsNullOrWhiteSpace(currentMaterial)) return;

                try { if (diaSubPop != null) diaSubPop.IsOpen = false; } catch { }
                try { if (matSubPop != null) matSubPop.IsOpen = false; } catch { }

                matSubPop = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = placementBtn,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                    HorizontalOffset = 1,
                    VerticalOffset = -1.5,
                    AllowsTransparency = true,
                    StaysOpen = true
                };

                var root = new StackPanel { Orientation = Orientation.Vertical };
                foreach (var mat in GetStirrupMaterialOptions())
                    root.Children.Add(WithRowDivider(MakeSubBtn(mat, string.Equals(mat, currentMaterial, StringComparison.Ordinal), () => onSelectMaterial?.Invoke(mat))));

                matSubPop.Child = WrapBox(root);
                placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                {
                    matSubPop.IsOpen = true;
                    SelectSub(null);
                }), DispatcherPriority.Input);
            }

            Button MakeMainBtn(string header, bool hasArrow)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var txt = new TextBlock
                {
                    Text = header ?? string.Empty,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(txt, 0);
                grid.Children.Add(txt);

                if (hasArrow)
                {
                    var arrow = new TextBlock
                    {
                        Text = "❯",
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(12, 0, 0, 0)
                    };
                    Grid.SetColumn(arrow, 1);
                    grid.Children.Add(arrow);
                }

                var b = new Button
                {
                    Content = grid,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = normalBg,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    MinWidth = 80,
                    OverridesDefaultStyle = true,
                    Template = GetFlatBtnTemplate(),
                    Focusable = false,
                    IsTabStop = false
                };

                b.MouseEnter += (_, __) => SelectMain(b);
                return b;
            }

            var rootMain = new StackPanel { Orientation = Orientation.Vertical };

            var btnDia = MakeMainBtn("スタラップ径", hasArrow: true);
            btnDia.MouseEnter += (_, __) =>
            {
                SelectMain(btnDia);
                OpenDiameterSubmenu(btnDia);
            };
            btnDia.Click += (_, __) =>
            {
                SelectMain(btnDia);
                OpenDiameterSubmenu(btnDia);
            };

            var btnPitch = MakeMainBtn("ピッチ", hasArrow: false);
            btnPitch.MouseEnter += (_, __) =>
            {
                SelectMain(btnPitch);
                CloseSubMenus();
            };
            btnPitch.Click += (_, __) =>
            {
                SelectMain(btnPitch);
                CloseSubMenus();
                onEditPitch?.Invoke();
                CloseAll();
            };

            var btnMat = MakeMainBtn("スタラップ材質", hasArrow: true);
            btnMat.IsEnabled = showMaterial || !string.IsNullOrWhiteSpace(currentMaterial);
            btnMat.MouseEnter += (_, __) =>
            {
                SelectMain(btnMat);
                OpenMaterialSubmenu(btnMat);
            };
            btnMat.Click += (_, __) =>
            {
                SelectMain(btnMat);
                OpenMaterialSubmenu(btnMat);
            };

            rootMain.Children.Add(WithRowDivider(btnDia));
            rootMain.Children.Add(WithRowDivider(btnPitch));
            rootMain.Children.Add(WithRowDivider(btnMat));

            mainPop.Child = WrapBox(rootMain);

            owner.Dispatcher.BeginInvoke(new Action(() =>
            {
                mainPop.IsOpen = true;
                SelectMain(null);
            }), DispatcherPriority.Input);
        }

        private void ShowCentralStirrupPopupWithHoverSubmenusnakago(
    Canvas canvas,
    TextBlock owner,
    double menuX,
    double menuY,
    string currentDiameter,
    string currentMaterial,
    bool showMaterial,
    Action<string> onSelectDiameter,
    Action onEditPitch,
    Action<string> onSelectMaterial)
        {
            if (canvas == null || owner == null) return;

            try
            {
                if (_activePopupMenu != null && _activePopupMenu.IsOpen)
                    _activePopupMenu.IsOpen = false;
            }
            catch { }

            var normalBg = Brushes.Transparent;
            var selectedBg = new SolidColorBrush(Color.FromRgb(210, 225, 255));
            var dividerBrush = PopupMenuDividerBrush;
            var win = Window.GetWindow(canvas);

            UIElement WithRowDivider(UIElement child)
            {
                return new Border
                {
                    BorderBrush = dividerBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = child
                };
            }

            ControlTemplate flatBtnTemplate = null;
            ControlTemplate GetFlatBtnTemplate()
            {
                if (flatBtnTemplate != null) return flatBtnTemplate;

                var b = new FrameworkElementFactory(typeof(Border));
                b.SetValue(Border.SnapsToDevicePixelsProperty, true);
                b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                b.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                b.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                b.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

                var p = new FrameworkElementFactory(typeof(ContentPresenter));
                p.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
                p.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
                p.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Button.VerticalContentAlignmentProperty));

                b.AppendChild(p);

                flatBtnTemplate = new ControlTemplate(typeof(Button)) { VisualTree = b };
                return flatBtnTemplate;
            }

            Border WrapBox(UIElement child)
            {
                var host = new ContentControl
                {
                    Content = child,
                    FontSize = SystemFonts.MessageFontSize,
                    FontFamily = SystemFonts.MessageFontFamily
                };

                return new Border
                {
                    Background = Brushes.White,
                    BorderBrush = PopupMenuBorderBrush,
                    BorderThickness = PopupMenuBorderThickness,
                    CornerRadius = new CornerRadius(2),
                    Child = host
                };
            }

            bool IsPointInside(FrameworkElement fe, Point screenPt)
            {
                if (fe == null || !fe.IsVisible) return false;
                try
                {
                    var topLeft = fe.PointToScreen(new Point(0, 0));
                    var rect = new Rect(topLeft, new Size(fe.ActualWidth, fe.ActualHeight));
                    return rect.Contains(screenPt);
                }
                catch { return false; }
            }

            Button selectedMainBtn = null;
            void SelectMain(Button btn)
            {
                if (selectedMainBtn != null) selectedMainBtn.Background = normalBg;
                selectedMainBtn = btn;
                if (selectedMainBtn != null) selectedMainBtn.Background = selectedBg;
            }

            Button selectedSubBtn = null;
            void SelectSub(Button btn)
            {
                if (selectedSubBtn != null) selectedSubBtn.Background = normalBg;
                selectedSubBtn = btn;
                if (selectedSubBtn != null) selectedSubBtn.Background = selectedBg;
            }

            var mainPop = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = canvas,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                HorizontalOffset = menuX,
                VerticalOffset = menuY,
                AllowsTransparency = true,
                StaysOpen = true
            };

            _activePopupMenu = mainPop;
            var handle = new StyledPopupHandle { Popup = mainPop };
            owner.Tag = handle;

            System.Windows.Controls.Primitives.Popup diaSubPop = null;
            System.Windows.Controls.Primitives.Popup matSubPop = null;

            MouseButtonEventHandler outsideCloser = null;
            KeyEventHandler escCloser = null;
            EventHandler winDeactivatedCloser = null;
            EventHandler appDeactivatedCloser = null;
            bool isClosing = false;

            void CloseSubMenus()
            {
                try { if (diaSubPop != null) diaSubPop.IsOpen = false; } catch { }
                try { if (matSubPop != null) matSubPop.IsOpen = false; } catch { }
            }

            void CloseAll()
            {
                if (isClosing) return;
                isClosing = true;

                try { if (ReferenceEquals(owner.Tag, handle)) owner.Tag = null; } catch { }
                try { if (ReferenceEquals(_activePopupMenu, mainPop)) _activePopupMenu = null; } catch { }

                CloseSubMenus();
                try { mainPop.IsOpen = false; } catch { }

                try
                {
                    if (win != null && outsideCloser != null) win.RemoveHandler(UIElement.PreviewMouseDownEvent, outsideCloser);
                    if (win != null && escCloser != null) win.RemoveHandler(UIElement.PreviewKeyDownEvent, escCloser);
                }
                catch { }

                try { if (win != null && winDeactivatedCloser != null) win.Deactivated -= winDeactivatedCloser; } catch { }
                try { if (Application.Current != null && appDeactivatedCloser != null) Application.Current.Deactivated -= appDeactivatedCloser; } catch { }

                isClosing = false;
            }

            outsideCloser = (ss, ee) =>
            {
                if (win == null) { CloseAll(); return; }
                var screenPt = win.PointToScreen(ee.GetPosition(win));

                bool inside =
                    (mainPop.Child is FrameworkElement m && IsPointInside(m, screenPt))
                    || (diaSubPop != null && diaSubPop.Child is FrameworkElement d && IsPointInside(d, screenPt))
                    || (matSubPop != null && matSubPop.Child is FrameworkElement mt && IsPointInside(mt, screenPt));

                if (!inside) CloseAll();
            };

            escCloser = (ss, ee) =>
            {
                if (ee.Key == Key.Escape)
                {
                    CloseAll();
                    ee.Handled = true;
                }
            };

            winDeactivatedCloser = (_, __) => CloseAll();
            appDeactivatedCloser = (_, __) => CloseAll();

            if (win != null)
            {
                win.AddHandler(UIElement.PreviewMouseDownEvent, outsideCloser, true);
                win.AddHandler(UIElement.PreviewKeyDownEvent, escCloser, true);
                win.Deactivated += winDeactivatedCloser;
            }
            if (Application.Current != null)
                Application.Current.Deactivated += appDeactivatedCloser;

            Button MakeSubBtn(string header, bool isChecked, Action onClick)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) }); // ✅ cột ✓ cố định
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var mark = new TextBlock
                {
                    Text = "✓",
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Opacity = isChecked ? 1.0 : 0.0 // ✅ không chọn vẫn giữ chỗ
                };
                Grid.SetColumn(mark, 0);

                var txt = new TextBlock
                {
                    Text = header ?? string.Empty,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(txt, 1);

                grid.Children.Add(mark);
                grid.Children.Add(txt);

                var b = new Button
                {
                    Content = grid,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = normalBg,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    MinWidth = 80,
                    OverridesDefaultStyle = true,
                    Template = GetFlatBtnTemplate(),
                    Focusable = false,
                    IsTabStop = false
                };

                b.MouseEnter += (_, __) => SelectSub(b);
                b.Click += (_, __) =>
                {
                    SelectSub(b);
                    onClick?.Invoke();
                    CloseAll();
                };

                return b;
            }

            void OpenDiameterSubmenu(Button placementBtn)
            {
                try { if (matSubPop != null) matSubPop.IsOpen = false; } catch { }
                try { if (diaSubPop != null) diaSubPop.IsOpen = false; } catch { }

                diaSubPop = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = placementBtn,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                    HorizontalOffset = 1,
                    VerticalOffset = -1.5,
                    AllowsTransparency = true,
                    StaysOpen = true
                };

                var root = new StackPanel { Orientation = Orientation.Vertical };
                foreach (var dia in _standardRebarDiameters)
                    root.Children.Add(WithRowDivider(MakeSubBtn(dia, string.Equals(dia, currentDiameter, StringComparison.Ordinal), () => onSelectDiameter?.Invoke(dia))));

                diaSubPop.Child = WrapBox(root);
                placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                {
                    diaSubPop.IsOpen = true;
                    SelectSub(null);
                }), DispatcherPriority.Input);
            }

            void OpenMaterialSubmenu(Button placementBtn)
            {
                if (!showMaterial && string.IsNullOrWhiteSpace(currentMaterial)) return;

                try { if (diaSubPop != null) diaSubPop.IsOpen = false; } catch { }
                try { if (matSubPop != null) matSubPop.IsOpen = false; } catch { }

                matSubPop = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = placementBtn,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                    HorizontalOffset = 1,
                    VerticalOffset = -1.5,
                    AllowsTransparency = true,
                    StaysOpen = true
                };

                var root = new StackPanel { Orientation = Orientation.Vertical };
                foreach (var mat in GetStirrupMaterialOptions())
                    root.Children.Add(WithRowDivider(MakeSubBtn(mat, string.Equals(mat, currentMaterial, StringComparison.Ordinal), () => onSelectMaterial?.Invoke(mat))));

                matSubPop.Child = WrapBox(root);
                placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                {
                    matSubPop.IsOpen = true;
                    SelectSub(null);
                }), DispatcherPriority.Input);
            }

            Button MakeMainBtn(string header, bool hasArrow)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var txt = new TextBlock
                {
                    Text = header ?? string.Empty,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(txt, 0);
                grid.Children.Add(txt);

                if (hasArrow)
                {
                    var arrow = new TextBlock
                    {
                        Text = "❯",
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(12, 0, 0, 0)
                    };
                    Grid.SetColumn(arrow, 1);
                    grid.Children.Add(arrow);
                }

                var b = new Button
                {
                    Content = grid,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = normalBg,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    MinWidth = 80,
                    OverridesDefaultStyle = true,
                    Template = GetFlatBtnTemplate(),
                    Focusable = false,
                    IsTabStop = false
                };

                b.MouseEnter += (_, __) => SelectMain(b);
                return b;
            }

            var rootMain = new StackPanel { Orientation = Orientation.Vertical };

            var btnDia = MakeMainBtn("中子筋径", hasArrow: true);
            btnDia.MouseEnter += (_, __) =>
            {
                SelectMain(btnDia);
                OpenDiameterSubmenu(btnDia);
            };
            btnDia.Click += (_, __) =>
            {
                SelectMain(btnDia);
                OpenDiameterSubmenu(btnDia);
            };

            var btnPitch = MakeMainBtn("ピッチ", hasArrow: false);
            btnPitch.MouseEnter += (_, __) =>
            {
                SelectMain(btnPitch);
                CloseSubMenus();
            };
            btnPitch.Click += (_, __) =>
            {
                SelectMain(btnPitch);
                CloseSubMenus();
                onEditPitch?.Invoke();
                CloseAll();
            };

            var btnMat = MakeMainBtn("中子筋材質", hasArrow: true);
            btnMat.IsEnabled = showMaterial || !string.IsNullOrWhiteSpace(currentMaterial);
            btnMat.MouseEnter += (_, __) =>
            {
                SelectMain(btnMat);
                OpenMaterialSubmenu(btnMat);
            };
            btnMat.Click += (_, __) =>
            {
                SelectMain(btnMat);
                OpenMaterialSubmenu(btnMat);
            };

            rootMain.Children.Add(WithRowDivider(btnDia));
            rootMain.Children.Add(WithRowDivider(btnPitch));
            rootMain.Children.Add(WithRowDivider(btnMat));

            mainPop.Child = WrapBox(rootMain);

            owner.Dispatcher.BeginInvoke(new Action(() =>
            {
                mainPop.IsOpen = true;
                SelectMain(null);
            }), DispatcherPriority.Input);
        }


        private void ShowCentralStirrupPopupWithHoverSubmenushabadome(
    Canvas canvas,
    TextBlock owner,
    double menuX,
    double menuY,
    string currentDiameter,
    Action<string> onSelectDiameter,
    Action onEditPitch)
        {
            ShowCentralStirrupPopupWithHoverSubmenushabadome(
                canvas,
                owner,
                menuX,
                menuY,
                currentDiameter: currentDiameter,
                currentMaterial: string.Empty,
                showMaterial: false,
                onSelectDiameter: onSelectDiameter,
                onEditPitch: onEditPitch,
                onSelectMaterial: _ => { }
            );
        }

        private void ShowCentralStirrupPopupWithHoverSubmenushabadome(
    Canvas canvas,
    TextBlock owner,
    double menuX,
    double menuY,
    string currentDiameter,
    string currentMaterial,
    bool showMaterial,
    Action<string> onSelectDiameter,
    Action onEditPitch,
    Action<string> onSelectMaterial)
        {
            if (canvas == null || owner == null) return;

            try
            {
                if (_activePopupMenu != null && _activePopupMenu.IsOpen)
                    _activePopupMenu.IsOpen = false;
            }
            catch { }

            var normalBg = Brushes.Transparent;
            var selectedBg = new SolidColorBrush(Color.FromRgb(210, 225, 255));
            var dividerBrush = PopupMenuDividerBrush;
            var win = Window.GetWindow(canvas);

            UIElement WithRowDivider(UIElement child)
            {
                return new Border
                {
                    BorderBrush = dividerBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = child
                };
            }

            ControlTemplate flatBtnTemplate = null;
            ControlTemplate GetFlatBtnTemplate()
            {
                if (flatBtnTemplate != null) return flatBtnTemplate;

                var b = new FrameworkElementFactory(typeof(Border));
                b.SetValue(Border.SnapsToDevicePixelsProperty, true);
                b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                b.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                b.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                b.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

                var p = new FrameworkElementFactory(typeof(ContentPresenter));
                p.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
                p.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
                p.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Button.VerticalContentAlignmentProperty));

                b.AppendChild(p);

                flatBtnTemplate = new ControlTemplate(typeof(Button)) { VisualTree = b };
                return flatBtnTemplate;
            }

            Border WrapBox(UIElement child)
            {
                var host = new ContentControl
                {
                    Content = child,
                    FontSize = SystemFonts.MessageFontSize,
                    FontFamily = SystemFonts.MessageFontFamily
                };

                return new Border
                {
                    Background = Brushes.White,
                    BorderBrush = PopupMenuBorderBrush,
                    BorderThickness = PopupMenuBorderThickness,
                    CornerRadius = new CornerRadius(2),
                    Child = host
                };
            }

            bool IsPointInside(FrameworkElement fe, Point screenPt)
            {
                if (fe == null || !fe.IsVisible) return false;
                try
                {
                    var topLeft = fe.PointToScreen(new Point(0, 0));
                    var rect = new Rect(topLeft, new Size(fe.ActualWidth, fe.ActualHeight));
                    return rect.Contains(screenPt);
                }
                catch { return false; }
            }

            Button selectedMainBtn = null;
            void SelectMain(Button btn)
            {
                if (selectedMainBtn != null) selectedMainBtn.Background = normalBg;
                selectedMainBtn = btn;
                if (selectedMainBtn != null) selectedMainBtn.Background = selectedBg;
            }

            Button selectedSubBtn = null;
            void SelectSub(Button btn)
            {
                if (selectedSubBtn != null) selectedSubBtn.Background = normalBg;
                selectedSubBtn = btn;
                if (selectedSubBtn != null) selectedSubBtn.Background = selectedBg;
            }

            var mainPop = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = canvas,
                Placement = System.Windows.Controls.Primitives.PlacementMode.Relative,
                HorizontalOffset = menuX,
                VerticalOffset = menuY,
                AllowsTransparency = true,
                StaysOpen = true
            };

            _activePopupMenu = mainPop;
            var handle = new StyledPopupHandle { Popup = mainPop };
            owner.Tag = handle;

            System.Windows.Controls.Primitives.Popup diaSubPop = null;
            System.Windows.Controls.Primitives.Popup matSubPop = null;

            MouseButtonEventHandler outsideCloser = null;
            KeyEventHandler escCloser = null;
            EventHandler winDeactivatedCloser = null;
            EventHandler appDeactivatedCloser = null;
            bool isClosing = false;

            void CloseSubMenus()
            {
                try { if (diaSubPop != null) diaSubPop.IsOpen = false; } catch { }
                try { if (matSubPop != null) matSubPop.IsOpen = false; } catch { }
            }

            void CloseAll()
            {
                if (isClosing) return;
                isClosing = true;

                try { if (ReferenceEquals(owner.Tag, handle)) owner.Tag = null; } catch { }
                try { if (ReferenceEquals(_activePopupMenu, mainPop)) _activePopupMenu = null; } catch { }

                CloseSubMenus();
                try { mainPop.IsOpen = false; } catch { }

                try
                {
                    if (win != null && outsideCloser != null) win.RemoveHandler(UIElement.PreviewMouseDownEvent, outsideCloser);
                    if (win != null && escCloser != null) win.RemoveHandler(UIElement.PreviewKeyDownEvent, escCloser);
                }
                catch { }

                try { if (win != null && winDeactivatedCloser != null) win.Deactivated -= winDeactivatedCloser; } catch { }
                try { if (Application.Current != null && appDeactivatedCloser != null) Application.Current.Deactivated -= appDeactivatedCloser; } catch { }

                isClosing = false;
            }

            outsideCloser = (ss, ee) =>
            {
                if (win == null) { CloseAll(); return; }
                var screenPt = win.PointToScreen(ee.GetPosition(win));

                bool inside =
                    (mainPop.Child is FrameworkElement m && IsPointInside(m, screenPt))
                    || (diaSubPop != null && diaSubPop.Child is FrameworkElement d && IsPointInside(d, screenPt))
                    || (matSubPop != null && matSubPop.Child is FrameworkElement mt && IsPointInside(mt, screenPt));

                if (!inside) CloseAll();
            };

            escCloser = (ss, ee) =>
            {
                if (ee.Key == Key.Escape)
                {
                    CloseAll();
                    ee.Handled = true;
                }
            };

            winDeactivatedCloser = (_, __) => CloseAll();
            appDeactivatedCloser = (_, __) => CloseAll();

            if (win != null)
            {
                win.AddHandler(UIElement.PreviewMouseDownEvent, outsideCloser, true);
                win.AddHandler(UIElement.PreviewKeyDownEvent, escCloser, true);
                win.Deactivated += winDeactivatedCloser;
            }
            if (Application.Current != null)
                Application.Current.Deactivated += appDeactivatedCloser;

            Button MakeSubBtn(string header, bool isChecked, Action onClick)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(15) }); // ✅ cột ✓ cố định
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var mark = new TextBlock
                {
                    Text = "✓",
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Opacity = isChecked ? 1.0 : 0.0 // ✅ không chọn vẫn giữ chỗ
                };
                Grid.SetColumn(mark, 0);

                var txt = new TextBlock
                {
                    Text = header ?? string.Empty,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(txt, 1);

                grid.Children.Add(mark);
                grid.Children.Add(txt);

                var b = new Button
                {
                    Content = grid,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = normalBg,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    MinWidth = 80,
                    OverridesDefaultStyle = true,
                    Template = GetFlatBtnTemplate(),
                    Focusable = false,
                    IsTabStop = false
                };

                b.MouseEnter += (_, __) => SelectSub(b);
                b.Click += (_, __) =>
                {
                    SelectSub(b);
                    onClick?.Invoke();
                    CloseAll();
                };

                return b;
            }

            void OpenDiameterSubmenu(Button placementBtn)
            {
                try { if (matSubPop != null) matSubPop.IsOpen = false; } catch { }
                try { if (diaSubPop != null) diaSubPop.IsOpen = false; } catch { }

                diaSubPop = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = placementBtn,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                    HorizontalOffset = 1,
                    VerticalOffset = -1.5,
                    AllowsTransparency = true,
                    StaysOpen = true
                };

                var root = new StackPanel { Orientation = Orientation.Vertical };
                foreach (var dia in _standardRebarDiameters)
                    root.Children.Add(WithRowDivider(MakeSubBtn(dia, string.Equals(dia, currentDiameter, StringComparison.Ordinal), () => onSelectDiameter?.Invoke(dia))));

                diaSubPop.Child = WrapBox(root);
                placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                {
                    diaSubPop.IsOpen = true;
                    SelectSub(null);
                }), DispatcherPriority.Input);
            }

            void OpenMaterialSubmenu(Button placementBtn)
            {
                if (!showMaterial && string.IsNullOrWhiteSpace(currentMaterial)) return;

                try { if (diaSubPop != null) diaSubPop.IsOpen = false; } catch { }
                try { if (matSubPop != null) matSubPop.IsOpen = false; } catch { }

                matSubPop = new System.Windows.Controls.Primitives.Popup
                {
                    PlacementTarget = placementBtn,
                    Placement = System.Windows.Controls.Primitives.PlacementMode.Right,
                    HorizontalOffset = 1,
                    VerticalOffset = -1.5,
                    AllowsTransparency = true,
                    StaysOpen = true
                };

                var root = new StackPanel { Orientation = Orientation.Vertical };
                foreach (var mat in GetStirrupMaterialOptions())
                    root.Children.Add(WithRowDivider(MakeSubBtn(mat, string.Equals(mat, currentMaterial, StringComparison.Ordinal), () => onSelectMaterial?.Invoke(mat))));

                matSubPop.Child = WrapBox(root);
                placementBtn.Dispatcher.BeginInvoke(new Action(() =>
                {
                    matSubPop.IsOpen = true;
                    SelectSub(null);
                }), DispatcherPriority.Input);
            }

            Button MakeMainBtn(string header, bool hasArrow)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var txt = new TextBlock
                {
                    Text = header ?? string.Empty,
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(txt, 0);
                grid.Children.Add(txt);

                if (hasArrow)
                {
                    var arrow = new TextBlock
                    {
                        Text = "❯",
                        VerticalAlignment = VerticalAlignment.Center,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(12, 0, 0, 0)
                    };
                    Grid.SetColumn(arrow, 1);
                    grid.Children.Add(arrow);
                }

                var b = new Button
                {
                    Content = grid,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = normalBg,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    MinWidth = 80,
                    OverridesDefaultStyle = true,
                    Template = GetFlatBtnTemplate(),
                    Focusable = false,
                    IsTabStop = false
                };

                b.MouseEnter += (_, __) => SelectMain(b);
                return b;
            }

            var rootMain = new StackPanel { Orientation = Orientation.Vertical };

            var btnDia = MakeMainBtn("幅止筋径", hasArrow: true);
            btnDia.MouseEnter += (_, __) =>
            {
                SelectMain(btnDia);
                OpenDiameterSubmenu(btnDia);
            };
            btnDia.Click += (_, __) =>
            {
                SelectMain(btnDia);
                OpenDiameterSubmenu(btnDia);
            };

            var btnPitch = MakeMainBtn("ピッチ", hasArrow: false);
            btnPitch.MouseEnter += (_, __) =>
            {
                SelectMain(btnPitch);
                CloseSubMenus();
            };
            btnPitch.Click += (_, __) =>
            {
                SelectMain(btnPitch);
                CloseSubMenus();
                onEditPitch?.Invoke();
                CloseAll();
            };

            //var btnMat = MakeMainBtn("スタラップ材質", hasArrow: true);
            //btnMat.IsEnabled = showMaterial || !string.IsNullOrWhiteSpace(currentMaterial);
            //btnMat.MouseEnter += (_, __) =>
            //{
            //    SelectMain(btnMat);
            //    OpenMaterialSubmenu(btnMat);
            //};
            //btnMat.Click += (_, __) =>
            //{
            //    SelectMain(btnMat);
            //    OpenMaterialSubmenu(btnMat);
            //};

            rootMain.Children.Add(WithRowDivider(btnDia));
            rootMain.Children.Add(WithRowDivider(btnPitch));
            //rootMain.Children.Add(WithRowDivider(btnMat));

            mainPop.Child = WrapBox(rootMain);

            owner.Dispatcher.BeginInvoke(new Action(() =>
            {
                mainPop.IsOpen = true;
                SelectMain(null);
            }), DispatcherPriority.Input);
        }




        private static class GroupClickHandlerStore
        {
            public static readonly DependencyProperty HandlerProperty =
                DependencyProperty.RegisterAttached(
                    "Handler",
                    typeof(MouseButtonEventHandler),
                    typeof(GroupClickHandlerStore),
                    new PropertyMetadata(null));

            public static void Set(DependencyObject obj, MouseButtonEventHandler value) =>
                obj.SetValue(HandlerProperty, value);

            public static MouseButtonEventHandler Get(DependencyObject obj) =>
                (MouseButtonEventHandler)obj.GetValue(HandlerProperty);
        }


        private sealed class StyledPopupHandle
        {
            public System.Windows.Controls.Primitives.Popup Popup { get; set; }
            public MouseButtonEventHandler OutsideCloser { get; set; }
            public KeyEventHandler EscCloser { get; set; }
            public EventHandler WinDeactivatedCloser { get; set; }
            public EventHandler AppDeactivatedCloser { get; set; }
            public bool IsClosing { get; set; }
        }

        private void ShowStyledPopupMenuLikeOrange(
            Canvas canvas,
            FrameworkElement placementTarget,
            System.Windows.Controls.Primitives.PlacementMode placement,
            IEnumerable<(string Header, bool Enabled, Action OnClick)> items,
            double minWidth = 80,
            double? horizontalOffset = null,
            double? verticalOffset = null)
        {
            if (canvas == null || placementTarget == null)
                return;

            // =========================
            // 0) CLOSE-ON-OPEN (OrangeDim-style)
            // =========================

            // 0.1) Close popup currently stored on THIS target (if any)
            var oldHandle = placementTarget.Tag as StyledPopupHandle;
            if (oldHandle != null)
            {
                try { if (oldHandle.Popup != null) oldHandle.Popup.IsOpen = false; } catch { }
                try
                {
                    var w0 = Window.GetWindow(canvas);
                    if (w0 != null)
                    {
                        if (oldHandle.OutsideCloser != null) w0.RemoveHandler(UIElement.PreviewMouseDownEvent, oldHandle.OutsideCloser);
                        if (oldHandle.EscCloser != null) w0.RemoveHandler(UIElement.PreviewKeyDownEvent, oldHandle.EscCloser);
                        if (oldHandle.WinDeactivatedCloser != null) w0.Deactivated -= oldHandle.WinDeactivatedCloser;
                    }
                    if (Application.Current != null && oldHandle.AppDeactivatedCloser != null)
                        Application.Current.Deactivated -= oldHandle.AppDeactivatedCloser;
                }
                catch { }

                placementTarget.Tag = null;
            }

            // 0.2) Close popup currently active globally (prevents stacking across targets)
            try
            {
                if (_activePopupMenu != null && _activePopupMenu.IsOpen)
                    _activePopupMenu.IsOpen = false;
            }
            catch { }

            var win = Window.GetWindow(canvas);

            var normalBg = Brushes.Transparent;
            var selectedBg = new SolidColorBrush(Color.FromRgb(210, 225, 255));
            var dividerBrush = PopupMenuDividerBrush;

            UIElement WithRowDivider(UIElement child)
            {
                return new Border
                {
                    BorderBrush = dividerBrush,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Child = child
                };
            }

            ControlTemplate flatBtnTemplate = null;
            ControlTemplate GetFlatBtnTemplate()
            {
                if (flatBtnTemplate != null) return flatBtnTemplate;

                var b = new FrameworkElementFactory(typeof(Border));
                b.SetValue(Border.SnapsToDevicePixelsProperty, true);
                b.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
                b.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
                b.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
                b.SetValue(Border.PaddingProperty, new TemplateBindingExtension(Button.PaddingProperty));

                var p = new FrameworkElementFactory(typeof(ContentPresenter));
                p.SetValue(ContentPresenter.ContentProperty, new TemplateBindingExtension(Button.ContentProperty));
                p.SetValue(ContentPresenter.HorizontalAlignmentProperty, new TemplateBindingExtension(Button.HorizontalContentAlignmentProperty));
                p.SetValue(ContentPresenter.VerticalAlignmentProperty, new TemplateBindingExtension(Button.VerticalContentAlignmentProperty));

                b.AppendChild(p);

                flatBtnTemplate = new ControlTemplate(typeof(Button)) { VisualTree = b };
                return flatBtnTemplate;
            }

            Border WrapBox(UIElement child)
            {
                var host = new ContentControl
                {
                    Content = child,
                    FontSize = SystemFonts.MessageFontSize,
                    FontFamily = SystemFonts.MessageFontFamily
                };

                return new Border
                {
                    Background = Brushes.White,
                    BorderBrush = PopupMenuBorderBrush,
                    BorderThickness = PopupMenuBorderThickness,
                    CornerRadius = new CornerRadius(2),
                    Child = host
                };
            }

            Button selectedBtn = null;
            void Select(Button btn)
            {
                if (selectedBtn != null) selectedBtn.Background = normalBg;
                selectedBtn = btn;
                if (selectedBtn != null) selectedBtn.Background = selectedBg;
            }

            var pop = new System.Windows.Controls.Primitives.Popup
            {
                PlacementTarget = placementTarget,
                Placement = placement,
                AllowsTransparency = true,
                StaysOpen = true
            };

            if (horizontalOffset.HasValue) pop.HorizontalOffset = horizontalOffset.Value;
            if (verticalOffset.HasValue) pop.VerticalOffset = verticalOffset.Value;

            // register as global active
            _activePopupMenu = pop;

            var handle = new StyledPopupHandle { Popup = pop };

            // IMPORTANT: store on placementTarget so next click can close it
            placementTarget.Tag = handle;

            bool IsPointInside(FrameworkElement fe, Point screenPt)
            {
                if (fe == null || !fe.IsVisible) return false;
                try
                {
                    var topLeft = fe.PointToScreen(new Point(0, 0));
                    var rect = new Rect(topLeft, new Size(fe.ActualWidth, fe.ActualHeight));
                    return rect.Contains(screenPt);
                }
                catch { return false; }
            }

            void CloseAll()
            {
                if (handle.IsClosing) return;
                handle.IsClosing = true;

                try { pop.IsOpen = false; } catch { }

                // cleanup hooks
                try
                {
                    if (win != null && handle.OutsideCloser != null)
                        win.RemoveHandler(UIElement.PreviewMouseDownEvent, handle.OutsideCloser);
                    if (win != null && handle.EscCloser != null)
                        win.RemoveHandler(UIElement.PreviewKeyDownEvent, handle.EscCloser);
                }
                catch { }

                try { if (win != null && handle.WinDeactivatedCloser != null) win.Deactivated -= handle.WinDeactivatedCloser; } catch { }
                try { if (Application.Current != null && handle.AppDeactivatedCloser != null) Application.Current.Deactivated -= handle.AppDeactivatedCloser; } catch { }

                // clear registrations
                try { if (ReferenceEquals(placementTarget.Tag, handle)) placementTarget.Tag = null; } catch { }
                try { if (ReferenceEquals(_activePopupMenu, pop)) _activePopupMenu = null; } catch { }

                handle.IsClosing = false;
            }

            handle.OutsideCloser = (ss, ee) =>
            {
                if (win == null) { CloseAll(); return; }
                var screenPt = win.PointToScreen(ee.GetPosition(win));

                var m = pop.Child as FrameworkElement; // C# 7.3 safe

                bool inside =
                    IsPointInside(placementTarget, screenPt)
                    || (m != null && IsPointInside(m, screenPt));

                if (!inside)
                    CloseAll();
            };

            handle.EscCloser = (ss, ee) =>
            {
                if (ee.Key == Key.Escape)
                {
                    CloseAll();
                    ee.Handled = true;
                }
            };

            handle.WinDeactivatedCloser = (ss, ee) => CloseAll();
            handle.AppDeactivatedCloser = (ss, ee) => CloseAll();

            if (win != null)
            {
                win.AddHandler(UIElement.PreviewMouseDownEvent, handle.OutsideCloser, true);
                win.AddHandler(UIElement.PreviewKeyDownEvent, handle.EscCloser, true);
                win.Deactivated += handle.WinDeactivatedCloser;
            }
            if (Application.Current != null)
            {
                Application.Current.Deactivated += handle.AppDeactivatedCloser;
            }

            // if popup closes for any reason, cleanup registrations
            pop.Closed += (s, e) =>
            {
                try { if (ReferenceEquals(placementTarget.Tag, handle)) placementTarget.Tag = null; } catch { }
                try { if (ReferenceEquals(_activePopupMenu, pop)) _activePopupMenu = null; } catch { }
            };

            var root = new StackPanel { Orientation = Orientation.Vertical };

            foreach (var it in items)
            {
                var btn = new Button
                {
                    Content = new TextBlock { Text = it.Header ?? string.Empty, VerticalAlignment = VerticalAlignment.Center },
                    HorizontalContentAlignment = HorizontalAlignment.Stretch,
                    VerticalContentAlignment = VerticalAlignment.Center,
                    Padding = new Thickness(12, 6, 12, 6),
                    Background = normalBg,
                    BorderBrush = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    MinWidth = minWidth,
                    OverridesDefaultStyle = true,
                    Template = GetFlatBtnTemplate(),
                    Focusable = false,
                    IsTabStop = false,
                    IsEnabled = it.Enabled
                };

                btn.MouseEnter += (_, __) => Select(btn);
                btn.Click += (_, __) =>
                {
                    Select(btn);
                    it.OnClick?.Invoke();
                    CloseAll();
                };

                root.Children.Add(WithRowDivider(btn));
            }

            pop.Child = WrapBox(root);

            placementTarget.Dispatcher.BeginInvoke(new Action(() =>
            {
                pop.IsOpen = true;
                Select(null);
            }), System.Windows.Threading.DispatcherPriority.Input);
        }

        private void EnableEditableHoverWithBox(TextBlock tb, Canvas canvas,
                                       Brush hoverForeground,
                                       Brush hoverFill,
                                       Brush hoverStroke,
                                       double pad = -1,
                                       double strokeThickness = 1.0)
        {
            if (tb == null || canvas == null) return;

            HoverBoxState existing;
            if (_hoverBoxStates.TryGetValue(tb, out existing)) return;

            if (hoverForeground == null) hoverForeground = Brushes.MediumBlue;
            if (hoverStroke == null) hoverStroke = Brushes.MediumBlue;
            if (hoverFill == null) hoverFill = new SolidColorBrush(Color.FromArgb(25, 30, 144, 255));

            // đảm bảo hover/click bắt được
            if (tb.Background == null) tb.Background = Brushes.Transparent;
            tb.IsHitTestVisible = true;

            // Box (đặt sau tb) - không hit test
            var box = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(strokeThickness),
                CornerRadius = new CornerRadius(3),
                IsHitTestVisible = false,
                Visibility = System.Windows.Visibility.Collapsed
            };

            // đặt box vào canvas (đứng sau tb)
            Panel.SetZIndex(box, Panel.GetZIndex(tb) - 1);
            canvas.Children.Add(box);

            _hoverBoxStates.Add(tb, new HoverBoxState
            {
                Foreground = tb.Foreground,
                Box = box,
                Pad = pad,
                BoxFill = hoverFill,
                BoxStroke = hoverStroke,
                BoxThickness = new Thickness(strokeThickness)
            });

            void UpdateBoxRect()
            {
                var st = _hoverBoxStates.GetValue(tb, _ => null);
                if (st == null || st.Box == null) return;

                double left = Canvas.GetLeft(tb);
                double top = Canvas.GetTop(tb);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;

                // ActualWidth/Height đôi khi = 0 lúc mới tạo => fallback DesiredSize
                double w = tb.ActualWidth;
                double h = tb.ActualHeight;

                if (w <= 0 || h <= 0)
                {
                    tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    w = tb.DesiredSize.Width;
                    h = tb.DesiredSize.Height;
                }

                Canvas.SetLeft(st.Box, left - st.Pad);
                Canvas.SetTop(st.Box, top - st.Pad);
                st.Box.Width = Math.Max(1, w + st.Pad * 2);
                st.Box.Height = Math.Max(1, h + st.Pad * 2);

                Panel.SetZIndex(st.Box, Panel.GetZIndex(tb) - 1);
            }

            tb.LayoutUpdated += (s, e) =>
            {
                HoverBoxState st;
                if (_hoverBoxStates.TryGetValue(tb, out st) && st.Box != null && st.Box.Visibility == System.Windows.Visibility.Visible)
                    UpdateBoxRect();
            };

            tb.MouseEnter += (s, e) =>
            {
                HoverBoxState st;
                if (!_hoverBoxStates.TryGetValue(tb, out st)) return;

                tb.Foreground = hoverForeground;

                st.Box.BorderThickness = st.BoxThickness;
                st.Box.BorderBrush = st.BoxStroke;
                st.Box.Background = st.BoxFill;
                st.Box.Visibility = System.Windows.Visibility.Visible;

                UpdateBoxRect();
            };

            tb.MouseLeave += (s, e) =>
            {
                HoverBoxState st;
                if (!_hoverBoxStates.TryGetValue(tb, out st)) return;

                tb.Foreground = st.Foreground;

                if (st.Box != null)
                {
                    st.Box.Visibility = System.Windows.Visibility.Collapsed;
                    st.Box.Background = Brushes.Transparent;
                    st.Box.BorderBrush = Brushes.Transparent;
                }
            };
        }
        private sealed class HoverBoxState
        {
            public Brush Foreground;
            public Border Box;
            public double Pad;
            public Brush BoxFill;
            public Brush BoxStroke;
            public Thickness BoxThickness;
        }

        private readonly ConditionalWeakTable<TextBlock, HoverBoxState> _hoverBoxStates
            = new ConditionalWeakTable<TextBlock, HoverBoxState>();

        [ThreadStatic]
        private static Stack<IList<Shape>> _shapeCollectors;

        private sealed class ShapeCollectScope : IDisposable
        {
            private readonly Action _onDispose;
            private bool _disposed;

            public ShapeCollectScope(Action onDispose)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_onDispose != null) _onDispose();
            }
        }

        private IDisposable BeginCollectShapes(IList<Shape> target)
        {
            if (target == null) return new ShapeCollectScope(null);

            if (_shapeCollectors == null)
                _shapeCollectors = new Stack<IList<Shape>>();

            _shapeCollectors.Push(target);

            return new ShapeCollectScope(() =>
            {
                if (_shapeCollectors != null && _shapeCollectors.Count > 0)
                    _shapeCollectors.Pop();
            });
        }
        private void ClearTanbuHookOverridesForSpan(int spanIndex)
        {
            if (spanIndex < 0) return;

            bool changed = false;

            foreach (var beamKvp in _tanbuHookOverrides.ToList())
            {
                var dict = beamKvp.Value;
                if (dict == null) continue;

                // remove 4 keys per span:
                changed = dict.Remove((spanIndex, false, false)) || changed; // 左の腹筋 - 左端
                changed = dict.Remove((spanIndex, false, true)) || changed;  // 左の腹筋 - 右端
                changed = dict.Remove((spanIndex, true, false)) || changed;  // 右の腹筋 - 左端
                changed = dict.Remove((spanIndex, true, true)) || changed;   // 右の腹筋 - 右端

                if (dict.Count == 0)
                    _tanbuHookOverrides.Remove(beamKvp.Key);
            }

            if (_currentSecoList?.GridBotsecozuMap != null)
            {
                foreach (var grids in _currentSecoList.GridBotsecozuMap.Values)
                {
                    if (grids == null) continue;
                    foreach (var grid in grids)
                    {
                        if (grid?.TanbuHookOverrides == null || grid.TanbuHookOverrides.Count == 0)
                            continue;

                        var toDelete = grid.TanbuHookOverrides.Keys
                            .Where(k => TryParseTanbuHookKey(k, out var parsed) && parsed.spanIndex == spanIndex)
                            .ToList();

                        foreach (var key in toDelete)
                            changed = grid.TanbuHookOverrides.Remove(key) || changed;
                    }
                }
            }

            if (changed)
                PropertyChangeTracker.MarkChanged();
        }

        private void CloseTaggedPopup(FrameworkElement target)
        {
            if (target == null) return;

            var h = target.Tag as StyledPopupHandle;
            if (h == null) return;

            try { if (h.Popup != null) h.Popup.IsOpen = false; } catch { }

            // gỡ hook nếu bạn có lưu ở handle (giống ShowStyledPopupMenuLikeOrange)
            try
            {
                var win = Window.GetWindow(target);
                if (win != null)
                {
                    if (h.OutsideCloser != null) win.RemoveHandler(UIElement.PreviewMouseDownEvent, h.OutsideCloser);
                    if (h.EscCloser != null) win.RemoveHandler(UIElement.PreviewKeyDownEvent, h.EscCloser);
                    if (h.WinDeactivatedCloser != null) win.Deactivated -= h.WinDeactivatedCloser;
                }
                if (Application.Current != null && h.AppDeactivatedCloser != null)
                    Application.Current.Deactivated -= h.AppDeactivatedCloser;
            }
            catch { }

            try { target.Tag = null; } catch { }
        }

        private void CloseGlobalActivePopup()
        {
            try
            {
                if (_activePopupMenu != null && _activePopupMenu.IsOpen)
                    _activePopupMenu.IsOpen = false;
            }
            catch { }
            finally
            {
                _activePopupMenu = null;
            }
        }
        private double MeasureTextWidthPx(Canvas canvas, string text, Typeface typeface, double fontSize)
        {
            text = text ?? string.Empty;
            try
            {
                var dpi = VisualTreeHelper.GetDpi(canvas);
                var ft = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.Black,
                    dpi.PixelsPerDip);
                return ft.WidthIncludingTrailingWhitespace;
            }
            catch
            {
                return 0;
            }
        }

        private double MeasureTextWidthDxfMm(Canvas canvas, string text, Typeface typeface, double fontPx, double targetHeightMm)
        {
            text = text ?? string.Empty;
            if (targetHeightMm <= 0) return 0;

            try
            {
                var dpi = VisualTreeHelper.GetDpi(canvas);
                var ft = new FormattedText(
                    text,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontPx,
                    Brushes.Black,
                    dpi.PixelsPerDip);

                var geometry = ft.BuildGeometry(new Point(0, 0));
                var bounds = geometry?.Bounds ?? Rect.Empty;
                if (!bounds.IsEmpty && bounds.Width > 0 && bounds.Height > 0)
                {
                    // PDF vector export normalizes text to DxfText.Height (mm),
                    // so horizontal spacing must follow the same width/height ratio.
                    return (bounds.Width / bounds.Height) * targetHeightMm;
                }
            }
            catch
            {
            }

            return 0;
        }
        private Rect GetElementBoundsOnCanvas(FrameworkElement element, Canvas canvas)
        {
            if (element == null || canvas == null) return Rect.Empty;

            element.UpdateLayout();
            double width = element.ActualWidth;
            double height = element.ActualHeight;
            if (width <= 0 || height <= 0)
            {
                element.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = width <= 0 ? element.DesiredSize.Width : width;
                height = height <= 0 ? element.DesiredSize.Height : height;
            }

            try
            {
                var topLeft = element.TransformToVisual(canvas).Transform(new Point(0, 0));
                return new Rect(topLeft.X, topLeft.Y, Math.Max(0, width), Math.Max(0, height));
            }
            catch
            {
                double left = Canvas.GetLeft(element);
                double top = Canvas.GetTop(element);
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;
                return new Rect(left, top, Math.Max(0, width), Math.Max(0, height));
            }
        }

        private (double X, double Y) GetElementGroupRightTopOnCanvas(
            Canvas canvas,
            double gapPx,
            params FrameworkElement[] elements)
        {
            if (canvas == null || elements == null || elements.Length == 0)
                return (0, 0);

            Rect union = Rect.Empty;
            foreach (var element in elements)
            {
                var rect = GetElementBoundsOnCanvas(element, canvas);
                if (rect.IsEmpty || rect.Width <= 0 || rect.Height <= 0)
                    continue;

                union = union.IsEmpty ? rect : Rect.Union(union, rect);
            }

            if (union.IsEmpty)
                return (0, 0);

            return (union.Right + gapPx, union.Top);
        }

        private TextBlock CloneTextBlockForCanvasOverlay(TextBlock source)
        {
            return new TextBlock
            {
                Text = source?.Text ?? string.Empty,
                FontFamily = source?.FontFamily ?? this.FontFamily,
                FontSize = source?.FontSize ?? 12.0,
                FontStyle = source?.FontStyle ?? FontStyles.Normal,
                FontWeight = source?.FontWeight ?? FontWeights.Normal,
                FontStretch = source?.FontStretch ?? FontStretches.Normal,
                Foreground = source?.Foreground ?? Brushes.Black,
                Background = Brushes.Transparent,
                TextWrapping = TextWrapping.NoWrap,
                IsHitTestVisible = false
            };
        }

        private void DrawCentralStirrupTripletPx(
    Canvas canvas, WCTransform T, GridBotsecozu item,
    double centerXmm, double yMm,
    double fontPx,
    Brush brush,
    string diaText, string pitchText, string matText,
    out TextBlock tbDia, out TextBlock tbPitch, out TextBlock tbMat,
    double gapPx = 6.0)
        {
            tbDia = null;
            tbPitch = null;
            tbMat = null;
            if (canvas == null) return;
            matText = matText ?? string.Empty;
            bool hasMat = !string.IsNullOrWhiteSpace(matText);

            double scalePxPerMm = Math.Abs(T.Scale) < 1e-9 ? 1.0 : Math.Abs(T.Scale);
            const double dxfTextHeightMm = 200.0;
            double effectiveFontPx = Math.Max(1.0, dxfTextHeightMm * scalePxPerMm);
            string fontFamilyName = this.FontFamily?.Source ?? "Yu Mincho";
            var fontFamily = new FontFamily(fontFamilyName);

            Point anchorPx = T.P(centerXmm, yMm);

            TextBlock CreateTripletTextBlock(string text)
            {
                return new TextBlock
                {
                    Text = text ?? string.Empty,
                    FontFamily = fontFamily,
                    FontSize = effectiveFontPx,
                    Foreground = brush ?? Brushes.Black,
                    Background = Brushes.Transparent,
                    TextWrapping = TextWrapping.NoWrap
                };
            }

            tbDia = CreateTripletTextBlock(diaText);
            tbPitch = CreateTripletTextBlock(pitchText);
            tbMat = CreateTripletTextBlock(matText);
            if (gapPx > 0) tbPitch.Margin = new Thickness(gapPx, 0, 0, 0);
            if (hasMat && gapPx > 0) tbMat.Margin = new Thickness(gapPx, 0, 0, 0);

            var tripletHost = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };
            tripletHost.Children.Add(tbDia);
            tripletHost.Children.Add(tbPitch);
            tripletHost.Children.Add(tbMat);

            tripletHost.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double totalPx = tripletHost.DesiredSize.Width;
            double maxHeightPx = tripletHost.DesiredSize.Height;
            double xStartPx = anchorPx.X - totalPx / 2.0;
            double topPx = anchorPx.Y - maxHeightPx;

            Canvas.SetLeft(tripletHost, xStartPx);
            Canvas.SetTop(tripletHost, topPx);
            canvas.Children.Add(tripletHost);

            var (h, v) = ToDxfAlign(HAnchor.Center, VAnchor.Bottom);
            var textColor = ColorFromBrush(brush ?? Brushes.Black, Colors.Black);
            string fullText = string.Join(" ", new[] { diaText, pitchText, matText }.Where(s => !string.IsNullOrWhiteSpace(s)));
            SceneFor(item).Add(new DxfText(fullText, centerXmm, yMm, dxfTextHeightMm, hAlign: h, vAlign: v, rotDeg: 0,
                                           layer: "TEXT", style: "STANDARD", fontPx: effectiveFontPx,
                                           fontFamily: fontFamilyName, color: textColor, hAnchor: HAnchor.Center, vAnchor: VAnchor.Bottom,
                                           skipCanvasBitmap: true));
        }
        private void BeginInlinePitchEditPushNeighbors(
        Canvas canvas,
        TextBlock tbPitch,
        FrameworkElement leftTb,   // tbDia
        FrameworkElement rightTb,  // tbMat
        Func<string> getCurrentText,
        Action<string> commitText,
        double gapPx = 6.0,
        double paddingPx = 14.0)
        {
            if (canvas == null || tbPitch == null || leftTb == null || rightTb == null) return;

            canvas.UpdateLayout();
            leftTb.UpdateLayout();
            rightTb.UpdateLayout();
            tbPitch.UpdateLayout();

            Rect pitchRect0 = GetElementBoundsOnCanvas(tbPitch, canvas);
            Rect leftRect0 = GetElementBoundsOnCanvas(leftTb, canvas);
            Rect rightRect0 = GetElementBoundsOnCanvas(rightTb, canvas);
            if (pitchRect0.IsEmpty || leftRect0.IsEmpty || rightRect0.IsEmpty) return;

            // Measure neighbors width
            leftTb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            rightTb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double wLeft = Math.Max(leftRect0.Width, leftTb.DesiredSize.Width);
            double wRight = Math.Max(rightRect0.Width, rightTb.DesiredSize.Width);

            // Center X of pitch (fixed anchor while editing)
            double pitchCenterX = pitchRect0.Left + (pitchRect0.Width / 2.0);

            var originalPitchText = tbPitch.Text ?? string.Empty;
            var startText = getCurrentText != null ? (getCurrentText() ?? "") : originalPitchText;
            var leftOverlay = leftTb is TextBlock leftText ? CloneTextBlockForCanvasOverlay(leftText) : null;
            var pitchOverlay = CloneTextBlockForCanvasOverlay(tbPitch);
            var rightOverlay = rightTb is TextBlock rightText ? CloneTextBlockForCanvasOverlay(rightText) : null;

            var edit = new TextBox
            {
                Text = startText,
                FontFamily = tbPitch.FontFamily,
                FontSize = tbPitch.FontSize,
                FontWeight = tbPitch.FontWeight,
                Padding = new Thickness(4, 1, 4, 1),
                BorderThickness = new Thickness(1),
                VerticalContentAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.NoWrap,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = ScrollBarVisibility.Hidden
            };

            if (leftOverlay != null)
            {
                Canvas.SetLeft(leftOverlay, leftRect0.Left);
                Canvas.SetTop(leftOverlay, leftRect0.Top);
                Panel.SetZIndex(leftOverlay, Panel.GetZIndex(leftTb) + 10);
                canvas.Children.Add(leftOverlay);
            }
            Canvas.SetLeft(pitchOverlay, pitchRect0.Left);
            Canvas.SetTop(pitchOverlay, pitchRect0.Top);
            Panel.SetZIndex(pitchOverlay, Panel.GetZIndex(tbPitch) + 10);
            canvas.Children.Add(pitchOverlay);
            if (rightOverlay != null)
            {
                Canvas.SetLeft(rightOverlay, rightRect0.Left);
                Canvas.SetTop(rightOverlay, rightRect0.Top);
                Panel.SetZIndex(rightOverlay, Panel.GetZIndex(rightTb) + 10);
                canvas.Children.Add(rightOverlay);
            }

            // Hide original texts while editing
            leftTb.Visibility = System.Windows.Visibility.Hidden;
            tbPitch.Visibility = System.Windows.Visibility.Hidden;
            rightTb.Visibility = System.Windows.Visibility.Hidden;

            Panel.SetZIndex(edit, Panel.GetZIndex(tbPitch) + 10);
            canvas.Children.Add(edit);

            double MeasureTextWidth(string text)
            {
                text = text ?? "";
                try
                {
                    var dpi = VisualTreeHelper.GetDpi(canvas);
                    var ft = new FormattedText(
                        text,
                        CultureInfo.CurrentCulture,
                        FlowDirection.LeftToRight,
                        new Typeface(edit.FontFamily, edit.FontStyle, edit.FontWeight, edit.FontStretch),
                        edit.FontSize,
                        Brushes.Black,
                        dpi.PixelsPerDip);
                    return ft.WidthIncludingTrailingWhitespace;
                }
                catch
                {
                    return tbPitch.ActualWidth > 0 ? tbPitch.ActualWidth : 40;
                }
            }

            void ApplyLayout()
            {
                // 1) Grow edit to fit the whole text (no clamp)
                double w = MeasureTextWidth(edit.Text) + paddingPx;
                double minW = Math.Max(24, pitchRect0.Width);
                if (w < minW) w = minW;

                edit.Width = w;
                edit.Height = Math.Max(pitchRect0.Height, 18);

                double editLeft = pitchCenterX - (w / 2.0);

                // 2) Place editor at pitch position
                Canvas.SetLeft(edit, editLeft);
                Canvas.SetTop(edit, pitchRect0.Top);

                // 3) Push neighbors away from the editor
                if (leftOverlay != null)
                {
                    Canvas.SetLeft(leftOverlay, editLeft - gapPx - wLeft);
                    Canvas.SetTop(leftOverlay, leftRect0.Top);
                }
                Canvas.SetLeft(pitchOverlay, editLeft);
                Canvas.SetTop(pitchOverlay, pitchRect0.Top);
                if (rightOverlay != null)
                {
                    Canvas.SetLeft(rightOverlay, editLeft + w + gapPx);
                    Canvas.SetTop(rightOverlay, rightRect0.Top);
                }
            }

            bool isClosing = false;

            void RestoreAndClose(bool commit)
            {
                if (isClosing) return;
                isClosing = true;

                try
                {
                    if (commit && commitText != null)
                        commitText(edit.Text);
                }
                catch { }

                // Restore original positions (Redraw cũng sẽ reset, nhưng restore giúp mượt)
                try
                {
                    if (leftOverlay != null) canvas.Children.Remove(leftOverlay);
                    canvas.Children.Remove(pitchOverlay);
                    if (rightOverlay != null) canvas.Children.Remove(rightOverlay);
                }
                catch { }

                try { canvas.Children.Remove(edit); } catch { }
                leftTb.Visibility = System.Windows.Visibility.Visible;
                tbPitch.Visibility = System.Windows.Visibility.Visible;
                rightTb.Visibility = System.Windows.Visibility.Visible;

                isClosing = false;
            }

            edit.TextChanged += (s, e) =>
            {
                ApplyLayout();
                edit.CaretIndex = edit.Text.Length;
                edit.Dispatcher.BeginInvoke(new Action(() =>
                {
                    try { edit.ScrollToEnd(); } catch { }
                }), System.Windows.Threading.DispatcherPriority.Input);
            };

            edit.PreviewKeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    e.Handled = true;
                    RestoreAndClose(commit: true);
                }
                else if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    RestoreAndClose(commit: false);
                }
            };

            edit.LostFocus += (s, e) => RestoreAndClose(commit: true);

            ApplyLayout();

            edit.Dispatcher.BeginInvoke(new Action(() =>
            {
                edit.Focus();
                edit.SelectAll();
            }), System.Windows.Threading.DispatcherPriority.Input);
        }
        private sealed class HoverGroupBoxState
        {
            public Border Box;
            public Brush FgDia;
            public Brush FgPitch;
            public Brush FgMat;
            public double Pad;
            public Brush BoxFill;
            public Brush BoxStroke;
            public Thickness BoxThickness;
            public int HoverCount;
        }

        private void EnableEditableHoverWithBox_Group3(
            TextBlock tbDia, TextBlock tbPitch, TextBlock tbMat,
            Canvas canvas,
            Brush hoverForeground,
            Brush hoverFill,
            Brush hoverStroke,
            double pad = -1,
            double strokeThickness = 1.0)
        {
            if (tbDia == null || tbPitch == null || tbMat == null || canvas == null) return;

            if (hoverForeground == null) hoverForeground = Brushes.MediumBlue;
            if (hoverStroke == null) hoverStroke = Brushes.MediumBlue;
            if (hoverFill == null) hoverFill = new SolidColorBrush(Color.FromArgb(25, 30, 144, 255));

            // đảm bảo bắt hover/click
            if (tbDia.Background == null) tbDia.Background = Brushes.Transparent;
            if (tbPitch.Background == null) tbPitch.Background = Brushes.Transparent;
            if (tbMat.Background == null) tbMat.Background = Brushes.Transparent;

            tbDia.IsHitTestVisible = true;
            tbPitch.IsHitTestVisible = true;
            tbMat.IsHitTestVisible = true;

            var box = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(strokeThickness),
                CornerRadius = new CornerRadius(3),
                IsHitTestVisible = false,
                Visibility = System.Windows.Visibility.Collapsed
            };

            int z = Math.Min(Panel.GetZIndex(tbDia), Math.Min(Panel.GetZIndex(tbPitch), Panel.GetZIndex(tbMat))) - 1;
            Panel.SetZIndex(box, z);
            canvas.Children.Add(box);

            var st = new HoverGroupBoxState
            {
                Box = box,
                FgDia = tbDia.Foreground,
                FgPitch = tbPitch.Foreground,
                FgMat = tbMat.Foreground,
                Pad = pad,
                BoxFill = hoverFill,
                BoxStroke = hoverStroke,
                BoxThickness = new Thickness(strokeThickness),
                HoverCount = 0
            };

            void MeasureFallback(TextBlock tb, ref double w, ref double h)
            {
                if (w > 0 && h > 0) return;
                tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                w = tb.DesiredSize.Width;
                h = tb.DesiredSize.Height;
            }

            void UpdateBoxRect()
            {
                Rect r1 = GetElementBoundsOnCanvas(tbDia, canvas);
                Rect r2 = GetElementBoundsOnCanvas(tbPitch, canvas);
                Rect r3 = GetElementBoundsOnCanvas(tbMat, canvas);

                double w1 = r1.Width, h1 = r1.Height; MeasureFallback(tbDia, ref w1, ref h1);
                double w2 = r2.Width, h2 = r2.Height; MeasureFallback(tbPitch, ref w2, ref h2);
                double w3 = r3.Width, h3 = r3.Height; MeasureFallback(tbMat, ref w3, ref h3);

                double l1 = r1.IsEmpty ? 0 : r1.Left;
                double t1 = r1.IsEmpty ? 0 : r1.Top;
                double l2 = r2.IsEmpty ? 0 : r2.Left;
                double t2 = r2.IsEmpty ? 0 : r2.Top;
                double l3 = r3.IsEmpty ? 0 : r3.Left;
                double t3 = r3.IsEmpty ? 0 : r3.Top;

                double left = Math.Min(l1, Math.Min(l2, l3));
                double top = Math.Min(t1, Math.Min(t2, t3));
                double right = Math.Max(l1 + w1, Math.Max(l2 + w2, l3 + w3));
                double bottom = Math.Max(t1 + h1, Math.Max(t2 + h2, t3 + h3));

                Canvas.SetLeft(box, left - st.Pad);
                Canvas.SetTop(box, top - st.Pad);
                box.Width = Math.Max(1, (right - left) + st.Pad * 2);
                box.Height = Math.Max(1, (bottom - top) + st.Pad * 2);

                Panel.SetZIndex(box, z);
            }

            void ApplyHoverOn()
            {
                tbDia.Foreground = hoverForeground;
                tbPitch.Foreground = hoverForeground;
                tbMat.Foreground = hoverForeground;

                box.BorderThickness = st.BoxThickness;
                box.BorderBrush = st.BoxStroke;
                box.Background = st.BoxFill;
                box.Visibility = System.Windows.Visibility.Visible;

                UpdateBoxRect();
            }

            void ApplyHoverOff()
            {
                tbDia.Foreground = st.FgDia;
                tbPitch.Foreground = st.FgPitch;
                tbMat.Foreground = st.FgMat;

                box.Visibility = System.Windows.Visibility.Collapsed;
                box.Background = Brushes.Transparent;

                box.BorderBrush = Brushes.Transparent;
            }

            void Enter()
            {
                st.HoverCount++;
                ApplyHoverOn();
            }

            void Leave()
            {
                st.HoverCount = Math.Max(0, st.HoverCount - 1);
                if (st.HoverCount == 0) ApplyHoverOff();
            }

            tbDia.MouseEnter += (s, e) => Enter();
            tbPitch.MouseEnter += (s, e) => Enter();
            tbMat.MouseEnter += (s, e) => Enter();

            tbDia.MouseLeave += (s, e) => Leave();
            tbPitch.MouseLeave += (s, e) => Leave();
            tbMat.MouseLeave += (s, e) => Leave();

            // Khi bạn “đẩy 2 bên” lúc edit => Canvas.Left thay đổi => box phải bám theo
            EventHandler layoutUpd = (s, e) =>
            {
                if (box.Visibility == System.Windows.Visibility.Visible)
                    UpdateBoxRect();
            };
            tbDia.LayoutUpdated += layoutUpd;
            tbPitch.LayoutUpdated += layoutUpd;
            tbMat.LayoutUpdated += layoutUpd;
        }

    }
}
