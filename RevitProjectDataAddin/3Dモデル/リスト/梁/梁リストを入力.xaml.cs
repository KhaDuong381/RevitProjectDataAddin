using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Autodesk.Revit.DB;

namespace RevitProjectDataAddin
{
    public partial class 梁リストを入力 : Window
    {
        private Document doc;
        private ProjectData _projectData;
        public 梁リストを入力(Document doc)
        {
            InitializeComponent();
            this.doc = doc;
            string selectedProjectName = ProjectManager.SelectedProjectName;
            _projectData = StorageUtils.LoadProject(doc, selectedProjectName);
            DataContext = _projectData.リスト;
            Load();
        }
        public void Load()
        {
            _projectData.SyncHarikaiWithNameKai(); // Đồng bộ danh sách tầng
            DataContext = _projectData.リスト;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (FloorNameListBox.SelectedItem is 梁リスト selectedFloor)
            {
                bool validName = false;
                string columnName = null;

                // Lặp lại cho đến khi nhập tên hợp lệ
                while (!validName)
                {
                    // Hiển thị hộp thoại nhập tên cột mới
                    var inputDialog = new InputDialog("梁の名前を入力してください:");
                    if (inputDialog.ShowDialog() != true)
                    {
                        return; // Người dùng hủy thao tác
                    }

                    columnName = inputDialog.ResponseText;

                    // Kiểm tra tên rỗng
                    if (string.IsNullOrWhiteSpace(columnName))
                    {
                        MessageBox.Show("梁の名前を入力してください。");
                        continue;
                    }

                    // Kiểm tra tên trùng lặp
                    if (selectedFloor.梁.Any(c => c.Name == columnName))
                    {
                        MessageBox.Show($"名前 '{columnName}' はすでに存在します。別の名前を入力してください。");
                        continue;
                    }

                    validName = true;
                }

                // Thêm cột mới vào danh sách tầng được chọn
                selectedFloor.梁.Add(new 梁 { Name = columnName });

                // Lưu dữ liệu
                StorageUtils.SaveProject(doc, _projectData);

                // Cập nhật giao diện danh sách cột
                ColumnListBox.ItemsSource = null;
                ColumnListBox.ItemsSource = selectedFloor.梁;

                // Thông báo thành công
                MessageBox.Show($"梁 '{columnName}' を階 '{selectedFloor.各階}' に追加し、保存しました。");
            }
            else
            {
                MessageBox.Show("階を選択してください。");
            }
        }
        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (FloorNameListBox.SelectedItem is 梁リスト selectedFloor)
            {
                if (ColumnListBox.SelectedItem is 梁 selectedColumn)
                {

                    // Mở cửa sổ chỉnh sửa thông tin cột
                    var editWindow = new 梁の配置(doc, selectedColumn.梁の配置, _projectData) { Left = 500, Top = 200 };
                    if (editWindow.ShowDialog() == true)
                    {
                        // Đảm bảo cập nhật lại instance đã chỉnh sửa
                        selectedColumn.梁の配置 = editWindow.DataContext as Z梁の配置;

                        string selectedProjectName = ProjectManager.SelectedProjectName;
                        _projectData = StorageUtils.LoadProject(doc, selectedProjectName);
                        DataContext = _projectData.リスト;

                        // Cập nhật giao diện danh sách cột
                        ColumnListBox.ItemsSource = null;
                        ColumnListBox.ItemsSource = selectedFloor.梁;

                        // Thông báo chỉnh sửa thành công
                        MessageBox.Show($"梁 '{selectedColumn.Name}' を更新し、保存しました。");
                    }
                }
                else
                {
                    MessageBox.Show("編集する梁を選択してください。");
                }
            }
            else
            {
                MessageBox.Show("階を選択してください。");
            }
        }
        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (FloorNameListBox.SelectedItem is 梁リスト selectedFloor)
            {
                if (ColumnListBox.SelectedItem is 梁 selectedColumn)
                {
                    // Hiển thị hộp thoại xác nhận xóa
                    var result = MessageBox.Show($"梁 '{selectedColumn.Name}' を階 '{selectedFloor.各階}' から削除しますか？",
                                                 "削除の確認", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.OK)
                    {
                        // Xóa cột khỏi danh sách
                        selectedFloor.梁.Remove(selectedColumn);

                        // Lưu dữ liệu
                        StorageUtils.SaveProject(doc, _projectData);

                        // Thông báo xóa thành công
                        MessageBox.Show($"梁 '{selectedColumn.Name}' を削除し、保存しました。");
                    }
                }
                else
                {
                    MessageBox.Show("削除する梁を選択してください。");
                }
            }
            else
            {
                MessageBox.Show("階を選択してください。");
            }
        }
        private void EditNameButton_Click(object sender, RoutedEventArgs e)
        {
            if (FloorNameListBox.SelectedItem is 梁リスト selectedFloor)
            {
                if (ColumnListBox.SelectedItem is 梁 selectedColumn)
                {
                    bool validName = false;
                    string newName = null;

                    // Lặp lại cho đến khi nhập tên hợp lệ
                    while (!validName)
                    {
                        // Hiển thị hộp thoại chỉnh sửa tên cột
                        var inputDialog = new InputDialog("新しい梁の名前を入力してください:");
                        inputDialog.InitialText = selectedColumn.Name; // Hiển thị tên hiện tại
                        if (inputDialog.ShowDialog() != true)
                        {
                            return; // Người dùng hủy thao tác
                        }

                        newName = inputDialog.ResponseText;

                        // Kiểm tra tên rỗng
                        if (string.IsNullOrWhiteSpace(newName))
                        {
                            MessageBox.Show("梁の名前を入力してください。");
                            continue;
                        }

                        // Kiểm tra tên trùng lặp (trừ tên gốc)
                        if (newName != selectedColumn.Name && selectedFloor.梁.Any(c => c.Name == newName))
                        {
                            MessageBox.Show($"名前 '{newName}' はすでに存在します。別の名前を入力してください。");
                            continue;
                        }

                        validName = true;
                    }

                    // Cập nhật tên cột
                    selectedColumn.Name = newName;

                    // Lưu dữ liệu
                    StorageUtils.SaveProject(doc, _projectData);

                    // Cập nhật giao diện danh sách cột
                    ColumnListBox.ItemsSource = null;
                    ColumnListBox.ItemsSource = selectedFloor.梁;

                    // Thông báo chỉnh sửa tên thành công
                    MessageBox.Show($"梁の名前を '{newName}' に更新し、保存しました。");
                }
                else
                {
                    MessageBox.Show("名前を編集する梁を選択してください。");
                }
            }
            else
            {
                MessageBox.Show("階を選択してください。");
            }
        }
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (FloorNameListBox.SelectedItem is 梁リスト selectedFloor)
            {
                if (ColumnListBox.SelectedItem is 梁 selectedColumn)
                {
                    bool validName = false;
                    string newColumnName = null;

                    // Lặp lại cho đến khi nhập tên hợp lệ
                    while (!validName)
                    {
                        // Hiển thị hộp thoại nhập tên cột sao chép
                        var inputDialog = new InputDialog("コピーする梁の新しい名前を入力してください:");
                        if (inputDialog.ShowDialog() != true)
                        {
                            return; // Người dùng hủy thao tác
                        }

                        newColumnName = inputDialog.ResponseText;

                        // Kiểm tra tên rỗng
                        if (string.IsNullOrWhiteSpace(newColumnName))
                        {
                            MessageBox.Show("梁の名前を入力してください。");
                            continue;
                        }

                        // Kiểm tra tên trùng với tên gốc
                        if (newColumnName == selectedColumn.Name)
                        {
                            MessageBox.Show("新しい名前は元の名前と同じにできません。");
                            continue;
                        }

                        // Kiểm tra tên trùng với các cột hiện có
                        if (selectedFloor.梁.Any(c => c.Name == newColumnName))
                        {
                            MessageBox.Show($"名前 '{newColumnName}' はすでに存在します。別の名前を入力してください。");
                            continue;
                        }

                        validName = true;
                    }

                    // Tạo bản sao của cột
                    var newColumn = new 梁
                    {
                        Name = newColumnName,
                        梁の配置 = selectedColumn.梁の配置
                    };

                    // Thêm cột mới vào danh sách
                    selectedFloor.梁.Add(newColumn);

                    // Lưu dữ liệu
                    StorageUtils.SaveProject(doc, _projectData);

                    // Cập nhật giao diện danh sách cột
                    ColumnListBox.ItemsSource = null;
                    ColumnListBox.ItemsSource = selectedFloor.梁;

                    // Thông báo sao chép thành công
                    MessageBox.Show($"梁 '{selectedColumn.Name}' を '{newColumnName}' としてコピーし、保存しました。");
                }
                else
                {
                    MessageBox.Show("コピーする梁を選択してください。");
                }
            }
            else
            {
                MessageBox.Show("階を選択してください。");
            }
        }
        private void FloorNameListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FloorNameListBox.SelectedItem is 梁リスト selectedFloor)
            {
                // Hiển thị danh sách cột của tầng được chọn
                ColumnListBox.ItemsSource = selectedFloor.梁;
            }
        }

    }

}

