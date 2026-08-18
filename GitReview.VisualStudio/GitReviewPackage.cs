using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace GitReview.VisualStudio
{
    [PackageRegistration(
        UseManagedResourcesOnly = true,
        AllowsBackgroundLoading = true)]

    [InstalledProductRegistration(
        VsixConstants.CategoryName,
        VsixConstants.ProductDetails,
        VsixConstants.ProductVersion)]

    [ProvideMenuResource("Menus.ctmenu", 1)]

    [ProvideToolWindow(typeof(ToolWindows.GitReviewToolWindow))]

    [ProvideOptionPage(typeof(Options.GitReviewOptionPage), VsixConstants.CategoryName, VsixConstants.PageName, 0, 0, true)]

    [Guid(PackageGuids.PackageGuidString)]
    public sealed class GitReviewPackage : AsyncPackage
    {
        public static GitReviewPackage Instance { get; private set; } = null!;

        protected override async Task InitializeAsync(
            CancellationToken cancellationToken,
            IProgress<ServiceProgressData> progress)
        {
            Instance = this;

            await GitReviewCommand.InitializeAsync(this);
        }

        internal async Task ShowGitReviewAsync()
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync();

            await ShowToolWindowAsync(
                typeof(ToolWindows.GitReviewToolWindow),
                0,
                true,
                CancellationToken.None);
        }
    }
}