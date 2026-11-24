using System.ComponentModel;

namespace Vokabeln.AppData.Enums;

// Import: dosyadaki kelime (Word + WordType) veritabanında zaten varsa ne yapılacağı
public enum ExistingWordMode
{
    [Description("Atla")]
    Skip,

    // Boş alanlar ve girilmemiş zamanlar dosyadan doldurulur; dolu olanlara dokunulmaz
    [Description("Yalnızca boş alanları doldur")]
    FillEmpty,

    // Dosyada dolu olan alanlar ve zamanlar üzerine yazılır (dosyada boş olan alan veritabanında silinmez)
    [Description("Dosyadakiyle güncelle")]
    Overwrite
}
