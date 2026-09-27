using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Linq;
using Frosty.Core;
using Frosty.Core.Attributes;
using Frosty.Core.Controls;
using FrostySdk.Managers.Entries;
using Host = Frosty.Core.App;

[assembly: AssemblyTitle("Cyberface Spreadsheet Importer")]
[assembly: AssemblyDescription("Spreadsheet-driven Madden 27 cyberface creation")]
[assembly: AssemblyVersion("1.0.2.0")]
[assembly: AssemblyFileVersion("1.0.2.0")]
[assembly: PluginDisplayName("Cyberface Spreadsheet Importer")]
[assembly: PluginAuthor("Custom MMC tools")]
[assembly: PluginVersion("1.0.2")]
[assembly: RegisterMenuExtension(typeof(CyberfaceSpreadsheetImporter.CyberfaceImporterMenu))]

namespace CyberfaceSpreadsheetImporter
{
    public sealed class CyberfaceImporterMenu : MenuExtension
    {
        public override string TopLevelMenuName { get { return "Tools"; } }
        public override string MenuItemName { get { return "Cyberface Spreadsheet Importer..."; } }
        public override RelayCommand MenuItemClicked
        {
            get { return new RelayCommand(delegate(object o) { Plugin.Open(); }); }
        }
    }

    public static class Plugin
    {
        public static void Open()
        {
            try
            {
                if (Host.AssetManager == null) throw new InvalidOperationException("Open Madden 27 and a project first.");
                CyberfaceImporterWindow w = new CyberfaceImporterWindow();
                w.Owner = Application.Current.MainWindow;
                w.ShowDialog();
            }
            catch (Exception ex)
            {
                if (Host.Logger != null) Host.Logger.LogError("Cyberface Spreadsheet Importer: " + ex);
                MessageBox.Show(ex.Message, "Cyberface Spreadsheet Importer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public sealed class ImportRow : INotifyPropertyChanged
    {
        private bool _include;
        private string _status;
        private string _newAsset;

        public int ExcelRow { get; set; }
        public string Name { get; set; }
        public string Position { get; set; }
        public string Dupe { get; set; }
        public string StrandbindHair { get; set; }
        public bool IsCompleted { get; set; }

        public bool Include
        {
            get { return _include; }
            set { _include = value; Changed("Include"); }
        }

        public string Status
        {
            get { return _status; }
            set { _status = value; Changed("Status"); }
        }

        public string NewAsset
        {
            get { return _newAsset; }
            set
            {
                _newAsset = value;
                Changed("NewAsset");
                Changed("TargetFolder");
                Changed("FinalPath");
            }
        }

        public string SourcePath
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Dupe)) return string.Empty;
                string d = Dupe.Trim().ToLowerInvariant();
                return "content/characters/player/players/" + d.Substring(0, 1) + "/" + d;
            }
        }

        public string TargetFolder
        {
            get
            {
                if (string.IsNullOrWhiteSpace(NewAsset)) return string.Empty;
                return "content/characters/player/players/" + NewAsset.Substring(0, 1).ToLowerInvariant();
            }
        }

        public string FinalPath
        {
            get { return string.IsNullOrWhiteSpace(NewAsset) ? string.Empty : TargetFolder + "/" + NewAsset.ToLowerInvariant(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string name)
        {
            PropertyChangedEventHandler h = PropertyChanged;
            if (h != null) h(this, new PropertyChangedEventArgs(name));
        }
    }

    public sealed class CyberfaceImporterWindow : Window
    {
        private readonly ObservableCollection<ImportRow> _rows = new ObservableCollection<ImportRow>();
        private ICollectionView _view;
        private DataGrid _grid;
        private TextBox _search;
        private TextBlock _fileText;
        private TextBlock _counts;
        private TextBlock _progressText;
        private ProgressBar _progress;
        private Button _run;
        private string _workbook;

        private static readonly Brush Bg = BrushFrom("#1b1f24");
        private static readonly Brush Panel = BrushFrom("#242a30");
        private static readonly Brush Input = BrushFrom("#171b20");
        private static readonly Brush Line = BrushFrom("#3c444d");
        private static readonly Brush Text = BrushFrom("#e9eef3");
        private static readonly Brush Muted = BrushFrom("#9aa6b2");
        private static readonly Brush Blue = BrushFrom("#2c6fa9");

        public CyberfaceImporterWindow()
        {
            Title = "Cyberface Spreadsheet Importer";
            Width = 1420;
            Height = 820;
            MinWidth = 1040;
            MinHeight = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = Bg;
            Foreground = Text;
            FontFamily = new FontFamily("Segoe UI");
            FontSize = 13;
            Content = BuildUi();

            _grid.ItemsSource = _rows;
            _view = CollectionViewSource.GetDefaultView(_rows);
            _view.Filter = Filter;
            UpdateCounts();
        }

        private UIElement BuildUi()
        {
            Grid root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            Border header = new Border { Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(0,0,0,1), Padding = new Thickness(16) };
            DockPanel hd = new DockPanel();
            Button import = Button("Import Spreadsheet...", true, 160);
            import.Click += Import_Click;
            DockPanel.SetDock(import, Dock.Left);
            hd.Children.Add(import);
            _fileText = new TextBlock { Text = "Creation Info sheet", Foreground = Muted, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(14,0,0,0) };
            hd.Children.Add(_fileText);
            header.Child = hd;
            root.Children.Add(header);

            Border toolbar = new Border { Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(0,0,0,1), Padding = new Thickness(16,9,16,9) };
            DockPanel td = new DockPanel();
            _counts = new TextBlock { Foreground = Muted, VerticalAlignment = VerticalAlignment.Center };
            DockPanel.SetDock(_counts, Dock.Right);
            td.Children.Add(_counts);
            _search = new TextBox { Width = 320, Height = 29, Background = Input, Foreground = Text, BorderBrush = Line, Padding = new Thickness(7,4,7,4), Text = "" };
            _search.TextChanged += delegate { if (_view != null) _view.Refresh(); };
            td.Children.Add(_search);
            toolbar.Child = td;
            Grid.SetRow(toolbar, 1);
            root.Children.Add(toolbar);

            _grid = new DataGrid
            {
                AutoGenerateColumns = false,
                Background = Bg,
                Foreground = Text,
                BorderBrush = Line,
                BorderThickness = new Thickness(0),
                GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
                HorizontalGridLinesBrush = Line,
                RowBackground = Bg,
                AlternatingRowBackground = BrushFrom("#20252b"),
                CanUserAddRows = false,
                CanUserDeleteRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                SelectionUnit = DataGridSelectionUnit.FullRow,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                Margin = new Thickness(0)
            };

            _grid.Columns.Add(new DataGridCheckBoxColumn { Header = "Run", Binding = new Binding("Include"), Width = 52 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Status", Binding = new Binding("Status"), Width = 130, IsReadOnly = true });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Pos", Binding = new Binding("Position"), Width = 65, IsReadOnly = true });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name"), Width = 190, IsReadOnly = true });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Dupe source", Binding = new Binding("Dupe"), Width = 190, IsReadOnly = true });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Generated asset name", Binding = new Binding("NewAsset") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }, Width = 220 });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Destination folder", Binding = new Binding("TargetFolder"), Width = 275, IsReadOnly = true });
            _grid.Columns.Add(new DataGridTextColumn { Header = "Strandbind_hair", Binding = new Binding("StrandbindHair"), Width = 190, IsReadOnly = true });
            Grid.SetRow(_grid, 2);
            root.Children.Add(_grid);

            Border footer = new Border { Background = Panel, BorderBrush = Line, BorderThickness = new Thickness(0,1,0,0), Padding = new Thickness(14,10,14,10) };
            Grid fg = new Grid();
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            fg.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StackPanel left = new StackPanel();
            _progressText = new TextBlock { Foreground = Muted };
            _progress = new ProgressBar { Height = 5, Minimum = 0, Maximum = 1, Value = 0, Visibility = Visibility.Collapsed, Margin = new Thickness(0,6,20,0) };
            left.Children.Add(_progressText);
            left.Children.Add(_progress);
            fg.Children.Add(left);

            Button regen = Button("Regenerate IDs", false, 120);
            regen.Click += Regenerate_Click;
            Grid.SetColumn(regen, 1);
            fg.Children.Add(regen);

            _run = Button("Run Ready Jobs", true, 145);
            _run.Margin = new Thickness(10,0,0,0);
            _run.Click += Run_Click;
            Grid.SetColumn(_run, 2);
            fg.Children.Add(_run);

            footer.Child = fg;
            Grid.SetRow(footer, 3);
            root.Children.Add(footer);
            return root;
        }

        private void Import_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Filter = "Excel workbooks (*.xlsx)|*.xlsx";
            if (dlg.ShowDialog(this) != true) return;

            try
            {
                List<ImportRow> loaded = WorkbookReader.Read(dlg.FileName);
                _rows.Clear();
                HashSet<string> used = ExistingAssetNames();
                foreach (ImportRow r in loaded)
                {
                    r.NewAsset = NameGenerator.Generate(r.Name, used);
                    r.Include = !r.IsCompleted && !string.IsNullOrWhiteSpace(r.Dupe);
                    r.Status = r.IsCompleted ? "Done / skipped" : (string.IsNullOrWhiteSpace(r.Dupe) ? "Missing Dupe" : "Ready");
                    _rows.Add(r);
                }
                _workbook = dlg.FileName;
                _fileText.Text = Path.GetFileName(dlg.FileName) + "   |   SHEET: Creation Info";
                if (_view != null) _view.Refresh();
                UpdateCounts();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Spreadsheet import failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Regenerate_Click(object sender, RoutedEventArgs e)
        {
            HashSet<string> used = ExistingAssetNames();
            foreach (ImportRow r in _rows)
            {
                if (!r.IsCompleted) r.NewAsset = NameGenerator.Generate(r.Name, used);
            }
        }

        private async void Run_Click(object sender, RoutedEventArgs e)
        {
            List<ImportRow> jobs = _rows.Where(x => x.Include && !x.IsCompleted && !string.IsNullOrWhiteSpace(x.Dupe)).ToList();
            if (jobs.Count == 0)
            {
                MessageBox.Show(this, "There are no ready jobs selected.", "Cyberface Spreadsheet Importer", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(this, "Run " + jobs.Count.ToString(CultureInfo.InvariantCulture) + " cyberface job(s)?", "Cyberface Spreadsheet Importer", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            _run.IsEnabled = false;
            _progress.Visibility = Visibility.Visible;
            _progress.Maximum = jobs.Count;
            _progress.Value = 0;

            int ok = 0;
            try
            {
                OfficialFaceDuplicator face = new OfficialFaceDuplicator();
                StrandbindHairDuplicator hair = new StrandbindHairDuplicator();

                for (int i = 0; i < jobs.Count; i++)
                {
                    ImportRow row = jobs[i];
                    _progressText.Text = (i + 1).ToString(CultureInfo.InvariantCulture) + "/" + jobs.Count.ToString(CultureInfo.InvariantCulture) + "  " + row.Name;

                    try
                    {
                        string err = face.Validate(row);
                        if (err != null) throw new InvalidOperationException(err);
                        string hairErr = hair.Validate(row.StrandbindHair);
                        if (hairErr != null) throw new InvalidOperationException(hairErr);

                        row.Status = "Duplicating face...";
                        await face.DuplicateAsync(row);

                        if (!string.IsNullOrWhiteSpace(row.StrandbindHair))
                        {
                            row.Status = "Applying hair...";
                            hair.Apply(row);
                        }

                        row.Status = "Completed";
                        row.Include = false;
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        row.Status = "Failed: " + ex.Message;
                        if (Host.Logger != null) Host.Logger.LogError("Cyberface spreadsheet row " + row.ExcelRow + " failed: " + ex);
                    }

                    _progress.Value = i + 1;
                    UpdateCounts();
                    await Task.Delay(40);
                }
            }
            finally
            {
                _run.IsEnabled = true;
                _progressText.Text = "Finished: " + ok.ToString(CultureInfo.InvariantCulture) + "/" + jobs.Count.ToString(CultureInfo.InvariantCulture) + " completed.";
            }
        }

        private bool Filter(object item)
        {
            ImportRow r = item as ImportRow;
            if (r == null) return false;
            string q = (_search == null ? "" : _search.Text).Trim();
            if (q.Length == 0) return true;
            return Contains(r.Name, q) || Contains(r.Dupe, q) || Contains(r.NewAsset, q) || Contains(r.StrandbindHair, q) || Contains(r.Status, q);
        }

        private static bool Contains(string s, string q)
        {
            return (s ?? "").IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void UpdateCounts()
        {
            int done = _rows.Count(x => x.IsCompleted || string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            int ready = _rows.Count(x => !x.IsCompleted && !string.IsNullOrWhiteSpace(x.Dupe) && !string.Equals(x.Status, "Completed", StringComparison.OrdinalIgnoreCase));
            int missing = _rows.Count(x => !x.IsCompleted && string.IsNullOrWhiteSpace(x.Dupe));
            if (_counts != null) _counts.Text = "Rows " + _rows.Count + "   |   Done " + done + "   |   Ready " + ready + "   |   Missing Dupe " + missing;
        }

        private static HashSet<string> ExistingAssetNames()
        {
            HashSet<string> used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (EbxAssetEntry e in Host.AssetManager.EnumerateEbx("", false, false, true, ""))
            {
                string n = Normalize(e.Name);
                if (!n.StartsWith("content/characters/player/players/", StringComparison.OrdinalIgnoreCase)) continue;
                string[] p = n.Split('/');
                if (p.Length >= 6) used.Add(p[5]);
            }
            return used;
        }

        private static Button Button(string text, bool primary, double width)
        {
            Button b = new Button { Content = text, Width = width, Height = 30, Padding = new Thickness(10,4,10,4), Foreground = Text, Background = primary ? Blue : Input, BorderBrush = Line, BorderThickness = new Thickness(1) };
            return b;
        }

        private static Brush BrushFrom(string s)
        {
            return (Brush)new BrushConverter().ConvertFromString(s);
        }

        private static string Normalize(string s)
        {
            return (s ?? "").Replace("\\", "/").Trim().ToLowerInvariant();
        }
    }

    public static class NameGenerator
    {
        public static string Generate(string playerName, ISet<string> used)
        {
            string stem = BuildStem(playerName);
            for (int i = 0; i < 30000; i++)
            {
                int digits = RandomInt(4, 7);
                int min = digits == 4 ? 1000 : (digits == 5 ? 10000 : 100000);
                int max = digits == 4 ? 10000 : (digits == 5 ? 100000 : 1000000);
                string c = stem + "_" + RandomInt(min, max).ToString(CultureInfo.InvariantCulture);
                if (used.Add(c)) return c;
            }
            throw new InvalidOperationException("Could not generate a unique asset ID.");
        }

        public static string BuildStem(string playerName)
        {
            string cleaned = Regex.Replace(playerName ?? "", "[^A-Za-z0-9'’ -]", " ");
            cleaned = cleaned.Replace("'", "").Replace("’", "");
            List<string> parts = cleaned.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 0) return "player";
            string lastToken = parts[parts.Count - 1].ToLowerInvariant().TrimEnd('.');
            if (lastToken == "jr" || lastToken == "sr" || lastToken == "ii" || lastToken == "iii" || lastToken == "iv") parts.RemoveAt(parts.Count - 1);
            if (parts.Count == 1) return Regex.Replace(parts[0].ToLowerInvariant(), "[^a-z0-9]", "");
            string first = parts[0];
            string last = parts[parts.Count - 1];
            return Regex.Replace((last + first).ToLowerInvariant(), "[^a-z0-9]", "");
        }

        private static int RandomInt(int min, int max)
        {
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                byte[] b = new byte[4];
                rng.GetBytes(b);
                uint n = BitConverter.ToUInt32(b, 0);
                return min + (int)(n % (uint)(max - min));
            }
        }
    }

    public static class WorkbookReader
    {
        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly HashSet<string> Green = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FF93C47D", "FF6AA84F", "93C47D", "6AA84F" };

        public static List<ImportRow> Read(string path)
        {
            using (ZipArchive zip = ZipFile.OpenRead(path))
            {
                string sheetPath = ResolveSheetPath(zip, "Creation Info");
                List<string> shared = ReadSharedStrings(zip);
                Dictionary<int, string> styleFills = ReadStyleFills(zip);
                XDocument sheet = Load(zip, sheetPath);
                List<ImportRow> rows = new List<ImportRow>();

                foreach (XElement xr in sheet.Descendants(MainNs + "row"))
                {
                    int rn = (int?)xr.Attribute("r") ?? 0;
                    if (rn <= 1) continue;

                    Dictionary<string, XElement> cells = xr.Elements(MainNs + "c").ToDictionary(c => Column((string)c.Attribute("r")), c => c, StringComparer.OrdinalIgnoreCase);
                    string name = Cell(cells, "C", shared);
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    ImportRow row = new ImportRow();
                    row.ExcelRow = rn;
                    row.Position = Cell(cells, "B", shared).Trim();
                    row.Name = name.Trim();
                    row.Dupe = Cell(cells, "D", shared).Trim();
                    row.StrandbindHair = Cell(cells, "F", shared).Trim();
                    row.IsCompleted = IsGreen(cells.ContainsKey("A") ? cells["A"] : null, styleFills);
                    rows.Add(row);
                }
                return rows;
            }
        }

        private static bool IsGreen(XElement cell, Dictionary<int,string> styleFills)
        {
            if (cell == null) return false;
            int s = (int?)cell.Attribute("s") ?? 0;
            string rgb;
            return styleFills.TryGetValue(s, out rgb) && Green.Contains(rgb ?? "");
        }

        private static Dictionary<int,string> ReadStyleFills(ZipArchive zip)
        {
            Dictionary<int,string> result = new Dictionary<int,string>();
            ZipArchiveEntry e = zip.GetEntry("xl/styles.xml");
            if (e == null) return result;
            XDocument d;
            using (Stream s = e.Open()) d = XDocument.Load(s);

            List<string> fills = new List<string>();
            foreach (XElement fill in d.Descendants(MainNs + "fills").Elements(MainNs + "fill"))
            {
                XElement fg = fill.Descendants(MainNs + "fgColor").FirstOrDefault();
                fills.Add(fg == null ? "" : ((string)fg.Attribute("rgb") ?? ""));
            }

            List<XElement> xfs = d.Descendants(MainNs + "cellXfs").Elements(MainNs + "xf").ToList();
            for (int i = 0; i < xfs.Count; i++)
            {
                int id = (int?)xfs[i].Attribute("fillId") ?? 0;
                result[i] = id >= 0 && id < fills.Count ? fills[id] : "";
            }
            return result;
        }

        private static string Cell(Dictionary<string,XElement> cells, string col, List<string> shared)
        {
            XElement c;
            if (!cells.TryGetValue(col, out c)) return "";
            string t = (string)c.Attribute("t") ?? "";
            if (t == "inlineStr") return string.Concat(c.Descendants(MainNs + "t").Select(x => (string)x));
            string raw = (string)c.Element(MainNs + "v") ?? "";
            if (t == "s")
            {
                int idx;
                if (int.TryParse(raw, out idx) && idx >= 0 && idx < shared.Count) return shared[idx];
            }
            return raw;
        }

        private static string Column(string reference)
        {
            return string.IsNullOrEmpty(reference) ? "" : new string(reference.TakeWhile(char.IsLetter).ToArray());
        }

        private static string ResolveSheetPath(ZipArchive zip, string wanted)
        {
            XDocument wb = Load(zip, "xl/workbook.xml");
            XElement sh = wb.Descendants(MainNs + "sheet").FirstOrDefault(x => string.Equals((string)x.Attribute("name"), wanted, StringComparison.OrdinalIgnoreCase));
            if (sh == null) throw new InvalidDataException("The workbook does not contain a 'Creation Info' sheet.");
            string id = (string)sh.Attribute(RelNs + "id");
            XDocument rels = Load(zip, "xl/_rels/workbook.xml.rels");
            XElement rel = rels.Descendants(PackageRelNs + "Relationship").FirstOrDefault(x => string.Equals((string)x.Attribute("Id"), id, StringComparison.Ordinal));
            if (rel == null) throw new InvalidDataException("Could not resolve Creation Info.");
            string target = (string)rel.Attribute("Target");
            return target.StartsWith("/") ? target.TrimStart('/') : "xl/" + target.Replace("\\", "/");
        }

        private static List<string> ReadSharedStrings(ZipArchive zip)
        {
            ZipArchiveEntry e = zip.GetEntry("xl/sharedStrings.xml");
            if (e == null) return new List<string>();
            XDocument d;
            using (Stream s = e.Open()) d = XDocument.Load(s);
            return d.Descendants(MainNs + "si").Select(si => string.Concat(si.Descendants(MainNs + "t").Select(t => (string)t))).ToList();
        }

        private static XDocument Load(ZipArchive zip, string p)
        {
            ZipArchiveEntry e = zip.GetEntry(p);
            if (e == null) throw new InvalidDataException("Missing XLSX part: " + p);
            using (Stream s = e.Open()) return XDocument.Load(s);
        }
    }

    public sealed class OfficialFaceDuplicator
    {
        private readonly object _extension;
        private readonly PropertyInfo _command;

        public OfficialFaceDuplicator()
        {
            Assembly a = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => string.Equals(x.GetName().Name, "DuplicationPlugin", StringComparison.OrdinalIgnoreCase));
            if (a == null) throw new InvalidOperationException("The official DuplicationPlugin.dll is not loaded.");
            Type t = a.GetType("DuplicationPlugin.DuplicationTool+DuplicatePlayerFaceMenuExtension", false);
            if (t == null) throw new InvalidOperationException("The loaded DuplicationPlugin does not expose Duplicate Player Face.");
            _extension = Activator.CreateInstance(t, true);
            _command = t.GetProperty("ContextItemClicked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (_command == null) throw new InvalidOperationException("Could not access Duplicate Player Face.");
        }

        public string Validate(ImportRow row)
        {
            if (FindSource(row.SourcePath) == null) return "No face assets found under " + row.SourcePath;
            string prefix = row.FinalPath.TrimEnd('/') + "/";
            if (Host.AssetManager.EnumerateEbx("", false, false, true, "").Any(x => Normalize(x.Name).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return "Target already exists: " + row.FinalPath;
            return null;
        }

        public async Task DuplicateAsync(ImportRow row)
        {
            FrostyDataExplorer explorer = Host.EditorWindow == null ? null : Host.EditorWindow.VisibleExplorer;
            if (explorer == null) throw new InvalidOperationException("Data Explorer is unavailable.");

            EbxAssetEntry source = FindSource(row.SourcePath);
            if (source == null) throw new InvalidOperationException("Could not select a source asset.");
            await explorer.SelectAsset(source, false);
            explorer.SelectedPath = row.SourcePath;

            ICommand cmd = _command.GetValue(_extension, null) as ICommand;
            if (cmd == null) throw new InvalidOperationException("Official face command could not be executed.");

            using (WindowDriver driver = new WindowDriver(row.TargetFolder, row.NewAsset))
            {
                driver.Start();
                cmd.Execute(null);
                await driver.WaitAsync(10000);
            }

            int elapsed = 0;
            string prefix = row.FinalPath.TrimEnd('/') + "/";
            while (elapsed < 60000)
            {
                if (Host.AssetManager.EnumerateEbx("", false, false, true, "").Any(x => Normalize(x.Name).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))) return;
                await Task.Delay(100);
                elapsed += 100;
            }
            throw new TimeoutException("The official duplicator did not create assets under " + row.FinalPath + ".");
        }

        private EbxAssetEntry FindSource(string folder)
        {
            string prefix = Normalize(folder).TrimEnd('/') + "/";
            List<EbxAssetEntry> list = Host.AssetManager.EnumerateEbx("", false, false, true, "").Where(x => Normalize(x.Name).StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
            EbxAssetEntry e = list.FirstOrDefault(x => string.Equals(x.Type, "ObjectBlueprint", StringComparison.OrdinalIgnoreCase));
            return e ?? list.FirstOrDefault();
        }

        private static string Normalize(string s) { return (s ?? "").Replace("\\", "/").Trim().ToLowerInvariant(); }

        private sealed class WindowDriver : IDisposable
        {
            private readonly string _path;
            private readonly string _name;
            private DispatcherTimer _timer;
            private TaskCompletionSource<bool> _done = new TaskCompletionSource<bool>();

            public WindowDriver(string path, string name) { _path = path; _name = name; }

            public void Start()
            {
                _timer = new DispatcherTimer(DispatcherPriority.Send);
                _timer.Interval = TimeSpan.FromMilliseconds(25);
                _timer.Tick += Tick;
                _timer.Start();
            }

            private void Tick(object sender, EventArgs e)
            {
                foreach (Window w in Application.Current.Windows)
                {
                    Type t = w.GetType();
                    if (!string.Equals(t.FullName, "DuplicationPlugin.Windows.DuplicateFaceWindow", StringComparison.Ordinal)) continue;
                    PropertyInfo p = t.GetProperty("SelectedPath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    PropertyInfo n = t.GetProperty("SelectedName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (p == null || n == null) { _done.TrySetException(new InvalidOperationException("Official Duplicate Face window fields changed.")); Stop(); return; }
                    p.SetValue(w, _path, null);
                    n.SetValue(w, _name, null);
                    try { w.DialogResult = true; }
                    catch (InvalidOperationException) { return; }
                    _done.TrySetResult(true);
                    Stop();
                    return;
                }
            }

            public async Task WaitAsync(int timeout)
            {
                Task t = Task.Delay(timeout);
                Task winner = await Task.WhenAny(_done.Task, t);
                if (winner == t) throw new TimeoutException("The official Duplicate Face window did not open.");
                await _done.Task;
            }

            private void Stop()
            {
                if (_timer != null) { _timer.Stop(); _timer.Tick -= Tick; _timer = null; }
            }

            public void Dispose() { Stop(); }
        }
    }

    public sealed class StrandbindHairDuplicator
    {
        private readonly MethodInfo _duplicate;

        public StrandbindHairDuplicator()
        {
            Assembly a = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => string.Equals(x.GetName().Name, "DuplicationPlugin", StringComparison.OrdinalIgnoreCase));
            Type t = a == null ? null : a.GetType("DuplicationPlugin.DuplicationTool", false);
            if (t == null) throw new InvalidOperationException("The official DuplicationPlugin.dll is not loaded.");

            _duplicate = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                .Where(m => m.Name == "DuplicateAsset")
                .Where(m =>
                {
                    ParameterInfo[] p = m.GetParameters();
                    return p.Length >= 2 && p.Length <= 5 && typeof(EbxAssetEntry).IsAssignableFrom(p[0].ParameterType) && p[1].ParameterType == typeof(string);
                })
                .OrderBy(m => m.GetParameters().Length)
                .FirstOrDefault();

            if (_duplicate == null) throw new InvalidOperationException("Official DuplicateAsset backend was not found.");
        }

        public string Validate(string donorText)
        {
            if (string.IsNullOrWhiteSpace(donorText)) return null;
            Donor d;
            string error;
            if (!ResolveDonor(donorText, out d, out error)) return error;
            if (HairAssets(d).Count == 0) return "No non-HelmetOn strandbind_hair assets found for " + donorText;
            return null;
        }

        public int Apply(ImportRow row)
        {
            if (string.IsNullOrWhiteSpace(row.StrandbindHair)) return 0;
            Donor donor;
            string error;
            if (!ResolveDonor(row.StrandbindHair, out donor, out error)) throw new InvalidOperationException(error);

            List<EbxAssetEntry> assets = HairAssets(donor);
            if (assets.Count == 0) throw new InvalidOperationException("No non-HelmetOn strandbind_hair assets found for " + row.StrandbindHair);

            int count = 0;
            foreach (EbxAssetEntry source in assets.OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase))
            {
                string target = ReplaceIgnoreCase(Normalize(source.Name), donor.Folder, row.FinalPath);
                target = ReplaceIgnoreCase(target, donor.AssetBase, row.NewAsset.ToLowerInvariant()).Trim('/');
                if (Host.AssetManager.GetEbxEntry(target) != null) continue;

                object[] args = BuildArgs(source, target);
                object result;
                try { result = _duplicate.Invoke(null, args); }
                catch (TargetInvocationException ex) { throw new InvalidOperationException(ex.InnerException == null ? ex.Message : ex.InnerException.Message, ex.InnerException ?? ex); }

                if (result is bool && !(bool)result) throw new InvalidOperationException("DuplicateAsset reported failure for " + source.Name);
                if (Host.AssetManager.GetEbxEntry(target) == null) throw new InvalidOperationException("Hair asset was not created: " + target);
                count++;
            }
            return count;
        }

        private object[] BuildArgs(EbxAssetEntry source, string target)
        {
            ParameterInfo[] p = _duplicate.GetParameters();
            object[] a = new object[p.Length];
            a[0] = source; a[1] = target;
            for (int i = 2; i < p.Length; i++)
            {
                Type t = p[i].ParameterType;
                if (t == typeof(Type)) a[i] = null;
                else if (t == typeof(bool)) a[i] = false;
                else if (p[i].HasDefaultValue) a[i] = p[i].DefaultValue;
                else a[i] = t.IsValueType ? Activator.CreateInstance(t) : null;
            }
            return a;
        }

        private bool ResolveDonor(string text, out Donor donor, out string error)
        {
            donor = null; error = null;
            string root = "content/characters/player/players/";
            Dictionary<string,Donor> folders = new Dictionary<string,Donor>(StringComparer.OrdinalIgnoreCase);
            foreach (EbxAssetEntry e in Host.AssetManager.EnumerateEbx("", false, false, true, ""))
            {
                string n = Normalize(e.Name);
                if (!n.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                string[] p = n.Substring(root.Length).Split('/');
                if (p.Length < 3) continue;
                string b = p[1];
                if (!folders.ContainsKey(b)) folders[b] = new Donor { AssetBase = b, Folder = root + p[0] + "/" + b };
            }

            string direct = Normalize(text);
            if (folders.TryGetValue(direct, out donor)) return true;

            string stem = NameGenerator.BuildStem(text);
            List<Donor> exact = folders.Values.Where(x => x.AssetBase.StartsWith(stem + "_", StringComparison.OrdinalIgnoreCase)).ToList();
            if (exact.Count > 0)
            {
                donor = exact.OrderByDescending(x => HairAssets(x).Count).First();
                return true;
            }

            error = "Could not resolve Strandbind_hair donor '" + text + "'.";
            return false;
        }

        private List<EbxAssetEntry> HairAssets(Donor d)
        {
            string prefix = d.Folder.TrimEnd('/') + "/";
            string wanted = d.AssetBase.ToLowerInvariant() + "_strandbind_hair";
            return Host.AssetManager.EnumerateEbx("", false, false, true, "")
                .Where(x => Normalize(x.Name).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Where(x =>
                {
                    string n = Normalize(x.Name);
                    int slash = n.LastIndexOf('/');
                    string f = slash < 0 ? n : n.Substring(slash + 1);
                    return f.StartsWith(wanted, StringComparison.OrdinalIgnoreCase) && n.IndexOf("helmeton", StringComparison.OrdinalIgnoreCase) < 0;
                }).ToList();
        }

        private static string ReplaceIgnoreCase(string input, string oldValue, string newValue)
        {
            int i = input.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
            return i < 0 ? input : input.Substring(0, i) + newValue + input.Substring(i + oldValue.Length);
        }

        private static string Normalize(string s) { return (s ?? "").Replace("\\", "/").Trim().ToLowerInvariant(); }

        private sealed class Donor
        {
            public string AssetBase;
            public string Folder;
        }
    }
}
