using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Sam_Lib
{
    public partial class Form1 : Form
    {

         static cardReader cReader = new cardReader();

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
    }
    
}
