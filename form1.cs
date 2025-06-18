using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MHLauncher
{
    public partial class MHLauncher : Form
    {
        public class UserSettings
        {
            public string username { get; set; }
            public string version_type { get; set; }
            public string version { get; set; }
            public string memory { get; set; }
            public List<string> all_versions { get; set; }
            public List<string> forge_versions { get; set; }
            public List<string> fabric_versions { get; set; }
            public List<string> installed_versions { get; set; }
            public string running { get; set; }
        }

        private UserSettings settings;
        private readonly string settingsPath = @"C:\Users\DecuShunoKapushino\Desktop\Супер Носки Дракона\user_settings.json";

        // Путь к папке с установленными версиями Minecraft
        private readonly string minecraftVersionsPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @".minecraft\versions");

        private static readonly HttpClient httpClient = new HttpClient();

        public MHLauncher()
        {
            InitializeComponent();

            trackBar1.ValueChanged += TrackBar1_ValueChanged;
            richTextBox2.TextChanged += richTextBox2_TextChanged;
            trackBarMemory.ValueChanged += trackBarMemory_ValueChanged;
            comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;

            LoadUserSettings();
            InitializeUIFromSettings();
            UpdateComboBoxByTrackBar();
            CheckVersionInstalled();
        }

        private void LoadUserSettings()
        {
            if (!File.Exists(settingsPath))
            {
                settings = new UserSettings();
                return;
            }
            try
            {
                string json = File.ReadAllText(settingsPath);
                settings = JsonConvert.DeserializeObject<UserSettings>(json) ?? new UserSettings();
            }
            catch
            {
                settings = new UserSettings();
            }
        }

        private void SaveUserSettings()
        {
            try
            {
                string json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(settingsPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении настроек: " + ex.Message);
            }
        }

        private void InitializeUIFromSettings()
        {
            if (settings == null) return;

            richTextBox2.Text = settings.username ?? "";

            int mem = 1024;
            if (int.TryParse(settings.memory, out int savedMem))
                mem = savedMem;

            switch (mem)
            {
                case 1024: trackBarMemory.Value = 1; break;
                case 2048: trackBarMemory.Value = 2; break;
                case 4096: trackBarMemory.Value = 3; break;
                case 8192: trackBarMemory.Value = 4; break;
                case 16384: trackBarMemory.Value = 5; break;
                default: trackBarMemory.Value = 1; break;
            }

            switch (settings.version_type?.ToLower())
            {
                case "vanilla":
                    trackBar1.Value = 1;
                    break;
                case "fabric":
                    trackBar1.Value = 2;
                    break;
                case "forge":
                    trackBar1.Value = 3;
                    break;
                default:
                    trackBar1.Value = 1;
                    break;
            }
        }

        private void UpdateComboBoxByTrackBar()
        {
            if (settings == null) return;

            List<string> versionsToShow = new List<string>();

            switch (trackBar1.Value)
            {
                case 1:
                    versionsToShow = settings.all_versions?.FindAll(v =>
                        !v.ToLower().Contains("fabric") && !v.ToLower().Contains("forge")) ?? new List<string>();
                    break;
                case 2:
                    versionsToShow = settings.fabric_versions ?? new List<string>();
                    break;
                case 3:
                    versionsToShow = settings.forge_versions ?? new List<string>();
                    break;
                default:
                    versionsToShow = settings.all_versions ?? new List<string>();
                    break;
            }

            comboBox1.DataSource = null;
            comboBox1.DataSource = versionsToShow;

            if (!string.IsNullOrEmpty(settings.version) && versionsToShow.Contains(settings.version))
            {
                comboBox1.SelectedItem = settings.version;
            }
            else if (versionsToShow.Count > 0)
            {
                comboBox1.SelectedIndex = 0;
            }
        }

        private void TrackBar1_ValueChanged(object sender, EventArgs e)
        {
            switch (trackBar1.Value)
            {
                case 1:
                    settings.version_type = "Vanilla";
                    break;
                case 2:
                    settings.version_type = "Fabric";
                    break;
                case 3:
                    settings.version_type = "Forge";
                    break;
                default:
                    settings.version_type = "Vanilla";
                    break;
            }
            SaveUserSettings();
            UpdateComboBoxByTrackBar();
            CheckVersionInstalled();
        }

        private int GetMemoryFromTrackBar()
        {
            switch (trackBarMemory.Value)
            {
                case 1: return 1024;
                case 2: return 2048;
                case 3: return 4096;
                case 4: return 8192;
                case 5: return 16384;
                default: return 1024;
            }
        }

        private async Task SetRunningStatusOnServerAsync(string status)
        {
            var json = $"{{\"status\":{(status == null ? "null" : $"\"{status}\"")}}}";
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            try
            {
                var response = await httpClient.PostAsync("http://localhost:5002/set_running_status", content);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при обновлении статуса на сервере: " + ex.Message);
            }
        }

        private async void button_start_Click(object sender, EventArgs e)
        {
            string versionType = settings.version_type ?? "Vanilla";
            string username = richTextBox2.Text.Trim();
            string version = comboBox1.SelectedItem?.ToString() ?? "";
            int memoryMb = GetMemoryFromTrackBar();

            if (string.IsNullOrEmpty(username))
            {
                MessageBox.Show("Введите никнейм.");
                return;
            }
            if (string.IsNullOrEmpty(version))
            {
                MessageBox.Show("Выберите версию.");
                return;
            }

            settings.username = username;
            settings.version = version;
            settings.memory = memoryMb.ToString();

            if (!string.IsNullOrEmpty(settings.running))
            {
                MessageBox.Show($"Игра уже запущена: {settings.running}");
                return;
            }

            settings.running = "app";
            SaveUserSettings();

            await SetRunningStatusOnServerAsync("app");

            string pythonExe = "python";
            string scriptPath = @"C:\Users\DecuShunoKapushino\Desktop\Супер Носки Дракона\app2.py";
            string args = $"\"{scriptPath}\" \"{username}\" \"{version}\" \"{memoryMb}\" \"{versionType}\"";

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo()
                {
                    FileName = pythonExe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                };

                Process.Start(psi);

                await Task.Delay(20000);

                settings.running = null;
                SaveUserSettings();
                await SetRunningStatusOnServerAsync(null);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка запуска Python: " + ex.Message);
                settings.running = null;
                SaveUserSettings();
                await SetRunningStatusOnServerAsync(null);
            }
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            string selectedVersion = comboBox1.SelectedItem as string;
            if (selectedVersion != null && settings != null)
            {
                settings.version = selectedVersion;
                SaveUserSettings();
                CheckVersionInstalled();
            }
        }

        private void richTextBox2_TextChanged(object sender, EventArgs e)
        {
            if (settings != null)
            {
                settings.username = richTextBox2.Text.Trim();
                SaveUserSettings();
            }
        }

        private void trackBarMemory_ValueChanged(object sender, EventArgs e)
        {
            if (settings != null)
            {
                settings.memory = GetMemoryFromTrackBar().ToString();
                SaveUserSettings();
            }
        }

        private void CheckVersionInstalled()
        {
            if (settings == null)
                return;

            string selectedVersion = comboBox1.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedVersion))
            {
                SetStatus("Версия не выбрана!", Color.DarkRed);
                button_start.Enabled = false;
                return;
            }

            // Проверяем наличие папки версии в .minecraft\versions
            string versionId = selectedVersion.Split(' ')[0];
            string versionPath = Path.Combine(minecraftVersionsPath, versionId);

            bool isInstalled = Directory.Exists(versionPath);

            if (isInstalled)
            {
                SetStatus("Готово к запуску!", Color.LightGreen);
                button_start.Enabled = true;
            }
            else
            {
                SetStatus("Версия не установлена!", Color.DarkRed);
                button_start.Enabled = false;

                settings.running = null;
                SaveUserSettings();
            }
        }

        private void SetStatus(string message, Color color)
        {
            richTextBox4.Text = message;
            richTextBox4.ForeColor = color;
        }

        // Остальные пустые обработчики
        private void richTextBox1_TextChanged(object sender, EventArgs e) { }
        private void richTextBox3_TextChanged(object sender, EventArgs e) { }
        private void richTextBox4_TextChanged(object sender, EventArgs e) { }
        private void richTextBox5_TextChanged(object sender, EventArgs e) { }
        private void richTextBox6_TextChanged(object sender, EventArgs e) { }
        private void richTextBox7_TextChanged(object sender, EventArgs e) { }
        private void richTextBox8_TextChanged(object sender, EventArgs e) { }
        private void richTextBox9_TextChanged(object sender, EventArgs e) { }
        private void richTextBox10_TextChanged(object sender, EventArgs e) { }
        private void richTextBox11_TextChanged(object sender, EventArgs e) { }
        private void richTextBox12_TextChanged(object sender, EventArgs e) { }
        private void richTextBox13_TextChanged(object sender, EventArgs e) { }
        private void pictureBox1_Click(object sender, EventArgs e) { }
        private void trackBar1_Scroll(object sender, EventArgs e) { }
        private void trackBar2_Scroll(object sender, EventArgs e) { }
        private void Form1_Load(object sender, EventArgs e) { }
    }
}
