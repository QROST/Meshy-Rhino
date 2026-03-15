// <author>QROST</author>

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using Rhino;
using MeshyRhino.Models;
using MeshyRhino.Services;

namespace MeshyRhino.UI
{
    public partial class MeshyGeneratorWindow : Window
    {
        private CancellationTokenSource _cts;
        private bool _isGenerating;
        private readonly List<string> _multiImagePaths = new List<string>();

        public MeshyGeneratorWindow()
        {
            InitializeComponent();
            ApplyDefaults();
        }

        #region Defaults

        private void ApplyDefaults()
        {
            var s = MeshySettingsService.Load();

            SelectByTag(CbAiModel, s.DefaultAiModel);
            SelectByTag(CbTopology, s.DefaultTopology);
            SelectByTag(CbModelType, s.DefaultModelType);
            SelectByTag(CbSymmetry, s.DefaultSymmetryMode);
            SelectByTag(CbFormat, s.DefaultFormat);

            TbPolycount.Text = s.DefaultPolycount.ToString();
            CbPbr.IsChecked = s.EnablePbr;

            if (s.DefaultPlacement == "block")
            {
                RbBlock.IsChecked = true;
            }
            else
            {
                RbMesh.IsChecked = true;
            }
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

        #endregion

        #region Shared Helpers

        private string GetSelectedAiModel()
        {
            return (CbAiModel.SelectedItem as ComboBoxItem)?.Tag as string ?? "latest";
        }

        private string GetSelectedTopology()
        {
            return (CbTopology.SelectedItem as ComboBoxItem)?.Tag as string ?? "triangle";
        }

        private string GetSelectedModelType()
        {
            return (CbModelType.SelectedItem as ComboBoxItem)?.Tag as string ?? "standard";
        }

        private string GetSelectedSymmetry()
        {
            return (CbSymmetry.SelectedItem as ComboBoxItem)?.Tag as string ?? "auto";
        }

        private int GetTargetPolycount()
        {
            if (int.TryParse(TbPolycount.Text, out int val) && val >= 100 && val <= 300000)
                return val;
            return 30000;
        }

        private void CbFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RbMesh == null || RbBlock == null) return;

            bool isObj = CbFormat.SelectedIndex == 1;
            RbMesh.IsEnabled = isObj;
            RbBlock.IsEnabled = isObj;
        }

        private string GetSelectedFormat()
        {
            return (CbFormat.SelectedItem as ComboBoxItem)?.Tag as string ?? "glb";
        }

        private PlacementMode GetPlacementMode()
        {
            return RbBlock.IsChecked == true ? PlacementMode.Block : PlacementMode.Mesh;
        }

        private int GetRetryCount()
        {
            return MeshySettingsService.Load().ApiRetryCount;
        }

        private void SetGenerating(bool generating)
        {
            _isGenerating = generating;
            BtnTextGenerate.IsEnabled = !generating;
            BtnImageGenerate.IsEnabled = !generating;
            BtnMultiGenerate.IsEnabled = !generating;
            BtnRetexture.IsEnabled = !generating;
            BtnCancel.Visibility = generating ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            _cts?.Cancel();
            SetStatus("Cancelling...", 0);
        }

        private void SetStatus(string text, int progress)
        {
            StatusText.Text = text;
            ProgressBar.Value = progress;
        }

        private static readonly string TextureFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MeshyRhino", "textures");

        private static void TryDeleteFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { /* best-effort cleanup */ }
        }

        private static string PersistTextureFile(string baseName, byte[] data)
        {
            if (!Directory.Exists(TextureFolder))
                Directory.CreateDirectory(TextureFolder);

            string filePath = Path.Combine(TextureFolder, $"{baseName}_{Guid.NewGuid()}.png");
            File.WriteAllBytes(filePath, data);
            return filePath;
        }

        private static string SanitizeFileName(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "Meshy_Model";

            foreach (char c in Path.GetInvalidFileNameChars())
                input = input.Replace(c, '_');

            input = input.Trim().TrimEnd('.');

            if (input.Length > 60)
                input = input.Substring(0, 60);

            return string.IsNullOrWhiteSpace(input) ? "Meshy_Model" : input;
        }

        private string ImageFileToDataUri(string filePath)
        {
            byte[] bytes = File.ReadAllBytes(filePath);
            string base64 = Convert.ToBase64String(bytes);
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            string mime;
            switch (ext)
            {
                case ".png":  mime = "image/png"; break;
                case ".webp": mime = "image/webp"; break;
                case ".gif":  mime = "image/gif"; break;
                case ".bmp":  mime = "image/bmp"; break;
                case ".tiff":
                case ".tif":  mime = "image/tiff"; break;
                default:      mime = "image/jpeg"; break;
            }
            return $"data:{mime};base64,{base64}";
        }

        private void PlaceMesh(ParsedMesh mesh, string texturePath = null)
        {
            var mode = GetPlacementMode();

            RhinoApp.InvokeOnUiThread(new Action(() =>
            {
                try
                {
                    var doc = RhinoDoc.ActiveDoc;
                    if (doc == null)
                    {
                        MessageBox.Show("No active Rhino document.", "Meshy Rhino",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        SetStatus("No document.", 0);
                        SetGenerating(false);
                        return;
                    }

                    Guid resultId;
                    switch (mode)
                    {
                        case PlacementMode.Block:
                            resultId = MeshyImportService.PlaceAsBlock(doc, mesh, texturePath);
                            break;
                        default:
                            resultId = MeshyImportService.PlaceAsMesh(doc, mesh, texturePath);
                            break;
                    }

                    RhinoApp.WriteLine($"[Meshy Rhino] Placed object {resultId} successfully.");
                    SetStatus("[Meshy Rhino] Placed successfully.", 100);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Meshy Rhino - Placement Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    SetStatus("Placement failed.", 0);
                }
                finally
                {
                    SetGenerating(false);
                }
            }));
        }

        #endregion

        #region Thumbnail

        private void ShowThumbnail(string thumbnailUrl)
        {
            if (string.IsNullOrWhiteSpace(thumbnailUrl)) return;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(thumbnailUrl, UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                ThumbnailImage.Source = bitmap;
                ThumbnailBorder.Visibility = Visibility.Visible;
            }
            catch
            {
                ThumbnailBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void HideThumbnail()
        {
            ThumbnailBorder.Visibility = Visibility.Collapsed;
            ThumbnailImage.Source = null;
        }

        #endregion

        #region Text-to-3D

        private async void BtnTextGenerate_Click(object sender, RoutedEventArgs e)
        {
            string prompt = TbTextPrompt.Text?.Trim();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                MessageBox.Show("Please enter a text prompt.", "Meshy Rhino",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetGenerating(true);
            HideThumbnail();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var settings = MeshySettingsService.Load();
            int retries = settings.ApiRetryCount;
            bool skipRefine = CbSkipRefine.IsChecked == true;

            try
            {
                using (var api = new MeshyApiService(settings.ApiKey))
                {
                    SetStatus("[Meshy Rhino] Creating preview task...", 0);
                    var previewRequest = new TextTo3DPreviewRequest
                    {
                        Prompt = prompt,
                        AiModel = GetSelectedAiModel(),
                        Topology = GetSelectedTopology(),
                        TargetPolycount = GetTargetPolycount(),
                        ModelType = GetSelectedModelType() == "standard" ? null : GetSelectedModelType(),
                        SymmetryMode = GetSelectedSymmetry()
                    };

                    string previewId = await api.WithRetryAsync(
                        () => api.CreateTextTo3DPreviewAsync(previewRequest, _cts.Token),
                        retries, _cts.Token);

                    SetStatus("[Meshy Rhino] Generating preview...", 5);

                    int progressScale = skipRefine ? 1 : 2;
                    var previewResult = await api.PollUntilCompleteAsync(
                        previewId, GenerationMode.TextTo3D,
                        new Progress<int>(p => SetStatus($"Preview: {p}%", p / progressScale)),
                        _cts.Token, settings.PollIntervalMs);

                    ShowThumbnail(previewResult.ThumbnailUrl);

                    if (skipRefine)
                    {
                        await DownloadAndPlace(api, previewResult, prompt, _cts.Token);
                        return;
                    }

                    SetStatus("[Meshy Rhino] Creating refine task...", 50);
                    var refineRequest = new TextTo3DRefineRequest
                    {
                        PreviewTaskId = previewId,
                        EnablePbr = CbPbr.IsChecked == true,
                        AiModel = GetSelectedAiModel(),
                        TexturePrompt = TbTexturePrompt.Text?.Trim()
                    };

                    string refineId = await api.WithRetryAsync(
                        () => api.CreateTextTo3DRefineAsync(refineRequest, _cts.Token),
                        retries, _cts.Token);

                    SetStatus("[Meshy Rhino] Refining with texture...", 55);

                    var refineResult = await api.PollUntilCompleteAsync(
                        refineId, GenerationMode.TextTo3D,
                        new Progress<int>(p => SetStatus($"Refine: {p}%", 50 + p / 2)),
                        _cts.Token, settings.PollIntervalMs);

                    ShowThumbnail(refineResult.ThumbnailUrl);
                    await DownloadAndPlace(api, refineResult, prompt, _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Cancelled.", 0);
                SetGenerating(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Meshy Rhino - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Failed.", 0);
                SetGenerating(false);
            }
        }

        #endregion

        #region Image-to-3D

        private async void BtnImageGenerate_Click(object sender, RoutedEventArgs e)
        {
            string imageUrl = TbImageUrl.Text?.Trim();
            string imagePath = TbImagePath.Text?.Trim();

            if (string.IsNullOrWhiteSpace(imageUrl) && string.IsNullOrWhiteSpace(imagePath))
            {
                MessageBox.Show("Please select an image or enter a URL.", "Meshy Rhino",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(imageUrl) && !string.IsNullOrWhiteSpace(imagePath))
                imageUrl = ImageFileToDataUri(imagePath);

            SetGenerating(true);
            HideThumbnail();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var settings = MeshySettingsService.Load();
            int retries = settings.ApiRetryCount;

            try
            {
                using (var api = new MeshyApiService(settings.ApiKey))
                {
                    SetStatus("[Meshy Rhino] Creating image-to-3D task...", 0);
                    var request = new ImageTo3DRequest
                    {
                        ImageUrl = imageUrl,
                        AiModel = GetSelectedAiModel(),
                        Topology = GetSelectedTopology(),
                        TargetPolycount = GetTargetPolycount(),
                        ShouldTexture = CbShouldTexture.IsChecked == true,
                        EnablePbr = CbPbr.IsChecked == true,
                        SymmetryMode = GetSelectedSymmetry(),
                        RemoveLighting = CbRemoveLighting.IsChecked == true ? true : (bool?)null
                    };

                    string taskId = await api.WithRetryAsync(
                        () => api.CreateImageTo3DAsync(request, _cts.Token),
                        retries, _cts.Token);

                    SetStatus("[Meshy Rhino] Generating...", 5);

                    var result = await api.PollUntilCompleteAsync(
                        taskId, GenerationMode.ImageTo3D,
                        new Progress<int>(p => SetStatus($"Progress: {p}%", p)),
                        _cts.Token, settings.PollIntervalMs);

                    ShowThumbnail(result.ThumbnailUrl);
                    await DownloadAndPlace(api, result, "Meshy_ImageTo3D", _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Cancelled.", 0);
                SetGenerating(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Meshy Rhino - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Failed.", 0);
                SetGenerating(false);
            }
        }

        private void BtnBrowseImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png|All Files|*.*",
                Title = "Select Image for 3D Generation"
            };
            if (dlg.ShowDialog() == true)
            {
                TbImagePath.Text = dlg.FileName;
                TbImageUrl.Text = string.Empty;
            }
        }

        #endregion

        #region Multi-Image-to-3D

        private async void BtnMultiGenerate_Click(object sender, RoutedEventArgs e)
        {
            if (_multiImagePaths.Count == 0)
            {
                MessageBox.Show("Please add at least one image.", "Meshy Rhino",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SetGenerating(true);
            HideThumbnail();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var settings = MeshySettingsService.Load();
            int retries = settings.ApiRetryCount;

            try
            {
                var imageUrls = _multiImagePaths.Select(p => ImageFileToDataUri(p)).ToList();

                using (var api = new MeshyApiService(settings.ApiKey))
                {
                    SetStatus("[Meshy Rhino] Creating multi-image-to-3D task...", 0);
                    var request = new MultiImageTo3DRequest
                    {
                        ImageUrls = imageUrls,
                        AiModel = GetSelectedAiModel(),
                        Topology = GetSelectedTopology(),
                        TargetPolycount = GetTargetPolycount(),
                        ShouldTexture = CbMultiShouldTexture.IsChecked == true,
                        EnablePbr = CbPbr.IsChecked == true,
                        SymmetryMode = GetSelectedSymmetry(),
                        RemoveLighting = CbRemoveLighting.IsChecked == true ? true : (bool?)null
                    };

                    string taskId = await api.WithRetryAsync(
                        () => api.CreateMultiImageTo3DAsync(request, _cts.Token),
                        retries, _cts.Token);

                    SetStatus("[Meshy Rhino] Generating...", 5);

                    var result = await api.PollUntilCompleteAsync(
                        taskId, GenerationMode.MultiImageTo3D,
                        new Progress<int>(p => SetStatus($"Progress: {p}%", p)),
                        _cts.Token, settings.PollIntervalMs);

                    ShowThumbnail(result.ThumbnailUrl);
                    await DownloadAndPlace(api, result, "Meshy_MultiImageTo3D", _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Cancelled.", 0);
                SetGenerating(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Meshy Rhino - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Failed.", 0);
                SetGenerating(false);
            }
        }

        private void BtnAddMultiImage_Click(object sender, RoutedEventArgs e)
        {
            if (_multiImagePaths.Count >= 4)
            {
                MessageBox.Show("Maximum 4 images allowed.", "Meshy Rhino",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png|All Files|*.*",
                Title = "Select Image"
            };
            if (dlg.ShowDialog() == true)
            {
                _multiImagePaths.Add(dlg.FileName);
                LbMultiImages.Items.Add(Path.GetFileName(dlg.FileName));
            }
        }

        private void BtnRemoveMultiImage_Click(object sender, RoutedEventArgs e)
        {
            int idx = LbMultiImages.SelectedIndex;
            if (idx >= 0)
            {
                _multiImagePaths.RemoveAt(idx);
                LbMultiImages.Items.RemoveAt(idx);
            }
        }

        #endregion

        #region Text-to-Texture (Retexture)

        private async void BtnRetexture_Click(object sender, RoutedEventArgs e)
        {
            var request = new RetextureRequest
            {
                AiModel = GetSelectedAiModel(),
                EnablePbr = CbPbr.IsChecked == true,
                EnableOriginalUv = CbRetextureOriginalUv.IsChecked == true,
                RemoveLighting = CbRemoveLighting.IsChecked == true
            };

            if (RbRetextureTaskId.IsChecked == true)
            {
                string taskId = TbRetextureTaskId.Text?.Trim();
                if (string.IsNullOrWhiteSpace(taskId))
                {
                    MessageBox.Show("Please enter a task ID.", "Meshy Rhino",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                request.InputTaskId = taskId;
            }
            else
            {
                string modelPath = TbRetextureModelPath.Text?.Trim();
                if (string.IsNullOrWhiteSpace(modelPath) || !File.Exists(modelPath))
                {
                    MessageBox.Show("Please select a valid model file.", "Meshy Rhino",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                byte[] modelBytes = File.ReadAllBytes(modelPath);
                string ext = Path.GetExtension(modelPath).ToLowerInvariant();
                string mime = ext == ".glb" ? "model/gltf-binary"
                    : ext == ".obj" ? "text/plain"
                    : ext == ".fbx" ? "application/octet-stream"
                    : "application/octet-stream";
                request.ModelUrl = $"data:{mime};base64,{Convert.ToBase64String(modelBytes)}";
            }

            if (RbRetextureByText.IsChecked == true)
            {
                string prompt = TbRetexturePrompt.Text?.Trim();
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    MessageBox.Show("Please enter a text style prompt.", "Meshy Rhino",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                request.TextStylePrompt = prompt;
            }
            else
            {
                string imagePath = TbRetextureImagePath.Text?.Trim();
                if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
                {
                    MessageBox.Show("Please select a reference image.", "Meshy Rhino",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                request.ImageStyleUrl = ImageFileToDataUri(imagePath);
            }

            SetGenerating(true);
            HideThumbnail();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var settings = MeshySettingsService.Load();
            int retries = settings.ApiRetryCount;

            try
            {
                using (var api = new MeshyApiService(settings.ApiKey))
                {
                    SetStatus("[Meshy Rhino] Creating retexture task...", 0);

                    string newTaskId = await api.WithRetryAsync(
                        () => api.CreateRetextureAsync(request, _cts.Token),
                        retries, _cts.Token);

                    SetStatus("[Meshy Rhino] Retexturing...", 5);

                    var result = await api.PollUntilCompleteAsync(
                        newTaskId, GenerationMode.Retexture,
                        new Progress<int>(p => SetStatus($"Progress: {p}%", p)),
                        _cts.Token, settings.PollIntervalMs);

                    ShowThumbnail(result.ThumbnailUrl);
                    await DownloadAndPlace(api, result, "Meshy_Retexture", _cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                SetStatus("Cancelled.", 0);
                SetGenerating(false);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Meshy Rhino - Error", MessageBoxButton.OK, MessageBoxImage.Error);
                SetStatus("Failed.", 0);
                SetGenerating(false);
            }
        }

        private void BtnBrowseRetextureModel_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "3D Models|*.glb;*.gltf;*.obj;*.fbx;*.stl|All Files|*.*",
                Title = "Select Model for Retexturing"
            };
            if (dlg.ShowDialog() == true)
                TbRetextureModelPath.Text = dlg.FileName;
        }

        private void BtnBrowseRetextureImage_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Images|*.jpg;*.jpeg;*.png|All Files|*.*",
                Title = "Select Reference Style Image"
            };
            if (dlg.ShowDialog() == true)
                TbRetextureImagePath.Text = dlg.FileName;
        }

        #endregion

        #region Download & Place

        private async Task DownloadAndPlace(MeshyApiService api, MeshyTaskStatus result, string name, CancellationToken ct)
        {
            name = SanitizeFileName(name);
            string format = GetSelectedFormat();
            int retries = GetRetryCount();

            if (format == "glb")
            {
                string glbUrl = result.ModelUrls?.Glb;
                if (string.IsNullOrWhiteSpace(glbUrl))
                {
                    MessageBox.Show("No GLB model URL in the result.", "Meshy Rhino", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetStatus("No GLB available.", 0);
                    SetGenerating(false);
                    return;
                }

                SetStatus("[Meshy Rhino] Downloading GLB...", 90);
                byte[] glbBytes = await api.WithRetryAsync(
                    () => api.DownloadBytesAsync(glbUrl, ct), retries, ct);
                string tempPath = Path.Combine(Path.GetTempPath(), $"{name}_{Guid.NewGuid()}.glb");
                File.WriteAllBytes(tempPath, glbBytes);

                SetStatus("[Meshy Rhino] Importing GLB...", 98);

                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    try
                    {
                        bool success = RhinoApp.RunScript($"_-Import \"{tempPath}\" _Enter", false);
                        SetStatus(success ? "[Meshy Rhino] Imported successfully." : "Import failed.",
                                  success ? 100 : 0);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Meshy Rhino - Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        SetStatus("Import failed.", 0);
                    }
                    finally
                    {
                        SetGenerating(false);
                        TryDeleteFile(tempPath);
                    }
                }));
            }
            else
            {
                string objUrl = result.ModelUrls?.Obj;
                if (string.IsNullOrWhiteSpace(objUrl))
                {
                    MessageBox.Show("No OBJ model URL in the result. The task may not have produced geometry.",
                        "Meshy Rhino - No Model", MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetStatus("No OBJ available.", 0);
                    SetGenerating(false);
                    return;
                }

                SetStatus("[Meshy Rhino] Downloading OBJ...", 90);
                string objContent = await api.WithRetryAsync(
                    () => api.DownloadObjAsync(objUrl, ct), retries, ct);

                string texturePath = null;
                if (result.TextureUrlsList != null && result.TextureUrlsList.Count > 0)
                {
                    string texUrl = result.TextureUrlsList[0].BaseColor;
                    if (!string.IsNullOrWhiteSpace(texUrl))
                    {
                        SetStatus("[Meshy Rhino] Downloading texture...", 95);
                        byte[] texBytes = await api.WithRetryAsync(
                            () => api.DownloadBytesAsync(texUrl, ct), retries, ct);
                        texturePath = PersistTextureFile(name, texBytes);
                    }
                }

                SetStatus("[Meshy Rhino] Parsing mesh...", 97);
                var mesh = MeshyObjParser.Parse(objContent, name);

                if (mesh.FaceCount == 0)
                {
                    MessageBox.Show("Downloaded model contains no faces.", "Meshy Rhino - Empty Model",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    SetStatus("Empty model.", 0);
                    SetGenerating(false);
                    return;
                }

                SetStatus($"[Meshy Rhino] Placing mesh ({mesh.VertexCount} vertices, {mesh.FaceCount} faces)...", 98);
                PlaceMesh(mesh, texturePath);
            }
        }

        #endregion

        #region Settings

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            var settingsWindow = new MeshySettingsWindow();
            settingsWindow.Owner = this;
            if (settingsWindow.ShowDialog() == true)
                ApplyDefaults();
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;

            base.OnClosed(e);
        }
    }
}
