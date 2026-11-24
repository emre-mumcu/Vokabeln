namespace Vokabeln.ViewModels
{
	// 404 ve genel hata sayfaları
	public class ErrorPageViewModel
	{
		public int StatusCode { get; set; }

		// Hata Kayıtları'ndaki numara (kaydedilemediyse null)
		public long? ErrorId { get; set; }

		// İstenen adres (sorgu dahil)
		public string? Path { get; set; }

		public string Title => StatusCode switch
		{
			400 => "Geçersiz istek",
			401 => "Giriş yapmanız gerekiyor",
			403 => "Bu sayfaya erişim izniniz yok",
			404 => "Sayfa bulunamadı",
			405 => "Bu işlem desteklenmiyor",
			_ => "Beklenmeyen bir hata oluştu"
		};
	}
}
