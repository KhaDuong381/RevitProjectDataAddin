using Autodesk.Revit.UI;

namespace RevitProjectDataAddin
{
    public static class BVBSButtonManager
    {
        private static PushButton _bvbsButton;

        public static void SetButton(PushButton button) => _bvbsButton = button;

        public static void EnableBVBS(bool enable)
        {
            if (_bvbsButton != null)
                _bvbsButton.Enabled = enable;
        }
    }

    public static class  KihonButtonManager
    {
        private static PushButton _kihonButton;

        public static void SetButton(PushButton button) => _kihonButton = button;
        public static void EnableKihon(bool enable)
        {
            if (_kihonButton != null)
                _kihonButton.Enabled = enable;
        }
    }

    public static class KesanButtonManager
    {
        private static PushButton _kesanButton;
        public static void SetButton(PushButton button) => _kesanButton = button;
        public static void EnableKesan(bool enable)
        {
            if (_kesanButton != null)
                _kesanButton.Enabled = enable;
        }
    }
    public static class リスト入力ButtonManager
    {
        private static PushButton _リストButton;
        public static void SetButton(PushButton button) => _リストButton = button;
        public static void Enableリスト(bool enable)
        {
            if (_リストButton != null)
                _リストButton.Enabled = enable;
        }
    }
    public static class 配置リストButtonManager
    {
        private static PushButton _配置リストButton;
        public static void SetButton(PushButton button) => _配置リストButton = button;
        public static void Enableリスト(bool enable)
        {
            if (_配置リストButton != null)
                _配置リストButton.Enabled = enable;
        }
    }
    public static class 施工図リストButtonManager
    {
        private static PushButton _施工図リストButton;
        public static void SetButton(PushButton button) => _施工図リストButton = button;
        public static void Enableリスト(bool enable)
        {
            if (_施工図リストButton != null)
                _施工図リストButton.Enabled = enable;
        }
    }
}

