using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Carto;
using System.Windows.Forms;

namespace PolygonCuter_OneThird
{
    public class PolygonCuter_OneThird : ESRI.ArcGIS.Desktop.AddIns.Tool
    {
        private bool m_isMouseDown = false;
        private INewLineFeedback m_lineFeedback = null;
        private IPoint m_currentPoint = null;
        private IPolyline m_line = null;
        private IFeature m_feature = null;


        public PolygonCuter_OneThird()
        {
        }

        protected override void OnActivate()
        {
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
                double Tan1 = (UpperLeft.Y - LowerRight.Y) / (UpperLeft.X - LowerRight.X);
                double Tan2 = (LowerLeft.Y - UpperRight.Y) / (LowerLeft.X - UpperRight.X);
                double Tanl = (EndPoint.Y - BeginPoint.Y) / (EndPoint.X - BeginPoint.X);

                IArea AreaBigger = GeometryCollection.get_Geometry(0) as IArea;
                IArea AreaSmaller = GeometryCollection.get_Geometry(1) as IArea;
                if (AreaSmaller.Area > ((((IArea)Geo).Area) / 3))
                {
                    IArea temp = AreaBigger;
                    AreaBigger = AreaSmaller;
                    AreaSmaller = temp;
                }
                IPoint Direction = new Point();
                //move cutline by directionline
                IPoint CentroidBigger = AreaBigger.Centroid;
                IPoint CentroidSmaller = AreaSmaller.Centroid;
                IPoint CentroidLine = new Point();
                CentroidLine.X = (BeginPoint.X + EndPoint.X) / 2;
                CentroidLine.Y = (BeginPoint.Y + EndPoint.Y) / 2;

                bool AreaBigLocal = false;
                if (Tanl <= Tan1 || Tanl >= Tan2)
                {
                    Direction.Y = 0;
                    Direction.X = (CentroidBigger.X - CentroidSmaller.X) / 2;
                    AreaBigLocal = CentroidBigger.X < ((Geo as IArea).Centroid.X);
                }
                else if (Tanl > Tan1 && Tanl < Tan2)
                {
                    Direction.X = 0;
                    Direction.Y = (CentroidBigger.Y - CentroidSmaller.Y) / 2;
                    AreaBigLocal = CentroidBigger.Y < ((Geo as IArea).Centroid.Y);
                }
                int Count = 0;

                while (((int)AreaBigger.Area != (2*((int)AreaSmaller.Area))) && Count < 100)
                {
                    ESRI.ArcGIS.Geometry.ITransform2D Transform2D = m_line as ESRI.ArcGIS.Geometry.ITransform2D;
                    Transform2D.Move(Direction.X, Direction.Y);
                    IPolyline CurrentLine = Transform2D as IPolyline;
                    //IPoint CurrentCentroidLine = new Point();
                    //CurrentCentroidLine.X = (CurrentLine.FromPoint.X + CurrentLine.ToPoint.X) / 2;
                    //CurrentCentroidLine.Y = (CurrentLine.FromPoint.Y + CurrentLine.ToPoint.Y) / 2;

                    //update Geometry
                    GeometryCollection.RemoveGeometries(0, 2);
                    GeometryCollection = Topo.Cut2(Transform2D as IPolyline);
                    AreaBigger = GeometryCollection.get_Geometry(0) as IArea;
                    AreaSmaller = GeometryCollection.get_Geometry(1) as IArea;
                    if (AreaSmaller.Area > ((((IArea)Geo).Area)/3))
                    {
                        IArea temp = AreaBigger;
                        AreaBigger = AreaSmaller;
                        AreaSmaller = temp;
                    }

                    //update direction
                    if (Tanl <= Tan1 || Tanl >= Tan2)
                    {
                        if (AreaBigLocal != ((AreaBigger.Centroid.X) < ((Geo as IArea).Centroid.X)))
                        {
                            Direction.X = -Direction.X / 2;
                            AreaBigLocal = ((AreaBigger.Centroid.X) < ((Geo as IArea).Centroid.X));
                        }
                    }
                    else if (Tanl > Tan1 && Tanl < Tan2)
                    {
                        if (AreaBigLocal != ((AreaBigger.Centroid.Y) < ((Geo as IArea).Centroid.Y)))
                        {
                            Direction.Y = -Direction.Y / 2;
                            AreaBigLocal = ((AreaBigger.Centroid.Y) < ((Geo as IArea).Centroid.Y));
                        }
                    }
                    Count++;
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

            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }
    }

}
