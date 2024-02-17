/*
 * PathCAM - Toolpath generation software for CNC manufacturing machines
 * Copyright (C) 2013  Benjamin R. Porter https://github.com/xenovacivus
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see [http://www.gnu.org/licenses/].
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Router;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Drawing;
using Robot;
using Commands;
using System.ComponentModel;


namespace GUI
{
    public class RouterGUI : Router.Router, IOpenGLDrawable
    {

        public RouterGUI() : base()
        {
        }

        void IOpenGLDrawable.Draw()
        {
            try
            {
                var commands = base.GetCommands();
                // Draw cut paths
                GL.Disable(EnableCap.Lighting);
                GL.Color3(Color.Blue);
                GL.Begin(PrimitiveType.LineStrip);
                for (int i = 0; i < commands.Count(); i++)
                {
                    if (commands[i] is MoveTool)
                    {
                        MoveTool m = commands[i] as MoveTool;
                        Vector3 finalPosition = m.Target;
                        GL.Vertex3(finalPosition);
                    }
                }
                GL.End();

                GL.PointSize(4);
                GL.Color3(Color.Red);
                GL.Begin(PrimitiveType.Points);
                for (int i = 0; i < commands.Count(); i++)
                {
                    if (commands[i] is MoveTool)
                    {
                        MoveTool m = commands[i] as MoveTool;
                        Vector3 finalPosition = m.Target;
                        GL.Vertex3(finalPosition);
                    }
                }
                GL.End();
                GL.PointSize(1);
                GL.Enable(EnableCap.Lighting);
            }
            catch (Exception)
            {
            }
        }
    }
}
