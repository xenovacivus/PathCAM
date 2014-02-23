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
        private int elapsedCounter = 0;
        private Vector3 currentPosition = new Vector3(0, 0, 0);
        private Vector3 lastPosition = new Vector3(0, 0, 0);
        private bool lastPositionKnown = false;

        private const float minSpeed = 0.01f; // All speeds are clamped to this.
        private float maxZSpeed = 30;
        private float maxCutSpeed = 100.0f;
        private float maxRapidSpeed = 250.0f;

        bool sendResumeCommand = false;
        bool sendPauseCommand = false;
        bool sendCancelCommand = false;
        bool sendEnableStepperCommand = false;
        bool sendDisableStepperCommand = false;
        
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



        public Robot(SerialPortWrapper serial)
        {
            this.serial = serial;
            serial.newDataAvailable += new SerialPortWrapper.newDataAvailableDelegate(NewDataAvailable);
            serial.receiveDataError += new SerialPortWrapper.receiveDataErrorDelegate(ReceiveDataError);
            t = new Timer();
            t.Interval = 50;
            t.Start();
            t.Elapsed += new ElapsedEventHandler(t_Elapsed);
        }

        void t_Elapsed(object sender, ElapsedEventArgs e)
        {
            t.Stop();
            lock (thisLock)
            {
                if (serial != null && serial.IsOpen)
                {
                    elapsedCounter++;
                    if ((elapsedCounter * 50) > (1000)) // More than 1 second to reply
                    {
                        Console.WriteLine("Device Timeout!");
                        
                        // Assume disconnected, won't give another move command until the position is known
                        lastPositionKnown = false;

                        // Send a status command
                        currentCommand = new StatusCommand();
                        
                        serial.Transmit(currentCommand.GenerateCommand(), 0x21);
                        elapsedCounter = 0;
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
        private void NewDataAvailable(SerialPortWrapper.SimpleSerialPacket packet)
        {
            lock (thisLock)
            {
                elapsedCounter = 0;
                if (currentCommand == null)
                {
                    Console.WriteLine("Error: Received data, but no command was sent!");
                    foreach (byte b in packet.Data)
                    {
                        Console.Write(b.ToString("x") + ", ");
                    }
                    Console.WriteLine();
                }
                else
                {
                    currentCommand.ProcessResponse(packet.Data);
                    if (currentCommand.IsDataValid())
                    {
                        int locations = 0;
                        if (currentCommand is StatusCommand)
                        {
                            StatusCommand c = currentCommand as StatusCommand;
                            currentPosition = c.CurrentPosition;
                            locations = c.Locations;
                            if (this.lastPositionKnown == false)
                            {
                                lastPosition = currentPosition;
                                lastPositionKnown = true;
                            }
                            if (onRobotStatusChange != null)
                            {
                                onRobotStatusChange(c, EventArgs.Empty);
                            }
                        }
                        if (currentCommand is MoveCommand)
                        {
                        }

                        currentCommand = GetNextCommand(locations);
                        serial.Transmit(currentCommand.GenerateCommand(), 0x21);
                    }
                    else
                    {
                        Console.WriteLine("Error: Did not process data correctly!");
                    }
                }
            }
        }

        private IRobotCommand GetNextCommand(int locations)
        {
            currentCommand = null;

            if (sendCancelCommand)
            {
                currentCommand = new CancelCommand();
                sendCancelCommand = false;
            }
            else if (sendResumeCommand)
            {
                currentCommand = new ResumeCommand();
                sendResumeCommand = false;
            }
            else if (sendPauseCommand)
            {
                currentCommand = new PauseCommand();
                sendPauseCommand = false;
            }
            else if (sendEnableStepperCommand)
            {
                currentCommand = new StepperEnableCommand();
                sendEnableStepperCommand = false;
            }
            else if (sendDisableStepperCommand)
            {
                currentCommand = new StepperDisableCommand();
                sendDisableStepperCommand = false;
            }
            else if (locations > 0 && lastPositionKnown)
            {
                while (currentCommand == null && commands.Count > 0)
                {
                    ICommand command = commands.Dequeue();
                    if (command is MoveTool)
                    {
                        currentCommand = CreateRobotCommand(command as MoveTool);
                    }
                }
            }
            if (currentCommand == null)
            {
                currentCommand = new StatusCommand();
            }
            
            return currentCommand;
        }
        
        /// <summary>
        /// Create a robot command from a MoveTool command.  Adjusts for robot specific
        /// max speeds.  May return null if there is no effective move.
        /// </summary>
        /// <param name="m"></param>
        /// <returns></returns>
        private IRobotCommand CreateRobotCommand(MoveTool m)
        {
            var p = m.Target;

            float inches_per_minute = m.Speed == MoveTool.SpeedType.Cutting ? MaxCutSpeed : MaxRapidSpeed;

            Vector3 delta = lastPosition - p;
            lastPosition = p;

            if (delta.Length > 0)
            {
                if (Math.Abs(delta.Z) > 0.0001f)
                {
                    inches_per_minute = Math.Min(MaxZSpeed, inches_per_minute);
                }

                return new MoveCommand(p, inches_per_minute / 60.0f);
            }
            else
            {
                Console.WriteLine("Ignoring command with length of 0");
                return null;
            }
        }

    }
}
