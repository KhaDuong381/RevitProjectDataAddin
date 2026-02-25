using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RevitProjectDataAddin
{
    //public partial class 基礎の配置 : BaseConfigurationWindow<Z基礎の配置>
    //{
    //    public List<string> Valid筋形Values => ValidationLists.筋形Values;
    //    public List<string> Valid材質Values => ValidationLists.材質Values;
    //    public 基礎の配置(Document document, Z基礎の配置 data) : base(document, data)
    //    {
    //        InitializeComponent();
    //    }

    //    private void AddButton_Click(object sender, RoutedEventArgs e)
    //    {
    //        // Triển khai logic cho nút Add nếu cần
    //    }

    //}
    public partial class 基礎の配置 : Window
    {
        private readonly Document doc;
        private readonly Z基礎の配置 _基礎の配置;
        private Z基礎の配置 _originalData;

        public List<string> Valid筋形Values => new List<string> { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "11", "12", "13", "14" };
        public List<string> Valid材質Values => new List<string> { "SD295", "SD345", "SD390", "SD490", "785級" };

        public 基礎の配置(Document document, Z基礎の配置 data)
        {
            InitializeComponent();
            _基礎の配置 = data ?? new Z基礎の配置();
            this.doc = document;
            DataContext = _基礎の配置; // Gán đối tượng dữ liệu cho DataContext
            SaveOriginalData();
            Loaded += (s, e) => SetupTextBoxes();
            this.Closing += Close;
        }
        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void Close(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                // Kiểm tra nếu textBox có giá trị lớn hơn 5 chữ số  
                string value = textBox.Text?.Trim();
                if (!string.IsNullOrEmpty(value) && value.Length > 5 && value.All(char.IsDigit) || !value.All(char.IsDigit))
                {
                    MessageBox.Show("Giá trị quá lớn!! (Chỉ cho phép tối đa 5 chữ số)", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Cancel = true;
                    textBox.Focus();
                    textBox.Background = Brushes.LightCoral;
                    return;
                    
                }
                
            }

            if (HasChanges())
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
                    DialogResult = true;
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
                else if (result == MessageBoxResult.No)
                {
                    // Xóa dữ liệu tạm  
                    ClearTemporaryData();
                }
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

        /// Lưu lại trạng thái ban đầu.
        private void SaveOriginalData()
        {
            _originalData = CloneZ基礎の配置(_基礎の配置);
        }

        /// So sánh dữ liệu hiện tại với dữ liệu gốc để xác định có thay đổi không.
        private bool HasChanges()
        {
            if (_originalData == null) SaveOriginalData();
            return !AreZ基礎の配置Equal(_originalData, _基礎の配置);
        }

        /// Phục hồi lại dữ liệu ban đầu.
        private void ClearTemporaryData()
        {
            if (_originalData == null) return;
            var type = typeof(Z基礎の配置);
            foreach (var prop in type.GetProperties())
            {
                if (prop.CanWrite)
                {
                    var value = prop.GetValue(_originalData);
                    prop.SetValue(_基礎の配置, value);
                }
            }
        }

        /// So sánh hai đối tượng Z基礎の配置.
        private bool AreZ基礎の配置Equal(Z基礎の配置 a, Z基礎の配置 b)
        {
            if (a == null || b == null) return false;
            var type = typeof(Z基礎の配置);
            foreach (var prop in type.GetProperties())
            {
                if (!Equals(prop.GetValue(a), prop.GetValue(b)))
                    return false;
            }
            return true;
        }

        /// Tạo bản sao sâu cho Z基礎の配置.
        private Z基礎の配置 CloneZ基礎の配置(Z基礎の配置 src)
        {
            if (src == null) return null;
            var clone = new Z基礎の配置();
            var type = typeof(Z基礎の配置);
            foreach (var prop in type.GetProperties())
            {
                if (prop.CanWrite)
                    prop.SetValue(clone, prop.GetValue(src));
            }
            return clone;
        }

        private void SetupTextBoxes()
        {
            // Duyệt qua tất cả các TextBox trong giao diện
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                SetupTextBoxKha(textBox);
            }
        }

        // Hàm đệ quy để tìm tất cả các TextBox trong cây giao diện
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj == null) yield break;

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
            {
                var child = VisualTreeHelper.GetChild(depObj, i);
                if (child is T t)
                {
                    yield return t;
                }

                foreach (var childOfChild in FindVisualChildren<T>(child))
                {
                    yield return childOfChild;
                }
            }
        }

        private void SetupTextBoxKha(TextBox textBox)
        {
            void UpdateColors(Brush background) => textBox.Background = background;
            void SetActiveColors() => UpdateColors(Brushes.LightGreen);
            void SetInactiveColors() => UpdateColors(Brushes.White);

            // Thêm các sự kiện khác
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
            textBox.LostFocus += (s, e) => SetInactiveColors();
            textBox.MouseEnter += (s, e) => SetActiveColors();
            textBox.MouseLeave += (s, e) => { if (!textBox.IsFocused) SetInactiveColors(); };
        }

        private void TEXTBOX_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
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

    }
}