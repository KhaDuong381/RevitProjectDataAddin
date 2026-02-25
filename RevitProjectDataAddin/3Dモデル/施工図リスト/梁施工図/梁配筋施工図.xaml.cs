using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace RevitProjectDataAddin
{
    public partial class 梁配筋施工図 : Window
    {
        private Document doc;
        private readonly ProjectData _projectData;
        private 梁施工図 _currentSecoList;
        public TrackedObject<梁施工図> _trackedSecoList;
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

        private void Close(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_trackedSecoList.HasChanged())
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
                    _trackedSecoList.RestoreOriginal();
                }
            }
        }
    }
}
