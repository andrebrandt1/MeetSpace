using System.Net;
using System.Net.Http.Json;
using Dapper;
using MeetSpace.Api.Bookings;
using MeetSpace.Api.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MeetSpace.Api.Tests;

public sealed class EmployeeTests(SqlServerFixture database) : IClassFixture<SqlServerFixture>
{
    public static bool IsSupported => SqlServerFixture.IsSupported;
    private const string SkipReason = "SQL-tester kräver Windows LocalDB eller MEETSPACE_TEST_SQLSERVER.";

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Get_all_returns_all_employees()
    {
        await ResetAsync();
        var anna = await InsertAsync("Anna Andersson", "anna@example.com");
        var bo = await InsertAsync("Bo Berg", "bo@example.com");
        using var client = CreateClient();

        var employees = await client.GetFromJsonAsync<List<EmployeeDto>>("/api/employees", Token);

        Assert.Equal([anna, bo], employees);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Get_by_id_returns_employee()
    {
        await ResetAsync();
        var anna = await InsertAsync("Anna Andersson", "anna@example.com");
        using var client = CreateClient();

        var employee = await client.GetFromJsonAsync<EmployeeDto>($"/api/employees/{anna.Id}", Token);

        Assert.Equal(anna, employee);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Get_by_id_returns_not_found_for_missing_employee()
    {
        await ResetAsync();
        using var client = CreateClient();

        using var response = await client.GetAsync("/api/employees/999999", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Create_returns_created_and_persists_employee()
    {
        await ResetAsync();
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/employees",
            new CreateEmployeeDto { Name = "Anna Andersson", Email = "anna@example.com" }, Token);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<EmployeeDto>(Token);
        Assert.NotNull(created);
        Assert.Equal($"/api/employees/{created.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(created, await GetAsync(created.Id));
    }

    [Theory(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    [InlineData("", "anna@example.com", "name")]
    [InlineData("A", "anna@example.com", "name")]
    [InlineData(" Anna", "anna@example.com", "name")]
    [InlineData("Anna Andersson", "", "email")]
    [InlineData("Anna Andersson", "inte-en-adress", "email")]
    [InlineData("Anna Andersson", "anna@example", "email")]
    [InlineData("Anna Andersson", "Anna <anna@example.com>", "email")]
    public async Task Create_with_invalid_input_returns_bad_request(string name, string email, string field)
    {
        await ResetAsync();
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/employees",
            new CreateEmployeeDto { Name = name, Email = email }, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(Token);
        Assert.Contains(field, problem!.Errors.Keys);
        Assert.Equal(0, await CountAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Create_with_existing_email_returns_conflict()
    {
        await ResetAsync();
        await InsertAsync("Anna Andersson", "anna@example.com");
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/employees",
            new CreateEmployeeDto { Name = "Annan Anna", Email = "anna@example.com" }, Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(1, await CountAsync());
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Update_returns_no_content_and_persists_changes()
    {
        await ResetAsync();
        var anna = await InsertAsync("Anna Andersson", "anna@example.com");
        using var client = CreateClient();

        using var response = await client.PutAsJsonAsync($"/api/employees/{anna.Id}",
            new UpdateEmployeeDto { Name = "Anna Berg", Email = "anna.berg@example.com" }, Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(new EmployeeDto { Id = anna.Id, Name = "Anna Berg", Email = "anna.berg@example.com" },
            await GetAsync(anna.Id));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Update_keeping_own_email_is_allowed()
    {
        await ResetAsync();
        var anna = await InsertAsync("Anna Andersson", "anna@example.com");
        using var client = CreateClient();

        using var response = await client.PutAsJsonAsync($"/api/employees/{anna.Id}",
            new UpdateEmployeeDto { Name = "Anna Berg", Email = "anna@example.com" }, Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Update_returns_not_found_for_missing_employee()
    {
        await ResetAsync();
        using var client = CreateClient();

        using var response = await client.PutAsJsonAsync("/api/employees/999999",
            new UpdateEmployeeDto { Name = "Anna Berg", Email = "anna@example.com" }, Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Update_with_invalid_input_returns_bad_request_and_keeps_row()
    {
        await ResetAsync();
        var anna = await InsertAsync("Anna Andersson", "anna@example.com");
        using var client = CreateClient();

        using var response = await client.PutAsJsonAsync($"/api/employees/{anna.Id}",
            new UpdateEmployeeDto { Name = "", Email = "anna@" }, Token);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblem>(Token);
        Assert.Contains("name", problem!.Errors.Keys);
        Assert.Contains("email", problem.Errors.Keys);
        Assert.Equal(anna, await GetAsync(anna.Id));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Update_to_email_of_another_employee_returns_conflict_and_keeps_row()
    {
        await ResetAsync();
        var anna = await InsertAsync("Anna Andersson", "anna@example.com");
        await InsertAsync("Bo Berg", "bo@example.com");
        using var client = CreateClient();

        using var response = await client.PutAsJsonAsync($"/api/employees/{anna.Id}",
            new UpdateEmployeeDto { Name = "Anna Andersson", Email = "bo@example.com" }, Token);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(anna, await GetAsync(anna.Id));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Delete_returns_no_content_and_removes_employee()
    {
        await ResetAsync();
        var anna = await InsertAsync("Anna Andersson", "anna@example.com");
        using var client = CreateClient();

        using var response = await client.DeleteAsync($"/api/employees/{anna.Id}", Token);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(await GetAsync(anna.Id));
    }

    [Fact(Skip = SkipReason, SkipUnless = nameof(IsSupported))]
    public async Task Delete_returns_not_found_for_missing_employee()
    {
        await ResetAsync();
        using var client = CreateClient();

        using var response = await client.DeleteAsync("/api/employees/999999", Token);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed record ValidationProblem(Dictionary<string, string[]> Errors);

    private HttpClient CreateClient()
    {
        var factory = new EmployeeApiFactory(database.ConnectionString);
        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
    }

    private async Task ResetAsync()
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.ExecuteAsync("DELETE FROM dbo.Employees;");
    }

    private async Task<int> CountAsync()
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        return await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.Employees;");
    }

    private async Task<EmployeeDto?> GetAsync(int id)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        return await connection.QuerySingleOrDefaultAsync<EmployeeDto>(
            "SELECT Id, Name, Email FROM dbo.Employees WHERE Id = @Id;", new { Id = id });
    }

    private async Task<EmployeeDto> InsertAsync(string name, string email)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        var id = await connection.ExecuteScalarAsync<int>(
            "INSERT INTO dbo.Employees (Name, Email) OUTPUT INSERTED.Id VALUES (@Name, @Email);",
            new { Name = name, Email = email });
        return new EmployeeDto { Id = id, Name = name, Email = email };
    }
}

// Employee-endpoints läser anslutningssträngen "Prod". Program.cs saknar för närvarande
// registreringen av IBookingRepository, vilket stoppar appstarten; den läggs bara till här.
public sealed class EmployeeApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Prod"] = connectionString,
                ["ConnectionStrings:DefaultConnection"] = connectionString
            }));
        builder.ConfigureServices(services =>
            services.TryAddScoped<IBookingRepository, SqlBookingRepository>());
    }
}
