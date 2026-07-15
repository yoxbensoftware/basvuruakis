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

        await UpsertContentPagesAsync(db, now);
        await UpsertLegalTextsAsync(db, now);

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

    private static async Task UpsertContentPagesAsync(AppDbContext db, DateTimeOffset now)
    {
        var pages = new[]
        {
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
                Body = "İletişim ve yönlendirme bilgileri yönetim panelinden güncel tutulur.",
                SeoTitle = "İletişim",
                SeoDescription = "BaşvuruAkış iletişim.",
                Status = PublishStatus.Published,
                PublishedAt = now
            }
        };

        foreach (var page in pages)
        {
            var existing = await db.ContentPages.FirstOrDefaultAsync(x => x.Slug == page.Slug);
            if (existing is null)
            {
                db.ContentPages.Add(page);
                continue;
            }

            existing.Title = page.Title;
            existing.Summary = page.Summary;
            existing.Body = page.Body;
            existing.SeoTitle = page.SeoTitle;
            existing.SeoDescription = page.SeoDescription;
            existing.Status = page.Status;
            existing.PublishedAt = page.PublishedAt;
        }
    }

    private static async Task UpsertLegalTextsAsync(AppDbContext db, DateTimeOffset now)
    {
        var legalTexts = new[]
        {
            new LegalText
            {
                Id = Guid.NewGuid(),
                Type = LegalTextType.PrivacyNotice,
                Version = "2026-07-15",
                Title = "KVKK Aydınlatma Metni",
                Body = "Kişisel verileriniz başvurunun alınması, telefon doğrulamasının tamamlanması, ilgili temsilciliğe yönlendirilmesi ve başvuru sürecinin denetlenmesi amacıyla işlenir. Kimlik, iletişim ve adres bilgileri güvenli şekilde saklanır; yalnızca yetkili kullanıcılar tarafından erişilebilir.",
                IsActive = true,
                PublishedAt = now
            },
            new LegalText
            {
                Id = Guid.NewGuid(),
                Type = LegalTextType.ExplicitConsent,
                Version = "2026-07-15",
                Title = "Açık Rıza Metni",
                Body = "Başvuru sürecinin yürütülmesi için kimlik, iletişim ve adres bilgilerimin güvenli sistemlerde işlenmesine ve ilgili temsilcilik birimine aktarılmasına açık rıza veriyorum.",
                IsActive = true,
                PublishedAt = now
            },
            new LegalText
            {
                Id = Guid.NewGuid(),
                Type = LegalTextType.CookiePolicy,
                Version = "2026-07-15",
                Title = "Çerez Politikası",
                Body = "Zorunlu çerezler oturum güvenliği ve tercihlerin saklanması için kullanılır. Zorunlu olmayan çerezler kullanıcı onayı olmadan çalıştırılmaz.",
                IsActive = true,
                PublishedAt = now
            }
        };

        foreach (var legalText in legalTexts)
        {
            var existing = await db.LegalTexts.FirstOrDefaultAsync(x => x.Type == legalText.Type && x.IsActive)
                ?? await db.LegalTexts.FirstOrDefaultAsync(x => x.Type == legalText.Type && x.Version == legalText.Version);

            if (existing is null)
            {
                db.LegalTexts.Add(legalText);
                continue;
            }

            existing.Version = legalText.Version;
            existing.Title = legalText.Title;
            existing.Body = legalText.Body;
            existing.IsActive = true;
            existing.PublishedAt = legalText.PublishedAt;
        }
    }
}
