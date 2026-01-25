using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

// ?? AUMENTAR LIMITES DO FORM
builder.Services.Configure<FormOptions>(options =>
{
    options.ValueCountLimit = 5000;   // suficiente para 30x30 
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = long.MaxValue;
});

builder.Services.AddControllersWithViews();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
