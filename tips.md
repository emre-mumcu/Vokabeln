dotnet tool install -g dotnet-aspnet-codegenerator

dotnet add package Microsoft.VisualStudio.Web.CodeGeneration.Design

git rm -r --cached Migrations Uploads

```cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("SadeceAdmin", policy =>
        policy.RequireRole("Admin"));

    options.AddPolicy("Yetiskin", policy =>
        policy.RequireClaim("YasGrubu", "18+"));

    options.AddPolicy("YoneticiVeEmailOnayli", policy =>
        policy.RequireRole("Yonetici")
              .RequireClaim("EmailOnayli", "true"));
});

[Authorize(Policy = "SadeceAdmin")] public IActionResult Ayarlar() { ... }

// Özel gereksinim (custom requirement) da yazılabilir
```