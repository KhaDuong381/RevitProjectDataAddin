using Autodesk.Revit.UI;
using System.Reflection;
namespace RevitProjectDataAddin
{
    public class App : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            string tabName = "DBS1";
            try { application.CreateRibbonTab(tabName); } catch { }

            RibbonPanel panel1 = application.CreateRibbonPanel(tabName, "Main");
            _ = panel1.AddItem(new PushButtonData(
                "Homepage",
                "Homepage",
                Assembly.GetExecutingAssembly().Location,
                "RevitProjectDataAddin.SelectProjectCommand")) as PushButton;

            PushButtonData KihonButtonData = new PushButtonData(
                "基本入力",
                "基本入力",
                Assembly.GetExecutingAssembly().Location,
                "RevitProjectDataAddin.KihonCommand");

            PushButton KihonButton = panel1.AddItem(KihonButtonData) as PushButton;
            KihonButton.Enabled = false;
            KihonButtonManager.SetButton(KihonButton);

            PushButtonData KesanButtonData = new PushButtonData(
                "計算値設定",
                "計算値設定",
                Assembly.GetExecutingAssembly().Location,
                "RevitProjectDataAddin.計算値設定Command");
            PushButton KesanButton = panel1.AddItem(KesanButtonData) as PushButton;
            KesanButton.Enabled = false;
            KesanButtonManager.SetButton(KesanButton);

            PushButtonData リストButtonData = new PushButtonData(
                "リスト入力",
                "リスト入力",
                Assembly.GetExecutingAssembly().Location,
                "RevitProjectDataAddin.リストCommand");
            PushButton リストButton = panel1.AddItem(リストButtonData) as PushButton;
            リストButton.Enabled = false;
            リスト入力ButtonManager.SetButton(リストButton);

            PushButtonData 配置リストButtonData = new PushButtonData(
                "配置リスト",
                "配置リスト",
                Assembly.GetExecutingAssembly().Location,
                "RevitProjectDataAddin.配置リストCommand");
            PushButton 配置リストButton = panel1.AddItem(配置リストButtonData) as PushButton;
            配置リストButton.Enabled = false;
            配置リストButtonManager.SetButton(配置リストButton);

            PushButtonData 施工図リストButtonData = new PushButtonData(
               "施工図リスト",
               "施工図リスト",
               Assembly.GetExecutingAssembly().Location,
               "RevitProjectDataAddin.施工図リストCommand");
            PushButton 施工図リストButton = panel1.AddItem(施工図リストButtonData) as PushButton;
            施工図リストButton.Enabled = false;
            施工図リストButtonManager.SetButton(施工図リストButton);

            RibbonPanel panel2 = application.CreateRibbonPanel(tabName, "BVBS");
            PushButtonData bvbsButtonData = new PushButtonData(
                "BVBSButton",
                "BVBS",
                Assembly.GetExecutingAssembly().Location,
                "RevitProjectDataAddin.BVBSCommand");

            PushButton bvbsButton = panel2.AddItem(bvbsButtonData) as PushButton;
            bvbsButton.Enabled = false;
            BVBSButtonManager.SetButton(bvbsButton);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }
    }
}
