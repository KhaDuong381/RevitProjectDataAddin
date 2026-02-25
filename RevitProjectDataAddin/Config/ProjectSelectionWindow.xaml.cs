using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Xml.Linq;

namespace RevitProjectDataAddin
{
    public partial class ProjectSelectionWindow : Window
    {
        private Document _doc;
        public string SelectedProjectName { get; private set; }

        public ProjectSelectionWindow(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            LoadProjects();
        }

        private void LoadProjects()
        {
            List<string> projects = StorageUtils.GetAllProjectNames(_doc);
            ProjectListBox.ItemsSource = projects;
        }

        private void AddProject_Click(object sender, RoutedEventArgs e)
        {
            string name = NewProjectNameBox.Text;
            // Kiểm tra nếu name rỗng hoặc toàn dấu cách
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Tên dự án không được để trống hoặc toàn khoảng trắng.");
                return;
            }

            if (!StorageUtils.GetAllProjectNames(_doc).Contains(name))
            {
                // Khởi tạo ProjectData mẫu với ID cho Project, SubProject, Task
                var project = new ProjectData
                {
                    Id = name,
                    ProjectName = name,
                    Kihon = new KihonData(),
                    Kesan = new KesanData(),
                };

                StorageUtils.SaveProject(_doc, project);
                LoadProjects();
                NewProjectNameBox.Text = "";
            }
            else
            {
                MessageBox.Show("Tên dự án đã tồn tại.");
            }
        }

        private void RenameProject_Click(object sender, RoutedEventArgs e)
        {
            string selectedName = ProjectListBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedName))
            {
                MessageBox.Show("Vui lòng chọn dự án cần đổi tên.");
                return;
            }

            var renameWindow = new RenameProjectWindow(selectedName)
            {
                Owner = this
            };

            if (renameWindow.ShowDialog() == true)
            {
                string newName = renameWindow.NewProjectName;

                if (StorageUtils.GetAllProjectNames(_doc).Contains(newName))
                {
                    MessageBox.Show("Tên mới đã tồn tại.");
                    return;
                }

                var data = StorageUtils.LoadProject(_doc, selectedName);
                if (data == null)
                {
                    MessageBox.Show("Không thể tải dữ liệu dự án.");
                    return;
                }

                data.Id = newName;
                data.ProjectName = newName;

                StorageUtils.DeleteProject(_doc, selectedName);
                StorageUtils.SaveProject(_doc, data);

                MessageBox.Show($"Đã đổi tên dự án thành \"{newName}\".");
                LoadProjects();
            }
        }



        private void DeleteProject_Click(object sender, RoutedEventArgs e)
        {
            string selectedName = ProjectListBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedName))
            {
                MessageBox.Show("Vui lòng chọn dự án để xóa.");
                return;
            }

            var result = MessageBox.Show($"Bạn có chắc muốn xóa dự án \"{selectedName}\"?", "Xác nhận", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                StorageUtils.DeleteProject(_doc, selectedName);
                LoadProjects();
            }
            ProjectManager.ClearProject();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string selectedName = ProjectListBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedName))
                return;

            var data = StorageUtils.LoadProject(_doc, selectedName);
            if (data == null)
            {
                MessageBox.Show("Không thể tải dữ liệu.");
                return;
            }

            // Lưu lại sau chỉnh sửa
            //StorageUtils.SaveProject(_doc, data);

            SelectedProjectName = selectedName;
            if (!string.IsNullOrWhiteSpace(SelectedProjectName))
            {
                DialogResult = true;
                Close();
            }
        }
    }
}
