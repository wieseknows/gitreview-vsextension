using Microsoft.VisualStudio.Shell;
using System;
using System.Runtime.InteropServices;

namespace GitReview.VisualStudio.ToolWindows
{
    [Guid(PackageGuids.ToolWindowGuidString)]
    public class GitReviewToolWindow : ToolWindowPane
    {
        public GitReviewToolWindow()
            : base(null)
        {
            Caption = "GitReview";
            Content = new GitReviewToolWindowControl();
        }
    }
}