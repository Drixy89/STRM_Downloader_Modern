using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace STRM_Downloader_Modern;

public sealed class MainForm : Form
{
    private sealed class MediaItem
    {
        public string Name { get; init; } = "";
        public string Path { get; init; } = "";
        public string Url { get; init; } = "";
        public string Type { get; init; } = "";
        public string Series { get; init; } = "";
        public string Season { get; init; } = "";
    }

    private readonly TextBox txtNas = new();
    private readonly TextBox txtSearch = new();
    private readonly Button btnScan = new();
    private readonly Button btnDownload = new();
    private readonly Button btnCancel = new();
    private readonly ListView list = new();
    private readonly Label lblInfo = new();
    private readonly Label lblDetails = new();
    private readonly Label lblProgress = new();
    private readonly Label lblEta = new();
    private readonly ProgressBar progress = new();

    private readonly List<MediaItem> items = new();
    private CancellationTokenSource? downloadCts;
    private MediaItem? selected;

    public MainForm()
    {
        Text = "STRM Downloader";
        Width = 1200;
        Height = 760;
        MinimumSize = new Size(900, 600);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(24, 24, 28);
        ForeColor = Color.White;

        BuildUi();
    }

    private void BuildUi()
    {
        var top = new Panel { Dock = DockStyle.Top, Height = 110, Padding = new Padding(18) };
        Controls.Add(top);

        var title = new Label
        {
            Text = "STRM Downloader",
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(18, 12)
        };
        top.Controls.Add(title);

        var lblNas = new Label
        {
            Text = "Dossier STRM / NAS :",
            AutoSize = true,
            Location = new Point(20, 58)
        };
        top.Controls.Add(lblNas);

        txtNas.Location = new Point(160, 54);
        txtNas.Width = 620;
        txtNas.Text = @"\\NAS\Video\STRM";
        txtNas.BackColor = Color.FromArgb(38, 38, 44);
        txtNas.ForeColor = Color.White;
        txtNas.BorderStyle = BorderStyle.FixedSingle;
        top.Controls.Add(txtNas);

        btnScan.Text = "Scanner";
        btnScan.Location = new Point(795, 51);
        btnScan.Size = new Size(110, 30);
        btnScan.Click += async (_, _) => await ScanAsync();
        top.Controls.Add(btnScan);

        txtSearch.PlaceholderText = "Rechercher...";
        txtSearch.Location = new Point(920, 51);
        txtSearch.Width = 240;
        txtSearch.BackColor = Color.FromArgb(38, 38, 44);
        txtSearch.ForeColor = Color.White;
        txtSearch.TextChanged += (_, _) => RefreshList();
        top.Controls.Add(txtSearch);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            SplitterDistance = 700,
            BackColor = Color.FromArgb(24, 24, 28)
        };
        Controls.Add(split);

        list.Dock = DockStyle.Fill;
        list.View = View.Details;
        list.FullRowSelect = true;
        list.GridLines = true;
        list.MultiSelect = false;
        list.BackColor = Color.FromArgb(30, 30, 35);
        list.ForeColor = Color.White;
        list.Columns.Add("Nom", 360);
        list.Columns.Add("Type", 90);
        list.Columns.Add("Saison", 100);
        list.Columns.Add("Source", 450);
        list.SelectedIndexChanged += (_, _) => SelectItem();
        split.Panel1.Padding = new Padding(18);
        split.Panel1.Controls.Add(list);

        var right = new Panel { Dock = DockStyle.Fill, Padding = new Padding(20) };
        split.Panel2.Controls.Add(right);

        var detailsTitle = new Label
        {
            Text = "Détails",
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 20)
        };
        right.Controls.Add(detailsTitle);

        lblDetails.Location = new Point(20, 65);
        lblDetails.Size = new Size(420, 190);
        lblDetails.AutoEllipsis = true;
        right.Controls.Add(lblDetails);

        btnDownload.Text = "Télécharger en MKV";
        btnDownload.Size = new Size(220, 42);
        btnDownload.Location = new Point(20, 275);
        btnDownload.Enabled = false;
        btnDownload.Click += async (_, _) => await DownloadAsync();
        right.Controls.Add(btnDownload);

        btnCancel.Text = "Annuler";
        btnCancel.Size = new Size(100, 42);
        btnCancel.Location = new Point(250, 275);
        btnCancel.Enabled = false;
        btnCancel.Click += (_, _) => downloadCts?.Cancel();
        right.Controls.Add(btnCancel);

        progress.Location = new Point(20, 340);
        progress.Size = new Size(420, 24);
        right.Controls.Add(progress);

        lblProgress.Location = new Point(20, 375);
        lblProgress.Size = new Size(420, 30);
        right.Controls.Add(lblProgress);

        lblEta.Location = new Point(20, 410);
        lblEta.Size = new Size(420, 30);
        right.Controls.Add(lblEta);

        lblInfo.Text = "Prêt.";
        lblInfo.Dock = DockStyle.Bottom;
        lblInfo.Height = 34;
        lblInfo.Padding = new Padding(18, 8, 0, 0);
        lblInfo.BackColor = Color.FromArgb(32, 32, 38);
        Controls.Add(lblInfo);

        // Keep bottom status bar visually on top of the Fill control.
        lblInfo.BringToFront();
    }

    private async Task ScanAsync()
    {
        var folder = txtNas.Text.Trim();
        if (!Directory.Exists(folder))
        {
            MessageBox.Show(this, "Le dossier n'existe pas ou le NAS n'est pas accessible.",
                "STRM Downloader", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        btnScan.Enabled = false;
        items.Clear();
        list.Items.Clear();
        selected = null;
        btnDownload.Enabled = false;
        lblDetails.Text = "";
        lblInfo.Text = "Scan en cours...";

        try
        {
            await Task.Run(() =>
            {
                foreach (var file in Directory.EnumerateFiles(folder, "*.strm", SearchOption.AllDirectories))
                {
                    try
                    {
                        var url = File.ReadLines(file)
                            .Select(x => x.Trim())
                            .FirstOrDefault(x => x.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                                              || x.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

                        if (string.IsNullOrWhiteSpace(url))
                            continue;

                        var name = Path.GetFileNameWithoutExtension(file);
                        var relative = Path.GetRelativePath(folder, file);
                        var isSeries = Regex.IsMatch(name, @"(?i)S\d{1,2}[\s._-]*E\d{1,2}")
                                     || relative.Contains($"{Path.DirectorySeparatorChar}Series{Path.DirectorySeparatorChar}",
                                         StringComparison.OrdinalIgnoreCase);

                        string season = "";
                        var match = Regex.Match(name, @"(?i)S(\d{1,2})[\s._-]*E\d{1,2}");
                        if (match.Success)
                            season = $"Saison {int.Parse(match.Groups[1].Value):00}";

                        var item = new MediaItem
                        {
                            Name = name,
                            Path = file,
                            Url = url,
                            Type = isSeries ? "Série" : "Film",
                            Season = season,
                            Series = isSeries ? Path.GetFileName(Path.GetDirectoryName(file) ?? "") : ""
                        };

                        lock (items)
                            items.Add(item);
                    }
                    catch
                    {
                        // Ignore one unreadable .strm and continue scanning.
                    }
                }
            });

            items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
            RefreshList();
            lblInfo.Text = $"Scan terminé : {items.Count:N0} éléments.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Erreur de scan",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            lblInfo.Text = "Erreur pendant le scan.";
        }
        finally
        {
            btnScan.Enabled = true;
        }
    }

    private void RefreshList()
    {
        list.BeginUpdate();
        try
        {
            list.Items.Clear();
            var search = txtSearch.Text.Trim();

            foreach (var item in items)
            {
                if (!string.IsNullOrEmpty(search) &&
                    !item.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase))
                    continue;

                var row = new ListViewItem(item.Name);
                row.SubItems.Add(item.Type);
                row.SubItems.Add(item.Season);
                row.SubItems.Add(item.Path);
                row.Tag = item;
                list.Items.Add(row);

                if (list.Items.Count >= 2000)
                    break;
            }
        }
        finally
        {
            list.EndUpdate();
        }
    }

    private void SelectItem()
    {
        if (list.SelectedItems.Count == 0)
        {
            selected = null;
            btnDownload.Enabled = false;
            return;
        }

        selected = list.SelectedItems[0].Tag as MediaItem;
        if (selected == null)
            return;

        lblDetails.Text =
            $"Nom : {selected.Name}\r\n" +
            $"Type : {selected.Type}\r\n" +
            $"Saison : {selected.Season}\r\n\r\n" +
            $".strm : {selected.Path}\r\n\r\n" +
            $"URL : {selected.Url}";

        btnDownload.Enabled = true;
    }

    private async Task DownloadAsync()
    {
        if (selected == null)
            return;

        using var dialog = new FolderBrowserDialog
        {
            Description = "Choisis le dossier de destination du MKV"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        var safeName = string.Join("_", selected.Name.Split(Path.GetInvalidFileNameChars()));
        var destination = Path.Combine(dialog.SelectedPath, safeName + ".mkv");

        if (File.Exists(destination))
        {
            var answer = MessageBox.Show(this,
                $"Le fichier existe déjà :\r\n{destination}\r\n\r\nLe remplacer ?",
                "Fichier existant", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

            if (answer != DialogResult.Yes)
                return;
        }

        downloadCts = new CancellationTokenSource();
        btnDownload.Enabled = false;
        btnCancel.Enabled = true;
        progress.Value = 0;
        lblProgress.Text = "Préparation...";
        lblEta.Text = "";

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromHours(12) };
            using var response = await client.GetAsync(selected.Url,
                HttpCompletionOption.ResponseHeadersRead, downloadCts.Token);

            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength;

            await using var input = await response.Content.ReadAsStreamAsync(downloadCts.Token);
            await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write,
                FileShare.None, 1024 * 1024, useAsync: true);

            var buffer = new byte[1024 * 1024];
            long received = 0;
            var stopwatch = Stopwatch.StartNew();

            while (true)
            {
                var read = await input.ReadAsync(buffer, downloadCts.Token);
                if (read == 0)
                    break;

                await output.WriteAsync(buffer.AsMemory(0, read), downloadCts.Token);
                received += read;

                var elapsed = Math.Max(0.1, stopwatch.Elapsed.TotalSeconds);
                var speed = received / elapsed;

                if (total.HasValue && total.Value > 0)
                {
                    var pct = Math.Clamp((int)(received * 100L / total.Value), 0, 100);
                    progress.Value = pct;
                    var left = Math.Max(0, total.Value - received);
                    var seconds = speed > 0 ? left / speed : 0;

                    lblProgress.Text =
                        $"Téléchargement : {FormatBytes(received)} / {FormatBytes(total.Value)} ({pct} %)";
                    lblEta.Text =
                        $"Temps restant : {FormatTime(seconds)}  |  Vitesse : {FormatBytes((long)speed)}/s";
                }
                else
                {
                    lblProgress.Text = $"Téléchargement : {FormatBytes(received)}";
                    lblEta.Text = $"Temps restant : inconnu  |  Vitesse : {FormatBytes((long)speed)}/s";
                }
            }

            progress.Value = 100;
            lblProgress.Text = $"Téléchargement terminé : {FormatBytes(received)}";
            lblEta.Text = "Temps restant : 00:00:00";
            lblInfo.Text = $"Fichier enregistré : {destination}";

            MessageBox.Show(this, $"Téléchargement terminé.\r\n\r\n{destination}",
                "STRM Downloader", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            lblInfo.Text = "Téléchargement annulé.";
            TryDelete(destination);
        }
        catch (Exception ex)
        {
            lblInfo.Text = "Erreur de téléchargement.";
            TryDelete(destination);
            MessageBox.Show(this, ex.Message, "Erreur de téléchargement",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            downloadCts.Dispose();
            downloadCts = null;
            btnDownload.Enabled = selected != null;
            btnCancel.Enabled = false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["o", "Ko", "Mo", "Go", "To"];
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static string FormatTime(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0)
            return "inconnu";

        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? ts.ToString(@"hh\:mm\:ss")
            : ts.ToString(@"mm\:ss");
    }
}
