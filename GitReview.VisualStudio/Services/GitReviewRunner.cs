using GitReview.Shared.Constants;
using GitReview.Shared.Enums;
using GitReview.Shared.Providers;
using GitReview.VisualStudio.Options;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GitReview.VisualStudio.Services
{
    public class GitReviewCliRunner
    {
        public async Task<int> RunAsync(string repoDir, string args, Action<string> logCallback, CancellationToken ct)
        {
            var cliExePath = CliConstants.ExecutableName;
            var psi = new ProcessStartInfo
            {
                FileName = cliExePath,
                Arguments = args,
                WorkingDirectory = repoDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            ApplyOptionsEnvironmentVariables(psi);

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    logCallback(e.Data);
                }
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null)
                {
                    logCallback($"[ERROR] {e.Data}");
                }
            };

            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start {cliExePath} process.");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await Task.Run(() =>
            {
                while (!process.WaitForExit(500))
                {
                    if (ct.IsCancellationRequested)
                    {
                        try { process.Kill(); } catch { }
                        ct.ThrowIfCancellationRequested();
                    }
                }
            }, ct);

            return process.ExitCode;
        }

        private static void ApplyOptionsEnvironmentVariables(ProcessStartInfo psi)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var options = (GitReviewOptionPage)GitReviewPackage.Instance.GetDialogPage(typeof(GitReviewOptionPage));

            if (options == null)
            {
                return;
            }

            foreach (AiProvider provider in Enum.GetValues(typeof(AiProvider)))
            {
                var apiKey = options.GetApiKey(provider);

                // Preserve inherited environment variables when no key is configured.
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    continue;
                }

                var envVar = provider.GetApiKeyEnvVar();
                psi.EnvironmentVariables[envVar] = apiKey;
            }
        }

        public static string? GetSolutionDirectory()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var solution = Package.GetGlobalService(typeof(SVsSolution)) as IVsSolution;
            if (solution == null)
            {
                return null;
            }

            var hr = solution.GetSolutionInfo(out var dir, out var file, out _);
            if (ErrorHandler.Failed(hr))
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(dir))
            {
                return dir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, ' ', '\0');
            }
            if (!string.IsNullOrWhiteSpace(file))
            {
                return Path.GetDirectoryName(file);
            }

            return null;
        }

        public static string? FindGitRoot(string startDir)
        {
            var dir = new DirectoryInfo(startDir);
            while (dir != null)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, ".git")))
                {
                    return dir.FullName;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}