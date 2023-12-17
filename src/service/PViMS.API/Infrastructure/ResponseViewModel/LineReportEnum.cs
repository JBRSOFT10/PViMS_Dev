using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;

namespace PVIMS.API.Infrastructure.ResponseViewModel
{
    public static class LineReportEnum
    {
        public enum ReportEnum {
            [Description("Date of Report Submission")]
            submitted_date,
            [Description("Age")]
            patient_age,
            [Description("Gender")]
            patient_sex,
            [Description("Brand/Trade Name")]
            brand_name,
            [Description("Generic Name With Strength")]
            generic_name,
            [Description("Frequency(Daily Dose)")]
            daily_dose,
            [Description("Medication start date")]
            medication_start_date,
            [Description("Event start date")]
            event_start_date,
            [Description("Indication")]
            indication,
            [Description("Describe event including relevant tests and laboratory results")]
            describe_event
        }
        



    public static T[] GetEnumValues<T>() where T : struct
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException("GetValues<T> can only be called for types derived from System.Enum", "T");
            }
            return (T[])Enum.GetValues(typeof(T));
        }
        public static List<string> GetEnumNames(Enum enm)
        {
            var type = enm.GetType();
            var displaynames = new List<string>();
            var names = Enum.GetNames(type);
            foreach (var name in names)
            {

                displaynames.Add(name);

            }
            return displaynames;
        }
        public static T GetEnumValue<T>(int intValue) where T : struct, IConvertible
        {
            Type enumType = typeof(T);
            if (!enumType.IsEnum)
            {
                throw new Exception("T must be an Enumeration type.");
            }

            return (T)Enum.ToObject(enumType, intValue);
        }
        public static string GetDisplayName(this Enum enumValue)
        {
            return enumValue.GetType()
                            .GetMember(enumValue.ToString())
                            .First()
                            .GetCustomAttribute<DisplayAttribute>()
                            .GetName();
        }
        public static string GetDescription(this Enum enumValue)
        {
            return enumValue.GetType()
                       .GetMember(enumValue.ToString())
                       .First()
                       .GetCustomAttribute<DescriptionAttribute>()?
                       .Description ?? string.Empty;
        }
    }
}
