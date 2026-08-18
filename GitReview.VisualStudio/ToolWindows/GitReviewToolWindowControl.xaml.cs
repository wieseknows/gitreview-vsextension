using GitReview.Shared.Constants;
using GitReview.Shared.Enums;
using GitReview.Shared.Extensions;
using GitReview.Shared.Providers;
using GitReview.VisualStudio.Models;
using GitReview.VisualStudio.Options;
using GitReview.VisualStudio.Services;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace GitReview.VisualStudio.ToolWindows
{
    public partial class GitReviewToolWindowControl : UserControl
    {
        private ReviewExecutionMode SelectedMode => ModeComboBox.SelectedValue is ReviewExecutionMode mode
            ? mode
            : ReviewExecutionMode.AiReview;

        private AiProvider SelectedProvider => ProviderComboBox.SelectedValue is AiProvider provider
            ? provider
            : AiProvider.OpenRouter;

        private readonly GitReviewCliRunner _runner = new();
        private CancellationTokenSource? _cts;

        public GitReviewToolWindowControl()
        {
            InitializeComponent();
            UpdateModelsForSelectedProvider();
            InitComboBoxes();
            Log("Ready.");
        }

        private static List<DisplayOption<T>> EnumToDisplayOptions<T>() where T : struct, Enum
        {
            return Enum.GetValues(typeof(T))
                       .Cast<T>()
                       .Select(v => new DisplayOption<T>(v.GetDescription(), v))
                       .ToList();
        }

        private void InitComboBoxes()
        {
            ModeComboBox.ItemsSource = EnumToDisplayOptions<ReviewExecutionMode>();
            ModeComboBox.SelectedIndex = 0;

            ProviderComboBox.ItemsSource = EnumToDisplayOptions<AiProvider>();
            ProviderComboBox.SelectedIndex = 0;
        }

        private void ModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            AiConfigurationPanel.Visibility = SelectedMode == ReviewExecutionMode.AiReview
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        private void ProviderComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateModelsForSelectedProvider();
        }

        private void UpdateModelsForSelectedProvider()
        {
            ModelComboBox.ItemsSource = SelectedProvider.GetAvailableModels();
            ModelComboBox.SelectedIndex = 0;
        }

        private void OpenSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            GitReviewPackage.Instance?.ShowOptionPage(typeof(GitReviewOptionPage));
        }

        private void ReviewButton_Click(object sender, RoutedEventArgs e)
        {
            _ = GitReviewPackage.Instance.JoinableTaskFactory.RunAsync(ReviewAsync);
        }

        private async Task ReviewAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            ReviewButton.IsEnabled = false;
            ReviewButton.Content = "Processing...";
            ClearLog();

            try
            {

                bool isInstalled = await CliInstaller.IsInstalledAsync();
                if (!isInstalled)
                {
                    Log("⚠️ GitReview CLI tool was not found on this machine.");

                    await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(ct);

                    var result = MessageBox.Show(
                        "GitReview CLI tool is required to run code reviews.\n\nWould you like to install 'wieseknows.GitReview' automatically now?",
                        "GitReview CLI Missing",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        Log("Starting automatic installation...");
                        bool installSuccess = await CliInstaller.InstallGlobalToolAsync(LogOnUIThread);

                        if (installSuccess)
                        {
                            Log("✅ CLI tool successfully installed!\n");
                        }
                        else
                        {
                            Log("❌ Failed to install CLI tool. Please try running 'dotnet tool install -g wieseknows.GitReview' manually in terminal.");
                            return;
                        }
                    }
                    else
                    {
                        Log("Operation cancelled: CLI tool is required.");
                        return;
                    }
                }

                Log("Starting GitReview execution...");

                var solutionDir = GitReviewCliRunner.GetSolutionDirectory();
                if (string.IsNullOrEmpty(solutionDir))
                {
                    Log("[ERROR] Please open a solution first.");
                    return;
                }

                var repoDir = GitReviewCliRunner.FindGitRoot(solutionDir!);
                if (repoDir == null)
                {
                    Log("[ERROR] Solution is not inside a Git repository.");
                    return;
                }

                var args = BuildCliArgs();
                Log($"> {CliConstants.ExecutableName} {args}");

                int exitCode = await _runner.RunAsync(repoDir, args, LogOnUIThread, ct);

                if (exitCode == 0)
                {
                    Log("\n✅ Execution completed successfully!");
                }
                else
                {
                    Log($"\n❌ Process exited with code {exitCode}");
                }
            }
            catch (OperationCanceledException)
            {
                Log("\n⚠️ Operation cancelled by user.");
            }
            catch (Exception ex)
            {
                Log($"\n❌ [EXCEPTION] {ex.Message}");
            }
            finally
            {
                ReviewButton.IsEnabled = true;
                ReviewButton.Content = "Run GitReview";
            }
        }

        private string BuildCliArgs()
        {
            return SelectedMode switch
            {
                ReviewExecutionMode.PromptWithClipboard => "--prompt-only",
                ReviewExecutionMode.RawDiffOnly => "raw",
                _ => $"--ai -p {SelectedProvider.ToCliValue()} -m \"{ModelComboBox.Text}\""
            };
        }

        private void LogOnUIThread(string text)
        {
            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                Log(text);
            }).FileAndForget("...");
        }

        private void Log(string message)
        {
            LogTextBox.AppendText($"{message}\n");
            LogTextBox.ScrollToEnd();
        }

        private void ClearLog()
        {
            LogTextBox.Clear();
        }
    }
}