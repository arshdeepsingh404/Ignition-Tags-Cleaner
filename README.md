# Ignition Tag Cleaner

A Windows desktop tool that prunes Ignition tag export files. Point it at a tag export `.json`, tell it which folders
and tags you want in an Excel workbook, and it writes a smaller export containing only those.

Raw PLC and tag-provider exports often carry thousands of internal, diagnostic or unused tags. Importing all of them
slows the Designer, uses gateway memory and clutters the tag browser. This tool removes them before import.

> This is an independent, unofficial tool. It is not affiliated with or endorsed by Inductive Automation.
> Ignition is a trademark of Inductive Automation.

## Features

- Keep whole folders, or keep only named tags inside chosen folders; everything else is removed.
- Rules live in an Excel workbook (`config.xlsx`), which can stay open in Excel while the app runs.
- Preview the original and the optimized tag tree before exporting, with folder and tag search.
- Process one file, several files, or a dropped folder (searched recursively).
- Live statistics: tags and folders removed, tags remaining, reduction ratio.
- Tag attributes that are kept are written back unchanged.

## Requirements

- Windows 10 or 11
- [.NET 10 SDK](https://dotnet.microsoft.com/download) to build from source

Microsoft Excel is only needed to edit the rules, not to run the app.

## Build and run

```powershell
dotnet build "Ignition Tags Cleaner.slnx"
dotnet run --project "Ignition Tags Cleaner/Ignition Tags Cleaner.csproj"
```

To produce a release build:

```powershell
dotnet publish "Ignition Tags Cleaner/Ignition Tags Cleaner.csproj" -c Release
```

## Usage

1. Export tags from the Ignition Designer as JSON.
2. Click **Configure Rules** to open `config.xlsx` and list the folders and tags to keep. Save the workbook; the app
   reloads it when its window regains focus.
3. Drop the export file(s) or a folder onto the app, or click **Select File(s)**.
4. Check the statistics, and use **Preview All Tags** / **Preview Optimized Tags** to inspect the result.
5. Click **Generate Export** and choose a destination. The app creates a dated folder containing one
   `<PLC>_Tags_Optimized_<date>.json` per input file and a text summary report.
6. Import the optimized JSON into Ignition.

To try it out, use `Sample/Sample-PLC-Tags.json` with the shipped `config.xlsx`: 9 of 17 tags and 3 of 6 folders
are removed.

## Rules (`config.xlsx`)

The workbook has three sheets. On each one, put the folder or tag name in column **B**; column **A** is a free-text
grouping for your own use and is ignored.

| Sheet | Effect |
|---|---|
| **Folders To Keep** | The folder and everything inside it is kept. |
| **Folders To Filter** | The folder is kept, but only tags listed on **Tags To Keep** survive inside it (including in its subfolders). The folder is removed if nothing is left. |
| **Tags To Keep** | Tag names allowed inside filtered folders. |

How matching works:

- Names are matched exactly, ignoring case, at any depth in the tree. There are no wildcards.
- Folders not listed on either folder sheet are kept only if something inside them is kept.
- Tags outside a kept or filtered folder are always removed.
- To disable a row without deleting it, add `FALSE` or `0` in column C.

## Contributing

Issues and pull requests are welcome. Please do not include real plant tag exports or site names in issues, samples
or tests; use synthetic data like the file in `Sample/`.

## License

Released under the [MIT License](LICENSE).
