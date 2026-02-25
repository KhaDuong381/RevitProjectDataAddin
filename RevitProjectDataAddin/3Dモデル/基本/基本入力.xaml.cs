using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Autodesk.Revit.DB;
using TextBox = System.Windows.Controls.TextBox;

namespace RevitProjectDataAddin
{
    public partial class 基本入力 : Window
    {
        private readonly Document _doc;
        private readonly ProjectData _projectData;
        //private bool _isDataChanged = false;
        private KihonData _currentKihonData;
        public TrackedObject<KihonData> _trackedKihonData;

        public 基本入力(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            string selectedProjectName = ProjectManager.SelectedProjectName;
            _projectData = StorageUtils.LoadProject(_doc, selectedProjectName);
            _currentKihonData = _projectData.Kihon;
            DataContext = _projectData.Kihon;
            NameX.ItemsSource = _projectData.Kihon.NameX;
            NameY.ItemsSource = _projectData.Kihon.NameY;
            xTextBox.ItemsSource = _projectData.Kihon.ListSpanX;
            yTextBox.ItemsSource = _projectData.Kihon.ListSpanY;
            NameKai.ItemsSource = _projectData.Kihon.NameKai;
            ListSpanKai.ItemsSource = _projectData.Kihon.ListSpanKai;

            Loaded += Window_Loaded;
            // Đoạn mã này sẽ được thực thi khi MainWindow được tạo hoặc khi bạn khởi tạo đối tượng TrackedObject
            _trackedKihonData = new TrackedObject<KihonData>(_currentKihonData);
            // Đăng ký sự kiện OnTrackedKakouchoChanged
            _trackedKihonData.Changed += OnTrackedKihonDataChanged; // Lắng nghe sự kiện thay đổi


            Closing += 基本入力_Closing; // Đăng ký sự kiện Closing
        }
        private void 基本入力_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (NUMBERXTEXTBOX.Text == "0" || NUMBERYTEXTBOX.Text == "0" || NUMBERKAITEXTBOX.Text == "0")
            {
                MessageBox.Show("Giá trị không được là 0!!");
                e.Cancel = true;
                return;
            }
            if (_trackedKihonData.HasChanged())
            {
                // Hiển thị hộp thoại xác nhận
                var result = MessageBox.Show(
                    "Dữ liệu đã thay đổi. Bạn có muốn lưu không?",
                    "Xác nhận",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    NormalizeAllTextBoxValues();
                    // Lưu dữ liệu
                    StorageUtils.SaveProject(_doc, _projectData);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    // Hủy đóng cửa sổ
                    e.Cancel = true; // Corrected from e.Handled to e.Cancel
                }
                // Nếu chọn No thì không làm gì, cửa sổ sẽ đóng
            }
        }
        private void PreventPasteForTextBox(TextBox textBox)
        {
            DataObject.AddPastingHandler(textBox, OnTextBoxPasting);
            textBox.PreviewKeyDown += OnTextBoxPreviewKeyDown;
        }

        private void OnTextBoxPasting(object sender, DataObjectPastingEventArgs e)
        {
            e.CancelCommand();
        }

        private void OnTextBoxPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.V) ||
                (Keyboard.Modifiers == ModifierKeys.Shift && e.Key == Key.Insert))
            {
                e.Handled = true;
            }
        }

        private void NormalizeAllTextBoxValues()
        {
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                if (textBox == null) continue;
                string value = textBox.Text;
                if (!string.IsNullOrEmpty(value) && value != "0")
                {
                    string normalized = value.TrimStart('0');
                    textBox.Text = string.IsNullOrEmpty(normalized) ? "0" : normalized;
                }
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SetupTextBoxColors();
            PreventPasteForTextBox(NUMBERXTEXTBOX);
            PreventPasteForTextBox(NUMBERYTEXTBOX);
            PreventPasteForTextBox(NUMBERKAITEXTBOX);

            // Gọi cập nhật canvas ngay sau khi tải
            UpdateCanvas("X");
            UpdateCanvas("Y");
        }

        private void UpdateCanvas(string canvasType)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (canvasType == "X")
                    UpdateXCanvas();
                else if (canvasType == "Y")
                    UpdateYCanvas();
                SetupTextBoxColors();
            }), System.Windows.Threading.DispatcherPriority.Render);
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

        private void UpdateXCanvas()
        {
            if (_projectData.Kihon == null)
                return;

            var kihonData = _projectData.Kihon;
            if (kihonData.ListSpanX == null || kihonData.NameX == null)
                return;
            // Sử dụng Dispatcher để đảm bảo Canvas được vẽ sau khi UI cập nhật
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var canvases = FindVisualChildren<Canvas>(this).Where(c => c.Name == "XCanvas").ToList();
                for (int i = 0; i < canvases.Count; i++)
                {
                    var canvas = canvases[i];
                    canvas.Children.Clear();

                    var spanValue = i < kihonData.ListSpanX.Count && int.TryParse(kihonData.ListSpanX[i].Span, out int span) ? span : 0;
                    var nameCanvas1 = i < kihonData.NameX.Count ? kihonData.NameX[i].Name : "";
                    var nameCanvas2 = i + 1 < kihonData.NameX.Count ? kihonData.NameX[i + 1].Name : "";

                    DrawIllustration1(canvas, spanValue, nameCanvas1, nameCanvas2);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }

        private void UpdateYCanvas()
        {
            if (_projectData.Kihon == null)
                return;

            var kihonData = _projectData.Kihon;
            if (kihonData.ListSpanY == null || kihonData.NameY == null)
                return;
            // Sử dụng Dispatcher để đảm bảo Canvas được vẽ sau khi UI cập nhật
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var canvases = FindVisualChildren<Canvas>(this).Where(c => c.Name == "YCanvas").ToList();
                for (int i = 0; i < canvases.Count; i++)
                {
                    var canvas = canvases[i];
                    canvas.Children.Clear();

                    var spanValue = i < kihonData.ListSpanY.Count && int.TryParse(kihonData.ListSpanY[i].Span, out int span) ? span : 0;
                    var nameCanvas1 = i < kihonData.NameY.Count ? kihonData.NameY[i].Name : "";
                    var nameCanvas2 = i + 1 < kihonData.NameY.Count ? kihonData.NameY[i + 1].Name : "";

                    DrawIllustration1(canvas, spanValue, nameCanvas1, nameCanvas2);
                }
            }), System.Windows.Threading.DispatcherPriority.Render);
        }


        public void NumberTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (!int.TryParse(e.Text, out _))
                {
                    // Tô đỏ TextBox
                    textBox.Background = Brushes.LightCoral;
                    e.Handled = true;
                    return;
                }

                // Nếu ký tự hợp lệ, khôi phục màu nền mặc định
                textBox.Background = Brushes.White;

                string newText;

                // Kiểm tra nếu toàn bộ văn bản được chọn
                if (textBox.SelectionLength == textBox.Text.Length)
                {
                    // Thay thế toàn bộ nội dung bằng ký tự mới
                    newText = e.Text;
                }
                else
                {
                    // Chèn ký tự mới vào vị trí con trỏ
                    newText = textBox.Text.Insert(textBox.SelectionStart, e.Text);
                }
                // Nếu chuỗi mới có độ dài > 1 và bắt đầu bằng '0' thì không cho phép
                if (newText.Length > 1 && newText.StartsWith("0"))
                {
                    textBox.Background = Brushes.LightCoral;
                    e.Handled = true;
                    return;
                }
                // Loại bỏ các số 0 thừa ở đầu
                newText = newText.TrimStart('0');

                // Ngăn không cho nhập số 0 đầu
                if (newText.Length == 0 && e.Text == "0")
                {
                    e.Handled = true;
                    return;
                }
                // Giới hạn giá trị tối đa là 50
                if (int.TryParse(newText, out int newValue) && newValue > 50 || textBox.MaxLength == 2)
                {
                    e.Handled = true;
                    return;
                }

                e.Handled = false;
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
        private void OnTrackedKihonDataChanged()
        {
            // Kiểm tra HasChanged để cập nhật UI ngay khi giá trị thay đổi
            bool hasChanged = _trackedKihonData.HasChanged();


            if (hasChanged)
            {
                UpdateCanvas("X");
                UpdateCanvas("Y");
                _currentKihonData.SyncListSpanXNames();
                _currentKihonData.SyncListSpanYNames();

            }
        }
    }
}