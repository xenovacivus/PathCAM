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
    public abstract class IRobotCommand
    {
        internal abstract byte[] GenerateCommand();
        internal abstract bool ProcessResponse(byte data);
        //internal abstract bool IsDataValid();
    }

    public abstract class IRobotCommandWithStatus : IRobotCommand
    {
        // Add in some properties for the current robot status
        public abstract bool Idle { get; }
        public abstract bool Paused { get; }
        public abstract bool Pausing { get; }
        public abstract bool SteppersEnabled { get; }
        public abstract Vector3 CurrentPosition { get; }
        public abstract float Time { get; }
        public abstract bool CanAcceptMoveCommand { get; }
        public abstract bool IsValid { get; }
    }
}
