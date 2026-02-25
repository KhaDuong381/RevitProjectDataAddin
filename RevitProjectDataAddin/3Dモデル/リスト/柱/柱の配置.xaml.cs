using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;

namespace RevitProjectDataAddin
{
    public partial class 柱の配置 : Window
    {

        private readonly ProjectData _projectData;
        private readonly Document doc;
        private Z柱の配置 _currentHashiraData;
        public TrackedObject<Z柱の配置> _trackedHashiraData;
        public string beamType;
        public Dictionary<string, GridBotDataHashira> DictGBD;
        public GridBotDataHashira CurrentGBD;
        private bool isInitializing = true;
        private bool suppressNakagoSelectionChanged = false;
        public 柱の配置(Document document, Z柱の配置 hashiradata, ProjectData projectData)
        {
            InitializeComponent();
            doc = document;
            string selectedProjectName = ProjectManager.SelectedProjectName;
            _currentHashiraData = hashiradata ?? new Z柱の配置();
            beamType = _currentHashiraData.梁COMBOBOX;
            DataContext = _currentHashiraData;
            _projectData = projectData;

            DictGBD = _currentHashiraData.gridbotdata;

            GridBotDataUpdater();

            CurrentGBD = DictGBD[beamType];

            _trackedHashiraData = new TrackedObject<Z柱の配置>(_currentHashiraData);


            Loaded += (s, e) =>
            {
                GridBot.DataContext = CurrentGBD;
                UpdateSinkinVisibility();
                UpdateComboBoxItemsSource();  // <-- GỌI TRƯỚC: để đảm bảo các Getter đã được thiết lập

                SetupTextBoxes();             // <-- GỌI SAU: lúc này gọi ValidXXXValues sẽ đúng
                DrawRebarLayout();
                isInitializing = false;
            };

            this.Closing += Close;

            梁.SelectionChanged += (s, e) =>
            {
                // Xác thực số lượng 中子 trước khi tiếp tục
                if (!Validate中子筋本数())
                {
                    MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (isInitializing) return;

                beamType = _currentHashiraData.梁COMBOBOX;

                if (DictGBD.ContainsKey(beamType))
                {
                    CurrentGBD = DictGBD[beamType];
                    GridBot.DataContext = CurrentGBD;
                    UpdateComboBoxItemsSource();
                    UpdateSinkinVisibility();

                    DrawRebarLayout();
                }
               
            };
            MyCanvas.SizeChanged += (s, e) => DrawRebarLayout();

            柱全断面.Checked += CheckBox_Checked;
            柱全断面.Unchecked += CheckBox_Unchecked;
        }
        private void Close(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Chỉ kiểm tra và hỏi lưu khi có thay đổi

            // Kiểm tra hợp lệ dữ liệu
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                string value = textBox.Text?.Trim();

                // Cho phép rỗng, bỏ qua kiểm tra
                if (string.IsNullOrEmpty(value))
                    continue;
                // Kiểm tra không cho phép dấu '-' và '.' nằm gần nhau
                if (value.Contains("-.") || value.Contains(".-"))
                {
                    MessageBox.Show("「-」と「.」は隣り合って入力できません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Cancel = true;
                    textBox.Focus();
                    textBox.Background = Brushes.LightCoral;
                    return;
                }
                // Loại bỏ dấu '-' đầu để kiểm tra độ dài
                string valueForLength = value.StartsWith("-") ? value.Substring(1) : value;
                // Nếu là số thập phân (có dấu .) thì giới hạn 10 ký tự, ngược lại 5 ký tự
                int maxLength = valueForLength.Contains(".") ? 10 : 5;
                if (valueForLength.Length > maxLength)
                {
                    MessageBox.Show(
                        valueForLength.Contains(".")
                            ? "入力値が大きすぎます。（小数点を含め最大10文字までです）"
                            : "入力値が大きすぎます。（最大5文字までです）",
                        "エラー",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    e.Cancel = true;
                    textBox.Focus();
                    textBox.Background = Brushes.LightCoral;
                    return;
                }

                // Kiểm tra hợp lệ số thực (double), chỉ cho phép 1 dấu chấm
                if (value.Count(c => c == '.') > 1 || value.StartsWith(".") || !double.TryParse(value, out double dValue))
                {
                    MessageBox.Show("有効な小数値のみ入力できます。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Cancel = true;
                    textBox.Focus();
                    textBox.Background = Brushes.LightCoral;
                    return;
                }

                // Kiểm tra giá trị tối đa
                if (dValue > 99999 || dValue < -99999)
                {
                    MessageBox.Show("入力値が99999を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    e.Cancel = true;
                    textBox.Focus();
                    textBox.Background = Brushes.LightCoral;
                    return;
                }
            }
            if (_trackedHashiraData.HasChanged())
            {
                // Nếu hợp lệ, hỏi xác nhận lưu
                var result = MessageBox.Show(
                    "データが変更されています。保存しますか？",
                    "確認",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    NormalizeAllTextBoxValues();
                    // Lưu dữ liệu sau khi chỉnh sửa
                    StorageUtils.SaveProject(doc, _projectData);

                    DialogResult = true;
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;
                }
                else if (result == MessageBoxResult.No)
                {
                    // Xóa dữ liệu tạm thời
                    _trackedHashiraData.RestoreOriginal();
                }
            }
            // Nếu không có thay đổi thì không làm gì cả (không hỏi lưu, không kiểm tra hợp lệ)
        }

        private void NormalizeAllTextBoxValues()
        {
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                if (textBox == null) continue;
                string value = textBox.Text;
                if (!string.IsNullOrEmpty(value) && value != "0")
                {
                    // Nếu là số thập phân (có dấu .) thì giữ nguyên, chỉ lược bỏ số 0 đầu nếu là số nguyên
                    if (value.Contains("."))
                    {
                        // Không lược bỏ số 0 đầu
                        textBox.Text = value;
                    }
                    else
                    {
                        string normalized = value.TrimStart('0');
                        textBox.Text = string.IsNullOrEmpty(normalized) ? "0" : normalized;
                    }
                }
            }
        }
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

        private void SetupTextBoxes()
        {
            // Duyệt qua tất cả các TextBox trong giao diện
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                SetupTextBoxKha(textBox);
            }
        }
        public void SetupTextBoxKha(TextBox textBox)
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
        private bool IsAllowNegativeTextBox(TextBox textBox)
        {
            var names = new[]
            {
                "左右", "上下", "左右1", "上下1", "左右2", "上下2", "左右3", "上下3",
                "左右4", "上下4", "左右5", "上下5", "左右6", "上下6", "左右7", "上下7",

            };
            return names.Contains(textBox.Name);
        }
        private bool Validate中子筋本数()
        {
            bool isValid = true;

            bool ValidatePair(TextBox nakago, TextBox upper)
            {
                int nakagoVal = int.TryParse(nakago.Text, out int n) ? n : 0;
                int upperVal = int.TryParse(upper.Text, out int u) ? u : 0;
                bool valid = nakagoVal <= upperVal;
                nakago.Background = valid ? Brushes.White : Brushes.LightCoral;
                return valid;
            }

            isValid &= ValidatePair(縦向き中子本数, 上側主筋本数);
            isValid &= ValidatePair(横向き中子本数, 左側主筋本数);

            isValid &= ValidatePair(縦向き中子本数1, 上側主筋本数1);
            isValid &= ValidatePair(横向き中子本数1, 左側主筋本数1);

            isValid &= ValidatePair(仕口部_縦向き中子本数, 上側主筋本数);
            isValid &= ValidatePair(仕口部_横向き中子本数, 左側主筋本数);

            return isValid;
        }

        private void TEXTBOX_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
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

    }
}
