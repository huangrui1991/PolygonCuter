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
using System.Windows.Forms;

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

        protected override bool OnDeactivate()
        {
            base.OnDeactivate();
            ArcMap.Document.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
            m_lineFeedback = null;
            m_isMouseDown = false;
            return true;
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
            try
            {
                IPolyline line = null;
                if (m_lineFeedback != null)
                    line = m_lineFeedback.Stop();

                if (line != null)
                    m_line = line;

                m_lineFeedback = null;
                m_isMouseDown = false;

                if (m_feature == null)
                    return;

                //get current Feature layer
                IMap Map = ArcMap.Document.FocusMap;
                IEnumLayer Layers = Map.Layers;
                ILayer Layer = Layers.Next();
                while (Layer.Name != "TBM")
                {
                    Layer = Layers.Next();
                }
                if (Layer == null)
                {
                    MessageBox.Show("获取图层TBM失败");
                    return;
                }
                Map.ClearSelection();
                IFeatureLayer FeatureLyr = Layer as IFeatureLayer;
                IFeatureClass FeatureCls = FeatureLyr.FeatureClass;

                //cut feature
                IGeometry Geo = m_feature.Shape;
                ITopologicalOperator4 Topo = Geo as ITopologicalOperator4;
                IGeometryCollection GeometryCollection = new GeometryBagClass();
                GeometryCollection = Topo.Cut2(m_line);

                if (GeometryCollection.GeometryCount == 0 || GeometryCollection.GeometryCount > 2)
                {
                    MessageBox.Show("分割失败！");
                    return;
                }
                IEnvelope Env = m_feature.Extent;
                IPoint LowerLeft = Env.LowerLeft;
                IPoint UpperLeft = Env.UpperLeft;
                IPoint LowerRight = Env.LowerRight;
                IPoint UpperRight = Env.UpperRight;
                IPoint BeginPoint = m_line.FromPoint;
                IPoint EndPoint = m_line.ToPoint;
                double tan1 = (UpperLeft.Y - LowerRight.Y) / (UpperLeft.X - LowerRight.X);
                double tan2 = (LowerLeft.Y - UpperRight.Y) / (LowerLeft.X - UpperRight.X);
                double tanl = (EndPoint.Y - BeginPoint.Y) / (EndPoint.X - BeginPoint.X);

                IArea AreaBigger = GeometryCollection.get_Geometry(0) as IArea;
                IArea AreaSmaller = GeometryCollection.get_Geometry(1) as IArea;
                if (AreaBigger.Area < AreaSmaller.Area)
                {
                    IArea temp = AreaBigger;
                    AreaBigger = AreaSmaller;
                    AreaSmaller = temp;
                }
                IPoint Direction = new Point();
                //move cutline by directionline
                IPoint CentroidBigger = AreaBigger.Centroid;
                IPoint CentroidSmaller = AreaSmaller.Centroid;
                if (tanl >= tan1 || tanl <= tan2)
                {
                    Direction.Y = 0;
                    Direction.X = CentroidBigger.X - CentroidSmaller.X;
                }
                else if (tanl > tan2 && tanl < tan1)
                {
                    Direction.X = 0;
                    Direction.Y = CentroidBigger.Y - CentroidSmaller.Y;
                }


                while ((int)AreaBigger.Area != (int)AreaSmaller.Area)
                {
                    ESRI.ArcGIS.Geometry.ITransform2D transform2D = m_line as ESRI.ArcGIS.Geometry.ITransform2D;
                    transform2D.Move(Direction.X, Direction.Y);
                    GeometryCollection.RemoveGeometries(0, 2);
                    GeometryCollection = Topo.Cut2(transform2D as IPolyline);
                    break;
                }

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
                OnDeactivate();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }

        }
    }
}
        