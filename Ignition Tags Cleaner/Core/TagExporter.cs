using System.IO;
using System.Text.Json;

namespace TagCleaner
{
    public sealed class ExportResult
    {
        public string OutputFolder { get; init; } = string.Empty;
        public List<string> OutputFiles { get; } = new List<string>();
        public string SummaryFile { get; set; } = string.Empty;
        public int FilesProcessed { get; set; }
        public int RemovedFolders { get; set; }
        public int RemovedTags { get; set; }
    }

    /// <summary>
    /// Filters export files and writes the optimized JSON plus a text summary into a new dated folder.
    /// </summary>
    public static class TagExporter
    {
        #region Folder naming
        private const string DefaultFolderPrefix = "Ignition";

        /// <summary>
        /// Names the output folder after the PLC(s): one name as-is, two or three joined with "-",
        /// otherwise a generic prefix.
        /// </summary>
        public static string GetFolderPrefix(IEnumerable<string> plcNames)
        {
            var distinct = plcNames
                .Where(p => !string.IsNullOrWhiteSpace(p) && p != TagExport.UnknownPlc && p != "-")
                .Distinct()
                .ToList();

            if (distinct.Count == 1) return distinct[0];
            if (distinct.Count > 1 && distinct.Count <= 3) return string.Join("-", distinct);
            return DefaultFolderPrefix;
        }
        #endregion

        #region Export
        /// <param name="onFileStarting">Called with the zero-based index and path of each file before it is processed.</param>
        public static ExportResult Run(
            IReadOnlyList<string> files,
            CleanerConfig config,
            string destinationFolder,
            string folderPrefix,
            DateTime now,
            Action<int, string>? onFileStarting = null)
        {
            string dateStr = now.ToString("yyyyMMdd");
            string cleanPrefix = RemoveInvalidFileNameChars(folderPrefix);
            if (string.IsNullOrWhiteSpace(cleanPrefix)) cleanPrefix = DefaultFolderPrefix;
            string safePrefix = cleanPrefix.Replace(" ", "_");

            string folderName = $"{cleanPrefix} Tags Export {dateStr}";
            string outputFolder = Path.Combine(destinationFolder, folderName);
            for (int n = 1; Directory.Exists(outputFolder); n++)
            {
                outputFolder = Path.Combine(destinationFolder, $"{folderName} ({n})");
            }
            Directory.CreateDirectory(outputFolder);

            var result = new ExportResult { OutputFolder = outputFolder };
            var fileSummaries = new List<string>();

            for (int i = 0; i < files.Count; i++)
            {
                string file = files[i];
                onFileStarting?.Invoke(i, file);

                TagExport? export = TagExport.Load(file);
                if (export == null) continue;

                FilterResult removed = TagFilter.Apply(export, config);

                string safePlcName = RemoveInvalidFileNameChars(export.PlcName).Replace(" ", "_");
                if (string.IsNullOrWhiteSpace(safePlcName) || export.PlcName == TagExport.UnknownPlc)
                {
                    safePlcName = Path.GetFileNameWithoutExtension(file).Replace(" ", "_");
                }

                string outputFileName = $"{safePlcName}_Tags_Optimized_{dateStr}.json";
                for (int n = 2; File.Exists(Path.Combine(outputFolder, outputFileName)); n++)
                {
                    outputFileName = $"{safePlcName}_{n}_Tags_Optimized_{dateStr}.json";
                }

                File.WriteAllText(
                    Path.Combine(outputFolder, outputFileName),
                    export.Root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

                result.OutputFiles.Add(outputFileName);
                result.FilesProcessed++;
                result.RemovedFolders += removed.RemovedFolders;
                result.RemovedTags += removed.RemovedTags;

                fileSummaries.Add(
                    $"Source File: {Path.GetFileName(file)}\n" +
                    $"  PLC Name: {export.PlcName}\n" +
                    $"  Output File: {outputFileName}\n" +
                    $"  Removed Folders: {removed.RemovedFolders}\n" +
                    $"  Removed Tags: {removed.RemovedTags}\n" +
                    $"  Total Items Removed: {removed.RemovedFolders + removed.RemovedTags}\n");
            }

            string timestamp = now.ToString("yyyy-MM-dd HH:mm:ss");
            string summaryText;
            if (files.Count == 1)
            {
                summaryText = "Ignition Tag Cleaner - Optimization Report\n" +
                              "===========================================\n" +
                              $"Date: {timestamp}\n\n" +
                              string.Join("\n", fileSummaries);
            }
            else
            {
                summaryText = "Ignition Tag Cleaner - Batch Optimization Report\n" +
                              "=================================================\n" +
                              $"Date: {timestamp}\n" +
                              $"Files Processed: {result.FilesProcessed}\n\n" +
                              "OVERALL BATCH SUMMARY:\n" +
                              "----------------------\n" +
                              $"- Total Folders Removed: {result.RemovedFolders}\n" +
                              $"- Total Tags Removed: {result.RemovedTags}\n" +
                              $"- Grand Total Items Removed: {result.RemovedFolders + result.RemovedTags}\n\n" +
                              "INDIVIDUAL FILE BREAKDOWN:\n" +
                              "--------------------------\n" +
                              string.Join("\n", fileSummaries);
            }

            result.SummaryFile = files.Count == 1
                ? $"{safePrefix}_Export_Summary_{dateStr}.txt"
                : $"Export_Summary_{dateStr}.txt";
            File.WriteAllText(Path.Combine(outputFolder, result.SummaryFile), summaryText);

            return result;
        }
        #endregion

        #region Helpers
        private static string RemoveInvalidFileNameChars(string name) =>
            string.Concat(name.Split(Path.GetInvalidFileNameChars())).Trim();
        #endregion
    }
}
