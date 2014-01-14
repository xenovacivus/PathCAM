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
                if (listBox1.SelectedItem == null)
                {
                    connect.Enabled = false;
                }
            }

            t = new Timer();
            t.Interval = 100;
            t.Tick += new EventHandler(t_Tick);
            t.Start();
        }

        void t_Tick(object sender, EventArgs e)
        {
            string [] s = port.PortNames;
            bool matched = true;
            if (s.Length != portNames.Length)
            {
                matched = false;
            }

            if (!matched)
            {
                object selectedObject = listBox1.SelectedItem;

                portNames = s;
                listBox1.Items.Clear();
                listBox1.Items.AddRange(portNames);
                
                if (selectedObject != null)
                {
                    string str = selectedObject.ToString();
                    SelectPortName(str);
                }
                if (listBox1.SelectedItem == null && !port.IsOpen)
                {
                    connect.Enabled = false;
                }
            }
        }

        private void SelectPortName(string name)
        {
            for (int i = 0; i < listBox1.Items.Count; i++)
            {
                if (listBox1.Items[i].ToString() == name)
                {
                    listBox1.SelectedIndex = i;
                }
            }
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
                    string portName = listBox1.SelectedItem.ToString();
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
            if (!port.IsOpen)
            {
                if (listBox1.SelectedItem != null)
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
