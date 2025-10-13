using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using todoapi;

// bool informuje, czy dzia³anie ma byæ wykonane na publicznych zadaniach (brak nazwy u¿ytkownika)
static (bool, User?, IResult?) Login(AccountReq request, TodoDb db, IPasswordHasher<User> hasher)
{
	if (string.IsNullOrEmpty(request.Username))
	{
		return (true, null, null);
	}

	if (string.IsNullOrEmpty(request.Password))
	{
		request.Password = "";
	}
	// log in
	var user = db.Users.SingleOrDefault(u => u.Username == request.Username);
	if (user == null)
	{
		return (false, null, Results.BadRequest("Nie istnieje konto o takim loginie"));
	}
	var verificationResult = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
	if (verificationResult == PasswordVerificationResult.Failed)
	{
		return (false, null, Results.BadRequest("Nieprawid³owe has³o"));
	}

	return (false, user, null);
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<TodoDb>(opt => opt.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
// builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
	options.AddPolicy("AllowFrontend",
		policy =>
		{
			policy.WithOrigins("https://localhost:7201")
				  .AllowAnyHeader()
				  .AllowAnyMethod();
		});
});


var app = builder.Build();

app.UseCors("AllowFrontend");

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseHttpsRedirection();

await using var scope = app.Services.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<TodoDb>();

await db.Database.EnsureCreatedAsync();

app.MapPost("/listtasks", async(AccountReq request, TodoDb db, IPasswordHasher<User> hasher,
	string? sorting, bool? onlyNotCompleted, int? minPriority) =>
{
	var (isPublic, user, errorResult) = Login(request, db, hasher);

	var query = db.Todos.AsQueryable();

	if (isPublic)
	{
		query = query.Where(t => t.UserId == null);

		if (onlyNotCompleted == true)
		{
			query = query.Where(t => !t.IsComplete);
		}

		if (minPriority != null)
		{
			query = query.Where(t => t.Priority >= minPriority);
		}

		query = sorting switch
		{
			"name" => query.OrderBy(t => t.Name),
			"priority" => query.OrderByDescending(t => t.Priority),
			"oldest" => query.OrderBy(t => t.CreatedAt),
			"newest" => query.OrderByDescending(t => t.CreatedAt),
			_ => query.OrderBy(t => t.DueDate)
		};

		List<Todo> todos1 = [];

		foreach (var todo in query)
		{
			if (todo.ParentTaskId == null)
			{
				todos1.Add(todo);
			}
		}
		query = query.Reverse();
		
		foreach (var todo in query)
		{
			if (todo.ParentTaskId != null)
			{
				todos1.Insert(todos1.FindIndex(t => t.Id == todo.ParentTaskId) + 1, todo);
			}
		}

		var query_copy1 = await query.ToListAsync();

		foreach (var todo in query_copy1)
		{
			if (todo.ParentTaskId != null)
			{
				if (!todos1.Any(t => t.Id == todo.ParentTaskId))
				{
					var parent = await db.Todos.FindAsync(todo.ParentTaskId);
					if (parent != null)
					{
						todos1.Insert(todos1.FindIndex(t => t.Id == todo.Id), parent);
					}
				}
			}
		}

		return Results.Ok(todos1);
	}

	if (user is null) return errorResult;

	//var User = await db.Users.Include(u => u.Todos).SingleAsync(u => u.Id == user.Id);

	query = query.Where(t => t.UserId == user.Id);

	if (onlyNotCompleted == true)
	{
		query = query.Where(t => !t.IsComplete);
	}

	if (minPriority != null)
	{
		query = query.Where(t => t.Priority >= minPriority);
	}

	query = sorting switch
	{
		"name" => query.OrderBy(t => t.Name),
		"priority" => query.OrderByDescending(t => t.Priority),
		"oldest" => query.OrderBy(t => t.CreatedAt),
		"newest" => query.OrderByDescending(t => t.CreatedAt),
		_ => query.OrderBy(t => t.DueDate)
	};

	List<Todo> todos = [];

	foreach (var todo in query)
	{
		if (todo.ParentTaskId == null)
		{
			todos.Add(todo);
		}
	}
	query = query.Reverse();

	foreach (var todo in query)
	{
		if (todo.ParentTaskId != null)
		{
			todos.Insert(todos.FindIndex(t => t.Id == todo.ParentTaskId) + 1, todo);
		}
	}

	var query_copy = await query.ToListAsync();

	foreach (var todo in query_copy)
	{
		if (todo.ParentTaskId != null)
		{
			if (!todos.Any(t => t.Id == todo.ParentTaskId))
			{
				var parent = await db.Todos.FindAsync(todo.ParentTaskId);
				if (parent != null)
				{
					todos.Insert(todos.FindIndex(t => t.Id == todo.Id), parent);
				}
			}
		}
	}

	return Results.Ok(todos);
});

// app.MapGet("/users", async (TodoDb db) =>
//     await db.Users.ToListAsync());

app.MapPost("/taskinfo/{id}", async (int id, AccountReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{
	var (isPublic, user, errorResult) = Login(request, db, hasher);

	if (isPublic)
	{
		var todo1 = await db.Todos.FindAsync(id);
		if (todo1 is null) return Results.NotFound();
		if (todo1.UserId != null) return Results.Unauthorized();
		return Results.Ok(todo1);
	}

	if (user is null) return errorResult;

	var todo = await db.Todos.FindAsync(id);
	if (todo is null) return Results.NotFound();
	if (todo.UserId != user.Id) return Results.Unauthorized();
	return Results.Ok(todo);
});

app.MapPost("/addtask", async (TaskReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{
	var (isPublic, user, errorResult) = Login(request, db, hasher);

	if (isPublic)
	{
		if (request.DueDate is not null)
		{
			request.DueDate = DateTime.SpecifyKind((DateTime)request.DueDate, DateTimeKind.Utc);
		}

		var todo1 = new Todo
		{
			Name = request.Name,
			Description = request.Description,
			Priority = request.Priority,
			IsComplete = request.IsComplete,
			CreatedAt = DateTime.UtcNow,
			DueDate = request.DueDate
		};
		// nazwa zadania nie mo¿e byæ pusta ani równa null
		if (string.IsNullOrEmpty(todo1.Name))
		{
			return Results.BadRequest("Zadanie musi mieæ nazwê");
		}
		// opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
		todo1.Description ??= "";
		db.Todos.Add(todo1);
		await db.SaveChangesAsync();
		return Results.Created($"/taskinfo/{todo1.Id}", todo1);
	}

	if (user is null) return errorResult;

	if (request.DueDate is not null)
	{
		request.DueDate = DateTime.SpecifyKind((DateTime)request.DueDate, DateTimeKind.Utc);
	}

	var todo = new Todo
	{
		Name = request.Name,
		Description = request.Description,
		Priority = request.Priority,
		IsComplete = request.IsComplete,
		User = user,
		UserId = user.Id,
		CreatedAt = DateTime.UtcNow,
		DueDate = request.DueDate
	};

	// nazwa zadania nie mo¿e byæ pusta ani równa null
	if (string.IsNullOrEmpty(todo.Name))
	{
		return Results.BadRequest("Zadanie musi mieæ nazwê");
	}
	// opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
	todo.Description ??= "";

	db.Todos.Add(todo);
	await db.SaveChangesAsync();
	return Results.Created($"/taskinfo/{todo.Id}", todo);
});

app.MapPost("/addchildtask/{parentId}", async (int parentId, TaskReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{
	var (isPublic, user, errorResult) = Login(request, db, hasher);

	if (isPublic)
	{
		var parentTask1 = await db.Todos.FindAsync(parentId);
		if (parentTask1 == null)
		{
			return Results.BadRequest("Zadanie nadrzêdne nie istnieje");
		}
		if (parentTask1.UserId != null)
		{
			return Results.Unauthorized();
		}
		if (parentTask1.ParentTaskId != null)
		{
			return Results.BadRequest("Nie mo¿na dodaæ zadania podrzêdnego do zadania podrzêdnego");
		}

		if (request.DueDate is not null)
		{
			request.DueDate = DateTime.SpecifyKind((DateTime)request.DueDate, DateTimeKind.Utc);
		}

		var todo1 = new Todo
		{
			Name = request.Name,
			Description = request.Description,
			Priority = request.Priority,
			IsComplete = request.IsComplete,
			ParentTaskId = parentId,
			CreatedAt = DateTime.UtcNow,
			DueDate = request.DueDate
		};

		// nazwa zadania nie mo¿e byæ pusta ani równa null
		if (string.IsNullOrEmpty(todo1.Name))
		{
			return Results.BadRequest("Zadanie musi mieæ nazwê");
		}
		// opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
		todo1.Description ??= "";

		db.Todos.Add(todo1);
		await db.SaveChangesAsync();
		return Results.Ok(todo1.Id);
	}

	if (user is null) return errorResult;

	var parentTask = await db.Todos.FindAsync(parentId);
	if (parentTask == null)
	{
		return Results.BadRequest("Zadanie nadrzêdne nie istnieje");
	}
	if (parentTask.UserId != user.Id)
	{
		return Results.Unauthorized();
	}
	if (parentTask.ParentTaskId != null)
	{
		return Results.BadRequest("Nie mo¿na dodaæ zadania podrzêdnego do zadania podrzêdnego");
	}

	if (request.DueDate is not null)
	{
		request.DueDate = DateTime.SpecifyKind((DateTime)request.DueDate, DateTimeKind.Utc);
	}

	var todo = new Todo
	{
		Name = request.Name,
		Description = request.Description,
		Priority = request.Priority,
		IsComplete = request.IsComplete,
		User = user,
		UserId = user.Id,
		ParentTaskId = parentId,
		CreatedAt = DateTime.UtcNow,
		DueDate = request.DueDate
	};

	// nazwa zadania nie mo¿e byæ pusta ani równa null
	if (string.IsNullOrEmpty(todo.Name))
	{
		return Results.BadRequest("Zadanie musi mieæ nazwê");
	}
	// opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
	todo.Description ??= "";

	db.Todos.Add(todo);
	await db.SaveChangesAsync();
	return Results.Ok(todo.Id);
});

app.MapPatch("/edittask/{id}", async (int id, TaskReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{
	var (isPublic, user, errorResult) = Login(request, db, hasher);

	if (isPublic)
	{
		var todo1 = await db.Todos.FindAsync(id);
		if (todo1 is null) return Results.NotFound();
		if (todo1.UserId != null) return Results.Unauthorized();

		if (request.DueDate is not null)
		{
			request.DueDate = DateTime.SpecifyKind((DateTime)request.DueDate, DateTimeKind.Utc);
		}

		todo1.Name = request.Name;
		todo1.Description = request.Description;
		todo1.Priority = request.Priority;
		todo1.IsComplete = request.IsComplete;
		todo1.DueDate = request.DueDate;
		// nazwa zadania nie mo¿e byæ pusta ani równa null
		if (string.IsNullOrEmpty(todo1.Name))
		{
			return Results.BadRequest("Zadanie musi mieæ nazwê");
		}
		// opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
		todo1.Description ??= "";
		await db.SaveChangesAsync();
		return Results.Ok();
	}

	if (user is null) return errorResult;

	var todo = await db.Todos.FindAsync(id);
	if (todo is null) return Results.NotFound();
	if (todo.UserId != user.Id) return Results.Unauthorized();

	if (request.DueDate is not null)
	{
		request.DueDate = DateTime.SpecifyKind((DateTime)request.DueDate, DateTimeKind.Utc);
	}

	todo.Name = request.Name;
	todo.Description = request.Description;
	todo.Priority = request.Priority;
	todo.IsComplete = request.IsComplete;
	todo.DueDate = request.DueDate;

	// nazwa zadania nie mo¿e byæ pusta ani równa null
	if (string.IsNullOrEmpty(todo.Name))
	{
		return Results.BadRequest("Zadanie musi mieæ nazwê");
	}
	// opis zadania nie mo¿e byæ równy null ale mo¿e byæ pusty
	todo.Description ??= "";

	await db.SaveChangesAsync();
	return Results.Ok();
});

app.MapPatch("/ticktask/{id}", async (int id, AccountReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{
	var (isPublic, user, errorResult) = Login(request, db, hasher);

	if (isPublic)
	{
		var todo1 = await db.Todos.FindAsync(id);
		if (todo1 is null) return Results.NotFound();
		if (todo1.UserId != null) return Results.Unauthorized();
		todo1.IsComplete = !todo1.IsComplete;
		await db.SaveChangesAsync();
		return Results.Ok();
	}

	if (user is null) return errorResult;

	var todo = await db.Todos.FindAsync(id);
	if (todo is null) return Results.NotFound();
	if (todo.UserId != user.Id) return Results.Unauthorized();
	todo.IsComplete = !todo.IsComplete;
	await db.SaveChangesAsync();
	return Results.Ok();
});

app.MapPost("/deletetask/{id}", async (int id, AccountReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{
	var (isPublic, user, errorResult) = Login(request, db, hasher);

	if (isPublic)
	{
		var todo1 = await db.Todos.FindAsync(id);
		if (todo1 is null) return Results.NotFound();
		if (todo1.UserId != null) return Results.Unauthorized();
		db.Todos.Remove(todo1);
		await db.SaveChangesAsync();
		return Results.Ok();
	}

	if (user is null) return errorResult;

	var todo = await db.Todos.FindAsync(id);
	if (todo is null) return Results.NotFound();
	if (todo.UserId != user.Id) return Results.Unauthorized();
	db.Todos.Remove(todo);
	await db.SaveChangesAsync();
	return Results.Ok();
});

app.MapPost("/user/remove", async (AccountReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{
	var (isPublic, user, errorResult) = Login(request, db, hasher);

	if (isPublic)
	{
		return Results.BadRequest("Usuniêcie konta wymaga zalogowania siê na nie");
	}

	if (user is null) return errorResult;

	db.Users.Remove(user);
	await db.SaveChangesAsync();
	return Results.Ok();
});

app.MapPost("/user/register", async (AccountReq request, TodoDb db, IPasswordHasher<User> hasher) =>
{
	if (string.IsNullOrEmpty(request.Username))
	{
		return Results.BadRequest("Nazwa u¿ytkownika nie mo¿e byæ pusta");
	}
	// Sprawdzenie, czy u¿ytkownik ju¿ istnieje
	if (await db.Users.AnyAsync(u => u.Username == request.Username))
	{
		return Results.BadRequest("U¿ytkownik z tym loginem ju¿ istnieje");
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

app.MapGet("/", () => Results.Ok("Hello"));

app.Run();