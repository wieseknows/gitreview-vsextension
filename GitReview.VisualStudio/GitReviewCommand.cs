using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel.Design;
using System.Threading.Tasks;

namespace GitReview.VisualStudio
{
    internal static class GitReviewCommand
    {
        public const int CommandId = 0x0100;

        public static readonly Guid CommandSet =
            new("f5b8e4a1-2c67-4d91-8e53-61a7c9b20f44");

        public static async Task InitializeAsync(
            AsyncPackage package)
        {
            await package.JoinableTaskFactory.SwitchToMainThreadAsync();

            var commandService =
                await package.GetServiceAsync(
                    typeof(IMenuCommandService))
                as OleMenuCommandService;

            if (commandService == null)
            {
                return;
            }

            var commandId = new CommandID(
                CommandSet,
                CommandId);

            var menuCommand = new MenuCommand(
                Execute,
                commandId);

            commandService.AddCommand(menuCommand);
        }

        private static void Execute(
            object sender,
            EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _ = GitReviewPackage.Instance.ShowGitReviewAsync();
        }
    }
}