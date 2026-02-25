using Autodesk.Revit.DB;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RevitProjectDataAddin
{
   
    public partial class 配置リスト : Window
    {
        private Document doc;
        private readonly ProjectData _projectData;
        public 配置リスト(Document doc)
        {
            InitializeComponent();
            this.doc = doc;
            string selectedProjectName = ProjectManager.SelectedProjectName;
            _projectData = StorageUtils.LoadProject(doc, selectedProjectName);
            DataContext = _projectData.リスト;
        }
        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // Lấy beamLayout đầu tiên từ danh sách (đảm bảo có sẵn)
            var beamLayout = _projectData.Haichi.柱配置図.FirstOrDefault();

            // Nếu chưa có thì khởi tạo và add vào (chỉ làm 1 lần duy nhất)
            if (beamLayout == null)
            {
                beamLayout = new 柱配置図();
                _projectData.Haichi.柱配置図.Add(beamLayout);
            }

            var 柱リスト = new 柱配置リスト(doc, beamLayout, _projectData) { Left = 920, Top = 290 };
            柱リスト.ShowDialog();
            柱リスト.Load();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            // Lấy beamLayout đầu tiên từ danh sách (đảm bảo có sẵn)
            var beamLayout = _projectData.Haichi.梁配置図.FirstOrDefault();

            // Nếu chưa có thì khởi tạo và add vào (chỉ làm 1 lần duy nhất)
            if (beamLayout == null)
            {
                beamLayout = new 梁配置図();
                _projectData.Haichi.梁配置図.Add(beamLayout);
            }

            // Truyền đủ 3 đối số vào constructor
            var 梁リスト = new 梁の配置リスト(doc, beamLayout, _projectData)
            {
                Left = 920,
                Top = 290
            };

            梁リスト.ShowDialog();
            梁リスト.Load();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
