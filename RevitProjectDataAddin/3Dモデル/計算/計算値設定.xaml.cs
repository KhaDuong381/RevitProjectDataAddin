using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using TextBox = System.Windows.Controls.TextBox;

namespace RevitProjectDataAddin
{
    public partial class 計算値設定 : Window
    {
        private readonly Document _doc;
        private ProjectData _projectData;
        public List<string> ValidValues => new List<string> { "中央部", "端部" };
        private DispatcherTimer _debounceTimer;
        private HashSet<string> _propertiesToUpdate;
       // private bool _isDataChanged = false; // Cờ theo dõi thay đổi dữ liệu
        private KesanData _currentKesanData;
        public TrackedObject<KesanData> _trackedKesanData;


        public 計算値設定(Document doc)
        {
            InitializeComponent();
            _doc = doc;
            string selectedProjectName = ProjectManager.SelectedProjectName;
            _projectData = StorageUtils.LoadProject(_doc, selectedProjectName);
            _currentKesanData = _projectData.Kesan;

            // Đoạn mã này sẽ được thực thi khi MainWindow được tạo hoặc khi bạn khởi tạo đối tượng TrackedObject
            _trackedKesanData = new TrackedObject<KesanData>(_currentKesanData);


            // Đăng ký sự kiện Loaded để vẽ Canvas sau khi giao diện tải hoàn toàn
            this.Loaded += (s, e) =>
            { 
                Load();
                // Xóa và vẽ lại các Canvas
                RedrawAllCanvases();
            };


            // Đăng ký sự kiện Closing
            this.Closing += 計算値設定_Closing;
        }
        private void RedrawAllCanvases()
        {
            // Xóa các Canvas trước khi vẽ lại
            page1_1_canvas.Children.Clear();
            page1_3_canvas.Children.Clear();
            MainCanvas.Children.Clear();
            MainCanvas1.Children.Clear();
            MainCanvas2.Children.Clear();
            MainCanvas3.Children.Clear();
            MainCanvas4.Children.Clear();
            MainCanvas5.Children.Clear();
            MainCanvas7.Children.Clear();
            tanbutsuryoiki4.Children.Clear();
            drawBeamDiagram10.Children.Clear();
            drawBeamDiagram11.Children.Clear();
            drawBeamDiagramPhat.Children.Clear();
            MainCanvas13.Children.Clear();
            MainCanvas14.Children.Clear();
            MainCanvas15.Children.Clear();

            // Vẽ lại tất cả các Canvas
            DrawBeamDiagramTon9();
            DrawBeamDiagramTon12();
            DrawBeamDiagramKha15();
            DrawBeamDiagramKha16();
            DrawBeamDiagramKha17();
            DrawBeamDiagramKha18();
            DrawBeamDiagramTon19();
            DrawBeamDiagramPhatSaigo();
            DrawBeamDiagram();
            DrawBeamDiagram1();
            DrawBeamDiagram2();
            DrawBeamDiagram3();
            DrawBeamDiagram4();
            DrawBeamDiagram5();
            DrawBeamDiagram6();
            DrawBeamDiagram8();
            DrawBeamDiagram10();
            DrawBeamDiagram11();
            DrawBeamDiagram13();
            DrawBeamDiagram14();
            DrawBeamDiagramKha19();
            DrawBeamDiagramTon20();
            DrawBeamDiagram1Phat();
            DrawBeamDiagramPhat();
            DrawBeamDiagramTon21();
            DrawBeamDiagramKha20();
            DrawBeamDiagramPhatSaigo1();
            DrawBeamDiagramKha21();
            DrawBeamDiagramKha22();
            DrawBeamDiagramKha23();
            DrawBeamDiagramKha24();

            // Làm mới các Canvas sau khi vẽ
            page1_1_canvas.InvalidateVisual();
            page1_3_canvas.InvalidateVisual();
            MainCanvas.InvalidateVisual();
            MainCanvas1.InvalidateVisual();
            MainCanvas2.InvalidateVisual();
            MainCanvas3.InvalidateVisual();
            MainCanvas4.InvalidateVisual();
            MainCanvas5.InvalidateVisual();
            MainCanvas7.InvalidateVisual();
            tanbutsuryoiki4.InvalidateVisual();
            drawBeamDiagram10.InvalidateVisual();
            drawBeamDiagram11.InvalidateVisual();
            drawBeamDiagramPhat.InvalidateVisual();
            MainCanvas13.InvalidateVisual();
            MainCanvas14.InvalidateVisual();
            MainCanvas15.InvalidateVisual();
        }
        private void 計算値設定_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                bool skipPreviewTextInput = textBox == TsuNaga1TextBox || textBox == TsuNaga2TextBox ||
                                        textBox == AnkaNagaHaraTextBox || textBox == NigeHaraTextBox;
                if (!skipPreviewTextInput)
                {
                    // Kiểm tra nếu textBox có giá trị lớn hơn 5 chữ số  
                    string value = textBox.Text?.Trim();
                    if (!string.IsNullOrEmpty(value) && value.Length > 5 && value.All(char.IsDigit) || !value.All(char.IsDigit))
                    {
                        MessageBox.Show("Giá trị quá lớn!! (Chỉ cho phép tối đa 5 chữ số)", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                        e.Cancel = true;
                        textBox.Focus();
                        textBox.Background = Brushes.LightCoral;
                        return;

                    }
                }

            }
            if (_trackedKesanData.HasChanged())
            {
                // Hiển thị hộp thoại xác nhận
                var result = MessageBox.Show(
                    "Dữ liệu đã thay đổi. Bạn có muốn lưu không?",
                    "Xác nhận",
                    MessageBoxButton.YesNoCancel,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Lưu dữ liệu
                    NormalizeAllTextBoxValues();
                    StorageUtils.SaveProject(_doc, _projectData);
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    // Hủy đóng cửa sổ
                    e.Cancel = true;
                }
                // Nếu chọn No thì không làm gì, cửa sổ sẽ đóng
            }
        }

        private void Load()
        {
                  
            // Gán DataContext
            ChichuUwaShimaiPanel.DataContext = _projectData.Kesan;
            this.DataContext = _projectData;
            matomeprint.DataContext = _projectData.Kesan;
            RadioButton.DataContext = _projectData.Kesan;
            harinaitei.DataContext = _projectData.Kesan;
            shukintei.DataContext = _projectData.Kesan;
            harinainaga.DataContext = _projectData.Kesan;
            shitatsuhook.DataContext = _projectData.Kesan;
            tsugite.DataContext = _projectData.Kesan;
            jogetsuzurasu1.DataContext = _projectData.Kesan;
            jogetsuzurasu2.DataContext = _projectData.Kesan;
            sizedifferent.DataContext = _projectData.Kesan;
            tanbutsuryoiki.DataContext = _projectData.Kesan;
            kobaichouten.DataContext = _projectData.Kesan;
            hanchishitakin.DataContext = _projectData.Kesan;
            ChichuUwaDashiPanel.DataContext = _projectData.Kesan;
            ChichuUwaDashiPanel.DataContext = _projectData.Kesan;
            ChichuShitaDashiPanel.DataContext = _projectData.Kesan;
            ChichuShitaShimaiPanel.DataContext = _projectData.Kesan;
            IppanUwaDashiPanel.DataContext = _projectData.Kesan;
            IppanUwaShimaiPanel.DataContext = _projectData.Kesan;
            IppanShitaDashiPanel.DataContext = _projectData.Kesan;
            IppanShitaShimaiPanel.DataContext = _projectData.Kesan;
            topkinuekaburikoryo.DataContext = _projectData.Kesan;
            梁の主筋の位置.DataContext = _projectData.Kesan;
            hanchihokyoSTP.DataContext = _projectData.Kesan;
            fukashiSTPteichakutenba.DataContext = _projectData.Kesan;
            fukashishukinteichaku.DataContext = _projectData.Kesan;
            hatarakion.DataContext = _projectData.Kesan;
            ankanagaon.DataContext = _projectData.Kesan;
            nigeon.DataContext = _projectData.Kesan;
            topkindimon.DataContext = _projectData.Kesan;
            nakagozuon.DataContext = _projectData.Kesan;
            nakago2on.DataContext = _projectData.Kesan;
            STPzaishitsuon.DataContext = _projectData.Kesan;
            tsuryoikion.DataContext = _projectData.Kesan;
            printsize.DataContext = _projectData.Kesan;
            printmuki.DataContext = _projectData.Kesan;
            tsuryoikiprint.DataContext = _projectData.Kesan;

            _debounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(0)
            };
            _debounceTimer.Tick += (s, e) =>
            {
                _debounceTimer.Stop();
                UpdateCanvas();
            };

            _propertiesToUpdate = new HashSet<string>();

            RegisterKesanPropertyChanged();
            Loaded1();
        }

        private void RegisterKesanPropertyChanged()
        {
            _projectData.Kesan.PropertyChanged += (sender, e) =>
            {
                _propertiesToUpdate.Add(e.PropertyName);
                _debounceTimer.Stop();
                _debounceTimer.Start();
               // _isDataChanged = true; // Gắn cờ khi dữ liệu thay đổi
            };
        }

        private void UpdateCanvas()
        {
            // Kiểm tra các thuộc tính đã thay đổi và vẽ lại Canvas tương ứng
            if (_propertiesToUpdate.Contains(nameof(KesanData.GaitanTanbuTopYochou)) ||
                _propertiesToUpdate.Contains(nameof(KesanData.NaitanTanbuTopYochou)) ||
                _propertiesToUpdate.Contains(nameof(KesanData.GaitanChubuTopYochou)) ||
                _propertiesToUpdate.Contains(nameof(KesanData.NaitanChubuTopYochou)) ||
                _propertiesToUpdate.Contains(nameof(KesanData.TeichakuUwa)) ||
                _propertiesToUpdate.Contains(nameof(KesanData.TeichakuShita)))
            {
                DrawBeamDiagramTon9();
            }

            if (_propertiesToUpdate.Contains(nameof(KesanData.GaitanChubuTopYochou3)) ||
                _propertiesToUpdate.Contains(nameof(KesanData.NaitanChubuTopYochou3)) ||
                _propertiesToUpdate.Contains(nameof(KesanData.GaitanTanbuTopYochou3)) ||
                _propertiesToUpdate.Contains(nameof(KesanData.NaitanTanbuTopYochou3)))
            {
                DrawBeamDiagramTon12();
            }

            if (_propertiesToUpdate.Contains(nameof(KesanData.AnkaNagaRF)))
            {
                DrawBeamDiagram();
            }
            if (_propertiesToUpdate.Contains(nameof(KesanData.ShukinKankaku)))
            {
                DrawBeamDiagram5();
            }

            if (_propertiesToUpdate.Contains(nameof(KesanData.Tsunaga16)) ||
               _propertiesToUpdate.Contains(nameof(KesanData.Tsunaga19)))
            {
                DrawBeamDiagram6();
            }
            if (_propertiesToUpdate.Contains(nameof(KesanData.TonariOo)) ||
                _propertiesToUpdate.Contains(nameof(KesanData.TonariKo)))
            {
                DrawBeamDiagram8();
            }

            if (_propertiesToUpdate.Contains(nameof(KesanData.HariHabaZure)) ||
                _propertiesToUpdate.Contains(nameof(KesanData.KakuShukinZure)))
            {
                DrawBeamDiagram10();
                DrawBeamDiagram11();
            }

            if (_propertiesToUpdate.Contains(nameof(KesanData.DansaKoteisa)))
            {
                DrawBeamDiagramPhat();
            }

            if (_propertiesToUpdate.Contains(nameof(KesanData.STPHabaHerisun)) ||
                _propertiesToUpdate.Contains(nameof(KesanData.STPSeiHerisun)))
            {
                DrawBeamDiagramPhatSaigo();
            }
            // Xóa danh sách thuộc tính sau khi cập nhật
            _propertiesToUpdate.Clear();
        }

        private void Loaded1()
        {
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                if (textBox == null) continue;
                SetupTextBoxKha(textBox);
            }
        }
        private void NormalizeAllTextBoxValues()
        {
            foreach (var textBox in FindVisualChildren<TextBox>(this))
            {
                if (textBox == null) continue;
                string value = textBox.Text;
                if (!string.IsNullOrEmpty(value) && value != "0")
                {
                    string normalized = value.TrimStart('0');
                    textBox.Text = string.IsNullOrEmpty(normalized) ? "0" : normalized;
                }
            }
        }

        private void SetupTextBoxKha(TextBox textBox)
        {
            // Kiểm tra nếu TextBox là một trong các TextBox cần bỏ qua sự kiện PreviewTextInput
            bool skipPreviewTextInput = textBox == TsuNaga1TextBox || textBox == TsuNaga2TextBox ||
                                         textBox == AnkaNagaHaraTextBox || textBox == NigeHaraTextBox;
            void UpdateColors(Brush background) => textBox.Background = background;
            void SetActiveColors() => UpdateColors(Brushes.LightGreen);
            void SetInactiveColors() => UpdateColors(Brushes.White);

            // Thêm các sự kiện khác
            textBox.GotFocus += (s, e) =>
            {
                // Tô màu khi TextBox được focus
                SetActiveColors();

                // Chọn toàn bộ nội dung trong TextBox
                textBox.SelectAll();
            };
            textBox.PreviewMouseDown += (s, e) =>
            {
                // Đảm bảo TextBox không bị mất focus khi nhấp chuột
                if (!textBox.IsFocused)
                {
                    e.Handled = true;
                    textBox.Focus();
                }
            };
            textBox.LostFocus += (s, e) => SetInactiveColors();
            textBox.MouseEnter += (s, e) => SetActiveColors();
            textBox.MouseLeave += (s, e) => { if (!textBox.IsFocused) SetInactiveColors(); };

            // Chỉ thêm sự kiện PreviewTextInput nếu không nằm trong danh sách bỏ qua
            if (!skipPreviewTextInput)
            {
                textBox.PreviewTextInput += (s, e) =>
                {
                    if (s is TextBox textBox1)
                    {
                        // Chỉ cho phép nhập số
                        if (!char.IsDigit(e.Text, 0))
                        {
                            textBox1.Background = Brushes.LightCoral;
                            e.Handled = true;
                            return;
                        }

                        // Giả lập chuỗi mới sau khi nhập
                        string newText = textBox1.Text.Remove(textBox1.SelectionStart, textBox1.SelectionLength)
                            .Insert(textBox1.SelectionStart, e.Text);

                        // Nếu chuỗi mới là "0" thì cho phép
                        if (newText == "0")
                        {
                            textBox1.Background = Brushes.White;
                            e.Handled = false;
                            return;
                        }

                        // Nếu chuỗi mới có độ dài > 1 và bắt đầu bằng '0' thì không cho phép
                        if (newText.Length > 1 && newText.StartsWith("0"))
                        {
                            textBox1.Background = Brushes.LightCoral;
                            e.Handled = true;
                            return;
                        }

                        // Nếu giá trị vượt quá 99999 hoặc vượt quá MaxLength thì không cho phép
                        if ((int.TryParse(newText, out int newValue) && newValue > 99999) || newText.Length > 5)
                        {
                            textBox1.Background = Brushes.LightCoral;
                            e.Handled = true;
                            return;
                        }

                        // Trường hợp hợp lệ
                        textBox1.Background = Brushes.White;
                        e.Handled = false;
                    }
                };

            }
        }
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child is T t)
                    {
                        yield return t;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
            }
        }
        private void ComboBoxUwa_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //StorageUtils.SaveProject(doc, _projectData);
            DrawBeamDiagramTon9();
            DrawBeamDiagramTon12();
        }

        private void ComboBoxShita_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //StorageUtils.SaveProject(doc, _projectData);
            DrawBeamDiagramTon9();
            DrawBeamDiagramTon12();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            //StorageUtils.SaveProject(doc, _projectData);
            bool teiuwaChecked = TeiUwaCheckBox.IsChecked ?? false;
            bool teiuwachuChecked = TeiUwaChuCheckBox.IsChecked ?? false;
            bool teishitaChecked = TeiShitaCheckBox.IsChecked ?? false;
            bool teishitachuChecked = TeiShitaChuCheckBox.IsChecked ?? false;
            DrawBeamDiagram(teiuwaChecked, teiuwachuChecked, teishitachuChecked, teishitaChecked);
        }

        private void CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            //StorageUtils.SaveProject(doc, _projectData);
            bool teiuwaChecked = TeiUwaCheckBox.IsChecked ?? false;
            bool teiuwachuChecked = TeiUwaChuCheckBox.IsChecked ?? false;
            bool teishitaChecked = TeiShitaCheckBox.IsChecked ?? false;
            bool teishitachuChecked = TeiShitaChuCheckBox.IsChecked ?? false;
            DrawBeamDiagram(teiuwaChecked, teiuwachuChecked, teishitachuChecked, teishitaChecked);
        }
        public void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            //StorageUtils.SaveProject(doc, _projectData);
            DrawBeamDiagram();
            DrawBeamDiagram1();
            DrawBeamDiagram2();
            DrawBeamDiagram3();
            DrawBeamDiagram4();
            DrawBeamDiagram5();
            DrawBeamDiagram6();
            DrawBeamDiagram8();
            DrawBeamDiagram10();
            DrawBeamDiagram11();
            DrawBeamDiagram13();
            DrawBeamDiagram14();
            DrawBeamDiagramKha19();
            DrawBeamDiagramTon20();
            DrawBeamDiagram1Phat();
            DrawBeamDiagramPhat();
            DrawBeamDiagramTon21();
            DrawBeamDiagramKha20();
            DrawBeamDiagramPhatSaigo1();
            DrawBeamDiagramKha21();
            DrawBeamDiagramKha22();
            DrawBeamDiagramKha23();
            DrawBeamDiagramKha24();
        }

    }
}