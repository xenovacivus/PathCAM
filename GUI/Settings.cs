using OpenTK;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
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

        public class UnitConverter
        {
            public Settings.MeasurementUnitTypes currentSelectedUnits;
            public UnitConverter(MeasurementUnitTypes defaultUnits)
            {
                currentSelectedUnits = defaultUnits;
            }

            public void UpdateUnits(MeasurementUnitTypes newUnits)
            {
                if (newUnits != currentSelectedUnits)
                {
                    currentSelectedUnits = newUnits;
                    onUnitsChange(this, null);
                }
            }

            public EventHandler onUnitsChange;



            private float FromPathCAMToUISpeedUnitScale
            {
                get
                {
                    switch (currentSelectedUnits)
                    {
                        case MeasurementUnitTypes.Inches:
                            return 1.0f;
                        case MeasurementUnitTypes.Millimeters:
                            // Convert from inches per minute to millimeters per second
                            return 25.4f / 60.0f;
                        default:
                            return float.NaN;
                    }
                }
            }

            public float SpeedToUIUnits(float speedInPathCAMUnits)
            {
                return speedInPathCAMUnits * FromPathCAMToUISpeedUnitScale;
            }

            public float SpeedFromUIUnits(float speedInUIUnits)
            {
                return speedInUIUnits / FromPathCAMToUISpeedUnitScale;
            }


            internal float ToUIUnits(float valueInPathCAMUnits)
            {
                switch (currentSelectedUnits)
                {
                    case MeasurementUnitTypes.Inches:
                        return valueInPathCAMUnits;
                    case MeasurementUnitTypes.Millimeters:
                        return valueInPathCAMUnits * 25.4f;
                    default:
                        return float.NaN; // Should never get here.
                }
            }

            internal float FromUIUnits(float valueInUIUnits)
            {
                switch (currentSelectedUnits)
                {
                    case MeasurementUnitTypes.Inches:
                        return valueInUIUnits;
                    case MeasurementUnitTypes.Millimeters:
                        return valueInUIUnits / 25.4f;
                    default:
                        return float.NaN; // Should never get here.
                }
            }
        }

        // Internally all units are in inches.
        public enum MeasurementUnitTypes
        {
            Millimeters,
            Inches,
        };

        public MeasurementUnitTypes GetUnitType()
        {
            return unitConverter.currentSelectedUnits;
        }

        public UnitConverter GetUnitConverter()
        {
            return unitConverter;
        }

        public void ChangeUnitType(MeasurementUnitTypes newType)
        {
            unitConverter.UpdateUnits(newType);
        }

        private float ToDisplayUnits(float inches)
        {
            return unitConverter.ToUIUnits(inches);
            
        }

        private float FromDisplayUnits(float uiUnits)
        {
            return unitConverter.FromUIUnits(uiUnits);
        }


        UnitConverter unitConverter;
        public Settings(Robot.Robot robot, Router.Router router)
        {
            this.unitConverter = new UnitConverter(MeasurementUnitTypes.Millimeters);
            this.router = router;
            this.robot = robot;
        }

        ///
        /// Accessors for the property grid
        ///

        //[DisplayName("Units")]
        //[Description("Unit of Measurement (Millimeters or Inches)")]
        //public string Units
        //{
        //    get { return this.units.ToString(); }
        //    set {
        //        if (value.ToLower().StartsWith("i"))
        //        {
        //            this.units = MeasurementUnitTypes.Inches;
        //        }
        //        else if (value.ToLower().StartsWith("m"))
        //        {
        //            this.units = MeasurementUnitTypes.Millimeters;
        //        }
        //    }
        //}

        // https://learn.microsoft.com/en-us/answers/questions/1183869/when-displaying-float-or-double-values-in-property
        public class ThreeDecimalPlaceConverter : SingleConverter
        {
            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                if (destinationType == typeof(string) && value is float)
                {
                    //return ((float)value).ToString("N3");
                    return String.Format("{0:0.###}", (float)value);
                }
                return base.ConvertTo(context, culture, value, destinationType);
            }
        }

        [Category("Path Planning")]
        [TypeConverter(typeof(ThreeDecimalPlaceConverter))]
        [DisplayName("Tab Height")]
        [Description("Height of the tabs")]
        public float TabHeight
        {
            get { return ToDisplayUnits(router.TabHeight); }
            set { router.TabHeight = FromDisplayUnits(value); }
        }

        [Category("Path Planning")]
        [TypeConverter(typeof(ThreeDecimalPlaceConverter))]
        [DisplayName("Last Pass Height")]
        [Description("Height of the last pass")]
        public float LastPassHeight
        {
            get { return ToDisplayUnits(router.LastPassHeight); }
            set { router.LastPassHeight = FromDisplayUnits(value); }
        }

        [Category("Path Planning")]
        [TypeConverter(typeof(ThreeDecimalPlaceConverter))]
        [DisplayName("Tool Diameter")]
        [Description("Tool Diameter")]
        public float ToolDiameter
        {
            get { return ToDisplayUnits(router.ToolDiameter); }
            set { router.ToolDiameter = FromDisplayUnits(value); }
        }

        [Category("Path Planning")]
        [TypeConverter(typeof(ThreeDecimalPlaceConverter))]
        [DisplayName("Move Height")]
        [Description("Tool height for rapid moves (set to a height above the workpiece and all clamps)")]
        public float MoveHeight
        {
            get { return ToDisplayUnits(router.MoveHeight); }
            set { router.MoveHeight = FromDisplayUnits(value); }
        }

        [Category("Path Planning")]
        [TypeConverter(typeof(ThreeDecimalPlaceConverter))]
        [DisplayName("Max Cut Depth")]
        [Description("All generated paths will be above this z-height.")]
        public float MaxCutDepth
        {
            get { return ToDisplayUnits(router.MaxCutDepth); }
            set { router.MaxCutDepth = FromDisplayUnits(value); }
        }

        [Category("Path Planning")]
        [TypeConverter(typeof(ThreeDecimalPlaceConverter))]
        [DisplayName("Point Span")]
        [Description("Distance between two points on generated paths")]
        public float MaxPointDistance
        {
            get { return ToDisplayUnits(router.maxPointDistance); }
            set { router.maxPointDistance = FromDisplayUnits(value); }
        }

        [Category("Path Planning")]
        [DisplayName("Uniform Points")]
        [Description("Encourage uniform point distance on generated paths")]
        public bool ForcePathMaxDistanceBetweenPoints
        {
            get { return router.enforceMaxPointDistance; }
            set { router.enforceMaxPointDistance = value; }
        }

        [Category("Robot")]
        [TypeConverter(typeof(ThreeDecimalPlaceConverter))]
        [DisplayName("Cutting Speed")]
        [Description("Cutting Speed (mm/second or inches/minute)")]
        public float RoutSpeed
        {
            get { return unitConverter.SpeedToUIUnits(robot.MaxCutSpeed); }
            set { robot.MaxCutSpeed = unitConverter.SpeedFromUIUnits(value); }
        }

        [Category("Robot")]
        [TypeConverter(typeof(ThreeDecimalPlaceConverter))]
        [DisplayName("Moving Speed")]
        [Description("Rapid movement speed (mm/second or inches/minute)")]
        public float MoveSpeed
        {
            get { return unitConverter.SpeedToUIUnits(robot.MaxRapidSpeed); }
            set { robot.MaxRapidSpeed = unitConverter.SpeedFromUIUnits(value); }
        }

        [Category("Robot")]
        [TypeConverter(typeof(ThreeDecimalPlaceConverter))]
        [DisplayName("Max Z Speed")]
        [Description("Maximum possible Z axis speed (mm/second or inches/minute)")]
        public float MaxAxisSpeeds
        {
            get { return unitConverter.SpeedToUIUnits(robot.MaxZSpeed); }
            set { robot.MaxZSpeed = unitConverter.SpeedFromUIUnits(value); }
        }
    }
}
