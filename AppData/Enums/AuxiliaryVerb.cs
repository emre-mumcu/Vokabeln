using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Vokabeln.AppData.Enums;

// Perfekt'te kullanılan yardımcı fiil. Display: form seçim listesi, Description: diğer ekranlar.
public enum AuxiliaryVerb
{
    [Display(Name = "haben")]
    [Description("haben")]
    Haben,

    [Display(Name = "sein")]
    [Description("sein")]
    Sein,

    // ör. fahren: "ich habe das Auto gefahren" / "ich bin nach Berlin gefahren"
    [Display(Name = "haben / sein")]
    [Description("haben / sein")]
    HabenOderSein
}
