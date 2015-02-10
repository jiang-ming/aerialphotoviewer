using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace Amherst_AerialPhotoViewer_1._0
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            ESRI.ArcGIS.RuntimeManager.Bind(ESRI.ArcGIS.ProductCode.Engine);
            Application.Run(new frmMain());
        }
    }
}
