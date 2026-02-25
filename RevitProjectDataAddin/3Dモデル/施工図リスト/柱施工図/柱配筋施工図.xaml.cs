using Autodesk.Revit.DB;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RevitProjectDataAddin
{
 
    public partial class 柱配筋施工図 : Window
    {
        private Document doc;
        private readonly ProjectData _projectData;
        private 柱施工図 _currentSecoList;
        public TrackedObject<柱施工図> _trackedSecoList;
        public 柱配筋施工図(Document document, 柱施工図 targetHoopSecozu, ProjectData projectData)
        {
            InitializeComponent();
            doc = document;
            _projectData = projectData;
            _currentSecoList = targetHoopSecozu;

            DataContext = _currentSecoList;
            Load();
            _trackedSecoList = new TrackedObject<柱施工図>(_currentSecoList);

            this.Closing += Close;
        }
        public void Load()
        {
            UpdateComboBoxItemsSource();

            var xtsuList = _projectData.Kihon.NameX.Select(x => x.Name).ToList();
            var ytsuList = _projectData.Kihon.NameY.Select(y => y.Name).ToList();

            // 階
            if (string.IsNullOrEmpty(_currentSecoList.X通を選択) || !xtsuList.Contains(_currentSecoList.X通を選択))
                _currentSecoList.X通を選択 = xtsuList.FirstOrDefault();

            // 通  ← kiểm tra cả “không thuộc list”
            if (string.IsNullOrEmpty(_currentSecoList.Y通を選択) || !ytsuList.Contains(_currentSecoList.Y通を選択))
                _currentSecoList.Y通を選択 = ytsuList.FirstOrDefault();

            // (Tùy chọn) không cần set SelectedItem bằng code-behind nếu đã Binding TwoWay
            X通を選択ComboBox.SelectedItem = _currentSecoList.X通を選択;
            Y通を選択ComboBox.SelectedItem = _currentSecoList.Y通を選択;


        }

        private void UpdateComboBoxItemsSource()
        {
            X通を選択ComboBox.ItemsSource = _projectData.Kihon.NameX.Select(x => x.Name).ToList();

            Y通を選択ComboBox.ItemsSource = _projectData.Kihon.NameY.Select(y => y.Name).ToList();
        }
        private void Close(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_trackedSecoList.HasChanged())
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
                    _trackedSecoList.RestoreOriginal();
                }
            }
            else
            {
            }

        }
    }
}
