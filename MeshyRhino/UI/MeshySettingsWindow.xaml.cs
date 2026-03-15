// <author>QROST</author>

using System.Windows;
using System.Windows.Controls;
using MeshyRhino.Services;

namespace MeshyRhino.UI
{
    public partial class MeshySettingsWindow : Window
    {
        public MeshySettingsWindow()
        {
            InitializeComponent();
            LoadSettings();
        }

        private void LoadSettings()
        {
            var s = MeshySettingsService.Load();

            ApiKeyTextBox.Text = s.ApiKey;

            SelectByTag(CbDefaultAiModel, s.DefaultAiModel);
            SelectByTag(CbDefaultModelType, s.DefaultModelType);
            SelectByTag(CbDefaultTopology, s.DefaultTopology);
            SelectByTag(CbDefaultSymmetry, s.DefaultSymmetryMode);
            SelectByTag(CbDefaultFormat, s.DefaultFormat);

            TbDefaultPolycount.Text = s.DefaultPolycount.ToString();
            CbDefaultPbr.IsChecked = s.EnablePbr;
            TbPollInterval.Text = s.PollIntervalMs.ToString();
            TbRetryCount.Text = s.ApiRetryCount.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string key = ApiKeyTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(key) || !key.StartsWith("msy_"))
            {
                MessageBox.Show(
                    "Please enter a valid Meshy API key (starts with msy_).",
                    "Invalid API Key",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TbDefaultPolycount.Text, out int polycount) || polycount < 100 || polycount > 300000)
            {
                MessageBox.Show("Polycount must be between 100 and 300,000.", "Invalid Setting",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TbPollInterval.Text, out int pollMs) || pollMs < 1000)
            {
                MessageBox.Show("Poll interval must be at least 1000 ms.", "Invalid Setting",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(TbRetryCount.Text, out int retryCount) || retryCount < 0 || retryCount > 10)
            {
                MessageBox.Show("Retry count must be between 0 and 10.", "Invalid Setting",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var settings = new MeshySettings
            {
                ApiKey = key,
                DefaultAiModel = GetTag(CbDefaultAiModel) ?? "latest",
                DefaultModelType = GetTag(CbDefaultModelType) ?? "standard",
                DefaultTopology = GetTag(CbDefaultTopology) ?? "triangle",
                DefaultSymmetryMode = GetTag(CbDefaultSymmetry) ?? "auto",
                DefaultFormat = GetTag(CbDefaultFormat) ?? "glb",
                DefaultPolycount = polycount,
                EnablePbr = CbDefaultPbr.IsChecked == true,
                PollIntervalMs = pollMs,
                ApiRetryCount = retryCount
            };

            MeshySettingsService.Save(settings);
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private static void SelectByTag(ComboBox cb, string tag)
        {
            if (string.IsNullOrEmpty(tag)) return;
            for (int i = 0; i < cb.Items.Count; i++)
            {
                if (cb.Items[i] is ComboBoxItem item && item.Tag as string == tag)
                {
                    cb.SelectedIndex = i;
                    return;
                }
            }
        }

        private static string GetTag(ComboBox cb)
        {
            return (cb.SelectedItem as ComboBoxItem)?.Tag as string;
        }
    }
}
