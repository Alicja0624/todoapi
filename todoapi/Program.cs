using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using todoapi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TodoDb>(opt => opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

var app = builder.Build();

await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<TodoDb>();

await db.Database.EnsureCreatedAsync();

app.MapGet("/listtasks", async (TodoDb db) =>
    await db.Todos.Where(t => t.User == null).ToListAsync());

app.MapPost("/account/listtasks", async (RegisterReq request1, TodoDb db, IPasswordHasher<User> hasher) =>
{
    if (string.IsNullOrEmpty(request1.Password))
    {
        request1.Password = "";
    }
    // log in
    var user = await db.Users.SingleOrDefaultAsync(u => u.Username == request1.Username);
    if (user == null)
    {
        return Results.BadRequest("Nie istnieje konto o takim loginie.");
    }
    var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, request1.Password);
    if (verificationResult == PasswordVerificationResult.Failed)
    {
        return Results.BadRequest("Nieprawid³owe has³o.");
    }

    var User = await db.Users.Include(u => u.Todos).SingleAsync(u => u.Id == user.Id);

    return Results.Ok(await db.Todos.Where(t => t.User == User).ToListAsync());
});

// app.MapGet("/users", async (TodoDb db) =>
//     await db.Users.ToListAsync());

app.MapGet("/taskinfo/{id}", async (int id, TodoDb db) =>
    // only public todos
    await db.Todos.FindAsync(id)
        is Todo todo
        && todo.User == null
            ? Results.Ok(todo)
            : Results.NotFound());

app.MapPost("/account/taskinfo/{id}", async (int id, RegisterReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{   if (string.IsNullOrEmpty(request.Password))
    {
        request.Password = "";
    }
    // log in
    var user = await db.Users.SingleOrDefaultAsync(u => u.Username == request.Username);
    if (user == null)
    {
        return Results.BadRequest("Nie istnieje konto o takim loginie.");
    }
    var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
    if (verificationResult == PasswordVerificationResult.Failed)
    {
        return Results.BadRequest("Nieprawid³owe has³o.");
    }
    var todo = await db.Todos.FindAsync(id);
    if (todo is null) return Results.NotFound();
    if (todo.UserId != user.Id) return Results.Unauthorized();
    return Results.Ok(todo);
});

app.MapPost("/addtask", async (Todo todo, TodoDb db) =>
{
    // nazwa zadania nie mo¿e byæ pusta ani równa null
    if (string.IsNullOrEmpty(todo.Name))
    {
        return Results.BadRequest("Zadanie musi mieæ nazwê.");
    }
    // opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
    todo.Description ??= "";

    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Created($"/taskinfo/{todo.Id}", todo);
});

app.MapPost("/addchildtask/{parentId}", async (int parentId, Todo todo, TodoDb db) =>
{
    // nazwa zadania nie mo¿e byæ pusta ani równa null
    if (string.IsNullOrEmpty(todo.Name))
    {
        return Results.BadRequest("Zadanie musi mieæ nazwê.");
    }
    // opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
    todo.Description ??= "";

    var parentTask = await db.Todos.FindAsync(parentId);
    if (parentTask == null)
    {
        return Results.BadRequest("Zadanie nadrzêdne nie istnieje.");
    }
    todo.ParentTaskId = parentId;
    db.Todos.Add(todo);
    await db.SaveChangesAsync();
    return Results.Created($"/taskinfo/{todo.Id}", todo);
});

app.MapPost("/account/addtask", async (TaskReq taskReq, TodoDb db, IPasswordHasher<User> hasher) =>
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
        UserId = user.Id,
        CreatedAt = DateTime.UtcNow,
        DueDate = taskReq.DueDate
    };

    // nazwa zadania nie mo¿e byæ pusta ani równa null
    if (string.IsNullOrEmpty(todo.Name))
    {
        return Results.BadRequest("Zadanie musi mieæ nazwê.");
    }
    // opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
    todo.Description ??= "";

    db.Todos.Add(todo);
    await db.SaveChangesAsync();
    return Results.Created($"/account/taskinfo/{todo.Id}", todo);
});

app.MapPost("/account/addchildtask/{parentId}", async (int parentId, TaskReq taskReq, TodoDb db, IPasswordHasher<User> hasher) =>
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
    var parentTask = await db.Todos.FindAsync(parentId);
    if (parentTask == null)
    {
        return Results.BadRequest("Zadanie nadrzêdne nie istnieje.");
    }
    var todo = new Todo
    {
        Name = taskReq.Name,
        Description = taskReq.Description,
        Priority = taskReq.Priority,
        IsComplete = taskReq.IsComplete,
        User = user,
        UserId = user.Id,
        ParentTaskId = parentId,
        CreatedAt = DateTime.UtcNow,
        DueDate = taskReq.DueDate
    };

    // nazwa zadania nie mo¿e byæ pusta ani równa null
    if (string.IsNullOrEmpty(todo.Name))
    {
        return Results.BadRequest("Zadanie musi mieæ nazwê.");
    }
    // opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
    todo.Description ??= "";

    db.Todos.Add(todo);
    await db.SaveChangesAsync();
    return Results.Created($"/account/taskinfo/{todo.Id}", todo);
});

app.MapPatch("/edittask/{id}", async (int id, Todo inputTodo, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);

    if (todo is null) return Results.NotFound();

    todo.Name = inputTodo.Name;
    todo.IsComplete = inputTodo.IsComplete;
    todo.Description = inputTodo.Description;
    todo.Priority = inputTodo.Priority;
    todo.DueDate = inputTodo.DueDate;

    // nazwa zadania nie mo¿e byæ pusta ani równa null
    if (string.IsNullOrEmpty(todo.Name))
    {
        return Results.BadRequest("Zadanie musi mieæ nazwê.");
    }
    // opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
    todo.Description ??= "";


    await db.SaveChangesAsync();

    return Results.Ok();
});

app.MapPost("/account/edittask/{id}", async (int id, TaskReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{
    if (string.IsNullOrEmpty(request.Password))
    {
        request.Password = "";
    }
    // log in
    var user = await db.Users.SingleOrDefaultAsync(u => u.Username == request.Username);
    if (user == null)
    {
        return Results.BadRequest("Nie istnieje konto o takim loginie.");
    }
    var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
    if (verificationResult == PasswordVerificationResult.Failed)
    {
        return Results.BadRequest("Nieprawid³owe has³o.");
    }
    var todo = await db.Todos.FindAsync(id);
    if (todo is null) return Results.NotFound();
    if (todo.UserId != user.Id) return Results.Unauthorized();

    todo.Name = request.Name;
    todo.Description = request.Description;
    todo.Priority = request.Priority;
    todo.IsComplete = request.IsComplete;
    todo.DueDate = request.DueDate;

    // nazwa zadania nie mo¿e byæ pusta ani równa null
    if (string.IsNullOrEmpty(todo.Name))
    {
        return Results.BadRequest("Zadanie musi mieæ nazwê.");
    }
    // opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
    todo.Description ??= "";

    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPatch("/ticktask/{id}", async (int id, TodoDb db) =>
{
    var todo = await db.Todos.FindAsync(id);

    if (todo is null) return Results.NotFound();
    if (todo.User != null) return Results.Unauthorized();

    todo.IsComplete = !todo.IsComplete;
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPatch("/account/ticktask/{id}", async (int id, RegisterReq request2, TodoDb db, IPasswordHasher<User> hasher) =>
{
    if (string.IsNullOrEmpty(request2.Password))
    {
        request2.Password = "";
    }
    // log in
    var user = await db.Users.SingleOrDefaultAsync(u => u.Username == request2.Username);
    if (user == null)
    {
        return Results.BadRequest("Nie istnieje konto o takim loginie.");
    }
    var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, request2.Password);
    if (verificationResult == PasswordVerificationResult.Failed)
    {
        return Results.BadRequest("Nieprawid³owe has³o.");
    }
    var todo = await db.Todos.FindAsync(id);
    if (todo is null) return Results.NotFound();
    if (todo.UserId != user.Id) return Results.Unauthorized();
    todo.IsComplete = !todo.IsComplete;
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapDelete("/deletetask/{id}", async (int id, TodoDb db) =>
{
    if (await db.Todos.FindAsync(id) is Todo todo)
    {
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
        return Results.Ok();
    }

    return Results.NotFound();
});

app.MapPost("/account/deletetask/{id}", async (int id, RegisterReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{
    if (string.IsNullOrEmpty(request.Password))
    {
        request.Password = "";
    }
    // log in
    var user = await db.Users.SingleOrDefaultAsync(u => u.Username == request.Username);
    if (user == null)
    {
        return Results.BadRequest("Nie istnieje konto o takim loginie.");
    }
    var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
    if (verificationResult == PasswordVerificationResult.Failed)
    {
        return Results.BadRequest("Nieprawid³owe has³o.");
    }
    var todo = await db.Todos.FindAsync(id);
    if (todo is null) return Results.NotFound();
    if (todo.UserId != user.Id) return Results.Unauthorized();
    db.Todos.Remove(todo);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/user/remove", async (RegisterReq request3, TodoDb db, IPasswordHasher<User> hasher) =>
{
    if (string.IsNullOrEmpty(request3.Password))
    {
        request3.Password = "";
    }
    // log in
    var user = await db.Users.SingleOrDefaultAsync(u => u.Username == request3.Username);
    if (user == null)
    {
        return Results.BadRequest("Nie istnieje konto o takim loginie.");
    }
    var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, request3.Password);
    if (verificationResult == PasswordVerificationResult.Failed)
    {
        return Results.BadRequest("Nieprawid³owe has³o.");
    }

    db.Users.Remove(user);
    await db.SaveChangesAsync();
    return Results.Ok();
});

app.MapPost("/user/register", async (RegisterReq request4, TodoDb db, IPasswordHasher<User> hasher) =>
{
    // Sprawdzenie, czy u¿ytkownik ju¿ istnieje
    if (await db.Users.AnyAsync(u => u.Username == request4.Username))
    {
        return Results.BadRequest("U¿ytkownik z tym loginem ju¿ istnieje.");
    }

    // Tworzenie nowego u¿ytkownika
    var user = new User
    {
        Username = request4.Username
    };

    // Hashowanie has³a
    user.PasswordHash = hasher.HashPassword(user, request4.Password);

    // Dodanie do bazy
    db.Users.Add(user);
    await db.SaveChangesAsync();

    return Results.Ok(new { user.Id, user.Username });
});

app.Run();