using Autodesk.Revit.DB;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace RevitProjectDataAddin
{
    public partial class リスト入力 : Window
    {
        private Document doc;
        private readonly ProjectData _projectData;
        public リスト入力(Document doc)
        {
            InitializeComponent();
            this.doc = doc;
            string selectedProjectName = ProjectManager.SelectedProjectName;
            _projectData = StorageUtils.LoadProject(doc, selectedProjectName);
            DataContext = _projectData.リスト;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var 基礎リストを入力 = new 基礎リストを入力(doc) { Left = 920, Top = 290 };
            基礎リストを入力.ShowDialog();
            基礎リストを入力.Load();
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            var 柱リストを入力 = new 柱リストを入力(doc) { Left = 920, Top = 290 };
            柱リストを入力.ShowDialog();
            柱リストを入力.Load();
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            var 梁リストを入力 = new 梁リストを入力(doc) { Left = 920, Top = 290 };
            梁リストを入力.ShowDialog();
            梁リストを入力.Load();

        }
    }
}