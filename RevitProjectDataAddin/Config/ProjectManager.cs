namespace RevitProjectDataAddin
{
    public static class ProjectManager
    {
        public static string SelectedProjectName { get; private set; }

        public static bool HasSelectedProject => !string.IsNullOrEmpty(SelectedProjectName);

        public static void SetProject(string projectName)
        {
            SelectedProjectName = projectName;
            BVBSButtonManager.EnableBVBS(true);
            KihonButtonManager.EnableKihon(true);
            KesanButtonManager.EnableKesan(true);
            リスト入力ButtonManager.Enableリスト(true);
            配置リストButtonManager.Enableリスト(true);
            施工図リストButtonManager.Enableリスト(true);
        }

        public static void ClearProject()
        {
            SelectedProjectName = null;
            BVBSButtonManager.EnableBVBS(false);
            KihonButtonManager.EnableKihon(false);
            KesanButtonManager.EnableKesan(false);
            リスト入力ButtonManager.Enableリスト(false);
            配置リストButtonManager.Enableリスト(false);
            施工図リストButtonManager.Enableリスト(false);
        }
    }
}

