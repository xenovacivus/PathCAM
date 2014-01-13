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

namespace Robot
{
    internal abstract class SingleByteStatusCommand : StatusCommand
    {
        internal override byte[] GenerateCommand()
        {
            return new byte[] { CommandCode };
        }
    }

    internal class StepperDisableCommand : SingleByteStatusCommand
    {
        protected override byte CommandCode { get { return 0x14; } }
        public StepperDisableCommand() : base() { }
    }

    internal class StepperEnableCommand : SingleByteStatusCommand
    {
        protected override byte CommandCode { get { return 0x15; } }
        public StepperEnableCommand() : base() { }
    }

    internal class CancelCommand : SingleByteStatusCommand
    {
        protected override byte CommandCode { get { return 0x13; } }
        public CancelCommand() : base() { }
    }

    internal class PauseCommand : SingleByteStatusCommand
    {
        protected override byte CommandCode { get { return 0x11; } }
        public PauseCommand() : base() { }
    }
    
    internal class ResumeCommand : SingleByteStatusCommand
    {
        protected override byte CommandCode { get { return 0x12; } }
        public ResumeCommand() : base() { }
    }

    internal class ResetCommand : SingleByteStatusCommand
    {
        protected override byte CommandCode { get { return 0x88; } }
        public ResetCommand() : base() { }
    }
}
