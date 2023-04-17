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

        // Internally all units are in inches.
        public enum MeasurementUnitTypes
        {
            Millimeters,
            Inches,
        };
        MeasurementUnitTypes units;

        public void ChangeUnitType(MeasurementUnitTypes newType)
        {
            units = newType;
        }

        private float ToDisplayUnits(float inches)
        {
            switch (units)
            {
                case MeasurementUnitTypes.Inches:
                    return inches;
                case MeasurementUnitTypes.Millimeters:
                    return inches * 25.4f;
                default:
                    return float.NaN; // Should never get here.
            }
        }

        private float FromDisplayUnits(float value)
        {
            switch(units)
            {
                case MeasurementUnitTypes.Inches:
                    return value;
                case MeasurementUnitTypes.Millimeters:
                    return value / 25.4f;
                default:
                    return float.NaN; // Should never get here.
            }
        }

        public Settings(Robot.Robot robot, Router.Router router)
        {
            this.router = router;
            this.robot = robot;
            this.units = Settings.MeasurementUnitTypes.Millimeters;
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
        public class FourDecimalPlaceConverter : SingleConverter
        {
            public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
            {
                if (destinationType == typeof(string) && value is float)
                {
                    //return ((float)value).ToString("N3");
                    return String.Format("{0:0.####}", (float)value);
                }
                return base.ConvertTo(context, culture, value, destinationType);
            }
        }

        [TypeConverter(typeof(FourDecimalPlaceConverter))]
        [DisplayName("Tab Height")]
        [Description("Height of the tabs")]
        public float TabHeight
        {
            get { return ToDisplayUnits(router.TabHeight); }
            set { router.TabHeight = FromDisplayUnits(value); }
        }

        [TypeConverter(typeof(FourDecimalPlaceConverter))]
        [DisplayName("Last Pass Height")]
        [Description("Height of the last pass")]
        public float LastPassHeight
        {
            get { return ToDisplayUnits(router.LastPassHeight); }
            set { router.LastPassHeight = FromDisplayUnits(value); }
        }

        [TypeConverter(typeof(FourDecimalPlaceConverter))]
        [DisplayName("Tool Diameter")]
        [Description("Tool Diameter")]
        public float ToolDiameter
        {
            get { return ToDisplayUnits(router.ToolDiameter); }
            set { router.ToolDiameter = FromDisplayUnits(value); }
        }

        [TypeConverter(typeof(FourDecimalPlaceConverter))]
        [DisplayName("Move Height")]
        [Description("Tool height for rapid moves (set to a height above the workpiece and all clamps)")]
        public float MoveHeight
        {
            get { return ToDisplayUnits(router.MoveHeight); }
            set { router.MoveHeight = FromDisplayUnits(value); }
        }

        [TypeConverter(typeof(FourDecimalPlaceConverter))]
        [DisplayName("Max Cut Depth")]
        [Description("All generated paths will be above this z-height.")]
        public float MaxCutDepth
        {
            get { return ToDisplayUnits(router.MaxCutDepth); }
            set { router.MaxCutDepth = FromDisplayUnits(value); }
        }

        [TypeConverter(typeof(FourDecimalPlaceConverter))]
        [DisplayName("Cutting Speed")]
        [Description("Cutting Speed (units per minute)")]
        public float RoutSpeed
        {
            get { return ToDisplayUnits(robot.MaxCutSpeed); }
            set { robot.MaxCutSpeed = FromDisplayUnits(value); }
        }

        [TypeConverter(typeof(FourDecimalPlaceConverter))]
        [DisplayName("Moving Speed")]
        [Description("Rapid movement speed (units per minute)")]
        public float MoveSpeed
        {
            get { return ToDisplayUnits(robot.MaxRapidSpeed); }
            set { robot.MaxRapidSpeed = FromDisplayUnits(value); }
        }

        [TypeConverter(typeof(FourDecimalPlaceConverter))]
        [DisplayName("Max Z Speed")]
        [Description("Maximum possible Z axis speed (units per minute)")]
        public float MaxAxisSpeeds
        {
            get { return ToDisplayUnits(robot.MaxZSpeed); }
            set { robot.MaxZSpeed = FromDisplayUnits(value); }
        }
    }
}
