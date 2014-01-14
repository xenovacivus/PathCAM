using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Drawing.Drawing2D;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;
using Robot;
using Serial;
using Router;
using OpenTK;
using Geometry;
using Router.Paths;
using Commands;
using System.Reflection;

namespace GUI
{   
    public partial class PathCAM : Form, IOpenGLDrawable
    {
        private RouterGUI router;
        private string dragDropFilename = null;
        private Regex acceptedFileRegex = new Regex(@".*(\.dae|\.obj|\.stl|\.nc|\.gcode)", RegexOptions.IgnoreCase);

        public PathCAM()
        {
            InitializeComponent();

            router = new RouterGUI();
            propertyGrid.SelectedObject = router;
            drawing3D.AddObject(router);
            robotControl.AssignRouter(router);
            drawing3D.AddObject(robotControl);
            drawing3D.AddObject(this);
            drawing3D.DragDrop += this.Drawing3D_DragDrop;
            drawing3D.DragOver += this.Drawing3D_DragOver;
            drawing3D.DragEnter += this.Drawing3D_DragEnter;
            drawing3D.DragLeave += this.Drawing3D_DragLeave;
        }
 
        void Drawing3D_DragLeave(object sender, EventArgs e)
        {
            //dragDropFilename = null;
            // TODO: cancel the background worker loading the mesh, completely remove the mesh from everywhere.
        }

        Vector3 dragEnterLocation = Vector3.Zero;
        void Drawing3D_DragOver(object sender, DragEventArgs e)
        {
            if (dragDropMesh != null)
            {
                var plane = new Plane(Vector3.UnitZ, new Vector3(0, 0, 0));
                var ray = drawing3D.GetPointerRay(drawing3D.PointToClient(MousePosition));
                dragDropMesh.Offset = ray.Start + ray.Direction * plane.Distance(ray);
            }
        }

        void Drawing3D_DragDrop(object sender, DragEventArgs e)
        {
            dragDropMesh = null;
        }

        TriangleMeshGUI dragDropMesh = null;
        void Drawing3D_DragEnter(object sender, DragEventArgs e)
        {
            this.Activate();
            if (dragDropMesh != null)
            {
                e.Effect = DragDropEffects.Copy;
                return;
            }

            e.Effect = DragDropEffects.None;
            if (openFileButton.Enabled && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                Array a = (Array)e.Data.GetData(DataFormats.FileDrop);
                if (a != null && a.Length == 1)
                {
                    string filename = a.GetValue(0).ToString();
                    if (acceptedFileRegex.IsMatch(filename))
                    {
                        dragDropFilename = filename;
                        dragDropMesh = AddFile(dragDropFilename, loadObjectScale);
                        var plane = new Plane(Vector3.UnitZ, new Vector3(0, 0, 0));
                        var ray = drawing3D.GetPointerRay(new Point(e.X, e.Y));
                        dragEnterLocation = ray.Start + ray.Direction * plane.Distance(ray);
                        e.Effect = DragDropEffects.Copy;
                    }
                }
            }
        }

        private class LoadMeshData
        {
            public float scale;
            public string filename;
            public TriangleMeshGUI mesh;
        }

        private List<TriangleMeshGUI> inProgressMeshes = new List<TriangleMeshGUI>();
        internal TriangleMeshGUI AddFile(string filename, float scale)
        {
            if (filename.EndsWith(".nc", StringComparison.OrdinalIgnoreCase) || filename.EndsWith(".gcode", StringComparison.OrdinalIgnoreCase))
            {
                var commands = GCodeLoader.Load(filename);
                foreach (ICommand command in commands)
                {
                    router.AddCommand(command);
                }
                return null;
            }
            BackgroundWorker worker = new BackgroundWorker();
            worker.DoWork += worker_LoadMesh;
            worker.RunWorkerCompleted += worker_LoadMeshCompleted;
            var mesh = new TriangleMeshGUI();
            inProgressMeshes.Add(mesh);
            worker.RunWorkerAsync(new LoadMeshData() { filename = filename, scale = loadObjectScale, mesh = mesh });
            return mesh;
        }

        void worker_LoadMesh(object sender, DoWorkEventArgs e)
        {
            var data = e.Argument as LoadMeshData;
            string filename = data.filename;
            float scale = data.scale;
            var triangleMesh = data.mesh;
            if (filename.EndsWith(".dae", StringComparison.OrdinalIgnoreCase))
            {
                DAE_Loader.Load(filename, triangleMesh, scale);
            }
            else if (filename.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
            {
                OBJ_Loader.Load(filename, triangleMesh, scale);
            }
            else if (filename.EndsWith(".stl", StringComparison.OrdinalIgnoreCase))
            {
                STL_Loader.Load(filename, triangleMesh, scale);
            }
            if (triangleMesh != null && triangleMesh.Triangles.Count() > 0)
            {
                triangleMesh.GenerateTabPaths(router.ToolDiameter / 2.0f);
                triangleMesh.RefreshDisplayLists(); // The triangles will be static after this point - make sure they're correctly displayed.
            }
            e.Result = triangleMesh;
        }

        
        void IOpenGLDrawable.Draw()
        {
            try
            {
                foreach (var mesh in inProgressMeshes)
                {
                    mesh.Draw();
                }
            }
            catch (Exception)
            {
            }
        }

        void worker_LoadMeshCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            var mesh = e.Result as TriangleMeshGUI;
            if (mesh != null && mesh.Triangles.Count() > 0)
            {
                drawing3D.AddObject(mesh);
                foreach (var tab in mesh.Tabs)
                {
                    drawing3D.AddObject(tab);
                }
                inProgressMeshes.RemoveAll(m => m == mesh);
            }
        }

        private void PermiterRoutsClick(object sender, EventArgs e)
        {
            foreach (Object o in drawing3D.GetObjects())
            {
                if (o is TriangleMeshGUI)
                {
                    var triangles = o as TriangleMeshGUI;
                    var routs = PathPlanner.PlanPaths(triangles, triangles.Tabs.ConvertAll<Tabs>(tab => tab as Tabs), router);
                    foreach (var rout in routs)
                    {
                        router.RoutPath(rout, false, triangles.Offset);
                    }
                }
            }
            router.Complete();
        }

        private void boundaryCheckButton_Click(object sender, EventArgs e)
        {
            float xMin = -router.ToolDiameter;
            float xMax = router.ToolDiameter;
            float yMin = -router.ToolDiameter;
            float yMax = router.ToolDiameter;

            foreach (Object o in drawing3D.GetObjects())
            {
                if (o is TriangleMeshGUI)
                {
                    var triangles = o as TriangleMeshGUI;
                    xMin = Math.Min(triangles.MinPoint.X - router.ToolDiameter + triangles.Offset.X, xMin);
                    xMax = Math.Max(triangles.MaxPoint.X + router.ToolDiameter + triangles.Offset.X, xMax);
                    yMin = Math.Min(triangles.MinPoint.Y - router.ToolDiameter + triangles.Offset.Y, yMin);
                    yMax = Math.Max(triangles.MaxPoint.Y + router.ToolDiameter + triangles.Offset.Y, yMax);
                }
            }

            LineStrip r = new LineStrip();
            r.Append(new Vector3(xMin, yMin, router.MoveHeight));
            r.Append(new Vector3(xMax, yMin, router.MoveHeight));
            r.Append(new Vector3(xMax, yMax, router.MoveHeight));
            r.Append(new Vector3(xMin, yMax, router.MoveHeight));
            r.Append(new Vector3(xMin, yMin, router.MoveHeight));
            router.RoutPath(r, false, Vector3.Zero);
            router.Complete();
        }

        private void loadButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog d = new OpenFileDialog();
            d.Filter = "3D Files |*.dae;*.obj;*.stl;*.nc;*.gcode";
            if (DialogResult.OK == d.ShowDialog())
            {
                AddFile(d.FileName, loadObjectScale);
                foreach (Object o in drawing3D.GetObjects())
                {
                    TriangleMesh mesh = o as TriangleMesh;
                    if (mesh != null)
                    {
                        router.MoveHeight = mesh.MaxPoint.Z + 0.025f;
                        this.propertyGrid.Refresh();
                    }
                }
            }
        }

        private void clearPathsButton_Click(object sender, EventArgs e)
        {
            router.ClearCommands();
        }

        private float loadObjectScale = 1.0f;
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            float targetScale = 0.0f;
            float sourceScale = 0.0f;

            var match = new Regex(@"^(?<source>\S+):(?<target>\S+)").Match(comboBox1.Text);

            if (match.Success
                && float.TryParse(match.Groups["source"].Value, out sourceScale)
                && float.TryParse(match.Groups["target"].Value, out targetScale)
                && targetScale != 0 && sourceScale != 0)
            {
                comboBox1.BackColor = SystemColors.Window;
                openFileButton.Enabled = true;
                loadObjectScale = targetScale / sourceScale;
            }
            else
            {
                comboBox1.BackColor = Color.LightPink;
                openFileButton.Enabled = false;
            }
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PathCAM));
            this.button2 = new System.Windows.Forms.Button();
            this.propertyGrid = new System.Windows.Forms.PropertyGrid();
            this.saveGcodeButton = new System.Windows.Forms.Button();
            this.clearPathsButton = new System.Windows.Forms.Button();
            this.boundaryCheck = new System.Windows.Forms.Button();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.openFileButton = new System.Windows.Forms.Button();
            this.showRobotFormCheckbox = new System.Windows.Forms.CheckBox();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.robotControl = new GUI.RobotControl();
            this.drawing3D = new GUI.Drawing3D();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // button2
            // 
            this.button2.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.button2.Location = new System.Drawing.Point(12, 12);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(183, 23);
            this.button2.TabIndex = 1;
            this.button2.Text = "Add Perimeter Paths";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.PermiterRoutsClick);
            // 
            // propertyGrid
            // 
            this.propertyGrid.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.propertyGrid.Location = new System.Drawing.Point(12, 128);
            this.propertyGrid.Name = "propertyGrid";
            this.propertyGrid.PropertySort = System.Windows.Forms.PropertySort.NoSort;
            this.propertyGrid.Size = new System.Drawing.Size(183, 163);
            this.propertyGrid.TabIndex = 5;
            this.propertyGrid.ToolbarVisible = false;
            // 
            // saveGcodeButton
            // 
            this.saveGcodeButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.saveGcodeButton.Location = new System.Drawing.Point(12, 99);
            this.saveGcodeButton.Name = "saveGcodeButton";
            this.saveGcodeButton.Size = new System.Drawing.Size(183, 23);
            this.saveGcodeButton.TabIndex = 4;
            this.saveGcodeButton.Text = "Save GCode";
            this.saveGcodeButton.UseVisualStyleBackColor = true;
            this.saveGcodeButton.Click += new System.EventHandler(this.saveGcodeButton_Click);
            // 
            // clearPathsButton
            // 
            this.clearPathsButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.clearPathsButton.Location = new System.Drawing.Point(12, 70);
            this.clearPathsButton.Name = "clearPathsButton";
            this.clearPathsButton.Size = new System.Drawing.Size(183, 23);
            this.clearPathsButton.TabIndex = 3;
            this.clearPathsButton.Text = "Clear Paths";
            this.clearPathsButton.UseVisualStyleBackColor = true;
            this.clearPathsButton.Click += new System.EventHandler(this.clearPathsButton_Click);
            // 
            // boundaryCheck
            // 
            this.boundaryCheck.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.boundaryCheck.Location = new System.Drawing.Point(12, 41);
            this.boundaryCheck.Name = "boundaryCheck";
            this.boundaryCheck.Size = new System.Drawing.Size(183, 23);
            this.boundaryCheck.TabIndex = 2;
            this.boundaryCheck.Text = "Boundary Check Paths";
            this.boundaryCheck.UseVisualStyleBackColor = true;
            this.boundaryCheck.Click += new System.EventHandler(this.boundaryCheckButton_Click);
            // 
            // comboBox1
            // 
            this.comboBox1.BackColor = System.Drawing.SystemColors.Window;
            this.comboBox1.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "1:1 (inches)",
            "25.4:1 (millimeters)",
            ".254:1 (meters)",
            "1:12 (feet)"});
            this.comboBox1.Location = new System.Drawing.Point(87, 299);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(107, 21);
            this.comboBox1.TabIndex = 7;
            this.comboBox1.Text = "1:1 (inches)";
            this.comboBox1.TextChanged += new System.EventHandler(this.comboBox1_SelectedIndexChanged);
            // 
            // openFileButton
            // 
            this.openFileButton.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.openFileButton.Location = new System.Drawing.Point(12, 297);
            this.openFileButton.Name = "openFileButton";
            this.openFileButton.Size = new System.Drawing.Size(70, 23);
            this.openFileButton.TabIndex = 6;
            this.openFileButton.Text = "Open File";
            this.openFileButton.UseVisualStyleBackColor = true;
            this.openFileButton.Click += new System.EventHandler(this.loadButton_Click);
            // 
            // showRobotFormCheckbox
            // 
            this.showRobotFormCheckbox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.showRobotFormCheckbox.AutoSize = true;
            this.showRobotFormCheckbox.Location = new System.Drawing.Point(-1, 449);
            this.showRobotFormCheckbox.Name = "showRobotFormCheckbox";
            this.showRobotFormCheckbox.Size = new System.Drawing.Size(15, 14);
            this.showRobotFormCheckbox.TabIndex = 69;
            this.showRobotFormCheckbox.UseVisualStyleBackColor = true;
            this.showRobotFormCheckbox.CheckedChanged += new System.EventHandler(this.checkBox1_CheckedChanged);
            // 
            // pictureBox1
            // 
            this.pictureBox1.BackColor = System.Drawing.SystemColors.ActiveBorder;
            this.pictureBox1.Location = new System.Drawing.Point(86, 298);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(109, 23);
            this.pictureBox1.TabIndex = 70;
            this.pictureBox1.TabStop = false;
            // 
            // robotControl
            // 
            this.robotControl.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.robotControl.BackColor = System.Drawing.Color.Transparent;
            this.robotControl.Location = new System.Drawing.Point(-1, 327);
            this.robotControl.Name = "robotControl";
            this.robotControl.Size = new System.Drawing.Size(169, 136);
            this.robotControl.TabIndex = 8;
            this.robotControl.Visible = false;
            // 
            // drawing3D
            // 
            this.drawing3D.AllowDrop = true;
            this.drawing3D.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.drawing3D.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.drawing3D.BackColor = System.Drawing.Color.Black;
            this.drawing3D.ClearColor = System.Drawing.Color.Empty;
            this.drawing3D.Location = new System.Drawing.Point(472, 12);
            this.drawing3D.MinimumSize = new System.Drawing.Size(10, 10);
            this.drawing3D.Name = "drawing3D";
            this.drawing3D.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.drawing3D.Size = new System.Drawing.Size(100, 98);
            this.drawing3D.TabIndex = 68;
            this.drawing3D.VSync = false;
            // 
            // PathCAM
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(584, 462);
            this.Controls.Add(this.showRobotFormCheckbox);
            this.Controls.Add(this.saveGcodeButton);
            this.Controls.Add(this.button2);
            this.Controls.Add(this.clearPathsButton);
            this.Controls.Add(this.boundaryCheck);
            this.Controls.Add(this.robotControl);
            this.Controls.Add(this.openFileButton);
            this.Controls.Add(this.propertyGrid);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.pictureBox1);
            this.Controls.Add(this.drawing3D);
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MinimumSize = new System.Drawing.Size(600, 500);
            this.Name = "PathCAM";
            this.Text = "PathCAM - Toolpath generation software for CNC robots";
            this.Load += new System.EventHandler(this.PathCAM_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        private void saveGcodeButton_Click(object sender, EventArgs e)
        {
            var dialog = new SaveFileDialog();
            dialog.Filter = "GCode Files |*.nc;*.gcode";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filename = dialog.FileName;
                if (!filename.EndsWith(".nc", StringComparison.OrdinalIgnoreCase) && !filename.EndsWith(".gcode", StringComparison.OrdinalIgnoreCase))
                {
                    filename = filename + ".nc";
                }
                GCodeLoader.ExportGCode(router.GetCommands(), filename);
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            robotControl.Visible = showRobotFormCheckbox.Checked;
        }

        private void drawing3D_Load(object sender, EventArgs e)
        {

        }

        private void PathCAM_Load(object sender, EventArgs e)
        {
            // Programatically fill the entire client rectangle with the drawing area.
            // This makes sure the size is independent of window border and makes
            // editing the GUI in the designer much easier.
            this.drawing3D.Location = new Point(0, 0);
            this.drawing3D.Size = this.ClientRectangle.Size;
            robotControl.Location = new Point(0, ClientRectangle.Height - robotControl.Height);
            showRobotFormCheckbox.Location = new Point(0, ClientRectangle.Height - showRobotFormCheckbox.Height + 1);
        }
    }
}
