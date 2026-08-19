using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Mapping_Tools.Classes.SystemTools.Platform;

namespace Mapping_Tools.Avalonia.Views.Standard {
    [DontShowTitle]
    public partial class StandardView : MappingTool {
        private static readonly HttpClient HttpClient = CreateClient();
        private string releaseStatus = "Loading releases…";

        public static readonly string ToolName = "Get started";
        public static readonly string ToolDescription = string.Empty;

        public StandardView() : this(true) { }

        internal StandardView(bool loadReleases) {
            InitializeComponent();
            DataContext = this;
            if (loadReleases) _ = LoadReleasesAsync();
            else ReleaseStatus = "Release loading disabled.";
        }

        public ObservableCollection<ReleaseItem> Releases { get; } = new();
        public string VersionText =>
            $"Mapping Tools {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";

        public string ReleaseStatus {
            get => releaseStatus;
            private set => Set(ref releaseStatus, value);
        }

        private static HttpClient CreateClient() {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.Add(
                new ProductInfoHeaderValue("Mapping-Tools", "1.0"));
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            return client;
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e) =>
            await LoadReleasesAsync();

        internal async Task LoadReleasesAsync() {
            ReleaseStatus = "Loading releases…";
            try {
                string json = await HttpClient.GetStringAsync(
                    "https://api.github.com/repos/magazine0410/Mapping_Tools/releases");
                var items = ParseReleases(json);
                Releases.Clear();
                foreach (var item in items) Releases.Add(item);
                ReleaseStatus = items.Count == 0
                    ? "No releases have been published for this fork."
                    : $"{items.Count} release{(items.Count == 1 ? string.Empty : "s")}";
            } catch (Exception e) {
                ReleaseStatus = $"Could not load releases: {e.Message}";
            }
        }

        internal static ObservableCollection<ReleaseItem> ParseReleases(string json) {
            var result = new ObservableCollection<ReleaseItem>();
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array) return result;
            foreach (var release in document.RootElement.EnumerateArray()) {
                string tag = GetString(release, "tag_name");
                string title = GetString(release, "name");
                string published = GetString(release, "published_at");
                if (DateTimeOffset.TryParse(published, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal, out var date)) {
                    published = date.ToLocalTime().ToString("d", CultureInfo.CurrentCulture);
                }
                result.Add(new ReleaseItem(
                    string.IsNullOrWhiteSpace(title) ? tag : title,
                    GetString(release, "body"), published,
                    GetString(release, "html_url")));
            }
            return result;
        }

        private static string GetString(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;

        private void Release_Click(object sender, RoutedEventArgs e) {
            if (sender is Button { Tag: string url } && !string.IsNullOrWhiteSpace(url)) {
                CorePlatform.Shell.OpenFolder(url);
            }
        }
    }

    public sealed record ReleaseItem(string Title, string Body, string PublishedText,
        string Url);
}
