using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;

namespace TagCleaner
{
    public partial class TagPreviewWindow : Window
    {
        #region Constructor & tree building
        public ObservableCollection<TagNode> Nodes { get; set; } = new ObservableCollection<TagNode>();

        public TagPreviewWindow(JsonArray tags, string title, string subtitle)
        {
            InitializeComponent();
            WindowTitle.Text = title;
            WindowSubtitle.Text = subtitle;
            
            BuildTree(tags, Nodes);
            TagTree.ItemsSource = Nodes;
        }

        private void BuildTree(JsonArray array, ObservableCollection<TagNode> parentCollection)
        {
            var sortedItems = array
                .Where(item => item != null)
                .OrderBy(item => (item!["tagType"]?.ToString() ?? "").Equals("Folder", System.StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                .ThenBy(item => item!["name"]?.ToString() ?? "");

            foreach (var item in sortedItems)
            {
                if (item == null) continue;

                var node = new TagNode
                {
                    Name = item["name"]?.ToString() ?? "Unknown",
                    DataType = item["dataType"]?.ToString() ?? "",
                    IsFolder = (item["tagType"]?.ToString() ?? "").Equals("Folder", System.StringComparison.OrdinalIgnoreCase),
                    HasAlarms = item["alarms"] is JsonArray alarmsArr && alarmsArr.Count > 0
                };

                if (node.IsFolder && item["tags"] is JsonArray children)
                {
                    BuildTree(children, node.Children);
                }

                parentCollection.Add(node);
            }
        }
        #endregion

        #region Filtering & expansion
        private void FilterBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string folderQuery = FolderSearchBox.Text;
            string tagQuery = TagSearchBox.Text;
            
            // Apply filtering logic to all root nodes
            foreach (var node in Nodes)
            {
                node.ApplyFilter(folderQuery, tagQuery, false);
            }
        }

        private void ExpandAll_Click(object sender, RoutedEventArgs e) { foreach (var node in Nodes) node.SetExpansion(true); }
        private void CollapseAll_Click(object sender, RoutedEventArgs e) { foreach (var node in Nodes) node.SetExpansion(false); }
        #endregion
    }
}
