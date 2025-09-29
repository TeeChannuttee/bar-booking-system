using BarBookingSystem.Data;
using BarBookingSystem.Models;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

public static class DataSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        // Create Roles
        string[] roles = { "Admin", "Manager", "Staff", "Customer" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Create Admin User
        if (!context.Users.Any())
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@barbooking.com",
                Email = "admin@barbooking.com",
                FullName = "System Administrator",
                PhoneNumber = "0812345678",
                EmailConfirmed = true,
                MemberTier = "Platinum",
                CreatedAt = DateTime.UtcNow
            };

            var result = await userManager.CreateAsync(admin, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }

            // Create Test Customer
            var customer = new ApplicationUser
            {
                UserName = "customer@test.com",
                Email = "customer@test.com",
                FullName = "Test Customer",
                PhoneNumber = "0891234567",
                EmailConfirmed = true,
                MemberTier = "Silver",
                LoyaltyPoints = 500,
                CreatedAt = DateTime.UtcNow
            };

            result = await userManager.CreateAsync(customer, "Test@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(customer, "Customer");
            }
        }

        // Seed Branches
        if (!context.Branches.Any())
        {
            var branches = new List<Branch>
                {
                    new Branch
                    {
                        Name = "สาขาทองหล่อ",
                        Address = "Thonglor Soi 10, Sukhumvit 55, Bangkok 10110",
                        Phone = "02-123-4567",
                        OpeningHours = @"{""mon-thu"":""17:00-01:00"",""fri-sun"":""17:00-02:00""}",
                        Latitude = 13.7325,
                        Longitude = 100.5835,
                       ImageUrl = "https://img.wongnai.com/p/624x0/2014/07/15/83ee3ec5288c4d7cae96673f0bc811f3.jpg",
                        Description = "บาร์สุดหรูย่านทองหล่อ บรรยากาศดี เหมาะสำหรับปาร์ตี้และสังสรรค์",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Branch
                    {
                        Name = "สาขาเอกมัย",
                        Address = "Ekkamai Soi 5, Sukhumvit 63, Bangkok 10110",
                        Phone = "02-234-5678",
                        OpeningHours = @"{""mon-thu"":""17:00-01:00"",""fri-sun"":""17:00-02:00""}",
                        Latitude = 13.7225,
                        Longitude = 100.5885,
                        ImageUrl = "https://media.timeout.com/images/106015766/image.jpg",
                        Description = "บาร์สไตล์โมเดิร์น ดนตรีสด ทุกวันศุกร์-เสาร์",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new Branch
                    {
                        Name = "สาขาอารีย์",
                        Address = "Ari Soi 4, Phahonyothin, Bangkok 10400",
                        Phone = "02-345-6789",
                        OpeningHours = @"{""mon-thu"":""17:00-00:00"",""fri-sun"":""17:00-02:00""}",
                        Latitude = 13.7802,
                        Longitude = 100.5445,
                        ImageUrl = "https://unseenthailand.tours/wp-content/uploads/2025/04/cover_large_p1g18hdeev1al6skcjduqg1ucg5-1024x576.jpg",
                        Description = "บาร์บรรยากาศชิลล์ ย่านอารีย์",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    }
                };

            context.Branches.AddRange(branches);
            await context.SaveChangesAsync();
        }

        // Seed Tables
        if (!context.Tables.Any())
        {
            var branches = context.Branches.ToList();
            var tables = new List<Table>();

            foreach (var branch in branches)
            {
                // Standard Tables
                for (int i = 1; i <= 5; i++)
                {
                    tables.Add(new Table
                    {
                        TableNumber = $"A{i:00}",
                        BranchId = branch.Id,
                        Zone = "Indoor",
                        TableType = "Standard",
                        Capacity = 4,
                        MinimumSpend = 1500,
                        BasePrice = 0,
                        FloorPosition = $"{10 * i},10",
                        IsActive = true
                    });
                }

                // Outdoor Tables
                for (int i = 1; i <= 3; i++)
                {
                    tables.Add(new Table
                    {
                        TableNumber = $"B{i:00}",
                        BranchId = branch.Id,
                        Zone = "Outdoor",
                        TableType = "Standard",
                        Capacity = 6,
                        MinimumSpend = 2000,
                        BasePrice = 0,
                        FloorPosition = $"{10 * i},20",
                        IsActive = true
                    });
                }

                // VIP Tables
                for (int i = 1; i <= 2; i++)
                {
                    tables.Add(new Table
                    {
                        TableNumber = $"V{i:00}",
                        BranchId = branch.Id,
                        Zone = "VIP",
                        TableType = "VIP",
                        Capacity = 8,
                        MinimumSpend = 5000,
                        BasePrice = 500,
                        FloorPosition = $"{20 * i},30",
                        IsActive = true
                    });
                }

                // Private Room
                tables.Add(new Table
                {
                    TableNumber = "P01",
                    BranchId = branch.Id,
                    Zone = "Private",
                    TableType = "Private",
                    Capacity = 15,
                    MinimumSpend = 10000,
                    BasePrice = 1500,
                    FloorPosition = "50,40",
                    IsActive = true,
                    Notes = "Private room with karaoke system"
                });
            }

            context.Tables.AddRange(tables);
            await context.SaveChangesAsync();
        }

        // Seed Promo Codes
        if (!context.PromoCodes.Any())
        {
            var promoCodes = new List<PromoCode>
                {
                    new PromoCode
                    {
                        Code = "WELCOME20",
                        Description = "ส่วนลด 20% สำหรับลูกค้าใหม่",
                        DiscountPercent = 20,
                        DiscountAmount = 0,
                        MinimumSpend = 1000,
                        ValidFrom = DateTime.UtcNow,
                        ValidTo = DateTime.UtcNow.AddMonths(3),
                        MaxUses = 100,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new PromoCode
                    {
                        Code = "HAPPY500",
                        Description = "ลด 500 บาท Happy Hour 17:00-19:00",
                        DiscountPercent = 0,
                        DiscountAmount = 500,
                        MinimumSpend = 2000,
                        ValidFrom = DateTime.UtcNow,
                        ValidTo = DateTime.UtcNow.AddMonths(1),
                        MaxUses = 50,
                        ApplicableDays = @"[""Monday"",""Tuesday"",""Wednesday"",""Thursday""]",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new PromoCode
                    {
                        Code = "VIP1000",
                        Description = "ลด 1,000 บาท สำหรับโต๊ะ VIP",
                        DiscountPercent = 0,
                        DiscountAmount = 1000,
                        MinimumSpend = 5000,
                        ValidFrom = DateTime.UtcNow,
                        ValidTo = DateTime.UtcNow.AddMonths(2),
                        MaxUses = 30,
                        ApplicableZones = @"[""VIP"",""Private""]",
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    },
                    new PromoCode
                    {
                        Code = "BIRTHDAY30",
                        Description = "ลด 30% วันเกิด (ใช้ได้ 7 วันก่อน-หลังวันเกิด)",
                        DiscountPercent = 30,
                        DiscountAmount = 0,
                        MinimumSpend = 1500,
                        ValidFrom = DateTime.UtcNow,
                        ValidTo = DateTime.UtcNow.AddYears(1),
                        MaxUses = 1000,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    }
                };

            context.PromoCodes.AddRange(promoCodes);
            await context.SaveChangesAsync();
        }
    }
}

