#if UNITY_EDITOR
using System;
using System.Globalization;
using System.Reflection;

namespace VMFramework.Pipeline.Editor
{
    /// <summary>
    /// Resolves the simple member-based Odin conditions that determine whether serialized configuration is active.
    /// Unsupported expressions stay active so validators fail conservatively instead of hiding possible defects.
    /// </summary>
    internal static class OdinConditionalFieldUtility
    {
        private const string ShowIfAttributeName = "Sirenix.OdinInspector.ShowIfAttribute";
        private const string HideIfAttributeName = "Sirenix.OdinInspector.HideIfAttribute";

        public static bool IsActive(object target, FieldInfo field)
        {
            if (target == null || field == null)
            {
                return true;
            }

            foreach (object attribute in field.GetCustomAttributes(true))
            {
                string attributeTypeName = attribute.GetType().FullName;
                bool isShowIf = attributeTypeName == ShowIfAttributeName;
                bool isHideIf = attributeTypeName == HideIfAttributeName;
                if ((isShowIf || isHideIf) == false ||
                    TryEvaluate(target, attribute, out bool conditionMatches) == false)
                {
                    continue;
                }

                if (isShowIf && conditionMatches == false ||
                    isHideIf && conditionMatches)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryEvaluate(object target, object attribute, out bool conditionMatches)
        {
            conditionMatches = false;
            if (target == null || attribute == null)
            {
                return false;
            }

            Type attributeType = attribute.GetType();
            string condition = attributeType
                .GetField("Condition", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(attribute)?.ToString();
            condition ??= attributeType
                .GetProperty("MemberName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(attribute)?.ToString();

            if (string.IsNullOrEmpty(condition) || condition.StartsWith("@", StringComparison.Ordinal))
            {
                return false;
            }

            if (TryGetMemberValue(target, condition, out object memberValue) == false)
            {
                return false;
            }

            object comparisonValue = attributeType
                .GetField("Value", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(attribute);
            if (comparisonValue != null)
            {
                conditionMatches = ValuesEqual(memberValue, comparisonValue);
                return true;
            }

            if (memberValue is bool booleanValue)
            {
                conditionMatches = booleanValue;
                return true;
            }

            return false;
        }

        private static bool TryGetMemberValue(object target, string memberName, out object value)
        {
            value = null;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public |
                                       BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

            try
            {
                for (Type type = target.GetType(); type != null && type != typeof(object); type = type.BaseType)
                {
                    if (type.GetField(memberName, flags) is { } conditionField)
                    {
                        value = conditionField.GetValue(target);
                        return true;
                    }

                    if (type.GetProperty(memberName, flags) is { CanRead: true } conditionProperty &&
                        conditionProperty.GetIndexParameters().Length == 0)
                    {
                        value = conditionProperty.GetValue(target);
                        return true;
                    }

                    if (type.GetMethod(memberName, flags, null, Type.EmptyTypes, null) is { } conditionMethod &&
                        conditionMethod.ReturnType != typeof(void))
                    {
                        value = conditionMethod.Invoke(target, null);
                        return true;
                    }
                }
            }
            catch
            {
                // Unsupported or throwing conditions stay active.
            }

            return false;
        }

        private static bool ValuesEqual(object memberValue, object comparisonValue)
        {
            if (ReferenceEquals(memberValue, comparisonValue))
            {
                return true;
            }

            if (memberValue == null || comparisonValue == null)
            {
                return false;
            }

            if (memberValue.Equals(comparisonValue))
            {
                return true;
            }

            Type memberType = memberValue.GetType();
            if (memberType.IsEnum)
            {
                if (comparisonValue is string comparisonString &&
                    Enum.TryParse(memberType, comparisonString, true, out object parsedValue))
                {
                    return memberValue.Equals(parsedValue);
                }

                try
                {
                    return memberValue.Equals(Enum.ToObject(memberType, comparisonValue));
                }
                catch
                {
                    return false;
                }
            }

            if (IsNumeric(memberValue) && IsNumeric(comparisonValue))
            {
                try
                {
                    return Convert.ToDecimal(memberValue, CultureInfo.InvariantCulture) ==
                           Convert.ToDecimal(comparisonValue, CultureInfo.InvariantCulture);
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }

        private static bool IsNumeric(object value)
        {
            return value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double
                or decimal;
        }
    }
}
#endif
