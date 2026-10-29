namespace PasadorScript.DTO.Git
{
    public class RecreationResult
    {
        public bool Success { get; set; }
        public string NewBranchName { get; set; }
        public int NewPullRequestId { get; set; }
        public string OriginalCommitId { get; set; }
        public string ErrorMessage { get; set; }
    }
    public class GitCommit
    {
        public string CommitId { get; set; }
        public string Message { get; set; }
        public string Author { get; set; }
        public DateTimeOffset Date { get; set; }
        public List<string> Parents { get; set; } = new();
    }

    public class MergeRevertResult
    {
        public bool Success { get; set; }
        public string NewCommitId { get; set; }
        public string NewBranchName { get; set; }
        public int NewPullRequestId { get; set; }
        public string ErrorMessage { get; set; }
    }

    public class RevertStrategy
    {
        public bool CreateBackupBranch { get; set; } = true;
        public bool PreserveOriginalAuthorship { get; set; } = true;
        public string NewBranchPrefix { get; set; } = "revert";
        public bool AddRevertDescription { get; set; } = true;

        public static RevertStrategy GetStandardRevertStrategy()
        {
            return new RevertStrategy
            {
                CreateBackupBranch = true,
                PreserveOriginalAuthorship = true,
                NewBranchPrefix = "revert",
                AddRevertDescription = true
            };
        }

        public static RevertStrategy GetSafeRevertStrategy()
        {
            return new RevertStrategy
            {
                CreateBackupBranch = true,
                PreserveOriginalAuthorship = true,
                NewBranchPrefix = "safe-revert",
                AddRevertDescription = true
            };
        }

        public static RevertStrategy GetQuickRevertStrategy()
        {
            return new RevertStrategy
            {
                CreateBackupBranch = false,
                PreserveOriginalAuthorship = true,
                NewBranchPrefix = "quick-revert",
                AddRevertDescription = false
            };
        }

    }
}
