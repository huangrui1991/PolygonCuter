﻿using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geometry;

namespace MeanCuter
{
    public class MeanCuter : ESRI.ArcGIS.Desktop.AddIns.Tool
    {
        private bool m_isMouseDown = false;
        private INewLineFeedback m_lineFeedback = null;
        private IPoint m_currentPoint = null;
        private IPolyline m_line = null;
        private IFeature m_feature = null;
        private ParamDialog m_dialog = null;
        public static int CutParam = 1;

        private void Cut(int Param,IFeature Feature)
        {
            try
            {
                if (Param == 1)
                    return;
                if (Feature == null)
                {
                    MessageBox.Show("没有选中的地块！");
                    return;
                }


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
                IGeometry Geo = Feature.Shape;
                ITopologicalOperator4 Topo = Geo as ITopologicalOperator4;
                IGeometryCollection GeometryCollection = new GeometryBagClass();

                Topo.IsKnownSimple_2 = false;
                Topo.Simplify();
                Geo.SnapToSpatialReference();
                m_line.SnapToSpatialReference();
                m_line.SpatialReference = Geo.SpatialReference;
                GeometryCollection = Topo.Cut2(m_line);

                IEnvelope Env = Feature.Extent;
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
                if (AreaSmaller.Area > ((((IArea)Geo).Area) / Param))
                {
                    IArea temp = AreaBigger;
                    AreaBigger = AreaSmaller;
                    AreaSmaller = temp;
                }
                IPoint Direction = new Point();
                //move cutline by directionline
                IPoint CentroidBigger = AreaBigger.Centroid;
                IPoint CentroidSmaller = AreaSmaller.Centroid;
                
                //double MinDirection = 0;

                bool AreaBigLocal = false;
                if (Tanl <= Tan1 || Tanl >= Tan2)
                {
                    Direction.Y = 0;
                    Direction.X = (CentroidBigger.X - CentroidSmaller.X) / Param;
                    AreaBigLocal = CentroidBigger.X < ((Geo as IArea).Centroid.X);
                }
                else if (Tanl > Tan1 && Tanl < Tan2)
                {
                    Direction.X = 0;
                    Direction.Y = (CentroidBigger.Y - CentroidSmaller.Y) / Param;
                    AreaBigLocal = CentroidBigger.Y < ((Geo as IArea).Centroid.Y);
                }
                int Count = 0;

                while (((int)AreaSmaller.Area != (int)(((IArea)Geo).Area / Param)))
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
                    if (GeometryCollection.GeometryCount >= 3)
                    {
                        Exception e = new Exception("不能将图块切割成三份，请调整切割线位置");
                        throw e;
                    }
                    AreaBigger = GeometryCollection.get_Geometry(0) as IArea;
                    AreaSmaller = GeometryCollection.get_Geometry(1) as IArea;
                    if (AreaSmaller.Area > AreaBigger.Area)
                    {
                        IArea temp = AreaBigger;
                        AreaBigger = AreaSmaller;
                        AreaSmaller = temp;
                        if (AreaSmaller.Area > ((((IArea)Geo).Area) / Param))
                        {
                            IArea temp2 = AreaBigger;
                            AreaBigger = AreaSmaller;
                            AreaSmaller = temp2;
                        }
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
                    
                    if (Count > 5000)
                    {
                        Exception e = new Exception("迭代失败！");
                        throw e;
                    }

                }

                //store feature
                IGeometry Geo1 = GeometryCollection.get_Geometry(0);
                Feature.Shape = Geo1;
                IGeometry Geo2 = GeometryCollection.get_Geometry(1);
                IFeature NewFeature = FeatureCls.CreateFeature();
                NewFeature.Shape = Geo2;
                NewFeature.Store();
                Feature.Store();
                --Param;


                ESRI.ArcGIS.Geometry.ITransform2D Transform = m_line as ESRI.ArcGIS.Geometry.ITransform2D;
                CentroidBigger = AreaBigger.Centroid;
                CentroidSmaller = AreaSmaller.Centroid;

                //double MinDirection = 0;
                if (Tanl <= Tan1 || Tanl >= Tan2)
                {
                    Direction.Y = 0;
                    Direction.X = (CentroidBigger.X - CentroidSmaller.X) / Param;
                }
                else if (Tanl > Tan1 && Tanl < Tan2)
                {
                    Direction.X = 0;
                    Direction.Y = (CentroidBigger.Y - CentroidSmaller.Y) / Param;
                }
                Transform.Move(Direction.X, Direction.Y);

                if (((IArea)Geo1).Area > ((IArea)Geo2).Area)
                {
                    Cut(Param, Feature);
                }
                else
                {
                    Cut(Param, NewFeature);
                }
                ArcMap.Document.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);

            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message);
            }
        }


        public MeanCuter()
        {
        }

        protected override void OnUpdate()
        {
            Enabled = ArcMap.Application != null;
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
            {
                MessageBox.Show("没有选中的地块！");
                return;
            }

            m_dialog = new ParamDialog();
            m_dialog.Show();
        }

        protected override bool OnDeactivate()
        {
            base.OnDeactivate();
            ArcMap.Document.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
            m_lineFeedback = null;
            m_isMouseDown = false;
            return true;
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
            Cut(CutParam,m_feature);
        }
    }

}
