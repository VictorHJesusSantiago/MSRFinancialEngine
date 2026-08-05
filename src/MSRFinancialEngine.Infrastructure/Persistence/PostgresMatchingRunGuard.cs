using Microsoft.EntityFrameworkCore;
using MSRFinancialEngine.Application.Matching;
using Npgsql;

namespace MSRFinancialEngine.Infrastructure.Persistence;

public class PostgresMatchingRunGuard : IMatchingRunGuard
{
    private const int LockNamespace = 0x4D53_5246;

    private static readonly SemaphoreSlim InProcessGate = new(1, 1);
    private static readonly HashSet<Guid> InProcessHeld = new();

    private readonly FinancialEngineDbContext _context;

    public PostgresMatchingRunGuard(FinancialEngineDbContext context)
    {
        _context = context;
    }

    public async Task<IAsyncDisposable?> TryAcquireAsync(Guid companyId, CancellationToken ct = default)
    {
        if (!_context.Database.IsRelational())
            return await TryAcquireInProcessAsync(companyId, ct);

        var key = DeriveLockKey(companyId);
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();

        var openedHere = false;
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            openedHere = true;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT pg_try_advisory_lock(@ns, @key)";
        command.Parameters.Add(new NpgsqlParameter("ns", LockNamespace));
        command.Parameters.Add(new NpgsqlParameter("key", key));

        var acquired = (bool)(await command.ExecuteScalarAsync(ct))!;

        if (!acquired)
        {
            if (openedHere)
                await connection.CloseAsync();

            return null;
        }

        return new AdvisoryLockScope(connection, LockNamespace, key, openedHere);
    }

    private static async Task<IAsyncDisposable?> TryAcquireInProcessAsync(Guid companyId, CancellationToken ct)
    {
        await InProcessGate.WaitAsync(ct);
        try
        {
            if (!InProcessHeld.Add(companyId))
                return null;
        }
        finally
        {
            InProcessGate.Release();
        }

        return new InProcessLockScope(companyId);
    }

    private static int DeriveLockKey(Guid companyId)
    {
        Span<byte> bytes = stackalloc byte[16];
        companyId.TryWriteBytes(bytes);
        return BitConverter.ToInt32(bytes[..4]);
    }

    private sealed class AdvisoryLockScope : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly int _namespaceKey;
        private readonly int _key;
        private readonly bool _closeConnection;

        public AdvisoryLockScope(NpgsqlConnection connection, int namespaceKey, int key, bool closeConnection)
        {
            _connection = connection;
            _namespaceKey = namespaceKey;
            _key = key;
            _closeConnection = closeConnection;
        }

        public async ValueTask DisposeAsync()
        {
            await using (var command = _connection.CreateCommand())
            {
                command.CommandText = "SELECT pg_advisory_unlock(@ns, @key)";
                command.Parameters.Add(new NpgsqlParameter("ns", _namespaceKey));
                command.Parameters.Add(new NpgsqlParameter("key", _key));
                await command.ExecuteScalarAsync();
            }

            if (_closeConnection)
                await _connection.CloseAsync();
        }
    }

    private sealed class InProcessLockScope : IAsyncDisposable
    {
        private readonly Guid _companyId;

        public InProcessLockScope(Guid companyId) => _companyId = companyId;

        public async ValueTask DisposeAsync()
        {
            await InProcessGate.WaitAsync();
            try
            {
                InProcessHeld.Remove(_companyId);
            }
            finally
            {
                InProcessGate.Release();
            }
        }
    }
}
