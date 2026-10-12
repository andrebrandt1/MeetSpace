using Dapper;
using MeetSpace.Api.Dtos;
using MeetSpace.Api.Validator;
using Microsoft.Data.SqlClient;

namespace MeetSpace.Api.Endpoints
{
    public static class EmployeeEndpoints
    {
        public static WebApplication MapEmployeeEndpoints(this WebApplication app)
        {
            app.MapGet("/api/employees", () =>
            {
                using var connection = CreateConnection(app);

                var employees = connection.Query<EmployeeDto>(
                    "SELECT Id, Name, Email FROM dbo.Employees ORDER BY Id;").ToList();

                return Results.Ok(employees);
            });

            app.MapGet("/api/employees/{id:int}", (int id) =>
            {
                using var connection = CreateConnection(app);
                var employee = connection.QueryFirstOrDefault<EmployeeDto>(
                    "SELECT Id, Name, Email FROM dbo.Employees WHERE Id = @Id;",
                    new { Id = id });
                if (employee == null)
                {
                    return Results.NotFound();
                }
                return Results.Ok(employee);
            });

            app.MapPost("/api/employees", (CreateEmployeeDto request) =>
            {
                var errors = EmployeeValidator.Validate(request.Name, request.Email);
                if (errors.Count > 0)
                {
                    return Results.ValidationProblem(errors);
                }

                using var connection = CreateConnection(app);
                try
                {
                    var id = connection.ExecuteScalar<int>(
                        "INSERT INTO dbo.Employees (Name, Email) OUTPUT INSERTED.Id VALUES (@Name, @Email);",
                        new { request.Name, request.Email });

                    var employee = new EmployeeDto { Id = id, Name = request.Name, Email = request.Email };
                    return Results.Created($"/api/employees/{id}", employee);
                }
                catch (SqlException ex) when (IsUniqueViolation(ex))
                {
                    return EmailConflict();
                }
            });

            app.MapPut("/api/employees/{id:int}", (int id, UpdateEmployeeDto request) =>
            {
                var errors = EmployeeValidator.Validate(request.Name, request.Email);
                if (errors.Count > 0)
                {
                    return Results.ValidationProblem(errors);
                }

                using var connection = CreateConnection(app);
                try
                {
                    var rows = connection.Execute(
                        "UPDATE dbo.Employees SET Name = @Name, Email = @Email WHERE Id = @Id;",
                        new { Id = id, request.Name, request.Email });

                    return rows == 0 ? Results.NotFound() : Results.NoContent();
                }
                catch (SqlException ex) when (IsUniqueViolation(ex))
                {
                    return EmailConflict();
                }
            });

            app.MapDelete("/api/employees/{id:int}", (int id) =>
            {
                using var connection = CreateConnection(app);

                var rows = connection.Execute(
                    "DELETE FROM dbo.Employees WHERE Id = @Id;",
                    new { Id = id });

                return rows == 0 ? Results.NotFound() : Results.NoContent();
            });

            return app;
        }

        private static SqlConnection CreateConnection(WebApplication app) =>
            new(app.Configuration.GetConnectionString("Prod")
                ?? throw new InvalidOperationException("Anslutningssträngen 'Prod' saknas."));

        // 2627 = unik constraint, 2601 = unikt index.
        private static bool IsUniqueViolation(SqlException ex) => ex.Number is 2627 or 2601;

        private static IResult EmailConflict() => Results.Problem(
            statusCode: StatusCodes.Status409Conflict,
            title: "E-postadressen används redan av en annan medarbetare.");
    }
}
