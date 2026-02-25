using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Autodesk.Revit.DB;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace RevitProjectDataAddin
{   
    public partial class 施工図リスト : Window
    {
        private Document doc;
        private readonly ProjectData _projectData;
        public 施工図リスト(Document doc)
        {
            InitializeComponent();
            this.doc = doc;
            string selectedProjectName = ProjectManager.SelectedProjectName;
            _projectData = StorageUtils.LoadProject(doc, selectedProjectName);
            DataContext = _projectData.リスト;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            // Lấy beamLayout đầu tiên từ danh sách (đảm bảo có sẵn)
            var beamLayout = _projectData.Secozu.柱施工図.FirstOrDefault();

            // Nếu chưa có thì khởi tạo và add vào (chỉ làm 1 lần duy nhất)
            if (beamLayout == null)
            {
                beamLayout = new 柱施工図();
                _projectData.Secozu.柱施工図.Add(beamLayout);
            }
            var 柱配筋施工図 = new 柱配筋施工図(doc, beamLayout, _projectData)
            {
                Left = 920,
                Top = 290
            };

            柱配筋施工図.ShowDialog();
            柱配筋施工図.Load();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            // Lấy beamLayout đầu tiên từ danh sách (đảm bảo có sẵn)
            var beamLayout = _projectData.Secozu.梁施工図.FirstOrDefault();

            //Nếu chưa có thì khởi tạo và add vào(chỉ làm 1 lần duy nhất)
            if (beamLayout == null)
            {
                beamLayout = new 梁施工図();
                _projectData.Secozu.梁施工図.Add(beamLayout);
            }

            // Truyền đủ 3 đối số vào constructor
            var 梁配筋施工図 = new 梁配筋施工図(doc, beamLayout, _projectData)
            {
                Left = 800,
                Top = 90
            };

            梁配筋施工図.ShowDialog();
            梁配筋施工図.Load();
        }
    }
}
