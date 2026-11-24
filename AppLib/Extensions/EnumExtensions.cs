using System.ComponentModel;
using System.Reflection;

namespace Vokabeln.AppLib.Extensions;

public static class EnumExtensions
{
    // [Description("das Nomen")] gibi açıklaması varsa onu, yoksa enum adını döndürür
    public static string GetDescription(this Enum value)
        => value.GetType()
            .GetField(value.ToString())?
            .GetCustomAttribute<DescriptionAttribute>()?
            .Description ?? value.ToString();
}
