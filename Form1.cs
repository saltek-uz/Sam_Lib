using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sam_Lib
{
    public partial class Form1 : Form
    {
   













        static cardReader cReader   = new cardReader();
        static tagWorks   tagReader = new tagWorks();
        public Form1()
        {
            InitializeComponent();

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {

            if (cReader.RdrState.RdrName == null ) cReader.Initialize();
             else textBox1.Text = cReader.RdrState.RdrName + " reader connected";

            }

        private void timer1_Tick(object sender, EventArgs e)
        {
            cReader.IsConnected = cReader.ConnectCard();
            if (cReader.IsConnected) label4.Text = "Card Found: "; else label4.Text = "No card";
            if (cReader.IsConnected)
            { 
              string ss = cReader.GetCardUid();

                label4.Text += ss;  

            };
            label2.Text = cReader.RdrState.RdrName;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (!tagReader.connectReader()) textBox1.Text = " no Tag reader enabled!";
            else
            {
                textBox1.Text = " Tag reader found !\r\n";
                string[] tmp = tagReader.metaData();
                for (int i=0;i<3;i++) textBox1.Text += tmp[i] +"\r\n";

            }




        }
    }
    
}
