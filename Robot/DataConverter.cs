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
using System.Runtime.InteropServices;
using System.Text;

namespace Robot
{
    /// <summary>
    /// Converts larger types to bytes.
    /// All conversions are done little-endian style, with
    /// the least significant byte first and most significant
    /// byte last in the list of bytes.
    /// </summary>
    class DataConverter
    {
        [StructLayout(LayoutKind.Explicit)]
        struct int_or_float
        {
            [FieldOffset(0)]
            public float FloatValue;

            [FieldOffset(0)]
            public int IntValue;
        }

        /// <summary>
        /// Convert a sequence of 4 bytes to a floating point value.
        /// The sequence is expected to be in Little Endian format, I.E
        /// least significant byte first.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static float FloatFromBytes(IEnumerable<byte> bytes)
        {
            return new int_or_float { IntValue = IntFromBytes(bytes) }.FloatValue;
        }

        /// <summary>
        /// Convert a float to 4 bytes.  The first byte is the least
        /// significant part of the float.
        /// </summary>
        /// <param name="f"></param>
        /// <returns></returns>
        public static byte[] BytesFromFloat(float f)
        {
            return BytesFromInt(new int_or_float { FloatValue = f }.IntValue);
        }

        /// <summary>
        /// Convert a short to 2 bytes, least significant part of the
        /// short will be byte[0] and most significant will be byte[1].
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static byte[] BytesFromShort(short s)
        {
            byte[] bytes = new byte[2];
            for (int i = 0; i < 2; i++)
            {
                bytes[i] = (byte)((s >> (i * 8)) & 0xFF);
            }
            return bytes;
        }

        /// <summary>
        /// Convert an int to 2 bytes, least significant part of the
        /// int will be in byte[0] and the most significant part will 
        /// be in byte[3].
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte[] BytesFromInt(int value)
        {
            byte[] bytes = new byte[4];
            for (int i = 0; i < 4; i++)
            {
                bytes[i] = (byte)((value >> (i * 8)) & 0xFF);
            }
            return bytes;
        }

        /// <summary>
        /// Convert 4 bytes to an integer.  The first byte should
        /// be the least significant, and the last the most significant.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static int IntFromBytes(IEnumerable<byte> bytes)
        {
            int raw = 0;
            int shifter = 0;
            foreach (byte b in bytes)
            {
                raw |= ((int)b) << shifter;
                shifter += 8;
            }
            return raw;
        }
    }
}
