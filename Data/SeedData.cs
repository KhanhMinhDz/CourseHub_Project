using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CourseManagement.Data
{
    public static class SeedData
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using var scope = serviceProvider.CreateScope();
            var services = scope.ServiceProvider;
            var context = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

            await context.Database.MigrateAsync();

            // Roles
            var roles = new[] { "Admin", "Instructor", "Student" };
            foreach (var r in roles)
            {
                if (await roleManager.FindByNameAsync(r) == null)
                {
                    await roleManager.CreateAsync(new IdentityRole(r));
                }
            }

            // Admin user
            var adminEmail = "admin@local.test";
            if (await userManager.FindByEmailAsync(adminEmail) == null)
            {
                var admin = new ApplicationUser { UserName = adminEmail, Email = adminEmail, FullName = "Site Admin" };
                await userManager.CreateAsync(admin, "Admin123!");
                await userManager.AddToRoleAsync(admin, "Admin");
            }

            // Instructor user
            var instrEmail = "instructor@local.test";
            var instr = await userManager.FindByEmailAsync(instrEmail);
            if (instr == null)
            {
                instr = new ApplicationUser { UserName = instrEmail, Email = instrEmail, FullName = "Demo Instructor" };
                await userManager.CreateAsync(instr, "Instructor123!");
                await userManager.AddToRoleAsync(instr, "Instructor");
            }

            // Student user
            var studEmail = "student@local.test";
            var stud = await userManager.FindByEmailAsync(studEmail);
            if (stud == null)
            {
                stud = new ApplicationUser { UserName = studEmail, Email = studEmail, FullName = "Demo Student" };
                await userManager.CreateAsync(stud, "Student123!");
                await userManager.AddToRoleAsync(stud, "Student");
            }

            // Sample class if none
            if (!await context.ClassRooms.AnyAsync())
            {
                var c = new Models.ClassRoom
                {
                    Title = "Lý thuyết Mác-Lênin (Mẫu)",
                    Description = "Trang lớp mẫu. Giảng viên có thể chỉnh sửa.",
                    InstructorId = instr.Id
                };
                context.ClassRooms.Add(c);
                await context.SaveChangesAsync();
            }
        }
    }
}
