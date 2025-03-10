using System.Windows;

namespace AdHealthMonitor
{
    public partial class TestParametersWindow : Window
    {
        // Properties to store user inputs (initialized to avoid warnings)
        public string DomainControllers { get; private set; } = string.Empty;
        public string RecipientEmail { get; private set; } = string.Empty;

        public TestParametersWindow()
        {
            InitializeComponent();
        }

        // Overloaded constructor to accept parameters and prevent null values
        public TestParametersWindow(string currentDcs, string currentEmail) : this()
        {
            DomainControllers = currentDcs ?? string.Empty;
            RecipientEmail = currentEmail ?? string.Empty;

            dcTextBox.Text = DomainControllers;
            emailTextBox.Text = RecipientEmail;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            // Retrieve values from input fields, ensuring they are not null
            DomainControllers = dcTextBox.Text.Trim();
            RecipientEmail = emailTextBox.Text.Trim();


            // Indicate success and close the window
            System.Windows.MessageBox.Show(messageBoxText: "Settings saved successfully!",
                            "Success",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Close without saving
            DialogResult = false;
            Close();
        }
    }
}
