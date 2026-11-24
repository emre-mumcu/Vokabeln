using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using Vokabeln.AppData;
using Vokabeln.AppLib.Filters;
using Vokabeln.AppLib.Logging;
using Vokabeln.AppLib.Services;

///
/// Arka planda şunları hazırlar:
/// Kestrel web sunucusu
/// Yapılandırma (configuration): appsettings.json, ortam değişkenleri, komut satırı argümanları (args)
/// Logging altyapısı
/// Dependency Injection (DI) konteyneri (builder.Services)
/// 
var builder = WebApplication.CreateBuilder(args);


builder.WebHost.UseUrls("https://localhost:5077");


/// 
/// MVC için gereken servisleri DI konteynerine ekler. Şunları kapsar:
/// * Controller'lar
/// * Razor View'ler
/// * Razor Pages
/// * Model binding, validation, filtreler vb.
/// 
/// Not: AddMvc() en "geniş" seçenektir. Daha dar alternatifler:
/// * AddControllers() -> Sadece controller'lar (Web API için)
/// * AddControllersWithViews() -> Controller + View (klasik MVC)
/// * AddRazorPages() -> Sadece Razor Pages
/// * AddMvc() -> Hepsi birden
/// 
builder.Services.AddMvc(options =>
{
	options.Filters.Add<GlobalExceptionFilter>();
});

/// 
/// AddAuthentication hangi şemayı (cookie, JWT, vb.) kullanacağınızı ve nasıl çalışacağını tanımlar.
/// builder.Services.AddAuthentication();
/// 
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        options.SlidingExpiration = true;
    });

/// 
///  AddMvc() / AddControllersWithViews() / AddControllers() zaten arka planda authorization servislerini ekler.
///  
builder.Services.AddAuthorization();


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

builder.Services.AddScoped<IWordService, WordService>();
builder.Services.AddScoped<IWordPlayService, WordPlayService>();
builder.Services.AddScoped<IGameStatsService, GameStatsService>();
builder.Services.AddScoped<IConjugationService, ConjugationService>();
builder.Services.AddScoped<IArticlePlayService, ArticlePlayService>();
builder.Services.AddScoped<IConjugationPlayService, ConjugationPlayService>();
builder.Services.AddScoped<IPluralPlayService, PluralPlayService>();

// Singleton: Wiktionary kaynak dosyasının indeksi uygulama boyunca bellekte tutulur (ilk aramada kurulur)
builder.Services.AddSingleton<IWiktionaryService, WiktionaryService>();

// PONS sözlüğü (Almanca -> Türkçe). Secret: "Pons:Secret" (user-secrets / ortam değişkeni Pons__Secret; appsettings'e yazılmaz)
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<IPonsService, PonsService>(client =>
{
    client.BaseAddress = new Uri("https://api.pons.com/v1/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

///
/// Log ve hata kayıtları (Tools -> Log Kayıtları / Hata Kayıtları): ayrı SQLite dosyası (ConnectionStrings:LogConnection, Logs.db).
/// DatabaseLoggerProvider, ILogger kayıtlarını arka planda bu dosyaya yazar; seviyeler appsettings "Logging:Database" altında.
///
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ILogStore, SqliteLogStore>();
builder.Logging.Services.AddSingleton<ILoggerProvider, DatabaseLoggerProvider>();

///
/// Bu noktadan sonra servis kaydı biter (artık builder.Services'e ekleme yapılmaz), 
/// app nesnesi üzerinden middleware pipeline tanımlanır.
/// Middleware'ler yazıldıkları sırayla çalışır. 
/// Bir istek yukarıdan aşağıya geçer, yanıt ise tersine döner. Sıra önemlidir.
/// 
var app = builder.Build();


await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Migration'lar git'te tutulmuyor: proje temiz bir clone'dan derlenirse DLL'de hiç migration olmaz.
    // O durumda yeni veritabanında (ya da Reset DB sonrası) tablolar oluşturulamaz; açılışta uyarılır.
    if (!dbContext.Database.GetMigrations().Any())
    {
        app.Logger.LogWarning(
            "Derlenmiş uygulamada hiç EF Core migration'ı yok. Yeni bir veritabanında tablolar oluşturulamaz ve Reset DB çalışmaz. " +
            "Migrations klasörünün bulunduğu makineden derleyip publish edin. Veritabanı: {DataSource}",
            dbContext.Database.GetDbConnection().DataSource);
    }

    await dbContext.Database.MigrateAsync();
}

///
/// Merkezi hata yönetimi (ErrorController):
/// * UseExceptionHandler: MVC filtresine (GlobalExceptionFilter) düşmeyen hatalar (görünüm oluşturma, middleware) -> /Error (500 sayfası)
/// * UseStatusCodePagesWithReExecute: gövdesiz hata yanıtları (404 vb.) -> /Error/404; adres değişmez, istek yeniden çalıştırılır
/// Her ikisi de Hata Kayıtları'na yazılır. Pipeline'ın başında olmalılar ki sonrasındaki her şeyi kapsasınlar.
///
app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error/{0}");

///
/// HTTP isteklerini otomatik olarak HTTPS'e yönlendirir (http://... → https://...).
/// 
app.UseHttpsRedirection();

/// 
/// wwwroot klasöründeki statik dosyaları (CSS, JS, resim vb.) sunar. 
/// Bu middleware'i routing'den önce koymak performans açısından iyidir: 
/// statik dosya isteği, gereksiz yere routing/controller aşamalarına girmeden yanıtlanır.
/// 
app.UseStaticFiles();

/// 
/// İsteğin URL'sine bakarak hangi endpoint'e (controller action'ı vb.) gideceğini belirler. 
/// Henüz çalıştırmaz, sadece eşleştirir.
/// 
app.UseRouting();

/// 
/// Authentication (kimlik doğrulama), "bu istek kimden geliyor?" sorusunu cevaplar.
/// Her istekte AddAuthentication ile tanımlanan şemayı çalıştırır, cookie/token'ı okur, geçerliyse HttpContext.User nesnesini doldurur.
/// Sıra kuralı: UseRouting → UseAuthentication → UseAuthorization. 
/// Authentication'ı authorization'dan sonra koyarsanız yetkilendirme kullanıcıyı göremez.
/// 
app.UseAuthentication();

/// 
/// Eşleşen endpoint'in yetkilendirme kurallarını ([Authorize] gibi) denetler. 
/// Authorization "bu kişi buna erişebilir mi?" sorusunu cevaplar. 
/// UseAuthorization, Authentication tarafından doldurulan User bilgisine bakarak [Authorize] kurallarını uygular.
/// Dikkat: Bu satır yalnızca yetkilendirmeyi kontrol eder. 
/// Kullanıcıyı tanımlamak için app.UseAuthentication() gerekir ve UseRouting ile UseAuthorization arasında olmalıdır.
/// 
app.UseAuthorization();

/// 
/// Attribute routing kullanan controller'ları eşler.
/// 
app.MapControllers();

/// 
/// Conventional (geleneksel) routing için varsayılan rotayı tanımlar.
/// 
app.MapDefaultControllerRoute();


app.Lifetime.ApplicationStarted.Register(() =>
{
	var url = app.Services.GetRequiredService<IServer>()
				  .Features.Get<IServerAddressesFeature>()?
				  .Addresses.FirstOrDefault() ?? "http://localhost:5000";

	// 0.0.0.0, [::] veya * ile dinleniyorsa tarayıcı için localhost'a çevir
	url = url.Replace("0.0.0.0", "localhost")
			 .Replace("[::]", "localhost")
			 .Replace("*", "localhost");

	OpenBrowser(url);
});


/// 
/// Sunucuyu başlatır ve uygulama kapanana kadar istekleri dinler (bloklayıcı çağrıdır).
/// 
app.Run();


static void OpenBrowser(string url)
{
	try
	{
		if (OperatingSystem.IsWindows())
			Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
		else if (OperatingSystem.IsMacOS())
			Process.Start("open", url);
		else if (OperatingSystem.IsLinux())
			Process.Start("xdg-open", url);
	}
	catch
	{
		// Tarayıcı açılamazsa uygulama yine de çalışmaya devam etsin
	}
}