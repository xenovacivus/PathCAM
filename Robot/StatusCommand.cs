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
    public class StatusCommand : IRobotCommand
    {
        private Vector3 currentPosition = new Vector3(0, 0, 0);
        private bool data_valid = false;
        private bool is_moving = false;
        private int locations;

        // TODO: find a better way to handle the time
        public float time;

        public StatusCommand()
        {
        }

        protected virtual byte CommandCode
        {
            get { return 0x77; }
        }
        
        public int Locations
        {
            get { return locations; }
        }

        public Vector3 CurrentPosition
        {
            get { return currentPosition; }
        }

        internal override bool IsDataValid()
        {
            return data_valid;
        }

        internal bool IsMoving()
        {
            return is_moving;
        }

        internal override byte[] GenerateCommand()
        {
            return new byte[] { CommandCode };
        }

        bool paused = false;
        bool pausing = false;
        bool steppers_enabled = false;
        public bool Paused { get { return paused; } }
        public bool Pausing { get { return pausing; } }
        public bool SteppersEnabled { get { return steppers_enabled; } }

        internal override void ProcessResponse(byte[] data)
        {
            if (data.Length <= 18)
            {
                data_valid = false;
                return;
            }
            data_valid = (data[0] == CommandCode);

            List<byte> data_list = new List<byte>(data);

            time = ((float)DataConverter.IntFromBytes(data_list.GetRange(15, 4))) / 10.0f;
            currentPosition = new Vector3(
                DataConverter.FloatFromBytes(data_list.GetRange(1, 4)),
                DataConverter.FloatFromBytes(data_list.GetRange(5, 4)),
                DataConverter.FloatFromBytes(data_list.GetRange(9, 4)));

            byte status_bits = data[13];
            paused = (status_bits & 0x01) > 0;
            pausing = (status_bits & 0x02) > 0;
            steppers_enabled = (status_bits & 0x04) > 0;

            is_moving = true;// x_moving | y_moving | z_moving;
            
            locations = (int)(data[14]);

            //Console.WriteLine("{0}, {1}, {2}, {3}, l = {4}, paused = {5}, pausing = {6}, resuming = {7}", time, currentPosition.X, currentPosition.Y, currentPosition.Z, locations, paused, pausing, resuming);
        }
 
    }
}
