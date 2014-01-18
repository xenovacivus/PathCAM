using OpenTK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Linq;
using System.Text;

namespace GUI
{
    /// <summary>
    /// Collects all settings and exposes them through one class.
    /// </summary>
    public class Settings
    {
        private Robot.Robot robot;
        private Router.Router router;
        public Settings(Robot.Robot robot, Router.Router router)
        {
            this.router = router;
            this.robot = robot;
        }


        ///
        /// Accessors for the property grid
        ///
        [DisplayName("Last Pass Height")]
        [Description("Height of the last pass in inches")]
        public float LastPassHeight
        {
            get { return router.LastPassHeight; }
            set { router.LastPassHeight = value; }
        }

        [DisplayName("Tool Diameter")]
        [Description("Tool Diameter in inches")]
        public float ToolDiameter
        {
            get { return router.ToolDiameter; }
            set { router.ToolDiameter = value; }
        }

        [DisplayName("Move Height")]
        [Description("Safe travel height")]
        public float MoveHeight
        {
            get { return router.MoveHeight; }
            set { router.MoveHeight = value; }
        }

        [DisplayName("Max Cut Depth")]
        [Description("Maximum Cut Depth")]
        public float MaxCutDepth
        {
            get { return router.MaxCutDepth; }
            set { router.MaxCutDepth = value; }
        }



        [DisplayName("Cutting Speed")]
        [Description("Cutting Speed (inches per minute)")]
        public float RoutSpeed
        {
            get { return robot.MaxCutSpeed; }
            set { robot.MaxCutSpeed = value; }
        }

        [DisplayName("Moving Speed")]
        [Description("Rapid movement speed (inches per minute)")]
        public float MoveSpeed
        {
            get { return robot.MaxRapidSpeed; }
            set { robot.MaxRapidSpeed = value; }
        }

        [DisplayName("Max Z Speed")]
        [Description("Maximum possible Z axis speed (inches per minute)")]
        public float MaxAxisSpeeds
        {
            get { return robot.MaxZSpeed; }
            set { robot.MaxZSpeed = value; }
        }
    }
}
