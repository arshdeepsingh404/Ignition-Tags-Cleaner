using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace TagCleaner
{
    /// <summary>A folder or tag in the preview tree.</summary>
    public class TagNode : INotifyPropertyChanged
    {
        #region Fields & properties
        private bool _isExpanded;
        private Visibility _visibility = Visibility.Visible;

        public string Name { get; set; } = string.Empty;
        public string DataType { get; set; } = string.Empty;
        public bool IsFolder { get; set; }
        public bool HasAlarms { get; set; }
        public ObservableCollection<TagNode> Children { get; set; } = new ObservableCollection<TagNode>();

        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; OnPropertyChanged(nameof(IsExpanded)); } }
        }

        public Visibility Visibility
        {
            get => _visibility;
            set { if (_visibility != value) { _visibility = value; OnPropertyChanged(nameof(Visibility)); } }
        }
        #endregion

        #region Filtering
        public bool ApplyFilter(string folderQuery, string tagQuery, bool parentMatchesFolder)
        {
            bool folderQueryActive = !string.IsNullOrWhiteSpace(folderQuery);
            bool tagQueryActive = !string.IsNullOrWhiteSpace(tagQuery);

            // 1. Determine local matches
            bool matchesFolder = IsFolder && folderQueryActive && Name.Contains(folderQuery, System.StringComparison.OrdinalIgnoreCase);
            bool matchesTag = !IsFolder && tagQueryActive && Name.Contains(tagQuery, System.StringComparison.OrdinalIgnoreCase);

            // 2. Propagate "folder match" state to children
            // If this folder matches the search, all its contents are potentially visible
            bool effectiveFolderMatch = matchesFolder || parentMatchesFolder;

            // 3. Recurse to children
            bool anyChildMatches = false;
            foreach (var child in Children)
            {
                if (child.ApplyFilter(folderQuery, tagQuery, effectiveFolderMatch))
                {
                    anyChildMatches = true;
                }
            }

            // 4. Visibility Logic
            bool visible = false;

            if (!folderQueryActive && !tagQueryActive)
            {
                visible = true; // Show everything if no filter
            }
            else
            {
                if (IsFolder)
                {
                    // Folders are visible if:
                    // - They match the folder filter
                    // - OR one of their children is visible (matches tag or nested folder)
                    visible = matchesFolder || anyChildMatches;
                }
                else
                {
                    // Tags are visible if:
                    // - They match the tag filter
                    // - OR their parent matched the folder filter (and tag filter is empty)
                    if (tagQueryActive)
                    {
                        // The tag filter decides on its own, whether or not a folder filter is also active
                        visible = matchesTag;
                    }
                    else
                    {
                        // Only folder filter is active: tag is visible if it's inside a matching folder
                        visible = effectiveFolderMatch;
                    }
                }
            }

            // Update properties ONLY if they changed to prevent layout thrashing
            Visibility newVis = visible ? Visibility.Visible : Visibility.Collapsed;
            if (Visibility != newVis) Visibility = newVis;

            // Auto-expand if something inside matched
            if (visible && (folderQueryActive || tagQueryActive) && anyChildMatches)
            {
                if (!IsExpanded) IsExpanded = true;
            }

            return visible;
        }
        #endregion

        #region Expansion
        public void SetExpansion(bool expand)
        {
            IsExpanded = expand;
            foreach (var child in Children) child.SetExpansion(expand);
        }
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        #endregion
    }
}
