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
using System.Diagnostics;

namespace Robot
{
    public class Robot
    {
        private Queue<ICommand> commands = new Queue<ICommand>();
        public EventHandler onRobotStatusChange;

        //private IRobotCommand currentCommand = null;
        private SerialPortWrapper serial;
        private Timer t;
        private int elapsedCounter = 0;
        //private Vector3 currentPosition = new Vector3(0, 0, 0);
        //private Vector3 lastPosition = new Vector3(0, 0, 0);
        //private bool lastPositionKnown = false;

        private const float minSpeed = 0.01f; // All speeds are clamped to this.
        private float maxZSpeed = 30.0f * 60.0f / 25.4f;
        private float maxCutSpeed = 30.0f * 60.0f / 25.4f;
        private float maxRapidSpeed = 100.0f * 60.0f / 25.4f;

        public float z_offset = 0;

        private IMachine machine = null;

        public void Zero()
        {
            if (machine != null)
            {
                machine.Zero();
            }
        }

        public class BasicStatus : IRobotCommandWithStatus
        {
            public bool idle = false;
            public bool paused = false;
            public bool pausing = false;
            public bool steppersEnabled = true;
            public Vector3 currentPosition = Vector3.Zero;

            public override bool Idle
            {
                get { return idle; }
            }
            public override bool Paused
            {
                get { return paused; }
            }

            public override bool Pausing
            {
                get { return pausing; }
            }

            public override bool SteppersEnabled
            {
                get { return steppersEnabled; }
            }

            public override Vector3 CurrentPosition
            {
                get { return currentPosition; }
            }

            public override float Time => throw new NotImplementedException();
            public override bool CanAcceptMoveCommand => throw new NotImplementedException();
            public override bool IsValid => throw new NotImplementedException();
            internal override byte[] GenerateCommand()
            {
                throw new NotImplementedException();
            }
            internal override bool ProcessResponse(byte data)
            {
                throw new NotImplementedException();
            }
        }
        BasicStatus currentStatus = new BasicStatus();

        public void SendPauseCommand()
        {
            if (machine != null)
            {
                machine.Pause();
                currentStatus.pausing = true;
                currentStatus.paused = false;
                onRobotStatusChange(currentStatus, null);
            }
        }

        public void SendResumeCommand()
        {
            if (machine != null)
            {
                machine.Resume();
                // leave it up to the machine status to move the state
            }
        }

        public void CancelPendingCommands()
        {
            if (machine != null)
            {
                // This will leave the machine stopped where it is.
                machine.ClearPendingCommands();
            }
            lock (commands)
            {
                commands.Clear();
            }
        }

        public void EnableMotors()
        {
            if (machine != null)
            {
                machine.EnableMotors();
            }
        }

        public void DisableMotors()
        {
            if (machine != null)
            {
                machine.DisableMotors();
            }
        }

        public Vector3 GetPosition()
        {
            return currentStatus.CurrentPosition - new Vector3(0, 0, z_offset);
        }

        public Vector3 GetPhysicalPosition()
        {
            return currentStatus.CurrentPosition;
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


        public Robot(SerialPortWrapper serial)
        {
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

                    if (s.EndsWith("\r\n", StringComparison.OrdinalIgnoreCase))
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
                var retString = "{ " + string.Join(" ", accumulator.Select(b => string.Format("{0:X2}", b)).ToArray()) + " } " + s.TrimEnd(new char[] { '\r', '\n' });
                return retString;
            }

            internal ICommandGenerator GetCommandGenerator()
            {
                return commandGenerator;
            }
        }

        const int timeout_ms = 1000;

        Stopwatch machineDetectionWatchdog = new Stopwatch();
        List<byte[]> statusCommands = new List<byte[]>()
        {
            System.Text.Encoding.ASCII.GetBytes("?"), // For GRBL
            System.Text.Encoding.ASCII.GetBytes("M115\n"), // For repetier, and maybe marlin?
        };
        int statusCommandIndex = 0;
        int attempts = 0;

        void t_Elapsed(object sender, ElapsedEventArgs e)
        {
            //t.Stop();
            lock (thisLock)
            {
                if (serial == null || !serial.IsOpen || elapsedCounter > timeout_ms)
                {
                    if (t.Interval != 100)
                    {
                        t.Interval = 100;
                    }
                    elapsedCounter = 0;
                    commands.Clear();
                    machine = null;
                    machineDetectionWatchdog.Stop();
                    statusCommandIndex = 0;
                    attempts = 0;
                    onRobotStatusChange(null, null);
                }
                else
                {
                    if (machine != null)
                    {
                        machineDetectionWatchdog.Stop();
                        if (t.Interval != 42)
                        {
                            t.Interval = 42;
                        }
                        // Periodically check for commands to generate (usually status commands).


                        if (machine.CanAcceptMove && commands.Count > 0)
                        {
                            // TODO: other commands that are important?
                            MoveTool move = commands.Dequeue() as MoveTool;
                            if (move != null)
                            {
                                float inches_per_minute = move.Speed == MoveTool.SpeedType.Cutting ? MaxCutSpeed : MaxRapidSpeed;
                                machine.AddMove(move.Target + new Vector3(0, 0, z_offset), inches_per_minute);
                            }
                        }

                        byte[] command = machine.GenerateNextCommand();
                        if (command.Length > 0)
                        {
                            //Console.WriteLine("Generated Periodic Command");
                            serial.Transmit(command);
                        }
                    }
                    else
                    {
                        if (!machineDetectionWatchdog.IsRunning || machineDetectionWatchdog.ElapsedMilliseconds > timeout_ms)
                        {
                            // Try to detect the machine.
                            // Send a ? to GRBL - they reply almost immediately.
                            dataBuffer.Length = 0;
                            serial.Transmit(statusCommands[statusCommandIndex]);
                            statusCommandIndex = (statusCommandIndex + 1) % statusCommands.Count;
                            if (statusCommandIndex == 0)
                            {
                                attempts++;
                            }
                            machineDetectionWatchdog.Reset();
                            machineDetectionWatchdog.Start();
                            if (attempts > 1)
                            {
                                // Looped through all possible robot detectors,
                                // nothing replied.  Disconnect.
                                machineDetectionWatchdog.Stop();
                                serial.Close();
                            }
                            //machine = new GrblMachine();
                            onRobotStatusChange(null, null);
                        }
                    }
                }
            }
            //t.Start();
        }

        private void ReceiveDataError(byte err)
        {
            Console.WriteLine("Data Error: " + err);
        }

        Object thisLock = new Object();

        //     private string ProcessGrblByte(byte b)
        //     {
        //         dataBuffer.Append((char)b);
        //
        //         var response = dataBuffer.ToString();
        //         if (response.EndsWith(">\r\n"))
        //             }
        // }
        public bool IsConnected
        {
            get { return machine != null; }
        }

        public string ConnectedMachineType
        {
            get
            {
                if (machine is GrblMachine)
                {
                    return "GRBL Machine";
                }
                else if (machine is RepetierMachine)
                {
                    return "Repetier?";
                }
                else if (machineDetectionWatchdog.IsRunning)
                {
                    return "Detecting...";
                }
                return "-";
            }
        }

        StringBuilder dataBuffer = new StringBuilder();
        private void NewDataAvailable(byte[] data)
        {
            //t.Stop();
            lock (thisLock)
            {
                if (machine != null)
                {
                    if (machine.ProcessByte(data))
                    {
                        if (machine.CanAcceptMove && commands.Count > 0)
                        {
                            // TODO: other commands that are important?
                            MoveTool move = commands.Dequeue() as MoveTool;
                            if (move != null)
                            {
                                float inches_per_minute = move.Speed == MoveTool.SpeedType.Cutting ? MaxCutSpeed : MaxRapidSpeed;
                                machine.AddMove(move.Target + new Vector3(0, 0, z_offset), inches_per_minute);
                            }
                        }

                        // Machine state has changed, maybe there are new commands.
                        while (true)
                        {
                            byte[] command = machine.GenerateNextCommand();
                            if (command.Length > 0)
                            {
                                serial.Transmit(command);
                            }
                            else
                            {
                                break;
                            }
                        }

                        currentStatus.currentPosition = machine.GetPosition();
                        
                        if (machine.IsPaused)
                        {
                            currentStatus.idle = false;
                            currentStatus.paused = true;
                            currentStatus.pausing = false;
                        }
                        else if (!currentStatus.pausing)
                        {
                            currentStatus.idle = false;
                            currentStatus.paused = false;
                        }
                        
                        if (!currentStatus.pausing && machine.IsIdle && commands.Count == 0)
                        {
                            currentStatus.idle = true;
                        }
                        onRobotStatusChange(currentStatus, null);
                    }
                }
                else
                {
                    string newData = System.Text.Encoding.ASCII.GetString(data);
                    dataBuffer.Append(newData);
                    if (newData.Contains('\n'))
                    {
                        string allData = dataBuffer.ToString();
                        List<string> lines = new List<string>(allData.Split('\n'));

                        // The last part is still being built.
                        dataBuffer = new StringBuilder(lines.Last());
                        lines.RemoveAt(lines.Count - 1);
                        foreach (string line in lines)
                        {
                            Console.WriteLine("Robot Detection: " + line.Trim());
                            // GRBL responds to '?' with something like this:
                            // <Idle|MPos:0.000,0.000,0.000|FS:0,0>
                            if (line.Contains("MPos"))
                            {
                                machine = new GrblMachine();
                                onRobotStatusChange(null, null);
                            }
                            else if (newData.Contains("Repetier"))
                            {
                                machine = new RepetierMachine();
                                onRobotStatusChange(null, null);
                            }
                        }
                    }
                }
            }
            //t.Start();
        }
    }
}
