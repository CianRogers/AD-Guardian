using Microsoft.Win32.TaskScheduler;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace AdHealthMonitor
{
    public partial class TaskSchedulerWindow : Window
    {
        public TaskSchedulerWindow()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string taskName = TaskNameTextBox.Text.Trim();
                string frequency = (FrequencyComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
                DateTime? startDate = StartDatePicker.SelectedDate;
                string startTime = StartTimeTextBox.Text.Trim();

                // Validate Inputs
                if (string.IsNullOrEmpty(taskName) || startDate == null || string.IsNullOrEmpty(startTime))
                {
                    System.Windows.MessageBox.Show("Please provide all required inputs.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!TimeSpan.TryParse(startTime, out TimeSpan parsedTime))
                {
                    System.Windows.MessageBox.Show("Invalid time format. Use HH:mm (e.g., 14:00).", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                DateTime startBoundary = startDate.Value.Add(parsedTime);

                using (TaskService ts = new TaskService())
                {
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = "AD Health Monitor Scheduled Test";

                    switch (frequency)
                    {
                        case "Daily":
                            td.Triggers.Add(new DailyTrigger { DaysInterval = 1, StartBoundary = startBoundary });
                            break;
                        case "Weekly":
                            td.Triggers.Add(new WeeklyTrigger { StartBoundary = startBoundary, DaysOfWeek = DaysOfTheWeek.Sunday });
                            break;
                        case "Hourly":
                            td.Triggers.Add(new TimeTrigger { StartBoundary = startBoundary, Repetition = new RepetitionPattern(TimeSpan.FromHours(1), TimeSpan.Zero) });
                            break;
                        default:
                            System.Windows.MessageBox.Show("Invalid frequency selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                    }

                    string exePath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (string.IsNullOrEmpty(exePath) || !System.IO.File.Exists(exePath))
                    {
                        System.Windows.MessageBox.Show("Application executable not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    td.Actions.Add(new ExecAction(exePath, "-runScheduled", null));
                    ts.RootFolder.RegisterTaskDefinition(taskName, td);

                    System.Windows.MessageBox.Show("Task scheduled successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                    this.DialogResult = true;
                    this.Close();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to schedule task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
