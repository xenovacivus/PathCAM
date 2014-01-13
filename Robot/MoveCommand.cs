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
using OpenTK;

namespace Robot
{
    internal class MoveCommand : StatusCommand
    {
        protected override byte CommandCode
        {
            get { return 0x32; }
        }

        private UInt16 thousandths_per_second;
        private Vector3 toLocation = new Vector3(0, 0, 0);

        public MoveCommand(Vector3 location, float inches_per_second) : base()
        {
            toLocation = location;
            this.thousandths_per_second = (UInt16)(inches_per_second * 1000);
        }

        internal override byte[] GenerateCommand()
        {
            List<byte> command = new List<byte>();
            command.Add(CommandCode);
            command.AddRange(DataConverter.BytesFromShort((short)thousandths_per_second));
            command.AddRange(DataConverter.BytesFromFloat(toLocation.X));
            command.AddRange(DataConverter.BytesFromFloat(toLocation.Y));
            command.AddRange(DataConverter.BytesFromFloat(toLocation.Z));
            return command.ToArray();
        }
    }
}
