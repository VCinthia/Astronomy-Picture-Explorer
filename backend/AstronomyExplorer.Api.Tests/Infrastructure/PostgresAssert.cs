using Npgsql;

namespace AstronomyExplorer.Api.Tests.Infrastructure;

internal static class PostgresAssert
{
  public static async Task ThrowsSqlStateAsync(
    string connectionString,
    string commandText,
    string expectedSqlState,
    params NpgsqlParameter[] parameters)
  {
    await using var connection = new NpgsqlConnection(connectionString);
    await connection.OpenAsync();
    await using var command = new NpgsqlCommand(commandText, connection);
    command.Parameters.AddRange(parameters);

    var exception = await Assert.ThrowsAsync<PostgresException>(
      () => command.ExecuteNonQueryAsync());

    Assert.Equal(expectedSqlState, exception.SqlState);
  }
}
