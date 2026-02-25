using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Path = System.Windows.Shapes.Path;

namespace RevitProjectDataAddin
{
    public partial class 梁の配置 : Window
    {
        public List<string> Valid径Values => new List<string> { "10", "13", "16", "19", "22", "25", "29", "32", "35", "38" };
        public List<string> Validスタラップ形Values => new List<string> { "1", "2", "3", "4", "5", "6", "7", "8", "9" };
        public List<string> Valid中子筋形Values => new List<string> { "1", "2", "3", "4", "5", "6", "7", "8" };
        public List<string> Valid材質Values => new List<string> { "SD295", "SD345", "SD390", "SD490", "ウルボン" };
        public List<string> Validスタラップ径Values => new List<string> { "10", "13", "16" };
        public List<string> Valid位置Values => new List<string> { "1", "2", "3", "4" };
        public List<string> Valid梁Values => new List<string> { "端部1", "中央", "端部2" };

        private string GetField(string fieldBase, string beamType)
        {
            File.AppendAllText("debugdata.txt", Environment.NewLine + $"-------------------- SetGetter (fieldBase: {fieldBase}, beamType: {beamType}) --------------------" + Environment.NewLine);
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [GetField] called with fieldBase: {fieldBase}, beamType: {beamType}" + Environment.NewLine);
            switch (beamType)
            {
                case "端部1":
                    var value1 = (string)_currentHariData.GetType().GetProperty($"端部1{fieldBase}")?.GetValue(_currentHariData) ?? "0";
                    File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Return value] 端部1{fieldBase} returns: {value1}" + Environment.NewLine);
                    return value1;
                case "中央":
                    var value2 = (string)_currentHariData.GetType().GetProperty($"中央{fieldBase}")?.GetValue(_currentHariData) ?? "0";
                    File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Return value] 中央{fieldBase} returns: {value2}" + Environment.NewLine);
                    return value2;
                case "端部2":
                    var value3 = (string)_currentHariData.GetType().GetProperty($"端部2{fieldBase}")?.GetValue(_currentHariData) ?? "0";
                    File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Return value] 端部2{fieldBase} returns: {value3}" + Environment.NewLine);
                    return value3;
                default:
                    File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Return value] Default case for beamType: {beamType}, returning '0'" + Environment.NewLine);
                    return "0";
            }
        }

        private void GridBotDataUpdater()
        {
            File.AppendAllText("debugdata.txt", Environment.NewLine + $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [GridBotDataUpdater] Start" + Environment.NewLine);

            foreach (var type in new[] { "端部1", "中央", "端部2" })
            {
                // Gán setter và khởi tạo Valid*Values
                SetGetters(type);
            }

            File.AppendAllText("debugdata.txt",
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [GridBotDataUpdater] End" + Environment.NewLine);
        }

        public void SetGetters(string beamType)
        {
            if (!DictGBD.ContainsKey(beamType))
            {
                DictGBD[beamType] = new GridBotData(beamType);
            }

            var data = DictGBD[beamType];

            // Gán getter tương ứng với beamType
            data.SetGetter("上筋本数", () => GetField("上筋本数", beamType));
            data.SetGetter("下筋本数", () => GetField("下筋本数", beamType));
            data.SetGetter("上宙1", () => GetField("上宙1", beamType));
            data.SetGetter("上宙2", () => GetField("上宙2", beamType));
            data.SetGetter("下宙1", () => GetField("下宙1", beamType));
            data.SetGetter("下宙2", () => GetField("下宙2", beamType));
            data.SetGetter("中子筋本数", () => GetField("中子筋本数", beamType));
        }

        private void UpdateComboBoxItemsSource()
        {

            上筋COMBOBOX.ItemsSource = CurrentGBD.Valid上筋本数Values;
            下筋COMBOBOX.ItemsSource = CurrentGBD.Valid下筋本数Values;
            上宙1COMBOBOX.ItemsSource = CurrentGBD.Valid上宙1Values;
            上宙2COMBOBOX.ItemsSource = CurrentGBD.Valid上宙2Values;
            下宙1COMBOBOX.ItemsSource = CurrentGBD.Valid下宙1Values;
            下宙2COMBOBOX.ItemsSource = CurrentGBD.Valid下宙2Values;
            中子筋の数COMBOBOX.ItemsSource = CurrentGBD.Valid数Values;
            中子筋の位置COMBOBOX.ItemsSource = CurrentGBD.Valid中子筋の位置Values;

        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [CheckBox_Checked] Called " + Environment.NewLine);
            //Copy 中央 sang cho 端部1
            端部1の幅TEXTBOX.Text = 中央の幅TEXTBOX.Text;
            端部1の成TEXTBOX.Text = 中央の成TEXTBOX.Text;
            端部1の主筋径COMBOBOX.SelectedIndex = 中央の主筋径COMBOBOX.SelectedIndex;
            端部1の主筋材質COMBOBOX.SelectedIndex = 中央の材質COMBOBOX.SelectedIndex;
            端部1の上筋本数TEXTBOX.Text = 中央の上筋本数TEXTBOX.Text;
            端部1の上宙1TEXTBOX.Text = 中央の上宙1TEXTBOX.Text;
            端部1の上宙2TEXTBOX.Text = 中央の上宙2TEXTBOX.Text;
            端部1の下宙2TEXTBOX.Text = 中央の下宙2TEXTBOX.Text;
            端部1の下宙1TEXTBOX.Text = 中央の下宙1TEXTBOX.Text;
            端部1の下筋本数TEXTBOX.Text = 中央の下筋本数TEXTBOX.Text;
            端部1のスタラップ径TEXTBOX.Text = 中央のスタラップ径TEXTBOX.Text;
            端部1のピッチTEXTBOX.Text = 中央のピッチTEXTBOX.Text;
            端部1のスタラップ材質COMBOBOX.SelectedIndex = 中央のスタラップ材質COMBOBOX.SelectedIndex;
            端部1のスタラップ形COMBOBOX.SelectedIndex = 中央のスタラップ形COMBOBOX.SelectedIndex;
            端部1のCAP径TEXTBOX.Text = 中央のCAP径TEXTBOX.Text;
            端部1の中子筋径TEXTBOX.Text = 中央の中子筋径TEXTBOX.Text;
            端部1の中子筋径ピッチTEXTBOX.Text = 中央の中子筋径ピッチTEXTBOX.Text;
            端部1の中子筋材質COMBOBOX.SelectedIndex = 中央の中子筋材質COMBOBOX.SelectedIndex;
            端部1の中子筋形COMBOBOX.SelectedIndex = 中央の中子筋形COMBOBOX.SelectedIndex;
            端部1の中子筋本数TEXTBOX.Text = 中央の中子筋本数TEXTBOX.Text;

            //Copy 中央 sang cho 端部2
            端部2の幅TEXTBOX.Text = 中央の幅TEXTBOX.Text;
            端部2のTEXTBOX.Text = 中央の成TEXTBOX.Text;
            端部2の主筋径COMBOBOX.SelectedIndex = 中央の主筋径COMBOBOX.SelectedIndex;
            端部2の主筋材質COMBOBOX.SelectedIndex = 中央の材質COMBOBOX.SelectedIndex;
            端部2の上筋本数TEXTBOX.Text = 中央の上筋本数TEXTBOX.Text;
            端部2の上宙1TEXTBOX.Text = 中央の上宙1TEXTBOX.Text;
            端部2の上宙2TEXTBOX.Text = 中央の上宙2TEXTBOX.Text;
            端部2の下宙2TEXTBOX.Text = 中央の下宙2TEXTBOX.Text;
            端部2の下宙1TEXTBOX.Text = 中央の下宙1TEXTBOX.Text;
            端部2の下筋本数TEXTBOX.Text = 中央の下筋本数TEXTBOX.Text;
            端部2のスタラップ径TEXTBOX.Text = 中央のスタラップ径TEXTBOX.Text;
            端部2のピッチTEXTBOX.Text = 中央のピッチTEXTBOX.Text;
            端部2のスタラップ材質COMBOBOX.SelectedIndex = 中央のスタラップ材質COMBOBOX.SelectedIndex;
            端部2のスタラップ形COMBOBOX.SelectedIndex = 中央のスタラップ形COMBOBOX.SelectedIndex;
            端部2のCAP径TEXTBOX.Text = 中央のCAP径TEXTBOX.Text;
            端部2の中子筋径TEXTBOX.Text = 中央の中子筋径TEXTBOX.Text;
            端部2の中子筋径ピッチTEXTBOX.Text = 中央の中子筋径ピッチTEXTBOX.Text;
            端部2の中子筋材質COMBOBOX.SelectedIndex = 中央の中子筋材質COMBOBOX.SelectedIndex;
            端部2の中子筋形COMBOBOX.SelectedIndex = 中央の中子筋形COMBOBOX.SelectedIndex;
            端部2の中子筋本数TEXTBOX.Text = 中央の中子筋本数TEXTBOX.Text;

            Control[] controls1 = { 端部1の幅TEXTBOX, 端部1の成TEXTBOX, 端部1の主筋径COMBOBOX, 端部1の主筋材質COMBOBOX,
                                    端部1の上筋本数TEXTBOX, 端部1の上宙1TEXTBOX, 端部1の上宙2TEXTBOX, 端部1の下宙2TEXTBOX,
                                    端部1の下宙1TEXTBOX, 端部1の下筋本数TEXTBOX, 端部1のスタラップ径TEXTBOX,
                                    端部1のピッチTEXTBOX, 端部1のスタラップ材質COMBOBOX, 端部1のスタラップ形COMBOBOX,
                                    端部1のCAP径TEXTBOX, 端部1の中子筋径TEXTBOX, 端部1の中子筋径ピッチTEXTBOX,
                                    端部1の中子筋材質COMBOBOX, 端部1の中子筋形COMBOBOX, 端部1の中子筋本数TEXTBOX };
            Control[] controls2 = { 端部2の幅TEXTBOX, 端部2のTEXTBOX, 端部2の主筋径COMBOBOX, 端部2の主筋材質COMBOBOX,
                                    端部2の上筋本数TEXTBOX, 端部2の上宙1TEXTBOX, 端部2の上宙2TEXTBOX, 端部2の下宙2TEXTBOX,
                                    端部2の下宙1TEXTBOX, 端部2の下筋本数TEXTBOX, 端部2のスタラップ径TEXTBOX,
                                    端部2のピッチTEXTBOX, 端部2のスタラップ材質COMBOBOX, 端部2のスタラップ形COMBOBOX,
                                    端部2のCAP径TEXTBOX, 端部2の中子筋径TEXTBOX, 端部2の中子筋径ピッチTEXTBOX,
                                    端部2の中子筋材質COMBOBOX, 端部2の中子筋形COMBOBOX, 端部2の中子筋本数TEXTBOX };
            // Vô hiệu hóa các điều khiển ở端部1 và端部2
            foreach (var control in controls1)
            {
                if (control is Control ctrl)
                {
                    ctrl.IsEnabled = false;
                }
            }
            foreach (var control in controls2)
            {
                if (control is Control ctrl)
                {
                    ctrl.IsEnabled = false;
                }
            }

            Update筋宙本数Columns(); // Cập nhật cột và ItemsSource khi đồng bộ
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [CheckBox_Unchecked] Called " + Environment.NewLine);
            Control[] controls1 = { 端部1の幅TEXTBOX, 端部1の成TEXTBOX, 端部1の主筋径COMBOBOX, 端部1の主筋材質COMBOBOX,
                                    端部1の上筋本数TEXTBOX, 端部1の上宙1TEXTBOX, 端部1の上宙2TEXTBOX, 端部1の下宙2TEXTBOX,
                                    端部1の下宙1TEXTBOX, 端部1の下筋本数TEXTBOX, 端部1のスタラップ径TEXTBOX,
                                    端部1のピッチTEXTBOX, 端部1のスタラップ材質COMBOBOX, 端部1のスタラップ形COMBOBOX,
                                    端部1のCAP径TEXTBOX, 端部1の中子筋径TEXTBOX, 端部1の中子筋径ピッチTEXTBOX,
                                    端部1の中子筋材質COMBOBOX, 端部1の中子筋形COMBOBOX, 端部1の中子筋本数TEXTBOX };
            Control[] controls2 = { 端部2の幅TEXTBOX, 端部2のTEXTBOX, 端部2の主筋径COMBOBOX, 端部2の主筋材質COMBOBOX,
                                    端部2の上筋本数TEXTBOX, 端部2の上宙1TEXTBOX, 端部2の上宙2TEXTBOX, 端部2の下宙2TEXTBOX,
                                    端部2の下宙1TEXTBOX, 端部2の下筋本数TEXTBOX, 端部2のスタラップ径TEXTBOX,
                                    端部2のピッチTEXTBOX, 端部2のスタラップ材質COMBOBOX, 端部2のスタラップ形COMBOBOX,
                                    端部2のCAP径TEXTBOX, 端部2の中子筋径TEXTBOX, 端部2の中子筋径ピッチTEXTBOX,
                                    端部2の中子筋材質COMBOBOX, 端部2の中子筋形COMBOBOX, 端部2の中子筋本数TEXTBOX };
            // Bỏ đồng bộ các điều khiển ở端部1 và端部2
            foreach (var control in controls1)
            {
                if (control is Control ctrl)
                {
                    ctrl.IsEnabled = true;
                }
            }
            foreach (var control in controls2)
            {
                if (control is Control ctrl)
                {
                    ctrl.IsEnabled = true;
                }
            }

            Update筋宙本数Columns(); // Cập nhật cột khi bỏ đồng bộ


        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
        private void Update筋宙本数Columns()
        {
            File.AppendAllText("debugdata.txt",
                    $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Update筋宙本数Columns] Start: CurrentGBD BeamType: {CurrentGBD?.BeamType}" + Environment.NewLine);
            // Xác định TextBox để kiểm tra dựa trên beamType
            string upperZ2Text = "";
            string lowerZ2Text = "";
            switch (beamType)
            {
                case "端部1":
                    upperZ2Text = 端部1の上宙2TEXTBOX.Text;
                    lowerZ2Text = 端部1の下宙2TEXTBOX.Text;
                    break;
                case "中央":
                    upperZ2Text = 中央の上宙2TEXTBOX.Text;
                    lowerZ2Text = 中央の下宙2TEXTBOX.Text;
                    break;
                case "端部2":
                    upperZ2Text = 端部2の上宙2TEXTBOX.Text;
                    lowerZ2Text = 端部2の下宙2TEXTBOX.Text;
                    break;
            }

            // Kiểm tra xem có hiển thị cột 上宙2 và 下宙2 không
            bool has上宙2 = int.TryParse(upperZ2Text, out int v1) && v1 != 0;
            bool has下宙2 = int.TryParse(lowerZ2Text, out int w1) && w1 != 0;

            // Tính số cột
            int colCount = 4 + (has上宙2 ? 1 : 0) + (has下宙2 ? 1 : 0);
            double totalWidth = 1520.0;
            double colWidth = totalWidth / colCount;

            var grid = 筋宙本数;
            grid.ColumnDefinitions.Clear();
            for (int i = 0; i < colCount; i++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(colWidth) });
            }

            RemoveDynamicColumnChildren(grid);

            var columnMap = new List<(FrameworkElement Control, TextBox SourceTextBox, string Label, int Column)>
            {
                (上筋COMBOBOX, beamType == "端部1" ? 端部1の上筋本数TEXTBOX : beamType == "中央" ? 中央の上筋本数TEXTBOX : 端部2の上筋本数TEXTBOX, "上筋", 0),
                (上宙1COMBOBOX, beamType == "端部1" ? 端部1の上宙1TEXTBOX : beamType == "中央" ? 中央の上宙1TEXTBOX : 端部2の上宙1TEXTBOX, "上宙1", 1)
            };
            if (has上宙2)
                columnMap.Add((上宙2COMBOBOX, beamType == "端部1" ? 端部1の上宙2TEXTBOX : beamType == "中央" ? 中央の上宙2TEXTBOX : 端部2の上宙2TEXTBOX, "上宙2", 2));
            if (has下宙2)
                columnMap.Add((下宙2COMBOBOX, beamType == "端部1" ? 端部1の下宙2TEXTBOX : beamType == "中央" ? 中央の下宙2TEXTBOX : 端部2の下宙2TEXTBOX, "下宙2", has上宙2 ? 3 : 2));
            columnMap.Add((下宙1COMBOBOX, beamType == "端部1" ? 端部1の下宙1TEXTBOX : beamType == "中央" ? 中央の下宙1TEXTBOX : 端部2の下宙1TEXTBOX, "下宙1", has上宙2 && has下宙2 ? 4 : (has上宙2 || has下宙2 ? 3 : 2)));
            columnMap.Add((下筋COMBOBOX, beamType == "端部1" ? 端部1の下筋本数TEXTBOX : beamType == "中央" ? 中央の下筋本数TEXTBOX : 端部2の下筋本数TEXTBOX, "下筋", has上宙2 && has下宙2 ? 5 : (has上宙2 || has下宙2 ? 4 : 3)));

            foreach (var (control, sourceTextBox, label, col) in columnMap)
            {
                PlaceColumnControls(grid, control, sourceTextBox, col, label);
            }

            // Đặt Width động cho tất cả TextBox dựa trên số cột mới
            int dynamicColumnCount = (has上宙2 ? 1 : 0) + (has下宙2 ? 1 : 0);
            double textBoxWidth = dynamicColumnCount == 1 ? 120 : (dynamicColumnCount == 2 ? 100 : 150);

            var allTextBoxes = grid.Children.OfType<TextBox>().Where(tb => tb.Name.EndsWith("左右TEXTBOX") || tb.Name.EndsWith("上下TEXTBOX"));
            foreach (var textBox in allTextBoxes)
            {
                textBox.Width = textBoxWidth;
            }

            int marginLeftOffset = dynamicColumnCount == 1 ? -40 : (dynamicColumnCount == 2 ? -70 : 0);
            var allUpDownLabels = grid.Children.OfType<TextBlock>().Where(tb => tb.Name.EndsWith("上下Label") && tb.Text == "上/下");
            foreach (var label in allUpDownLabels)
            {
                double originalLeftMargin = 198;
                Thickness currentMargin = label.Margin;
                label.Margin = new Thickness(originalLeftMargin + marginLeftOffset, currentMargin.Top, currentMargin.Right, currentMargin.Bottom);
            }

            // Cập nhật vị trí và visibility cho 上宙2
            if (has上宙2)
            {
                Grid.SetColumn(上宙2Label, 2);
                Grid.SetColumn(上宙2COMBOBOX, 2);
                Grid.SetColumn(Reset上宙2ボタン, 2);
                Grid.SetColumn(上宙2左右Label, 2);
                Grid.SetColumn(上宙2左右TEXTBOX, 2);
                Grid.SetColumn(上宙2上下Label, 2);
                Grid.SetColumn(上宙2上下TEXTBOX, 2);
                上宙2Label.Visibility = Visibility.Visible;
                上宙2COMBOBOX.Visibility = Visibility.Visible;
                Reset上宙2ボタン.Visibility = Visibility.Visible;
                上宙2左右Label.Visibility = Visibility.Visible;
                上宙2左右TEXTBOX.Visibility = Visibility.Visible;
                上宙2上下Label.Visibility = Visibility.Visible;
                上宙2上下TEXTBOX.Visibility = Visibility.Visible;
            }
            else
            {
                上宙2Label.Visibility = Visibility.Collapsed;
                上宙2COMBOBOX.Visibility = Visibility.Collapsed;
                Reset上宙2ボタン.Visibility = Visibility.Collapsed;
                上宙2左右Label.Visibility = Visibility.Collapsed;
                上宙2左右TEXTBOX.Visibility = Visibility.Collapsed;
                上宙2上下Label.Visibility = Visibility.Collapsed;
                上宙2上下TEXTBOX.Visibility = Visibility.Collapsed;
            }

            // Cập nhật vị trí và visibility cho 下宙2
            if (has下宙2)
            {
                Grid.SetColumn(下宙2Label, has上宙2 ? 3 : 2);
                Grid.SetColumn(下宙2COMBOBOX, has上宙2 ? 3 : 2);
                Grid.SetColumn(Reset下宙2ボタン, has上宙2 ? 3 : 2);
                Grid.SetColumn(下宙2左右Label, has上宙2 ? 3 : 2);
                Grid.SetColumn(下宙2左右TEXTBOX, has上宙2 ? 3 : 2);
                Grid.SetColumn(下宙2上下Label, has上宙2 ? 3 : 2);
                Grid.SetColumn(下宙2上下TEXTBOX, has上宙2 ? 3 : 2);
                下宙2Label.Visibility = Visibility.Visible;
                下宙2COMBOBOX.Visibility = Visibility.Visible;
                Reset下宙2ボタン.Visibility = Visibility.Visible;
                下宙2左右Label.Visibility = Visibility.Visible;
                下宙2左右TEXTBOX.Visibility = Visibility.Visible;
                下宙2上下Label.Visibility = Visibility.Visible;
                下宙2上下TEXTBOX.Visibility = Visibility.Visible;
            }
            else
            {
                下宙2Label.Visibility = Visibility.Collapsed;
                下宙2COMBOBOX.Visibility = Visibility.Collapsed;
                Reset下宙2ボタン.Visibility = Visibility.Collapsed;
                下宙2左右Label.Visibility = Visibility.Collapsed;
                下宙2左右TEXTBOX.Visibility = Visibility.Collapsed;
                下宙2上下Label.Visibility = Visibility.Collapsed;
                下宙2上下TEXTBOX.Visibility = Visibility.Collapsed;
            }

            UpdateColumnForControl(下宙1Label, has上宙2 && has下宙2 ? 4 : (has上宙2 || has下宙2 ? 3 : 2));
            UpdateColumnForControl(下宙1COMBOBOX, has上宙2 && has下宙2 ? 4 : (has上宙2 || has下宙2 ? 3 : 2));
            UpdateColumnForControl(Reset下宙1ボタン, has上宙2 && has下宙2 ? 4 : (has上宙2 || has下宙2 ? 3 : 2));
            UpdateColumnForControl(下宙1左右Label, has上宙2 && has下宙2 ? 4 : (has上宙2 || has下宙2 ? 3 : 2));
            UpdateColumnForControl(下宙1左右TEXTBOX, has上宙2 && has下宙2 ? 4 : (has上宙2 || has下宙2 ? 3 : 2));
            UpdateColumnForControl(下宙1上下Label, has上宙2 && has下宙2 ? 4 : (has上宙2 || has下宙2 ? 3 : 2));
            UpdateColumnForControl(下宙1上下TEXTBOX, has上宙2 && has下宙2 ? 4 : (has上宙2 || has下宙2 ? 3 : 2));

            UpdateColumnForControl(下筋Label, has上宙2 && has下宙2 ? 5 : (has上宙2 || has下宙2 ? 4 : 3));
            UpdateColumnForControl(下筋COMBOBOX, has上宙2 && has下宙2 ? 5 : (has上宙2 || has下宙2 ? 4 : 3));
            UpdateColumnForControl(Reset下筋ボタン, has上宙2 && has下宙2 ? 5 : (has上宙2 || has下宙2 ? 4 : 3));
            UpdateColumnForControl(下筋左右Label, has上宙2 && has下宙2 ? 5 : (has上宙2 || has下宙2 ? 4 : 3));
            UpdateColumnForControl(下筋左右TEXTBOX, has上宙2 && has下宙2 ? 5 : (has上宙2 || has下宙2 ? 4 : 3));
            UpdateColumnForControl(下筋上下Label, has上宙2 && has下宙2 ? 5 : (has上宙2 || has下宙2 ? 4 : 3));
            UpdateColumnForControl(下筋上下TEXTBOX, has上宙2 && has下宙2 ? 5 : (has上宙2 || has下宙2 ? 4 : 3));


            File.AppendAllText("debugdata.txt",
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Update筋宙本数Columns] End: CurrentGBD BeamType: {CurrentGBD?.BeamType}" + Environment.NewLine);
        }
        private void UpdateColumnForControl(FrameworkElement control, int targetColumn)
        {
            if (control != null && 筋宙本数.Children.Contains(control))
            {
                Grid.SetColumn(control, targetColumn);
            }
        }
        private void RemoveDynamicColumnChildren(Grid grid)
        {
            int colCount = grid.ColumnDefinitions.Count;
            var dynamicCols = new List<int>();
            if (colCount == 5) dynamicCols.Add(2);
            if (colCount == 6) { dynamicCols.Add(2); dynamicCols.Add(3); }

            var toRemove = new List<UIElement>();
            foreach (UIElement child in grid.Children)
            {
                int col = Grid.GetColumn(child);
                if (dynamicCols.Contains(col))
                {
                    if (child is FrameworkElement fe &&
                        (fe.Name.StartsWith("btn_") ||
                         fe.Name.StartsWith("lbl_") && !fe.Name.EndsWith("Label") ||
                         fe.Name.StartsWith("cb_") ||
                         fe.Name.StartsWith("lblLR_") ||
                         fe.Name.StartsWith("tbLR_") ||
                         fe.Name.StartsWith("lblUD_") ||
                         fe.Name.StartsWith("tbUD_")))
                    {
                        toRemove.Add(child);
                    }
                }
            }

            foreach (var child in toRemove)
            {
                grid.Children.Remove(child);
            }
        }
        private void PlaceColumnControls(Grid grid, FrameworkElement control, TextBox sourceTextBox, int col, string labels)
        {
            if (grid.Children.Contains(control))
            {
                Grid.SetColumn(control, col);
                if (control is ComboBox comboBox)
                {
                    comboBox.Margin = new Thickness(10, 198, 30, 0);
                    //comboBox.ItemsSource = GetValidCount(sourceTextBox?.Text);
                }
            }
            else
            {
                if (control.Parent is Panel parent)
                {
                    parent.Children.Remove(control);
                }
                grid.Children.Add(control);
                Grid.SetColumn(control, col);
                if (control is ComboBox comboBox)
                {
                    comboBox.Margin = new Thickness(10, 198, 30, 0);
                    //comboBox.ItemsSource = GetValidCount(sourceTextBox?.Text);
                }
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            TextBox textBox = sender as TextBox;
            string textBoxName = textBox?.Name ?? "Unknown";
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{textBoxName}.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);

            DrawScaledRectangle();
        }
        private void CopyGridColumn0To1()
        {
            //Copy 中央 sang cho 端部1
            端部1の幅TEXTBOX.Text = 中央の幅TEXTBOX.Text;
            端部1の成TEXTBOX.Text = 中央の成TEXTBOX.Text;
            端部1の主筋径COMBOBOX.SelectedIndex = 中央の主筋径COMBOBOX.SelectedIndex;
            端部1の主筋材質COMBOBOX.SelectedIndex = 中央の材質COMBOBOX.SelectedIndex;
            端部1の上筋本数TEXTBOX.Text = 中央の上筋本数TEXTBOX.Text;
            端部1の上宙1TEXTBOX.Text = 中央の上宙1TEXTBOX.Text;
            端部1の上宙2TEXTBOX.Text = 中央の上宙2TEXTBOX.Text;
            端部1の下宙2TEXTBOX.Text = 中央の下宙2TEXTBOX.Text;
            端部1の下宙1TEXTBOX.Text = 中央の下宙1TEXTBOX.Text;
            端部1の下筋本数TEXTBOX.Text = 中央の下筋本数TEXTBOX.Text;
            端部1のスタラップ径TEXTBOX.Text = 中央のスタラップ径TEXTBOX.Text;
            端部1のピッチTEXTBOX.Text = 中央のピッチTEXTBOX.Text;
            端部1のスタラップ材質COMBOBOX.SelectedIndex = 中央のスタラップ材質COMBOBOX.SelectedIndex;
            端部1のスタラップ形COMBOBOX.SelectedIndex = 中央のスタラップ形COMBOBOX.SelectedIndex;
            端部1のCAP径TEXTBOX.Text = 中央のCAP径TEXTBOX.Text;
            端部1の中子筋径TEXTBOX.Text = 中央の中子筋径TEXTBOX.Text;
            端部1の中子筋径ピッチTEXTBOX.Text = 中央の中子筋径ピッチTEXTBOX.Text;
            端部1の中子筋材質COMBOBOX.SelectedIndex = 中央の中子筋材質COMBOBOX.SelectedIndex;
            端部1の中子筋形COMBOBOX.SelectedIndex = 中央の中子筋形COMBOBOX.SelectedIndex;
            端部1の中子筋本数TEXTBOX.Text = 中央の中子筋本数TEXTBOX.Text;

            //Copy 中央 sang cho 端部2
            端部2の幅TEXTBOX.Text = 中央の幅TEXTBOX.Text;
            端部2のTEXTBOX.Text = 中央の成TEXTBOX.Text;
            端部2の主筋径COMBOBOX.SelectedIndex = 中央の主筋径COMBOBOX.SelectedIndex;
            端部2の主筋材質COMBOBOX.SelectedIndex = 中央の材質COMBOBOX.SelectedIndex;
            端部2の上筋本数TEXTBOX.Text = 中央の上筋本数TEXTBOX.Text;
            端部2の上宙1TEXTBOX.Text = 中央の上宙1TEXTBOX.Text;
            端部2の上宙2TEXTBOX.Text = 中央の上宙2TEXTBOX.Text;
            端部2の下宙2TEXTBOX.Text = 中央の下宙2TEXTBOX.Text;
            端部2の下宙1TEXTBOX.Text = 中央の下宙1TEXTBOX.Text;
            端部2の下筋本数TEXTBOX.Text = 中央の下筋本数TEXTBOX.Text;
            端部2のスタラップ径TEXTBOX.Text = 中央のスタラップ径TEXTBOX.Text;
            端部2のピッチTEXTBOX.Text = 中央のピッチTEXTBOX.Text;
            端部2のスタラップ材質COMBOBOX.SelectedIndex = 中央のスタラップ材質COMBOBOX.SelectedIndex;
            端部2のスタラップ形COMBOBOX.SelectedIndex = 中央のスタラップ形COMBOBOX.SelectedIndex;
            端部2のCAP径TEXTBOX.Text = 中央のCAP径TEXTBOX.Text;
            端部2の中子筋径TEXTBOX.Text = 中央の中子筋径TEXTBOX.Text;
            端部2の中子筋径ピッチTEXTBOX.Text = 中央の中子筋径ピッチTEXTBOX.Text;
            端部2の中子筋材質COMBOBOX.SelectedIndex = 中央の中子筋材質COMBOBOX.SelectedIndex;
            端部2の中子筋形COMBOBOX.SelectedIndex = 中央の中子筋形COMBOBOX.SelectedIndex;
            端部2の中子筋本数TEXTBOX.Text = 中央の中子筋本数TEXTBOX.Text;
        }

        private bool ValidateNakagoCount(string beamType, bool showMessage = true)
        {
            ComboBox nakagoShapeComboBox = null;
            ComboBox nakagoShapeComboBox1 = null;
            ComboBox nakagoShapeComboBox2 = null;
            TextBox nakagoCountTextBox = null;
            TextBox nakagoCountTextBox1 = null;
            TextBox nakagoCountTextBox2 = null;

            nakagoShapeComboBox = 端部1の中子筋形COMBOBOX;
            nakagoCountTextBox = 端部1の中子筋本数TEXTBOX;

            nakagoShapeComboBox1 = 中央の中子筋形COMBOBOX;
            nakagoCountTextBox1 = 中央の中子筋本数TEXTBOX;

            nakagoShapeComboBox2 = 端部2の中子筋形COMBOBOX;
            nakagoCountTextBox2 = 端部2の中子筋本数TEXTBOX;

            if (nakagoShapeComboBox.SelectedIndex == 5 || nakagoShapeComboBox.SelectedIndex == 6)
            {
                if (nakagoCountTextBox.Text != "2")
                {
                    nakagoCountTextBox.Text = "2";
                    if (showMessage)
                    {
                        MessageBox.Show("この選択では中子の数は2のみ許可されています", "注意", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return false;
                }
            }
            else
            {
                if (!Validate中子筋本数())
                {
                    MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            if (nakagoShapeComboBox1.SelectedIndex == 5 || nakagoShapeComboBox1.SelectedIndex == 6)
            {
                if (nakagoCountTextBox1.Text != "2")
                {
                    nakagoCountTextBox1.Text = "2";
                    if (showMessage)
                    {
                        MessageBox.Show("この選択では中子の数は2のみ許可されています", "注意", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return false;
                }
            }
            else
            {
                if (!Validate中子筋本数())
                {
                    MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            if (nakagoShapeComboBox2.SelectedIndex == 5 || nakagoShapeComboBox2.SelectedIndex == 6)
            {
                if (nakagoCountTextBox2.Text != "2")
                {
                    nakagoCountTextBox2.Text = "2";
                    if (showMessage)
                    {
                        MessageBox.Show("この選択では中子の数は2のみ許可されています", "注意", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                    return false;
                }
            }
            else
            {
                if (!Validate中子筋本数())
                {
                    MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }
        private void ResetGridBotControls()
        {
            // Xác thực số lượng 中子 trước khi tiếp tục
            if (!Validate中子筋本数())
            {
                MessageBox.Show("中子筋本数の数が規定を超えています。", "エラー", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // Copy data if 柱全断面 is checked
            if (全断面.IsChecked == true)
            {
                CopyGridColumn0To1();
            }
            ValidateNakagoCount(beamType);
            GridBotDataUpdater();

            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ResetGridBotControls] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            // Tạo mới lại GridBotData cho beamType hiện tại
            DictGBD[beamType] = new GridBotData(beamType);

            // Nếu bạn dùng SetGetters, thì gọi lại cho beamType này
            SetGetters(beamType);

            // Gán lại để cập nhật UI
            CurrentGBD = DictGBD[beamType];
            GridBot.DataContext = CurrentGBD;
            UpdateComboBoxItemsSource();
            Update筋宙本数Columns();
            DrawScaledRectangle();    // Vẽ lại
        }

        private void Reset上筋ボタン_Click(object sender, RoutedEventArgs e)
        {
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Reset上筋ボタン] Clicked: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            // Reset toàn bộ offset trái-phải
            foreach (var key in data.上筋左右Offsets.Keys.ToList())
            {
                data.上筋左右Offsets[key] = "0";

            }
            foreach (var key in data.上筋上下Offsets.Keys.ToList())
            {
                data.上筋上下Offsets[key] = "0";

            }
            data.上筋左右 = "0";
            data.上筋上下 = "0";
            DrawScaledRectangle();
        }


        private void Reset上宙1ボタン_Click(object sender, RoutedEventArgs e)
        {
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Reset上宙1ボタン] Clicked: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            foreach (var key in data.上宙1左右Offsets.Keys.ToList())
            {
                data.上宙1左右Offsets[key] = "0";
            }
            foreach (var key in data.上宙1上下Offsets.Keys.ToList())
            {
                data.上宙1上下Offsets[key] = "0";
            }
            data.上宙1左右 = "0";
            data.上宙1上下 = "0";

            DrawScaledRectangle();
        }

        private void Reset上宙2ボタン_Click(object sender, RoutedEventArgs e)
        {
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Reset上宙2ボタン] Clicked: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            foreach (var key in data.上宙2左右Offsets.Keys.ToList())
            {
                data.上宙2左右Offsets[key] = "0";
            }
            foreach (var key in data.上宙2上下Offsets.Keys.ToList())
            {
                data.上宙2上下Offsets[key] = "0";
            }
            data.上宙2左右 = "0";
            data.上宙2上下 = "0";

            DrawScaledRectangle();
        }

        private void Reset下宙1ボタン_Click(object sender, RoutedEventArgs e)
        {
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Reset下宙1ボタン] Clicked: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            foreach (var key in data.下宙1左右Offsets.Keys.ToList())
            {
                data.下宙1左右Offsets[key] = "0";
            }
            foreach (var key in data.下宙1上下Offsets.Keys.ToList())
            {
                data.下宙1上下Offsets[key] = "0";
            }
            data.下宙1左右 = "0";
            data.下宙1上下 = "0";

            DrawScaledRectangle();
        }

        private void Reset下宙2ボタン_Click(object sender, RoutedEventArgs e)
        {
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Reset下宙2ボタン] Clicked: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            foreach (var key in data.下宙2左右Offsets.Keys.ToList())
            {
                data.下宙2左右Offsets[key] = "0";
            }
            foreach (var key in data.下宙2上下Offsets.Keys.ToList())
            {
                data.下宙2上下Offsets[key] = "0";
            }
            data.下宙2左右 = "0";
            data.下宙2上下 = "0";

            DrawScaledRectangle();
        }

        private void Reset下筋ボタン_Click(object sender, RoutedEventArgs e)
        {
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [Reset下筋ボタン] Clicked: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            foreach (var key in data.下筋左右Offsets.Keys.ToList())
            {
                data.下筋左右Offsets[key] = "0";
            }
            foreach (var key in data.下筋上下Offsets.Keys.ToList())
            {
                data.下筋上下Offsets[key] = "0";
            }
            data.下筋左右 = "0";
            data.下筋上下 = "0";

            DrawScaledRectangle();
        }


        /// ////////// 2025 06 03 ////////////////
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

        private void DrawArc_腹筋(Canvas canvas, double originX, double originY, double scale,
        double cx, double cy, double radius, int quadrant, double d, Brush stroke, int rotation_direction)
        {
            double startAngle = 0;
            double shape = 0;
            double multiples1 = 0;
            double multiples2 = 0;
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

        private void line_腹筋(Canvas canvas, double originX, double originY, double scale,
        double cx1, double cy1, double cx11, double cy11, double radius1, double radius11, int quadrant, double d, Brush stroke, int rotation_direction)
        {
            double startAngle = 0;
            double shape = 0;
            double multiples1 = 0;
            double multiples2 = 0;

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
        private void DrawDimension(Canvas canvas, double originX, double originY, double scale,
            double x1_real, double y1_real, double x2_real, double y2_real, string label, double changed_x, double changed_y, Brush color)
        {
            double x1 = x1_real;
            double y1 = y1_real;
            double x2 = x2_real;
            double y2 = y2_real;

            double dx = x2 - x1;
            double dy = y2 - y1;
            double length = Math.Sqrt(dx * dx + dy * dy);
            double midX = (x1 + x2) / 2;
            double midY = (y1 + y2) / 2;

            // Vẽ hai tick (vuông góc đường kích thước)
            double tickLength = 5 / scale;
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

            // Tạo TextBlock trước để đo kích thước
            TextBlock text = new TextBlock
            {
                Text = $" {label} ",
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
            Canvas.SetLeft(text, originX + midX * scale - textSize.Width / 2 + changed_x);
            Canvas.SetTop(text, originY + midY * scale - textSize.Height / 2 + changed_y);

            canvas.Children.Add(text);
        }


        private void DrawRebarDimensions_X(Canvas canvas, double originX, double originY, double scale, List<double> xPositions, double yPosition, double rebarDiameter, double changed_X, double changed_Y, Brush color)
        {
            for (int i = 0; i < xPositions.Count - 1; i++)
            {

                double x1 = xPositions[i];
                //MessageBox.Show($"{x1}");
                double x2 = xPositions[i + 1];
                double dist = Math.Abs(x2 - x1);
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
                    x1, yPosition - rebarDiameter / scale,
                    x2, yPosition - rebarDiameter / scale,
                    label, changed_X, changed_Y, color);
            }
        }
        private void DrawRebarDimensions_Y(Canvas canvas, double originX, double originY, double scale, double xPositions, List<double> yPositions, double rebarDiameter, double changed_X, double changed_Y, Brush color)
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
                    xPositions + rebarDiameter / scale,
                    y1,
                    xPositions + rebarDiameter / scale,
                    y2,

                    label, changed_X, changed_Y, color);
            }
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
            if (B.Count > 4)
            {
                if (countA == countB)
                {
                    if (countA % 2 == 0 && countB % 2 == 0)
                    {
                        int mid = countA / 2;
                        newB.Add(B[1]);
                        newB.AddRange(list_A.Take(countA - mid + 1));

                        newB.AddRange(list_A.Skip(countA - mid + 1));
                        newB.Add(B[B.Count - 2]);
                    }
                    else if (countA % 2 == 1 && countB % 2 == 1)
                    {
                        int mid = countA / 2;
                        newB.Add(B[1]);
                        newB.AddRange(list_A.Take(countA - mid - 1));
                        newB.Add(list_A[mid]);
                        newB.AddRange(list_A.Skip(countA - mid));
                        newB.Add(B[B.Count - 2]);
                    }
                }
                else if (countA < countB)
                {
                    if (countA % 2 == 1 && countB % 2 == 1)
                    {
                        int mid = countA / 2;
                        int midB = countB / 2;
                        int numSides = (countB - 1) / 2;

                        newB.Add(B[1]);
                        newB.AddRange(list_B.Take(midB - mid));
                        newB.AddRange(list_A.Take(countA - mid - 1));
                        newB.Add(list_A[mid]);
                        newB.AddRange(list_A.Skip(countA - mid));
                        newB.AddRange(list_B.Skip(midB + mid + 1));
                        newB.Add(B[B.Count - 2]);
                    }
                    else if (countA % 2 == 1 && countB % 2 == 0)
                    {
                        int mid = countA / 2;
                        int midB = countB / 2;
                        int numSides = (countB - 1) / 2;

                        newB.Add(B[1]);
                        newB.AddRange(list_B.Take(midB - mid));
                        newB.AddRange(list_A.Take(countA - mid - 1));
                        newB.Add(list_A[mid]);
                        newB.AddRange(list_A.Skip(countA - mid));
                        newB.AddRange(list_B.Skip(midB + mid + 1));
                        newB.Add(B[B.Count - 2]);
                    }
                    else if (countA % 2 == 0 && countB % 2 == 0)
                    {
                        int mid = countA / 2;
                        int midB = countB / 2;
                        int numSides = (countB - 1) / 2;

                        newB.Add(B[1]);
                        newB.AddRange(list_B.Take(midB - mid));
                        newB.AddRange(list_A.Take(countA - mid + 1));

                        newB.AddRange(list_A.Skip(countA - mid + 1));
                        newB.AddRange(list_B.Skip(midB + mid));
                        newB.Add(B[B.Count - 2]);
                    }
                    else if (countA % 2 == 0 && countB % 2 == 1)
                    {
                        int mid = countA / 2;
                        int midB = countB / 2;
                        int numSides = (countB - 1) / 2;

                        newB.Add(B[1]);
                        newB.AddRange(list_B.Take(midB - mid));
                        newB.AddRange(list_A.Take(countA - mid + 1));
                        newB.AddRange(list_A.Skip(countA - mid + 1));
                        newB.AddRange(list_B.Skip(midB + mid));
                        newB.Add(B[B.Count - 2]);
                    }
                }

                else
                {
                    // TH1: A lẻ, B lẻ
                    if (countA % 2 == 1 && countB % 2 == 1)
                    {
                        int mid = countA / 2;
                        int numSides = (countB - 1) / 2;
                        newB.Add(B[1]);
                        newB.AddRange(list_A.Take(numSides));                      // đầu
                        newB.Add(list_A[mid]);                                     // giữa
                        newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                        newB.Add(B[B.Count - 2]);
                    }
                    // TH2: A lẻ, B chẵn
                    else if (countA % 2 == 1 && countB % 2 == 0)
                    {
                        int numSides = countB / 2;
                        newB.Add(B[1]);
                        newB.AddRange(list_A.Take(numSides));                      // đầu
                        newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                        newB.Add(B[B.Count - 2]);
                    }
                    // TH3: A chẵn, B lẻ
                    else if (countA % 2 == 0 && countB % 2 == 1)
                    {
                        int numSides = (countB - 1) / 2;
                        int midLeft = countA / 2 - 1;
                        newB.Add(B[1]);
                        newB.AddRange(list_A.Take(numSides));                      // đầu
                        newB.Add(list_A[midLeft]);                                 // giữa trái
                        newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                        newB.Add(B[B.Count - 2]);
                    }
                    // TH4: A chẵn, B chẵn
                    else if (countA % 2 == 0 && countB % 2 == 0)
                    {
                        int numSides = countB / 2;
                        newB.Add(B[1]);
                        newB.AddRange(list_A.Take(numSides));                      // đầu
                        newB.AddRange(list_A.Skip(countA - numSides));             // cuối
                        newB.Add(B[B.Count - 2]);
                    }

                }
            }
            else if (B.Count == 3)
            {
                int mid = countA / 2;
                newB.Add(B[1]);
                //MessageBox.Show($"[{B[1]}]");
                //newB.AddRange(list_A.Take(1));
            }
            else if (B.Count == 4)
            {
                int mid = countA / 2;
                newB.Add(B[1]);
                newB.Add(B[B.Count - 2]);
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
            else if (B.Count == 3)
            {
                int mid = countA / 2;
                newB.Add(B[0]);
                newB.Add(B[1]);
                //MessageBox.Show($"[{B[1]}]");
                newB.Add(B[B.Count - 1]);
            }
            else if (B.Count == 4)
            {
                int mid = countA / 2;
                newB.Add(B[0]);
                newB.Add(B[1]);
                newB.Add(B[B.Count - 2]);
                newB.Add(B[B.Count - 1]);
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
        private void DrawArc_スタラップ(Canvas canvas, double originX, double originY, double scale,
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

        private void line_スタラップ(Canvas canvas, double originX, double originY, double scale,
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
        private void DrawArc_フック(Canvas canvas, double originX, double originY, double scale,
double cx, double cy, double radius, int quadrant, double d, Brush stroke, int rotation_direction)
        {
            double startAngle = 0;
            double shape = 0;
            double multiples1 = 0;
            double multiples2 = 0;
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
                    multiples1 = 4;
                    multiples2 = 4;
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
                    multiples1 = 4;
                    multiples2 = 4;
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

        private void line_フック(Canvas canvas, double originX, double originY, double scale,
        double cx1, double cy1, double cx11, double cy11, double radius1, double radius11, int quadrant, double d, Brush stroke, int rotation_direction)
        {
            double startAngle = 0;
            double shape = 0;
            double multiples1 = 0;
            double multiples2 = 0;

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
                    multiples1 = 4;
                    multiples2 = 4;
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
                    multiples1 = 4;
                    multiples2 = 4;
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
        private void DrawScaledRectangle()
        {
            MyCanvas.Children.Clear();
            var data = DictGBD[beamType];

            double realWidth = 0;
            double realHeight = 0;
            double 上TEXTBOX_ = 0;
            double 下TEXTBOX_ = 0;
            double 左TEXTBOX_ = 0;
            double 右TEXTBOX_ = 0;
            double スタラップ径 = 10;
            double 主筋径 = 10;
            double 上筋本数 = 0;
            double 上宙1 = 0;
            double 上宙2 = 0;
            double 下宙2 = 0;
            double 下宙1 = 0;
            double 下筋本数 = 0;
            double フックの位置 = 0;
            //bool 中子筋_方向_ = 中子筋_方向.IsChecked ?? false;
            int 中子筋形 = 0;
            int 中子筋本数 = 0;
            int 中子筋径 = 0;
            int 腹筋本数 = 0;
            int 腹筋径 = 0;
            int 幅止筋径 = 0;

            int スタラップ形 = 0;
            //int 中子筋の位置 = 中子筋の位置_1TEXTBOX.SelectedIndex;
            switch (beamType)
            {
                case "端部1":
                    double.TryParse(端部1の幅TEXTBOX.Text, out realWidth);
                    double.TryParse(端部1の成TEXTBOX.Text, out realHeight);
                    double.TryParse(上TEXTBOX.Text, out 上TEXTBOX_);
                    double.TryParse(下TEXTBOX.Text, out 下TEXTBOX_);
                    double.TryParse(左TEXTBOX.Text, out 左TEXTBOX_);
                    double.TryParse(右TEXTBOX.Text, out 右TEXTBOX_);
                    double.TryParse(端部1のスタラップ径TEXTBOX.Text, out スタラップ径);
                    double.TryParse(端部1の主筋径COMBOBOX.Text, out 主筋径);
                    double.TryParse(端部1の上筋本数TEXTBOX.Text, out 上筋本数);
                    double.TryParse(端部1の上宙1TEXTBOX.Text, out 上宙1);
                    double.TryParse(端部1の上宙2TEXTBOX.Text, out 上宙2);
                    double.TryParse(端部1の下宙1TEXTBOX.Text, out 下宙1);
                    double.TryParse(端部1の下宙2TEXTBOX.Text, out 下宙2);
                    double.TryParse(端部1の下筋本数TEXTBOX.Text, out 下筋本数);
                    int.TryParse(端部1の中子筋形COMBOBOX.Text, out 中子筋形);
                    int.TryParse(端部1の中子筋本数TEXTBOX.Text, out 中子筋本数);
                    int.TryParse(端部1の中子筋径TEXTBOX.Text, out 中子筋径);
                    int.TryParse(端部1の腹筋本数TEXTBOX.Text, out 腹筋本数);
                    int.TryParse(端部1の腹筋径TEXTBOX.Text, out 腹筋径);
                    int.TryParse(端部1の幅止筋径TEXTBOX.Text, out 幅止筋径);
                    double.TryParse(フックの位置COMBOBOX.SelectedIndex.ToString(), out フックの位置);
                    int.TryParse(端部1のスタラップ形COMBOBOX.Text, out スタラップ形);
                    break;
                case "中央":
                    double.TryParse(中央の幅TEXTBOX.Text, out realWidth);
                    double.TryParse(中央の成TEXTBOX.Text, out realHeight);
                    double.TryParse(上TEXTBOX.Text, out 上TEXTBOX_);
                    double.TryParse(下TEXTBOX.Text, out 下TEXTBOX_);
                    double.TryParse(左TEXTBOX.Text, out 左TEXTBOX_);
                    double.TryParse(右TEXTBOX.Text, out 右TEXTBOX_);
                    double.TryParse(中央のスタラップ径TEXTBOX.Text, out スタラップ径);
                    double.TryParse(中央の主筋径COMBOBOX.Text, out 主筋径);
                    double.TryParse(中央の上筋本数TEXTBOX.Text, out 上筋本数);
                    double.TryParse(中央の上宙1TEXTBOX.Text, out 上宙1);
                    double.TryParse(中央の上宙2TEXTBOX.Text, out 上宙2);
                    double.TryParse(中央の下宙1TEXTBOX.Text, out 下宙1);
                    double.TryParse(中央の下宙2TEXTBOX.Text, out 下宙2);
                    double.TryParse(中央の下筋本数TEXTBOX.Text, out 下筋本数);
                    int.TryParse(中央の中子筋形COMBOBOX.Text, out 中子筋形);
                    int.TryParse(中央の中子筋本数TEXTBOX.Text, out 中子筋本数);
                    int.TryParse(中央の中子筋径TEXTBOX.Text, out 中子筋径);
                    int.TryParse(端部1の腹筋本数TEXTBOX.Text, out 腹筋本数);
                    int.TryParse(端部1の腹筋径TEXTBOX.Text, out 腹筋径);
                    int.TryParse(端部1の幅止筋径TEXTBOX.Text, out 幅止筋径);
                    double.TryParse(フックの位置COMBOBOX.SelectedIndex.ToString(), out フックの位置);
                    int.TryParse(中央のスタラップ形COMBOBOX.Text, out スタラップ形);
                    break;
                case "端部2":
                    double.TryParse(端部2の幅TEXTBOX.Text, out realWidth);
                    double.TryParse(端部2のTEXTBOX.Text, out realHeight);
                    double.TryParse(上TEXTBOX.Text, out 上TEXTBOX_);
                    double.TryParse(下TEXTBOX.Text, out 下TEXTBOX_);
                    double.TryParse(左TEXTBOX.Text, out 左TEXTBOX_);
                    double.TryParse(右TEXTBOX.Text, out 右TEXTBOX_);
                    double.TryParse(端部2のスタラップ径TEXTBOX.Text, out スタラップ径);
                    double.TryParse(端部2の主筋径COMBOBOX.Text, out 主筋径);
                    double.TryParse(端部2の上筋本数TEXTBOX.Text, out 上筋本数);
                    double.TryParse(端部2の上宙1TEXTBOX.Text, out 上宙1);
                    double.TryParse(端部2の上宙2TEXTBOX.Text, out 上宙2);
                    double.TryParse(端部2の下宙1TEXTBOX.Text, out 下宙1);
                    double.TryParse(端部2の下宙2TEXTBOX.Text, out 下宙2);
                    double.TryParse(端部2の下筋本数TEXTBOX.Text, out 下筋本数);
                    int.TryParse(端部2の中子筋形COMBOBOX.Text, out 中子筋形);
                    int.TryParse(端部2の中子筋本数TEXTBOX.Text, out 中子筋本数);
                    int.TryParse(端部2の中子筋径TEXTBOX.Text, out 中子筋径);
                    int.TryParse(端部1の腹筋本数TEXTBOX.Text, out 腹筋本数);
                    int.TryParse(端部1の腹筋径TEXTBOX.Text, out 腹筋径);
                    int.TryParse(端部1の幅止筋径TEXTBOX.Text, out 幅止筋径);
                    double.TryParse(フックの位置COMBOBOX.SelectedIndex.ToString(), out フックの位置);
                    int.TryParse(端部2のスタラップ形COMBOBOX.Text, out スタラップ形);
                    break;
            }

            if (realWidth <= 0 || realHeight <= 0)
            {
                MessageBox.Show("幅 または 成 に有効な数値を入力してください。");
                return;
            }

            // Scale để vừa hình trong canvas
            double maxWidth = MyCanvas.ActualWidth * 0.9;
            double maxHeight = MyCanvas.ActualHeight * 0.9;
            double scaleX = maxWidth / realWidth;
            double scaleY = maxHeight / realHeight;
            double scale = Math.Min(scaleX, scaleY);

            double originX = MyCanvas.ActualWidth / 2;
            double originY = (MyCanvas.ActualHeight - realHeight * scale) / 2;

            Brush strokeColor = Brushes.Red;
            double left = -realWidth / 2;
            double right = realWidth / 2;
            double top = 0;
            double bottom = realHeight;
            //MessageBox.Show($"{bottom}");



            DrawLine(MyCanvas, originX, originY, scale, left, top, right, top, strokeColor);
            DrawLine(MyCanvas, originX, originY, scale, right, top, right, bottom, strokeColor);
            DrawLine(MyCanvas, originX, originY, scale, right, bottom, left, bottom, strokeColor);
            DrawLine(MyCanvas, originX, originY, scale, left, bottom, left, top, strokeColor);

            double スタラップ径_changed = 0;
            if (ToActualDiameter.ContainsKey(スタラップ径))
            {
                スタラップ径_changed = ToActualDiameter[スタラップ径];
            }

            Brush strokeColor1 = Brushes.Blue;
            double cornerRadius0 = (スタラップ径 * 2) + スタラップ径_changed;

            //DrawLine(MyCanvas, originX, originY, scale,
            //    left + 左TEXTBOX_ + cornerRadius0,
            //    top + 上TEXTBOX_,
            //    right - 右TEXTBOX_ - cornerRadius0,
            //    top + 上TEXTBOX_, strokeColor1);
            DrawLine(MyCanvas, originX, originY, scale,
                right - 右TEXTBOX_,
                top + 上TEXTBOX_ + cornerRadius0,
                right - 右TEXTBOX_,
                bottom - 下TEXTBOX_ - cornerRadius0, strokeColor1);
            DrawLine(MyCanvas, originX, originY, scale,
                right - 右TEXTBOX_ - cornerRadius0,
                bottom - 下TEXTBOX_,
                left + 左TEXTBOX_ + cornerRadius0,
                bottom - 下TEXTBOX_, strokeColor1);
            DrawLine(MyCanvas, originX, originY, scale,
                left + 左TEXTBOX_,
                bottom - 下TEXTBOX_ - cornerRadius0,
                left + 左TEXTBOX_,
                top + 上TEXTBOX_ + cornerRadius0, strokeColor1);

            //DrawArc(MyCanvas, originX, originY, scale,
            //    left + 左TEXTBOX_ + cornerRadius0,
            //    top + 上TEXTBOX_ + cornerRadius0,
            //    cornerRadius0, 2, strokeColor1);
            //DrawArc(MyCanvas, originX, originY, scale,
            //    right - 右TEXTBOX_ - cornerRadius0,
            //    top + 上TEXTBOX_ + cornerRadius0,
            //    cornerRadius0, 1, strokeColor1);
            DrawArc(MyCanvas, originX, originY, scale,
                right - 右TEXTBOX_ - cornerRadius0,
                bottom - 下TEXTBOX_ - cornerRadius0,
                cornerRadius0, 4, strokeColor1);
            DrawArc(MyCanvas, originX, originY, scale,
                left + 左TEXTBOX_ + cornerRadius0,
                bottom - 下TEXTBOX_ - cornerRadius0,
                cornerRadius0, 3, strokeColor1);

            double cornerRadius = スタラップ径 * 2;

            //DrawLine(MyCanvas, originX, originY, scale,
            //    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
            //    top + 上TEXTBOX_ + スタラップ径_changed,
            //    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
            //    top + 上TEXTBOX_ + スタラップ径_changed, strokeColor1);
            DrawLine(MyCanvas, originX, originY, scale,
                right - 右TEXTBOX_ - スタラップ径_changed,
                top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                right - 右TEXTBOX_ - スタラップ径_changed,
                bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius, strokeColor1);
            DrawLine(MyCanvas, originX, originY, scale,
                right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                bottom - 下TEXTBOX_ - スタラップ径_changed,
                left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                bottom - 下TEXTBOX_ - スタラップ径_changed, strokeColor1);
            DrawLine(MyCanvas, originX, originY, scale,
                left + 左TEXTBOX_ + スタラップ径_changed,
                bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius,
                left + 左TEXTBOX_ + スタラップ径_changed,
                top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius, strokeColor1);

            //DrawArc(MyCanvas, originX, originY, scale,
            //    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
            //    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
            //    cornerRadius, 2, strokeColor1);
            //DrawArc(MyCanvas, originX, originY, scale,
            //    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
            //    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
            //    cornerRadius, 1, strokeColor1);
            DrawArc(MyCanvas, originX, originY, scale,
                right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius,
                cornerRadius, 4, strokeColor1);
            DrawArc(MyCanvas, originX, originY, scale,
                left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius,
                cornerRadius, 3, strokeColor1);

            Brush strokeColor2 = Brushes.Red;
            if (スタラップ形 == 1 || スタラップ形 == 2)
            {
                DrawLine(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_,
                    right - 右TEXTBOX_ - cornerRadius0,
                    top + 上TEXTBOX_, strokeColor1);
                DrawArc(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    cornerRadius0, 2, strokeColor1);
                DrawLine(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed, strokeColor1);
                DrawArc(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, 2, strokeColor1);
                DrawArc(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    cornerRadius0, 1, strokeColor1);
                DrawArc(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, 1, strokeColor1);
            }
            else if (スタラップ形 == 9)

            {
                DrawLine(MyCanvas, originX, originY, scale,
                left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_,
                    right - 右TEXTBOX_ - cornerRadius0,
                    top + 上TEXTBOX_, strokeColor1);
                DrawArc(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    cornerRadius0, 2, strokeColor1);
                DrawArc(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    cornerRadius0, 1, strokeColor1);
                DrawLine(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed, strokeColor1);
                DrawArc(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, 2, strokeColor1);
                DrawArc(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, 1, strokeColor1);
                double y_left = (bottom + 上TEXTBOX_ - 下TEXTBOX_);
                if (フックの位置 == 0)
                {
                    DrawCircle1(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed / 2,
                    y_left / 2,
                    スタラップ径_changed, strokeColor1);
                    DrawCircle1(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed / 2,
                    y_left / 2,
                    スタラップ径_changed / 2, strokeColor1);
                }
                else if (フックの位置 == 2)
                {
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        right - 右TEXTBOX_ - スタラップ径_changed / 2,
                        y_left / 2,
                        スタラップ径_changed, strokeColor1);
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        right - 右TEXTBOX_ - スタラップ径_changed / 2,
                        y_left / 2,
                        スタラップ径_changed / 2, strokeColor1);
                }

                else if (フックの位置 == 1)
                {
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        0,
                        bottom - 下TEXTBOX_ - スタラップ径_changed / 2,
                        スタラップ径_changed, strokeColor1);
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        0,
                        bottom - 下TEXTBOX_ - スタラップ径_changed / 2,
                        スタラップ径_changed / 2, strokeColor1);
                }
                else if (フックの位置 == 3)
                {
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        0,
                        上TEXTBOX_ + スタラップ径_changed / 2,
                        スタラップ径_changed, strokeColor1);
                    DrawCircle1(MyCanvas, originX, originY, scale,
                        0,
                        上TEXTBOX_ + スタラップ径_changed / 2,
                        スタラップ径_changed / 2, strokeColor1);
                }



            }
            else
            {
                int left_スタラップ_形 = 0;
                int right_スタラップ_形 = 0;
                int left_フック_形 = 0;
                int right_フック_形 = 0;
                if (スタラップ形 == 3)
                {
                    left_スタラップ_形 = 4;
                    right_スタラップ_形 = 4;
                    left_フック_形 = 2;
                    right_フック_形 = 13;
                }
                else if (スタラップ形 == 4)
                {
                    left_スタラップ_形 = 4;
                    right_スタラップ_形 = 4;
                    left_フック_形 = 2;
                    right_フック_形 = 14;
                }
                else if (スタラップ形 == 5)
                {
                    left_スタラップ_形 = 1;
                    right_スタラップ_形 = 1;
                    left_フック_形 = 2;
                    right_フック_形 = 13;
                }
                else if (スタラップ形 == 6)
                {
                    left_スタラップ_形 = 1;
                    right_スタラップ_形 = 1;
                    left_フック_形 = 2;
                    right_フック_形 = 14;
                }
                else if (スタラップ形 == 7)
                {
                    left_スタラップ_形 = 4;
                    right_スタラップ_形 = 4;
                    left_フック_形 = 4;
                    right_フック_形 = 14;
                }
                else if (スタラップ形 == 8)
                {
                    left_スタラップ_形 = 1;
                    right_スタラップ_形 = 1;
                    left_フック_形 = 4;
                    right_フック_形 = 14;
                }
                //else if (スタラップ形 == 10)
                //{
                //    left_スタラップ_形 = 1;
                //    right_スタラップ_形 = 1;
                //    left_フック_形 = 4;
                //    right_フック_形 = 14;
                //}
                DrawArc_スタラップ(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    cornerRadius0, left_スタラップ_形, スタラップ径, strokeColor1, 0, false);
                DrawArc_スタラップ(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, left_スタラップ_形, スタラップ径, strokeColor1, 0, false);
                line_スタラップ(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, cornerRadius0, left_スタラップ_形, スタラップ径, strokeColor1, 0, false);
                DrawArc_スタラップ(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    cornerRadius0, right_スタラップ_形, スタラップ径, strokeColor1, 1, true);
                DrawArc_スタラップ(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, right_スタラップ_形, スタラップ径, strokeColor1, 1, true);
                line_スタラップ(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    right - 右TEXTBOX_ - cornerRadius0,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, cornerRadius0, right_スタラップ_形, スタラップ径, strokeColor1, 1, true);
                // フック //
                DrawLine(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_,
                    right - 右TEXTBOX_ - cornerRadius0,
                    top + 上TEXTBOX_, strokeColor2);
                DrawLine(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed, strokeColor2);

                DrawArc_フック(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                     top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius0, left_フック_形, スタラップ径, strokeColor2, 1);
                DrawArc_フック(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, left_フック_形, スタラップ径, strokeColor2, 1);
                line_フック(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius0, cornerRadius, left_フック_形, スタラップ径, strokeColor2, 1);

                DrawArc_フック(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - cornerRadius0,
                     top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius0, right_フック_形, スタラップ径, strokeColor2, 0);
                DrawArc_フック(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, right_フック_形, スタラップ径, strokeColor2, 0);
                line_フック(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - cornerRadius0,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius0, cornerRadius, right_フック_形, スタラップ径, strokeColor2, 0);

                // END フック //
            }

            ////// DrawArc_HOOP //////
            int スタラップ形1 = 0;
            int スタラップ形2 = 0;
            int スタラップ形3 = 0;
            int スタラップ形4 = 0;
            ///// Lấy giá trị HOOP形Text theo beamType
            // Sử dụng hoopShapeText cho logic vẽ
            if (スタラップ形 == 1)
            {

                スタラップ形1 = 1;
                スタラップ形2 = 5;
                スタラップ形3 = 9;
                スタラップ形4 = 13;

            }
            else if (スタラップ形 == 2)
            {

                スタラップ形1 = 2;
                スタラップ形2 = 6;
                スタラップ形3 = 10;
                スタラップ形4 = 14;

            }
            Brush strokeColor3 = Brushes.Blue;
            if (フックの位置 == 0 && (スタラップ形 == 1 || スタラップ形 == 2))
            {
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    cornerRadius0, スタラップ形1, スタラップ径, strokeColor3);
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, スタラップ形1, スタラップ径, strokeColor3);
                // 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn.
                line_HOOP(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius0, cornerRadius, スタラップ形1, スタラップ径, strokeColor3);
            }
            if (フックの位置 == 1 && (スタラップ形 == 1 || スタラップ形 == 2))
            {
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    cornerRadius0, スタラップ形2, スタラップ径, strokeColor3);
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius, スタラップ形2, スタラップ径, strokeColor3);
                // 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn.
                line_HOOP(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + cornerRadius0,
                    top + 上TEXTBOX_ + cornerRadius0,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    cornerRadius0, cornerRadius, スタラップ形2, スタラップ径, strokeColor3);
            }
            if (フックの位置 == 2 && (スタラップ形 == 1 || スタラップ形 == 2))
            {
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + cornerRadius0,
                    bottom - 下TEXTBOX_ - cornerRadius0,
                    cornerRadius0, スタラップ形3, スタラップ径, strokeColor3);
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    cornerRadius, スタラップ形3, スタラップ径, strokeColor3);
                // 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn.
                line_HOOP(MyCanvas, originX, originY, scale,
                    left + 左TEXTBOX_ + cornerRadius0,
                    bottom - 下TEXTBOX_ - cornerRadius0,
                    left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    cornerRadius0, cornerRadius, スタラップ形3, スタラップ径, strokeColor3);
            }
            if (フックの位置 == 3 && (スタラップ形 == 1 || スタラップ形 == 2))
            {
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - cornerRadius0,
                    bottom - 下TEXTBOX_ - cornerRadius0,
                    cornerRadius0, スタラップ形4, スタラップ径, strokeColor3);
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    cornerRadius, スタラップ形4, スタラップ径, strokeColor3);
                // 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn.
                line_HOOP(MyCanvas, originX, originY, scale,
                    right - 右TEXTBOX_ - cornerRadius0,
                    bottom - 下TEXTBOX_ - cornerRadius0,
                    right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius,
                    cornerRadius0, cornerRadius, スタラップ形4, スタラップ径, strokeColor3);
            }
            //////////// end drawArc Hoop /////////////////



            double 主筋径_changd = 0;
            if (ToActualDiameter.ContainsKey(主筋径))
            {
                主筋径_changd = ToActualDiameter[主筋径];
            }
            ///////////// draw 上筋本数 /////////////////////////////
            double spacing = ((right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius) - (left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius)) / (上筋本数 - 1);
            double y_top_rebar = top + 上TEXTBOX_ + スタラップ径_changed + 主筋径_changd / 2;
            List<double> 縦向き_Dimension = new List<double>();
            縦向き_Dimension.Add(top); //縦向き_Dimension
            List<double> rebarXPositions = new List<double>();
            rebarXPositions.Add(left);
            for (int i = 0; i < 上筋本数; i++)
            {
                double x_offset = 0;
                double y_offset = 0;
                // Use per-index offset if available
                if (data.上筋左右Offsets.TryGetValue(i, out string xStr))
                    double.TryParse(xStr, out x_offset);

                if (data.上筋上下Offsets.TryGetValue(i, out string yStr))
                    double.TryParse(yStr, out y_offset);

                double y_上筋本数 = y_top_rebar + y_offset;

                if (i > 0)
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius) + (spacing * i) + x_offset;
                    rebarXPositions.Add(x);
                    DrawCircle(MyCanvas, originX, originY, scale, x, y_上筋本数, 主筋径_changd / 2, Brushes.Black);
                }
                else
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius + x_offset);
                    rebarXPositions.Add(x);
                    DrawCircle(MyCanvas, originX, originY, scale, x, y_上筋本数, 主筋径_changd / 2, Brushes.Black);
                    縦向き_Dimension.Add(y_上筋本数);
                }
            }

            rebarXPositions.Add(right);
            //中子筋//
            /////////////////////////////////////

            if (中子筋形 == 6 || 中子筋形 == 7)
            {
                中子筋_方向.IsEnabled = false;
            }
            else
            {
                中子筋_方向.IsEnabled = true;
            }

            double 中子筋径_changed = 0;
            if (ToActualDiameter.ContainsKey(中子筋径))
            {
                中子筋径_changed = ToActualDiameter[中子筋径];
            }
            double cornerRadius_中子筋径_3 = (中子筋径 * 2) + 中子筋径_changed;
            double cornerRadius_中子筋径_2 = 中子筋径 * 2;

            // Lấy danh sách vị trí cốt thép trên (上筋) để làm cơ sở
            List<double> newXCoords_中子筋本数 = rebarXPositions.Skip(2).Take(rebarXPositions.Count - 4).ToList(); // Bỏ 2 vị trí đầu và 2 vị trí cuối
            // Vẽ từng thanh 中子筋 dựa trên số lượng và vị trí được chọn
            int.TryParse(data.中子筋の数COMBOBOX, out int selectedItem);
            int selectedIndex = 中子筋の数COMBOBOX.SelectedIndex;


            //int selectedIndex = selectedItem - 1; // index cua chỉ số đã chọn
            if (中子筋形 < 6)
            {
                for (int i = 0; i < 中子筋本数; i++)
                {
                    int positionIndex = data.NakagoCustomPositions.ContainsKey(i) ? data.NakagoCustomPositions[i] : i;
                    if (positionIndex < 0 || positionIndex >= newXCoords_中子筋本数.Count) continue;

                    Brush color = (i == selectedIndex) ? Brushes.Gold : Brushes.Black;
                    bool direction = data.NakagoDirections.ContainsKey(i) ? data.NakagoDirections[i] : false; // data.中子筋_方向; // Sử dụng giá trị boolean mới  

                    double x = newXCoords_中子筋本数[positionIndex];
                    int 中子方向_1 = (int)((中子筋形 >= 1 && 中子筋形 <= 4) ? 中子筋形 : 5);
                    int 中子方向_2 = 10 + 中子方向_1;
                    int check_右 = direction ? 1 : 0;
                    int check_左 = direction ? 0 : 1;
                    int check_左_右 = direction ? 1 : -1;

                    // Vẽ cung tròn trên
                    DrawArc_中子筋(MyCanvas, originX, originY, scale,
                        x,
                        top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_3 - 中子筋径_changed,
                        cornerRadius_中子筋径_3, 中子方向_1, 中子筋径, color, check_右, direction);
                    DrawArc_中子筋(MyCanvas, originX, originY, scale,
                        x,
                        top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_2,
                        cornerRadius_中子筋径_2, 中子方向_1, 中子筋径, color, check_右, direction);
                    line_中子筋(MyCanvas, originX, originY, scale,
                        x,
                        top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_3 - 中子筋径_changed,
                        x,
                        top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_2,
                        cornerRadius_中子筋径_3, cornerRadius_中子筋径_2, 中子方向_1, 中子筋径, color, check_右, direction);

                    // Vẽ cung tròn dưới
                    DrawArc_中子筋(MyCanvas, originX, originY, scale,
                        x,
                        bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_3 + 中子筋径_changed,
                        cornerRadius_中子筋径_3, 中子方向_2, 中子筋径, color, check_左, direction);
                    DrawArc_中子筋(MyCanvas, originX, originY, scale,
                        x,
                        bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_2,
                        cornerRadius_中子筋径_2, 中子方向_2, 中子筋径, color, check_左, direction);
                    line_中子筋(MyCanvas, originX, originY, scale,
                        x,
                        bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_3 + 中子筋径_changed,
                        x,
                        bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_2,
                        cornerRadius_中子筋径_3, cornerRadius_中子筋径_2, 中子方向_2, 中子筋径, color, check_左, direction);

                    // Vẽ đường thẳng nối hai cung tròn
                    DrawLine(MyCanvas, originX, originY, scale,
                        x + (check_左_右 * cornerRadius_中子筋径_3),
                        top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_3 - 中子筋径_changed,
                        x + (check_左_右 * cornerRadius_中子筋径_3),
                        bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_3 + 中子筋径_changed,
                        color);
                    DrawLine(MyCanvas, originX, originY, scale,
                        x + (check_左_右 * cornerRadius_中子筋径_2),
                        top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_2,
                        x + (check_左_右 * cornerRadius_中子筋径_2),
                        bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_2,
                        color);
                }
            }

            else if (中子筋形 == 6 || 中子筋形 == 7)
            {
                int フック形_ = 1;
                if (中子筋形 == 6) { フック形_ = 1; }
                else if (中子筋形 == 7) { フック形_ = 2; }
                int i = 0;
                int positionIndex = data.NakagoCustomPositions.ContainsKey(i)
                    ? data.NakagoCustomPositions[i] : i;
                int nextPositionIndex = data.NakagoCustomPositions.ContainsKey(i + 1)
                    ? data.NakagoCustomPositions[i + 1] : i + 1;
                if (positionIndex < 0 || positionIndex >= newXCoords_中子筋本数.Count
                    || nextPositionIndex < 0 || nextPositionIndex >= newXCoords_中子筋本数.Count) { return; }
                Brush color = (i == selectedIndex) ? Brushes.Black : Brushes.Black;
                //bool direction = gridBotData[beamType].NakagoDirections.ContainsKey(i) ? gridBotData[beamType].NakagoDirections[i] : false; // data.中子筋_方向; // Sử dụng giá trị boolean mới  
                // Lấy tọa độ x
                double xStart = newXCoords_中子筋本数[positionIndex];
                double xEnd = newXCoords_中子筋本数[nextPositionIndex];

                int 中子方向_1 = 5;
                int 中子方向_2 = 10 + 中子方向_1;

                DrawLine(MyCanvas, originX, originY, scale,
                    xStart,
                    top + 上TEXTBOX_ + スタラップ径_changed - 中子筋径_changed,
                    xEnd,
                    top + 上TEXTBOX_ + スタラップ径_changed - 中子筋径_changed,
                    color);
                DrawLine(MyCanvas, originX, originY, scale,
                    xStart,
                    top + 上TEXTBOX_ + スタラップ径_changed,
                    xEnd,
                    top + 上TEXTBOX_ + スタラップ径_changed,
                    color);
                DrawArc(MyCanvas, originX, originY, scale,
                    xStart,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_3 - 中子筋径_changed,
                    cornerRadius_中子筋径_3, 2, color);

                DrawArc(MyCanvas, originX, originY, scale,
                    xStart,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_2,
                    cornerRadius_中子筋径_2, 2, color);
                DrawLine(MyCanvas, originX, originY, scale,
                    xStart,
                    bottom - 下TEXTBOX_ - スタラップ径_changed,
                    xEnd,
                    bottom - 下TEXTBOX_ - スタラップ径_changed, color);
                DrawLine(MyCanvas, originX, originY, scale,
                    xStart,
                    bottom - 下TEXTBOX_ - スタラップ径_changed + 中子筋径_changed,
                    xEnd,
                    bottom - 下TEXTBOX_ - スタラップ径_changed + 中子筋径_changed, color);
                DrawArc(MyCanvas, originX, originY, scale,
                    xStart,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_3 + 中子筋径_changed,
                    cornerRadius_中子筋径_3, 3, color);
                DrawArc(MyCanvas, originX, originY, scale,
                    xStart,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_2,
                    cornerRadius_中子筋径_2, 3, color);
                ////Vẽ đường thẳng nối hai cung tròn
                DrawLine(MyCanvas, originX, originY, scale,
                    xStart - cornerRadius_中子筋径_3,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_3 - 中子筋径_changed,
                    xStart - cornerRadius_中子筋径_3,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_3 + 中子筋径_changed,
                    color);
                DrawLine(MyCanvas, originX, originY, scale,
                    xStart - cornerRadius_中子筋径_2,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_2,
                    xStart - cornerRadius_中子筋径_2,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_2,
                    color);
                ////right//
                DrawArc(MyCanvas, originX, originY, scale,
                    xEnd,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_3 - 中子筋径_changed,
                    cornerRadius_中子筋径_3, 1, color);
                DrawArc(MyCanvas, originX, originY, scale,
                    xEnd,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_2,
                    cornerRadius_中子筋径_2, 1, color);

                DrawArc(MyCanvas, originX, originY, scale,
                    xEnd,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_2,
                    cornerRadius_中子筋径_2, 4, color);
                DrawArc(MyCanvas, originX, originY, scale,
                    xEnd,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_3 + 中子筋径_changed,
                    cornerRadius_中子筋径_3, 4, color);
                //
                DrawLine(MyCanvas, originX, originY, scale,
                    xEnd + cornerRadius_中子筋径_2,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_2,
                    xEnd + cornerRadius_中子筋径_2,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_2, color);
                DrawLine(MyCanvas, originX, originY, scale,
                    xEnd + cornerRadius_中子筋径_2 + 中子筋径_changed,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_2,
                    xEnd + cornerRadius_中子筋径_2 + 中子筋径_changed,
                    bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius_中子筋径_2, color);
                //// フック //
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    xEnd,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_3 - 中子筋径_changed,
                    cornerRadius_中子筋径_3, フック形_, 中子筋径, color);
                DrawArc_HOOP(MyCanvas, originX, originY, scale,
                    xEnd,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_2,
                    cornerRadius_中子筋径_2, フック形_, 中子筋径, color);
                //// 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn.
                line_HOOP(MyCanvas, originX, originY, scale,
                    xEnd,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_3 - 中子筋径_changed,
                    xEnd,
                    top + 上TEXTBOX_ + スタラップ径_changed + cornerRadius_中子筋径_2,
                    cornerRadius_中子筋径_3, cornerRadius_中子筋径_2, フック形_, 中子筋径, color);
            }            ////////////////////////////////////////
            if (上筋本数 > 0)
            {
                DrawRebarDimensions_X(MyCanvas, originX, originY, scale, rebarXPositions, y_top_rebar, 38, 0, 0, Brushes.Green);
            }
            var list_梁主筋の場合 = new Dictionary<double, Dictionary<double, double>>
                {
                    { 10, new Dictionary<double, double> { {19, 59.5}, {22, 62}, {25, 65.5}, {29, 76.5}, {32, 84}, {35, 92.5} } },
                    { 13, new Dictionary<double, double> { {19, 71.5}, {22, 75}, {25, 76.5}, {29, 80.5}, {32, 84}, {35, 92.5}, {38, 100}, {41, 107.5} } },
                    { 16, new Dictionary<double, double> { {19, 86.5}, {22, 89}, {25, 91.5}, {29, 94.5}, {32, 97}, {35, 99.5}, {38, 102}, {41, 107.5} } }
                };
            ///////////// draw 上宙1 /////////////////////////////
            double location_梁主筋 = 0;
            if (list_梁主筋の場合.ContainsKey(スタラップ径))
            {
                var subDict = list_梁主筋の場合[スタラップ径];
                if (subDict.ContainsKey(主筋径))
                {
                    location_梁主筋 = subDict[主筋径];
                }
                else
                {
                    Console.WriteLine("直径筋主筋は適切ではありません");
                }
            }
            else
            {
                Console.WriteLine("あばら筋の直径が適切ではありません");
            }

            double spacing_上宙1 = ((right - 右TEXTBOX_ - スタラップ径_changed) - (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd)) / (上宙1 - 1);
            double y_top_rebar_上宙1 = top + 上TEXTBOX_ + スタラップ径_changed + 主筋径_changd / 2 + location_梁主筋;
            List<double> rebarXPositions_上宙1 = new List<double>();
            rebarXPositions_上宙1.Add(left);
            for (int i = 0; i < 上宙1; i++)
            {
                if (i > 0)
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd / 2) + (spacing_上宙1 * i);
                    rebarXPositions_上宙1.Add(x);
                }
                if (i == 0) //上宙1
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd / 2);
                    rebarXPositions_上宙1.Add(x);
                    縦向き_Dimension.Add(y_top_rebar_上宙1); //縦向き_Dimension
                }
                //rebarYPositions_上宙1.Add(y_top_rebar_上宙1);
            }
            rebarXPositions_上宙1.Add(right);

            List<double> newXCoords_上宙1 = AswapB(rebarXPositions, rebarXPositions_上宙1);
            List<double> XPositions_上宙1 = new List<double>();
            XPositions_上宙1.Add(left);
            for (int i = 0; i < newXCoords_上宙1.Count; i++)
            {
                double x_offset = 0;
                double y_offset = 0;
                // 上宙1
                if (data.上宙1左右Offsets.TryGetValue(i, out string xStr1))
                    double.TryParse(xStr1, out x_offset);
                if (data.上宙1上下Offsets.TryGetValue(i, out string yStr1))
                    double.TryParse(yStr1, out y_offset);


                double x = newXCoords_上宙1[i];
                XPositions_上宙1.Add(x + x_offset);

                DrawCircle(MyCanvas, originX, originY, scale, x + x_offset, y_top_rebar_上宙1 + y_offset, 主筋径_changd / 2, Brushes.Black);
            }

            XPositions_上宙1.Add(right);
            // kích thước

            if (上宙1 > 0)
            {

                List<double> newXCoords_上宙1_dimension = Aswap_dimension_lineB(rebarXPositions, XPositions_上宙1);
                DrawRebarDimensions_X(MyCanvas, originX, originY, scale, newXCoords_上宙1_dimension, y_top_rebar_上宙1, 25, 0, 0, Brushes.Green);
            }

            ///////////// draw 上宙2 /////////////////////////////
            double spacing_上宙2 = ((right - 右TEXTBOX_ - スタラップ径_changed) - (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd)) / (上宙2 - 1);
            double y_top_rebar_上宙2 = top + 上TEXTBOX_ + スタラップ径_changed + 主筋径_changd / 2 + location_梁主筋 * 2;
            List<double> rebarXPositions_上宙2 = new List<double>();
            List<double> XPositions_上宙2 = new List<double>();
            rebarXPositions_上宙2.Add(left);
            for (int i = 0; i < 上宙2; i++)
            {
                if (i > 0)
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd / 2) + (spacing_上宙2 * i);
                    rebarXPositions_上宙2.Add(x);
                }
                else
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd / 2);
                    rebarXPositions_上宙2.Add(x);
                    縦向き_Dimension.Add(y_top_rebar_上宙2); //縦向き_Dimension

                }
            }
            rebarXPositions_上宙2.Add(right);
            XPositions_上宙2.Add(left);
            List<double> newXCoords_上宙2 = AswapB(rebarXPositions, rebarXPositions_上宙2);
            for (int i = 0; i < newXCoords_上宙2.Count; i++)
            {
                double x_offset = 0;
                double y_offset = 0;
                // 上宙2
                if (data.上宙2左右Offsets.TryGetValue(i, out string xStr2))
                    double.TryParse(xStr2, out x_offset);
                if (data.上宙2上下Offsets.TryGetValue(i, out string yStr2))
                    double.TryParse(yStr2, out y_offset);


                double x = newXCoords_上宙2[i];
                XPositions_上宙2.Add(x + x_offset);

                DrawCircle(MyCanvas, originX, originY, scale, x + x_offset, y_top_rebar_上宙2 + y_offset, 主筋径_changd / 2, Brushes.Black);
            }

            XPositions_上宙2.Add(right);
            // kích thước
            if (上宙2 > 0)
            {
                List<double> newXCoords_上宙2_dimension = Aswap_dimension_lineB(rebarXPositions, XPositions_上宙2);
                DrawRebarDimensions_X(MyCanvas, originX, originY, scale, newXCoords_上宙2_dimension, y_top_rebar_上宙2, 25, 0, 0, Brushes.Green);
            }
            ///////////// draw spacing_下宙2 /////////////////////////////
            double spacing_下宙2 = ((right - 右TEXTBOX_ - スタラップ径_changed) - (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd)) / (下宙2 - 1);
            double y_bottom_rebar_下宙2 = bottom - 下TEXTBOX_ - スタラップ径_changed - 主筋径_changd / 2 - location_梁主筋 * 2;
            List<double> rebarXPositions_下宙2 = new List<double>();

            rebarXPositions_下宙2.Add(left);
            for (int i = 0; i < 下宙2; i++)
            {
                if (i > 0)
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd / 2) + (spacing_下宙2 * i);
                    rebarXPositions_下宙2.Add(x);
                }
                else
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd / 2);
                    rebarXPositions_下宙2.Add(x);
                    縦向き_Dimension.Add(y_bottom_rebar_下宙2); //縦向き_Dimension
                }
            }
            rebarXPositions_下宙2.Add(right);

            List<double> newXCoords_下宙2 = AswapB(rebarXPositions, rebarXPositions_下宙2);
            List<double> XPositions_下宙2 = new List<double>();
            XPositions_下宙2.Add(left);
            for (int i = 0; i < newXCoords_下宙2.Count; i++)
            {
                double x_offset = 0;
                double y_offset = 0;
                // 下宙2
                if (data.下宙2左右Offsets.TryGetValue(i, out string xStr4))
                    double.TryParse(xStr4, out x_offset);
                if (data.下宙2上下Offsets.TryGetValue(i, out string yStr4))
                    double.TryParse(yStr4, out y_offset);


                double x = newXCoords_下宙2[i];
                XPositions_下宙2.Add(x + x_offset);

                DrawCircle(MyCanvas, originX, originY, scale, x + x_offset, y_bottom_rebar_下宙2 + y_offset, 主筋径_changd / 2, Brushes.Black);
            }

            XPositions_下宙2.Add(right);
            // kích thước
            if (下宙2 > 0)
            {
                List<double> newXCoords_下宙2_dimension = Aswap_dimension_lineB(rebarXPositions, XPositions_下宙2);
                DrawRebarDimensions_X(MyCanvas, originX, originY, scale, newXCoords_下宙2_dimension, y_bottom_rebar_下宙2, 25, 0, 0, Brushes.Green);
            }

            double spacing_下宙1 = ((right - 右TEXTBOX_ - スタラップ径_changed) - (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd)) / (下宙1 - 1);
            double y_bottom_rebar_下宙1 = bottom - 下TEXTBOX_ - スタラップ径_changed - 主筋径_changd / 2 - location_梁主筋;
            List<double> rebarXPositions_下宙1 = new List<double>();

            rebarXPositions_下宙1.Add(left);
            for (int i = 0; i < 下宙1; i++)
            {
                if (i > 0)
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd / 2) + (spacing_下宙1 * i);
                    rebarXPositions_下宙1.Add(x);
                }
                else
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + 主筋径_changd / 2);
                    rebarXPositions_下宙1.Add(x);
                    縦向き_Dimension.Add(y_bottom_rebar_下宙1); //縦向き_Dimension
                }
            }
            rebarXPositions_下宙1.Add(right);

            List<double> newXCoords_下宙1 = AswapB(rebarXPositions, rebarXPositions_下宙1);
            List<double> XPositions_下宙1 = new List<double>();
            XPositions_下宙1.Add(left);
            for (int i = 0; i < newXCoords_下宙1.Count; i++)
            {
                double x_offset = 0;
                double y_offset = 0;
                // 下宙1
                if (data.下宙1左右Offsets.TryGetValue(i, out string xStr3))
                    double.TryParse(xStr3, out x_offset);
                if (data.下宙1上下Offsets.TryGetValue(i, out string yStr3))
                    double.TryParse(yStr3, out y_offset);



                double x = newXCoords_下宙1[i];
                XPositions_下宙1.Add(x + x_offset);
                DrawCircle(MyCanvas, originX, originY, scale, x + x_offset, y_bottom_rebar_下宙1 + y_offset, 主筋径_changd / 2, Brushes.Black);
            }

            XPositions_下宙1.Add(right);
            // kích thước
            if (下宙1 > 0)
            {
                List<double> newXCoords_下宙1_dimension = Aswap_dimension_lineB(rebarXPositions, XPositions_下宙1);
                DrawRebarDimensions_X(MyCanvas, originX, originY, scale, newXCoords_下宙1_dimension, y_bottom_rebar_下宙1, 25, 0, 0, Brushes.Green);
            }
            ///////////// draw 下筋本数 /////////////////////////////
            double spacing_下筋本数 = ((right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius) - (left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius)) / (下筋本数 - 1);
            double y_bottom_rebar_下筋本数 = bottom - 下TEXTBOX_ - スタラップ径_changed - 主筋径_changd / 2;
            List<double> rebarXPositions_下筋本数 = new List<double>();
            List<double> XPositions_下筋本数 = new List<double>();
            rebarXPositions_下筋本数.Add(left);
            for (int i = 0; i < 下筋本数; i++)
            {
                if (i > 0)
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius) + (spacing_下筋本数 * i);
                    rebarXPositions_下筋本数.Add(x);
                }
                else
                {
                    double x = (left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius);
                    rebarXPositions_下筋本数.Add(x);
                    縦向き_Dimension.Add(y_bottom_rebar_下筋本数); //縦向き_Dimension
                }
            }
            rebarXPositions_下筋本数.Add(right);
            縦向き_Dimension.Add(bottom); //縦向き_Dimension
            List<double> newXCoords_下筋本数 = AswapB(rebarXPositions, rebarXPositions_下筋本数);
            XPositions_下筋本数.Add(left);
            for (int i = 0; i < newXCoords_下筋本数.Count; i++)
            {
                double x_offset = 0;
                double y_offset = 0;
                // 下筋
                if (data.下筋左右Offsets.TryGetValue(i, out string xStr5))
                    double.TryParse(xStr5, out x_offset);
                if (data.下筋上下Offsets.TryGetValue(i, out string yStr5))
                    double.TryParse(yStr5, out y_offset);



                double x = newXCoords_下筋本数[i];
                XPositions_下筋本数.Add(x + x_offset);
                DrawCircle(MyCanvas, originX, originY, scale, x + x_offset, y_bottom_rebar_下筋本数 + y_offset, 主筋径_changd / 2, Brushes.Black);
            }

            XPositions_下筋本数.Add(right);
            // kích thước
            if (下筋本数 > 0)
            {
                List<double> newXCoords_下筋本数_dimension = Aswap_dimension_lineB(rebarXPositions, XPositions_下筋本数);
                DrawRebarDimensions_X(MyCanvas, originX, originY, scale, newXCoords_下筋本数_dimension, y_bottom_rebar_下筋本数, 25, 0, 0, Brushes.Green);
            }
            // Vẽ kích thước tổng thể
            DrawDimension(MyCanvas, originX, originY, scale, left, top - 50 / scale, right, top - 50 / scale, $"{realWidth}", 0, 0, Brushes.Green);
            DrawDimension(MyCanvas, originX, originY, scale, left + 左TEXTBOX_, top - 20 / scale, right - 右TEXTBOX_, top - 20 / scale, $"{realWidth - 右TEXTBOX_ - 左TEXTBOX_}", 0, 0, Brushes.Green);
            DrawDimension(MyCanvas, originX, originY, scale, left - 50 / scale, top, left - 50 / scale, bottom, $"{realHeight}", 0, 0, Brushes.Green);
            DrawDimension(MyCanvas, originX, originY, scale, left - 20 / scale, top + 上TEXTBOX_, left - 20 / scale, bottom - 下TEXTBOX_, $"{realHeight - 下TEXTBOX_ - 上TEXTBOX_}", 0, 0, Brushes.Green);
            if (スタラップ径_changed > 0)
            {
                DrawDimension(MyCanvas, originX, originY, scale, (left + 左TEXTBOX_),
                    (((bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius) - (top + 上TEXTBOX_ + スタラップ径_changed)) / 2) + (top + 上TEXTBOX_ + スタラップ径_changed) / scale,
                    (left + 左TEXTBOX_ + スタラップ径_changed), (((bottom - 下TEXTBOX_ - スタラップ径_changed - cornerRadius) - (top + 上TEXTBOX_ + スタラップ径_changed)) / 2) + (top + 上TEXTBOX_ + スタラップ径_changed) / scale,
                    $"{スタラップ径_changed}", 0, 0, Brushes.Red);
            }
            if (上TEXTBOX_ > 0)
            {
                DrawDimension(MyCanvas, originX, originY, scale, (0) / scale, top, 0 / scale, 上TEXTBOX_, $"{上TEXTBOX_}", 0, 0, Brushes.Red);
            }
            if (下TEXTBOX_ > 0)
            {
                DrawDimension(MyCanvas, originX, originY, scale, (0) / scale, bottom - 下TEXTBOX_, 0 / scale, bottom, $"{下TEXTBOX_}", 0, 0, Brushes.Red);
            }
            if (左TEXTBOX_ > 0)
            {
                DrawDimension(MyCanvas, originX, originY, scale, left, bottom / 2 / scale, left + 左TEXTBOX_, bottom / 2 / scale, $"{左TEXTBOX_}", 0, 0, Brushes.Red);
            }
            if (右TEXTBOX_ > 0)
            {
                DrawDimension(MyCanvas, originX, originY, scale, right - 右TEXTBOX_, bottom / 2 / scale, right, bottom / 2 / scale, $"{右TEXTBOX_}", 0, 0, Brushes.Red);
            }
            if (上筋本数 > 0)
            {
                DrawRebarDimensions_Y(MyCanvas, originX, originY, scale, right, 縦向き_Dimension, 主筋径_changd, 0, 0, Brushes.Green);
            }

            //// DrawArc 腹筋　///////////////
            double 腹筋径_changd = 0;
            if (ToActualDiameter.ContainsKey(腹筋径))
            {
                腹筋径_changd = ToActualDiameter[腹筋径];
            }
            double 幅止筋径_changd = 0;
            if (ToActualDiameter.ContainsKey(幅止筋径))
            {
                幅止筋径_changd = ToActualDiameter[幅止筋径];
            }
            double cornerRadius_幅止筋径_2 = 幅止筋径 * 2;
            double cornerRadius_幅止筋径_3 = (幅止筋径 * 2) + 幅止筋径_changd;
            double x_left_rebar = left + 左TEXTBOX_ + スタラップ径_changed + (腹筋径_changd / 2);
            double x_right_rebar = right - 右TEXTBOX_ - スタラップ径_changed - (腹筋径_changd / 2);
            List<double> rebarXPositions_腹筋 = new List<double>();
            //rebarXPositions.Add(left);
            int 腹筋本数_divide_2 = 腹筋本数 / 2;
            //MessageBox.Show($"{腹筋本数_divide_2}");
            if (腹筋本数 % 2 == 0)
            {
                for (int i = 1; i < 腹筋本数_divide_2 + 1; i++)
                {
                    double y_left = ((bottom + 上TEXTBOX_ - 下TEXTBOX_) / (腹筋本数_divide_2 + 1)) * i;
                    DrawCircle(MyCanvas, originX, originY, scale, x_left_rebar, y_left, 腹筋径_changd / 2, Brushes.Black);
                    //rebarXPositions.Add(y);
                    double y_right = ((bottom + 上TEXTBOX_ - 下TEXTBOX_) / (腹筋本数_divide_2 + 1)) * i;
                    DrawCircle(MyCanvas, originX, originY, scale, x_right_rebar, y_right, 腹筋径_changd / 2, Brushes.Black);

                    // Draw_腹筋 //
                    int 中子方向_1 = (int)((中子筋形 >= 1 && 中子筋形 <= 4) ? 中子筋形 : 5);
                    int 中子方向_2 = 10 + 中子方向_1;

                    DrawLine(MyCanvas, originX, originY, scale,
                        left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius_幅止筋径_3 - 幅止筋径_changd,
                        y_left - cornerRadius_幅止筋径_3,
                        right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius_幅止筋径_3 + 幅止筋径_changd,
                        y_right - cornerRadius_幅止筋径_3,
                        Brushes.Black);
                    DrawLine(MyCanvas, originX, originY, scale,
                        left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius_幅止筋径_2,
                        y_left - cornerRadius_幅止筋径_2,
                        right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius_幅止筋径_2,
                        y_right - cornerRadius_幅止筋径_2,
                        Brushes.Black);

                    DrawArc_腹筋(MyCanvas, originX, originY, scale,
                        left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius_幅止筋径_3 - 幅止筋径_changd,
                        y_left,
                        cornerRadius_幅止筋径_3, 中子方向_1, 幅止筋径, Brushes.Black, 1);
                    DrawArc_腹筋(MyCanvas, originX, originY, scale,
                        left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius_幅止筋径_2,
                        y_left,
                        cornerRadius_幅止筋径_2, 中子方向_1, 幅止筋径, Brushes.Black, 1);
                    line_腹筋(MyCanvas, originX, originY, scale,
                        left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius_幅止筋径_3 - 幅止筋径_changd,
                        y_left,
                        left + 左TEXTBOX_ + スタラップ径_changed + cornerRadius_幅止筋径_2,
                        y_left,
                        cornerRadius_幅止筋径_3, cornerRadius_幅止筋径_2, 中子方向_1, 幅止筋径, Brushes.Black, 1);

                    DrawArc_腹筋(MyCanvas, originX, originY, scale,
                        right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius_幅止筋径_3 + 幅止筋径_changd,
                        y_right,
                        cornerRadius_幅止筋径_3, 中子方向_2, 幅止筋径, Brushes.Black, 0);
                    DrawArc_腹筋(MyCanvas, originX, originY, scale,
                        right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius_幅止筋径_2,
                        y_right,
                        cornerRadius_幅止筋径_2, 中子方向_2, 幅止筋径, Brushes.Black, 0);
                    //// 🔽 Vẽ đoạn thẳng ngắn từ đầu và cuối cung tròn.
                    line_腹筋(MyCanvas, originX, originY, scale,
                        right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius_幅止筋径_3 + 幅止筋径_changd,
                        y_right,
                        right - 右TEXTBOX_ - スタラップ径_changed - cornerRadius_幅止筋径_2,
                        y_right,
                        cornerRadius_幅止筋径_3, cornerRadius_幅止筋径_2, 中子方向_2, 幅止筋径, Brushes.Black, 0);

                }
            }

        }
        private void フックの位置COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [フックの位置COMBOBOX.SelectionChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            DrawScaledRectangle();
        }
        private void 中子筋_方向_Checked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [中子筋_方向.Checked] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.中子筋の数COMBOBOX;
            int nakagoIndex = data.Valid数Values.IndexOf(countString);

            data.NakagoDirections[nakagoIndex] = true;
            DrawScaledRectangle();
        }
        private void 中子筋_方向_Unchecked(object sender, RoutedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [中子筋_方向.Unchecked] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.中子筋の数COMBOBOX;
            int nakagoIndex = data.Valid数Values.IndexOf(countString);

            data.NakagoDirections[nakagoIndex] = false;
            DrawScaledRectangle();
        }

        // Khi người dùng thay đổi 中子筋の数
        private void 中子筋の数COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [中子筋の数COMBOBOX.SelectionChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];

            string countString = data.中子筋の数COMBOBOX;
            int nakagoIndex = data.Valid数Values.IndexOf(countString);
            var positionItems = data.Valid中子筋の位置Values;

            // Get the shape ComboBox for the current beamType
            ComboBox nakagoShapeComboBox = null;
            switch (beamType)
            {
                case "端部1": nakagoShapeComboBox = 端部1の中子筋形COMBOBOX; break;
                case "中央": nakagoShapeComboBox = 中央の中子筋形COMBOBOX; break;
                case "端部2": nakagoShapeComboBox = 端部2の中子筋形COMBOBOX; break;
            }

            // Only apply logic if shape is 5 or 6 and count is 2
            if (nakagoShapeComboBox != null &&
                (nakagoShapeComboBox.SelectedIndex == 5 || nakagoShapeComboBox.SelectedIndex == 6) &&
                data.Valid数Values.Count == 2)
            {
                int pos1 = data.NakagoCustomPositions.ContainsKey(1) ? data.NakagoCustomPositions[1] : 1;
                int pos0 = data.NakagoCustomPositions.ContainsKey(0) ? data.NakagoCustomPositions[0] : 0;

                if (nakagoIndex == 0)
                {
                    // When selecting "1", restrict position to <= pos1
                    if (pos0 > pos1)
                    {
                        pos0 = pos1;
                        data.NakagoCustomPositions[0] = pos0;
                    }
                    string selectedPosition = positionItems[pos0];
                    suppressNakagoSelectionChanged = true;
                    data.中子筋の位置COMBOBOX = selectedPosition;
                    suppressNakagoSelectionChanged = false;
                }
                else if (nakagoIndex == 1)
                {
                    // When selecting "2", allow any value
                    string selectedPosition = positionItems[pos1];
                    suppressNakagoSelectionChanged = true;
                    data.中子筋の位置COMBOBOX = selectedPosition;
                    suppressNakagoSelectionChanged = false;
                }
            }
            else
            {
                // Default behavior
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
                suppressNakagoSelectionChanged = true;
                data.中子筋の位置COMBOBOX = selectedPosition;
                suppressNakagoSelectionChanged = false;
            }

            data.中子筋_方向 = data.NakagoDirections[nakagoIndex];
            DrawScaledRectangle();
        }

        // Khi người dùng chọn lại vị trí 中子筋
        private void 中子筋の位置COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || suppressNakagoSelectionChanged) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [中子筋の位置COMBOBOX.SelectionChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];

            string countString = data.中子筋の数COMBOBOX;
            int nakagoIndex = data.Valid数Values.IndexOf(countString);
            string selectedPosition = data.中子筋の位置COMBOBOX;
            int selectedIndex = data.Valid中子筋の位置Values.IndexOf(selectedPosition);

            // Get the shape ComboBox for the current beamType
            ComboBox nakagoShapeComboBox = null;
            switch (beamType)
            {
                case "端部1": nakagoShapeComboBox = 端部1の中子筋形COMBOBOX; break;
                case "中央": nakagoShapeComboBox = 中央の中子筋形COMBOBOX; break;
                case "端部2": nakagoShapeComboBox = 端部2の中子筋形COMBOBOX; break;
            }

            if (nakagoShapeComboBox != null &&
                (nakagoShapeComboBox.SelectedIndex == 5 || nakagoShapeComboBox.SelectedIndex == 6) &&
                data.Valid数Values.Count == 2)
            {
                int pos0 = data.NakagoCustomPositions.ContainsKey(0) ? data.NakagoCustomPositions[0] : 0;
                int pos1 = data.NakagoCustomPositions.ContainsKey(1) ? data.NakagoCustomPositions[1] : 1;

                if (nakagoIndex == 0)
                {
                    // "1" cannot be greater than "2"
                    if (selectedIndex > pos1)
                    {
                        selectedIndex = pos1;
                        suppressNakagoSelectionChanged = true;
                        data.中子筋の位置COMBOBOX = data.Valid中子筋の位置Values[selectedIndex];
                        suppressNakagoSelectionChanged = false;
                    }
                    data.NakagoCustomPositions[0] = selectedIndex;
                }
                else if (nakagoIndex == 1)
                {
                    // "2" cannot be less than "1"
                    if (selectedIndex < pos0)
                    {
                        selectedIndex = pos0;
                        suppressNakagoSelectionChanged = true;
                        data.中子筋の位置COMBOBOX = data.Valid中子筋の位置Values[selectedIndex];
                        suppressNakagoSelectionChanged = false;
                    }
                    data.NakagoCustomPositions[1] = selectedIndex;
                }
            }
            else
            {
                data.NakagoCustomPositions[nakagoIndex] = selectedIndex;
            }

            data.NakagoDirections[nakagoIndex] = data.中子筋_方向;
            DrawScaledRectangle();
        }

        // 上筋
        private void 上筋COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [上筋COMBOBOX.SelectionChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);

            var data = DictGBD[beamType];
            string countString = data.上筋COMBOBOX;
            int index = data.Valid上筋本数Values.IndexOf(countString);

            data.上筋左右 = (index >= 0 && data.上筋左右Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.上筋上下 = (index >= 0 && data.上筋上下Offsets.TryGetValue(index, out string y)) ? y : "0";

            DrawScaledRectangle();
        }
        // 上宙1
        private void 上宙1COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [上宙1COMBOBOX.SelectionChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);

            var data = DictGBD[beamType];
            string countString = data.上宙1COMBOBOX;
            int index = data.Valid上宙1Values.IndexOf(countString);

            data.上宙1左右 = (index >= 0 && data.上宙1左右Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.上宙1上下 = (index >= 0 && data.上宙1上下Offsets.TryGetValue(index, out string y)) ? y : "0";

            DrawScaledRectangle();
        }
        // 上宙2
        private void 上宙2COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [上宙2COMBOBOX.SelectionChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);

            var data = DictGBD[beamType];
            string countString = data.上宙2COMBOBOX;
            int index = data.Valid上宙2Values.IndexOf(countString);

            data.上宙2左右 = (index >= 0 && data.上宙2左右Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.上宙2上下 = (index >= 0 && data.上宙2上下Offsets.TryGetValue(index, out string y)) ? y : "0";

            DrawScaledRectangle();
        }
        // 下宙1
        private void 下宙1COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [下宙1COMBOBOX.SelectionChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);

            var data = DictGBD[beamType];
            string countString = data.下宙1COMBOBOX;
            int index = data.Valid下宙1Values.IndexOf(countString);

            data.下宙1左右 = (index >= 0 && data.下宙1左右Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.下宙1上下 = (index >= 0 && data.下宙1上下Offsets.TryGetValue(index, out string y)) ? y : "0";

            DrawScaledRectangle();
        }
        // 下宙2
        private void 下宙2COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [下宙2COMBOBOX.SelectionChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);

            var data = DictGBD[beamType];
            string countString = data.下宙2COMBOBOX;
            int index = data.Valid下宙2Values.IndexOf(countString);

            data.下宙2左右 = (index >= 0 && data.下宙2左右Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.下宙2上下 = (index >= 0 && data.下宙2上下Offsets.TryGetValue(index, out string y)) ? y : "0";

            DrawScaledRectangle();
        }
        // 下筋
        private void 下筋COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [下筋COMBOBOX.SelectionChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);

            var data = DictGBD[beamType];
            string countString = data.下筋COMBOBOX;
            int index = data.Valid下筋本数Values.IndexOf(countString);

            data.下筋左右 = (index >= 0 && data.下筋左右Offsets.TryGetValue(index, out string x)) ? x : "0";
            data.下筋上下 = (index >= 0 && data.下筋上下Offsets.TryGetValue(index, out string y)) ? y : "0";

            DrawScaledRectangle();
        }

        private void 上筋左右TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [上筋左右TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.上筋COMBOBOX;
            int index = data.Valid上筋本数Values.IndexOf(countString);

            data.上筋左右Offsets[index] = data.上筋左右;

            DrawScaledRectangle();
        }

        private void 上筋上下TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [上筋上下TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.上筋COMBOBOX;
            int index = data.Valid上筋本数Values.IndexOf(countString);

            data.上筋上下Offsets[index] = data.上筋上下;


            DrawScaledRectangle();
        }
        private void 上宙1左右TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [上宙1左右TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.上宙1COMBOBOX;
            int index = data.Valid上宙1Values.IndexOf(countString);

            data.上宙1左右Offsets[index] = data.上宙1左右;

            DrawScaledRectangle();
        }
        private void 上宙1上下TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [上宙1上下TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.上宙1COMBOBOX;
            int index = data.Valid上宙1Values.IndexOf(countString);

            data.上宙1上下Offsets[index] = data.上宙1上下;

            DrawScaledRectangle();
        }
        private void 上宙2左右TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [上宙2左右TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.上宙2COMBOBOX;
            int index = data.Valid上宙2Values.IndexOf(countString);

            data.上宙2左右Offsets[index] = data.上宙2左右;

            DrawScaledRectangle();
        }
        private void 上宙2上下TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [上宙2上下TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.上宙2COMBOBOX;
            int index = data.Valid上宙2Values.IndexOf(countString);

            data.上宙2上下Offsets[index] = data.上宙2上下;

            DrawScaledRectangle();
        }
        private void 下宙1左右TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [下宙1左右TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.下宙1COMBOBOX;
            int index = data.Valid下宙1Values.IndexOf(countString);

            data.下宙1左右Offsets[index] = data.下宙1左右;

            DrawScaledRectangle();
        }
        private void 下宙1上下TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [下宙1上下TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.下宙1COMBOBOX;
            int index = data.Valid下宙1Values.IndexOf(countString);

            data.下宙1上下Offsets[index] = data.下宙1上下;

            DrawScaledRectangle();
        }
        private void 下宙2左右TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [下宙2左右TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.下宙2COMBOBOX;
            int index = data.Valid下宙2Values.IndexOf(countString);

            data.下宙2左右Offsets[index] = data.下宙2左右;

            DrawScaledRectangle();
        }
        private void 下宙2上下TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [下宙2上下TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.下宙2COMBOBOX;
            int index = data.Valid下宙2Values.IndexOf(countString);

            data.下宙2上下Offsets[index] = data.下宙2上下;

            DrawScaledRectangle();
        }
        private void 下筋左右TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [下筋左右TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.下筋COMBOBOX;
            int index = data.Valid下筋本数Values.IndexOf(countString);

            data.下筋左右Offsets[index] = data.下筋左右;

            DrawScaledRectangle();
        }
        private void 下筋上下TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing) return;
            File.AppendAllText("debugdata.txt", $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [下筋上下TEXTBOX.TextChanged] Called: CurrentGBD : {CurrentGBD?.BeamType}" + Environment.NewLine);
            var data = DictGBD[beamType];
            string countString = data.下筋COMBOBOX;
            int index = data.Valid下筋本数Values.IndexOf(countString);

            data.下筋上下Offsets[index] = data.下筋上下;

            DrawScaledRectangle();
        }
        private void 端部1の中子筋形COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || suppressNakagoSelectionChanged) return;

            ValidateNakagoCount("端部1");
        }
        private void 中央の中子筋形COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || suppressNakagoSelectionChanged) return;
            ValidateNakagoCount("中央");
        }
        private void 端部2の中子筋形COMBOBOX_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isInitializing || suppressNakagoSelectionChanged) return;
            ValidateNakagoCount("端部2");
        }
        private void 端部1の中子筋本数TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing || suppressNakagoSelectionChanged) return;
            ValidateNakagoCount("端部1");
        }
        private void 中央の中子筋本数TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing || suppressNakagoSelectionChanged) return;
            ValidateNakagoCount("中央");
        }
        private void 端部2の中子筋本数TEXTBOX_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isInitializing || suppressNakagoSelectionChanged) return;
            ValidateNakagoCount("端部2");
        }

        ////////////////2025 07 29////////////////

    }
}

