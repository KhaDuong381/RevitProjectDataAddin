using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Linq;
using Autodesk.Revit.UI;
using ComboBox = System.Windows.Controls.ComboBox;
using Document = Autodesk.Revit.DB.Document;
using TextBox = System.Windows.Controls.TextBox;

namespace RevitProjectDataAddin
{
    public partial class 梁の配置リスト : Window
    {
        private readonly ProjectData _projectData;
        private Document doc;

        private 梁配置図 _currentHaichiList;
        public TrackedObject<梁配置図> _trackedHaichiList;

        public 梁の配置リスト(Document document, 梁配置図 targetBeamLayout, ProjectData projectData)
        {
            InitializeComponent();

            this.doc = document;
            _projectData = projectData;

            _currentHaichiList = targetBeamLayout ?? new 梁配置図();

            // đảm bảo map trong model không null (tránh null ref khi dùng)
            if (_currentHaichiList.BeamSegmentsMap == null)
            {
                _currentHaichiList.BeamSegmentsMap =
                    new Dictionary<string, ObservableCollection<梁セグメント>>();
            }

            // 1) Gán DataContext trước
            DataContext = _currentHaichiList;

            // 2) Khởi tạo UI + đặt giá trị mặc định (階/通, BeamSegments, 梁候補リスト…)
            Load();

            // 3) Sau khi model đã ổn định mặc định → chụp snapshot
            _trackedHaichiList = new TrackedObject<梁配置図>(_currentHaichiList);

            // 4) Đăng ký Close sau cùng
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
            var tsuList = _projectData.Kihon.NameX.Select(x => x.Name).ToList();

            // 階
            if (string.IsNullOrEmpty(_currentHaichiList.階を選択) || !kaiList.Contains(_currentHaichiList.階を選択))
                _currentHaichiList.階を選択 = kaiList.FirstOrDefault();

            // 通  ← kiểm tra cả “không thuộc list”
            if (string.IsNullOrEmpty(_currentHaichiList.通を選択) || !tsuList.Contains(_currentHaichiList.通を選択))
                _currentHaichiList.通を選択 = tsuList.FirstOrDefault();

            // Ấn định UI theo model
            階を選択ComboBox.SelectedItem = _currentHaichiList.階を選択;
            通を選択ComboBox.SelectedItem = _currentHaichiList.通を選択;

            SetupTextBoxColors();
            // Tạo/áp segments dựa trên 2 giá trị này
            ComboBox_SelectionChanged(null, null);
        }

        private void UpdateComboBoxItemsSource()
        {
            階を選択ComboBox.ItemsSource = _projectData.Kihon.NameKai
                .Select(kai => kai.Name)
                .ToList();

            通を選択ComboBox.ItemsSource = _projectData.Kihon.NameX
                .Select(x => x.Name)
                .Concat(_projectData.Kihon.NameY.Select(y => y.Name))
                .ToList();
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedKai = 階を選択ComboBox.SelectedItem as string;
            var selectedTsu = 通を選択ComboBox.SelectedItem as string;

            if (string.IsNullOrEmpty(selectedKai) || string.IsNullOrEmpty(selectedTsu))
                return;

            var key = MakeKey(selectedKai, selectedTsu);

            // ✅ Dùng cache nằm trong model (key: string)
            var dict = _currentHaichiList.BeamSegmentsMap;
            if (dict == null)
            {
                dict = new Dictionary<string, ObservableCollection<梁セグメント>>();
                _currentHaichiList.BeamSegmentsMap = dict;
            }

            // 1) Resolve BeamSegments cho (階, 通)
            if (!dict.TryGetValue(key, out var segments))
            {
                // 1a) Tái dùng nếu current đã đúng prefix
                if (_currentHaichiList.BeamSegments != null
                    && _currentHaichiList.BeamSegments.Any()
                    && _currentHaichiList.BeamSegments.All(s =>
                        s.タイトル != null &&
                        s.タイトル.StartsWith(string.Format("{0} {1}の", selectedKai, selectedTsu))))
                {
                    segments = _currentHaichiList.BeamSegments;
                }
                else
                {
                    // 1b) Chưa có thì tạo mới
                    segments = new ObservableCollection<梁セグメント>();
                }

                dict[key] = segments;
            }

            // 2) ✅ LUÔN đồng bộ theo trục sweep (giống kiểu base)
            var xNames = _projectData.Kihon.NameX.Select(x => x.Name).ToList();
            var yNames = _projectData.Kihon.NameY.Select(y => y.Name).ToList();

            // 通 đang chọn là trục nào? Nếu 通 nằm trong X => sweep theo Y, ngược lại sweep theo X
            var sweepNames = xNames.Contains(selectedTsu) ? yNames : xNames;

            SyncSegments(segments, sweepNames, selectedKai, selectedTsu); // <— QUAN TRỌNG

            // 3) Gán segments cho UI (detaching khỏi bộ cũ nếu có)
            _currentHaichiList.BeamSegments = segments;

            // 4) Luôn lấy danh sách 梁 theo 階; nếu không có thì ["G0"]
            var selected梁リスト = _projectData.リスト.梁リスト.FirstOrDefault(x => x.各階 == selectedKai);
            var 梁Names = (selected梁リスト?.梁 != null)
                ? selected梁リスト.梁.Select(b => b.Name).ToList()
                : new List<string> { "G0" };

            // 5) Thay ItemsSource bằng collection mới
            _currentHaichiList.梁候補リスト = new ObservableCollection<string>(梁Names);

            // 6) Chuẩn hóa selection
            var firstName = _currentHaichiList.梁候補リスト.FirstOrDefault() ?? "G0";
            foreach (var s in _currentHaichiList.BeamSegments)
            {
                if (string.IsNullOrEmpty(s.梁の符号) || !_currentHaichiList.梁候補リスト.Contains(s.梁の符号))
                    s.梁の符号 = firstName;
            }

            SetupTextBoxColors();
        }

        void SyncSegments(ObservableCollection<梁セグメント> segs, List<string> names, string kai, string tsu)
        {
            // 梁 segment là (names[i] -> names[i+1]) nên cần count = names.Count - 1
            int required = Math.Max(0, names.Count - 1);

            // 1) Nếu số lượng khác → rebuild
            if (segs.Count != required)
            {
                segs.Clear();
                for (int i = 0; i < required; i++)
                {
                    segs.Add(new 梁セグメント
                    {
                        梁の符号 = null,
                        上側のズレ寸法 = "300",
                        下側のズレ寸法 = "300",
                        梁の段差 = "-200",
                        タイトル = string.Format("{0} {1}の{2}-{3}", kai, tsu, names[i], names[i + 1]),
                        左側 = names[i],
                        右側 = names[i + 1],
                    });
                }
                return;
            }

            // 2) Nếu số lượng khớp → cập nhật in-place (giữ binding + dữ liệu khác)
            for (int i = 0; i < required; i++)
            {
                segs[i].タイトル = string.Format("{0} {1}の{2}-{3}", kai, tsu, names[i], names[i + 1]);
                segs[i].左側 = names[i];
                segs[i].右側 = names[i + 1];
            }
        }


        private void Canvas_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is Canvas canvas && canvas.DataContext is 梁セグメント segment)
            {
                // Vẽ lần đầu
                Redraw(canvas, segment);

                // Theo dõi thay đổi thuộc tính để vẽ lại
                segment.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(梁セグメント.左側) ||
                        args.PropertyName == nameof(梁セグメント.右側) ||
                        args.PropertyName == nameof(梁セグメント.梁の符号) ||
                        args.PropertyName == nameof(梁セグメント.上側のズレ寸法) ||
                        args.PropertyName == nameof(梁セグメント.下側のズレ寸法) ||
                        args.PropertyName == nameof(梁セグメント.梁の段差))
                    {
                        // vẽ lại trên UI thread
                        canvas.Dispatcher.Invoke(() => Redraw(canvas, segment));
                    }
                };
            }
        }

        private void Redraw(Canvas canvas, 梁セグメント segment)
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

            // Lấy GIÁ TRỊ MỚI từ segment
            var left = segment.左側 ?? string.Empty;
            var right = segment.右側 ?? string.Empty;
            var G0 = segment.梁の符号 ?? string.Empty;
            var 上側のズレ寸法 = segment.上側のズレ寸法 ?? string.Empty;
            var 下側のズレ寸法 = segment.下側のズレ寸法 ?? string.Empty;
            var 梁の段差 = segment.梁の段差 ?? string.Empty;

            DrawIllustration3(canvas, left, right, G0, 上側のズレ寸法, 下側のズレ寸法, 梁の段差);
        }

        // 梁の配置リスト.xaml.cs
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
        private bool IsAllowNegativeTextBox(TextBox textBox)
        {
            var names = new[]
            {
                "梁の段差",
            };
            return names.Contains(textBox.Name);
        }
        private void NumberTextBox_PreviewTextInput1(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                bool allowNegative = IsAllowNegativeTextBox(textBox);
                char inputChar = e.Text[0];

                // Allow '-' only at the start if negative is allowed
                if (allowNegative && inputChar == '-')
                {
                    // Only allow '-' as the first character and not already present
                    if (textBox.SelectionStart == 0 && !textBox.Text.StartsWith("-"))
                    {
                        textBox.Background = Brushes.White;
                        e.Handled = false;
                        return;
                    }
                    else
                    {
                        textBox.Background = Brushes.LightCoral;
                        e.Handled = true;
                        return;
                    }
                }

                // Cho phép số và dấu chấm (.)
                if (!char.IsDigit(inputChar) && inputChar != '.')
                {
                    textBox.Background = Brushes.LightCoral;
                    e.Handled = true;
                    return;
                }

                // Giả lập chuỗi mới sau khi nhập
                string newText = textBox.Text.Remove(textBox.SelectionStart, textBox.SelectionLength)
                    .Insert(textBox.SelectionStart, e.Text);

                // Prevent '-' and '.' being adjacent (e.g., '-.012' or '.-012')
                if (newText.Contains("-.") || newText.Contains(".-"))
                {
                    textBox.Background = Brushes.LightCoral;
                    e.Handled = true;
                    return;
                }
                // Allow negative sign at the start if allowed
                if (allowNegative)
                {
                    // Only one '-' at the start
                    if (newText.Count(c => c == '-') > 1 || (newText.IndexOf('-') > 0))
                    {
                        textBox.Background = Brushes.LightCoral;
                        e.Handled = true;
                        return;
                    }
                }
                else
                {
                    // Not allowed to have '-'
                    if (newText.Contains('-'))
                    {
                        textBox.Background = Brushes.LightCoral;
                        e.Handled = true;
                        return;
                    }
                }

                // Không cho phép nhiều hơn một dấu chấm
                if (newText.Count(c => c == '.') > 1)
                {
                    textBox.Background = Brushes.LightCoral;
                    e.Handled = true;
                    return;
                }

                // Không cho phép bắt đầu bằng dấu chấm
                if (newText.StartsWith("."))
                {
                    textBox.Background = Brushes.LightCoral;
                    e.Handled = true;
                    return;
                }

                // Không cho phép số 0 đứng trước (trừ trường hợp "0." hoặc "0")
                string checkText = newText.StartsWith("-") ? newText.Substring(1) : newText;
                if (newText.Length > 1 && newText.StartsWith("0") && newText[1] != '.')
                {
                    textBox.Background = Brushes.LightCoral;
                    e.Handled = true;
                    return;
                }

                // Kiểm tra độ dài tối đa (n ký tự, có thể điều chỉnh nếu cần)
                if (newText.Length > 10)
                {
                    textBox.Background = Brushes.LightCoral;
                    e.Handled = true;
                    return;
                }

                // Kiểm tra giá trị tối đa (99999)
                if (double.TryParse(newText, out double newValue) && newValue > 99999 || newValue < -99999)
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
