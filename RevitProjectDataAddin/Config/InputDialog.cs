using System.Windows;
using System.Windows.Controls;
using TextBox = System.Windows.Controls.TextBox;

namespace RevitProjectDataAddin
{
    public class InputDialog : Window
    {
        private TextBox inputTextBox;
        public string ResponseText { get; set; }
        public string InitialText
        {
            get => inputTextBox.Text;
            set => inputTextBox.Text = value;
        }

        public InputDialog(string question)
        {
            Title = question;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Width = 500;
            Height = 250;
            FontSize = 18;
            ResizeMode = ResizeMode.NoResize;

            var stackPanel1 = new StackPanel();
            var stackPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var label = new Label { Content = "名前:", Margin = new Thickness(20, 20, 10, 20), FontSize = 18 };
            inputTextBox = new TextBox { Margin = new Thickness(0, 20, 20, 20), FontSize = 18, Width = 200, Height = 24 };
            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
            var okButton = new Button { Content = "確定", Width = 75, Margin = new Thickness(10, 10, 40, 10), FontSize = 18 };
            var cancelButton = new Button { Content = "取消", Width = 75, Margin = new Thickness(40, 10, 10, 10), FontSize = 18 };

            okButton.Click += OkButton_Click;
            cancelButton.Click += CancelButton_Click;

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);

            stackPanel.Children.Add(label);
            stackPanel.Children.Add(inputTextBox);
            stackPanel1.Children.Add(stackPanel);
            stackPanel1.Children.Add(buttonPanel);

            Content = stackPanel1;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseText = inputTextBox.Text;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
