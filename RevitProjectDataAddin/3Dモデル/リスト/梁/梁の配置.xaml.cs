using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Autodesk.Revit.DB;
using TextBox = System.Windows.Controls.TextBox;

namespace RevitProjectDataAddin
{
    public partial class 梁の配置 : Window
    {

        private readonly ProjectData _projectData;
        private readonly Document doc;
        public Z梁の配置 _currentHariData;
        public TrackedObject<Z梁の配置> _trackedHariData;
        public string beamType;
        public Dictionary<string, GridBotData> DictGBD;
        public GridBotData CurrentGBD;
        private bool isInitializing = true;
        private bool suppressNakagoSelectionChanged = false;

        public 梁の配置(Document document, Z梁の配置 haridata, ProjectData projectData)
        {
            InitializeComponent();
            doc = document;
            string selectedProjectName = ProjectManager.SelectedProjectName;
            _currentHariData = haridata ?? new Z梁の配置();
            beamType = _currentHariData.梁COMBOBOX;
            DataContext = _currentHariData;
            _projectData = projectData;

            DictGBD = _currentHariData.gridbotdata;

            // Ghi log truoc GridBotDataUpdater
            File.AppendAllText("debugdata.txt", Environment.NewLine + $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]------------------------------------Before GridBotDataUpdater----------------------------------" + Environment.NewLine);

            GridBotDataUpdater();

            // Ghi log sau GridBotDataUpdater
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]------------------------------------After GridBotDataUpdater----------------------------------" + Environment.NewLine);

            CurrentGBD = DictGBD[beamType];

            File.AppendAllText("debugdata.txt", Environment.NewLine + $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]------------------------------------Before TrackedObject call----------------------------------" + Environment.NewLine);
            _trackedHariData = new TrackedObject<Z梁の配置>(_currentHariData);
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]------------------------------------After TrackedObject call----------------------------------" + Environment.NewLine);

            Loaded += (s, e) =>
            {
                File.AppendAllText("debugdata.txt", Environment.NewLine + $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Debug] Loaded: CurrentGridBotData set, beamType: {beamType}, Valid中子筋の位置Values: {string.Join(",", CurrentGBD?.Valid中子筋の位置Values ?? new List<string>())}" + Environment.NewLine);
                GridBot.DataContext = CurrentGBD;
                UpdateComboBoxItemsSource();
                Update筋宙本数Columns();
                SetupTextBoxes();
                ValidateNakagoCount(beamType);
                DrawScaledRectangle();
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
                ValidateNakagoCount(beamType);
                if (isInitializing) return;
                // Cập nhật beamType từ 梁COMBOBOX
                beamType = _currentHariData.梁COMBOBOX;
                File.AppendAllText("debugdata.txt",
                Environment.NewLine + $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Debug] 梁.SelectionChanged: beamType updated to: {beamType}" + Environment.NewLine);

                if (DictGBD.ContainsKey(beamType))
                {
                    CurrentGBD = DictGBD[beamType];
                    GridBot.DataContext = CurrentGBD;


                    // Log CurrentGBD thông tin chi tiết
                    var valid数 = CurrentGBD?.Valid数Values ?? new List<string>();
                    var valid位置 = CurrentGBD?.Valid中子筋の位置Values ?? new List<string>();

                    var nakagoPosList = new List<string>();
                    foreach (var kvp in CurrentGBD?.NakagoCustomPositions ?? new Dictionary<int, int>())
                    {
                        string 数 = (kvp.Key >= 0 && kvp.Key < valid数.Count) ? valid数[kvp.Key] : $"invalid_index_{kvp.Key}";
                        string 位置 = (kvp.Value >= 0 && kvp.Value < valid位置.Count) ? valid位置[kvp.Value] : $"invalid_index_{kvp.Value}";
                        nakagoPosList.Add($"数={数}→位置={位置}");
                    }
                    string nakagoPosStr = string.Join(", ", nakagoPosList);
                    // Debug for NakagoDirections
                    var nakagoDirList = new List<string>();
                    foreach (var kvp in CurrentGBD?.NakagoDirections ?? new Dictionary<int, bool>())
                    {
                        string 数 = (kvp.Key >= 0 && kvp.Key < valid数.Count) ? valid数[kvp.Key] : $"invalid_index_{kvp.Key}";
                        string 向き = kvp.Value ? "true" : "false"; // hoặc có thể là "true"/"false" tùy theo ngữ cảnh
                        nakagoDirList.Add($"数={数}→向き={向き}");
                    }
                    string nakagoDirStr = string.Join(", ", nakagoDirList);
                    File.AppendAllText("debugdata.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Debug] 梁.SelectionChanged: CurrentGridBotData set to: {CurrentGBD.BeamType}" + Environment.NewLine +
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Debug] 中子筋の数COMBOBOX = {CurrentGBD?.中子筋の数COMBOBOX}, Valid数Values = [{string.Join(",", valid数)}]" + Environment.NewLine +
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Debug] 中子筋の位置COMBOBOX = {CurrentGBD?.中子筋の位置COMBOBOX}, Valid中子筋の位置Values = [{string.Join(",", valid位置)}]" + Environment.NewLine +
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Debug] NakagoCustomPositions = [{nakagoPosStr}]" + Environment.NewLine +
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Debug] NakagoDirections = [{nakagoDirStr}]" + Environment.NewLine
                    );


                    UpdateComboBoxItemsSource();
                    Update筋宙本数Columns();
                    DrawScaledRectangle();
                }
                else
                {
                    File.AppendAllText("debugdata.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Debug] 梁.SelectionChanged: beamType {beamType} not found in DictGBD" + Environment.NewLine);
                }
            };


            MyCanvas.SizeChanged += (s, e) =>
            {
                if (isInitializing) return;
                File.AppendAllText("debugdata.txt",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [MyCanvas.SizeChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
                DrawScaledRectangle();
            };

            全断面.Checked += CheckBox_Checked;

            全断面.Unchecked += (s, e) =>
            {
                if (isInitializing) return;
                File.AppendAllText("debugdata.txt",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [全断面.Unchecked] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
                CheckBox_Unchecked(s, e);
            };

            端部1の上宙2TEXTBOX.TextChanged += (s, e) =>
            {
                if (isInitializing) return;
                File.AppendAllText("debugdata.txt",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [端部1の上宙2TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
                Update筋宙本数Columns();
            };

            端部1の下宙2TEXTBOX.TextChanged += (s, e) =>
            {
                if (isInitializing) return;
                File.AppendAllText("debugdata.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [端部1の下宙2TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
                Update筋宙本数Columns();
            };
            中央の上宙2TEXTBOX.TextChanged += (s, e) =>
            {
                if (isInitializing) return;
                File.AppendAllText("debugdata.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [中央の上宙2TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
                Update筋宙本数Columns();
            };

            中央の下宙2TEXTBOX.TextChanged += (s, e) =>
            {
                if (isInitializing) return;
                File.AppendAllText("debugdata.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [中央の下宙2TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
                Update筋宙本数Columns();
            };
            端部2の上宙2TEXTBOX.TextChanged += (s, e) =>
            {
                if (isInitializing) return;
                File.AppendAllText("debugdata.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [端部2の上宙2TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
                Update筋宙本数Columns();
            };

            端部2の下宙2TEXTBOX.TextChanged += (s, e) =>
            {
                if (isInitializing) return;
                File.AppendAllText("debugdata.txt",
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [端部2の下宙2TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
                Update筋宙本数Columns();
            };

        }

        private void Close(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Log trạng thái của DictGBD trước khi kiểm tra hợp lệ
            var data = DictGBD[beamType];
            //File.AppendAllText("Debug_Log.txt",
            //    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Close: NakagoCustomPositions[{data.Valid数Values.IndexOf(data.中子筋の数COMBOBOX)}] = {(data.NakagoCustomPositions.ContainsKey(data.Valid数Values.IndexOf(data.中子筋の数COMBOBOX)) ? data.NakagoCustomPositions[data.Valid数Values.IndexOf(data.中子筋の数COMBOBOX)].ToString() : "null")}, 中子筋の位置COMBOBOX.SelectedItem = {中子筋の位置COMBOBOX.SelectedItem?.ToString() ?? "null"}\n");

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

            // Log trạng thái của _current trước khi gọi HasChanged
            //File.AppendAllText("Debug_Log.txt",
            //    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Before HasChanged: _current = {JsonSerializer.Serialize(_trackedHariData.Current, JsonHelper.Options)}\n");

            if (_trackedHariData.HasChanged())
            {
                // Log trạng thái của _current và _original khi phát hiện thay đổi
                //File.AppendAllText("Debug_Log.txt",
                //    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] HasChanged returned true: _current = {JsonSerializer.Serialize(_trackedHariData.Current, JsonHelper.Options)}, _original = {JsonSerializer.Serialize(_trackedHariData.Value, JsonHelper.Options)}\n");

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

                    // Log sau khi lưu
                    //File.AppendAllText("Debug_Log.txt",
                    //    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Saved: _current = {JsonSerializer.Serialize(_trackedHariData.Current, JsonHelper.Options)}\n");
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    e.Cancel = true;

                    // Log khi chọn Cancel
                    //File.AppendAllText("Debug_Log.txt",
                    //    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Cancel selected\n");
                }
                else if (result == MessageBoxResult.No)
                {
                    // Log trước khi gọi RestoreOriginal
                    //File.AppendAllText("Debug_Log.txt",
                    //    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Before RestoreOriginal: _current = {JsonSerializer.Serialize(_trackedHariData.Current, JsonHelper.Options)}\n");

                    // Xóa dữ liệu tạm thời
                    _trackedHariData.RestoreOriginal();

                    // Log sau khi gọi RestoreOriginal
                    //File.AppendAllText("Debug_Log.txt",
                    //    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] After RestoreOriginal: _current = {JsonSerializer.Serialize(_trackedHariData.Current, JsonHelper.Options)}\n");
                }
            }
            else
            {
                // Log khi không có thay đổi
                //File.AppendAllText("Debug_Log.txt",
                //    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] HasChanged returned false: _current = {JsonSerializer.Serialize(_trackedHariData.Current, JsonHelper.Options)}\n");
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
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [SetupTextBoxes] Start: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            // Logic hiện tại của SetupTextBoxes
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                SetupTextBoxKha(textBox);
            }
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [SetupTextBoxes] End: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
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
        private bool IsAllowNegativeTextBox(TextBox textBox)
        {
            var names = new[]
            {
              "下筋上下TEXTBOX", "下筋左右TEXTBOX", "下宙1上下TEXTBOX", "下宙1左右TEXTBOX", "上宙1上下TEXTBOX", "上宙1左右TEXTBOX",
                "上筋上下TEXTBOX", "上筋左右TEXTBOX", "上宙2上下TEXTBOX", "上宙2左右TEXTBOX","下宙2上下TEXTBOX", "下宙2左右TEXTBOX",
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
                bool valid = nakagoVal <= upperVal - 2;
                nakago.Background = valid ? Brushes.White : Brushes.LightCoral;
                return valid;
            }

            isValid &= ValidatePair(端部1の中子筋本数TEXTBOX, 端部1の上筋本数TEXTBOX);
            isValid &= ValidatePair(中央の中子筋本数TEXTBOX, 中央の上筋本数TEXTBOX);
            isValid &= ValidatePair(端部2の中子筋本数TEXTBOX, 端部2の上筋本数TEXTBOX);

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


        private void 更新ボタン_Click(object sender, RoutedEventArgs e)
        {
            ResetGridBotControls();
        }

    }
}
