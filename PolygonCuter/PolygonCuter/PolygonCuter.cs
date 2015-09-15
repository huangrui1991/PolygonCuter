using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ESRI.ArcGIS.Geometry;

using ESRI.ArcGIS.Controls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.esriSystem;

namespace PolygonCuter
{
    public class PolygonCuter : ESRI.ArcGIS.Desktop.AddIns.Tool
    {

        private bool m_isMouseDown = false;
        private INewLineFeedback m_lineFeedback = null;
        private IPoint m_currentPoint = null;
        private IPolyline m_line = null;
        private IPolygon m_polygon = null;
        private IFeature m_feature = null;


        public PolygonCuter()
        {
        }

        protected override void OnActivate()
        {
            //set cursor image
            Stream sm = this.GetType().Assembly.GetManifestResourceStream("PolygonCuter.Images.Sketch.cur");
            this.Cursor = new System.Windows.Forms.Cursor(sm);

            //get selected feature
            IEnumFeature Features = ArcMap.Document.FocusMap.FeatureSelection as IEnumFeature;
            if (Features != null)
            {
                 Features.Reset();
                 m_feature = Features.Next();
            }
            if (m_feature == null)
                return;
            
        }

        protected override void OnUpdate()
        {
            Enabled = ArcMap.Application != null;
        }

        protected override void OnMouseDown(ESRI.ArcGIS.Desktop.AddIns.Tool.MouseEventArgs arg)
        {
            if (m_lineFeedback == null)
            {
                m_lineFeedback = new NewLineFeedback();
                m_lineFeedback.Display = ArcMap.Document.ActiveView.ScreenDisplay;
            }
            if (m_isMouseDown == false)
            {
                m_isMouseDown = true;
                m_lineFeedback.Start(m_currentPoint);
            }
            else
            {
                m_lineFeedback.AddPoint(m_currentPoint);
            }
        }

        protected override void OnMouseMove(ESRI.ArcGIS.Desktop.AddIns.Tool.MouseEventArgs arg)
        {
            m_currentPoint = ArcMap.Document.ActiveView.ScreenDisplay.DisplayTransformation.ToMapPoint(arg.X, arg.Y);
            if (m_lineFeedback == null)
                return;
            m_lineFeedback.MoveTo(m_currentPoint);
        }

        protected override void OnDoubleClick()
        {
            IPolyline line = null;
            if (m_lineFeedback != null)
                line = m_lineFeedback.Stop();

            if (line != null)
                m_line = line;

            m_lineFeedback = null;
            m_isMouseDown = false;

            //IFeatureEdit2 FeatureEdit = m_feature as IFeatureEdit2;
            //ISet Set = FeatureEdit.SplitWithUpdate(m_line);
            //IPolygon p1 = Set.Next() as IPolygon;
            //IPolygon p2 = Set.Next() as IPolygon;
            //IArea a1 = p1 as IArea;
            //IArea a2 = p2 as IArea;
            //double area1 = a1.Area;
            //double area2 = a2.Area;
            
            //ArcMap.Document.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);


            //get current Feature layer
            IMap Map = ArcMap.Document.FocusMap;
            IEnumLayer Layers = Map.Layers;
            ILayer Layer = Layers.Next();
            while (Layer.Name != "TBM")
            {
                Layer = Layers.Next();
            }
            if (Layer == null)
                return;
            Map.ClearSelection();
            IFeatureLayer FeatureLyr = Layer as IFeatureLayer;
            IFeatureClass FeatureCls = FeatureLyr.FeatureClass;

            //cut feature
            IGeometry Geo = m_feature.Shape;
            ITopologicalOperator4 Topo = Geo as ITopologicalOperator4;
            IGeometryCollection GeometryCollection = new GeometryBagClass();
            GeometryCollection = Topo.Cut2(m_line);
            

            //store feature
            int count = GeometryCollection.GeometryCount;
            IGeometry Geo1 = GeometryCollection.get_Geometry(0);
            m_feature.Shape = Geo1;
            IGeometry Geo2 = GeometryCollection.get_Geometry(1);
            IFeature NewFeature = FeatureCls.CreateFeature();
            NewFeature.Shape = Geo2;
            NewFeature.Store();
            m_feature.Store();
            ArcMap.Document.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);

            
        }

    }

}
