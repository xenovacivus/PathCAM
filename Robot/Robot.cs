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

        IRobotCommand currentCommand = null;
        SerialPortWrapper serial;
        Timer t;
        int elapsedCounter = 0;

        Vector3 currentPosition = new Vector3(0, 0, 0);

        Vector3 lastPosition = new Vector3(0, 0, 0);
        bool lastPositionKnown = false;

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

                        // Send a status command
                        currentCommand = new StatusCommand();
                        
                        serial.Transmit(currentCommand.GenerateCommand(), 0x21);
                        elapsedCounter = 0;
                    }
                }
            }
            t.Start();
        }

        #region Serial Interface Callbacks
        
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
                        // See if there's any state information in the command used to 
                        // update location or other fields...
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
                                //onPositionUpdate(new object[] { currentPosition, c.time }, EventArgs.Empty);
                                onRobotStatusChange(c, EventArgs.Empty);
                            }
                        }
                        if (currentCommand is MoveCommand)
                        {
                            MoveCommand m = currentCommand as MoveCommand;
                            locations = m.Locations;
                            currentPosition = m.CurrentPosition;
                            if (this.lastPositionKnown == false)
                            {
                                lastPosition = currentPosition;
                                lastPositionKnown = true;
                            }
                            if (onRobotStatusChange != null)
                            {
                                //onPositionUpdate(new object[] { currentPosition, m.time }, EventArgs.Empty);
                                onRobotStatusChange(m, EventArgs.Empty);
                            }
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

        #endregion

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
            else if (locations > 0)
            {
                while (currentCommand == null && commands.Count > 0)
                {
                    ICommand command = commands.Dequeue();
                    if (command is MoveTool)
                    {
                        MoveTool m = command as MoveTool;
                        GoTo(m.Target, m.Speed);
                    }
                }
            }

            //if (currentCommand == null && locations > 0)
            //{
            //    // Ok to pass in another movement command
            //    // TODO: rework this to use a local buffer...
            //    if (onRobotReady != null)
            //    {
            //        onRobotReady(this, EventArgs.Empty);
            //    }
            //}

            if (currentCommand == null)
            {
                currentCommand = new StatusCommand();
            }
            
            return currentCommand;
        }

        public Vector3 GetPosition()
        {
            return currentPosition;
        }

        public void AddCommand(ICommand command)
        {
            commands.Enqueue(command);
        }
        
        /// <summary>
        /// Run the router from the current position to the given position
        /// </summary>
        /// <param name="p">Destination location in inches</param>
        /// <param name="inches_per_minute">Tool speed in inches per second</param>
        private void GoTo(Vector3 p, float inches_per_minute)
        {
            lock (thisLock)
            {
                if (this.lastPositionKnown == false)
                {
                    inches_per_minute = Math.Min(MaxInchesPerMinute.X, Math.Min(MaxInchesPerMinute.Y, MaxInchesPerMinute.Z));
                }
                Vector3 delta = lastPosition - p;
                lastPosition = p;
                lastPositionKnown = true;

                float inches = delta.Length;

                UInt16 time_milliseconds = (UInt16)(1000 * 60 * inches / inches_per_minute);

                if (delta.Length > 0)
                {
                    if (Math.Abs(delta.X) > 0.0001f)
                    {
                        inches_per_minute = Math.Min(MaxInchesPerMinute.X, inches_per_minute);
                    }
                    if (Math.Abs(delta.Y) > 0.0001f)
                    {
                        inches_per_minute = Math.Min(MaxInchesPerMinute.Y, inches_per_minute);
                    }
                    if (Math.Abs(delta.Z) > 0.0001f)
                    {
                        inches_per_minute = Math.Min(MaxInchesPerMinute.Z, inches_per_minute);
                    }

                    currentCommand = new MoveCommand(p, inches_per_minute / 60.0f);
                }
                else
                {
                    Console.WriteLine("Ignoring command with time of 0");
                }
            }
        }

        

        Vector3 MaxInchesPerMinute
        {
            get { return new Vector3(400, 400, 40); }
        }


    }
}
