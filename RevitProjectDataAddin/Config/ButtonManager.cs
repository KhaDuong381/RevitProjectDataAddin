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

    public static class GridButtonManager
    {
        private static PushButton _gridButton;

        public static void SetButton(PushButton button) => _gridButton = button;

        public static void EnableGrid(bool enable)
        {
            if (_gridButton != null)
                _gridButton.Enabled = enable;
        }
    }

    public static class LevelButtonManager
    {
        private static PushButton _levelButton;

        public static void SetButton(PushButton button) => _levelButton = button;

        public static void EnableLevel(bool enable)
        {
            if (_levelButton != null)
                _levelButton.Enabled = enable;
        }
    }

    public static class ColumnButtonManager
    {
        private static PushButton _columnButton;

        public static void SetButton(PushButton button) => _columnButton = button;

        public static void EnableColumn(bool enable)
        {
            if (_columnButton != null)
                _columnButton.Enabled = enable;
        }
    }

    public static class ColumnHoopButtonManager
    {
        private static PushButton _columnHoopButton;

        public static void SetButton(PushButton button) => _columnHoopButton = button;

        public static void EnableColumnHoop(bool enable)
        {
            if (_columnHoopButton != null)
                _columnHoopButton.Enabled = enable;
        }
    }

    public static class ColumnYokoNakagoButtonManager
    {
        private static PushButton _columnYokoNakagoButton;

        public static void SetButton(PushButton button) => _columnYokoNakagoButton = button;

        public static void EnableColumnYokoNakago(bool enable)
        {
            if (_columnYokoNakagoButton != null)
                _columnYokoNakagoButton.Enabled = enable;
        }
    }

    public static class BeamButtonManager
    {
        private static PushButton _beamButton;

        public static void SetButton(PushButton button) => _beamButton = button;

        public static void EnableBeam(bool enable)
        {
            if (_beamButton != null)
                _beamButton.Enabled = enable;
        }
    }
}

