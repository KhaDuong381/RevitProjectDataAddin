using System.Windows;

namespace RevitProjectDataAddin
{
    public partial class RenameProjectWindow : Window
    {
        public string NewProjectName { get; private set; }

        public RenameProjectWindow(string currentName)
        {
            InitializeComponent();
            NewNameBox.Text = currentName;
            NewNameBox.SelectAll();
            NewNameBox.Focus();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            string newName = NewNameBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                MessageBox.Show("Tên mới không được để trống.");
                return;
            }

            NewProjectName = newName;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
