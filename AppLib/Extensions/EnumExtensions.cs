using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace Vokabeln.AppLib.Extensions;

public static class EnumExtensions
{
    // [Description("das Nomen")] varsa onu, yoksa [Display(Name = ...)], o da yoksa enum adını döndürür
    public static string GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());

        return field?.GetCustomAttribute<DescriptionAttribute>()?.Description
            ?? field?.GetCustomAttribute<DisplayAttribute>()?.GetName()
            ?? value.ToString();
    }
}
