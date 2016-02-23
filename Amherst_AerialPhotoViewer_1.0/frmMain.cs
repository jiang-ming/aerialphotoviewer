using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Catalog;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.DataSourcesRaster;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Output;
using stdole;
using ESRI.ArcGIS.OutputUI;
using System.Runtime.InteropServices;
using System.Configuration;

namespace Amherst_AerialPhotoViewer_1._0
{
    public partial class frmMain : Form
    {
        IMapControl4 m_MapControl = null;
        IMapControl4 m_MapControltop = null;
        IMapControl4 m_MapControlbase = null;
        private CommandsEnvironment m_CommandsEnvironment = new CommandsEnvironmentClass();
        private IMosaicLayer botPublicMosaicLayer = null;
        private IMosaicLayer topPublicMosaicLayer = null;
        private string[] availableYrs = new string[] { "1980", "1984","1999", "2002", "2005", "2008", "2011", "2014" };
        private string[] oldYears = new string[] { "1980", "1984" };
        private ArrayList arylistParcelAdd = new ArrayList();
        private List<string> distinctStNum = new List<string>();
        private List<string> distinctStName = new List<string>();
        private IFeatureClass featureClassParcels = null;
        private IElement pEE = null;
        private bool m_bUpdateFocusMap1 = true;
        private bool m_bUpdateFocusMap2 = true;
        private string strBaseYr = null;
        private string strTopYr = null;
        private bool maploaded = false;
        #region setup Font Smoothing for EXPORT
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint uiAction, uint uiParam, ref int pvParam, uint fWinIni);
        /* constants used for user32 calls */
        const uint SPI_GETFONTSMOOTHING = 74;
        const uint SPI_SETFONTSMOOTHING = 75;
        const uint SPIF_UPDATEINIFILE = 0x1;
        private void DisableFontSmoothing()
        {
            bool iResult;
            int pv = 0;

            /* call to systemparametersinfo to set the font smoothing value */
            iResult = SystemParametersInfo(SPI_SETFONTSMOOTHING, 0, ref pv, SPIF_UPDATEINIFILE);
        }
        private void EnableFontSmoothing()
        {
            bool iResult;
            int pv = 0;

            /* call to systemparametersinfo to set the font smoothing value */
            iResult = SystemParametersInfo(SPI_SETFONTSMOOTHING, 1, ref pv, SPIF_UPDATEINIFILE);
        }
        private Boolean GetFontSmoothing()
        {
            bool iResult;
            int pv = 0;

            /* call to systemparametersinfo to get the font smoothing value */
            iResult = SystemParametersInfo(SPI_GETFONTSMOOTHING, 0, ref pv, 0);

            if (pv > 0)
            {
                //pv > 0 means font smoothing is ON.
                return true;
            }
            else
            {
                //pv == 0 means font smoothing is OFF.
                return false;
            }
        }
        #endregion
        public frmMain()
        {
            InitializeComponent();
            m_MapControl = (IMapControl4)this.axMapControl1.Object;
            m_MapControltop = (IMapControl4)this.axMapControl2.Object;
            m_MapControlbase = (IMapControl4)this.axMapControl3.Object;
            IMap map = new MapClass();
            map.Name = "Map";
            m_MapControl.DocumentFilename = string.Empty;
            m_MapControl.Map = map;
            //test if it's necessary
            m_MapControl.ActiveView.Refresh();
        }

        private void frmMain_Load(object sender, EventArgs e)
        {
            cboTopYear.Items.Clear();
            cboTopYear.Items.AddRange(availableYrs);
            cboBotYear.Items.Clear();
            cboBotYear.Items.AddRange(availableYrs);
        

            string dataPath = ConfigurationManager.AppSettings["BaseDataPath"];

            #region Load base map layers through lyr file CODE for desktop with ILayerFile with engine license
            ILayerFile layerFile = new LayerFileClass();
            string basemappath = ConfigurationManager.AppSettings["BaseMapLyrPath"];
            ////TODO////Validate BaseMap Layer file Path
            layerFile.Open(basemappath);
            if (layerFile.Layer != null)
            {
                IMap map = m_MapControl.ActiveView.FocusMap;
                map.AddLayer(layerFile.Layer);
            }
            #endregion
            //Load parcel layers
            IWorkspaceFactory2 workspaceFactory = new ShapefileWorkspaceFactoryClass();
            IFeatureWorkspace featureWorkspace = (IFeatureWorkspace)workspaceFactory.OpenFromFile(dataPath + @"Shape", 0);
            featureClassParcels = featureWorkspace.OpenFeatureClass("parcels");

            //?????next time we will upgrade it to read fields from layer. I read from txt because I believe it will speed up my application.
            string[] lines = System.IO.File.ReadAllLines(dataPath + @"Table\ParcelTable_3F.txt");
            int i = 0;
            List<string> lststNum = new List<string>();
            List<string> lststName = new List<string>();
            foreach (string line in lines)
            {
                string[] fields = new string[3];
                fields = line.Split(new Char[] { ',' });
                arylistParcelAdd.Add(new Parcel { FID = fields[0], StNum = fields[1], StName = fields[2] });
                lststNum.Add(fields[1]);
                lststName.Add(fields[2]);
                i += 1;
            }
            distinctStNum = lststNum.Distinct().ToList();
            distinctStName = lststName.Distinct().ToList();
            var autoCompleStNum = new AutoCompleteStringCollection();
            var autoCompleStName = new AutoCompleteStringCollection();
            //why not use ToArray 4 lines ago??
            autoCompleStNum.AddRange(distinctStNum.ToArray());
            autoCompleStName.AddRange(distinctStName.ToArray());
            tbStNa.AutoCompleteMode = AutoCompleteMode.Suggest;
            tbStNa.AutoCompleteSource = AutoCompleteSource.CustomSource;
            tbStNa.AutoCompleteCustomSource = autoCompleStName;
            tbStNum.AutoCompleteMode = AutoCompleteMode.Suggest;
            tbStNum.AutoCompleteSource = AutoCompleteSource.CustomSource;
            tbStNum.AutoCompleteCustomSource = autoCompleStNum;
            //Zoom the display to the full extent of all layers in the maop
            m_MapControl.ActiveView.Extent = m_MapControl.ActiveView.FullExtent;
            m_MapControl.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);

        }

        private void cboTopYear_DropDownClosed(object sender, EventArgs e)
        {
            string text = "";
            if (cboBotYear.Text != "")
            {
                text = cboBotYear.Text;
            }
            cboBotYear.Items.Clear();
            cboBotYear.Items.AddRange(availableYrs);
            cboBotYear.Items.Remove(cboTopYear.SelectedItem);
            cboBotYear.SelectedIndex = cboBotYear.FindStringExact(text, 0);
        }


        private void cboBotYear_DropDownClosed(object sender, EventArgs e)
        {
            string text = "";
            if (cboTopYear.Text != "")
            {
                text = cboTopYear.Text;
            }
            cboTopYear.Items.Clear();
            cboTopYear.Items.AddRange(availableYrs);
            cboTopYear.Items.Remove(cboBotYear.SelectedItem);
            cboTopYear.SelectedIndex = cboTopYear.FindStringExact(text, 0);
        }

        private void btnChangeY_Click(object sender, EventArgs e)
        {
            maploaded = true;
            System.Windows.Forms.Cursor.Current = Cursors.WaitCursor;
            if ((cboBotYear.Text != "") && (cboTopYear.Text != ""))
            {
                label3.Text = cboTopYear.Text;
                label4.Text = cboBotYear.Text;
                if (!(topPublicMosaicLayer == null))
                {
                    //Delete old top layer, ??check still the same top to save time?
                    m_MapControl.ActiveView.FocusMap.DeleteLayer((ILayer)topPublicMosaicLayer);
                    m_MapControltop.ActiveView.FocusMap.DeleteLayer((ILayer)topPublicMosaicLayer);
                }
                if (!(botPublicMosaicLayer == null))
                {
                    //Delete old bot layer, ??check still the same bot to save time?
                    m_MapControl.ActiveView.FocusMap.DeleteLayer((ILayer)botPublicMosaicLayer);
                    m_MapControlbase.ActiveView.FocusMap.DeleteLayer((ILayer)botPublicMosaicLayer);
                }
                //This version, we will use mosaicdataset========================
                IMosaicWorkspaceExtensionHelper MosaicWsHelper = new MosaicWorkspaceExtensionHelperClass();
                IWorkspaceFactory2 workspaceFactory = new FileGDBWorkspaceFactoryClass();
                string mosaicdatabase = ConfigurationManager.AppSettings["MosaicDatabase"];
                IWorkspace ws = workspaceFactory.OpenFromFile(mosaicdatabase, 0);
                IMosaicWorkspaceExtension mosaicWsExtension = MosaicWsHelper.FindExtension(ws);


                //BOT
                strBaseYr = "AmherstOrthoMosaic" + cboBotYear.Text;
                IMosaicDataset pMosaicDatasetBot = mosaicWsExtension.OpenMosaicDataset(strBaseYr);
                botPublicMosaicLayer = new MosaicLayerClass();
                botPublicMosaicLayer.CreateFromMosaicDataset(pMosaicDatasetBot);
                if (!(botPublicMosaicLayer == null))
                {
                    IFeatureLayer footprint = (IFeatureLayer)botPublicMosaicLayer.FootprintLayer;
                    ((ILayer)footprint).Visible = false;
                    ILayer botLayer = (ILayer)botPublicMosaicLayer;
                    botLayer.MinimumScale = 6000;
                    m_MapControl.ActiveView.FocusMap.AddLayer(botLayer);
                    m_MapControlbase.ActiveView.FocusMap.AddLayer(botLayer);
                }
                //TOP
                strTopYr = "AmherstOrthoMosaic" + cboTopYear.Text;
                IMosaicDataset pMosaicDatasetTop = mosaicWsExtension.OpenMosaicDataset(strTopYr);
                topPublicMosaicLayer = new MosaicLayerClass();
                topPublicMosaicLayer.CreateFromMosaicDataset(pMosaicDatasetTop);
                if (!(topPublicMosaicLayer == null))
                {
                    IFeatureLayer footprint = (IFeatureLayer)topPublicMosaicLayer.FootprintLayer;
                    ((ILayer)footprint).Visible = false;
                    ILayer topLayer = (ILayer)topPublicMosaicLayer;
                    topLayer.MinimumScale = 6000;
                    m_MapControl.ActiveView.FocusMap.AddLayer(topLayer);
                    m_MapControltop.ActiveView.FocusMap.AddLayer(topLayer);
                    ILayerEffectProperties lepSwip = m_CommandsEnvironment as ILayerEffectProperties;
                    lepSwip.SwipeLayer = topLayer;
                    //===possible problem toplayer is not public variable any more
                }

                IFeatureLayer featurelayerParcel = new FeatureLayerClass();
                featurelayerParcel.FeatureClass = featureClassParcels;
                featurelayerParcel.Name = "parcels";
                featurelayerParcel.MinimumScale = 6000;
                featurelayerParcel.Visible = false;
                IRgbColor pLColor = new RgbColorClass();
                pLColor.Red = 255;
                pLColor.Green = 255;
                pLColor.Blue = 255;

                ISimpleFillSymbol pSFS = new SimpleFillSymbolClass();
                ICartographicLineSymbol pCLineS = new CartographicLineSymbolClass();
                pCLineS.Color = pLColor;
                ILineProperties lineProperties = pCLineS as ILineProperties;
                lineProperties.Offset = 0;
                System.Double[] hpe = new System.Double[4];
                hpe[0] = 7;
                hpe[1] = 2;
                hpe[2] = 1;
                hpe[3] = 2;

                ITemplate template = new TemplateClass();
                template.Interval = 3;
                for (int i = 0; i < hpe.Length; i = i + 2)
                {
                    template.AddPatternElement(hpe[i], hpe[i + 1]);
                }
                lineProperties.Template = template;
                pCLineS.Width = 1;
                pCLineS.Cap = esriLineCapStyle.esriLCSButt;
                pCLineS.Join = esriLineJoinStyle.esriLJSBevel;
                pCLineS.Color = pLColor;
                pSFS.Outline = pCLineS;
                pSFS.Style = esriSimpleFillStyle.esriSFSHollow;

                IGeoFeatureLayer pGFL = (IGeoFeatureLayer)featurelayerParcel;
                ISimpleRenderer pRend = pGFL.Renderer as ISimpleRenderer;
                pRend.Symbol = pSFS as ISymbol;

                if ((Helper.FindMyFeatureLayer(m_MapControl.ActiveView.FocusMap, "parcels") == null))
                {
                    if (!(featurelayerParcel == null))
                    {
                        m_MapControl.ActiveView.FocusMap.AddLayer(featurelayerParcel);
                        m_MapControltop.ActiveView.FocusMap.AddLayer(featurelayerParcel);
                        m_MapControlbase.ActiveView.FocusMap.AddLayer(featurelayerParcel);
                    }
                }
                else
                {
                    //is this temp necessary???????
                    IFeatureLayer parcellayertemp = Helper.FindMyFeatureLayer(m_MapControl.ActiveView.FocusMap, "parcels");
                    m_MapControl.ActiveView.FocusMap.MoveLayer(parcellayertemp, 0);
                    m_MapControltop.ActiveView.FocusMap.MoveLayer(parcellayertemp, 0);
                    m_MapControlbase.ActiveView.FocusMap.MoveLayer(parcellayertemp, 0);
                }
                //MessageBox.Show("Top aerophoto is taken on: " + cboTopYear.Text + ".\r\nBottom areophoto is taken on: " + cboBotYear.Text + ".");
            }
            else
            {
                MessageBox.Show("Please select two years to compare");
              
            }
            GeneratePageLayout();
            System.Windows.Forms.Cursor.Current = Cursors.Default;
        }

        private void btnSwitch_Click(object sender, EventArgs e)
        {
            string topyear = cboTopYear.Text;
            string botyear = cboBotYear.Text;
            if (topyear == botyear)
            {

            }
            else
            {
                cboBotYear.Items.Clear();
                cboBotYear.Items.AddRange(availableYrs);
                cboBotYear.Items.Remove(botyear);
                cboBotYear.SelectedIndex = cboBotYear.FindStringExact(topyear, 0);

                cboTopYear.Items.Clear();
                cboTopYear.Items.AddRange(availableYrs);
                cboTopYear.Items.Remove(topyear);
                cboTopYear.SelectedIndex = cboTopYear.FindStringExact(botyear, 0);
            }
            btnChangeY_Click(sender, e);
        }

        private void tbStNum_Leave(object sender, EventArgs e)
        {
            string stNum = tbStNum.Text;
            tbStNa.AutoCompleteCustomSource.Clear();
            if (distinctStNum.Contains(stNum) && !(stNum == ""))
            {
                var query = from Parcel parcel in arylistParcelAdd
                            where parcel.StNum == stNum
                            select parcel;
                List<string> listStNa2 = new List<string>();
                foreach (Parcel p in query)
                {
                    listStNa2.Add(p.StName);
                }
                tbStNa.AutoCompleteCustomSource.AddRange(listStNa2.Distinct().ToArray());
            }
            else
            {
                tbStNa.AutoCompleteCustomSource.AddRange(distinctStName.ToArray());
            }
        }

        private void tbStNa_Leave(object sender, EventArgs e)
        {
            string stNa = tbStNa.Text;
            tbStNum.AutoCompleteCustomSource.Clear();
            if (distinctStName.Contains(stNa) && !(stNa == ""))
            {
                var query = from Parcel parcel in arylistParcelAdd
                            where parcel.StName == stNa
                            select parcel;
                List<string> listStNum2 = new List<string>();
                foreach (Parcel p in query)
                {
                    listStNum2.Add(p.StNum);
                }
                tbStNum.AutoCompleteCustomSource.AddRange(listStNum2.Distinct().ToArray());
            }
            else
            {
                tbStNum.AutoCompleteCustomSource.AddRange(distinctStNum.ToArray());
            }
            //? StName does not exist, should we pop up some notification
        }

        
        private void btnZoomTo_Click(object sender, EventArgs e)
        {
            System.Windows.Forms.Cursor.Current = Cursors.WaitCursor;
            lblWarning.Text = "";
            IQueryFilter pQF = new QueryFilterClass();
            var query = from Parcel parcel in arylistParcelAdd
                        where parcel.StNum == tbStNum.Text.ToUpper() && parcel.StName == tbStNa.Text.ToUpper()
                        select parcel.FID;
            string strZoomWhereClause = "";
            var enumerator = query.GetEnumerator();
            enumerator.MoveNext();
            lblWarning.Text = (query.Count()).ToString() + " parcel is found";
            if (query.Count() == 1)
            {
                strZoomWhereClause = "FID_M='" + enumerator.Current + "'";
                pQF.WhereClause = strZoomWhereClause;
                IFeatureCursor pFC = featureClassParcels.Search(pQF, true);
                IFeature pFea = pFC.NextFeature();
                IEnvelope pEnv = pFea.Shape.Envelope;
                pEnv.Expand(1.2, 1.2, true);
                m_MapControl.ActiveView.Extent = pEnv;
                IGeometry geometry = pFea.Shape as IGeometry;
                ISimpleFillSymbol pSFS = new ESRI.ArcGIS.Display.SimpleFillSymbolClass();
                IRgbColor myColor = new RgbColorClass();
                myColor.RGB = ColorTranslator.ToWin32(Color.SkyBlue);
                pSFS.Color = myColor;
                //outline width 20?
                pSFS.Outline.Width = 20;
                pSFS.Outline.Color = myColor;
                pSFS.Style = esriSimpleFillStyle.esriSFSForwardDiagonal;
                IFillShapeElement pFSE = new PolygonElementClass();
                pFSE.Symbol = pSFS;
                pEE = (IElement)pFSE;
                pEE.Geometry = geometry;
                m_MapControl.ActiveView.GraphicsContainer.DeleteAllElements();
                m_MapControlbase.ActiveView.GraphicsContainer.DeleteAllElements();
                m_MapControltop.ActiveView.GraphicsContainer.DeleteAllElements();
                m_MapControl.ActiveView.GraphicsContainer.AddElement(pEE, 0);
                m_MapControlbase.ActiveView.GraphicsContainer.AddElement(pEE, 0);
                m_MapControltop.ActiveView.GraphicsContainer.AddElement(pEE, 0);
                //IMapFrame MF1 = this.axPageLayoutControl1.FindElementByName("MapTop", 1) as IMapFrame;
                //IActiveView acttt = MF1.Map as IActiveView;
                //acttt.GraphicsContainer.AddElement(pEE, 0);
                //acttt.Refresh();
                //? try to remove refresh
                //?try partialrefresh and geometry
                m_MapControl.ActiveView.Refresh();
            }
            //multiple parcels share parcel information, Haven't test it yet???
            else if (query.Count() > 1)
            {
                foreach (string strFID in query)
                {
                    strZoomWhereClause += "FID_M='" + strFID + "' OR ";
                    pQF.WhereClause = strZoomWhereClause;
                }
                //absolutely wrong here 1. not eliminate the" OR " in the end. 2. why assign whereclause twice??
                pQF.WhereClause = strZoomWhereClause;
                IFeatureCursor pFC = featureClassParcels.Search(pQF, true);
                IEnvelope pEnv = new EnvelopeClass();
                IEnvelope pEnv2 = new EnvelopeClass();
                IGeometry pShp;
                IFeature pFea = pFC.NextFeature();
                //not necessary at all???
                IFeature m_Fea = pFea;
                while (!(pFea == null))
                {
                    pShp = pFea.Shape;
                    pEnv2 = pShp.Envelope;
                    pEnv.Union(pEnv2);
                    pFea = pFC.NextFeature();
                }
                pEnv.Expand(1.2, 1.2, true);
                //??add selected parcels into graphicscontainer
                m_MapControl.ActiveView.Extent = pEnv;
                m_MapControl.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
            }
            System.Windows.Forms.Cursor.Current = Cursors.Default;
        }
       
        private void btnClear_Click(object sender, EventArgs e)
        {
            tbStNa.Text = "";
            tbStNum.Text = "";
            tbStNum_Leave(sender, e);
            tbStNa_Leave(sender, e);
        }

        private void ckbParcel_CheckedChanged(object sender, EventArgs e)
        {
            if (ckbParcel.Checked)
            {
                IFeatureLayer featurelayerParcel = Helper.FindMyFeatureLayer(m_MapControl.ActiveView.FocusMap, "parcels");
                if (!(featurelayerParcel.Visible))
                {
                    featurelayerParcel.Visible = true;
                }
                if (!(pEE == null))
                {
                    m_MapControl.ActiveView.GraphicsContainer.AddElement(pEE, 0);
                    m_MapControltop.ActiveView.GraphicsContainer.AddElement(pEE, 0);
                    m_MapControlbase.ActiveView.GraphicsContainer.AddElement(pEE, 0);
                }
                m_MapControl.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                m_MapControltop.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                m_MapControlbase.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
            }
            else
            {
                IFeatureLayer featurelayerParcel = Helper.FindMyFeatureLayer(m_MapControl.ActiveView.FocusMap, "parcels");
                if (pEE != null)
                {
                    m_MapControl.ActiveView.GraphicsContainer.DeleteAllElements();
                    m_MapControltop.ActiveView.GraphicsContainer.DeleteAllElements();
                    m_MapControlbase.ActiveView.GraphicsContainer.DeleteAllElements();
                }
                if (featurelayerParcel.Visible)
                {
                    featurelayerParcel.Visible = false;
                }
                m_MapControl.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                m_MapControltop.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
                m_MapControlbase.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
            }
        }

        private void axMapControl1_OnExtentUpdated(object sender, IMapControlEvents2_OnExtentUpdatedEvent e)
        {
            if ((topPublicMosaicLayer != null) && (botPublicMosaicLayer != null))
            {
                if (((ILayer)topPublicMosaicLayer).MinimumScale >= m_MapControl.MapScale)
                {

                    IEnvelope envCurrent = m_MapControl.ActiveView.Extent;
                    IPoint ptViewCenter = new PointClass();
                    ptViewCenter.X = (m_MapControl.ActiveView.Extent.XMin + m_MapControl.ActiveView.Extent.XMax) / 2;
                    ptViewCenter.Y = (m_MapControl.ActiveView.Extent.YMin + m_MapControl.ActiveView.Extent.YMax) / 2;
                    ISpatialFilter pSFIndex = new SpatialFilterClass();
                    pSFIndex.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                    pSFIndex.Geometry = envCurrent;
                    //treat old images like 1980 1984 differently
                    int ii = System.Array.IndexOf(oldYears, cboTopYear.Text);
                    if (ii > -1)
                    {
                        IFeatureClass fcMosaicTop = topPublicMosaicLayer.MosaicDataset.Catalog;
                        if (!(fcMosaicTop == null))
                        {
                            IFeatureCursor pFCurSFResult = fcMosaicTop.Search(pSFIndex, true);
                            ICursor pCursor = (ICursor)pFCurSFResult;
                            string strClosetPhotoName = "";
                            int ixName = fcMosaicTop.Fields.FindField("Name");
                            int ixX = fcMosaicTop.Fields.FindField("CenterX");
                            int ixY = fcMosaicTop.Fields.FindField("CenterY");
                            IRow pRow = pCursor.NextRow();
                            double dsMin = 10000000000000000;
                            while (!(pRow == null))
                            {
                                double xCoor = ((System.IConvertible)pRow.get_Value(ixX)).ToDouble(null);
                                double yCoor = ((System.IConvertible)pRow.get_Value(ixY)).ToDouble(null);
                                double distance = (ptViewCenter.X - xCoor) * (ptViewCenter.X - xCoor) + (ptViewCenter.Y - yCoor) * (ptViewCenter.Y - yCoor);
                                if (distance < dsMin)
                                {
                                    dsMin = distance;
                                    strClosetPhotoName = pRow.get_Value(ixName).ToString();
                                }
                                pRow = pCursor.NextRow();
                            }

                            IMosaicFunction mosaicfunctionTop = topPublicMosaicLayer.MosaicDataset.MosaicFunction;
                            mosaicfunctionTop.DefinitionExpression = "Name = " + "'" + strClosetPhotoName + "'";
                            IMapFrame MF1 = this.axPageLayoutControl1.FindElementByName("MapTop", 1) as IMapFrame;
                            IMosaicLayer mlTopMF = Helper.FindMyMosaicLayer(MF1.Map, strTopYr);
                            if (!(mlTopMF == null))
                            {
                                IMosaicFunction mftopMosaicFunction = mlTopMF.MosaicDataset.MosaicFunction;
                                mftopMosaicFunction.DefinitionExpression = mosaicfunctionTop.DefinitionExpression;
                            }
                        }
                    }

                    int iii = System.Array.IndexOf(oldYears, cboBotYear.Text);
                    if (iii > -1)
                    {
                        IFeatureClass fcMosaicBot = botPublicMosaicLayer.MosaicDataset.Catalog;
                        if (!(fcMosaicBot == null))
                        {
                            IFeatureCursor pFCurSFResult = fcMosaicBot.Search(pSFIndex, true);
                            ICursor pCursor = (ICursor)pFCurSFResult;
                            string strClosetPhotoName = "";
                            int ixName = fcMosaicBot.Fields.FindField("Name");
                            int ixX = fcMosaicBot.Fields.FindField("CenterX");
                            int ixY = fcMosaicBot.Fields.FindField("CenterY");
                            IRow pRow = pCursor.NextRow();
                            double dsMin = 10000000000000000;
                            while (!(pRow == null))
                            {
                                double xCoor = ((System.IConvertible)pRow.get_Value(ixX)).ToDouble(null);
                                double yCoor = ((System.IConvertible)pRow.get_Value(ixY)).ToDouble(null);
                                double distance = (ptViewCenter.X - xCoor) * (ptViewCenter.X - xCoor) + (ptViewCenter.Y - yCoor) * (ptViewCenter.Y - yCoor);
                                if (distance < dsMin)
                                {
                                    dsMin = distance;
                                    strClosetPhotoName = pRow.get_Value(ixName).ToString();
                                }
                                pRow = pCursor.NextRow();
                            }

                            IMosaicFunction mosaicfunctionBot = botPublicMosaicLayer.MosaicDataset.MosaicFunction;
                            mosaicfunctionBot.DefinitionExpression = "Name = " + "'" + strClosetPhotoName + "'";
                            IMapFrame MF2 = this.axPageLayoutControl1.FindElementByName("MapBot", 1) as IMapFrame;
                            IMosaicLayer mlBotMF = Helper.FindMyMosaicLayer(MF2.Map, strBaseYr);
                            if (!(mlBotMF == null))
                            {
                                IMosaicFunction mfbotMosaicFunction = mlBotMF.MosaicDataset.MosaicFunction;
                                mfbotMosaicFunction.DefinitionExpression = mosaicfunctionBot.DefinitionExpression;
                            }
                        }
                    }
                    double mapScale = m_MapControl.MapScale;
                    m_MapControltop.CenterAt(ptViewCenter);
                    m_MapControlbase.CenterAt(ptViewCenter);
                    m_MapControltop.MapScale = mapScale;
                    m_MapControlbase.MapScale = mapScale;
                    m_MapControl.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);

                    //Update scale bar division length
                    IElement eleScaleBar = this.axPageLayoutControl1.FindElementByName("Alternating Scale Bar", 1);
                    IMapSurroundFrame msfScaleBar = eleScaleBar as IMapSurroundFrame;
                    IMapSurround mapSurround = msfScaleBar.MapSurround;
                    IScaleBar markerScaleBar = (IScaleBar)mapSurround;
                    if (m_MapControl.MapScale < 500)
                    {
                        markerScaleBar.Division = 100;
                    }
                    else if (m_MapControl.MapScale < 1000)
                    {
                        markerScaleBar.Division = 250;
                    }
                    else if (m_MapControl.MapScale < 2000)
                    {
                        markerScaleBar.Division = 500;
                    }
                    else
                    {
                        markerScaleBar.Division = 1000;
                    }
                }
                if (!(this.axPageLayoutControl1.FindElementByName("MapBot", 1) == null))
                {
                    m_bUpdateFocusMap1 = true;
                    m_bUpdateFocusMap2 = true;
                    SetExtent1();
                    SetExtent2();
                }
            }
        }

        private void axMapControl2_OnMouseDown(object sender, IMapControlEvents2_OnMouseDownEvent e)
        {
            m_bUpdateFocusMap1 = true;
            m_bUpdateFocusMap2 = true;
            IPoint ptSplitCenter = new PointClass();
            ptSplitCenter.X = e.mapX;
            ptSplitCenter.Y = e.mapY;
            m_MapControl.CenterAt(ptSplitCenter);
            m_MapControlbase.CenterAt(ptSplitCenter);
            m_MapControltop.CenterAt(ptSplitCenter);
        }
        private void axMapControl2_OnBeforeScreenDraw(object sender, IMapControlEvents2_OnBeforeScreenDrawEvent e)
        {
            axPageLayoutControl1.MousePointer = esriControlsMousePointer.esriPointerHourglass;
            axMapControl2.MousePointer = esriControlsMousePointer.esriPointerHourglass;
            axMapControl3.MousePointer = esriControlsMousePointer.esriPointerHourglass;
        }
        private void axMapControl2_OnAfterScreenDraw(object sender, IMapControlEvents2_OnAfterScreenDrawEvent e)
        {
            axPageLayoutControl1.MousePointer = esriControlsMousePointer.esriPointerDefault;
            axMapControl2.MousePointer = esriControlsMousePointer.esriPointerDefault;
            axMapControl3.MousePointer = esriControlsMousePointer.esriPointerDefault;
            SetExtent1();
        }

        private void axMapControl3_OnMouseDown(object sender, IMapControlEvents2_OnMouseDownEvent e)
        {
            m_bUpdateFocusMap2 = true;
            m_bUpdateFocusMap1 = true;
            IPoint ptSplitCenter = new PointClass();
            ptSplitCenter.X = e.mapX;
            ptSplitCenter.Y = e.mapY;
            m_MapControl.CenterAt(ptSplitCenter);
            m_MapControlbase.CenterAt(ptSplitCenter);
            m_MapControltop.CenterAt(ptSplitCenter);
        }
        private void axMapControl3_OnBeforeScreenDraw(object sender, IMapControlEvents2_OnBeforeScreenDrawEvent e)
        {
            axPageLayoutControl1.MousePointer = esriControlsMousePointer.esriPointerHourglass;
            axMapControl2.MousePointer = esriControlsMousePointer.esriPointerHourglass;
            axMapControl3.MousePointer = esriControlsMousePointer.esriPointerHourglass;
        }
        private void axMapControl3_OnAfterScreenDraw(object sender, IMapControlEvents2_OnAfterScreenDrawEvent e)
        {
            axPageLayoutControl1.MousePointer = esriControlsMousePointer.esriPointerDefault;
            axMapControl2.MousePointer = esriControlsMousePointer.esriPointerDefault;
            axMapControl3.MousePointer = esriControlsMousePointer.esriPointerDefault;
            SetExtent2();
        }
        //set extent for top MapFrame
        private void SetExtent1()
        {
            if (m_bUpdateFocusMap1 == false) return;
            IMapFrame MF1 = (IMapFrame)axPageLayoutControl1.FindElementByName("MapTop", 1);
            IActiveView activeView = (IActiveView)MF1.Map;
            IDisplayTransformation displayTransformation = activeView.ScreenDisplay.DisplayTransformation;
            displayTransformation.VisibleBounds = this.axMapControl2.Extent;
            activeView.Refresh();
            m_bUpdateFocusMap1 = false;
        }
        private void SetExtent2()
        {
            if (m_bUpdateFocusMap2 == false) return;
            IMapFrame MF2 = (IMapFrame)axPageLayoutControl1.FindElementByName("MapBot", 1);
            IActiveView activeView = (IActiveView)MF2.Map;
            IDisplayTransformation displayTransformation = activeView.ScreenDisplay.DisplayTransformation;
            displayTransformation.VisibleBounds = this.axMapControl3.Extent;
            activeView.Refresh();
            m_bUpdateFocusMap2 = false;
        }

        private void btnPrint_Click(object sender, EventArgs e)
        {
            /* The OnClick method calls the ExportActiveViewParameterized function with some parameters
           * which you can of course change.  The first parameter is resolution in dpi, the second is the resample ratio 
           * from 1(best quality) to 5 (fastest).  the third is a string which represents which export type you want to 
           * output (JPEG, PDF, etc.), the fourth is the directory to which you'd like to write, and the last is a 
           * boolean which determines whether or not the output will be clipped to graphics extent (for layouts).
           */
            //string exportFolder = ConfigurationManager.AppSettings["MapSaveDir"];
            string exportFolder = Environment.GetFolderPath(Environment.SpecialFolder.Personal);

            string sNameRoot;
            sNameRoot = "ExportActiveViewSampleOutput";

            saveFileDialog1.Filter = "PDF(*.pdf)|*.pdf";
            saveFileDialog1.Title = "Export to file";
            saveFileDialog1.InitialDirectory = exportFolder;
            saveFileDialog1.RestoreDirectory = true;
            saveFileDialog1.FileName = sNameRoot;
            saveFileDialog1.FilterIndex = 1;
            string exportfile = "";
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                exportfile = saveFileDialog1.FileName;

            }
            else
            {
                return;
            }
            System.Windows.Forms.Cursor.Current = Cursors.WaitCursor;
            lblWarning.Text = "Please Wait and Do Not Click...";
            ExportActiveViewParameterized(300, 1, "PDF", exportfile, false);
            lblWarning.Text = "Map exported!";
            System.Windows.Forms.Cursor.Current = Cursors.Default;
        }

        private void tabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            
            //////m_MapControl.ActiveView.Activate(m_MapControl.hWnd);
            //////m_MapControl.ActiveView.Deactivate();
            if (this.tabControl1.SelectedIndex == 0)
            {
                //if (this.axPageLayoutControl1.ActiveView.IsActive()) this.axPageLayoutControl1.ActiveView.Deactivate();
                //if (!m_MapControl.ActiveView.IsActive()) m_MapControl.ActiveView.Activate(m_MapControl.hWnd);
            }
            else if (this.tabControl1.SelectedIndex == 1)
            {
                //IGraphicsContainer grc = this.axPageLayoutControl1 as IGraphicsContainer;
                //grc.DeleteAllElements();
                //if (this.axPageLayoutControl1.ActiveView.IsActive()) DeactivePageLayoutControl();
                m_MapControltop.MapScale = m_MapControl.MapScale;
                m_MapControlbase.MapScale = m_MapControl.MapScale;
                //m_MapControltop.ActiveView.Activate(m_MapControltop.hWnd);
                //m_MapControlbase.ActiveView.Activate(m_MapControlbase.hWnd);
            }
            else
            {

                //if (!this.axPageLayoutControl1.ActiveView.IsActive()) this.axPageLayoutControl1.ActiveView.Activate(this.axPageLayoutControl1.hWnd);
                //if (!this.axPageLayoutControl1.ActiveView.IsActive()) ActivePageLayoutControl();

            }
        }

        #region local method
        private void GeneratePageLayout()
        {
            IPageLayout pageLayout = axPageLayoutControl1.PageLayout;
            IGraphicsContainer pGraphicsContainer = pageLayout as IGraphicsContainer;
            pGraphicsContainer.DeleteAllElements();
            IPage pPage = new PageClass();
            pPage = pageLayout.Page;
            pPage.PutCustomSize(36, 24);
            IActiveView pActiveView = pageLayout as IActiveView;
            IMap pMap = pActiveView.FocusMap;

            IMapFrame mapFrame1 = pGraphicsContainer.FindFrame(pMap) as IMapFrame;
            IEnvelope pEnvelope = new EnvelopeClass();
            pEnvelope.PutCoords(1.5, 3, 17.5, 20);
            IElement pElement = mapFrame1 as IElement;
            pElement.Geometry = pEnvelope;


            //Map frame 2
            IMap mapDF1 = new MapClass();

            IMapFrame mapFrame2 = new MapFrameClass();

            mapFrame2.Map = mapDF1;
            IElement element1 = mapFrame2 as IElement;
            IEnvelope envelope1 = new EnvelopeClass();
            envelope1.PutCoords(18.5, 3, 34.5, 20);
            element1.Geometry = envelope1;
            //?????element 1 or bot map always hide???

            pGraphicsContainer.AddElement(element1, 0);
            //#
            //////add map---problem shot
            //mapFrame1.Map = m_MapControltop.ActiveView.FocusMap;
            //mapFrame2.Map = m_MapControlbase.ActiveView.FocusMap;
            //#
            //TRY to use ObjectCopy
            //copy map to top mapframe of pagelayoutcontrol
            IObjectCopy objectCopy1 = new ObjectCopyClass();
            object toCopyMap1 = m_MapControltop.ActiveView.FocusMap;
            //IMap map1 = toCopyMap1 as IMap;
            //map1.IsFramed = true;
            object copiedMap1 = objectCopy1.Copy(toCopyMap1);
            object toOverwriteMap1 = mapFrame1.Map;
            objectCopy1.Overwrite(copiedMap1, ref toOverwriteMap1);
            //SetMapExtent1();
            //copy map to bot mapframe of pagelayoutcontrol
            IObjectCopy objectCopy2 = new ObjectCopyClass();
            object toCopyMap2 = m_MapControlbase.ActiveView.FocusMap;
            object copiedMap2 = objectCopy2.Copy(toCopyMap2);
            object toOverwriteMap2 = mapFrame2.Map;
            objectCopy2.Overwrite(copiedMap2, ref toOverwriteMap2);
            mapFrame1.Map.Name = "MapTop";
            mapFrame2.Map.Name = "MapBot";
            //try to change scale



            IGraphicsContainer container = this.axPageLayoutControl1.GraphicsContainer;
            container.Reset();
            IElement element = container.Next();
            int index = 0;
            while (element != null)
            {
                if (element is IMapFrame)
                {
                    IMapFrame mapFrame = (IMapFrame)element;
                    string sMapName = mapFrame.Map.Name;
                    IElementProperties elementProperties = (IElementProperties)element;
                    string slementName = elementProperties.Name;
                    index += 1;
                }
                element = container.Next();
            }

            //add title
            ITextElement teTitle = new TextElementClass();
            IPoint ptTitle = new PointClass();
            ptTitle.PutCoords(18, 22.5);
            IElement eleTitle = teTitle as IElement;
            eleTitle = MakeATextElement(ptTitle, tbStNum.Text + " " + tbStNa.Text + " Historical Aerial Comparison", 80);
            pGraphicsContainer.AddElement(eleTitle, 0);

            string topYear = cboTopYear.Text;
            string botYear = cboBotYear.Text;
            //Add subtitle
            ITextElement teSubTitle1 = new TextElementClass();
            IPoint ptSubTitle1 = new PointClass();
            ptSubTitle1.PutCoords(9.5, 21);
            IElement eleSubTitle1 = teSubTitle1 as IElement;
            eleSubTitle1 = MakeATextElement(ptSubTitle1, topYear, 45);
            pGraphicsContainer.AddElement(eleSubTitle1, 0);
            //Add Subtitle2
            ITextElement teSubTitle2 = new TextElementClass();
            IPoint ptSubTitle2 = new PointClass();
            ptSubTitle2.PutCoords(26.5, 21);
            IElement eleSubTitle2 = teSubTitle2 as IElement;
            eleSubTitle2 = MakeATextElement(ptSubTitle2, botYear, 45);
            pGraphicsContainer.AddElement(eleSubTitle2, 0);

            //Add Scale Bar
            AddScaleBar(mapFrame1.Map);
            //Add North Arrow
            AddNorthArrow(axPageLayoutControl1.PageLayout, mapFrame1.Map);
            axPageLayoutControl1.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewGeography, null, null);
            MessageBox.Show("Done!");
        }
        private IElement MakeATextElement(IPoint pPoint, string strText, double fontsize)
        {
            IRgbColor pRGBColor;
            ITextElement pTextElement;
            ITextSymbol pTextSymbol;
            IElement pElement;
            pRGBColor = new RgbColorClass();
            pRGBColor.Blue = 0;
            pRGBColor.Red = 0;
            pRGBColor.Green = 0;
            pTextElement = new TextElementClass();
            pElement = pTextElement as IElement;
            pElement.Geometry = pPoint;
            IFontDisp pFontDisp = new StdFontClass() as IFontDisp;
            pFontDisp.Name = "Time NewRoman";
            pFontDisp.Bold = true;

            pTextSymbol = new TextSymbolClass();
            pTextSymbol.Font = pFontDisp;
            pTextSymbol.Color = pRGBColor;
            pTextSymbol.Size = fontsize;
            pTextElement.Symbol = pTextSymbol;
            pTextElement.Text = strText;
            return pElement;
        }
        private void AddScaleBar(IMap map)
        {
            IPageLayout pagelayout = axPageLayoutControl1.PageLayout;
            IGraphicsContainer graphicscontainer = pagelayout as IGraphicsContainer;
            IActiveView activeView = pagelayout as IActiveView;
            IFrameElement frameElement = graphicscontainer.FindFrame(map);
            if (pagelayout == null || frameElement == null)
            {
                return;
            }
            IEnvelope envelope = new EnvelopeClass();
            envelope.PutCoords(1.5, 0.2, 12, 1);
            IUID uid = new UIDClass();
            uid.Value = "esriCarto.AlternatingScaleBar";
            IMapFrame mapFrame = frameElement as IMapFrame;
            IMapSurroundFrame mapSurroundFrame = mapFrame.CreateSurroundFrame(uid as UID, null);
            IElement element = mapSurroundFrame as IElement;
            element.Geometry = envelope;
            element.Activate(activeView.ScreenDisplay);
            graphicscontainer.AddElement(element, 0);
            IMapSurround mapSurround = mapSurroundFrame.MapSurround;
            IScaleBar markerScaleBar = (IScaleBar)mapSurround;
            markerScaleBar.Division = 2000;
            markerScaleBar.Divisions = 2;
            markerScaleBar.Subdivisions = 2;
            markerScaleBar.ResizeHint = esriScaleBarResizeHint.esriScaleBarFixed;
            markerScaleBar.Units = esriUnits.esriFeet;
            markerScaleBar.UnitLabelPosition = esriScaleBarPos.esriScaleBarBelow;
            markerScaleBar.DivisionsBeforeZero = 0;
            //markerScaleBar.UseMapSettings();
        }
        public void AddNorthArrow(IPageLayout pageLayout, IMap map)
        {
            if ((pageLayout == null) || (map == null))
            {
                return;
            }
            IEnvelope envelope = new EnvelopeClass();
            envelope.PutCoords(34, 21, 36, 24);
            IUID uid = new UIDClass();
            uid.Value = "esriCarto.MarkerNorthArrow";

            IGraphicsContainer graphicsContainer = pageLayout as IGraphicsContainer;
            IActiveView activeView = pageLayout as IActiveView;
            IFrameElement frameElement = graphicsContainer.FindFrame(map);
            IMapFrame mapFrame = frameElement as IMapFrame;
            IMapSurroundFrame mapSurroundFrame = mapFrame.CreateSurroundFrame(uid as UID, null);
            IElement element = mapSurroundFrame as IElement;
            element.Geometry = envelope;
            element.Activate(activeView.ScreenDisplay);
            graphicsContainer.AddElement(element, 0);
            IMapSurround mapSurround = mapSurroundFrame.MapSurround;
            IMarkerNorthArrow markerNorthArrow = mapSurround as IMarkerNorthArrow;
            IMarkerSymbol markerSymbol = markerNorthArrow.MarkerSymbol;
            ICharacterMarkerSymbol characterMarkerSymbol = markerSymbol as ICharacterMarkerSymbol;
            characterMarkerSymbol.CharacterIndex = 177;
            characterMarkerSymbol.Size = 220;
            //???is this necessary?
            markerNorthArrow.MarkerSymbol = characterMarkerSymbol;

        }
        private void ExportActiveViewParameterized(long iOutputResolution, long lResampleRatio, string ExportType, string exportfile, Boolean bClipToGraphicsExtent)
        {

            /* EXPORT PARAMETER: (iOutputResolution) the resolution requested.
             * EXPORT PARAMETER: (lResampleRatio) Output Image Quality of the export.  The value here will only be used if the export
             * object is a format that allows setting of Output Image Quality, i.e. a vector exporter.
             * The value assigned to ResampleRatio should be in the range 1 to 5.
             * 1 corresponds to "Best", 5 corresponds to "Fast"
             * EXPORT PARAMETER: (ExportType) a string which contains the export type to create.
             * EXPORT PARAMETER: (sOutputDir) a string which contains the directory to output to.
             * EXPORT PARAMETER: (bClipToGraphicsExtent) Assign True or False to determine if export image will be clipped to the graphic 
             * extent of layout elements.  This value is ignored for data view exports
             */

            /* Exports the Active View of the document to selected output format. */

            // using predefined static member
            IActiveView docActiveView = this.axPageLayoutControl1.ActiveView;
            IExport docExport;
            IPrintAndExport docPrintExport;
            IOutputRasterSettings RasterSettings;

            bool bReenable = false;

            if (GetFontSmoothing())
            {
                /* font smoothing is on, disable it and set the flag to reenable it later. */
                bReenable = true;
                DisableFontSmoothing();
                if (GetFontSmoothing())
                {
                    //font smoothing is NOT successfully disabled, error out.
                    return;
                }
                //else font smoothing was successfully disabled.
            }

            // The Export*Class() type initializes a new export class of the desired type.
            if (ExportType == "PDF")
            {
                docExport = new ExportPDFClass();
            }
            else if (ExportType == "EPS")
            {
                docExport = new ExportPSClass();
            }
            else if (ExportType == "AI")
            {
                docExport = new ExportAIClass();
            }
            else if (ExportType == "BMP")
            {

                docExport = new ExportBMPClass();
            }
            else if (ExportType == "TIFF")
            {
                docExport = new ExportTIFFClass();
            }
            else if (ExportType == "SVG")
            {
                docExport = new ExportSVGClass();
            }
            else if (ExportType == "PNG")
            {
                docExport = new ExportPNGClass();
            }
            else if (ExportType == "GIF")
            {
                docExport = new ExportGIFClass();
            }
            else if (ExportType == "EMF")
            {
                docExport = new ExportEMFClass();
            }
            else if (ExportType == "JPEG")
            {
                docExport = new ExportJPEGClass();
            }
            else
            {
                MessageBox.Show("Unsupported export type " + ExportType + ", defaulting to EMF.");
                ExportType = "EMF";
                docExport = new ExportEMFClass();
            }

            docPrintExport = new PrintAndExportClass();

            //set the name root for the export

            docExport.ExportFileName = exportfile;

            //set the export filename (which is the nameroot + the appropriate file extension)
            ////docExport.ExportFileName = sOutputDir + sNameRoot + "." + docExport.Filter.Split('.')[1].Split('|')[0].Split(')')[0];

            //Output Image Quality of the export.  The value here will only be used if the export
            // object is a format that allows setting of Output Image Quality, i.e. a vector exporter.
            // The value assigned to ResampleRatio should be in the range 1 to 5.
            // 1 corresponds to "Best", 5 corresponds to "Fast"

            // check if export is vector or raster
            if (docExport is IOutputRasterSettings)
            {
                // for vector formats, assign the desired ResampleRatio to control drawing of raster layers at export time   
                RasterSettings = (IOutputRasterSettings)docExport;
                RasterSettings.ResampleRatio = (int)lResampleRatio;

                // NOTE: for raster formats output quality of the DISPLAY is set to 1 for image export 
                // formats by default which is what should be used
            }

            docPrintExport.Export(docActiveView, docExport, iOutputResolution, bClipToGraphicsExtent, null);

            //MessageBox.Show("Finished exporting " + sOutputDir + sNameRoot + "." + docExport.Filter.Split('.')[1].Split('|')[0].Split(')')[0] + ".", "Export Active View Sample");

            MessageBox.Show("Finished exporting " + exportfile + ".", "Export Active View Sample");

            if (bReenable)
            {
                /* reenable font smoothing if we disabled it before */
                EnableFontSmoothing();
                bReenable = false;
                if (!GetFontSmoothing())
                {
                    //error: cannot reenable font smoothing.
                    MessageBox.Show("Unable to reenable Font Smoothing", "Font Smoothing error");
                }
            }
        }
        public IPoint GetCentroidOfExtent(IEnvelope env)
        {

            IPoint pt = new PointClass();
            pt.X = (env.XMax + env.XMin) / 2;
            pt.Y = (env.YMax + env.YMin) / 2;
            return pt;
        }
        private void ActivePageLayoutControl()
        {
            this.axPageLayoutControl1.ActiveView.Activate(this.axPageLayoutControl1.hWnd);
            IGraphicsContainer pGraphicsContainer = this.axPageLayoutControl1.GraphicsContainer;
            pGraphicsContainer.Reset();
            IElement pElement = pGraphicsContainer.Next();
            IDisplay pDisplay = this.axPageLayoutControl1.ActiveView.ScreenDisplay as IDisplay;
            while (pElement != null)
            {
                pElement.Activate(pDisplay);
                pElement = pGraphicsContainer.Next();
            }
            //m_bIsMapCtrActive = false;
        }
        private void DeactivePageLayoutControl()
        {
            this.axPageLayoutControl1.ActiveView.Deactivate();
            IGraphicsContainer pGraphicsContainer = this.axPageLayoutControl1.GraphicsContainer;
            pGraphicsContainer.Reset();
            IElement pElement = pGraphicsContainer.Next();
            //IDisplay pDisplay = this.axPageLayoutControl1.ActiveView.ScreenDisplay as IDisplay;
            while (pElement != null)
            {
                pElement.Deactivate();
                pElement = pGraphicsContainer.Next();
            }
        }
        #endregion local method

        private void tabControl1_Selecting(object sender, TabControlCancelEventArgs e)
        {
            e.Cancel = !maploaded;
        }

        private void axToolbarControl1_OnMouseDown(object sender, IToolbarControlEvents_OnMouseDownEvent e)
        {

        }

    }
}
