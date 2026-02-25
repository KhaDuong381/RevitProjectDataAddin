using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Document = Autodesk.Revit.DB.Document;

namespace RevitProjectDataAddin
{
    public partial class 柱配置リスト : Window
    {
        private Document doc;
        private readonly ProjectData _projectData;
        //private Dictionary<(string 階, string 通), ObservableCollection<柱セグメント>> _hoopSegmentsDict = new Dictionary<(string, string), ObservableCollection<柱セグメント>>();

        private 柱配置図 _currentHaichiList;
        public TrackedObject<柱配置図> _trackedHaichiList;
        public 柱配置リスト(Document document, 柱配置図 targetHoopLayout, ProjectData projectData)
        {
            InitializeComponent();
            this.doc = document;
            //string selectedProjectName = ProjectManager.SelectedProjectName;
            _projectData = projectData;
            _currentHaichiList = targetHoopLayout;

            // đảm bảo map trong model không null (tránh null ref khi dùng)
            if (_currentHaichiList.BeamSegmentsMap == null)
            {
                _currentHaichiList.BeamSegmentsMap =
                    new Dictionary<string, ObservableCollection<柱セグメント>>();
            }

            DataContext = _currentHaichiList;
            Load();
            _trackedHaichiList = new TrackedObject<柱配置図>(_currentHaichiList);

            this.Closing += Close;
        }

        // Helper tạo key chuỗi an toàn cho map
        private static string MakeKey(string kai, string tsu)
        {
            return string.Format("{0}::{1}", kai ?? string.Empty, tsu ?? string.Empty);
        }
        public void Load()
        {
            UpdateComboBoxItemsSource();

            var kaiList = _projectData.Kihon.NameKai.Select(k => k.Name).ToList();
            var tsuList = _projectData.Kihon.NameY.Select(y => y.Name).ToList();

            // 階
            if (string.IsNullOrEmpty(_currentHaichiList.階を選択) || !kaiList.Contains(_currentHaichiList.階を選択))
                _currentHaichiList.階を選択 = kaiList.FirstOrDefault();

            // 通  ← kiểm tra cả “không thuộc list”
            if (string.IsNullOrEmpty(_currentHaichiList.通を選択) || !tsuList.Contains(_currentHaichiList.通を選択))
                _currentHaichiList.通を選択 = tsuList.FirstOrDefault();

            // (Tùy chọn) không cần set SelectedItem bằng code-behind nếu đã Binding TwoWay
            階を選択ComboBox.SelectedItem = _currentHaichiList.階を選択;
            通を選択ComboBox.SelectedItem = _currentHaichiList.通を選択;

            SetupTextBoxColors();
            ComboBox_SelectionChanged(null, null);
            
        }

        // Fix for CS0119, CS1002, CS1513 errors in Canvas_Loaded

        private void UpdateComboBoxItemsSource()
        {
            階を選択ComboBox.ItemsSource = _projectData.Kihon.NameKai
                .Select(kai => kai.Name)
                .ToList();

            通を選択ComboBox.ItemsSource = _projectData.Kihon.NameY.Select(y => y.Name)
                .ToList();
        }
        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedKai = 階を選択ComboBox.SelectedItem as string;
            var selectedTsu = 通を選択ComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedKai) || string.IsNullOrEmpty(selectedTsu)) return;

            var key = MakeKey(selectedKai, selectedTsu);

            // ✅ Cache trong model
            var dict = _currentHaichiList.BeamSegmentsMap;
            if (dict == null)
            {
                dict = new Dictionary<string, ObservableCollection<柱セグメント>>();
                _currentHaichiList.BeamSegmentsMap = dict;
            }

            // 1) Lấy hoặc tạo segments
            if (!dict.TryGetValue(key, out var segments))
            {
                // 1a) Tái dùng nếu current đúng prefix
                if (_currentHaichiList.BeamSegments != null
                    && _currentHaichiList.BeamSegments.Any()
                    && _currentHaichiList.BeamSegments.All(s => s.位置表示 != null &&
                                                                s.位置表示.StartsWith($"{selectedKai} {selectedTsu}")))
                {
                    segments = _currentHaichiList.BeamSegments;
                }
                else
                {
                    // 1b) Tạo mới
                    segments = new ObservableCollection<柱セグメント>();
                }
                dict[key] = segments;
            }

            // 2) LUÔN đồng bộ theo NameX vì 通 luôn là Y
            var xNames = _projectData.Kihon.NameX.Select(x => x.Name).ToList();
            var sweepNames = xNames; // thông số quét
            SyncSegments(segments, sweepNames, selectedKai, selectedTsu); // <— QUAN TRỌNG

            // 3) Gán vào UI (detaching khỏi bộ cũ nếu có)
            _currentHaichiList.BeamSegments = segments;

            // 4) Lấy danh sách cột theo floor
            var selected柱リスト = _projectData.リスト.柱リスト.FirstOrDefault(x => x.各階 == selectedKai);
            var 柱Names = selected柱リスト?.柱?.Select(b => b.Name).ToList() ?? new List<string> { "C0" };

            // 5) Thay ItemsSource mới cho combo
            _currentHaichiList.ColumnNames = new ObservableCollection<string>(柱Names);

            // 6) Chuẩn hoá selection
            var firstName = _currentHaichiList.ColumnNames.FirstOrDefault() ?? "C0";
            foreach (var s in _currentHaichiList.BeamSegments)
            {
                if (string.IsNullOrEmpty(s.柱の符号) || !_currentHaichiList.ColumnNames.Contains(s.柱の符号))
                    s.柱の符号 = firstName;
            }

            SetupTextBoxColors();

            // (Tuỳ chọn) Nếu muốn vẽ ngay:
            //ShowSingleSegment("1F Y1-X2");
        }

        void SyncSegments(ObservableCollection<柱セグメント> segs, List<string> names, string kai, string tsu)
        {
            // 1) Nếu số lượng khác → rebuild
            if (segs.Count != names.Count)
            {
                segs.Clear();
                for (int i = 0; i < names.Count; i++)
                {
                    segs.Add(new 柱セグメント
                    {
                        柱の符号 = null,
                        上側のズレ = "500",
                        下側のズレ = "500",
                        左側のズレ = "500",
                        右側のズレ = "500",
                        位置表示 = $"{kai} {tsu}-{names[i]}",
                    });
                }
                return;
            }

            // 2) Nếu số lượng khớp → cập nhật 位置表示 in-place
            for (int i = 0; i < segs.Count; i++)
                segs[i].位置表示 = $"{kai} {tsu}-{names[i]}";
        }

        private void Canvas_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Canvas canvas && canvas.DataContext is 柱セグメント segment)
            {
                // Vẽ lần đầu
                Redraw(canvas, segment);

                // Theo dõi thay đổi thuộc tính để vẽ lại
                segment.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(柱セグメント.柱の符号) ||
                        args.PropertyName == nameof(柱セグメント.上側のズレ) ||
                        args.PropertyName == nameof(柱セグメント.下側のズレ) ||
                        args.PropertyName == nameof(柱セグメント.左側のズレ) ||
                        args.PropertyName == nameof(柱セグメント.右側のズレ))
                    {
                        // BẮT BUỘC: lấy lại giá trị MỚI từ segment và vẽ lại trên UI thread
                        canvas.Dispatcher.Invoke(() => Redraw(canvas, segment));
                    }
                };
            }
        }

        private void Redraw(Canvas canvas, 柱セグメント segment)
        {
            canvas.Children.Clear();

            // (Ví dụ) khung tham chiếu
            var rect = new System.Windows.Shapes.Rectangle
            {
                Width = 100,
                Height = 50,
                Stroke = Brushes.Blue,
                StrokeThickness = 2
            };
            Canvas.SetLeft(rect, 10);
            Canvas.SetTop(rect, 10);
            canvas.Children.Add(rect);          
            
            DrawIlluskiso(canvas, 0, 0);
        }
        private void Close(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_trackedHaichiList.HasChanged())
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
                    _trackedHaichiList.RestoreOriginal();
                }
            }
            else
            {
            }

        }
        public void NumberTextBox_PreviewTextInput1(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                // Chỉ cho phép nhập số
                if (!char.IsDigit(e.Text, 0))
                {
                    textBox.Background = Brushes.LightCoral;
                    e.Handled = true;
                    return;
                }

                // Giả lập chuỗi mới sau khi nhập
                string newText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
                    .Insert(textBox.SelectionStart, e.Text);

                // Nếu chuỗi mới là "0" thì cho phép
                if (newText == "0")
                {
                    textBox.Background = Brushes.White;
                    e.Handled = false;
                    return;
                }

                // Nếu chuỗi mới có độ dài > 1 và bắt đầu bằng '0' thì không cho phép
                if (newText.Length > 1 && newText.StartsWith("0"))
                {
                    textBox.Background = Brushes.LightCoral;
                    e.Handled = true;
                    return;
                }

                // Nếu giá trị vượt quá 99999 hoặc vượt quá MaxLength thì không cho phép
                if ((int.TryParse(newText, out int newValue) && newValue > 99999) || newText.Length > 5)
                {
                    textBox.Background = Brushes.LightCoral;
                    e.Handled = true;
                    return;
                }

                // Trường hợp hợp lệ
                textBox.Background = Brushes.White;
                e.Handled = false;
            }
        }
        private void SetupTextBoxColors()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var TextBoxx = FindVisualChildren<TextBox>(this);

                foreach (var textBox in TextBoxx)
                {
                    SetupTextBoxKha(textBox);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
        private void SetupTextBoxKha(TextBox textBox)
        {
            void UpdateColors(Brush background)
            {
                textBox.Background = background;
            }

            void SetActiveColors()
            {
                UpdateColors(Brushes.LightGreen);
            }

            void SetInactiveColors()
            {
                UpdateColors(Brushes.White);
            }

            textBox.GotFocus += (s, e) =>
            {
                // Tô màu khi TextBox được focus
                SetActiveColors();

                // Chọn toàn bộ nội dung trong TextBox
                textBox.SelectAll();
            };

            textBox.PreviewMouseDown += (s, e) =>
            {
                // Đảm bảo TextBox không bị mất focus khi nhấp chuột
                if (!textBox.IsFocused)
                {
                    e.Handled = true;
                    textBox.Focus();
                }
            };

            textBox.LostFocus += (s, e) =>
            {
                // Khôi phục màu nền mặc định khi TextBox mất focus
                SetInactiveColors();
            };

            textBox.MouseLeave += (s, e) =>
            {
                if (!textBox.IsFocused)
                {
                    SetInactiveColors();
                }
            };
            textBox.MouseEnter += (s, e) => SetActiveColors();
        }

    }

}
