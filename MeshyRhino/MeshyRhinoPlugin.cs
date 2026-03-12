// <author>QROST</author>

using System;
using System.Runtime.InteropServices;
using Rhino;
using Rhino.PlugIns;

namespace MeshyRhino
{
    [Guid("024F47AB-85F2-4939-BCD2-26F89FBED188")]
    public class MeshyRhinoPlugin : PlugIn
    {
        public static MeshyRhinoPlugin Instance { get; private set; }

        public MeshyRhinoPlugin()
        {
            Instance = this;
        }

        public override PlugInLoadTime LoadTime => PlugInLoadTime.AtStartup;

        protected override LoadReturnCode OnLoad(ref string errorMessage)
        {
            RhinoApp.WriteLine("[Meshy Rhino] Plugin loaded.");
            return LoadReturnCode.Success;
        }
    }
}
