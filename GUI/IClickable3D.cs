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
using System.Threading.Tasks;
using Geometry;
using OpenTK;

namespace GUI
{
    public interface IClickable3D
    {
        void MouseDown(Ray pointer);
        void MouseUp(Ray pointer);
        void MouseMove(Ray pointer);

        /// <summary>
        /// Mouse is hovering on this object.
        /// The ray will be the same as the last
        /// call to DistanceToObject.
        /// </summary>
        void MouseHover();
        float DistanceToObject(Ray pointer);
    }
}
