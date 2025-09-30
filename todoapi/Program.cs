using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using System;
using todoapi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TodoDb>(opt => opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var app = builder.Build();

await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<TodoDb>();

await db.Database.EnsureCreatedAsync();

app.MapGet("/todoitems", async (TodoDb db) =>
    await db.Todos.ToListAsync());

app.MapGet("/users", async (TodoDb db) =>
    await db.Users.ToListAsync());

app.MapGet("/todoitems/complete", async (TodoDb db) =>
    await db.Todos.Where(t => t.IsComplete).ToListAsync());

app.MapGet("/todoitems/{id}", async (int id, TodoDb db) =>
    await db.Todos.FindAsync(id)
        is Todo todo
            ? Results.Ok(todo)
            : Results.NotFound());

app.MapPost("/todoitems", async (Todo todo, TodoDb db) =>
{
    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Created($"/todoitems/{todo.Id}", todo);
});

app.MapPost("/account/todoitems", async (TaskReq taskReq, TodoDb db, IPasswordHasher<User> hasher) =>
{
    if (string.IsNullOrEmpty(taskReq.Password))
    {
        taskReq.Password = "";
    }
    // log in
    var user = await db.Users.SingleOrDefaultAsync(u => u.Username == taskReq.Username);
    if (user == null)
    {
        return Results.BadRequest("Nie istnieje konto o takim loginie.");
    }
    var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, taskReq.Password);
    if (verificationResult == PasswordVerificationResult.Failed)
    {
        return Results.BadRequest("Nieprawid³owe has³o.");
    }

    var todo = new Todo
    {
        Name = taskReq.Name,
        Description = taskReq.Description,
        Priority = taskReq.Priority,
        IsComplete = taskReq.IsComplete,
        User = user,
        UserId = user.Id
    };

    db.Todos.Add(todo);
    await db.SaveChangesAsync();
    return Results.Created($"/todoitems/{todo.Id}", todo);
});

app.MapPut("/todoitems/{id}", async (int id, Todo inputTodo, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);

    if (todo is null) return Results.NotFound();

    todo.Name = inputTodo.Name;
    todo.IsComplete = inputTodo.IsComplete;

    await db.SaveChangesAsync();

    return Results.NoContent();
});

app.MapDelete("/todoitems/{id}", async (int id, TodoDb db) =>
{
    if (await db.Todos.FindAsync(id) is Todo todo)
    {
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }

    return Results.NotFound();
});

app.MapDelete("/users/{id}", async (int id, TodoDb db) =>
{
    if (await db.Users.FindAsync(id) is User user)
    {
        db.Users.Remove(user);
        await db.SaveChangesAsync();
        return Results.NoContent();
    }
    return Results.NotFound();
});

app.MapPost("/account/register", async (RegisterReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{
    // Sprawdzenie, czy u¿ytkownik ju¿ istnieje
    if (await db.Users.AnyAsync(u => u.Username == request.Username))
    {
        return Results.BadRequest("U¿ytkownik z tym loginem ju¿ istnieje.");
    }

    // Tworzenie nowego u¿ytkownika
    var user = new User
    {
        Username = request.Username
    };

    // Hashowanie has³a
    user.PasswordHash = hasher.HashPassword(user, request.Password);

    // Dodanie do bazy
    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(new { user.Id, user.Username });
});

app.Run();