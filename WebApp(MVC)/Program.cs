using Microsoft.EntityFrameworkCore;
using WebApp_MVC_.Models;


var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

// Register the DbContext with dependency injection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MyConStr")));

// Add controllers and views
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Default route configuration
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}"); // Default to Validation controller and Index action

app.MapControllers();  // Ensure this line is present to map API controllers

app.Run();
