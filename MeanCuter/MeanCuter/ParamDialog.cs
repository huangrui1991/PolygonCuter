using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace MeanCuter
{
    public partial class ParamDialog : Form
    {
        public ParamDialog()
        {
            InitializeComponent();
        }

        private void OKButton_Click(object sender, EventArgs e)
        {
            
            int Param ;
            if (int.TryParse(this.ParamBox.Text.Replace(" ",""), out Param) && (Param > 1 && Param <= 10))
            {
                MeanCuter.CutParam = Param;
                this.Close();
            }
            else
            {
                MessageBox.Show("无效的分割参数！");
                return;
            }
        }

    }
}
