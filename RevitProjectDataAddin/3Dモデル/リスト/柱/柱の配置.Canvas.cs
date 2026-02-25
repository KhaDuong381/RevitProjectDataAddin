using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ComboBox = System.Windows.Controls.ComboBox;
using Path = System.Windows.Shapes.Path;
using TextBox = System.Windows.Controls.TextBox;

namespace RevitProjectDataAddin
{
    public partial class 柱の配置 : Window
    {
        public List<string> Valid径Values => new List<string> { "10", "13", "16", "19", "22", "25", "29", "32", "35", "38" };
        public List<string> Valid中子筋形Values => new List<string> { "1", "2", "3", "4", "5" };
        public List<string> ValidHOOP筋形Values => new List<string> { "1", "2", "3" };
        public List<string> Valid材質Values => new List<string> { "SD295", "SD345", "SD390", "SD490", "785級" };
        public List<string> Valid筋Values => new List<string> { "1", "2", "3" };
        public List<string> Valid宙Values => new List<string> { "1", "2", };
        public List<string> Valid梁Values => new List<string> { "柱脚", "柱頭", "仕口部", };
        public List<string> Valid位置Values => new List<string> { "1", "2", "3", "4" };

        string GetField(string fieldBase, string beamType)
        {
            switch (beamType)
            {
                case "柱脚":
                    return (string)_currentHashiraData.GetType().GetProperty($"柱脚{fieldBase}")?.GetValue(_currentHashiraData) ?? "0";
                case "柱頭":
                    return (string)_currentHashiraData.GetType().GetProperty($"柱頭{fieldBase}")?.GetValue(_currentHashiraData) ?? "0";
                case "仕口部":
                    return (string)_currentHashiraData.GetType().GetProperty($"仕口部{fieldBase}")?.GetValue(_currentHashiraData) ?? "0";
                default:
                    return "0";
            }
        }
        string GetField1(string fieldBase, string beamType)
        {
            switch (beamType)
            {
                case "柱脚":
                    return (string)_currentHashiraData.GetType().GetProperty($"柱脚{fieldBase}")?.GetValue(_currentHashiraData) ?? "0";
                case "柱頭":
                    return (string)_currentHashiraData.GetType().GetProperty($"柱頭{fieldBase}")?.GetValue(_currentHashiraData) ?? "0";
                case "仕口部":
                    return (string)_currentHashiraData.GetType().GetProperty($"柱脚{fieldBase}")?.GetValue(_currentHashiraData) ?? "0";
                default:
                    return "0";
            }
        }
        public void GridBotDataUpdater()
        {
            foreach (var beamTypes in new[] { "柱脚", "柱頭", "仕口部" })
            {
                // Gán setter và khởi tạo Valid*Values
                SetGetters(beamTypes);
            }
        }
        public void SetGetters(string beamType)
        {
            if (!DictGBD.ContainsKey(beamType))
            {
                DictGBD[beamType] = new GridBotDataHashira(beamType);
            }

            var data = DictGBD[beamType];

            // Gán getter tương ứng với beamType
            data.SetGetter("上側主筋本数", () => GetField1("上側主筋本数", beamType));
            data.SetGetter("下側主筋本数", () => GetField1("下側主筋本数", beamType));
            data.SetGetter("左側主筋本数", () => GetField1("左側主筋本数", beamType));
            data.SetGetter("右側主筋本数", () => GetField1("右側主筋本数", beamType));
            data.SetGetter("上側芯筋本数", () => GetField1("上側芯筋本数", beamType));
            data.SetGetter("下側芯筋本数", () => GetField1("下側芯筋本数", beamType));
            data.SetGetter("左側芯筋本数", () => GetField1("左側芯筋本数", beamType));
            data.SetGetter("右側芯筋本数", () => GetField1("右側芯筋本数", beamType));

            data.SetGetter("縦向き中子本数", () => GetField("縦向き中子本数", beamType));
            data.SetGetter("横向き中子本数", () => GetField("横向き中子本数", beamType));
        }

        private void UpdateComboBoxItemsSource()
        {
            上側主筋本数2.ItemsSource = CurrentGBD.Valid上側主筋本数Values;
            下側主筋本数2.ItemsSource = CurrentGBD.Valid下側主筋本数Values;
            左側主筋本数2.ItemsSource = CurrentGBD.Valid左側主筋本数Values;
            右側主筋本数2.ItemsSource = CurrentGBD.Valid右側主筋本数Values;
            上側芯筋本数2.ItemsSource = CurrentGBD.Valid上側芯筋本数Values;
            下側芯筋本数2.ItemsSource = CurrentGBD.Valid下側芯筋本数Values;
            左側芯筋本数2.ItemsSource = CurrentGBD.Valid左側芯筋本数Values;
            右側芯筋本数2.ItemsSource = CurrentGBD.Valid右側芯筋本数Values;

            縦向き中子の位置.ItemsSource = CurrentGBD.Valid縦向き中子の位置Values;
            横向き中子の位置.ItemsSource = CurrentGBD.Valid横向き中子の位置Values;

            縦向き中子の数.ItemsSource = CurrentGBD.Valid縦向き中子本数Values;
            横向き中子の数.ItemsSource = CurrentGBD.Valid横向き中子本数Values;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

            // Sao chép giá trị từ Grid.Column="0" sang Grid.Column="1"
            柱幅1.Text = 柱幅.Text;
            柱成1.Text = 柱成.Text;
            主筋径1.SelectedIndex = 主筋径.SelectedIndex;
            主筋材質1.SelectedIndex = 材質.SelectedIndex;
            芯筋径1.SelectedIndex = 芯筋径.SelectedIndex;
            芯筋材質1.SelectedIndex = 材質0.SelectedIndex;
            上側主筋本数1.Text = 上側主筋本数.Text;
            下側主筋本数1.Text = 下側主筋本数.Text;
            左側主筋本数1.Text = 左側主筋本数.Text;
            右側主筋本数1.Text = 右側主筋本数.Text;

            上側芯筋本数1.Text = 上側芯筋本数.Text;
            下側芯筋本数1.Text = 下側芯筋本数.Text;
            左側芯筋本数1.Text = 左側芯筋本数.Text;
            右側芯筋本数1.Text = 右側芯筋本数.Text;

            HOOP径1.SelectedIndex = HOOP径.SelectedIndex;
            HOOP形1.SelectedIndex = HOOP形.SelectedIndex;
            HOOP材質1.SelectedIndex = HOOP材質.SelectedIndex;
            ピッチ1.Text = ピッチ.Text;
            縦向き中子径1.SelectedIndex = 縦向き中子径.SelectedIndex;
            縦向き中子形1.SelectedIndex = 縦向き中子形.SelectedIndex;
            縦向き中子材質1.SelectedIndex = 縦向き中子材質.SelectedIndex;
            縦向き中子ピッチ1.Text = 縦向き中子ピッチ.Text;
            縦向き中子本数1.Text = 縦向き中子本数.Text;
            横向き中子径1.SelectedIndex = 横向き中子径.SelectedIndex;
            横向き中子形1.SelectedIndex = 横向き中子形.SelectedIndex;
            横向き中子材質1.SelectedIndex = 横向き中子材質.SelectedIndex;
            横向き中子ピッチ1.Text = 横向き中子ピッチ.Text;
            横向き中子本数1.Text = 横向き中子本数.Text;


            // Vô hiệu hóa TextBox ở Grid.Column="1"
            // Vô hiệu hóa các điều khiển ở Grid.Column="1"
            Control[] controls1 = { 柱幅1, 柱成1, 主筋径1, 主筋材質1, 芯筋径1, 芯筋材質1, 上側主筋本数1, 下側主筋本数1, 左側主筋本数1, 右側主筋本数1,
                                        上側芯筋本数1, 下側芯筋本数1, 左側芯筋本数1, 右側芯筋本数1, HOOP径1, HOOP形1, HOOP材質1, ピッチ1,
                                        縦向き中子径1, 縦向き中子形1, 縦向き中子材質1, 縦向き中子ピッチ1, 縦向き中子本数1, 横向き中子径1, 横向き中子形1, 横向き中子材質1, 横向き中子ピッチ1, 横向き中子本数1 };
            foreach (var control in controls1)
                control.IsEnabled = false;
            UpdateComboBoxItemsSource();
            //DrawRebarLayout();

        }
        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {

            // Kích hoạt lại TextBox ở Grid.Column="1"
            Control[] controls1 = { 柱幅1, 柱成1, 主筋径1, 主筋材質1, 芯筋径1, 芯筋材質1, 上側主筋本数1, 下側主筋本数1, 左側主筋本数1, 右側主筋本数1,
                                        上側芯筋本数1, 下側芯筋本数1, 左側芯筋本数1, 右側芯筋本数1, HOOP径1, HOOP形1, HOOP材質1, ピッチ1,
                                        縦向き中子径1, 縦向き中子形1, 縦向き中子材質1, 縦向き中子ピッチ1, 縦向き中子本数1, 横向き中子径1, 横向き中子形1, 横向き中子材質1, 横向き中子ピッチ1, 横向き中子本数1 };
            foreach (var control in controls1)
                control.IsEnabled = true;
            UpdateComboBoxItemsSource();
        }
        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            // Binding đã lo việc cập nhật ViewModel rồi (nếu dùng INotifyPropertyChanged)

            DrawRebarLayout();
        }
        private void CopyGridColumn0To1()
        {
            柱幅1.Text = 柱幅.Text;
            柱成1.Text = 柱成.Text;
            主筋径1.SelectedIndex = 主筋径.SelectedIndex;
            主筋材質1.SelectedIndex = 材質.SelectedIndex;
            芯筋径1.SelectedIndex = 芯筋径.SelectedIndex;
            芯筋材質1.SelectedIndex = 材質0.SelectedIndex;
            上側主筋本数1.Text = 上側主筋本数.Text;
            下側主筋本数1.Text = 下側主筋本数.Text;
            左側主筋本数1.Text = 左側主筋本数.Text;
            右側主筋本数1.Text = 右側主筋本数.Text;
            上側芯筋本数1.Text = 上側芯筋本数.Text;
            下側芯筋本数1.Text = 下側芯筋本数.Text;
            左側芯筋本数1.Text = 左側芯筋本数.Text;
            右側芯筋本数1.Text = 右側芯筋本数.Text;
            HOOP径1.SelectedIndex = HOOP径.SelectedIndex;
            HOOP形1.SelectedIndex = HOOP形.SelectedIndex;
            HOOP材質1.SelectedIndex = HOOP材質.SelectedIndex;
            ピッチ1.Text = ピッチ.Text;
            縦向き中子径1.SelectedIndex = 縦向き中子径.SelectedIndex;
            縦向き中子形1.SelectedIndex = 縦向き中子形.SelectedIndex;
            縦向き中子材質1.SelectedIndex = 縦向き中子材質.SelectedIndex;
            縦向き中子ピッチ1.Text = 縦向き中子ピッチ.Text;
            縦向き中子本数1.Text = 縦向き中子本数.Text;
            横向き中子径1.SelectedIndex = 横向き中子径.SelectedIndex;
            横向き中子形1.SelectedIndex = 横向き中子形.SelectedIndex;
            横向き中子材質1.SelectedIndex = 横向き中子材質.SelectedIndex;
            横向き中子ピッチ1.Text = 横向き中子ピッチ.Text;
            横向き中子本数1.Text = 横向き中子本数.Text;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Xác thực số lượng 中子 trước khi tiếp tục
            if (!Validate中子筋本数())
            {
                MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Copy data if 柱全断面 is checked
            if (柱全断面.IsChecked == true)
            {
                CopyGridColumn0To1();
            }
            GridBotDataUpdater();
            DictGBD[beamType] = new GridBotDataHashira(beamType);
            SetGetters(beamType);

            CurrentGBD = DictGBD[beamType];
            GridBot.DataContext = CurrentGBD;

            UpdateComboBoxItemsSource();
            UpdateSinkinVisibility();
            DrawRebarLayout();
            UpdateGridBotControlsEnabled();
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var data = DictGBD[beamType];
            // Reset toàn bộ offset trái-phải
            foreach (var key in data.左右Offsets.Keys.ToList())
            {
                data.左右Offsets[key] = "0";
            }
            foreach (var key in data.上下Offsets.Keys.ToList())
            {
                data.上下Offsets[key] = "0";
            }
            // Reset giao diện TextBox hiển thị theo index hiện tại
            data.左右 = "0";
            data.上下 = "0";
            DrawRebarLayout();
            UpdateSinkinVisibility();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var data = DictGBD[beamType];
            // Reset toàn bộ offset trên-dưới
            foreach (var key in data.上下1Offsets.Keys.ToList())
            {
                data.上下1Offsets[key] = "0";
            }
            foreach (var key in data.左右1Offsets.Keys.ToList())
            {
                data.左右1Offsets[key] = "0";
            }
            data.上下1 = "0";
            data.左右1 = "0";
            DrawRebarLayout();
            UpdateSinkinVisibility();

        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            var data = DictGBD[beamType];
            // Reset toàn bộ offset trái-phải cho 左側芯筋
            foreach (var key in data.左右2Offsets.Keys.ToList())
            {
                data.左右2Offsets[key] = "0";
            }
            foreach (var key in data.上下2Offsets.Keys.ToList())
            {
                data.上下2Offsets[key] = "0";
            }
            data.左右2 = "0";
            data.上下2 = "0";
            DrawRebarLayout();
            UpdateSinkinVisibility();

        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            var data = DictGBD[beamType];
            // Reset toàn bộ offset trên-dưới cho 右側芯筋
            foreach (var key in data.上下3Offsets.Keys.ToList())
            {
                data.上下3Offsets[key] = "0";
            }
            foreach (var key in data.左右3Offsets.Keys.ToList())
            {
                data.左右3Offsets[key] = "0";
            }
            data.上下3 = "0";
            data.左右3 = "0";
            DrawRebarLayout();
            UpdateSinkinVisibility();

        }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        ////////////////2025 06 06////////////////////////     

        // Vô hiệu hóa các thành phần liên quan trong GridBot khi dữ liệu bằng 0
        private void UpdateGridBotControlsEnabled()
        {
            // 上側主筋
            TextBox topCountBox = beamType == "柱頭" ? 上側主筋本数1 : 上側主筋本数;
            bool enabledTop = !string.IsNullOrEmpty(topCountBox.Text) && topCountBox.Text != "0";
            Control[] controlsTop = { Reset上側主筋本数, 上側主筋本数2, 左右, 上下 };
            foreach (var control in controlsTop)
                control.IsEnabled = enabledTop;

            // 下側主筋
            TextBox bottomCountBox = beamType == "柱頭" ? 下側主筋本数1 : 下側主筋本数;
            bool enabledBottom = !string.IsNullOrEmpty(bottomCountBox.Text) && bottomCountBox.Text != "0";
            Control[] controlsBottom = { Reset下側主筋本数, 下側主筋本数2, 左右1, 上下1 };
            foreach (var control in controlsBottom)
                control.IsEnabled = enabledBottom;

            // 左側主筋
            TextBox leftCountBox = beamType == "柱頭" ? 左側主筋本数1 : 左側主筋本数;
            bool enabledLeft = !string.IsNullOrEmpty(leftCountBox.Text) && leftCountBox.Text != "0";
            Control[] controlsLeft = { Reset左側主筋本数, 左側主筋本数2, 左右2, 上下2 };
            foreach (var control in controlsLeft)
                control.IsEnabled = enabledLeft;

            // 右側主筋
            TextBox rightCountBox = beamType == "柱頭" ? 右側主筋本数1 : 右側主筋本数;
            bool enabledRight = !string.IsNullOrEmpty(rightCountBox.Text) && rightCountBox.Text != "0";
            Control[] controlsRight = { Reset右側主筋本数, 右側主筋本数2, 左右3, 上下3 };
            foreach (var control in controlsRight)
                control.IsEnabled = enabledRight;
        }
        // Thêm hàm mới để cập nhật Visibility của các StackPanel trong GirdSinkin
        private void UpdateSinkinVisibility()
        {
            var data = DictGBD[beamType];

            // Lấy giá trị từ TextBox tương ứng dựa trên beamType
            string topCount = beamType == "柱頭" ? 上側芯筋本数1.Text : 上側芯筋本数.Text;
            string bottomCount = beamType == "柱頭" ? 下側芯筋本数1.Text : 下側芯筋本数.Text;
            string leftCount = beamType == "柱頭" ? 左側芯筋本数1.Text : 左側芯筋本数.Text;
            string rightCount = beamType == "柱頭" ? 右側芯筋本数1.Text : 右側芯筋本数.Text;

            // Cập nhật Visibility của các StackPanel trong GirdSinkin
            bool isTopVisible = int.TryParse(topCount, out int top) && top > 0;
            上側芯筋Panel.Visibility = isTopVisible ? Visibility.Visible : Visibility.Collapsed;
            上側芯筋左右Panel.Visibility = isTopVisible ? Visibility.Visible : Visibility.Collapsed;
            上側芯筋上下Panel.Visibility = isTopVisible ? Visibility.Visible : Visibility.Collapsed;

            bool isBottomVisible = int.TryParse(bottomCount, out int bottom) && bottom > 0;
            下側芯筋Panel.Visibility = isBottomVisible ? Visibility.Visible : Visibility.Collapsed;
            下側芯筋左右Panel.Visibility = isBottomVisible ? Visibility.Visible : Visibility.Collapsed;
            下側芯筋上下Panel.Visibility = isBottomVisible ? Visibility.Visible : Visibility.Collapsed;

            bool isLeftVisible = int.TryParse(leftCount, out int left) && left > 0;
            左側芯筋Panel.Visibility = isLeftVisible ? Visibility.Visible : Visibility.Collapsed;
            左側芯筋左右Panel.Visibility = isLeftVisible ? Visibility.Visible : Visibility.Collapsed;
            左側芯筋上下Panel.Visibility = isLeftVisible ? Visibility.Visible : Visibility.Collapsed;

            bool isRightVisible = int.TryParse(rightCount, out int right) && right > 0;
            右側芯筋Panel.Visibility = isRightVisible ? Visibility.Visible : Visibility.Collapsed;
            右側芯筋左右Panel.Visibility = isRightVisible ? Visibility.Visible : Visibility.Collapsed;
            右側芯筋上下Panel.Visibility = isRightVisible ? Visibility.Visible : Visibility.Collapsed;

            // Đếm số hàng hiển thị trong GirdSinkin (mỗi nhóm芯筋 là một hàng)
            int visibleRows = (isTopVisible ? 1 : 0) +
                                (isBottomVisible ? 1 : 0) +
                                (isLeftVisible ? 1 : 0) +
                                (isRightVisible ? 1 : 0);

            // Đếm số cột hiển thị và tính ColumnSpan, chiều rộng
            int visibleColumns = visibleRows;
            int[] columnSpans = new int[visibleColumns];
            double[] elementWidths = new double[visibleColumns];
            double[] elementWidths1 = new double[visibleColumns];

            // Định nghĩa các margin cho StackPanel
            Thickness stackPanelMargin = new Thickness(0, 10, 10, 0); // Margin cho StackPanel chính (cột 1)
            Thickness stackPanelMargin1 = new Thickness(0, 80, 0, 0); // Margin cho StackPanel 左右
            Thickness stackPanelMargin2 = new Thickness(750, 80, 0, 0); // Margin cho StackPanel 上下 (case 1)
            Thickness stackPanelMargin3 = new Thickness(375, 80, 0, 0); // Margin cho StackPanel 上下 (case 2)
            Thickness stackPanelMargin4 = new Thickness(-250, 10, 10, 0); // Margin cho StackPanel chính (cột 2)
            Thickness stackPanelMargin5 = new Thickness(-125, 10, 10, 0); // Margin cho StackPanel chính (cột 3)

            switch (visibleColumns)
            {
                case 1:
                    columnSpans[0] = 4;
                    elementWidths[0] = 1500 - 20;
                    elementWidths1[0] = 750 - 20;
                    break;
                case 2:
                    columnSpans[0] = 2;
                    columnSpans[1] = 2;
                    elementWidths[0] = 750 - 20;
                    elementWidths[1] = 750 - 20;
                    elementWidths1[0] = 375 - 20;
                    elementWidths1[1] = 375 - 20;
                    break;
                case 3:
                    columnSpans[0] = 2;
                    columnSpans[1] = 2;
                    columnSpans[2] = 2;
                    elementWidths[0] = 500 - 20;
                    elementWidths[1] = 500 - 20;
                    elementWidths[2] = 500 - 20;
                    elementWidths1[0] = 250 - 20;
                    elementWidths1[1] = 250 - 20;
                    elementWidths1[2] = 250 - 20;
                    break;
                case 4:
                default:
                    columnSpans = new int[] { 1, 1, 1, 1 };
                    elementWidths = new double[] { 375 - 20, 375 - 20, 375 - 20, 375 - 20 };
                    elementWidths1 = new double[] { 187.5 - 20, 187.5 - 20, 187.5 - 20, 187.5 - 20 };
                    break;
            }

            // Tạo danh sách các StackPanel hiển thị theo thứ tự ưu tiên
            var visiblePanels = new List<(StackPanel main, StackPanel leftRight, StackPanel upDown, ComboBox combo, TextBox leftText, TextBox upText)>();
            if (isTopVisible) visiblePanels.Add((上側芯筋Panel, 上側芯筋左右Panel, 上側芯筋上下Panel, 上側芯筋本数2, 左右4, 上下4));
            if (isBottomVisible) visiblePanels.Add((下側芯筋Panel, 下側芯筋左右Panel, 下側芯筋上下Panel, 下側芯筋本数2, 左右5, 上下5));
            if (isLeftVisible) visiblePanels.Add((左側芯筋Panel, 左側芯筋左右Panel, 左側芯筋上下Panel, 左側芯筋本数2, 左右6, 上下6));
            if (isRightVisible) visiblePanels.Add((右側芯筋Panel, 右側芯筋左右Panel, 右側芯筋上下Panel, 右側芯筋本数2, 左右7, 上下7));

            // Gán vị trí và thuộc tính cho các StackPanel hiển thị
            int currentColumn = 0;
            for (int index = 0; index < visiblePanels.Count; index++)
            {
                var (mainPanel, leftRightPanel, upDownPanel, comboBox, leftTextBox, upTextBox) = visiblePanels[index];

                // Gán ColumnSpan và Column
                mainPanel.SetValue(Grid.ColumnSpanProperty, columnSpans[index]);
                leftRightPanel.SetValue(Grid.ColumnSpanProperty, columnSpans[index]);
                upDownPanel.SetValue(Grid.ColumnSpanProperty, columnSpans[index]);
                mainPanel.SetValue(Grid.ColumnProperty, currentColumn);
                leftRightPanel.SetValue(Grid.ColumnProperty, currentColumn);
                upDownPanel.SetValue(Grid.ColumnProperty, currentColumn);

                // Gán Width
                comboBox.Width = elementWidths[index];
                leftTextBox.Width = elementWidths1[index];
                upTextBox.Width = elementWidths1[index];
                leftTextBox.HorizontalAlignment = HorizontalAlignment.Left;
                upTextBox.HorizontalAlignment = HorizontalAlignment.Left;

                // Gán Margin theo vị trí cột trong case 3
                if (visibleColumns == 3)
                {
                    if (index == 0) // Cột 1: Dùng margin của 上側芯筋
                    {
                        mainPanel.Margin = stackPanelMargin;
                        leftRightPanel.Margin = stackPanelMargin1;
                        upDownPanel.Margin = new Thickness(250, 80, 0, 0); // 上側芯筋上下PanelMargin
                    }
                    else if (index == 1) // Cột 2: Dùng margin của 下側芯筋
                    {
                        mainPanel.Margin = stackPanelMargin4;
                        leftRightPanel.Margin = new Thickness(-250, 80, 10, 5); // 下側芯筋左右PanelMargin
                        upDownPanel.Margin = new Thickness(0, 80, 10, 5); // 下側芯筋上下PanelMargin
                    }
                    else if (index == 2) // Cột 3: Dùng margin của 左側芯筋/右側芯筋
                    {
                        mainPanel.Margin = stackPanelMargin5;
                        leftRightPanel.Margin = new Thickness(-125, 80, 10, 5); // 左側芯筋左右PanelMargin
                        upDownPanel.Margin = new Thickness(125, 80, 10, 5); // 左側芯筋上下PanelMargin
                    }
                }
                else if (visibleColumns == 2)
                {
                    mainPanel.Margin = stackPanelMargin;
                    leftRightPanel.Margin = stackPanelMargin1;
                    upDownPanel.Margin = stackPanelMargin3;
                }
                else if (visibleColumns == 4)
                {
                    mainPanel.Margin = stackPanelMargin;
                    leftRightPanel.Margin = stackPanelMargin1;
                    upDownPanel.Margin = new Thickness(187.5, 80, 0, 0); // Margin cho case 4
                }
                else // Case 1
                {
                    mainPanel.Margin = stackPanelMargin;
                    leftRightPanel.Margin = stackPanelMargin1;
                    upDownPanel.Margin = stackPanelMargin2;
                }

                currentColumn += columnSpans[index];
            }

            // Điều chỉnh Margin của các thành phần phía dưới
            double additionalMargin = visibleRows > 0 ? 110 : 0;
            Thickness newMargin = new Thickness(10, 397 + additionalMargin, 10, 0);
            Thickness newMarginBottom = new Thickness(10, 455 + additionalMargin, 10, 0);
            Thickness checkBoxMargin1 = new Thickness(10, 423 + additionalMargin, 10, 0);
            Thickness checkBoxMargin2 = new Thickness(10, 481 + additionalMargin, 10, 0);

            // Áp dụng Margin cho các StackPanel
            フックの位置.Parent.SetValue(FrameworkElement.MarginProperty, newMargin);
            縦向き中子の数.Parent.SetValue(FrameworkElement.MarginProperty, newMargin);
            縦向き中子の位置.Parent.SetValue(FrameworkElement.MarginProperty, newMargin);
            横向き中子の数.Parent.SetValue(FrameworkElement.MarginProperty, newMarginBottom);
            横向き中子の位置.Parent.SetValue(FrameworkElement.MarginProperty, newMarginBottom);

            // Áp dụng Margin cho các CheckBox
            縦向き中子_方向.Margin = checkBoxMargin1;
            横向き中子_方向.Margin = checkBoxMargin2;
        }
        private List<double> AswapB(List<double> A, List<double> B)
        {
            List<double> list_A = new List<double>();
            List<double> list_B = new List<double>();

            for (int i = 2; i < A.Count - 2; i++)
            {
                list_A.Add(A[i]);
            }

            for (int i = 2; i < B.Count - 2; i++)
            {
                list_B.Add(B[i]);
            }

            int countA = list_A.Count;
            int countB = list_B.Count;

            List<double> newB = new List<double>();
            if (B.Count > 2)
            {
                if (countA <= countB)
                {
                    //for (int i = 1; i < B.Count - 1; i++)
                    //{
                    //    list_B.Add(B[i]);
                    //}
                    return list_B;
                }
                else
                {
                    // TH1: A lẻ, B lẻ
                    if (countA % 2 == 1 && countB % 2 == 1)
                    {
                        int mid = countA / 2;
                        int numSides = (countB - 1) / 2;
                        //newB.Add(B[1]);
                        newB.AddRange(list_A.Take(numSides));                      // đầu
                        newB.Add(list_A[mid]);                                     // giữa
                        newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                        //newB.Add(B[B.Count - 2]);
                    }
                    // TH2: A lẻ, B chẵn
                    else if (countA % 2 == 1 && countB % 2 == 0)
                    {
                        int numSides = countB / 2;
                        //newB.Add(B[1]);
                        newB.AddRange(list_A.Take(numSides));                      // đầu
                        newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                        //newB.Add(B[B.Count - 2]);
                    }
                    // TH3: A chẵn, B lẻ
                    else if (countA % 2 == 0 && countB % 2 == 1)
                    {
                        int numSides = (countB - 1) / 2;
                        int midLeft = countA / 2 - 1;
                        //newB.Add(B[1]);
                        newB.AddRange(list_A.Take(numSides));                      // đầu
                        newB.Add(list_A[midLeft]);                                 // giữa trái
                        newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                        //newB.Add(B[B.Count - 2]);
                    }
                    // TH4: A chẵn, B chẵn
                    else if (countA % 2 == 0 && countB % 2 == 0)
                    {
                        int numSides = countB / 2;
                        //newB.Add(B[1]);
                        newB.AddRange(list_A.Take(numSides));                      // đầu
                        newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                        //newB.Add(B[B.Count - 2]);
                    }

                }
            }

            //string listBStr = newB.Count > 0 ? String.Join(", ", newB.Select(x => x.ToString("F2"))) : "Empty";
            //MessageBox.Show($"list_B after swap: [{listBStr}]");
            return newB;

        }

        private List<double> Aswap_dimension_lineB(List<double> A, List<double> B)
        {
            List<double> list_A = new List<double>();
            List<double> list_B = new List<double>();

            for (int i = 2; i < A.Count - 2; i++)
            {
                list_A.Add(A[i]);
            }

            for (int i = 2; i < B.Count - 2; i++)
            {
                list_B.Add(B[i]);
            }

            int countA = list_A.Count;
            int countB = list_B.Count;

            List<double> newB = new List<double>();

            if (countA <= countB)
            {
                for (int i = 0; i < B.Count; i++)
                {
                    newB.Add(B[i]);
                }
            }
            else
            {
                // TH1: A lẻ, B lẻ
                if (countA % 2 == 1 && countB % 2 == 1)
                {
                    int mid = countA / 2;
                    int numSides = (countB - 1) / 2;
                    newB.Add(B[0]);
                    newB.Add(B[1]);
                    newB.AddRange(list_A.Take(numSides));                      // đầu
                    newB.Add(list_A[mid]);                                     // giữa
                    newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                    newB.Add(B[B.Count - 2]);
                    newB.Add(B[B.Count - 1]);
                }
                // TH2: A lẻ, B chẵn
                else if (countA % 2 == 1 && countB % 2 == 0)
                {
                    int numSides = countB / 2;
                    newB.Add(B[0]);
                    newB.Add(B[1]);
                    newB.AddRange(list_A.Take(numSides));                      // đầu
                    newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                    newB.Add(B[B.Count - 2]);
                    newB.Add(B[B.Count - 1]);
                }
                // TH3: A chẵn, B lẻ
                else if (countA % 2 == 0 && countB % 2 == 1)
                {
                    int numSides = (countB - 1) / 2;
                    int midLeft = countA / 2 - 1;
                    newB.Add(B[0]);
                    newB.Add(B[1]);
                    newB.AddRange(list_A.Take(numSides));                      // đầu
                    newB.Add(list_A[midLeft]);                                 // giữa trái
                    newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                    newB.Add(B[B.Count - 2]);
                    newB.Add(B[B.Count - 1]);
                }
                // TH4: A chẵn, B chẵn
                else if (countA % 2 == 0 && countB % 2 == 0)
                {
                    int numSides = countB / 2;
                    newB.Add(B[0]);
                    newB.Add(B[1]);
                    newB.AddRange(list_A.Take(numSides));                      // đầu
                    newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                    newB.Add(B[B.Count - 2]);
                    newB.Add(B[B.Count - 1]);
                }

            }


            //string listBStr = newB.Count > 0 ? String.Join(", ", newB.Select(x => x.ToString("F2"))) : "Empty";
            //MessageBox.Show($"list_B after swap: [{listBStr}]");
            return newB;

        }
        private void DrawLine(Canvas canvas, double originX, double originY, double scale,
                          double x1_real, double y1_real, double x2_real, double y2_real, Brush color, double thickness = 2)
        {
            double X1 = originX + x1_real * scale;
            double Y1 = originY + y1_real * scale;
            double X2 = originX + x2_real * scale;
            double Y2 = originY + y2_real * scale;

            if (double.IsNaN(X1) || double.IsNaN(Y1) || double.IsNaN(X2) || double.IsNaN(Y2))
                return; // Không vẽ nếu có NaN

            Line line = new Line
            {
                X1 = X1,
                Y1 = Y1,
                X2 = X2,
                Y2 = Y2,
                Stroke = color,
                StrokeThickness = thickness
            };
            canvas.Children.Add(line);
        }

        private void DrawArc(Canvas canvas, double originX, double originY, double scale, double cx, double cy, double radius, int quadrant, Brush stroke)
        {
            double startAngle = 0;
            double endAngle = 90;

            // Tính góc bắt đầu dựa vào góc phần tư
            switch (quadrant)
            {
                case 1: startAngle = 270; break; // top-right
                case 2: startAngle = 180; break; // top-left
                case 3: startAngle = 90; break;  // bottom-left
                case 4: startAngle = 0; break;   // bottom-right
            }

            // Tính điểm bắt đầu và kết thúc của cung
            double x1 = cx + radius * Math.Cos(startAngle * Math.PI / 180);
            double y1 = cy + radius * Math.Sin(startAngle * Math.PI / 180);
            double x2 = cx + radius * Math.Cos((startAngle + 90) * Math.PI / 180);
            double y2 = cy + radius * Math.Sin((startAngle + 90) * Math.PI / 180);

            PathFigure figure = new PathFigure
            {
                StartPoint = new Point(originX + x1 * scale, originY + y1 * scale)
            };

            ArcSegment arc = new ArcSegment
            {
                Point = new Point(originX + x2 * scale, originY + y2 * scale),
                Size = new Size(radius * scale, radius * scale),
                IsLargeArc = false,
                SweepDirection = SweepDirection.Clockwise
            };

            figure.Segments.Add(arc);

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);

            Path path = new Path
            {
                Stroke = stroke,
                StrokeThickness = 2,
                Data = geometry
            };

            canvas.Children.Add(path);
        }
        private void DrawCircle(Canvas canvas, double originX, double originY, double scale, double x, double y, double radius, Brush fill)
        {

            Ellipse circle = new Ellipse
            {
                Width = radius * 2 * scale,
                Height = radius * 2 * scale,
                Fill = fill
            };

            // Canvas vị trí
            Canvas.SetLeft(circle, originX + x * scale - radius * scale);
            Canvas.SetTop(circle, originY + y * scale - radius * scale);
            canvas.Children.Add(circle);
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
            { 38, 43 }
        };
        private void DrawRebarDimensions(Canvas canvas, double originX, double originY, double scale, List<double> xPositions, double yPosition, double rebarDiameter, double linechange_x, double linechange_y, double change_x, double change_y, Brush color)
        {

            for (int i = 0; i < xPositions.Count - 1; i++)
            {
                double x1 = xPositions[i];
                double x2 = xPositions[i + 1];
                double dist = Math.Abs(x2 - x1);
                string label;
                if (Math.Abs(dist - Math.Floor(dist)) < 0.001)
                {
                    label = $"{(int)dist}";
                }
                else
                {
                    label = $"{dist:F2}";
                }
                DrawDimension(canvas, originX, originY, scale,
                    x1, yPosition + linechange_y / scale,
                    x2, yPosition + linechange_y / scale,
                    label, linechange_x, linechange_y, change_x, change_y, color);
            }
        }


        private void DrawRebarDimensionsVertical(Canvas canvas, double originX, double originY, double scale, double xPosition, List<double> yPositions, double rebarDiameter, double linechange_x, double linechange_y, double change_x, double change_y, Brush color)
        {
            for (int i = 0; i < yPositions.Count - 1; i++)
            {
                double y1 = yPositions[i];
                double y2 = yPositions[i + 1];
                double dist = Math.Abs(y2 - y1);
                string label;
                if (Math.Abs(dist - Math.Floor(dist)) < 0.001) // Kiểm tra nếu dist là số nguyên
                {
                    label = $"{(int)dist}"; // Hiển thị số nguyên (chẵn)
                }
                else
                {
                    label = $"{dist:F2}"; // Hiển thị 1 chữ số thập phân (lẻ)
                }
                DrawDimension(canvas, originX, originY, scale,
                    xPosition + linechange_x / scale, y1,
                    xPosition + linechange_x / scale, y2, label, linechange_x, linechange_y,
                     change_x, change_y, color);
            }
        }
        private void DrawDimension(Canvas canvas, double originX, double originY, double scale,
        double x1_real, double y1_real, double x2_real, double y2_real, string label, double linechange_x, double linechange_y, double change_x, double change_y, Brush color)
        {
            //double change_x = 0;
            //double change_y = 0;

            double x1 = x1_real;
            double y1 = y1_real;
            double x2 = x2_real;
            double y2 = y2_real;

            double dx = x2 - x1;
            double dy = y2 - y1;
            double length = Math.Sqrt(dx * dx + dy * dy);
            double midX = (x1 + x2) / 2;
            double midY = (y1 + y2) / 2;

            // Vẽ hai tick (gạch đứng nhỏ)
            double tickLength = 5 / scale; // Chuyển về đơn vị thực để đúng tỉ lệ
            Vector tickDir = new Vector(-dy, dx);
            tickDir.Normalize();
            tickDir *= tickLength;

            DrawLine(canvas, originX, originY, scale,
                x1 - tickDir.X, y1 - tickDir.Y,
                x1 + tickDir.X, y1 + tickDir.Y,
                color, 1);

            DrawLine(canvas, originX, originY, scale,
                x2 - tickDir.X, y2 - tickDir.Y,
                x2 + tickDir.X, y2 + tickDir.Y,
                color, 1);

            //// Vẽ đường kích thước chính
            //DrawLine(canvas, originX, originY, scale,
            //    x1, y1, x2, y2, color, 1);


            // Text hiển thị kích thước
            TextBlock text = new TextBlock
            {
                Text = $"{label}",
                Foreground = color,
                FontSize = 12,
                FontWeight = FontWeights.Bold
            };
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size textSize = text.DesiredSize;

            // Tính khoảng hở giữa line dựa theo kích thước text (tính ra đơn vị thực tế)
            double textHalfLengthReal = textSize.Width / scale / 2.0;

            // Hướng của đoạn line
            double dirX = dx / length;
            double dirY = dy / length;

            double gapX = dirX * textHalfLengthReal;
            double gapY = dirY * textHalfLengthReal;

            // Vẽ đoạn trái
            DrawLine(canvas, originX, originY, scale,
                x1, y1,
                midX - gapX, midY - gapY,
                color, 0.7);

            // Vẽ đoạn phải
            DrawLine(canvas, originX, originY, scale,
                midX + gapX, midY + gapY,
                x2, y2,
                color, 0.7);

            // Xoay nếu là đường thẳng đứng
            double angle = Math.Atan2(dy, dx) * 180 / Math.PI;
            if (Math.Abs(dx) < 0.001) angle = -90;

            text.RenderTransformOrigin = new Point(0.5, 0.5);
            text.RenderTransform = new RotateTransform(angle);

            // Căn giữa text


            Canvas.SetLeft(text, originX + midX * scale - textSize.Width / 2 + change_x);
            Canvas.SetTop(text, originY + midY * scale - textSize.Height / 2 + change_y);

            canvas.Children.Add(text);
        }
        private void DrawArc_HOOP(Canvas canvas, double originX, double originY, double scale,
        double cx, double cy, double radius, int quadrant, double d, Brush stroke)
        {
            double startAngle = 0;
            double shape = 0;
            double multiples1 = 0;
            double multiples2 = 0;
            switch (quadrant)
            {

                case 1:
                    startAngle = 225;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 2:
                    startAngle = 225;
                    shape = 135;
                    multiples1 = 6;
                    multiples2 = 8;
                    break;
                case 3:
                    startAngle = 180;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 4:
                    startAngle = 270;
                    shape = 90;
                    multiples1 = 8;
                    multiples2 = 8;
                    break;

                case 5:
                    startAngle = 135;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 6:
                    startAngle = 180;
                    shape = 135;
                    multiples1 = 8;
                    multiples2 = 6;
                    break;

                case 7:
                    startAngle = 180;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 8:
                    startAngle = 180;
                    shape = 90;
                    multiples1 = 8;
                    multiples2 = 8;
                    break;

                case 9:
                    startAngle = 45;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 10:
                    startAngle = 45;
                    shape = 135;
                    multiples1 = 6;
                    multiples2 = 8;
                    break;
                case 11:
                    startAngle = 0;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 12:
                    startAngle = 90;
                    shape = 90;
                    multiples1 = 8;
                    multiples2 = 8;
                    break;

                case 13:
                    startAngle = -45;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 14:
                    startAngle = 0;
                    shape = 135;
                    multiples1 = 8;
                    multiples2 = 6;
                    break;
                case 15:
                    startAngle = 0;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 16:
                    startAngle = 0;
                    shape = 90;
                    multiples1 = 8;
                    multiples2 = 8;
                    break;
            }

            // startAngle = 180 // shape = 135; có móc
            // startAngle = 180 // shape = 180; quay xuống
            // startAngle = 135 // shape = 180; góc 135


            // Tính điểm đầu và cuối cung
            double x1 = cx + radius * Math.Cos(startAngle * Math.PI / 180);
            double y1 = cy + radius * Math.Sin(startAngle * Math.PI / 180);
            double x2 = cx + radius * Math.Cos((startAngle + shape) * Math.PI / 180);
            double y2 = cy + radius * Math.Sin((startAngle + shape) * Math.PI / 180);

            // Vẽ cung tròn
            PathFigure figure = new PathFigure
            {
                StartPoint = new Point(originX + x1 * scale, originY + y1 * scale)
            };
            ArcSegment arc = new ArcSegment
            {
                Point = new Point(originX + x2 * scale, originY + y2 * scale),
                Size = new Size(radius * scale, radius * scale),
                IsLargeArc = false,
                SweepDirection = SweepDirection.Clockwise
            };
            figure.Segments.Add(arc);

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            Path path = new Path
            {
                Stroke = stroke,
                StrokeThickness = 2,
                Data = geometry
            };
            canvas.Children.Add(path);

            //// 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn
            double lineLength1 = (d * multiples1); // Chiều dài đoạn thẳng, đã scale
            double lineLength2 = (d * multiples2); // Chiều dài đoạn thẳng, đã scale
            // Vector pháp tuyến tiếp tuyến tại điểm đầu
            double dx1 = Math.Sin(startAngle * Math.PI / 180);
            double dy1 = -Math.Cos(startAngle * Math.PI / 180);
            // Vector pháp tuyến tiếp tuyến tại điểm cuối
            double dx2 = -Math.Sin((startAngle + shape) * Math.PI / 180);
            double dy2 = Math.Cos((startAngle + shape) * Math.PI / 180);
            // Vẽ đoạn thẳng ngắn từ điểm đầu
            DrawLine(canvas, originX, originY, scale,
                     x1, y1,
                     x1 + dx1 * lineLength1,
                     y1 + dy1 * lineLength1,
                     stroke, 2);
            //Vẽ đoạn thẳng ngắn từ điểm cuối
            DrawLine(canvas, originX, originY, scale,
                     x2, y2,
                     x2 + dx2 * lineLength2,
                     y2 + dy2 * lineLength2,
                     stroke, 2);

        }
        private void line_HOOP(Canvas canvas, double originX, double originY, double scale,
        double cx1, double cy1, double cx11, double cy11, double radius1, double radius11, int quadrant, double d, Brush stroke)
        {
            double startAngle = 0;
            double shape = 0;
            double multiples1 = 0;
            double multiples2 = 0;
            switch (quadrant)
            {
                case 1:
                    startAngle = 225;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 2:
                    startAngle = 225;
                    shape = 135;
                    multiples1 = 6;
                    multiples2 = 8;
                    break;
                case 3:
                    startAngle = 180;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 4:
                    startAngle = 270;
                    shape = 90;
                    multiples1 = 8;
                    multiples2 = 8;
                    break;

                case 5:
                    startAngle = 135;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 6:
                    startAngle = 180;
                    shape = 135;
                    multiples1 = 8;
                    multiples2 = 6;
                    break;

                case 7:
                    startAngle = 180;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 8:
                    startAngle = 180;
                    shape = 90;
                    multiples1 = 8;
                    multiples2 = 8;
                    break;

                case 9:
                    startAngle = 45;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 10:
                    startAngle = 45;
                    shape = 135;
                    multiples1 = 6;
                    multiples2 = 8;
                    break;
                case 11:
                    startAngle = 0;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 12:
                    startAngle = 90;
                    shape = 90;
                    multiples1 = 8;
                    multiples2 = 8;
                    break;

                case 13:
                    startAngle = -45;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 14:
                    startAngle = 0;
                    shape = 135;
                    multiples1 = 8;
                    multiples2 = 6;
                    break;
                case 15:
                    startAngle = 0;
                    shape = 180;
                    multiples1 = 6;
                    multiples2 = 6;
                    break;
                case 16:
                    startAngle = 0;
                    shape = 90;
                    multiples1 = 8;
                    multiples2 = 8;
                    break;
            }
            double lineLength1 = (d * multiples1); // Chiều dài đoạn thẳng, đã scale
            double lineLength2 = (d * multiples2); // Chiều dài đoạn thẳng, đã scale
            //double lineLength = 6 * スタラップ径_changed / scale; // Chiều dài đoạn thẳng, đã scale
            // Vector pháp tuyến tiếp tuyến tại điểm đầu
            double dx1 = Math.Sin(startAngle * Math.PI / 180);
            double dy1 = -Math.Cos(startAngle * Math.PI / 180);
            // Vector pháp tuyến tiếp tuyến tại điểm cuối
            double dx2 = -Math.Sin((startAngle + shape) * Math.PI / 180);
            double dy2 = Math.Cos((startAngle + shape) * Math.PI / 180);
            // Tính điểm đầu và cuối cung
            double x1 = cx1 + radius1 * Math.Cos(startAngle * Math.PI / 180);
            double y1 = cy1 + radius1 * Math.Sin(startAngle * Math.PI / 180);
            double x2 = cx1 + radius1 * Math.Cos((startAngle + shape) * Math.PI / 180);
            double y2 = cy1 + radius1 * Math.Sin((startAngle + shape) * Math.PI / 180);

            double x11 = cx11 + radius11 * Math.Cos(startAngle * Math.PI / 180);
            double y11 = cy11 + radius11 * Math.Sin(startAngle * Math.PI / 180);
            double x22 = cx11 + radius11 * Math.Cos((startAngle + shape) * Math.PI / 180);
            double y22 = cy11 + radius11 * Math.Sin((startAngle + shape) * Math.PI / 180);
            // Vẽ đoạn thẳng ngắn từ điểm đầu
            DrawLine(MyCanvas, originX, originY, scale,
                     x1 + dx1 * lineLength1, y1 + dy1 * lineLength1,
                     x11 + dx1 * lineLength1, y11 + dy1 * lineLength1,
                     stroke, 2);
            DrawLine(MyCanvas, originX, originY, scale,
                    x22 + dx2 * lineLength2,
                    y22 + dy2 * lineLength2,
                    x2 + dx2 * lineLength2,
                    y2 + dy2 * lineLength2,
                    stroke, 2);
        }

        private void DrawArc_中子筋(Canvas canvas, double originX, double originY, double scale,
        double cx, double cy, double radius, int quadrant, double d, Brush stroke, int rotation_direction, bool direction)
        {
            double startAngle = 0;
            double shape = 0;
            double multiples1 = 0;
            double multiples2 = 0;
            if (direction)
            {
                switch (quadrant)
                {
                    case 1:
                        startAngle = 180;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 2:
                        startAngle = 270;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 3:
                        startAngle = 270;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 4:
                        startAngle = 225;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 5:
                        startAngle = 270;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;

                    case 11:
                        startAngle = 0;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 12:
                        startAngle = 0;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 13:
                        startAngle = 0;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 14:
                        startAngle = 0;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 15:
                        startAngle = 0;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                }
            }
            else
            {
                switch (quadrant)
                {
                    case 1:
                        startAngle = 180;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 2:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 3:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 4:
                        startAngle = 180;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 5:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;

                    case 11:
                        startAngle = 0;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 12:
                        startAngle = 0;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 13:
                        startAngle = 90;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 14:
                        startAngle = 45;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 15:
                        startAngle = 45;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                }
            }

            // Tính điểm đầu và cuối cung
            double x1 = cx + radius * Math.Cos(startAngle * Math.PI / 180);
            double y1 = cy + radius * Math.Sin(startAngle * Math.PI / 180);
            double x2 = cx + radius * Math.Cos((startAngle + shape) * Math.PI / 180);
            double y2 = cy + radius * Math.Sin((startAngle + shape) * Math.PI / 180);

            // Vẽ cung tròn
            PathFigure figure = new PathFigure
            {
                StartPoint = new Point(originX + x1 * scale, originY + y1 * scale)
            };
            ArcSegment arc = new ArcSegment
            {
                Point = new Point(originX + x2 * scale, originY + y2 * scale),
                Size = new Size(radius * scale, radius * scale),
                IsLargeArc = false,
                SweepDirection = SweepDirection.Clockwise
            };
            figure.Segments.Add(arc);

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            Path path = new Path
            {
                Stroke = stroke,
                StrokeThickness = 2,
                Data = geometry
            };
            canvas.Children.Add(path);

            //// 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn
            double lineLength1 = (d * multiples1); // Chiều dài đoạn thẳng, đã scale
            double lineLength2 = (d * multiples2); // Chiều dài đoạn thẳng, đã scale
            // Vector pháp tuyến tiếp tuyến tại điểm đầu
            double dx1 = Math.Sin(startAngle * Math.PI / 180);
            double dy1 = -Math.Cos(startAngle * Math.PI / 180);
            // Vector pháp tuyến tiếp tuyến tại điểm cuối
            double dx2 = -Math.Sin((startAngle + shape) * Math.PI / 180);
            double dy2 = Math.Cos((startAngle + shape) * Math.PI / 180);
            // Vẽ đoạn thẳng ngắn từ điểm đầu
            if (rotation_direction == 1)
            {
                DrawLine(canvas, originX, originY, scale,
                         x1, y1,
                         x1 + dx1 * lineLength1,
                         y1 + dy1 * lineLength1,
                         stroke, 2);
            }
            else
            {
                //Vẽ đoạn thẳng ngắn từ điểm cuối
                DrawLine(canvas, originX, originY, scale,
                         x2, y2,
                         x2 + dx2 * lineLength2,
                         y2 + dy2 * lineLength2,
                         stroke, 2);
            }



        }
        private void line_中子筋(Canvas canvas, double originX, double originY, double scale,
        double cx1, double cy1, double cx11, double cy11, double radius1, double radius11, int quadrant, double d, Brush stroke, int rotation_direction, bool direction)
        {
            double startAngle = 0;
            double shape = 0;
            double multiples1 = 0;
            double multiples2 = 0;

            if (direction)
            {
                switch (quadrant)
                {
                    case 1:
                        startAngle = 180;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 2:
                        startAngle = 270;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 3:
                        startAngle = 270;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 4:
                        startAngle = 225;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 5:
                        startAngle = 270;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;

                    case 11:
                        startAngle = 0;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 12:
                        startAngle = 0;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 13:
                        startAngle = 0;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 14:
                        startAngle = 0;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 15:
                        startAngle = 0;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                }
            }
            else
            {
                switch (quadrant)
                {
                    case 1:
                        startAngle = 180;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 2:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 3:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 4:
                        startAngle = 180;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 5:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;

                    case 11:
                        startAngle = 0;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 12:
                        startAngle = 0;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 13:
                        startAngle = 90;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 14:
                        startAngle = 45;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 15:
                        startAngle = 45;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                }
            }
            double lineLength1 = (d * multiples1); // Chiều dài đoạn thẳng, đã scale
            double lineLength2 = (d * multiples2); // Chiều dài đoạn thẳng, đã scale
            //double lineLength = 6 * スタラップ径_changed / scale; // Chiều dài đoạn thẳng, đã scale
            // Vector pháp tuyến tiếp tuyến tại điểm đầu
            double dx1 = Math.Sin(startAngle * Math.PI / 180);
            double dy1 = -Math.Cos(startAngle * Math.PI / 180);
            // Vector pháp tuyến tiếp tuyến tại điểm cuối
            double dx2 = -Math.Sin((startAngle + shape) * Math.PI / 180);
            double dy2 = Math.Cos((startAngle + shape) * Math.PI / 180);
            // Tính điểm đầu và cuối cung
            double x1 = cx1 + radius1 * Math.Cos(startAngle * Math.PI / 180);
            double y1 = cy1 + radius1 * Math.Sin(startAngle * Math.PI / 180);
            double x2 = cx1 + radius1 * Math.Cos((startAngle + shape) * Math.PI / 180);
            double y2 = cy1 + radius1 * Math.Sin((startAngle + shape) * Math.PI / 180);

            double x11 = cx11 + radius11 * Math.Cos(startAngle * Math.PI / 180);
            double y11 = cy11 + radius11 * Math.Sin(startAngle * Math.PI / 180);
            double x22 = cx11 + radius11 * Math.Cos((startAngle + shape) * Math.PI / 180);
            double y22 = cy11 + radius11 * Math.Sin((startAngle + shape) * Math.PI / 180);
            // Vẽ đoạn thẳng ngắn từ điểm đầu
            if (rotation_direction == 1)
            {
                DrawLine(MyCanvas, originX, originY, scale,
                         x1 + dx1 * lineLength1, y1 + dy1 * lineLength1,
                         x11 + dx1 * lineLength1, y11 + dy1 * lineLength1,
                         stroke, 2);
            }
            else
            {
                DrawLine(MyCanvas, originX, originY, scale,
                        x22 + dx2 * lineLength2,
                        y22 + dy2 * lineLength2,
                        x2 + dx2 * lineLength2,
                        y2 + dy2 * lineLength2,
                        stroke, 2);
            }

        }
        private void DrawArc_横中子筋(Canvas canvas, double originX, double originY, double scale,
        double cx, double cy, double radius, int quadrant, double d, Brush stroke, int rotation_direction, bool direction)
        {
            double startAngle = 0;
            double shape = 0;
            double multiples1 = 0;
            double multiples2 = 0;
            if (direction)
            {
                switch (quadrant)
                {
                    case 1:
                        startAngle = 90;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 2:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 3:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 4:
                        startAngle = 135;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 5:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;

                    case 11:
                        startAngle = 270;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 12:
                        startAngle = 270;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 13:
                        startAngle = 270;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 14:
                        startAngle = 270;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 15:
                        startAngle = 270;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                }
            }
            else
            {
                switch (quadrant)
                {
                    case 1:
                        startAngle = 90;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 2:
                        startAngle = 90;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 3:
                        startAngle = 90;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 4:
                        startAngle = 90;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 5:
                        startAngle = 90;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;

                    case 11:
                        startAngle = 270;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 12:
                        startAngle = 270;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 13:
                        startAngle = 0;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 14:
                        startAngle = 315;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 15:
                        startAngle = 315;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                }
            }

            // Tính điểm đầu và cuối cung
            double x1 = cx + radius * Math.Cos(startAngle * Math.PI / 180);
            double y1 = cy + radius * Math.Sin(startAngle * Math.PI / 180);
            double x2 = cx + radius * Math.Cos((startAngle + shape) * Math.PI / 180);
            double y2 = cy + radius * Math.Sin((startAngle + shape) * Math.PI / 180);

            // Vẽ cung tròn
            PathFigure figure = new PathFigure
            {
                StartPoint = new Point(originX + x1 * scale, originY + y1 * scale)
            };
            ArcSegment arc = new ArcSegment
            {
                Point = new Point(originX + x2 * scale, originY + y2 * scale),
                Size = new Size(radius * scale, radius * scale),
                IsLargeArc = false,
                SweepDirection = SweepDirection.Clockwise
            };
            figure.Segments.Add(arc);

            PathGeometry geometry = new PathGeometry();
            geometry.Figures.Add(figure);
            Path path = new Path
            {
                Stroke = stroke,
                StrokeThickness = 2,
                Data = geometry
            };
            canvas.Children.Add(path);

            //// 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn
            double lineLength1 = (d * multiples1); // Chiều dài đoạn thẳng, đã scale
            double lineLength2 = (d * multiples2); // Chiều dài đoạn thẳng, đã scale
                                                   // Vector pháp tuyến tiếp tuyến tại điểm đầu
            double dx1 = Math.Sin(startAngle * Math.PI / 180);
            double dy1 = -Math.Cos(startAngle * Math.PI / 180);
            // Vector pháp tuyến tiếp tuyến tại điểm cuối
            double dx2 = -Math.Sin((startAngle + shape) * Math.PI / 180);
            double dy2 = Math.Cos((startAngle + shape) * Math.PI / 180);
            // Vẽ đoạn thẳng ngắn từ điểm đầu
            if (rotation_direction == 1)
            {
                DrawLine(canvas, originX, originY, scale,
                         x1, y1,
                         x1 + dx1 * lineLength1,
                         y1 + dy1 * lineLength1,
                         stroke, 2);
            }
            else
            {
                //Vẽ đoạn thẳng ngắn từ điểm cuối
                DrawLine(canvas, originX, originY, scale,
                         x2, y2,
                         x2 + dx2 * lineLength2,
                         y2 + dy2 * lineLength2,
                         stroke, 2);
            }

        }
        private void line_横中子筋(Canvas canvas, double originX, double originY, double scale,
        double cx1, double cy1, double cx11, double cy11, double radius1, double radius11, int quadrant, double d, Brush stroke, int rotation_direction, bool direction)
        {
            double startAngle = 0;
            double shape = 0;
            double multiples1 = 0;
            double multiples2 = 0;

            if (direction)
            {
                switch (quadrant)
                {
                    case 1:
                        startAngle = 90;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 2:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 3:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 4:
                        startAngle = 135;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 5:
                        startAngle = 180;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;

                    case 11:
                        startAngle = 270;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 12:
                        startAngle = 270;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 13:
                        startAngle = 270;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 14:
                        startAngle = 270;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 15:
                        startAngle = 270;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                }
            }
            else
            {
                switch (quadrant)
                {
                    case 1:
                        startAngle = 90;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 2:
                        startAngle = 90;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 3:
                        startAngle = 90;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 4:
                        startAngle = 90;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 5:
                        startAngle = 90;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;

                    case 11:
                        startAngle = 270;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 12:
                        startAngle = 270;
                        shape = 180;
                        multiples1 = 4;
                        multiples2 = 4;
                        break;
                    case 13:
                        startAngle = 0;
                        shape = 90;
                        multiples1 = 8;
                        multiples2 = 8;
                        break;
                    case 14:
                        startAngle = 315;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                    case 15:
                        startAngle = 315;
                        shape = 135;
                        multiples1 = 6;
                        multiples2 = 6;
                        break;
                }
            }
            double lineLength1 = (d * multiples1); // Chiều dài đoạn thẳng, đã scale
            double lineLength2 = (d * multiples2); // Chiều dài đoạn thẳng, đã scale
            //double lineLength = 6 * スタラップ径_changed / scale; // Chiều dài đoạn thẳng, đã scale
            // Vector pháp tuyến tiếp tuyến tại điểm đầu
            double dx1 = Math.Sin(startAngle * Math.PI / 180);
            double dy1 = -Math.Cos(startAngle * Math.PI / 180);
            // Vector pháp tuyến tiếp tuyến tại điểm cuối
            double dx2 = -Math.Sin((startAngle + shape) * Math.PI / 180);
            double dy2 = Math.Cos((startAngle + shape) * Math.PI / 180);
            // Tính điểm đầu và cuối cung
            double x1 = cx1 + radius1 * Math.Cos(startAngle * Math.PI / 180);
            double y1 = cy1 + radius1 * Math.Sin(startAngle * Math.PI / 180);
            double x2 = cx1 + radius1 * Math.Cos((startAngle + shape) * Math.PI / 180);
            double y2 = cy1 + radius1 * Math.Sin((startAngle + shape) * Math.PI / 180);

            double x11 = cx11 + radius11 * Math.Cos(startAngle * Math.PI / 180);
            double y11 = cy11 + radius11 * Math.Sin(startAngle * Math.PI / 180);
            double x22 = cx11 + radius11 * Math.Cos((startAngle + shape) * Math.PI / 180);
            double y22 = cy11 + radius11 * Math.Sin((startAngle + shape) * Math.PI / 180);
            // Vẽ đoạn thẳng ngắn từ điểm đầu
            if (rotation_direction == 1)
            {
                DrawLine(MyCanvas, originX, originY, scale,
                         x1 + dx1 * lineLength1, y1 + dy1 * lineLength1,
                         x11 + dx1 * lineLength1, y11 + dy1 * lineLength1,
                         stroke, 2);
            }
            else
            {
                DrawLine(MyCanvas, originX, originY, scale,
                        x22 + dx2 * lineLength2,
                        y22 + dy2 * lineLength2,
                        x2 + dx2 * lineLength2,
                        y2 + dy2 * lineLength2,
                        stroke, 2);
            }

        }

        private List<double> A_swap_dimension_中子筋_B(List<double> A, int countB)
        {
            List<double> list_A = new List<double>();

            for (int i = 2; i < A.Count - 2; i++)
            {
                list_A.Add(A[i]);
            }
            int countA = list_A.Count;
            List<double> newB = new List<double>();

            // TH1: A lẻ, B lẻ
            if (countA % 2 == 1 && countB % 2 == 1)
            {
                int mid = countA / 2;
                int numSides = (countB - 1) / 2;
                newB.AddRange(list_A.Take(numSides));                      // đầu
                newB.Add(list_A[mid]);                                     // giữa
                newB.AddRange(list_A.Skip(countA - numSides));             // cuối
            }
            // TH2: A lẻ, B chẵn
            else if (countA % 2 == 1 && countB % 2 == 0)
            {
                int numSides = countB / 2;

                newB.AddRange(list_A.Take(numSides));                      // đầu
                newB.AddRange(list_A.Skip(countA - numSides));             // cuối

            }
            // TH3: A chẵn, B lẻ
            else if (countA % 2 == 0 && countB % 2 == 1)
            {
                int numSides = (countB - 1) / 2;
                int midLeft = countA / 2 - 1;

                newB.AddRange(list_A.Take(numSides));                      // đầu
                newB.Add(list_A[midLeft]);                                 // giữa trái
                newB.AddRange(list_A.Skip(countA - numSides));             // cuối

            }
            // TH4: A chẵn, B chẵn
            else if (countA % 2 == 0 && countB % 2 == 0)
            {
                int numSides = countB / 2;
                newB.AddRange(list_A.Take(numSides));                      // đầu
                newB.AddRange(list_A.Skip(countA - numSides));             // cuối

            }

            //string listBStr = newB.Count > 0 ? String.Join(", ", newB.Select(x => x.ToString("F2"))) : "Empty";
            //MessageBox.Show($"list_B after swap: [{listBStr}]");
            return newB;

        }
        private void DrawCircle1(Canvas canvas, double originX, double originY, double scale, double cx, double cy, double radius, Brush stroke)
        {
            Ellipse circle = new Ellipse
            {
                Width = radius * 2 * scale,
                Height = radius * 2 * scale,
                Stroke = stroke,
                StrokeThickness = 2
            };

            // Đặt vị trí cho hình tròn
            Canvas.SetLeft(circle, originX + (cx - radius) * scale);
            Canvas.SetTop(circle, originY + (cy - radius) * scale);

            canvas.Children.Add(circle);
        }

        private void DrawRebarLayout()
        {
            MyCanvas.Children.Clear(); // Xóa Canvas trước khi vẽ
            var data = DictGBD[beamType];

            double realWidth = 0;
            double realHeight = 0;
            double 上_ = 0;
            double 下_ = 0;
            double 左_ = 0;
            double 右_ = 0;
            double HOOP径_ = 10;
            double 主筋径_ = 10;
            double 芯筋径_ = 10;
            double 上側主筋本数_ = 0;
            double 下側主筋本数_ = 0;
            double 左側主筋本数_ = 0;
            double 右側主筋本数_ = 0;
            double 上側芯筋本数_ = 0;
            double 下側芯筋本数_ = 0;
            double 左側芯筋本数_ = 0;
            double 右側芯筋本数_ = 0;

            double フックの位置_ = 0;
            int HOOP形_ = 0;
            int 縦向き中子の数_ = 0;
            int 中子筋形 = 0;
            int 中子筋形1 = 0;
            //bool 縦向き中子_方向_ = 縦向き中子_方向.IsChecked ?? false;
            double 縦向き中子径_ = 10;
            int 横向き中子の数_ = 0;
            int 横向き中子径_ = 0;
            // Lấy giá trị từ TextBox dựa trên beamType
            switch (beamType)
            {
                case "柱脚":
                    double.TryParse(柱幅.Text, out realWidth);
                    double.TryParse(柱成.Text, out realHeight);
                    double.TryParse(上.Text, out 上_);
                    double.TryParse(下.Text, out 下_);
                    double.TryParse(左.Text, out 左_);
                    double.TryParse(右.Text, out 右_);
                    double.TryParse(HOOP径.Text, out HOOP径_);
                    double.TryParse(主筋径.Text, out 主筋径_);
                    double.TryParse(上側主筋本数.Text, out 上側主筋本数_);
                    double.TryParse(下側主筋本数.Text, out 下側主筋本数_);
                    double.TryParse(左側主筋本数.Text, out 左側主筋本数_);
                    double.TryParse(右側主筋本数.Text, out 右側主筋本数_);
                    double.TryParse(上側芯筋本数.Text, out 上側芯筋本数_);
                    double.TryParse(下側芯筋本数.Text, out 下側芯筋本数_);
                    double.TryParse(左側芯筋本数.Text, out 左側芯筋本数_);
                    double.TryParse(右側芯筋本数.Text, out 右側芯筋本数_);
                    double.TryParse(芯筋径.Text, out 芯筋径_);
                    int.TryParse(縦向き中子本数.Text, out 縦向き中子の数_);
                    int.TryParse(縦向き中子形.Text, out 中子筋形);
                    int.TryParse(横向き中子形.Text, out 中子筋形1);
                    double.TryParse(縦向き中子径.Text, out 縦向き中子径_);
                    int.TryParse(横向き中子本数.Text, out 横向き中子の数_);
                    int.TryParse(横向き中子径.Text, out 横向き中子径_);
                    double.TryParse(フックの位置.SelectedIndex.ToString(), out フックの位置_);
                    int.TryParse(HOOP形.Text, out HOOP形_);
                    break;
                case "仕口部":
                    double.TryParse(柱幅.Text, out realWidth);
                    double.TryParse(柱成.Text, out realHeight);
                    double.TryParse(上.Text, out 上_);
                    double.TryParse(下.Text, out 下_);
                    double.TryParse(左.Text, out 左_);
                    double.TryParse(右.Text, out 右_);
                    double.TryParse(HOOP径.Text, out HOOP径_);
                    double.TryParse(主筋径.Text, out 主筋径_);
                    double.TryParse(上側主筋本数.Text, out 上側主筋本数_);
                    double.TryParse(下側主筋本数.Text, out 下側主筋本数_);
                    double.TryParse(左側主筋本数.Text, out 左側主筋本数_);
                    double.TryParse(右側主筋本数.Text, out 右側主筋本数_);
                    double.TryParse(上側芯筋本数.Text, out 上側芯筋本数_);
                    double.TryParse(下側芯筋本数.Text, out 下側芯筋本数_);
                    double.TryParse(左側芯筋本数.Text, out 左側芯筋本数_);
                    double.TryParse(右側芯筋本数.Text, out 右側芯筋本数_);
                    double.TryParse(芯筋径.Text, out 芯筋径_);
                    int.TryParse(仕口部_縦向き中子本数.Text, out 縦向き中子の数_);
                    int.TryParse(仕口部_縦向き中子形.Text, out 中子筋形);
                    int.TryParse(仕口部_横向き中子形.Text, out 中子筋形1);
                    double.TryParse(仕口部_縦向き中子径.Text, out 縦向き中子径_);
                    int.TryParse(仕口部_横向き中子本数.Text, out 横向き中子の数_);
                    int.TryParse(仕口部_横向き中子径.Text, out 横向き中子径_);
                    double.TryParse(フックの位置.SelectedIndex.ToString(), out フックの位置_);
                    int.TryParse(仕口部_HOOP形.Text, out HOOP形_);
                    break;
                /////
                case "柱頭":
                    double.TryParse(柱幅1.Text, out realWidth);
                    double.TryParse(柱成1.Text, out realHeight);
                    double.TryParse(上.Text, out 上_);
                    double.TryParse(下.Text, out 下_);
                    double.TryParse(左.Text, out 左_);
                    double.TryParse(右.Text, out 右_);
                    double.TryParse(HOOP径1.Text, out HOOP径_);
                    double.TryParse(主筋径1.Text, out 主筋径_);
                    double.TryParse(上側主筋本数1.Text, out 上側主筋本数_);
                    double.TryParse(下側主筋本数1.Text, out 下側主筋本数_);
                    double.TryParse(左側主筋本数1.Text, out 左側主筋本数_);
                    double.TryParse(右側主筋本数1.Text, out 右側主筋本数_);
                    double.TryParse(上側芯筋本数1.Text, out 上側芯筋本数_);
                    double.TryParse(下側芯筋本数1.Text, out 下側芯筋本数_);
                    double.TryParse(左側芯筋本数1.Text, out 左側芯筋本数_);
                    double.TryParse(右側芯筋本数1.Text, out 右側芯筋本数_);
                    double.TryParse(芯筋径1.Text, out 芯筋径_);
                    int.TryParse(縦向き中子本数1.Text, out 縦向き中子の数_);
                    int.TryParse(縦向き中子形1.Text, out 中子筋形);
                    int.TryParse(横向き中子形1.Text, out 中子筋形1);
                    double.TryParse(縦向き中子径1.Text, out 縦向き中子径_);
                    int.TryParse(横向き中子本数1.Text, out 横向き中子の数_);
                    int.TryParse(横向き中子径1.Text, out 横向き中子径_);
                    double.TryParse(フックの位置.SelectedIndex.ToString(), out フックの位置_);
                    int.TryParse(HOOP形1.Text, out HOOP形_);
                    break;

            }

            // Scale để vừa hình trong canvas (cách đều 5% lề)
            double maxWidth = MyCanvas.ActualWidth * 0.9;
            double maxHeight = MyCanvas.ActualHeight * 0.9;

            double scaleX = maxWidth / realWidth;
            double scaleY = maxHeight / realHeight;
            double scale = Math.Min(scaleX, scaleY);

            // originX là giữa canvas ngang
            double originX = MyCanvas.ActualWidth / 2;
            double originY = (MyCanvas.ActualHeight - realHeight * scale) / 2;

            Brush strokeColor = Brushes.Red;
            // Vẽ 4 cạnh hình chữ nhật bằng tọa độ pixel
            double left = -realWidth / 2;
            double right = realWidth / 2;
            double top = 0;
            double bottom = realHeight;

            DrawLine(MyCanvas, originX, originY, scale, left, top, right, top, strokeColor);       // Top edge
            DrawLine(MyCanvas, originX, originY, scale, right, top, right, bottom, strokeColor);   // Right edge
            DrawLine(MyCanvas, originX, originY, scale, right, bottom, left, bottom, strokeColor); // Bottom edge
            DrawLine(MyCanvas, originX, originY, scale, left, bottom, left, top, strokeColor);     // Left edge
            double HOOP径_changed = 0;
            if (ToActualDiameter.ContainsKey(HOOP径_))
            {
                HOOP径_changed = ToActualDiameter[HOOP径_];
            }
            Brush strokeColor1 = Brushes.Blue;
            double cornerRadius0 = (HOOP径_ * 2) + HOOP径_changed; // Bán kính bo tròn
            // Vẽ HOOP mép ngoài trên
            DrawLine(MyCanvas, originX, originY, scale,
                left + 左_ + cornerRadius0,
                top + 上_,
                right - 右_ - cornerRadius0,
                top + 上_, strokeColor1); // Top edge
            // Vẽ HOOP mép ngoài phải
            DrawLine(MyCanvas, originX, originY, scale,
                right - 右_,
                top + 上_ + cornerRadius0,
                right - 右_,
                bottom - 下_ - cornerRadius0, strokeColor1); // Right edge
            // Vẽ HOOP mép ngoài dưới
            DrawLine(MyCanvas, originX, originY, scale,
                right - 右_ - cornerRadius0,
                bottom - 下_,
                left + 左_ + cornerRadius0,
                bottom - 下_, strokeColor1); // Bottom edge
            // Vẽ HOOP mép ngoài trái
            DrawLine(MyCanvas, originX, originY, scale,
                left + 左_,
                bottom - 下_ - cornerRadius0,
                left + 左_,
                top + 上_ + cornerRadius0, strokeColor1); // Left edge

            // Góc trên-trái ngoài
            DrawArc(MyCanvas, originX, originY, scale,
                left + 左_ + cornerRadius0,
                top + 上_ + cornerRadius0,
                cornerRadius0, 2, strokeColor1);
            // Góc trên-phải ngoài
            DrawArc(MyCanvas, originX, originY, scale,
                right - 右_ - cornerRadius0,
                top + 上_ + cornerRadius0,
                cornerRadius0, 1, strokeColor1);
            // Góc dưới-phải ngoài
            DrawArc(MyCanvas, originX, originY, scale,
                right - 右_ - cornerRadius0,
                bottom - 下_ - cornerRadius0,
                cornerRadius0, 4, strokeColor1);
            // Góc dưới-trái ngoài
            DrawArc(MyCanvas, originX, originY, scale,
                left + 左_ + cornerRadius0,
                bottom - 下_ - cornerRadius0,
                cornerRadius0, 3, strokeColor1);

            double cornerRadius = HOOP径_ * 2; // Bán kính bo tròn bằng đường kính của stirrup
            //MessageBox.Show($"{cornerRadius}");
            // Vẽ HOOP mép trong trên
            DrawLine(MyCanvas, originX, originY, scale,
                left + 左_ + HOOP径_changed + cornerRadius,
                top + 上_ + HOOP径_changed,
                right - 右_ - HOOP径_changed - cornerRadius,
                top + 上_ + HOOP径_changed, strokeColor1); // Top edge
                                                         // Vẽ HOOP mép trong phải
            DrawLine(MyCanvas, originX, originY, scale,
                right - 右_ - HOOP径_changed,
                top + 上_ + HOOP径_changed + cornerRadius,
                right - 右_ - HOOP径_changed,
                bottom - 下_ - HOOP径_changed - cornerRadius, strokeColor1); // Right edge
                                                                           // Vẽ HOOP mép trong dưới
            DrawLine(MyCanvas, originX, originY, scale,
                right - 右_ - HOOP径_changed - cornerRadius,
                bottom - 下_ - HOOP径_changed,
                left + 左_ + HOOP径_changed + cornerRadius,
                bottom - 下_ - HOOP径_changed, strokeColor1); // Bottom edge
                                                            // Vẽ HOOP mép trong trái
            DrawLine(MyCanvas, originX, originY, scale,
                left + 左_ + HOOP径_changed,
                bottom - 下_ - HOOP径_changed - cornerRadius,
                left + 左_ + HOOP径_changed,
                top + 上_ + HOOP径_changed + cornerRadius, strokeColor1); // Left edge

            // Góc trên-trái trong
            DrawArc(MyCanvas, originX, originY, scale,
                left + 左_ + HOOP径_changed + cornerRadius,
                top + 上_ + HOOP径_changed + cornerRadius,
                cornerRadius, 2, strokeColor1);
            // Góc trên-phải trong
            DrawArc(MyCanvas, originX, originY, scale,
                right - 右_ - HOOP径_changed - cornerRadius,
                top + 上_ + HOOP径_changed + cornerRadius,
                cornerRadius, 1, strokeColor1);
            // Góc dưới-phải trong
            DrawArc(MyCanvas, originX, originY, scale,
                right - 右_ - HOOP径_changed - cornerRadius,
                bottom - 下_ - HOOP径_changed - cornerRadius,
                cornerRadius, 4, strokeColor1);
            // Góc dưới-trái trong
            DrawArc(MyCanvas, originX, originY, scale,
                left + 左_ + HOOP径_changed + cornerRadius,
                bottom - 下_ - HOOP径_changed - cornerRadius,
                cornerRadius, 3, strokeColor1);

            ////////////////2025 06 04////////////////////
            ///////// Vẽ Thép chủ (主筋) ở 4 góc///////////
            double 主筋径_changed = 0;
            if (ToActualDiameter.ContainsKey(主筋径_))
            {
                主筋径_changed = ToActualDiameter[主筋径_];
            }

            // Tâm cung tròn trong phải trên
            double cx_right_top = right - 右_ - HOOP径_changed - cornerRadius; // Tâm cung tròn (x)
            double cy_right_top = top + 上_ + HOOP径_changed + cornerRadius; // Tâm cung tròn (y)
            // Tâm cung tròn trong trái trên
            double cx_left_top = left + 左_ + HOOP径_changed + cornerRadius; // Tâm cung tròn (x)
            double cy_left_top = top + 上_ + HOOP径_changed + cornerRadius; // Tâm cung tròn (y)
            // Tâm cung tròn trong phải dưới
            double cx_right_bottom = right - 右_ - HOOP径_changed - cornerRadius; // Tâm cung tròn (x)
            double cy_right_bottom = bottom - 下_ - HOOP径_changed - cornerRadius; // Tâm cung tròn (y)
            // Tâm cung tròn trong trái dưới
            double cx_left_bottom = left + 左_ + HOOP径_changed + cornerRadius; // Tâm cung tròn (x)
            double cy_left_bottom = bottom - 下_ - HOOP径_changed - cornerRadius; // Tâm cung tròn (y)

            double distance = cornerRadius - 主筋径_changed / 2; // Khoảng cách từ tâm cung đến tâm hình tròn
            double angle = 315 * Math.PI / 180; // Góc 315° để đặt tâm hình tròn bên trong stirrup

            // Tâm của hình tròn thép chủ phải trên
            double x_right_top = cx_right_top + distance * Math.Cos(angle);
            double y_right_top = cy_right_top + distance * Math.Sin(angle);
            // Tâm của hình tròn thép chủ trái trên
            double x_left_top = cx_left_top - distance * Math.Cos(angle);
            double y_left_top = cy_left_top + distance * Math.Sin(angle);
            // Tâm của hình tròn thép chủ phải dưới
            double x_right_bottom = cx_right_bottom + distance * Math.Cos(angle);
            double y_right_bottom = cy_right_bottom - distance * Math.Sin(angle);
            // Tâm của hình tròn thép chủ trái dưới
            double x_left_bottom = cx_left_bottom - distance * Math.Cos(angle);
            double y_left_bottom = cy_left_bottom - distance * Math.Sin(angle);

            List<double> rebarYPositions_top = new List<double>();
            rebarYPositions_top.Add(top);

            List<double> rebarXPositions = new List<double>();
            rebarXPositions.Add(left);


            // Vẽ hình tròn thép chủ trái trên
            DrawCircle(MyCanvas, originX, originY, scale, x_left_top, y_left_top, 主筋径_changed / 2, Brushes.Black);
            rebarXPositions.Add(x_left_top);
            rebarYPositions_top.Add(y_left_top);

            // Vẽ 上側主筋本数 
            double spacing_top = ((x_right_top) - (x_left_top)) / (上側主筋本数_ + 1);
            double y_top_rebar = top + 上_ + HOOP径_changed + 主筋径_changed / 2;


            // Lấy dữ liệu offset cho từng index

            for (int i = 0; i < 上側主筋本数_; i++)
            {
                double x = (x_left_top + spacing_top * (i + 1));


                // Lấy offset riêng cho từng index
                double offsetX = 0, offsetY = 0;
                if (data.左右Offsets.TryGetValue(i, out string xStr))
                    double.TryParse(xStr, out offsetX);

                if (data.上下Offsets.TryGetValue(i, out string yStr))
                    double.TryParse(yStr, out offsetY);
                rebarXPositions.Add(x + offsetX);
                DrawCircle(MyCanvas, originX, originY, scale, x + offsetX, y_top_rebar + offsetY, 主筋径_changed / 2, Brushes.Black);
            }

            List<double> rebarYPositions_top1 = new List<double>();
            rebarYPositions_top1.Add(top);

            // Vẽ hình tròn thép chủ phải trên
            DrawCircle(MyCanvas, originX, originY, scale, x_right_top, y_right_top, 主筋径_changed / 2, Brushes.Black);
            rebarXPositions.Add(x_right_top);
            rebarYPositions_top1.Add(y_right_top);
            rebarXPositions.Add(right);


            // Vẽ 縦向き中子
            if (縦向き中子の数_ > 0)
            {
                double 縦向き中子径_changed = ToActualDiameter.TryGetValue(縦向き中子径_, out double nakagoDia) ? nakagoDia : 10;
                double cornerRadius_中子筋径_3 = (縦向き中子径_ * 2) + 縦向き中子径_changed;
                double cornerRadius_中子筋径_2 = 縦向き中子径_ * 2;

                List<double> List_A = rebarXPositions.Skip(2).Take(rebarXPositions.Count - 4).ToList();

                int.TryParse(data.縦向き中子本数, out int selectedItem);
                int selectedIndex = 縦向き中子の数.SelectedIndex;

                for (int i = 0; i < 縦向き中子の数_; i++)
                {
                    //MessageBox.Show($"{i}");

                    int positionIndex = data.NakagoCustomPositions.ContainsKey(i) ? data.NakagoCustomPositions[i] : i;
                    if (positionIndex < 0 || positionIndex >= List_A.Count) continue;

                    Brush color = (i == selectedIndex) ? Brushes.Gold : Brushes.Black;

                    double x = List_A[positionIndex];
                    bool direction = data.NakagoDirections.ContainsKey(i) ? data.NakagoDirections[i] : false;
                    int 中子方向_1 = (int)((中子筋形 >= 1 && 中子筋形 <= 4) ? 中子筋形 : 5);
                    int 中子方向_2 = 10 + 中子方向_1;
                    int check_右 = direction ? 1 : 0;
                    int check_左 = direction ? 0 : 1;
                    int check_左_右 = direction ? 1 : -1;

                    double offsetX = 0.0;
                    DrawArc_中子筋(MyCanvas, originX, originY, scale,
                       x + offsetX, top + 上_ + HOOP径_changed + cornerRadius_中子筋径_3 - 縦向き中子径_changed,
                       cornerRadius_中子筋径_3, 中子方向_1, 縦向き中子径_, color, check_右, direction);
                    DrawArc_中子筋(MyCanvas, originX, originY, scale,
                        x + offsetX, top + 上_ + HOOP径_changed + cornerRadius_中子筋径_2,
                        cornerRadius_中子筋径_2, 中子方向_1, 縦向き中子径_, color, check_右, direction);
                    line_中子筋(MyCanvas, originX, originY, scale,
                        x + offsetX, top + 上_ + HOOP径_changed + cornerRadius_中子筋径_3 - 縦向き中子径_changed,
                        x + offsetX, top + 上_ + HOOP径_changed + cornerRadius_中子筋径_2,
                        cornerRadius_中子筋径_3, cornerRadius_中子筋径_2, 中子方向_1, 縦向き中子径_, color, check_右, direction);
                    DrawArc_中子筋(MyCanvas, originX, originY, scale,
                        x + offsetX, bottom - 下_ - HOOP径_changed - cornerRadius_中子筋径_3 + 縦向き中子径_changed,
                        cornerRadius_中子筋径_3, 中子方向_2, 縦向き中子径_, color, check_左, direction);
                    DrawArc_中子筋(MyCanvas, originX, originY, scale,
                        x + offsetX, bottom - 下_ - HOOP径_changed - cornerRadius_中子筋径_2,
                        cornerRadius_中子筋径_2, 中子方向_2, 縦向き中子径_, color, check_左, direction);
                    line_中子筋(MyCanvas, originX, originY, scale,
                        x + offsetX, bottom - 下_ - HOOP径_changed - cornerRadius_中子筋径_3 + 縦向き中子径_changed,
                        x + offsetX, bottom - 下_ - HOOP径_changed - cornerRadius_中子筋径_2,
                        cornerRadius_中子筋径_3, cornerRadius_中子筋径_2, 中子方向_2, 縦向き中子径_, color, check_左, direction);
                    DrawLine(MyCanvas, originX, originY, scale,
                        x + (check_左_右 * cornerRadius_中子筋径_3) + offsetX,
                        top + 上_ + HOOP径_changed + cornerRadius_中子筋径_3 - 縦向き中子径_changed,
                        x + (check_左_右 * cornerRadius_中子筋径_3) + offsetX,
                        bottom - 下_ - HOOP径_changed - cornerRadius_中子筋径_3 + 縦向き中子径_changed,
                        color);
                    DrawLine(MyCanvas, originX, originY, scale,
                        x + (check_左_右 * cornerRadius_中子筋径_2) + offsetX,
                        top + 上_ + HOOP径_changed + cornerRadius_中子筋径_2,
                        x + (check_左_右 * cornerRadius_中子筋径_2) + offsetX,
                        bottom - 下_ - HOOP径_changed - cornerRadius_中子筋径_2,
                        color);
                }
            }

            List<double> rebarXPositions1 = new List<double>();
            rebarXPositions1.Add(left);
            rebarXPositions1.Add(x_left_bottom);

            // Vẽ 下側主筋本数 
            double spacing_bottom = ((x_right_bottom) - (x_left_bottom)) / (下側主筋本数_ + 1);
            double y_bottom_rebar = bottom - 下_ - HOOP径_changed - 主筋径_changed / 2;

            for (int i = 0; i < 下側主筋本数_; i++)
            {
                if (i > 0)
                {
                    double x = (x_left_bottom + spacing_bottom) + (spacing_bottom * i);
                    rebarXPositions1.Add(x);
                    //MessageBox.Show($"{x}");
                }
                else
                {
                    double x = (x_left_bottom + spacing_bottom);
                    rebarXPositions1.Add(x);
                }
            }

            rebarXPositions1.Add(x_right_bottom);
            rebarXPositions1.Add(right);

            List<double> newXCoords_下側主筋本数 = AswapB(rebarXPositions, rebarXPositions1);
            List<double> list_A = rebarXPositions.Skip(2).Take(rebarXPositions.Count - 4).ToList();
            List<double> list_B = rebarXPositions1.Skip(2).Take(rebarXPositions1.Count - 4).ToList();
            int countA = list_A.Count;
            int countB = list_B.Count;
            // Draw rebars with offset
            for (int i = 0; i < newXCoords_下側主筋本数.Count; i++)
            {
                double x = newXCoords_下側主筋本数[i];
                double offsetX = 0, offsetY = 0, offsetX1 = 0, offsetY1 = 0;
                // Xác định index của thanh phía trên tương ứng dựa trên logic của AswapB
                int upperIndex = -1;


                if (countA > countB)
                {
                    // TH1: A lẻ, B lẻ
                    if (countA % 2 == 1 && countB % 2 == 1)
                    {
                        int mid = countA / 2;
                        int numSides = (countB - 1) / 2;
                        if (i < numSides)
                            upperIndex = i; // Đầu
                        else if (i == numSides)
                            upperIndex = mid; // Giữa
                        else if (i > numSides)
                            upperIndex = countA - (countB - i); // Cuối
                    }
                    // TH2: A lẻ, B chẵn
                    else if (countA % 2 == 1 && countB % 2 == 0)
                    {
                        int numSides = countB / 2;
                        if (i < numSides)
                            upperIndex = i; // Đầu
                        else
                            upperIndex = countA - (countB - i); // Cuối
                    }
                    // TH3: A chẵn, B lẻ
                    else if (countA % 2 == 0 && countB % 2 == 1)
                    {
                        int numSides = (countB - 1) / 2;
                        int midLeft = countA / 2 - 1;
                        if (i < numSides)
                            upperIndex = i; // Đầu
                        else if (i == numSides)
                            upperIndex = midLeft; // Giữa trái
                        else
                            upperIndex = countA - (countB - i); // Cuối
                    }
                    // TH4: A chẵn, B chẵn
                    else if (countA % 2 == 0 && countB % 2 == 0)
                    {
                        int numSides = countB / 2;
                        if (i < numSides)
                            upperIndex = i; // Đầu
                        else
                            upperIndex = countA - (countB - i); // Cuối
                    }
                }
                else
                {
                    // Nếu countA <= countB, giữ nguyên thứ tự
                    if (i < list_A.Count)
                        upperIndex = i;
                }


                if (data.左右Offsets.TryGetValue(i, out string xStr))
                    double.TryParse(xStr, out offsetX);

                if (data.左右1Offsets.TryGetValue(i, out string xStr1))
                    double.TryParse(xStr1, out offsetX1);

                if (data.上下1Offsets.TryGetValue(i, out string yStr1))
                    double.TryParse(yStr1, out offsetY1);

                DrawCircle(MyCanvas, originX, originY, scale, x + offsetX + offsetX1, y_bottom_rebar + offsetY + offsetY1, 主筋径_changed / 2, Brushes.Black);
            }

            // Vẽ 左側主筋本数 
            double spacing_left = ((y_left_bottom) - (y_left_top)) / (左側主筋本数_ + 1);
            double x_left_rebar = left + 左_ + HOOP径_changed + 主筋径_changed / 2;

            for (int i = 0; i < 左側主筋本数_; i++)
            {
                {
                    double y = y_left_top + spacing_left * (i + 1);


                    // Offset cho từng thanh
                    double offsetX = 0, offsetY = 0;
                    // Use per-index offset if available
                    if (data.左右2Offsets.TryGetValue(i, out string xStr))
                        double.TryParse(xStr, out offsetX);

                    if (data.上下2Offsets.TryGetValue(i, out string yStr))
                        double.TryParse(yStr, out offsetY);
                    rebarYPositions_top.Add(y + offsetY);
                    DrawCircle(MyCanvas, originX, originY, scale, x_left_rebar + offsetX, y + offsetY, 主筋径_changed / 2, Brushes.Black);
                }
            }

            // Vẽ hình tròn thép chủ trái dưới
            DrawCircle(MyCanvas, originX, originY, scale, x_left_bottom, y_left_bottom, 主筋径_changed / 2, Brushes.Black);
            rebarYPositions_top.Add(y_left_bottom);
            rebarYPositions_top.Add(bottom);

            /////// Vẽ 横向き中子//////
            if (横向き中子の数_ > 0) // Sửa thành 横向き中子の数_
            {
                double 横向き中子径_changed = ToActualDiameter.TryGetValue(横向き中子径_, out double nakagoDia) ? nakagoDia : 10;
                double cornerRadius_中子筋径_3 = (横向き中子径_ * 2) + 横向き中子径_changed;
                double cornerRadius_中子筋径_2 = 横向き中子径_ * 2;

                List<double> List_A = rebarYPositions_top.Skip(2).Take(rebarYPositions_top.Count - 4).ToList();

                int.TryParse(data.横向き中子本数, out int selectedItem);
                int selectedIndex = 横向き中子の数.SelectedIndex;
                //MessageBox.Show($"{selectedIndex}");

                for (int i = 0; i < 横向き中子の数_; i++)
                {
                    int positionIndex = data.YokogaoNakagoCustomPositions.ContainsKey(i) ? data.YokogaoNakagoCustomPositions[i] : i;
                    if (positionIndex < 0 || positionIndex >= List_A.Count) continue;

                    Brush color = (i == selectedIndex) ? Brushes.Gold : Brushes.Black;


                    double y = List_A[positionIndex];
                    bool direction = data.YokogaoNakagoDirections.ContainsKey(i) ? data.YokogaoNakagoDirections[i] : false;
                    int 中子方向_1 = (int)((中子筋形1 >= 1 && 中子筋形1 <= 4) ? 中子筋形1 : 5);
                    int 中子方向_2 = 10 + 中子方向_1;
                    int check_右 = direction ? 1 : 0;
                    int check_左 = direction ? 0 : 1;
                    int check_左_右 = direction ? -1 : 1;

                    double offsetY = 0.0;

                    DrawArc_横中子筋(MyCanvas, originX, originY, scale,
                        left + 左_ + HOOP径_changed + cornerRadius_中子筋径_3 - 横向き中子径_changed, y + offsetY,
                        cornerRadius_中子筋径_3, 中子方向_1, 横向き中子径_, color, check_右, direction);
                    DrawArc_横中子筋(MyCanvas, originX, originY, scale,
                        left + 左_ + HOOP径_changed + cornerRadius_中子筋径_2, y + offsetY,
                        cornerRadius_中子筋径_2, 中子方向_1, 横向き中子径_, color, check_右, direction);
                    line_横中子筋(MyCanvas, originX, originY, scale,
                        left + 左_ + HOOP径_changed + cornerRadius_中子筋径_3 - 横向き中子径_changed, y + offsetY,
                        left + 左_ + HOOP径_changed + cornerRadius_中子筋径_2, y + offsetY,
                        cornerRadius_中子筋径_3, cornerRadius_中子筋径_2, 中子方向_1, 横向き中子径_, color, check_右, direction);
                    DrawArc_横中子筋(MyCanvas, originX, originY, scale,
                        right - 右_ - HOOP径_changed - cornerRadius_中子筋径_3 + 横向き中子径_changed, y + offsetY,
                        cornerRadius_中子筋径_3, 中子方向_2, 横向き中子径_, color, check_左, direction);
                    DrawArc_横中子筋(MyCanvas, originX, originY, scale,
                        right - 右_ - HOOP径_changed - cornerRadius_中子筋径_2, y + offsetY,
                        cornerRadius_中子筋径_2, 中子方向_2, 横向き中子径_, color, check_左, direction);
                    line_横中子筋(MyCanvas, originX, originY, scale,
                        right - 右_ - HOOP径_changed - cornerRadius_中子筋径_3 + 横向き中子径_changed, y + offsetY,
                        right - 右_ - HOOP径_changed - cornerRadius_中子筋径_2, y + offsetY,
                        cornerRadius_中子筋径_3, cornerRadius_中子筋径_2, 中子方向_2, 横向き中子径_, color, check_左, direction);
                    DrawLine(MyCanvas, originX, originY, scale,
                         left + 左_ + HOOP径_changed + cornerRadius_中子筋径_3 - 横向き中子径_changed,
                         y + (check_左_右 * cornerRadius_中子筋径_3) + offsetY,
                         right - 右_ - HOOP径_changed - cornerRadius_中子筋径_3 + 横向き中子径_changed,
                         y + (check_左_右 * cornerRadius_中子筋径_3) + offsetY,
                         color);
                    DrawLine(MyCanvas, originX, originY, scale,
                        left + 左_ + HOOP径_changed + cornerRadius_中子筋径_2,
                        y + (check_左_右 * cornerRadius_中子筋径_2) + offsetY,
                        right - 右_ - HOOP径_changed - cornerRadius_中子筋径_2,
                        y + (check_左_右 * cornerRadius_中子筋径_2) + offsetY,
                        color);
                }
            }

            // Vẽ 右側主筋本数 
            double spacing_right = ((y_right_bottom) - (y_right_top)) / (右側主筋本数_ + 1);
            double x_right_rebar = right - 右_ - HOOP径_changed - 主筋径_changed / 2;

            for (int i = 0; i < 右側主筋本数_; i++)
            {

                double y = (y_right_top + spacing_right) + (spacing_right * i);
                rebarYPositions_top1.Add(y);

            }

            // Vẽ hình tròn thép chủ phải dưới
            DrawCircle(MyCanvas, originX, originY, scale, x_right_bottom, y_right_bottom, 主筋径_changed / 2, Brushes.Black);

            rebarYPositions_top1.Add(y_right_bottom);
            rebarYPositions_top1.Add(bottom);

            List<double> newYCoords_右側主筋本数 = AswapB(rebarYPositions_top, rebarYPositions_top1);

            // Vẽ thép với offset
            for (int i = 0; i < newYCoords_右側主筋本数.Count; i++)
            {
                double y = newYCoords_右側主筋本数[i];
                double offsetX = 0, offsetY = 0, offsetX3 = 0, offsetY3 = 0;

                // Xác định index của thanh bên trái tương ứng dựa trên logic của AswapB
                int leftIndex = -1;
                List<double> list_A1 = rebarYPositions_top.Skip(2).Take(rebarYPositions_top.Count - 4).ToList();
                List<double> list_B1 = rebarYPositions_top1.Skip(2).Take(rebarYPositions_top1.Count - 4).ToList();
                int countA1 = list_A1.Count;
                int countB1 = list_B1.Count;

                if (countA1 > countB1)
                {
                    // TH1: A lẻ, B lẻ
                    if (countA1 % 2 == 1 && countB1 % 2 == 1)
                    {
                        int mid = countA1 / 2;
                        int numSides = (countB1 - 1) / 2;
                        if (i < numSides)
                            leftIndex = i; // Đầu
                        else if (i == numSides)
                            leftIndex = mid; // Giữa
                        else if (i > numSides)
                            leftIndex = countA1 - (countB1 - i); // Cuối
                    }
                    // TH2: A lẻ, B chẵn
                    else if (countA1 % 2 == 1 && countB1 % 2 == 0)
                    {
                        int numSides = countB1 / 2;
                        if (i < numSides)
                            leftIndex = i; // Đầu
                        else
                            leftIndex = countA1 - (countB1 - i); // Cuối
                    }
                    // TH3: A chẵn, B lẻ
                    else if (countA1 % 2 == 0 && countB1 % 2 == 1)
                    {
                        int numSides = (countB1 - 1) / 2;
                        int midLeft = countA1 / 2 - 1;
                        if (i < numSides)
                            leftIndex = i; // Đầu
                        else if (i == numSides)
                            leftIndex = midLeft; // Giữa trái
                        else
                            leftIndex = countA1 - (countB1 - i); // Cuối
                    }
                    // TH4: A chẵn, B chẵn
                    else if (countA1 % 2 == 0 && countB1 % 2 == 0)
                    {
                        int numSides = countB1 / 2;
                        if (i < numSides)
                            leftIndex = i; // Đầu
                        else
                            leftIndex = countA1 - (countB1 - i); // Cuối
                    }
                }
                else
                {
                    // Nếu countA <= countB, giữ nguyên thứ tự
                    if (i < list_A1.Count)
                        leftIndex = i;
                }

                if (data.上下2Offsets.TryGetValue(i, out string yStr))
                    double.TryParse(yStr, out offsetY);

                if (data.左右3Offsets.TryGetValue(i, out string xStr3))
                    double.TryParse(xStr3, out offsetX3);

                if (data.上下3Offsets.TryGetValue(i, out string yStr3))
                    double.TryParse(yStr3, out offsetY3);

                DrawCircle(MyCanvas, originX, originY, scale, x_right_rebar + offsetX + offsetX3, y + offsetY + offsetY3, 主筋径_changed / 2, Brushes.Black);
            }


            // Điều chỉnh tọa độ cho thanh phía trên với offset
            List<double> adjustedXPositions_top = new List<double>();
            adjustedXPositions_top.Add(rebarXPositions[0]); // Tọa độ biên trái
            adjustedXPositions_top.Add(rebarXPositions[1]); // Thanh góc trái trên
            for (int i = 0; i < 上側主筋本数_; i++)
            {
                double x = rebarXPositions[i + 2];
                double offsetX = 0;
                adjustedXPositions_top.Add(x + offsetX);
            }
            adjustedXPositions_top.Add(rebarXPositions[rebarXPositions.Count - 2]); // Thanh góc phải trên
            adjustedXPositions_top.Add(rebarXPositions[rebarXPositions.Count - 1]); // Tọa độ biên phải

            // Dim khoảng cách thép chủ phía trên
            DrawRebarDimensions(MyCanvas, originX, originY, scale, adjustedXPositions_top, top, 主筋径_changed, 0, -15, 0, 0, Brushes.Green);

            // Điều chỉnh tọa độ cho thanh phía dưới với offset
            List<double> newXCoords_下側主筋本数_dimension = Aswap_dimension_lineB(rebarXPositions, rebarXPositions1);
            List<double> adjustedXPositions_bottom = new List<double>();
            adjustedXPositions_bottom.Add(newXCoords_下側主筋本数_dimension[0]); // Tọa độ biên trái
            adjustedXPositions_bottom.Add(newXCoords_下側主筋本数_dimension[1]); // Thanh góc trái dưới


            for (int i = 0; i < countB; i++)
            {
                double x = newXCoords_下側主筋本数_dimension[i + 2];
                double offsetX = 0, offsetX1 = 0;

                // Xác định index của thanh phía trên tương ứng
                int upperIndex = -1;
                if (countA > countB)
                {
                    // TH1: A lẻ, B lẻ
                    if (countA % 2 == 1 && countB % 2 == 1)
                    {
                        int mid = countA / 2;
                        int numSides = (countB - 1) / 2;
                        if (i < numSides)
                            upperIndex = i; // Đầu
                        else if (i == numSides)
                            upperIndex = mid; // Giữa
                        else
                            upperIndex = countA - (countB - i); // Cuối
                    }
                    // TH2: A lẻ, B chẵn
                    else if (countA % 2 == 1 && countB % 2 == 0)
                    {
                        int numSides = countB / 2;
                        if (i < numSides)
                            upperIndex = i; // Đầu
                        else
                            upperIndex = countA - (countB - i); // Cuối
                    }
                    // TH3: A chẵn, B lẻ
                    else if (countA % 2 == 0 && countB % 2 == 1)
                    {
                        int numSides = (countB - 1) / 2;
                        int midLeft = countA / 2 - 1;
                        if (i < numSides)
                            upperIndex = i; // Đầu
                        else if (i == numSides)
                            upperIndex = midLeft; // Giữa trái
                        else
                            upperIndex = countA - (countB - i); // Cuối
                    }
                    // TH4: A chẵn, B chẵn
                    else if (countA % 2 == 0 && countB % 2 == 0)
                    {
                        int numSides = countB / 2;
                        if (i < numSides)
                            upperIndex = i; // Đầu
                        else
                            upperIndex = countA - (countB - i); // Cuối
                    }
                }
                else
                {
                    upperIndex = i; // Giữ nguyên thứ tự
                }
                if (data.左右Offsets.TryGetValue(upperIndex, out string xStr))
                    double.TryParse(xStr, out offsetX);
                if (data.左右1Offsets.TryGetValue(i, out string xStr1))
                    double.TryParse(xStr1, out offsetX1);

                adjustedXPositions_bottom.Add(x + offsetX + offsetX1);
            }
            adjustedXPositions_bottom.Add(newXCoords_下側主筋本数_dimension[newXCoords_下側主筋本数_dimension.Count - 2]); // Thanh góc phải dưới
            adjustedXPositions_bottom.Add(newXCoords_下側主筋本数_dimension[newXCoords_下側主筋本数_dimension.Count - 1]); // Tọa độ biên phải

            // Dim khoảng cách thép chủ phía dưới
            DrawRebarDimensions(MyCanvas, originX, originY, scale, adjustedXPositions_bottom, bottom, 主筋径_changed, 0, +15, 0, 0, Brushes.Green);

            // Điều chỉnh tọa độ cho thanh bên trái với offset
            List<double> adjustedYPositions_left = new List<double>();
            adjustedYPositions_left.Add(rebarYPositions_top[0]); // Tọa độ biên trên
            adjustedYPositions_left.Add(rebarYPositions_top[1]); // Thanh góc trái trên
            for (int i = 0; i < 左側主筋本数_; i++)
            {
                double y = rebarYPositions_top[i + 2];
                double offsetY = 0;
                if (data.上下2Offsets.TryGetValue(i, out string yStr))
                    double.TryParse(yStr, out offsetY);
                adjustedYPositions_left.Add(y + offsetY);
            }
            adjustedYPositions_left.Add(rebarYPositions_top[rebarYPositions_top.Count - 2]); // Thanh góc trái dưới
            adjustedYPositions_left.Add(rebarYPositions_top[rebarYPositions_top.Count - 1]); // Tọa độ biên dưới

            // Dim khoảng cách thép chủ bên trái
            DrawRebarDimensionsVertical(MyCanvas, originX, originY, scale, left, adjustedYPositions_left, 主筋径_changed, -15, 0, 0, 0, Brushes.Green);

            // Điều chỉnh tọa độ cho thanh bên phải với offset
            List<double> newYCoords_右側主筋本数_dimension = Aswap_dimension_lineB(rebarYPositions_top, rebarYPositions_top1);
            List<double> adjustedYPositions_right = new List<double>();
            adjustedYPositions_right.Add(newYCoords_右側主筋本数_dimension[0]); // Tọa độ biên trên
            adjustedYPositions_right.Add(newYCoords_右側主筋本数_dimension[1]); // Thanh góc phải trên
            List<double> list_A_y = rebarYPositions_top.Skip(2).Take(rebarYPositions_top.Count - 4).ToList();
            List<double> list_B_y = rebarYPositions_top1.Skip(2).Take(rebarYPositions_top1.Count - 4).ToList();
            int countA_y = list_A_y.Count;
            int countB_y = list_B_y.Count;

            for (int i = 0; i < countB_y; i++)
            {
                double y = newYCoords_右側主筋本数_dimension[i + 2];
                double offsetY = 0, offsetY3 = 0;

                // Xác định index của thanh bên trái tương ứng
                int leftIndex = -1;
                if (countA_y > countB_y)
                {
                    // TH1: A lẻ, B lẻ
                    if (countA_y % 2 == 1 && countB_y % 2 == 1)
                    {
                        int mid = countA_y / 2;
                        int numSides = (countB_y - 1) / 2;
                        if (i < numSides)
                            leftIndex = i; // Đầu
                        else if (i == numSides)
                            leftIndex = mid; // Giữa
                        else
                            leftIndex = countA_y - (countB_y - i); // Cuối
                    }
                    // TH2: A lẻ, B chẵn
                    else if (countA_y % 2 == 1 && countB_y % 2 == 0)
                    {
                        int numSides = countB_y / 2;
                        if (i < numSides)
                            leftIndex = i; // Đầu
                        else
                            leftIndex = countA_y - (countB_y - i); // Cuối
                    }
                    // TH3: A chẵn, B lẻ
                    else if (countA_y % 2 == 0 && countB_y % 2 == 1)
                    {
                        int numSides = (countB_y - 1) / 2;
                        int midLeft = countA_y / 2 - 1;
                        if (i < numSides)
                            leftIndex = i; // Đầu
                        else if (i == numSides)
                            leftIndex = midLeft; // Giữa trái
                        else
                            leftIndex = countA_y - (countB_y - i); // Cuối
                    }
                    // TH4: A chẵn, B chẵn
                    else if (countA_y % 2 == 0 && countB_y % 2 == 0)
                    {
                        int numSides = countB_y / 2;
                        if (i < numSides)
                            leftIndex = i; // Đầu
                        else
                            leftIndex = countA_y - (countB_y - i); // Cuối
                    }
                }
                else
                {
                    leftIndex = i; // Giữ nguyên thứ tự
                }
                if (data.上下2Offsets.TryGetValue(leftIndex, out string yStr))
                    double.TryParse(yStr, out offsetY);
                if (data.上下3Offsets.TryGetValue(i, out string yStr3))
                    double.TryParse(yStr3, out offsetY3);
                adjustedYPositions_right.Add(y + offsetY + offsetY3);
            }
            adjustedYPositions_right.Add(newYCoords_右側主筋本数_dimension[newYCoords_右側主筋本数_dimension.Count - 2]); // Thanh góc phải dưới
            adjustedYPositions_right.Add(newYCoords_右側主筋本数_dimension[newYCoords_右側主筋本数_dimension.Count - 1]); // Tọa độ biên dưới

            // Dim khoảng cách thép chủ bên phải
            DrawRebarDimensionsVertical(MyCanvas, originX, originY, scale, right, adjustedYPositions_right, 主筋径_changed, +15, 0, 0, 0, Brushes.Green);
            // Dim kích thước chiều rộng thật
            DrawDimension(MyCanvas, originX, originY, scale, left, ((top - 100) / scale), right, ((top - 100) / scale), $"{realWidth}", 0, 0, 0, 0, Brushes.Green);
            // Dim kích thước chiều cao thật
            DrawDimension(MyCanvas, originX, originY, scale, (left + (-150) / scale), top, (left + (-150) / scale), bottom, $"{realHeight}", 0, 0, 0, 0, Brushes.Green);
            // Dim kích thước chiều rộng trừ bê tông bảo vệ
            DrawDimension(MyCanvas, originX, originY, scale, (left + 左_), ((top - 50) / scale), (right - 右_), ((top - 50) / scale), $"{realWidth - 右_ - 左_}", 0, 0, 0, 0, Brushes.Green);
            // Dim kích thước chiều cao trừ bê tông bảo vệ
            DrawDimension(MyCanvas, originX, originY, scale, (left + (-80) / scale), (top + 上_), (left + (-80) / scale), (bottom - 下_), $"{realHeight - 上_ - 下_}", 0, 0, 0, 0, Brushes.Green);

            // Dim lớp bê tông bảo vệ trên
            DrawDimension(MyCanvas, originX, originY, scale, 0, top, 0, (top + 上_), $"{上_}", 0, 0, 0, 0, Brushes.Black);
            // Dim lớp bê tông bảo vệ dưới
            DrawDimension(MyCanvas, originX, originY, scale, 0, (bottom - 下_), 0, bottom, $"{下_}", 0, 0, 0, 0, Brushes.Black);
            // Dim lớp bê tông bảo vệ trái
            DrawDimension(MyCanvas, originX, originY, scale, left, (bottom / 2), (left + 左_), (bottom / 2), $"{左_}", 0, 0, 0, 0, Brushes.Black);
            // Dim lớp bê tông bảo vệ phải
            DrawDimension(MyCanvas, originX, originY, scale, (right - 右_), (bottom / 2), (right), (bottom / 2), $"{右_}", 0, 0, 0, 0, Brushes.Black);

            // Dim đường kính HOOP
            DrawDimension(MyCanvas, originX, originY, scale, (right - 右_ - HOOP径_changed), (bottom / 2.2), (right - 右_), (bottom / 2.2), $"{HOOP径_changed}", 0, 0, 0, 0, Brushes.Black);

            //////   DrawArc_HOOP   //////           

            int HOOP形1_ = 0;
            int HOOP形2_ = 0;
            int HOOP形3_ = 0;
            int HOOP形4_ = 0;
            /////////////////////
            ///// Lấy giá trị HOOP形Text theo beamType
            if (HOOP形_ == 1)
            {
                HOOP形1_ = 1;
                HOOP形2_ = 5;
                HOOP形3_ = 9;
                HOOP形4_ = 13;
            }
            else if (HOOP形_ == 2)
            {
                HOOP形1_ = 2;
                HOOP形2_ = 6;
                HOOP形3_ = 10;
                HOOP形4_ = 14;

            }
            else if (HOOP形_ == 3)
            {
                double y_上_下 = (bottom + 上_ - 下_);
                if (フックの位置_ == 0)
                {
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        left + 左_ + HOOP径_changed / 2,
                        y_上_下 / 2,
                        HOOP径_changed, strokeColor1);
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        left + 左_ + HOOP径_changed / 2,
                        y_上_下 / 2,
                        HOOP径_changed / 2, strokeColor1);
                }
                else if (フックの位置_ == 2)
                {
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        right - 右_ - HOOP径_changed / 2,
                        y_上_下 / 2,
                        HOOP径_changed, strokeColor1);
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        right - 右_ - HOOP径_changed / 2,
                        y_上_下 / 2,
                        HOOP径_changed / 2, strokeColor1);
                }

                else if (フックの位置_ == 1)
                {
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        0,
                        bottom - 下_ - HOOP径_changed / 2,
                        HOOP径_changed, strokeColor1);
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        0,
                        bottom - 下_ - HOOP径_changed / 2,
                        HOOP径_changed / 2, strokeColor1);
                }
                else if (フックの位置_ == 3)
                {
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        0,
                        上_ + HOOP径_changed / 2,
                        HOOP径_changed, strokeColor1);
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        0,
                        上_ + HOOP径_changed / 2,
                        HOOP径_changed / 2, strokeColor1);
                }


            }


            if (フックの位置_ == 0 && (HOOP形_ == 1 || HOOP形_ == 2))
            {
                // Góc trên-phải ngoài
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                right - 右_ - cornerRadius0,
                top + 上_ + cornerRadius0,
                cornerRadius0, HOOP形1_, HOOP径_, strokeColor1);
                //MessageBox.Show($"{HOOP径_changed}");
                // Góc trên-phải trong
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    right - 右_ - HOOP径_changed - cornerRadius,
                    top + 上_ + HOOP径_changed + cornerRadius,
                    cornerRadius, HOOP形1_, HOOP径_, strokeColor1);

                // 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn.
                line_HOOP(MyCanvas, originX, originY, scale,
                    right - 右_ - cornerRadius0,
                    top + 上_ + cornerRadius0,
                    right - 右_ - HOOP径_changed - cornerRadius,
                    top + 上_ + HOOP径_changed + cornerRadius,
                    cornerRadius0, cornerRadius, HOOP形1_, HOOP径_, strokeColor1);
            }
            else if (フックの位置_ == 1 && (HOOP形_ == 1 || HOOP形_ == 2))
            {
                // Góc trên-trái ngoài
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                left + 左_ + cornerRadius0,
                    top + 上_ + cornerRadius0,
                cornerRadius0, HOOP形2_, HOOP径_, strokeColor1);
                // Góc trên-trái trong
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                        left + 左_ + HOOP径_changed + cornerRadius,
                    top + 上_ + HOOP径_changed + cornerRadius,
                    cornerRadius, HOOP形2_, HOOP径_, strokeColor1);
                // 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn.
                line_HOOP(MyCanvas, originX, originY, scale,
                    left + 左_ + cornerRadius0,
                    top + 上_ + cornerRadius0,
                    left + 左_ + HOOP径_changed + cornerRadius,
                    top + 上_ + HOOP径_changed + cornerRadius,
                    cornerRadius0, cornerRadius, HOOP形2_, HOOP径_, strokeColor1);
            }
            else if (フックの位置_ == 2 && (HOOP形_ == 1 || HOOP形_ == 2))
            {
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                left + 左_ + cornerRadius0,
                    bottom - 上_ - cornerRadius0,
                cornerRadius0, HOOP形3_, HOOP径_, strokeColor1);
                // Góc trên-trái trong
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                        left + 左_ + HOOP径_changed + cornerRadius,
                    bottom - 上_ - HOOP径_changed - cornerRadius,
                    cornerRadius, HOOP形3_, HOOP径_, strokeColor1);
                // 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn.
                line_HOOP(MyCanvas, originX, originY, scale,
                    left + 左_ + cornerRadius0,
                    bottom - 上_ - cornerRadius0,
                    left + 左_ + HOOP径_changed + cornerRadius,
                    bottom - 上_ - HOOP径_changed - cornerRadius,
                    cornerRadius0, cornerRadius, HOOP形3_, HOOP径_, strokeColor1);
            }
            else if (フックの位置_ == 3 && (HOOP形_ == 1 || HOOP形_ == 2))
            {
                // Góc dưới-phải ngoài
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                right - 右_ - cornerRadius0,
                bottom - 下_ - cornerRadius0,
                cornerRadius0, HOOP形4_, HOOP径_, strokeColor1);
                // Góc dưới-phải trong
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    right - 右_ - HOOP径_changed - cornerRadius,
                    bottom - 下_ - HOOP径_changed - cornerRadius,
                    cornerRadius, HOOP形4_, HOOP径_, strokeColor1);
                // 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn.
                line_HOOP(MyCanvas, originX, originY, scale,
                    right - 右_ - cornerRadius0,
                    bottom - 下_ - cornerRadius0,
                    right - 右_ - HOOP径_changed - cornerRadius,
                    bottom - 下_ - HOOP径_changed - cornerRadius,
                    cornerRadius0, cornerRadius, HOOP形4_, HOOP径_, strokeColor1);
            }
            //////////////////Vẽ Thép 中子  (2025 06 10)/////////////////////////



            ////////////////2025 06 05////////////////////
            ///////// Vẽ Thép (芯筋径) ở 4 góc///////////
            // Determine which TextBoxes to use based on beamType
            string topText, bottomText, leftText, rightText;
            switch (beamType)
            {
                case "柱頭":
                    topText = 上側芯筋本数1.Text;
                    bottomText = 下側芯筋本数1.Text;
                    leftText = 左側芯筋本数1.Text;
                    rightText = 右側芯筋本数1.Text;
                    break;
                default: // "柱脚" or "仕口部"
                    topText = 上側芯筋本数.Text;
                    bottomText = 下側芯筋本数.Text;
                    leftText = 左側芯筋本数.Text;
                    rightText = 右側芯筋本数.Text;
                    break;
            }

            double topCount = 0, bottomCount = 0, leftCount = 0, rightCount = 0;
            double.TryParse(topText, out topCount);
            double.TryParse(bottomText, out bottomCount);
            double.TryParse(leftText, out leftCount);
            double.TryParse(rightText, out rightCount);

            // Chỉ vẽ khi giá trị lớn hơn 0
            if (topCount > 0 || bottomCount > 0 || leftCount > 0 || rightCount > 0)
            {
                double 芯筋径_changed = 0;
                if (ToActualDiameter.ContainsKey(芯筋径_))
                {
                    芯筋径_changed = ToActualDiameter[芯筋径_];
                }
                // Tâm thép 芯筋 trái trên
                double x_shinkin_left_top = -((x_right_rebar) - (x_left_rebar)) / 4;
                double y_shinkin_left_top = (((y_bottom_rebar) - (y_top_rebar)) / 4) + y_top_rebar;
                DrawCircle(MyCanvas, originX, originY, scale, x_shinkin_left_top, y_shinkin_left_top, 芯筋径_changed / 2, Brushes.Black);

                // Tâm thép 芯筋 phải trên
                double x_shinkin_right_top = ((x_right_rebar) - (x_left_rebar)) / 4;
                double y_shinkin_right_top = (((y_bottom_rebar) - (y_top_rebar)) / 4) + y_top_rebar;
                DrawCircle(MyCanvas, originX, originY, scale, x_shinkin_right_top, y_shinkin_right_top, 芯筋径_changed / 2, Brushes.Black);

                // Tâm thép 芯筋 trái dưới
                double x_shinkin_left_bottom = x_shinkin_left_top;
                double y_shinkin_left_bottom = bottom - y_shinkin_left_top;
                DrawCircle(MyCanvas, originX, originY, scale, x_shinkin_left_bottom, y_shinkin_left_bottom, 芯筋径_changed / 2, Brushes.Black);

                // Tâm thép 芯筋 phải dưới
                double x_shinkin_right_bottom = x_shinkin_right_top;
                double y_shinkin_right_bottom = bottom - y_shinkin_right_top;
                DrawCircle(MyCanvas, originX, originY, scale, x_shinkin_right_bottom, y_shinkin_right_bottom, 芯筋径_changed / 2, Brushes.Black);

                // Vẽ 上側芯筋本数 (Top center rebars with offset)
                double spacing_shinkin_top = ((x_shinkin_right_top) - (x_shinkin_left_top)) / (topCount + 1);
                for (int i = 0; i < topCount; i++)
                {
                    double x = x_shinkin_left_top + spacing_shinkin_top * (i + 1);
                    double offsetX = 0, offsetY = 0;
                    if (data.左右4Offsets.TryGetValue(i, out string xStr))
                        double.TryParse(xStr, out offsetX);
                    if (data.上下4Offsets.TryGetValue(i, out string yStr))
                        double.TryParse(yStr, out offsetY);
                    DrawCircle(MyCanvas, originX, originY, scale, x + offsetX, y_shinkin_left_top + offsetY, 芯筋径_changed / 2, Brushes.Black);
                }

                // Vẽ 下側芯筋本数 (Bottom center rebars with offset)
                double spacing_shinkin_bottom = ((x_shinkin_right_bottom) - (x_shinkin_left_bottom)) / (bottomCount + 1);
                for (int i = 0; i < bottomCount; i++)
                {
                    double x = x_shinkin_left_bottom + spacing_shinkin_bottom * (i + 1);
                    double offsetX = 0, offsetY = 0;
                    if (data.左右5Offsets.TryGetValue(i, out string xStr))
                        double.TryParse(xStr, out offsetX);
                    if (data.上下5Offsets.TryGetValue(i, out string yStr))
                        double.TryParse(yStr, out offsetY);
                    DrawCircle(MyCanvas, originX, originY, scale, x + offsetX, y_shinkin_left_bottom + offsetY, 芯筋径_changed / 2, Brushes.Black);
                }

                // Vẽ 左側芯筋本数 (Left center rebars with offset)
                double spacing_shinkin_left = ((y_shinkin_left_bottom) - (y_shinkin_left_top)) / (leftCount + 1);
                for (int i = 0; i < leftCount; i++)
                {
                    double y = y_shinkin_left_top + spacing_shinkin_left * (i + 1);
                    double offsetX = 0, offsetY = 0;
                    if (data.左右6Offsets.TryGetValue(i, out string xStr))
                        double.TryParse(xStr, out offsetX);
                    if (data.上下6Offsets.TryGetValue(i, out string yStr))
                        double.TryParse(yStr, out offsetY);
                    DrawCircle(MyCanvas, originX, originY, scale, x_shinkin_left_top + offsetX, y + offsetY, 芯筋径_changed / 2, Brushes.Black);
                }
                // Vẽ 右側芯筋本数 (Right center rebars with offset)
                double spacing_shinkin_right = ((y_shinkin_right_bottom) - (y_shinkin_right_top)) / (rightCount + 1);
                for (int i = 0; i < rightCount; i++)
                {
                    double y = y_shinkin_right_top + spacing_shinkin_right * (i + 1);
                    double offsetX = 0, offsetY = 0;
                    if (data.左右7Offsets.TryGetValue(i, out string xStr))
                        double.TryParse(xStr, out offsetX);
                    if (data.上下7Offsets.TryGetValue(i, out string yStr))
                        double.TryParse(yStr, out offsetY);
                    DrawCircle(MyCanvas, originX, originY, scale, x_shinkin_right_top + offsetX, y + offsetY, 芯筋径_changed / 2, Brushes.Black);
                }
            }

        }
        /// ////////// 2025 06 03 ////////////////
        private void フックの位置_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            DrawRebarLayout();
        }

        private void 縦向き中子_方向_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var data = DictGBD[beamType];
            string countString = data.縦向き中子本数;
            int nakagoIndex = data.Valid縦向き中子本数Values.IndexOf(countString);

            data.NakagoDirections[nakagoIndex] = true;
            DrawRebarLayout();

        }
        private void 縦向き中子_方向_Unchecked(object sender, RoutedEventArgs e)
        {

            if (isInitializing) return;

            var data = DictGBD[beamType];
            string countString = data.縦向き中子本数;
            int nakagoIndex = data.Valid縦向き中子本数Values.IndexOf(countString);

            data.NakagoDirections[nakagoIndex] = false;
            DrawRebarLayout();
        }

        private void 横向き中子_方向_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;

            var data = DictGBD[beamType];
            string countString = data.横向き中子本数;
            int nakagoIndex = data.Valid横向き中子本数Values.IndexOf(countString);

            data.YokogaoNakagoDirections[nakagoIndex] = true;
            DrawRebarLayout();

        }
        private void 横向き中子_方向_Unchecked(object sender, RoutedEventArgs e)
        {

            if (isInitializing) return;

            var data = DictGBD[beamType];
            string countString = data.横向き中子本数;
            int nakagoIndex = data.Valid横向き中子本数Values.IndexOf(countString);

            data.YokogaoNakagoDirections[nakagoIndex] = false;
            DrawRebarLayout();

        }


        private void 縦向き中子の数_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;

            var data = DictGBD[beamType];

            string countString = data.縦向き中子本数;
            int nakagoIndex = data.Valid縦向き中子本数Values.IndexOf(countString);

            var positionItems = data.Valid縦向き中子の位置Values;

            string selectedPosition = null;
            int selectedIndex = 0;

            if (nakagoIndex >= 0 &&
                data.NakagoCustomPositions.TryGetValue(nakagoIndex, out int savedIndex) &&
                savedIndex >= 0 && savedIndex < positionItems.Count)
            {
                selectedIndex = savedIndex;
                selectedPosition = positionItems[savedIndex];
            }
            else if (positionItems.Count > 0)
            {
                selectedIndex = 0;
                selectedPosition = positionItems[0];
            }

            // TẠM THỜI NGẮT SỰ KIỆN
            suppressNakagoSelectionChanged = true;
            data.縦向き中子位置 = selectedPosition;
            suppressNakagoSelectionChanged = false;

            // 2025 07 25
            data.縦向き中子_方向 = data.NakagoDirections[nakagoIndex];
            DrawRebarLayout();
        }

        private void 縦向き中子の位置_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || suppressNakagoSelectionChanged) return;

            var data = DictGBD[beamType];

            string countString = data.縦向き中子本数;
            int nakagoIndex = data.Valid縦向き中子本数Values.IndexOf(countString);

            string selectedPosition = data.縦向き中子位置;
            int selectedIndex = data.Valid縦向き中子の位置Values.IndexOf(selectedPosition);

            data.NakagoCustomPositions[nakagoIndex] = selectedIndex;
            data.NakagoDirections[nakagoIndex] = data.縦向き中子_方向;

            DrawRebarLayout();
        }


        private void 横向き中子の数_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;

            var data = DictGBD[beamType];

            string countString = data.横向き中子本数;
            int nakagoIndex = data.Valid横向き中子本数Values.IndexOf(countString);

            var positionItems = data.Valid横向き中子の位置Values;

            string selectedPosition = null;
            int selectedIndex = 0;

            if (nakagoIndex >= 0 &&
                data.YokogaoNakagoCustomPositions.TryGetValue(nakagoIndex, out int savedIndex) &&
                savedIndex >= 0 && savedIndex < positionItems.Count)
            {
                selectedIndex = savedIndex;
                selectedPosition = positionItems[savedIndex];
            }
            else if (positionItems.Count > 0)
            {
                selectedIndex = 0;
                selectedPosition = positionItems[0];
            }

            // TẠM THỜI NGẮT SỰ KIỆN
            suppressNakagoSelectionChanged = true;
            data.横向き中子位置 = selectedPosition;
            suppressNakagoSelectionChanged = false;

            // 2025 07 25
            data.横向き中子_方向 = data.YokogaoNakagoDirections[nakagoIndex];
            DrawRebarLayout();
        }

        private void 横向き中子の位置_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || suppressNakagoSelectionChanged) return;

            var data = DictGBD[beamType];

            string countString = data.横向き中子本数;
            int nakagoIndex = data.Valid横向き中子本数Values.IndexOf(countString);

            string selectedPosition = data.横向き中子位置;
            int selectedIndex = data.Valid横向き中子の位置Values.IndexOf(selectedPosition);

            data.YokogaoNakagoCustomPositions[nakagoIndex] = selectedIndex;
            data.YokogaoNakagoDirections[nakagoIndex] = data.横向き中子_方向;

            DrawRebarLayout();
        }

        private void 上側主筋本数2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.上側主筋本数2;


            int index = data.Valid上側主筋本数Values.IndexOf(countString);

            data.左右 = (index >= 0 && data.左右Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.上下 = (index >= 0 && data.上下Offsets.TryGetValue(index, out string y)) ? y : "0";

            DrawRebarLayout();
        }

        private void 下側主筋本数2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.下側主筋本数2;
            int index = data.Valid下側主筋本数Values.IndexOf(countString);
            data.左右1 = (index >= 0 && data.左右1Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.上下1 = (index >= 0 && data.上下1Offsets.TryGetValue(index, out string y)) ? y : "0";
            DrawRebarLayout();

        }

        private void 左側主筋本数2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.左側主筋本数2;
            int index = data.Valid左側主筋本数Values.IndexOf(countString);
            data.左右2 = (index >= 0 && data.左右2Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.上下2 = (index >= 0 && data.上下2Offsets.TryGetValue(index, out string y)) ? y : "0";
            DrawRebarLayout();

        }

        private void 右側主筋本数2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.右側主筋本数2;
            int index = data.Valid右側主筋本数Values.IndexOf(countString);
            data.左右3 = (index >= 0 && data.左右3Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.上下3 = (index >= 0 && data.上下3Offsets.TryGetValue(index, out string y)) ? y : "0";
            DrawRebarLayout();

        }
        private void 上側芯筋本数2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.上側芯筋本数2;
            int index = data.Valid上側芯筋本数Values.IndexOf(countString);
            data.左右4 = (index >= 0 && data.左右4Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.上下4 = (index >= 0 && data.上下4Offsets.TryGetValue(index, out string y)) ? y : "0";
            DrawRebarLayout();

        }
        private void 下側芯筋本数2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.下側芯筋本数2;
            int index = data.Valid下側芯筋本数Values.IndexOf(countString);
            data.左右5 = (index >= 0 && data.左右5Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.上下5 = (index >= 0 && data.上下5Offsets.TryGetValue(index, out string y)) ? y : "0";
            DrawRebarLayout();
        }
        private void 左側芯筋本数2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.左側芯筋本数2;
            int index = data.Valid左側芯筋本数Values.IndexOf(countString);
            data.左右6 = (index >= 0 && data.左右6Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.上下6 = (index >= 0 && data.上下6Offsets.TryGetValue(index, out string y)) ? y : "0";
            DrawRebarLayout();
        }
        private void 右側芯筋本数2_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.右側芯筋本数2;
            int index = data.Valid右側芯筋本数Values.IndexOf(countString);
            data.左右7 = (index >= 0 && data.左右7Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.上下7 = (index >= 0 && data.上下7Offsets.TryGetValue(index, out string y)) ? y : "0";
            DrawRebarLayout();
        }

        private void 左右_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.上側主筋本数2;
            int index = data.Valid上側主筋本数Values.IndexOf(countString);

            data.左右Offsets[index] = data.左右;
            DrawRebarLayout();
        }

        private void 上下_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.上側主筋本数2;
            int index = data.Valid上側主筋本数Values.IndexOf(countString);

            data.上下Offsets[index] = data.上下;
            DrawRebarLayout();

        }

        private void 左右1_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.下側主筋本数2;
            int index = data.Valid下側主筋本数Values.IndexOf(countString);
            data.左右1Offsets[index] = data.左右1;
            DrawRebarLayout();
        }

        private void 上下1_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.下側主筋本数2;
            int index = data.Valid下側主筋本数Values.IndexOf(countString);
            data.上下1Offsets[index] = data.上下1;
            DrawRebarLayout();

        }

        private void 左右2_TextChanged(object sender, TextChangedEventArgs e)
        {

            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.左側主筋本数2;
            int index = data.Valid左側主筋本数Values.IndexOf(countString);
            data.左右2Offsets[index] = data.左右2;
            DrawRebarLayout();
        }

        private void 上下2_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.左側主筋本数2;
            int index = data.Valid左側主筋本数Values.IndexOf(countString);
            data.上下2Offsets[index] = data.上下2;
            DrawRebarLayout();

        }

        private void 左右3_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.右側主筋本数2;
            int index = data.Valid右側主筋本数Values.IndexOf(countString);
            data.左右3Offsets[index] = data.左右3;
            DrawRebarLayout();

        }

        private void 上下3_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.右側主筋本数2;
            int index = data.Valid右側主筋本数Values.IndexOf(countString);
            data.上下3Offsets[index] = data.上下3;
            DrawRebarLayout();

        }
        private void 左右4_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.上側芯筋本数2;
            int index = data.Valid上側芯筋本数Values.IndexOf(countString);
            data.左右4Offsets[index] = data.左右4;
            DrawRebarLayout();
        }
        private void 上下4_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.上側芯筋本数2;
            int index = data.Valid上側芯筋本数Values.IndexOf(countString);
            data.上下4Offsets[index] = data.上下4;
            DrawRebarLayout();
        }
        private void 左右5_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.下側芯筋本数2;
            int index = data.Valid下側芯筋本数Values.IndexOf(countString);
            data.左右5Offsets[index] = data.左右5;
            DrawRebarLayout();
        }
        private void 上下5_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.下側芯筋本数2;
            int index = data.Valid下側芯筋本数Values.IndexOf(countString);
            data.上下5Offsets[index] = data.上下5;
            DrawRebarLayout();
        }
        private void 左右6_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.左側芯筋本数2;
            int index = data.Valid左側芯筋本数Values.IndexOf(countString);
            data.左右6Offsets[index] = data.左右6;
            DrawRebarLayout();
        }
        private void 上下6_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.左側芯筋本数2;
            int index = data.Valid左側芯筋本数Values.IndexOf(countString);
            data.上下6Offsets[index] = data.上下6;
            DrawRebarLayout();
        }
        private void 左右7_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.右側芯筋本数2;
            int index = data.Valid右側芯筋本数Values.IndexOf(countString);
            data.左右7Offsets[index] = data.左右7;
            DrawRebarLayout();
        }
        private void 上下7_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            var data = DictGBD[beamType];
            string countString = data.右側芯筋本数2;
            int index = data.Valid右側芯筋本数Values.IndexOf(countString);
            data.上下7Offsets[index] = data.上下7;
            DrawRebarLayout();
        }

        private void 縦向き中子本数_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Validate中子筋本数())
            {
                MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        private void 横向き中子本数_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Validate中子筋本数())
            {
                MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        private void 縦向き中子本数1_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Validate中子筋本数())
            {
                MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        private void 横向き中子本数1_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Validate中子筋本数())
            {
                MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        private void 仕口部_縦向き中子本数_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Validate中子筋本数())
            {
                MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        private void 仕口部_横向き中子本数_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!Validate中子筋本数())
            {
                MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }
    }
}
