﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using Robot;
using Serial;
using Router;
using Geometry;

namespace GUI
{
    public partial class Drawing3D : GLControl
    {
        Color clearColor;
        Viewport3d viewport;
        Vector2 lastMouseLocation = new Vector2(0, 0);
        IClickable3D clickedObject = null;
        private Router.Router router;

        Timer drawTimer;

        public Drawing3D() : base(new OpenTK.Graphics.GraphicsMode(32, 24, 0, 8))
        {
            this.InitializeComponent();

            this.viewport = new Viewport3d(this);
            this.Resize += new EventHandler(HandleControlResize);
            this.MouseMove += new MouseEventHandler(HandleMouseMove);
            this.MouseEnter += new EventHandler(HandleMouseEnter);
            this.MouseLeave += new EventHandler(HandleMouseLeave);
            this.MouseUp += new MouseEventHandler(HandleMouseUp);
            this.MouseDown += new MouseEventHandler(HandleMouseDown);
            this.MouseWheel += new MouseEventHandler(HandleMouseWheel);

            drawTimer = new Timer();
            drawTimer.Tick += new EventHandler(drawTimer_Tick);
            drawTimer.Interval = 15;
            drawTimer.Start();
        }

        void drawTimer_Tick(object sender, EventArgs e)
        {
            this.Invalidate();
        }

        #region Mouse Control

        void HandleMouseWheel(object sender, MouseEventArgs e)
        {
            Ray pointer = viewport.GetPointerRay(e.Location);
            viewport.Zoom(pointer, -e.Delta / 120);
            this.Invalidate();
        }

        private Point mouseDownLocation;
        private bool mouseRDown = false;
        void HandleMouseDown(object sender, MouseEventArgs e)
        {
            if (clickedObject != null)
            {
                // If something's already clicked, don't allow selecting other items.
                // This will force a mouse up in the client area.
                return;
            }

            mouseDownLocation = e.Location;
            
            Ray pointer = viewport.GetPointerRay(e.Location);
            

            if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                mouseRDown = true;
                viewport.BeginRotate();
                return;
            }
            else if (e.Button != System.Windows.Forms.MouseButtons.Left)
            {
                return;
            }

            
            clickedObject = GetClosestObject(pointer) as IClickable3D;
            if (clickedObject != null)
            {
                clickedObject.MouseDown(pointer);
            }

            this.Invalidate();
        }

        public Ray GetPointerRay(Point mouseLocation)
        {
            return viewport.GetPointerRay(mouseLocation);
        }

        private object GetClosestObject(Ray pointer)
        {
            object closestObject = null;
            float closest = float.PositiveInfinity;
            foreach (var o in objects)
            {
                if (o is IClickable3D)
                {
                    var clickable = o as IClickable3D;
                    float distance = clickable.DistanceToObject(pointer);
                    if (distance < closest)
                    {
                        closestObject = o;
                        closest = distance;
                    }
                }
            }

            if (FilterSelectable(closestObject))
            {
                IClickable3D closestClickable = closestObject as IClickable3D;
                if (closestClickable != null)
                {
                    closestClickable.MouseHover(); // TODO: find a better way to handle hover indication...
                }
                return closestObject;
            }
            return viewport as IClickable3D;
        }

        private bool FilterSelectable(Object selecting)
        {
            if (selecting == null)
            {
                return false;
            }
            //if (selecting is TabsGUI)
            //{
            //    return true;
            //}
            //return false;
            return true;
        }

        void HandleMouseUp(object sender, MouseEventArgs e)
        {
            // Display a context menu?
            if (mouseRDown)
            {
                Console.WriteLine("Mouse Right Unclick");
                // Right-click down mouse move is rotate UI.
                // If UI was rotated, don't do a context menu.
                if (mouseDownLocation == e.Location)
                {
                    Console.WriteLine("Mouse Right UnclickUnclick");
                    if (clickedObject != null && clickedObject is TriangleMeshGUI)
                    {
                        System.Windows.Forms.ContextMenu menu = new ContextMenu();
                        var item = new MenuItem("Delete", new EventHandler(objectDeleteClicked));
                        item.Tag = clickedObject;
                        menu.MenuItems.Add(item);

                        item = new MenuItem("Set as Bottom Face", new EventHandler(objectSetAsBottomFaceClicked));
                        item.Tag = clickedObject;
                        menu.MenuItems.Add(item);

                        menu.Show(this, e.Location);
                    }
                    else // More generic case - TODO: put TriangleMeshGUI into this generic case.
                    {
                        Ray pointer = viewport.GetPointerRay(e.Location);
                        IRightClickable3D rightClickedObject = GetClosestObject(pointer) as IRightClickable3D;
                        if (rightClickedObject != null)
                        {
                            string[] items = rightClickedObject.MouseRightClick(pointer);

                            System.Windows.Forms.ContextMenu menu = new ContextMenu();
                            foreach (string s in items)
                            {
                                string text = s;
                                string displayName = s;
                                object tag = rightClickedObject;
                                if (text.Contains("<ACTION="))
                                {
                                    string[] result = text.Split('<');
                                    string[] fields = result[1].Trim(new char[] { ' ', '>' }).Split(',');
                                    displayName = result[0];
                                    RightClickWithContext context = new RightClickWithContext();
                                    context.target = rightClickedObject;
                                    context.fields = fields;
                                    context.original = displayName;
                                    context.mouseEventArgs = e;
                                    tag = context;
                                }
                                var item = new MenuItem(displayName, new EventHandler(objectRightClicked));
                                item.Tag = tag;
                                menu.MenuItems.Add(item);
                            }
                            menu.Show(this, e.Location);
                        }
                    }
                }
            }


            mouseRDown = false;
            if (clickedObject != null)
            {
                Ray pointer = viewport.GetPointerRay(e.Location);
                clickedObject.MouseUp(pointer);
                clickedObject = null;
            }
        }


        private class RightClickWithContext
        {
            public IRightClickable3D target;
            public string original;
            public string[] fields;
            public MouseEventArgs mouseEventArgs;
        }

        private void objectRightClicked(object sender, EventArgs e)
        {
            var item = sender as MenuItem;
            if (item != null)
            {
                IRightClickable3D rightClickedObject = item.Tag as IRightClickable3D;
                if (rightClickedObject != null)
                {
                    rightClickedObject.MouseRightClickSelect(item.Text);
                }
                RightClickWithContext rightClickWithContext = item.Tag as RightClickWithContext;
                if (rightClickWithContext != null)
                {
                    double start = 0.0f;
                    foreach (string s in rightClickWithContext.fields)
                    {
                        if (s.Contains("VALUE="))
                        {
                            double.TryParse(s.Remove(0, 6), out start);
                        }
                    }
                    start *= 25.4f; // Translate to millimeters
                    
                    ModalFloatInput m = new ModalFloatInput("Change Board Thickness", "Board Thickness (mm):", start);
                    m.Location = PointToScreen(rightClickWithContext.mouseEventArgs.Location);
                    m.ShowDialog();
                    
                    if (m.Confirmed)
                    {
                        double value = m.Result / 25.4f; // And back to inches
                        rightClickWithContext.target.MouseRightClickSelect(rightClickWithContext.original + 
                            value.ToString());
                    }
                }
            }
        }

        private void objectSetAsBottomFaceClicked(object sender, EventArgs e)
        {
            var item = sender as MenuItem;
            if (item != null)
            {
                IClickable3D toModify = item.Tag as IClickable3D;
                var triangleMesh = item.Tag as TriangleMeshGUI;
                if (triangleMesh != null)
                {
                    Ray pointer = viewport.GetPointerRay(mouseDownLocation);

                    // If we rotate the object, tabs will become invalid.
                    foreach (var tab in triangleMesh.Tabs)
                    {
                        objects.Remove(tab);
                    }

                    triangleMesh.SetClickedFaceAsBottom(pointer);

                    foreach (var tab in triangleMesh.Tabs)
                    {
                        objects.Add(tab);
                    }
                }
            }
        }

        void objectDeleteClicked(object sender, EventArgs e)
        {
            var item = sender as MenuItem;
            if (item != null)
            {
                IClickable3D toRemove = item.Tag as IClickable3D;
                if (toRemove != null)
                {
                    if (toRemove is TriangleMeshGUI)
                    {
                        foreach (var tab in (toRemove as TriangleMeshGUI).Tabs)
                        {
                            objects.RemoveAll(o => o == tab);
                        }
                    }
                    objects.RemoveAll(o => o == toRemove);
                }
            }
        }

        void HandleMouseLeave(object sender, EventArgs e)
        {
        }

        void HandleMouseEnter(object sender, EventArgs e)
        {
            this.Focus();
        }

        IClickable3D hoveredObject = null;
        void HandleMouseMove(object sender, MouseEventArgs e)
        {
            hoveredObject = null;
            if (mouseRDown)
            {
                Point mouseDelta = new Point(e.Location.X - mouseDownLocation.X, e.Location.Y - mouseDownLocation.Y);
                viewport.ViewportRotate(mouseDelta.X, mouseDelta.Y);
            }
            else
            {
                Ray pointer = viewport.GetPointerRay(e.Location);
                if (clickedObject != null)
                {
                    clickedObject.MouseMove(pointer);
                }
                else
                {
                    IClickable3D hovered = GetClosestObject(pointer) as IClickable3D;
                    if (hovered != null)
                    {
                        hoveredObject = hovered;
                    }
                }
            }
            this.Invalidate();
        }

        #endregion

        #region Drawing

        void HandleControlResize(object sender, EventArgs e)
        {
            // TODO: move viewport setup code here, only call when changing the viewport size.
        }
        
        public Color ClearColor
        {
            get { return clearColor; }
            set
            {
                clearColor = value;

                if (!this.DesignMode)
                {
                    MakeCurrent();
                    GL.ClearColor(clearColor);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (!this.DesignMode)
            {
                MakeCurrent();
                GL.Viewport(ClientRectangle);
                //float aspect = this.ClientSize.Width / (float)this.ClientSize.Height;

                GL.MatrixMode(MatrixMode.Projection);
                GL.LoadIdentity();

                // 3D Setup
                Matrix4 projection = viewport.ProjectionMatrix;
                GL.LoadMatrix(ref projection);
                GL.MatrixMode(MatrixMode.Modelview);

                GL.Enable(EnableCap.Blend); // glEnable(GL_BLEND);
                GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha); // glBlendFunc(GL_SRC_ALPHA, GL_ONE_MINUS_SRC_ALPHA);

                GL.ClearColor(Color.White);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                GL.LoadIdentity();

                GL.Enable(EnableCap.Lighting);
                GL.Enable(EnableCap.ColorMaterial);
                GL.Light(LightName.Light0, LightParameter.Position, new Vector4(0, 0, 1, 0));
                GL.Light(LightName.Light0, LightParameter.Diffuse, new Vector4 (.8f, .8f, .8f, 1));
                GL.Light(LightName.Light0, LightParameter.Ambient, new Vector4(.3f, .3f, .3f, 1));
                //GL.Light(LightName.Light0, LightParameter.Specular, new Vector4(1.0f, 1.0f, 1.0f, 1));
                GL.Enable(EnableCap.Light0);
                GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);
                //GL.ColorMaterial(MaterialFace.Front, ColorMaterialParameter.AmbientAndDiffuse);
                GL.Enable(EnableCap.ColorMaterial);

                GL.LoadMatrix(ref viewport.viewMatrix);
                GL.Normal3(new Vector3(0, 0, 1));
                GL.Enable(EnableCap.Normalize);
                
                GL.DepthMask(true);
                GL.Enable(EnableCap.DepthTest);
                GL.DepthFunc(DepthFunction.Lequal);
                //GL.Enable(EnableCap.CullFace);
                //GL.CullFace(CullFaceMode.Back);
                GL.PolygonOffset(1.0f, 2.0f);
                GL.Enable(EnableCap.PolygonOffsetFill);
                
                viewport.Draw();

                //GL.PushMatrix();
                foreach (object o in objects)
                {
                    if (o is IOpenGLDrawable)
                    {
                        var drawable = o as IOpenGLDrawable;
                        drawable.Draw();
                    }
                }
                //GL.PopMatrix();

                SwapBuffers();
            }
        }

        private void UserControl1_Load(object sender, EventArgs e)
        {
            this.HandleControlResize(sender, e);
        }

        #endregion

        List<Object> objects = new List<object>();
        internal void AddObject(Object o)
        {
            if (o != null)
            {
                objects.Add(o);
                if (o is Router.Router)
                {
                    router = o as Router.Router;
                }
            }
        }

        internal List<object> GetObjects()
        {
            // TODO: return the viewport as an object?
            return objects;
        }

        internal void DeleteSelectedObjected()
        {
            if (clickedObject != null)
            {
                objects.RemoveAll(c => c == clickedObject);
            }
            clickedObject = null;
        }
    }
}
