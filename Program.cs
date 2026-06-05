using ASPNetTodoList.Components;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Rewrite;

var builder = WebApplication.CreateBuilder(args);

// Dependency injection for the task service
builder.Services.AddSingleton<ITaskService>(new InMemoryTaskService());
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
app.UseStaticFiles();
app.UseAntiforgery();

// Middleware to rewrite "tasks" to "todos" and log request details
app.UseRewriter(new Microsoft.AspNetCore.Rewrite.RewriteOptions().AddRedirect("tasks", "todos"));

// Middleware to log request details before and after processing
app.Use(async (context, next) =>
{
    Console.WriteLine($"[{context.Request.Method}] {context.Request.Path} {DateTime.UtcNow}] Started.");
    await next(context);
    Console.WriteLine($"[{context.Request.Method}] {context.Request.Path} {DateTime.UtcNow}] Finished.");
}
);

var todos = new List<Todo>();

// CRUD API for Todo items

// GET - get a todo item
app.MapGet("/todos/{id}", Results<Ok<Todo>, NotFound> (int id, ITaskService service) =>
{
    var targetTodo = service.GetTodoById(id);
    return targetTodo is null ? TypedResults.NotFound() : TypedResults.Ok(targetTodo);
});

// GET - all todos
app.MapGet("/todos", (ITaskService service) => service.GetTodos());

// POST - add a todo item with an endpoint filter to validate the input
app.MapPost("/todos", (Todo task, ITaskService service) =>
{
    service.AddTodo(task);
    return TypedResults.Created("/todos/{id}", task);
}).AddEndpointFilter(async (context, next) => {
    var taskArgument = context.GetArgument<Todo>(0);
    var errors = new Dictionary<string, string[]>();

    if (taskArgument.DueDate < DateTime.UtcNow)
    {
        errors.Add(nameof(taskArgument.DueDate), new[] { "Due date cannot be in the past." });
    }
    if (taskArgument.IsCompleted)
    {
        errors.Add(nameof(taskArgument.IsCompleted), new[] { "New todos cannot be marked as completed." });
    }

    if (errors.Count > 0)
    {
        return Results.ValidationProblem(errors);
    }

    return await next(context);
});

// DELETE - delete a todo item
app.MapDelete("/todos/{id}", (int id, ITaskService service) =>
{
    service.DeleteTodoById(id);
    return TypedResults.NoContent();
});

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

public class Todo
{
    public Todo(int id, string name, DateTime dueDate, bool isCompleted)
    {
        Id = id;
        Name = name;
        DueDate = dueDate;
        IsCompleted = isCompleted;
    }
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public DateTime DueDate { get; init; }
    public bool IsCompleted { get; set; }
}

public interface ITaskService
{
    Todo? GetTodoById(int id);
    List<Todo> GetTodos();
    void DeleteTodoById(int id);
    Todo AddTodo(Todo task);
    void MarkComplete (int id);
}

public class InMemoryTaskService : ITaskService
{
    private readonly List<Todo> _todos = new();

    public Todo? GetTodoById(int id) => _todos.SingleOrDefault(t => t.Id == id);

    public List<Todo> GetTodos() => _todos;

    public void DeleteTodoById(int id) => _todos.RemoveAll(t => t.Id == id);

    public Todo AddTodo(Todo task)
    {
        _todos.Add(task);
        return task;
    }
    public void MarkComplete(int id)
    {
        var completedTask = _todos.SingleOrDefault(t => t.Id == id);
        if (completedTask != null)
        {
            completedTask.IsCompleted = true;
        }
    }
}