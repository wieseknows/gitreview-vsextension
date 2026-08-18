using GitReview.Shared.Constants;
using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace GitReview.VisualStudio.Services
{
    internal static class CliInstaller
    {
        public static async Task<bool> IsInstalledAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = CliConstants.ExecutableName,
                    Arguments = "--version",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);

                if (process == null)
                {
                    return false;
                }

                // Respect the timeout before accessing ExitCode.
                var exited = await Task.Run(() => process.WaitForExit(3000));
                if (!exited)
                {
                    try
                    {
                        process.Kill();
                    }
                    catch { }

                    return false;
                }

                // Read ExitCode only after the process has exited.
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<bool> InstallGlobalToolAsync(Action<string> logCallback)
        {
            return await Task.Run(() =>
            {
                try
                {
                    logCallback?.Invoke("📦 Installing wieseknows.GitReview CLI globally...");

                    var psi = new ProcessStartInfo
                    {
                        FileName = "dotnet",
                        Arguments = "tool install -g wieseknows.GitReview",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8
                    };

                    using (var process = new Process { StartInfo = psi })
                    {
                        process.OutputDataReceived += (s, e) => { if (e.Data != null) logCallback?.Invoke(e.Data); };
                        process.ErrorDataReceived += (s, e) => { if (e.Data != null) logCallback?.Invoke(e.Data); };

                        process.Start();
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        process.WaitForExit();

                        return process.ExitCode == 0;
                    }
                }
                catch (Exception ex)
                {
                    logCallback?.Invoke($"❌ Installation failed: {ex.Message}");
                    return false;
                }
            });
        }
    }
}