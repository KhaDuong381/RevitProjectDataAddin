using System.Windows;
using System.Windows.Controls;
using Document = Autodesk.Revit.DB.Document;
using System.Linq;

namespace RevitProjectDataAddin
{
    /// <summary>
    /// Interaction logic for リスト.xaml
    /// </summary>
    public partial class 基礎リストを入力 : Window
    {
        private Document doc;
        private readonly ProjectData _projectData;

        public 基礎リストを入力(Document document)
        {
            InitializeComponent();
            this.doc = document;
            string selectedProjectName = ProjectManager.SelectedProjectName;
            _projectData = StorageUtils.LoadProject(doc, selectedProjectName);
            DataContext = _projectData.リスト;
            Load();
        }

        // Tải và đồng bộ dữ liệu khi khởi tạo
        public void Load()
        {
            _projectData.SyncKisokaiWithNameKai(); // Đồng bộ danh sách tầng
            DataContext = _projectData.リスト;
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (FloorNameListBox.SelectedItem is 基礎リスト selectedFloor)
            {
                bool validName = false;
                string columnName = null;

                // Lặp lại cho đến khi nhập tên hợp lệ
                while (!validName)
                {
                    // Hiển thị hộp thoại nhập tên cột mới
                    var inputDialog = new InputDialog("基礎の名前を入力してください:");
                    if (inputDialog.ShowDialog() != true)
                    {
                        return; // Người dùng hủy thao tác
                    }

                    columnName = inputDialog.ResponseText;

                    // Kiểm tra tên rỗng
                    if (string.IsNullOrWhiteSpace(columnName))
                    {
                        MessageBox.Show("基礎の名前を入力してください。");
                        continue;
                    }

                    // Kiểm tra tên trùng lặp
                    if (selectedFloor.基礎.Any(c => c.Name == columnName))
                    {
                        MessageBox.Show($"名前 '{columnName}' はすでに存在します。別の名前を入力してください。");
                        continue;
                    }

                    validName = true;
                }

                // Thêm cột mới vào danh sách tầng được chọn
                selectedFloor.基礎.Add(new 基礎 { Name = columnName });

                // Lưu dữ liệu
                StorageUtils.SaveProject(doc, _projectData);

                // Cập nhật giao diện danh sách cột
                ColumnListBox.ItemsSource = null;
                ColumnListBox.ItemsSource = selectedFloor.基礎;

                // Thông báo thành công
                MessageBox.Show($"基礎 '{columnName}' を階 '{selectedFloor.各階}' に追加し、保存しました。");
            }
            else
            {
                MessageBox.Show("階を選択してください。");
            }
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            
            var abc = _projectData;
            if (FloorNameListBox.SelectedItem is 基礎リスト selectedFloor)
            {
                if (ColumnListBox.SelectedItem is 基礎 selectedColumn)
                {
                    // Mở cửa sổ chỉnh sửa thông tin cột
                    var editWindow = new 基礎の配置(doc, selectedColumn.基礎の配置) { Left = 820, Top = 260 };
                    if (editWindow.ShowDialog() == true)
                    {
                        // Lưu dữ liệu sau khi chỉnh sửa
                        StorageUtils.SaveProject(doc, _projectData);

                        // Cập nhật giao diện danh sách cột
                        ColumnListBox.ItemsSource = null;
                        ColumnListBox.ItemsSource = selectedFloor.基礎;

                        // Thông báo chỉnh sửa thành công
                        MessageBox.Show($"基礎 '{selectedColumn.Name}' を更新し、保存しました。");
                    }
                }
                else
                {
                    MessageBox.Show("編集する基礎を選択してください。");
                }
            }
            else 
            {
                MessageBox.Show("階を選択してください。");
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (FloorNameListBox.SelectedItem is 基礎リスト selectedFloor)
            {
                if (ColumnListBox.SelectedItem is 基礎 selectedColumn)
                {
                    // Hiển thị hộp thoại xác nhận xóa
                    var result = MessageBox.Show($"基礎 '{selectedColumn.Name}' を階 '{selectedFloor.各階}' から削除しますか？",
                                                 "削除の確認", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.OK)
                    {
                        // Xóa cột khỏi danh sách
                        selectedFloor.基礎.Remove(selectedColumn);

                        // Lưu dữ liệu
                        StorageUtils.SaveProject(doc, _projectData);

                        // Thông báo xóa thành công
                        MessageBox.Show($"基礎 '{selectedColumn.Name}' を削除し、保存しました。");
                    }
                }
                else
                {
                    MessageBox.Show("削除する基礎を選択してください。");
                }
            }
            else
            {
                MessageBox.Show("階を選択してください。");
            }
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (FloorNameListBox.SelectedItem is 基礎リスト selectedFloor)
            {
                if (ColumnListBox.SelectedItem is 基礎 selectedColumn)
                {
                    bool validName = false;
                    string newColumnName = null;

                    // Lặp lại cho đến khi nhập tên hợp lệ
                    while (!validName)
                    {
                        // Hiển thị hộp thoại nhập tên cột sao chép
                        var inputDialog = new InputDialog("コピーする基礎の新しい名前を入力してください:");
                        if (inputDialog.ShowDialog() != true)
                        {
                            return; // Người dùng hủy thao tác
                        }

                        newColumnName = inputDialog.ResponseText;

                        // Kiểm tra tên rỗng
                        if (string.IsNullOrWhiteSpace(newColumnName))
                        {
                            MessageBox.Show("基礎の名前を入力してください。");
                            continue;
                        }

                        // Kiểm tra tên trùng với tên gốc
                        if (newColumnName == selectedColumn.Name)
                        {
                            MessageBox.Show("新しい名前は元の名前と同じにできません。");
                            continue;
                        }

                        // Kiểm tra tên trùng với các cột hiện có
                        if (selectedFloor.基礎.Any(c => c.Name == newColumnName))
                        {
                            MessageBox.Show($"名前 '{newColumnName}' はすでに存在します。別の名前を入力してください。");
                            continue;
                        }

                        validName = true;
                    }

                    // Tạo bản sao của cột
                    var newColumn = new 基礎
                    {
                        Name = newColumnName,
                        基礎の配置 = selectedColumn.基礎の配置 != null ? new Z基礎の配置
                        {
                            // Sao chép các thuộc tính của Z基礎の配置
                            水平方向はかま筋径 = selectedColumn.基礎の配置.水平方向はかま筋径,
                            水平方向はかま筋形 = selectedColumn.基礎の配置.水平方向はかま筋形,
                            水平方向はかま筋材質 = selectedColumn.基礎の配置.水平方向はかま筋材質,
                            ピッチ = selectedColumn.基礎の配置.ピッチ,
                            縦向きはかま筋径 = selectedColumn.基礎の配置.縦向きはかま筋径,
                            縦向きはかま筋本数 = selectedColumn.基礎の配置.縦向きはかま筋本数,
                            縦向きはかま筋形 = selectedColumn.基礎の配置.縦向きはかま筋形,
                            縦向きはかま筋材質 = selectedColumn.基礎の配置.縦向きはかま筋材質,
                            縦向き下端筋径 = selectedColumn.基礎の配置.縦向き下端筋径,
                            縦向き下端筋本数 = selectedColumn.基礎の配置.縦向き下端筋本数,
                            縦向き下端筋形 = selectedColumn.基礎の配置.縦向き下端筋形,
                            縦向き下端筋材質 = selectedColumn.基礎の配置.縦向き下端筋材質,
                            横向きはかま筋径 = selectedColumn.基礎の配置.横向きはかま筋径,
                            横向きはかま筋本数 = selectedColumn.基礎の配置.横向きはかま筋本数,
                            横向きはかま筋形 = selectedColumn.基礎の配置.横向きはかま筋形,
                            横向きはかま筋材質 = selectedColumn.基礎の配置.横向きはかま筋材質,
                            横向き下端筋径 = selectedColumn.基礎の配置.横向き下端筋径,
                            横向き下端筋本数 = selectedColumn.基礎の配置.横向き下端筋本数,
                            横向き下端筋形 = selectedColumn.基礎の配置.横向き下端筋形,
                            横向き下端筋材質 = selectedColumn.基礎の配置.横向き下端筋材質
                        } : null
                    };

                    // Thêm cột mới vào danh sách
                    selectedFloor.基礎.Add(newColumn);

                    // Lưu dữ liệu
                    StorageUtils.SaveProject(doc, _projectData);

                    // Cập nhật giao diện danh sách cột
                    ColumnListBox.ItemsSource = null;
                    ColumnListBox.ItemsSource = selectedFloor.基礎;

                    // Thông báo sao chép thành công
                    MessageBox.Show($"基礎 '{selectedColumn.Name}' を '{newColumnName}' としてコピーし、保存しました。");
                }
                else
                {
                    MessageBox.Show("コピーする基礎を選択してください。");
                }
            }
            else
            {
                MessageBox.Show("階を選択してください。");
            }
        }

        private void EditNameButton_Click(object sender, RoutedEventArgs e)
        {
            if (FloorNameListBox.SelectedItem is 基礎リスト selectedFloor)
            {
                if (ColumnListBox.SelectedItem is 基礎 selectedColumn)
                {
                    bool validName = false;
                    string newName = null;

                    // Lặp lại cho đến khi nhập tên hợp lệ
                    while (!validName)
                    {
                        // Hiển thị hộp thoại chỉnh sửa tên cột
                        var inputDialog = new InputDialog("新しい基礎の名前を入力してください:");
                        inputDialog.InitialText = selectedColumn.Name; // Hiển thị tên hiện tại
                        if (inputDialog.ShowDialog() != true)
                        {
                            return; // Người dùng hủy thao tác
                        }

                        newName = inputDialog.ResponseText;

                        // Kiểm tra tên rỗng
                        if (string.IsNullOrWhiteSpace(newName))
                        {
                            MessageBox.Show("基礎の名前を入力してください。");
                            continue;
                        }

                        // Kiểm tra tên trùng lặp (trừ tên gốc)
                        if (newName != selectedColumn.Name && selectedFloor.基礎.Any(c => c.Name == newName))
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
                    ColumnListBox.ItemsSource = selectedFloor.基礎;

                    // Thông báo chỉnh sửa tên thành công
                    MessageBox.Show($"基礎の名前を '{newName}' に更新し、保存しました。");
                }
                else
                {
                    MessageBox.Show("名前を編集する基礎を選択してください。");
                }
            }
            else
            {
                MessageBox.Show("階を選択してください。");
            }
        }

        private void FloorNameListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FloorNameListBox.SelectedItem is 基礎リスト selectedFloor)
            {
                // Hiển thị danh sách cột của tầng được chọn
                ColumnListBox.ItemsSource = selectedFloor.基礎;
            }
        }
    }
}