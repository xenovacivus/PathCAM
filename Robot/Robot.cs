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
using Serial;
using System.Timers;
using OpenTK;
using Commands;

namespace Robot
{
    public class Robot
    {
        private Queue<ICommand> commands = new Queue<ICommand>();
        public EventHandler onRobotStatusChange;

        private IRobotCommand currentCommand = null;
        private SerialPortWrapper serial;
        private Timer t;
        private int timeoutCountdown = 0;
        private Vector3 currentPosition = new Vector3(0, 0, 0);
        private Vector3 lastPosition = new Vector3(0, 0, 0);
        private bool lastPositionKnown = false;

        private const float minSpeed = 0.01f; // All speeds are clamped to this.
        private float maxZSpeed = 30;
        private float maxCutSpeed = 100.0f;
        private float maxRapidSpeed = 250.0f;

        bool isSpindleSpinning = false;
        bool sendResumeCommand = false;
        bool sendPauseCommand = false;
        bool sendCancelCommand = false;
        bool sendEnableStepperCommand = false;
        bool sendDisableStepperCommand = false;

        bool sendZeroCommand = false;


        public float z_offset = 0;



        public void Zero()
        {
            sendZeroCommand = true;
            // TODO: combine with logic for pause commands
        }

        public void SendPauseCommand()
        {
            sendPauseCommand = true;
            sendResumeCommand = false;
        }

        public void SendResumeCommand()
        {
            sendResumeCommand = true;
            sendPauseCommand = false;
        }

        public void CancelPendingCommands()
        {
            sendCancelCommand = true;
            lock (commands)
            {
                commands.Clear();
            }
        }

        public void EnableMotors()
        {
            sendDisableStepperCommand = false;
            sendEnableStepperCommand = true;
        }

        public void DisableMotors()
        {
            sendEnableStepperCommand = false;
            sendDisableStepperCommand = true;
        }

        public Vector3 GetPosition()
        {
            return currentPosition - new Vector3(0, 0, z_offset);
        }

        public Vector3 GetPhysicalPosition()
        {
            return currentPosition;
        }

        public void AddCommand(ICommand command)
        {
            commands.Enqueue(command);
        }


        public float MaxZSpeed
        {
            get { return maxZSpeed; }
            set { maxZSpeed = Math.Max(value, minSpeed); }
        }

        public float MaxCutSpeed
        {
            get { return maxCutSpeed; }
            set { maxCutSpeed = Math.Max(value, minSpeed); }
        }

        public float MaxRapidSpeed
        {
            get { return maxRapidSpeed; }
            set { maxRapidSpeed = Math.Max(value, minSpeed); }
        }


        ICommandGenerator commandGenerator;
        public Robot(SerialPortWrapper serial)
        {
            commandGenerator = null;
            this.serial = serial;
            serial.newDataAvailable += new SerialPortWrapper.newDataAvailableDelegate(NewDataAvailable);
            t = new Timer();
            t.Interval = 50;
            t.Start();
            t.Elapsed += new ElapsedEventHandler(t_Elapsed);
        }

        /// <summary>
        /// Special command that can detect the type of robot connected and
        /// create the proper ICommandGenerator.
        /// </summary>
        private class RobotDetectionCommand : IRobotCommand
        {
            List<byte> accumulator = new List<byte>();
            IRobotCommand binaryStatusCommand;
            //private StringBuilder accumulator = new StringBuilder();
            private ICommandGenerator commandGenerator = null;
            internal override byte[] GenerateCommand()
            {
                // This command works both as a status command for the binary format
                // And as a reset command for the Grbl commands.
                return new byte[] { 0xCA, 0x21, 0x02, 0x77, 0x4D, 0x18 };
            }

            internal override bool ProcessResponse(byte data)
            {
                if (data == 0xCA) // Only the binary robot will send this character (it's the packet start character)
                {
                    // Drop anything earlier from the accumulator
                    accumulator.Clear();
                    Console.WriteLine("Found 0xCA start byte, looks like the packetized binary protocol");
                    commandGenerator = new PacketizedCommandGenerator();
                    binaryStatusCommand = commandGenerator.GenerateStatusCommand();
                }

                accumulator.Add(data);
                if (binaryStatusCommand != null)
                {
                    // The response will be a binary status command, just forward the data and wait until it's good.
                    return binaryStatusCommand.ProcessResponse(data);
                }
                else
                {
                    // Look for an ASCII communicating robot (like GRBL)
                    var s = System.Text.Encoding.ASCII.GetString(accumulator.ToArray());

                    if (s.EndsWith("\n", StringComparison.OrdinalIgnoreCase))
                    {
                        if (s.StartsWith("Grbl ") && s.Length >= 9)
                        {
                            var version = s.Substring(5, 3);
                            float version_float = 0.0f;
                            if (float.TryParse(version, out version_float) && version_float >= 0.8f)
                            {
                                Console.WriteLine("Compatible Grbl type robot found: " + s.Substring(0, 9));
                                commandGenerator = new GrblCommandGenerator();
                                return true;
                            }
                        }
                        else
                        {
                            // Seems like a GRBL type robot, but the start of the string wasn't right.  Maybe some garbage
                            // or an extra \r\n, clear it out and wait for more.
                            accumulator.Clear();
                        }
                    }
                }
                return false;
            }

            internal string DumpData()
            {
                // Create a string containing the bytes received in both hex and characters
                var s = System.Text.Encoding.ASCII.GetString(accumulator.ToArray());
                var retString = "{ " + string.Join(" ", accumulator.Select(b => string.Format("{0:X2}", b)).ToArray()) + " } " + s.TrimEnd(new char [] {'\r', '\n'});
                return retString;
            }

            internal ICommandGenerator GetCommandGenerator()
            {
                return commandGenerator;
            }
        }

        const int timeout_ms = 1000; // Set to a general command response timeout value
        const int robot_detection_timeout = 5000; // Set to the worst case robot timeout

        // Set to elapse every 50 milliseconds
        void t_Elapsed(object sender, ElapsedEventArgs e)
        {
            t.Stop();
            lock (thisLock)
            {
                // Do we have a connection to the robot open?
                if (serial == null || !serial.IsOpen)
                {
                    // No connection, do nothing, indicate that we know nothing about the robot.
                    lastPositionKnown = false;
                    commandGenerator = null;
                    currentCommand = null;
                }
                else
                {
                    // We have a serial port open
                    if (currentCommand == null)
                    {
                        // Must have just connected to a robot - send out a command to figure out what type of robot we have.
                        currentCommand = new RobotDetectionCommand();
                        serial.Transmit(currentCommand.GenerateCommand());
                        timeoutCountdown = robot_detection_timeout;
                    }
                    else if (timeoutCountdown < 0)
                    {
                        // Expected reply not received within timeout, assume command was lost.
                        Console.WriteLine("Device Timeout!");

                        if (currentCommand != null && currentCommand is RobotDetectionCommand)
                        {
                            // We tried to connect to a robot, but didn't get the expected response.
                            Console.WriteLine("Unexpected Response from robot detection: " + (currentCommand as RobotDetectionCommand).DumpData());
                        }
                        currentCommand = null;
                    }
                    else
                    {
                        timeoutCountdown -= 50; // ticks every 50 milliseconds
                    }
                }
            }
            t.Start();
        }

        private void ReceiveDataError(byte err)
        {
            Console.WriteLine("Data Error: " + err);
        }

        Object thisLock = new Object();
        private void NewDataAvailable(byte data)
        {
            t.Stop(); // debugging
            lock (thisLock)
            {
                timeoutCountdown = timeout_ms;
                if (currentCommand == null)
                {
                    Console.WriteLine("Error: Received data, but no command was sent!");
                    Console.Write(data.ToString("x") + ", ");
                    Console.WriteLine();
                }
                else
                {
                    if (currentCommand.ProcessResponse(data))
                    {
                        bool canAcceptMoveCommand = false;
                        if (currentCommand is IRobotCommandWithStatus)
                        {
                            IRobotCommandWithStatus status = currentCommand as IRobotCommandWithStatus;
                            currentPosition = status.CurrentPosition;
                            canAcceptMoveCommand = status.CanAcceptMoveCommand;
                            if (this.lastPositionKnown == false)
                            {
                                lastPosition = currentPosition;
                                lastPositionKnown = true;
                            }
                            if (onRobotStatusChange != null)
                            {
                                onRobotStatusChange(status, EventArgs.Empty);
                            }
                        }
                        else if (currentCommand is RobotDetectionCommand)
                        {
                            RobotDetectionCommand r = currentCommand as RobotDetectionCommand;
                            commandGenerator = r.GetCommandGenerator();
                        }

                        currentCommand = GetNextCommand(canAcceptMoveCommand);
                        serial.Transmit(currentCommand.GenerateCommand());
                    }
                }
            }
            t.Start(); // debugging
        }

        private IRobotCommand GetNextCommand(bool canAcceptMoveCommand)
        {
            currentCommand = null;
            if (commandGenerator == null)
            {
                return null;
            }
            
            
            // If the command generator has setup commands, send them out first.
            currentCommand = commandGenerator.GetNextSetupCommand();
            if (currentCommand != null)
            {
                return currentCommand;
            }

            if (sendZeroCommand)
            {
                currentCommand = commandGenerator.GenerateZeroCommand();
                sendZeroCommand = false;
            }
            if (sendCancelCommand)
            {
                currentCommand = commandGenerator.GenerateCancelCommand();
                sendCancelCommand = false;
            }
            else if (sendResumeCommand)
            {
                currentCommand = commandGenerator.GenerateResumeCommand();
                sendResumeCommand = false;
            }
            else if (sendPauseCommand)
            {
                currentCommand = commandGenerator.GeneratePauseCommand();
                sendPauseCommand = false;
            }
            else if (sendEnableStepperCommand)
            {
                currentCommand = commandGenerator.GenerateStepperEnableCommand();
                sendEnableStepperCommand = false;
            }
            else if (sendDisableStepperCommand)
            {
                currentCommand = commandGenerator.GenerateStepperDisableCommand();
                sendDisableStepperCommand = false;
            }
            else if (canAcceptMoveCommand && lastPositionKnown)
            {
                lock (commands)
                {
                    while (currentCommand == null && commands.Count > 0)
                    {
                        ICommand command = commands.Peek();
                        if (command is MoveTool)
                        {
                            var moveTool = command as MoveTool;
                            // If this is a cutting command and the spindle isn't going, spin it up.
                            if (moveTool.Speed == MoveTool.SpeedType.Cutting && !isSpindleSpinning)
                            {
                                currentCommand = commandGenerator.GenerateSpindleEnableCommand(5000.0f);
                                isSpindleSpinning = true;
                            }
                            else
                            {
                                currentCommand = CreateRobotCommand(moveTool, commandGenerator);
                                commands.Dequeue();
                            }
                        }
                    }
                    if (commands.Count == 0 && currentCommand == null && isSpindleSpinning)
                    {
                        currentCommand = commandGenerator.GenerateSpindleDisableCommand();
                        isSpindleSpinning = false;
                    }
                }
            }
            if (currentCommand == null)
            {
                currentCommand = commandGenerator.GenerateStatusCommand();
            }


            return currentCommand;
        }
        
        /// <summary>
        /// Create a robot command from a MoveTool command.  Adjusts for robot specific
        /// max speeds.  May return null if there is no effective move.
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        private IRobotCommand CreateRobotCommand(MoveTool m, ICommandGenerator c)
        {
            var p = m.Target;
            p.Z += z_offset;

            float inches_per_minute = m.Speed == MoveTool.SpeedType.Cutting ? MaxCutSpeed : MaxRapidSpeed;

            Vector3 delta = lastPosition - p;
            lastPosition = p;

            if (delta.Length > 0)
            {
                if (Math.Abs(delta.Z) > 0.0001f)
                {
                    inches_per_minute = Math.Min(MaxZSpeed, inches_per_minute);
                }
                return c.GenerateMoveCommand(p, inches_per_minute / 60.0f);
            }
            else
            {
                Console.WriteLine("Ignoring command with length of 0");
                return null;
            }
        }

    }
}
