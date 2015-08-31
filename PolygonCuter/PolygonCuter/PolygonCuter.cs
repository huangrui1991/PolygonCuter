using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace PolygonCuter
{
    public class PolygonCuter : ESRI.ArcGIS.Desktop.AddIns.Tool
    {
        public PolygonCuter()
        {
        }

        protected override void OnUpdate()
        {
            Enabled = ArcMap.Application != null;
        }
    }

}
