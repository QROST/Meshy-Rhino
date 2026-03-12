// <author>QROST</author>

using System;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.Commands;
using MeshyRhino.Services;
using MeshyRhino.UI;

namespace MeshyRhino
{
    [Guid("A15C807B-B630-4574-B00C-F159BE77D1A3")]
    public class MeshyCommand : Command
    {
        private static MeshyGeneratorWindow _window;

        public override string EnglishName => "Meshy3DGenerator";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                if (_window != null && _window.IsLoaded)
                {
                    _window.Activate();
                    return Result.Success;
                }

                if (!MeshySettingsService.HasApiKey())
                {
                    var settingsWindow = new MeshySettingsWindow();
                    new System.Windows.Interop.WindowInteropHelper(settingsWindow).Owner = RhinoApp.MainWindowHandle();
                    settingsWindow.ShowDialog();

                    if (!MeshySettingsService.HasApiKey())
                    {
                        RhinoApp.WriteLine("[Meshy Rhino] A valid Meshy API key is required.");
                        return Result.Cancel;
                    }
                }

                _window = new MeshyGeneratorWindow();
                var helper = new System.Windows.Interop.WindowInteropHelper(_window);
                helper.Owner = RhinoApp.MainWindowHandle();
                _window.Closed += (s, args) => _window = null;
                _window.Show();

                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"[Meshy Rhino] Error: {ex.Message}");
                return Result.Failure;
            }
        }
    }
}
