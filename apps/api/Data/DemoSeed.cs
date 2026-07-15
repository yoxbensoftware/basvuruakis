using BasvuruAkis.Api.Domain;
using BasvuruAkis.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BasvuruAkis.Api.Data;

public static class DemoSeed
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var now = DateTimeOffset.UtcNow;

        if (!await db.Regions.AnyAsync())
        {
            db.Regions.Add(new Region { Id = 1, Name = "Marmara" });
        }

        if (!await db.Provinces.AnyAsync())
        {
            db.Provinces.Add(new Province { Id = 34, Name = "İstanbul", RegionId = 1 });
            db.Districts.Add(new District { Id = 3401, ProvinceId = 34, Name = "Kadıköy" });
            db.Neighborhoods.Add(new Neighborhood { Id = 340101, DistrictId = 3401, Name = "Caferağa" });
        }

        if (!await db.RepresentativeOffices.AnyAsync())
        {
            db.RepresentativeOffices.AddRange(
                new RepresentativeOffice { Id = 1, Name = "Genel Merkez", IsDefault = true, IsActive = true },
                new RepresentativeOffice { Id = 2, Name = "Kadıköy Temsilciliği", IsDefault = false, IsActive = true });
        }

        if (!await db.AssignmentRules.AnyAsync())
        {
            db.AssignmentRules.AddRange(
                new AssignmentRule { Id = 1, Scope = AssignmentRuleScope.Neighborhood, ScopeId = 340101, RepresentativeOfficeId = 2, Priority = 1, IsActive = true, CreatedAt = now },
                new AssignmentRule { Id = 2, Scope = AssignmentRuleScope.Default, ScopeId = null, RepresentativeOfficeId = 1, Priority = 999, IsActive = true, CreatedAt = now });
        }

        if (!await db.ContentPages.AnyAsync())
        {
            db.ContentPages.AddRange(
                new ContentPage
                {
                    Id = Guid.NewGuid(),
                    Slug = "anasayfa",
                    Title = "BaşvuruAkış",
                    Summary = "KVKK uyumlu başvuru ve otomatik yönlendirme platformu.",
                    Body = "Başvurularınızı güvenli telefon doğrulaması ve yetki kontrollü yönetim paneli ile toplayın.",
                    SeoTitle = "BaşvuruAkış",
                    SeoDescription = "Güvenli başvuru toplama ve otomatik temsilcilik atama.",
                    Status = PublishStatus.Published,
                    PublishedAt = now
                },
                new ContentPage
                {
                    Id = Guid.NewGuid(),
                    Slug = "iletisim",
                    Title = "İletişim",
                    Summary = "Kurumsal iletişim bilgileri.",
                    Body = "Demo ortamında iletişim bilgileri yönetim panelinden güncellenir.",
                    SeoTitle = "İletişim",
                    SeoDescription = "BaşvuruAkış iletişim.",
                    Status = PublishStatus.Published,
                    PublishedAt = now
                });
        }

        if (!await db.LegalTexts.AnyAsync())
        {
            db.LegalTexts.AddRange(
                new LegalText
                {
                    Id = Guid.NewGuid(),
                    Type = LegalTextType.PrivacyNotice,
                    Version = "2026-07-15",
                    Title = "KVKK Aydınlatma Metni",
                    Body = "Bu metin demo teknik şablondur. Hukuki içerik veri sorumlusu ve hukuk danışmanı tarafından onaylanmalıdır.",
                    IsActive = true,
                    PublishedAt = now
                },
                new LegalText
                {
                    Id = Guid.NewGuid(),
                    Type = LegalTextType.ExplicitConsent,
                    Version = "2026-07-15",
                    Title = "Açık Rıza Metni",
                    Body = "Bu açık rıza metni demo teknik şablondur.",
                    IsActive = true,
                    PublishedAt = now
                },
                new LegalText
                {
                    Id = Guid.NewGuid(),
                    Type = LegalTextType.CookiePolicy,
                    Version = "2026-07-15",
                    Title = "Çerez Politikası",
                    Body = "Zorunlu olmayan çerezler kullanıcı onayı olmadan çalıştırılmaz.",
                    IsActive = true,
                    PublishedAt = now
                });
        }

        if (!await db.AdminUsers.AnyAsync())
        {
            var admin = new AdminUser
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Email = "admin@basvuruakis.local",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("ChangeMe!12345", workFactor: 12),
                MfaEnabled = false,
                CreatedAt = now
            };
            admin.Permissions = Permissions.SuperAdmin.Select(permission => new AdminUserPermission
            {
                AdminUserId = admin.Id,
                Permission = permission
            }).ToList();
            db.AdminUsers.Add(admin);
        }

        await db.SaveChangesAsync();
    }
}
