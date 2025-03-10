using Microsoft.Win32;
using Microsoft.Win32.TaskScheduler;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using System.Windows.Data;
using System.Windows.Documents;

namespace AdHealthMonitor
{
    public partial class MainWindow : Window
    {
        private string domainControllers = "";
        private string recipientEmail = "";
        private CancellationTokenSource cancellationTokenSource;
        private List<TestResult> allResults = new List<TestResult>();

        public MainWindow()
        {
            InitializeComponent();
            cancellationTokenSource = new CancellationTokenSource();

            // Load saved settings
            domainControllers = Properties.Settings.Default.DomainControllers;
            recipientEmail = Properties.Settings.Default.RecipientEmail;
        }


        public class TestResult
        {
            public string Service { get; set; } = string.Empty;
            public string Server { get; set; } = string.Empty;
            public string Result { get; set; } = string.Empty;
            public string Message { get; set; } = string.Empty;
            public string LogFilePath { get; set; } = string.Empty;
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var paramWindow = new TestParametersWindow(domainControllers, recipientEmail);
            paramWindow.Owner = this;

            bool? result = paramWindow.ShowDialog();
            if (result == true)
            {
                domainControllers = paramWindow.DomainControllers ?? "";
                recipientEmail = paramWindow.RecipientEmail ?? "";

                // Save settings persistently
                Properties.Settings.Default.DomainControllers = domainControllers;
                Properties.Settings.Default.RecipientEmail = recipientEmail;
                Properties.Settings.Default.Save();
            }
        }


        private void ScheduleTestsButton_Click(object sender, RoutedEventArgs e)
        {
            var schedulerWindow = new TaskSchedulerWindow();
            schedulerWindow.Owner = this;
            schedulerWindow.ShowDialog();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                // Get full log file path from hyperlink
                string logFilePath = new Uri(e.Uri.AbsoluteUri).LocalPath;

                // Get the specific service name from the Tag property
                string selectedService = ((Hyperlink)sender).Tag?.ToString();

                if (!File.Exists(logFilePath))
                {
                    System.Windows.MessageBox.Show("Log file not found!", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Read the log file and extract the relevant section
                string[] logLines = File.ReadAllLines(logFilePath);
                List<string> filteredLines = new List<string>();

                bool capture = false;
                foreach (string line in logLines)
                {
                    // Detect the start of the test section
                    if (!string.IsNullOrEmpty(selectedService) && line.Contains($"Starting test: {selectedService}"))
                    {
                        capture = true;
                    }

                    if (capture)
                    {
                        filteredLines.Add(line);
                    }

                    // Stop capturing when test section ends
                    if (capture && (line.Contains("End of test") || line.Contains("Test completed") || line.Contains($"Finished test: {selectedService}")))
                    {
                        break;
                    }
                }

                if (filteredLines.Count == 0)
                {
                    System.Windows.MessageBox.Show($"No specific log section found for {selectedService}.", "Log Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Create a temporary log file
                string tempLogPath = Path.Combine(Path.GetTempPath(), $"{selectedService}_Log.txt");
                File.WriteAllLines(tempLogPath, filteredLines);

                // Open the temporary log file in Notepad
                Process.Start(new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = tempLogPath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error opening log: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            if (allResults.Count == 0)
            {
                System.Windows.MessageBox.Show("No test results available to export.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            System.Windows.Forms.SaveFileDialog saveFileDialog = new System.Windows.Forms.SaveFileDialog
            {
                Filter = "Log Files (*.txt)|*.txt",
                FileName = "AD_Test_Results.txt"
            };

            bool v = saveFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK;
            if (v)
            {
                try
                {
                    using (StreamWriter writer = new StreamWriter(saveFileDialog.FileName))
                    {
                        writer.WriteLine("AD Test Results:");
                        writer.WriteLine("---------------------------------------------------");

                        foreach (var result in allResults)
                        {
                            writer.WriteLine($"Service: {result.Service}");
                            writer.WriteLine($"Server: {result.Server}");
                            writer.WriteLine($"Result: {result.Result}");
                            writer.WriteLine($"Message: {result.Message}");
                            writer.WriteLine($"Log File: {result.LogFilePath}");
                            writer.WriteLine("---------------------------------------------------");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"Failed to export logs: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(domainControllers))
            {
                System.Windows.MessageBox.Show("No domain controllers specified. Please configure settings first.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;
            allResults.Clear();

            string[] dcList = domainControllers.Split(',').Select(dc => dc.Trim()).ToArray();
            ProgressPanel.Visibility = Visibility.Visible;
            TestProgressBar.Value = 0;
            TestProgressBar.Maximum = dcList.Length;

            foreach (string dc in dcList)
            {
                if (token.IsCancellationRequested)
                {
                    System.Windows.MessageBox.Show("Test execution stopped by user.", "Stopped", MessageBoxButton.OK, MessageBoxImage.Information);
                    break;
                }

                string logFilePath = $"C:\\ADCheckLogs\\{dc}_TestResults.txt";
                Directory.CreateDirectory("C:\\ADCheckLogs");

                string dcdiagResult = await RunCommandAsync($"dcdiag /s:{dc} /c /v", logFilePath, token);
                string repadminResult = await RunCommandAsync($"repadmin /showrepl {dc}", logFilePath, token);

                var parsedResults = ParseDCDiagOutput(dc, dcdiagResult, logFilePath);
                allResults.AddRange(parsedResults);

                TestProgressBar.Value += 1;
            }

            testResultsGrid.ItemsSource = allResults;
            ProgressPanel.Visibility = Visibility.Collapsed;
        }

        private async Task<string> RunCommandAsync(string command, string logFilePath, CancellationToken cancellationToken)
        {
            return await System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    using (Process process = new Process())
                    {
                        process.StartInfo = new ProcessStartInfo
                        {
                            FileName = "cmd.exe",
                            Arguments = $"/C {command}",
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                        process.Start();
                        string output = process.StandardOutput.ReadToEnd();
                        string error = process.StandardError.ReadToEnd();
                        process.WaitForExit();

                        using (StreamWriter writer = new StreamWriter(logFilePath, true))
                        {
                            writer.WriteLine($"Command: {command}");
                            writer.WriteLine($"Timestamp: {DateTime.Now}");
                            writer.WriteLine("---- OUTPUT ----");
                            writer.WriteLine(output);
                            writer.WriteLine("---- ERROR ----");
                            writer.WriteLine(error);
                            writer.WriteLine("-------------------------------");
                        }

                        return output + error;
                    }
                }
                catch (Exception ex)
                {
                    File.AppendAllText(logFilePath, $"Error running command: {ex.Message}\n");
                    return "ERROR";
                }
            }, cancellationToken);
        }

        private List<TestResult> ParseDCDiagOutput(string server, string output, string logFilePath)
        {
            List<TestResult> results = new List<TestResult>();
            string[] lines = output.Split('\n');
            TestResult lastTestResult = null;

            foreach (var line in lines)
            {
                if (line.Contains("Starting test:"))
                {
                    string testName = line.Replace("Starting test:", "").Trim();
                    lastTestResult = new TestResult
                    {
                        Service = testName,
                        Server = server,
                        Result = "In Progress",
                        Message = "Awaiting result...",
                        LogFilePath = logFilePath
                    };
                    results.Add(lastTestResult);
                }
                else if (line.Contains("failed") || line.Contains("passed"))
                {
                    if (lastTestResult == null)
                    {
                        lastTestResult = new TestResult
                        {
                            Service = "Unknown Test",
                            Server = server,
                            Result = line.Contains("failed") ? "FAIL" : "PASS",
                            Message = line.Trim(),
                            LogFilePath = logFilePath
                        };
                        results.Add(lastTestResult);
                    }
                    else
                    {
                        lastTestResult.Result = line.Contains("failed") ? "FAIL" : "PASS";
                        lastTestResult.Message = line.Trim();
                    }
                }
            }

            return results;
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            cancellationTokenSource.Cancel();
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            SearchPlaceholder.Visibility = Visibility.Collapsed; // Hide placeholder when user focuses
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                SearchPlaceholder.Visibility = Visibility.Visible; // Show placeholder if empty
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            string searchText = SearchBox.Text.ToLower();
            testResultsGrid.ItemsSource = allResults
                .Where(r => r.Service.ToLower().Contains(searchText) ||
                            r.Server.ToLower().Contains(searchText) ||
                            r.Result.ToLower().Contains(searchText) ||
                            r.Message.ToLower().Contains(searchText))
                .ToList();
        }
    }
}
