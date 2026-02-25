using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace RevitProjectDataAddin
{
    [Transaction(TransactionMode.Manual)]
    public class SelectProjectCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;
            var uiDoc = uiApp.ActiveUIDocument;
            var doc = uiDoc.Document;

            var window = new ProjectSelectionWindow(doc)
            {
                Left = 1100, // Vị trí ngang
                Top = 500   // Vị trí dọc
            };
            if (window.ShowDialog() == true)
            {
                string selectedProject = window.SelectedProjectName;
                ProjectManager.SetProject(selectedProject);
            }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class KihonCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Kiểm tra xem người dùng đã chọn project chưa
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Thông báo", "Vui lòng chọn project trước.");
                return Result.Cancelled;
            }
            // Lấy đối tượng Document từ commandData
            Document doc = commandData.Application.ActiveUIDocument.Document;
            // Khởi tạo cửa sổ Kihon và truyền đối tượng Document vào
            var window = new 基本入力(doc)
            {
                Left = 700, // Vị trí ngang
                Top = 200   // Vị trí dọc
            };
            // Hiển thị cửa sổ dưới dạng modal
            window.ShowDialog();
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class 計算値設定Command : IExternalCommand
    {
        private KesanData kesanData;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Kiểm tra xem người dùng đã chọn project chưa
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Thông báo", "Vui lòng chọn project trước.");
                return Result.Cancelled;
            }
            // Lấy đối tượng Document từ commandData
            Document doc = commandData.Application.ActiveUIDocument.Document;
            // Khởi tạo cửa sổ 計算値設定 và truyền đối tượng Document vào
            var window = new 計算値設定(doc) { Left = 300, Top = 200 };
            // Hiển thị cửa sổ dưới dạng modal
            window.ShowDialog();
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class リストCommand : IExternalCommand
    {
        private KesanData kesanData;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Kiểm tra xem người dùng đã chọn project chưa
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Thông báo", "Vui lòng chọn project trước.");
                return Result.Cancelled;
            }
            // Lấy đối tượng Document từ commandData
            Document doc = commandData.Application.ActiveUIDocument.Document;
            // Khởi tạo cửa sổ リスト入力 và truyền đối tượng Document vào
            var window = new リスト入力(doc) { Left = 945, Top = 300 };
            // Hiển thị cửa sổ dưới dạng modal
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
    [Transaction(TransactionMode.Manual)]
    public class 配置リストCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Kiểm tra xem người dùng đã chọn project chưa
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Thông báo", "Vui lòng chọn project trước.");
                return Result.Cancelled;
            }
            // Lấy đối tượng Document từ commandData
            Document doc = commandData.Application.ActiveUIDocument.Document;
            // Khởi tạo cửa sổ 配置リスト và truyền đối tượng Document vào
            var window = new 配置リスト(doc) { Left = 945, Top = 300 };
            // Hiển thị cửa sổ dưới dạng modal
            window.ShowDialog();
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    public class 施工図リストCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // Kiểm tra xem người dùng đã chọn project chưa
            if (!ProjectManager.HasSelectedProject)
            {
                TaskDialog.Show("Thông báo", "Vui lòng chọn project trước.");
                return Result.Cancelled;
            }
            // Lấy đối tượng Document từ commandData
            Document doc = commandData.Application.ActiveUIDocument.Document;
            // Khởi tạo cửa sổ 施工図リスト và truyền đối tượng Document vào
            var window = new 施工図リスト(doc) { Left = 945, Top = 300 };
            // Hiển thị cửa sổ dưới dạng modal
            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
