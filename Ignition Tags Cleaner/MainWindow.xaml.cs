using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Input;

namespace TagCleaner
{
    public partial class MainWindow : Window
    {
        #region Types & fields
        private sealed class FileAnalysis
        {
            public string FilePath { get; init; } = string.Empty;
            public string FileName { get; init; } = string.Empty;
            public long FileSizeBytes { get; init; }
            public string PlcName { get; set; } = TagExport.UnknownPlc;
            public int TotalTags { get; set; }
            public int RemovedFolders { get; set; }
            public int RemovedTags { get; set; }
            public int RemainingTags => TotalTags - RemovedTags;
        }

        private const string ConfigExcelFileName = "config.xlsx";

        private List<string> _selectedFilePaths = new List<string>();
        private List<FileAnalysis> _fileAnalyses = new List<FileAnalysis>();
        private string _lastExportPath = string.Empty;
        private CleanerConfig _config = new CleanerConfig();
        private DateTime _lastConfigModified = DateTime.MinValue;
        #endregion

        #region Constructor
        public MainWindow()
        {
            InitializeComponent();
            Activated += MainWindow_Activated;
            LoadConfiguration();
        }
        #endregion

        #region Configuration
        private void MainWindow_Activated(object? sender, EventArgs e)
        {
            // Pick up edits made to config.xlsx while the app was in the background.
            string excelPath = LocateConfigExcelFile();
            DateTime currentModified = File.Exists(excelPath) ? File.GetLastWriteTimeUtc(excelPath) : DateTime.MinValue;
            if (currentModified != _lastConfigModified)
            {
                LoadConfiguration();
                if (_selectedFilePaths.Count > 0) AnalyzeFiles(_selectedFilePaths);
            }
        }

        private static string LocateConfigExcelFile()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string beside = Path.Combine(baseDir, ConfigExcelFileName);

            var candidates = new[]
            {
                beside,
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", ConfigExcelFileName)),
                Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", ConfigExcelFileName)),
                Path.GetFullPath(ConfigExcelFileName)
            };

            return candidates.FirstOrDefault(File.Exists) ?? beside;
        }

        private void LoadConfiguration()
        {
            try
            {
                string excelPath = LocateConfigExcelFile();
                if (File.Exists(excelPath))
                {
                    _lastConfigModified = File.GetLastWriteTimeUtc(excelPath);
                    _config = ConfigLoader.Load(excelPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error reading Excel config: " + ex.Message, "Excel Config Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            int fullCount = _config.FoldersToKeepFull.Count;
            int filteredCount = _config.FoldersToKeepFiltered.Count;
            int tagsCount = _config.TagsToKeep.Count;

            TotalRulesText.Text = $"{_config.TotalRules:N0} Active";
            TotalRulesText.ToolTip = $"Full Folders: {fullCount}\nFiltered Folders: {filteredCount}\nSpecific Tags: {tagsCount}";
            FullFoldersRuleCountText.Text = fullCount.ToString("N0");
            FilteredFoldersRuleCountText.Text = filteredCount.ToString("N0");
            TagsToKeepRuleCountText.Text = tagsCount.ToString("N0");
        }

        private void EditConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string excelPath = LocateConfigExcelFile();
                if (File.Exists(excelPath))
                {
                    Process.Start(new ProcessStartInfo { FileName = excelPath, UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("Could not locate config.xlsx file.", "File Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error opening Excel file: " + ex.Message, "Excel Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        #endregion

        #region File selection & analysis
        private void SelectFile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                Multiselect = true
            };
            if (openFileDialog.ShowDialog() == true && openFileDialog.FileNames.Length > 0)
            {
                AnalyzeFiles(openFileDialog.FileNames);
            }
        }

        private void File_DragOver(object sender, DragEventArgs e) =>
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

        private void File_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            var jsonFiles = new List<string>();
            foreach (var item in (string[])e.Data.GetData(DataFormats.FileDrop))
            {
                if (File.Exists(item) && Path.GetExtension(item).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    jsonFiles.Add(item);
                }
                else if (Directory.Exists(item))
                {
                    jsonFiles.AddRange(Directory.GetFiles(item, "*.json", SearchOption.AllDirectories));
                }
            }
            if (jsonFiles.Count > 0) AnalyzeFiles(jsonFiles);
        }

        /// <summary>Scans the files with a dry-run filter and refreshes the statistics.</summary>
        private async void AnalyzeFiles(IEnumerable<string> paths)
        {
            var validPaths = paths.Where(File.Exists).Distinct().ToList();
            if (validPaths.Count == 0) return;

            _selectedFilePaths = validPaths;
            var config = _config;
            int totalFiles = validPaths.Count;

            try
            {
                if (totalFiles == 1)
                    BeginBusy($"Analyzing {Path.GetFileName(validPaths[0])}...", "Reading tags...");
                else
                    BeginBusy($"Analyzing {totalFiles} PLC tag export files...", $"0 of {totalFiles} files scanned", totalFiles);

                var analyses = await Task.Run(() =>
                {
                    var list = new List<FileAnalysis>();
                    for (int i = 0; i < totalFiles; i++)
                    {
                        var fi = new FileInfo(validPaths[i]);
                        string sizeText = $"{fi.Length / 1024.0 / 1024.0:F1} MB";
                        int fileNumber = i + 1;

                        Dispatcher.Invoke(() =>
                        {
                            if (totalFiles > 1)
                                ReportProgress($"Analyzing {fi.Name} (File {fileNumber} of {totalFiles})...", $"{sizeText} • File {fileNumber} of {totalFiles}", fileNumber - 1);
                            else
                                ReportProgress($"Analyzing {fi.Name} ({sizeText})...", "Evaluating rules...");
                        });

                        var analysis = new FileAnalysis { FilePath = fi.FullName, FileName = fi.Name, FileSizeBytes = fi.Length };

                        TagExport? export = TagExport.Load(fi.FullName);
                        if (export != null)
                        {
                            analysis.PlcName = export.PlcName;
                            analysis.TotalTags = export.Count().Tags;

                            FilterResult removed = TagFilter.Apply(export, config);
                            analysis.RemovedFolders = removed.RemovedFolders;
                            analysis.RemovedTags = removed.RemovedTags;
                        }
                        list.Add(analysis);

                        if (totalFiles > 1)
                        {
                            Dispatcher.Invoke(() => ReportProgress(null, $"{fileNumber} of {totalFiles} files scanned", fileNumber));
                        }
                    }
                    return list;
                });

                _fileAnalyses = analyses;
                ShowAnalysis();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Scan Error: " + ex.Message);
            }
            finally
            {
                EndBusy();
            }
        }

        private void ShowAnalysis()
        {
            int fileCount = _fileAnalyses.Count;
            long totalBytes = _fileAnalyses.Sum(a => a.FileSizeBytes);
            int totalTags = _fileAnalyses.Sum(a => a.TotalTags);
            int removedTags = _fileAnalyses.Sum(a => a.RemovedTags);

            if (fileCount == 1)
            {
                FileNameText.Text = _fileAnalyses[0].FileName;
                FileNameText.ToolTip = _fileAnalyses[0].FilePath;
                SelectedPLCNameDisplay.Text = _fileAnalyses[0].PlcName;
                SelectedPLCNameDisplay.ToolTip = null;
            }
            else
            {
                FileNameText.Text = $"{fileCount} Files Selected";
                FileNameText.ToolTip = string.Join("\n", _fileAnalyses.Select(a => a.FileName));

                var distinctPlcs = _fileAnalyses.Select(a => a.PlcName).Distinct().ToList();
                SelectedPLCNameDisplay.Text = distinctPlcs.Count <= 2
                    ? string.Join(", ", distinctPlcs)
                    : $"Multiple ({distinctPlcs.Count} PLCs)";
                SelectedPLCNameDisplay.ToolTip = string.Join("\n", _fileAnalyses.Select(a => $"{a.FileName} → {a.PlcName}"));
            }

            FileSizeText.Text = $"{totalBytes / 1024.0 / 1024.0:F2} MB";
            TotalTagsText.Text = totalTags.ToString("N0");
            TagsToRemoveText.Text = removedTags.ToString("N0");
            TagsRemainingText.Text = _fileAnalyses.Sum(a => a.RemainingTags).ToString("N0");
            FoldersRemovedText.Text = _fileAnalyses.Sum(a => a.RemovedFolders).ToString("N0");

            double reductionPercent = totalTags > 0 ? (double)removedTags / totalTags * 100 : 0;
            ReductionRatioText.Text = $"{reductionPercent:F1}%";
        }
        #endregion

        #region Previews
        private void PreviewAll_Click(object sender, RoutedEventArgs e) => ShowPreview(optimized: false);

        private void PreviewOptimized_Click(object sender, RoutedEventArgs e) => ShowPreview(optimized: true);

        private async void ShowPreview(bool optimized)
        {
            if (_selectedFilePaths.Count == 0) return;

            var files = _selectedFilePaths.ToList();
            var config = _config;
            string kind = optimized ? "Optimized" : "Original";

            try
            {
                string fileLabel = files.Count == 1 ? Path.GetFileName(files[0]) : $"{files.Count} files";
                BeginBusy(
                    optimized ? $"Preparing Optimized Tags Preview for {fileLabel}..." : $"Loading Original Tags Preview for {fileLabel}...",
                    optimized ? "Filtering tags and evaluating rules..." : "Reading and parsing tag data...");

                JsonArray tags = await Task.Run(() => BuildPreviewTags(files, optimized ? config : null));
                if (tags.Count == 0) return;

                string title = files.Count == 1 ? $"{kind} Tags Preview" : $"{kind} Tags Preview (All Files)";
                string subtitle = (optimized, files.Count == 1) switch
                {
                    (false, true) => $"Showing all tags from {Path.GetFileName(files[0])}.",
                    (false, false) => $"Showing tags from {files.Count} files.",
                    (true, true) => "Showing tags remaining after rules applied.",
                    (true, false) => $"Showing tags remaining across {files.Count} files after rules applied."
                };

                var preview = new TagPreviewWindow(tags, title, subtitle) { Owner = this };
                EndBusy();
                preview.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Preview Error: " + ex.Message, "Preview Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                EndBusy();
            }
        }

        /// <summary>
        /// Builds the tree shown in the preview: a single file shows its top-level tags; several files
        /// are shown side by side, each under its root folder (or a folder named after the file).
        /// </summary>
        private static JsonArray BuildPreviewTags(IReadOnlyList<string> files, CleanerConfig? filterConfig)
        {
            var combined = new JsonArray();

            foreach (var file in files)
            {
                TagExport? export = TagExport.Load(file);
                if (export?.Tags == null) continue;

                if (filterConfig != null) TagFilter.Apply(export, filterConfig);

                if (files.Count == 1) return export.Tags;

                if (export.RootIsFolder)
                {
                    combined.Add(export.Root);
                }
                else
                {
                    combined.Add(new JsonObject
                    {
                        ["name"] = Path.GetFileNameWithoutExtension(file),
                        ["tagType"] = "Folder",
                        ["tags"] = export.Tags.DeepClone()
                    });
                }
            }

            return combined;
        }
        #endregion

        #region Export
        private async void Process_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedFilePaths.Count == 0) return;
            LoadConfiguration();

            var openFolderDialog = new OpenFolderDialog
            {
                Title = "Select Destination Folder",
                InitialDirectory = Path.GetDirectoryName(_selectedFilePaths[0])
            };
            if (openFolderDialog.ShowDialog() != true) return;

            var files = _selectedFilePaths.ToList();
            var config = _config;
            string destination = openFolderDialog.FolderName;
            string folderPrefix = TagExporter.GetFolderPrefix(_fileAnalyses.Select(a => a.PlcName));
            int totalFiles = files.Count;

            try
            {
                if (totalFiles == 1)
                    BeginBusy($"Exporting {Path.GetFileName(files[0])}...", "Optimizing tags and writing export...");
                else
                    BeginBusy($"Exporting {totalFiles} files...", $"0 of {totalFiles} files complete", totalFiles);

                ExportResult result = await Task.Run(() => TagExporter.Run(files, config, destination, folderPrefix, DateTime.Now, (index, file) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        string fileName = Path.GetFileName(file);
                        if (totalFiles > 1)
                            ReportProgress($"Exporting {fileName} (File {index + 1} of {totalFiles})...", $"{index} of {totalFiles} files complete", index);
                        else
                            ReportProgress($"Exporting {fileName}...", "Writing JSON...");
                    });
                }));

                _lastExportPath = result.OutputFolder;
                SuccessDetailsText.Text =
                    $"Successfully processed {result.FilesProcessed} file(s) and filtered out {result.RemovedFolders + result.RemovedTags} items\n" +
                    $"({result.RemovedFolders} folders and {result.RemovedTags} tags).\n\nFiles exported to:\n{result.OutputFolder}";
                SuccessOverlay.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            finally
            {
                EndBusy();
            }
        }

        private void OpenExportFolder_Click(object sender, RoutedEventArgs e)
        {
            if (Directory.Exists(_lastExportPath))
            {
                Process.Start(new ProcessStartInfo { FileName = _lastExportPath, UseShellExecute = true });
            }
        }

        private void CloseSuccessOverlay_Click(object sender, RoutedEventArgs e) => SuccessOverlay.Visibility = Visibility.Collapsed;
        #endregion

        #region Busy indicator
        /// <summary>Shows the progress bar and disables the action buttons. A step count of 0 means indeterminate.</summary>
        private void BeginBusy(string status, string detail, int steps = 0)
        {
            ProcessingIndicator.Visibility = Visibility.Visible;
            ProcessingProgressBar.IsIndeterminate = steps == 0;
            ProcessingProgressBar.Minimum = 0;
            ProcessingProgressBar.Maximum = Math.Max(steps, 1);
            ProcessingProgressBar.Value = 0;
            ProcessingStatusText.Text = status;
            ProcessingDetailText.Text = detail;

            PreviewAllButton.IsEnabled = false;
            PreviewOptimizedButton.IsEnabled = false;
            ProcessButton.IsEnabled = false;
            Mouse.OverrideCursor = Cursors.Wait;
        }

        private void ReportProgress(string? status, string detail, int? step = null)
        {
            if (status != null) ProcessingStatusText.Text = status;
            ProcessingDetailText.Text = detail;
            if (step.HasValue) ProcessingProgressBar.Value = step.Value;
        }

        private void EndBusy()
        {
            ProcessingIndicator.Visibility = Visibility.Collapsed;
            Mouse.OverrideCursor = null;

            bool hasFiles = _selectedFilePaths.Count > 0;
            PreviewAllButton.IsEnabled = hasFiles;
            PreviewOptimizedButton.IsEnabled = hasFiles;
            ProcessButton.IsEnabled = hasFiles;
        }
        #endregion

        #region About
        private void About_Click(object sender, RoutedEventArgs e) => new AboutWindow { Owner = this }.ShowDialog();
        #endregion
    }
}
