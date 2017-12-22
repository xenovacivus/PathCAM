using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO.Ports;
using Serial;
using Robot;
using Router;

namespace GUI
{
    public partial class COMPortForm : Form
    {
        SerialPortWrapper port;
        Timer t;
        String[] portNames = new String [0];


        public COMPortForm(SerialPortWrapper p)
        {
            if (p == null)
            {
                throw new NullReferenceException("Serial Port Wrapper object cannot be null");
            }

            InitializeComponent();
            port = p;

            t_Tick(null, EventArgs.Empty);
            baudBox.Text = port.BaudRate.ToString();
            SelectPortName(port.PortName);
            if (port.IsOpen)
            {
                connect.Text = "Disconnect";
            }
            else
            {
                if (comboBox1.SelectedItem == null || comboBox1.SelectedItem.ToString() == "")
                {
                    connect.Enabled = false;
                }
                //if (listBox1.SelectedItem == null)
                //{
                //    connect.Enabled = false;
                //}
            }

            t = new Timer();
            t.Interval = 100;
            t.Tick += new EventHandler(t_Tick);
            t.Start();
        }

        void t_Tick(object sender, EventArgs e)
        {
            string [] s = port.PortNames;
            if (s.Length == portNames.Length)
            {
                // No change in items, do nothing.
                return;
            }

            portNames = s;

            string lastSelected = comboBox1.SelectedItem as string;

            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(portNames);

            SelectPortName(lastSelected);

            comboBox1_SelectedIndexChanged(null, EventArgs.Empty);
        }

        private void SelectPortName(string name)
        {
            if (name == null)
            {
                return;
            }
            var index = comboBox1.Items.IndexOf(name);
            if (index >= 0)
            {
                comboBox1.SelectedIndex = index;
            }
            else if (name != null && name != "")
            {
                comboBox1.Items.Insert(0, name);
                comboBox1.SelectedIndex = 0;
            }
            //for (int i = 0; i < listBox1.Items.Count; i++)
            //{
            //    if (listBox1.Items[i].ToString() == name)
            //    {
            //        listBox1.SelectedIndex = i;
            //    }
            //}
        }

        private void connect_Click(object sender, EventArgs e)
        {
            if (port.IsOpen)
            {
                port.Close();
                connect.Text = "Connect";
            }
            else
            {
                try
                {
                    int baudRate = Convert.ToInt32(this.baudBox.Text);
                    string portName = comboBox1.Text;
                    port.Open(portName, baudRate);
                    connect.Text = "Disconnect";
                }
                catch (NullReferenceException)
                {
                    MessageBox.Show("Error Opening Com Port: No Port Name Selected!");
                }
                catch (FormatException)
                {
                    MessageBox.Show("Error Opening Com Port: Invalid Baud Rate!");
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error Opening Com Port: " + ex.Message);
                }
            }
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
           
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!port.IsOpen)
            {
                if (comboBox1.SelectedItem != null)
                {
                    this.connect.Enabled = true;
                }
                else
                {
                    this.connect.Enabled = false;
                }
            }
        }
    }
}
