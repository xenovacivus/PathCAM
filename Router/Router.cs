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
using System.Drawing;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.ComponentModel;
using Commands;
using Geometry;

namespace Router
{
    public class Router
    {
        private List<ICommand> commands;
        private Vector3 finalPosition = new Vector3(0, 0, 0);

        private float toolDiameter = 0.1875f;
        private float move_height = 0.550f; // How high above the surface to move the router
        private float max_cut_depth = .250f; // Maximum cut depth in inches
        private float lastPassHeight = -.020f; // Height of the last rout

        public Router()
        {
            commands = new List<ICommand>();
        }

        public List<ICommand> GetCommands()
        {
            return commands;
        }

        public void AddCommand(ICommand r)
        {
            if (r is MoveTool)
            {
                finalPosition = (r as MoveTool).Target;
            }
            commands.Add(r);
        }

        public void ClearCommands()
        {
            commands.Clear();
        }

        public float LastPassHeight
        {
            get { return lastPassHeight; }
            set { lastPassHeight = value; }
        }

        public float ToolDiameter
        {
            get { return toolDiameter; }
            set { if (value > 0.0f) { toolDiameter = value; } }
        }
        
        public float MoveHeight
        {
            get { return move_height; }
            set { move_height = value; }
        }

        public float MaxCutDepth
        {
            get { return max_cut_depth; }
            set { max_cut_depth = value; }
        }

        public void RoutPath(LineStrip line, bool backwards, Vector3 offset)
        {
            bool first = true;
            
            foreach (Vector3 point in line.Vertices)
            {
                // TODO: Pick some unit and stick with it!  Inches would be fine.
                Vector3 pointOffset = point + offset;
                
                MoveTool m = new MoveTool(pointOffset, MoveTool.SpeedType.Cutting);
                if (first)
                {
                    first = false;

                    if ((finalPosition.Xy - pointOffset.Xy).Length > .0001)
                    {
                        // Need to move the router up, over to new position, then down again.
                        MoveTool m1 = new MoveTool(new Vector3(finalPosition.X, finalPosition.Y, move_height), MoveTool.SpeedType.Rapid);
                        MoveTool m2 = new MoveTool(new Vector3(m.Target.X, m.Target.Y, move_height), MoveTool.SpeedType.Rapid);
                        AddCommand(m1);
                        AddCommand(m2);
                    }
                }
                AddCommand(m);
            }
        }

        /// <summary>
        /// Safely move to (0, 0, move_height)
        /// </summary>
        public void Complete()
        {
            if (finalPosition.Z < move_height)
            {
                AddCommand(new MoveTool(new Vector3(finalPosition.X, finalPosition.Y, move_height), MoveTool.SpeedType.Rapid));
            }
            else
            {
                AddCommand(new MoveTool(new Vector3(0, 0, finalPosition.Z), MoveTool.SpeedType.Rapid));
            }
            AddCommand(new MoveTool(new Vector3(0, 0, move_height), MoveTool.SpeedType.Rapid));
        }
    }
}
