namespace TagCleaner
{
    /// <summary>
    /// The keep-rules loaded from config.xlsx. All names are matched exactly, ignoring case.
    /// </summary>
    public sealed class CleanerConfig
    {
        /// <summary>Folders kept together with everything inside them.</summary>
        public List<string> FoldersToKeepFull { get; set; } = new List<string>();

        /// <summary>Folders kept, but whose tags are reduced to those in <see cref="TagsToKeep"/>.</summary>
        public List<string> FoldersToKeepFiltered { get; set; } = new List<string>();

        /// <summary>Tag names allowed inside filtered folders.</summary>
        public List<string> TagsToKeep { get; set; } = new List<string>();

        public int TotalRules => FoldersToKeepFull.Count + FoldersToKeepFiltered.Count + TagsToKeep.Count;
    }
}
