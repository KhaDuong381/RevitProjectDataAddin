using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Microsoft.Win32;

namespace RevitProjectDataAddin
{
    public partial class 梁配筋施工図 : Window
    {
        private const double PdfSceneCanvasWidth = 1400.0;
        private const double PdfSceneCanvasHeight = 1000.0;

        private Document doc;
        private readonly ProjectData _projectData;
        private 梁施工図 _currentSecoList;
        public TrackedObject<梁施工図> _trackedSecoList;
        private TrackedObject<KihonData> _trackedKihonData;
        private KesanData _kesan;
        private Z梁の配置 _z梁の配置;

        private readonly Dictionary<string, 梁施工図> _secoMap = new Dictionary<string, 梁施工図>();
        private string _currentKey;

        private static string MakeKey(string kai, string tsu) => $"{kai}::{tsu}";

        public 梁配筋施工図(Document document, 梁施工図 targetBeamSecozu, ProjectData projectData)
        {
            InitializeComponent();
            doc = document;
            _projectData = projectData;
            _currentSecoList = targetBeamSecozu ?? new 梁施工図();

            DataContext = _currentSecoList;
            Load();
            _trackedSecoList = new TrackedObject<梁施工図>(_currentSecoList);
            _trackedKihonData = new TrackedObject<KihonData>(_projectData.Kihon);

            this.Closing += Close;
        }

        public void Load()
        {
            UpdateComboBoxItemsSource();

            var kaiList = _projectData.Kihon.NameKai.Select(k => k.Name).ToList();
            var tsuList = _projectData.Kihon.NameX.Select(x => x.Name)
                          .Concat(_projectData.Kihon.NameY.Select(y => y.Name)).ToList();

            if (string.IsNullOrEmpty(_currentSecoList.階を選択) || !kaiList.Contains(_currentSecoList.階を選択))
                _currentSecoList.階を選択 = kaiList.FirstOrDefault();

            if (string.IsNullOrEmpty(_currentSecoList.通を選択) || !tsuList.Contains(_currentSecoList.通を選択))
                _currentSecoList.通を選択 = tsuList.FirstOrDefault();

            階を選択ComboBox.SelectedItem = _currentSecoList.階を選択;
            通を選択ComboBox.SelectedItem = _currentSecoList.通を選択;

            _currentKey = MakeKey(_currentSecoList.階を選択, _currentSecoList.通を選択);
            if (!_secoMap.ContainsKey(_currentKey))
                _secoMap[_currentKey] = _currentSecoList;

            // Khởi tạo bộ theo (階, 通) hiện tại
            Combo_SelectionChanged(null, null);
        }

        private void BotsecozuCanvas_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Canvas canvas && canvas.DataContext is GridBotsecozu item)
            {
                //ApplyPaperSizeToCanvas(canvas);
                Redraw(canvas, item);

                item.PropertyChanged += (s, args) =>
                canvas.Dispatcher.Invoke(() => Redraw(canvas, item));
                //canvas.Dispatcher.Invoke(() =>
                //    {
                //        ApplyPaperSizeToCanvas(canvas);
                //        Redraw(canvas, item);
                //    });

                // [ZOOM] Wire events
                canvas.Focusable = true;    // để nhận phím (phím F)
                //canvas.MouseWheel += Canvas_MouseWheel;
                canvas.AddHandler(
                    UIElement.MouseWheelEvent,
                    new MouseWheelEventHandler(Canvas_MouseWheel),
                    /*handledEventsToo:*/ true
                );
                canvas.MouseDown += Canvas_MouseDown;
                canvas.MouseMove += Canvas_MouseMove;
                canvas.MouseUp += Canvas_MouseUp;
                //canvas.MouseLeave += Canvas_MouseUp;
                canvas.KeyDown += Canvas_KeyDown;
                // trong BotsecozuCanvas_Loaded(...)
                canvas.Background = Brushes.Transparent; // vùng trống vẫn bắt sự kiện
                canvas.ClipToBounds = true;                   // CHẶN vẽ tràn ra ngoài
                canvas.SizeChanged += (_, __) =>
                    canvas.Clip = new RectangleGeometry(new Rect(0, 0, canvas.ActualWidth, canvas.ActualHeight));

            }
        }

        //private void BotsecozuCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        //{
        //    if (sender is Canvas canvas && canvas.DataContext is GridBotsecozu item)
        //        Redraw(canvas, item);
        //}

        private void UpdateComboBoxItemsSource()
        {
            階を選択ComboBox.ItemsSource = _projectData.Kihon.NameKai
                .Select(kai => kai.Name).ToList();

            通を選択ComboBox.ItemsSource = _projectData.Kihon.NameX.Select(x => x.Name)
                .Concat(_projectData.Kihon.NameY.Select(y => y.Name)).ToList();
        }

        private void Combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!(階を選択ComboBox.SelectedItem is string kai)) return;
            if (!(通を選択ComboBox.SelectedItem is string tsu)) return;

            var key = MakeKey(kai, tsu);

            var dict = _currentSecoList.GridBotsecozuMap;
            if (dict == null)
            {
                dict = new Dictionary<string, ObservableCollection<GridBotsecozu>>();
                _currentSecoList.GridBotsecozuMap = dict;
            }

            if (!dict.TryGetValue(key, out var grids))
            {
                grids = new ObservableCollection<GridBotsecozu>
                {
                    new GridBotsecozu()
                };
                dict[key] = grids;
            }

            _currentSecoList.gridbotsecozu = grids;
            _currentKey = key;
            // Không cần gọi Redraw ở đây – các Canvas mới sẽ tự Loaded và vẽ.
        }

        private void ExportPdfScene_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSecoList?.gridbotsecozu == null || _currentSecoList.gridbotsecozu.Count == 0)
            {
                MessageBox.Show("Không có gì để xuất.");
                return;
            }

            GridList?.UpdateLayout();
            var sources = EnumerateCanvasVisuals()
                .Select(canvas => new { canvas, item = canvas?.DataContext as GridBotsecozu })
                .Where(x => x.canvas != null && x.item != null)
                .Select(x => new PdfExportSource(x.item, x.canvas, BuildDxfGeometry(x.item).fileKey))
                .ToList();

            if (sources.Count == 0)
            {
                MessageBox.Show("Không tìm thấy canvas để plot.");
                return;
            }

            var plotSettings = ShowPdfPlotDialog(sources);
            if (plotSettings == null)
                return;

            var dlg = new SaveFileDialog
            {
                Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"{_currentSecoList.階を選択}_{_currentSecoList.通を選択}.pdf"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var scenePages = BuildPdfScenePages(sources, plotSettings);
                PdfExporter.Export(
                    dlg.FileName,
                    scenePages,
                    FontFamily?.Source ?? "Yu Mincho",
                    plotSettings);

                MessageBox.Show("PDF exported!");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Xuất PDF thất bại: {ex.Message}");
            }
        }

        private List<PdfScenePageData> BuildPdfScenePages(IReadOnlyList<PdfExportSource> sources, PdfPlotSettings plotSettings)
        {
            var pages = new List<PdfScenePageData>();
            var selectedKeys = plotSettings?.SelectedKeys ?? new List<string>();
            var selectedSet = new HashSet<string>(selectedKeys, StringComparer.Ordinal);
            var paperSize = plotSettings?.PaperSize ?? PdfPaperSize.A4;

            foreach (var src in sources)
            {
                if (src?.Item == null) continue;
                if (selectedSet.Count > 0 && !selectedSet.Contains(src.Key)) continue;

                var scene = CaptureSceneForPdfExport(src.Item, src.Key);
                var viewportWindows = BuildPdfViewportWindows(paperSize, scene);
                if (viewportWindows == null || viewportWindows.Count == 0)
                {
                    viewportWindows = new List<PdfViewportWindow>
                    {
                        new PdfViewportWindow
                        {
                            PageIndex = 0,
                            PageCount = 1
                        }
                    };
                }

                foreach (var viewportWindow in viewportWindows)
                {
                    string pageKey = viewportWindow.PageCount > 1 ? $"{src.Key}_{viewportWindow.PageIndex + 1}" : src.Key;
                    pages.Add(new PdfScenePageData(pageKey, scene, viewportWindow));
                }
            }

            return pages;
        }

        private IReadOnlyList<object> CaptureSceneForPdfExport(GridBotsecozu item, string key)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            var scratchCanvas = new Canvas
            {
                Width = PdfSceneCanvasWidth,
                Height = PdfSceneCanvasHeight,
                Visibility = System.Windows.Visibility.Collapsed,
                IsHitTestVisible = false
            };
            scratchCanvas.Measure(new Size(PdfSceneCanvasWidth, PdfSceneCanvasHeight));
            scratchCanvas.Arrange(new Rect(0, 0, PdfSceneCanvasWidth, PdfSceneCanvasHeight));
            scratchCanvas.UpdateLayout();

            var viewState = VS(item);
            double zoom = viewState.Zoom;
            double panX = viewState.PanXmm;
            double panY = viewState.PanYmm;

            try
            {
                // Export always records from a normalized view state so PDF is not tied to UI pan/zoom.
                viewState.Zoom = 1.0;
                viewState.PanXmm = 0.0;
                viewState.PanYmm = 0.0;
                Redraw(scratchCanvas, item);
            }
            finally
            {
                viewState.Zoom = zoom;
                viewState.PanXmm = panX;
                viewState.PanYmm = panY;
            }

            if (_sceneByItem.TryGetValue(item, out var scene) && scene != null && scene.Count > 0)
                return scene.ToList();

            throw new InvalidOperationException($"Scene geometry chưa sẵn sàng cho: {key}");
        }

        private PdfPaperSize GetCurrentPdfPaperSize()
            => _projectData?.Kesan?.Printsize2 == true ? PdfPaperSize.A3 : PdfPaperSize.A4;

        private sealed class PdfScenePageData
        {
            public PdfScenePageData(string key, IReadOnlyList<object> scene, PdfViewportWindow viewportWindow)
            {
                Key = string.IsNullOrWhiteSpace(key) ? "page" : key;
                Scene = scene ?? throw new ArgumentNullException(nameof(scene));
                ViewportWindow = viewportWindow;
            }

            public string Key { get; }
            public IReadOnlyList<object> Scene { get; }
            public PdfViewportWindow ViewportWindow { get; }
        }

        private static class PdfExporter
        {
            public static void Export(
                string path,
                IReadOnlyList<PdfScenePageData> scenePages,
                string fallbackFont,
                PdfPlotSettings plotSettings)
            {
                if (scenePages == null || scenePages.Count == 0)
                    throw new ArgumentException("No scene pages to export.", nameof(scenePages));
                if (plotSettings == null)
                    throw new ArgumentNullException(nameof(plotSettings));

                var vectorPages = new List<PdfVectorPage>();
                foreach (var scenePage in scenePages)
                {
                    CollectSceneEntities(
                        scenePage.Scene,
                        out var lines,
                        out var texts,
                        out var circles,
                        out var arcs,
                        out var solids);

                    var pageSettings = new PdfPlotSettings
                    {
                        PaperSize = plotSettings.PaperSize,
                        Orientation = plotSettings.Orientation,
                        ScaleDenominator = plotSettings.ScaleDenominator,
                        TitleText = plotSettings.TitleText,
                        DateText = plotSettings.DateText,
                        SelectedKeys = plotSettings.SelectedKeys != null ? new List<string>(plotSettings.SelectedKeys) : new List<string>(),
                        ViewportWindow = scenePage.ViewportWindow
                    };

                    var page = PdfVectorBuilder.Create(
                        scenePage.Key,
                        lines,
                        texts,
                        circles,
                        arcs,
                        solids,
                        fallbackFont,
                        pageSettings);

                    if (page != null)
                        vectorPages.Add(page);
                }

                if (vectorPages.Count == 0)
                    throw new InvalidOperationException("No vector pages were created from the recorded scene.");

                PdfVectorWriter.WritePdf(path, vectorPages);
            }

            private static void CollectSceneEntities(
                IReadOnlyList<object> scene,
                out List<DxfLine> lines,
                out List<DxfText> texts,
                out List<DxfCircle> circles,
                out List<DxfArc> arcs,
                out List<DxfSolid> solids)
            {
                lines = new List<DxfLine>();
                texts = new List<DxfText>();
                circles = new List<DxfCircle>();
                arcs = new List<DxfArc>();
                solids = new List<DxfSolid>();

                if (scene == null) return;

                foreach (var entity in scene)
                {
                    if (entity is SceneLine ln)
                    {
                        lines.Add(new DxfLine(ln.X1, ln.Y1, ln.X2, ln.Y2, ln.Layer, ln.Thickness, ln.Dash, ln.StrokeColor));
                    }
                    else if (entity is DxfText text)
                    {
                        texts.Add(text);
                    }
                    else if (entity is DxfCircle circle)
                    {
                        circles.Add(circle);
                    }
                    else if (entity is DxfArc arc)
                    {
                        arcs.Add(arc);
                    }
                    else if (entity is DxfSolid solid)
                    {
                        solids.Add(solid);
                    }
                }
            }
        }

        private void Close(object sender, System.ComponentModel.CancelEventArgs e)
        {
            bool hasSecoChanges = _trackedSecoList?.HasChanged() == true;
            bool hasKihonChanges = _trackedKihonData?.HasChanged() == true;

            if (hasSecoChanges || hasKihonChanges)
            {
                var result = MessageBox.Show(
                    "データが変更されています。保存しますか？",
                    "確認",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    StorageUtils.SaveProject(doc, _projectData);
                    DialogResult = true;
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
                else if (result == MessageBoxResult.No)
                {
                    _trackedSecoList?.RestoreOriginal();
                    _trackedKihonData?.RestoreOriginal();
                }
            }
        }
    }
}
