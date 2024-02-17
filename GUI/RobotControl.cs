using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Robot;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Serial;
using Router;
using Commands;

namespace GUI
{
    public partial class RobotControl : UserControl, IOpenGLDrawable
    {
        private Router.Router router;
        private Robot.Robot robot;
        private SerialPortWrapper serial;
        private COMPortForm comPortForm = null;
        Settings.UnitConverter unitConverter;

        public RobotControl()
        {
            InitializeComponent();
            serial = new SerialPortWrapper();
        }

        public void AssignRouter(Router.Router router)
        {
            this.router = router;
            this.robot = new Robot.Robot(serial);
            this.robot.onRobotStatusChange += new EventHandler(RobotStatusUpdate);
        }

        public Robot.Robot GetRobot()
        {
            return robot;
        }

        void RobotStatusUpdate(object o, EventArgs e)
        {
            if (this.InvokeRequired && !this.Disposing)
            {
                try
                {
                    this.Invoke(new EventHandler(RobotStatusUpdate), new object[] { o, e });
                }
                catch (ObjectDisposedException)
                {
                }
            }
            else
            {
                this.label3.Text = robot.IsConnected ? "Connected!" : "disconnected...";
                this.label4.Text = robot.ConnectedMachineType;
                IRobotCommandWithStatus status = o as IRobotCommandWithStatus;
                if (status != null)
                {
                    this.steppersEnabledBox.Enabled = true;
                    this.zbox.Enabled = true;
                    this.zGo.Enabled = true;

                    this.steppersEnabledBox.Checked = status.SteppersEnabled;
                    if (status.Pausing)
                    {
                        if (runButton.Text != "Pausing...")
                        {
                            runButton.Text = "Pausing...";
                            runButton.Enabled = false;
                        }
                    }
                    else
                    {
                        if (status.Paused)
                        {
                            if (runButton.Text != "Resume" || !runButton.Enabled)
                            {
                                runButton.Text = "Resume";
                                runButton.Enabled = true;
                                cancelButton.Enabled = true;
                            }
                        }
                        else if (status.Idle)
                        {
                            if (runButton.Text != "Run" || !runButton.Enabled)
                            {
                                runButton.Text = "Run";
                                runButton.Enabled = true;
                                cancelButton.Enabled = false;
                            }
                        }
                        else
                        {
                            if (runButton.Text != "Pause" || !runButton.Enabled)
                            {
                                runButton.Text = "Pause";
                                runButton.Enabled = true;
                                cancelButton.Enabled = false;
                            }
                        }
                    }
                }
                else
                {
                    if (!serial.IsOpen)
                    {
                        this.comPortButton.Text = "Connect";
                    }
                }
            }
        }


        
        internal void AssignUnitScale(Settings.UnitConverter newUnitConverter)
        {
            newUnitConverter.onUnitsChange += new EventHandler(UnitChangeEventHandler);
            unitConverter = newUnitConverter;
        }

        float zGoValue = 0.0f;

        void UnitChangeEventHandler(object o, EventArgs e)
        {
            Settings.UnitConverter unitConverter = o as Settings.UnitConverter;
            if (o == null)
            {
                return;
            }
            // If in mm, increment by 0.01.
            // If in in, increment by 0.001.
            if (unitConverter.currentSelectedUnits == Settings.MeasurementUnitTypes.Millimeters)
            {
                numericUpDown1.Increment = (decimal)0.01;
                numericUpDown1.DecimalPlaces = 2;
                numericUpDown1.Maximum = (decimal)2540.0;
            }
            else
            {
                numericUpDown1.Increment = (decimal)0.001;
                numericUpDown1.DecimalPlaces = 3;
                numericUpDown1.Maximum = (decimal)100;
            }
            numericUpDown1.Minimum = -numericUpDown1.Maximum;
            numericUpDown1.Value = (decimal)unitConverter.ToUIUnits(robot.z_offset);

            zbox.Text = string.Format("{0:0.###}", unitConverter.ToUIUnits(zGoValue));
        }

        void IOpenGLDrawable.Draw()
        {
            float diameter = router.ToolDiameter;
            diameter = Math.Max(0.1f, diameter);

            // Draw the tool location as a cone
            Vector3 position = robot.GetPosition();
            GL.Color3(Color.Silver);
            Polyhedra.DrawCone(position + new Vector3(0, 0, diameter), position, diameter / 2.0f);

            Vector3 physicalPosition = robot.GetPhysicalPosition();
            GL.Color3(Color.Black);
            Polyhedra.DrawCone(physicalPosition + new Vector3(0, 0, diameter), physicalPosition, diameter / 2.0f);
        }


        private void cancelButton_Click(object sender, EventArgs e)
        {
            robot.CancelPendingCommands();
            Vector3 position = robot.GetPosition();
            robot.AddCommand(new MoveTool(new Vector3(position.X, position.Y, router.MoveHeight), MoveTool.SpeedType.Rapid));
            robot.AddCommand(new MoveTool(new Vector3(0, 0, router.MoveHeight), MoveTool.SpeedType.Rapid));
            robot.SendResumeCommand();
            this.cancelButton.Enabled = false;
            runButton.Text = "Run";
            runButton.Enabled = true;
        }

        private void steppersEnabledBox_Click(object sender, EventArgs e)
        {
            if (steppersEnabledBox.Checked)
            {
                robot.DisableMotors();
            }
            else
            {
                robot.EnableMotors();
            }
        }

        private void runButton_Click(object sender, EventArgs e)
        {
            if (runButton.Text == "Run")
            {
                foreach (ICommand command in router.GetCommands())
                {
                    robot.AddCommand(command);
                }
                //runButton.Text = "Pause";
            }
            else if (runButton.Text == "Pause")
            {
                robot.SendPauseCommand();
                //runButton.Text = "Pausing...";
                runButton.Enabled = false;
            }
            else if (runButton.Text == "Resume")
            {
                robot.SendResumeCommand();
                //runButton.Text = "Resuming...";
                cancelButton.Enabled = false;
            }
            runButton.Enabled = false;
        }

        private void zGo_Click(object sender, EventArgs e)
        {
            if (float.TryParse(zbox.Text, out float newValue))
            {
                zGoValue = unitConverter.FromUIUnits(newValue);
                MoveTool move = new MoveTool(new Vector3(0, 0, zGoValue), MoveTool.SpeedType.Rapid);
                robot.AddCommand(move);
            }
        }

        private void comPortButton_Click(object sender, EventArgs e)
        {
            if (serial.IsOpen)
            {
                try
                {
                    serial.Close();
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error closing serial port: " + ex.Message);
                }
                comPortButton.Text = "Connect";
            }
            else
            {
                if (comPortForm == null || comPortForm.IsDisposed)
                {
                    comPortForm = new COMPortForm(serial);
                    comPortForm.Location = this.PointToScreen(new Point(0, 0));
                    comPortForm.FormClosed += new FormClosedEventHandler(formClosedEventHandler);
                }
                if (!comPortForm.Visible)
                {
                    comPortForm.Show(null);
                }
                else
                {
                    comPortForm.Focus();
                }
            }
        }

        private void formClosedEventHandler(object o, FormClosedEventArgs e)
        {
            if (serial.IsOpen)
            {
                comPortButton.Text = "Disconnect";
            }
        }

        private void zeroButton_Click(object sender, EventArgs e)
        {
            robot.Zero();
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            robot.z_offset = unitConverter.FromUIUnits((float)numericUpDown1.Value);
            this.zGo_Click(null, EventArgs.Empty);
        }

        //private List<PreviousPoint> previousPoints = new List<PreviousPoint>();
        //public class PreviousPoint
        //{
        //    public PreviousPoint(float time, Vector3 location)
        //    {
        //        this.createTime = time;
        //        this.location = location;
        //    }
        //    public float createTime;
        //    public Vector3 location;
        //}


        //void RouterPositionUpdate(object o, EventArgs e)
        //{
        //    StatusCommand status = o as StatusCommand;
        //    if (status != null)
        //    {
        //        Vector3 position = status.CurrentPosition;
        //        float time = status.time;
        //        float distance = (lastPosition - position).Length;
        //
        //        if ((lastPosition - position).Length > 0.0001f)
        //        {
        //            lock (previousPoints)
        //            {
        //                //Console.WriteLine("{0},{1}", time, distance);
        //                while (previousPoints.Count > 1000)
        //                {
        //                    previousPoints.RemoveAt(0);
        //                }
        //                previousPoints.Add(new PreviousPoint(time, position));
        //                lastPosition = position;
        //            }
        //        }
        //    }
        //}

    }
}
