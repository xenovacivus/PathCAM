using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Geometry;
using OpenTK;

namespace GUI
{
    interface IRightClickable3D
    {
        /// <summary>
        /// Provide a list of strings for a right-click context menu
        /// </summary>
        /// <param name="pointer"></param>
        /// <returns></returns>
        string[] MouseRightClick(Ray pointer);
        void MouseRightClickSelect(string result);
    }
}
