using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace GUI
{
    public partial class ModalFloatInput : Form
    {
        double result;
        public double Result
        {
            get { return result; }
        }

        bool confirmed = false;
        public bool Confirmed
        {
            get { return confirmed; }
        }

        public ModalFloatInput(string title, string message, double initial)
        {
            InitializeComponent();
            this.Name = title;
            this.Text = title;
            this.textBox1.Text = string.Format("{0:F2}", initial);
            this.label1.Text = message;
        }

        private void OkButton_Click(object sender, EventArgs e)
        {
            if (double.TryParse(textBox1.Text, out result))
            {
                confirmed = true;
            }
            this.Close();
        }

        private void CancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
