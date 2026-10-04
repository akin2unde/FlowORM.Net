using FlowORM.Net.Services;
#pragma warning disable CS1591
using System.Dynamic;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using FlowORM.Net.Abstractions;
using FlowORM.Net.Configuration;
using FlowORM.Net.Query;
using FlowORM.Net.Runtime;
namespace FlowORM.Net.SqlServer;
/// <summary>SQL Server runtime entity data and schema provider.</summary>
public sealed class SqlServerRuntimeProvider : IRuntimeDatabaseProvider, IRuntimeSchemaProvider
{
    private const string MetadataTable = "__SimpleOrmRuntimeEntities";
    private readonly FlowOrmOptions _options;
    private readonly ITenantProvider _tenant;
    private readonly IUserProvider _user;
    public SqlServerRuntimeProvider(FlowOrmOptions options, ITenantProvider tenant, IUserProvider user, FlowOrmExecutionContext? executionContext = null)
    {
        _options = options;
        _tenant = executionContext?.Wrap(tenant) ?? tenant;
        _user = user;
    }
    public async Task CreateEntity(RuntimeEntityDefinition d, CancellationToken ct = default)
    {
        ValidateDefinition(d);
        await EnsureMetadata(ct);
        if (await GetEntity(d.Name, ct) is not null) throw new InvalidOperationException($"Runtime entity '{d.Name}' already exists.");
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        try
        {
            var cols = new List<string> { "[Code] nvarchar(100) NOT NULL", "[Tenant] nvarchar(100) NULL", "[Version] bigint NOT NULL CONSTRAINT [DF_" + Safe(d.Name) + "_Version] DEFAULT(1)", "[CreatedAt] datetime2 NOT NULL", "[UpdatedAt] datetime2 NULL", "[DeletedAt] datetime2 NULL", "[CreatedBy] nvarchar(100) NULL", "[UpdatedBy] nvarchar(100) NULL" };
            cols.AddRange(d.Fields.Select(FieldSql));
            var pk = d.Global ? "CONSTRAINT [PK_" + Safe(d.Name) + "] PRIMARY KEY ([Code])" : "CONSTRAINT [PK_" + Safe(d.Name) + "] PRIMARY KEY ([Tenant],[Code])";
            cols.Add(pk);
            await Exec(c, tx, $"CREATE TABLE [{Esc(d.Name)}] ({string.Join(",", cols)});", ct);
            await SaveMetadata(c, tx, d, ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
        foreach (var i in d.Indexes) await CreateIndex(d.Name, i, ct);
    }
    public async Task DropEntity(string entity, bool allowDestructiveChange = false, CancellationToken ct = default)
    {
        if (!allowDestructiveChange) throw new InvalidOperationException("Dropping a runtime entity is destructive. Set allowDestructiveChange to true.");
        await EnsureMetadata(ct);
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await using var tx = (SqlTransaction)await c.BeginTransactionAsync(ct);
        await Exec(
            c,
            tx,
            $"DROP TABLE [{Esc(entity)}];",
            ct);

        await Exec(
            c,
            tx,
            $"DELETE FROM [{MetadataTable}] WHERE [Name] = @name;",
            ct,
            new SqlParameter("@name", entity));

        await tx.CommitAsync(ct);
    }
    public async Task AddField(string entity, RuntimeFieldDefinition field, CancellationToken ct = default)
    {
        ValidateField(field);
        var d = await Required(entity, ct);
        if (d.Fields.Any(x => Eq(x.Name, field.Name))) throw new InvalidOperationException($"Field '{field.Name}' already exists.");
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await Exec(c, null, $"ALTER TABLE [{Esc(entity)}] ADD {FieldSql(field)};", ct);
        d.Fields.Add(field);
        await UpdateMetadata(d, ct);
    }
    public async Task DropField(string entity, string field, bool allowDestructiveChange = false, CancellationToken ct = default)
    {
        if (!allowDestructiveChange) throw new InvalidOperationException("Dropping a runtime field is destructive. Set allowDestructiveChange to true.");
        var d = await Required(entity, ct);
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await Exec(c, null, $"ALTER TABLE [{Esc(entity)}] DROP COLUMN [{Esc(field)}];", ct);
        d.Fields.RemoveAll(x => Eq(x.Name, field));
        d.Indexes.RemoveAll(x => x.Fields.Any(f => Eq(f.Name, field)));
        await UpdateMetadata(d, ct);
    }
    public async Task CreateIndex(string entity, RuntimeIndexDefinition index, CancellationToken ct = default)
    {
        var d = await Required(entity, ct);
        ValidateIndex(d, index);
        var fields = new List<string>();
        if (!d.Global && !index.Fields.Any(f => Eq(f.Name, "Tenant"))) fields.Add("[Tenant] ASC");
        fields.AddRange(index.Fields.Select(f => $"[{Esc(f.Name)}] {(f.Direction == IndexDirection.Descending ? "DESC" : "ASC")}"));
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await Exec(c, null, $"CREATE {(index.Unique ? "UNIQUE " : "")}INDEX [{Esc(index.Name)}] ON [{Esc(entity)}] ({string.Join(",", fields)});", ct);
        d.Indexes.RemoveAll(x => Eq(x.Name, index.Name));
        d.Indexes.Add(index);
        await UpdateMetadata(d, ct);
    }
    public async Task DropIndex(string entity, string indexName, CancellationToken ct = default)
    {
        var d = await Required(entity, ct);
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await Exec(c, null, $"DROP INDEX [{Esc(indexName)}] ON [{Esc(entity)}];", ct);
        d.Indexes.RemoveAll(x => Eq(x.Name, indexName));
        await UpdateMetadata(d, ct);
    }
    public async Task<RuntimeEntityDefinition?> GetEntity(string entity, CancellationToken ct = default)
    {
        await EnsureMetadata(ct);
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT [Definition] FROM [{MetadataTable}] WHERE [Name]=@name";
        cmd.Parameters.AddWithValue("@name", entity);
        var v = await cmd.ExecuteScalarAsync(ct);
        return v is null ? null : JsonSerializer.Deserialize<RuntimeEntityDefinition>((string)v);
    }
    public async Task<IReadOnlyList<RuntimeEntityDefinition>> GetEntities(CancellationToken ct = default)
    {
        await EnsureMetadata(ct);
        var r = new List<RuntimeEntityDefinition>();
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT [Definition] FROM [{MetadataTable}] ORDER BY [Name]";
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            var d = JsonSerializer.Deserialize<RuntimeEntityDefinition>(rd.GetString(0));
            if (d != null) r.Add(d);
        }
        return r;
    }
    public async Task<IReadOnlyList<dynamic>> Select(string entity, SearchParam search, int skip, int limit, CancellationToken ct = default)
    {
        var d = await Required(entity, ct);
        var (where, ps) = Where(d, search);
        var fields = search.Fields.Count == 0 ? "*" : string.Join(",", search.Fields.Select(x => $"{FieldExpression(d, x)} AS [{Esc(x.Replace('.', '_'))}]"));
        var order = Order(d, search);
        var page = limit > 0 ? $" OFFSET {skip} ROWS FETCH NEXT {limit} ROWS ONLY" : $" OFFSET {skip} ROWS";
        return await Read($"SELECT {fields} FROM [{Esc(entity)}] {where} {order}{page}", ps, ct);
    }
    public async Task<dynamic?> SelectSingle(string entity, SearchParam search, CancellationToken ct = default)
    {
        var d = await Required(entity, ct);
        var (where, ps) = Where(d, search);
        var rows = await Read($"SELECT TOP 1 * FROM [{Esc(entity)}] {where} {Order(d, search)}", ps, ct);
        return rows.FirstOrDefault();
    }
    public async Task<long> Count(string entity, SearchParam search, CancellationToken ct = default)
    {
        var d = await Required(entity, ct);
        var (where, ps) = Where(d, search);
        var v = await Scalar($"SELECT COUNT_BIG(*) FROM [{Esc(entity)}] {where}", ps, ct);
        return Convert.ToInt64(v ?? 0, CultureInfo.InvariantCulture);
    }
    public async Task<object?> Sum(string entity, string field, SearchParam search, CancellationToken ct = default)
    {
        var d = await Required(entity, ct);
        var f = ResolveField(d, field);
        var (where, ps) = Where(d, search);
        return await Scalar($"SELECT SUM({FieldExpression(d, f)}) FROM [{Esc(entity)}] {where}", ps, ct);
    }
    public async Task<IReadOnlyList<dynamic>> Save(
        string entity,
        IReadOnlyList<IDictionary<string, object?>> data,
        bool upsert,
        IDBTransaction transaction,
        CancellationToken ct = default)
    {
        var definition = await Required(entity, ct);

        if (upsert && definition.ConcurrencyEnabled)
        {
            throw new InvalidOperationException(
                $"Runtime entity '{entity}' has optimistic concurrency enabled. " +
                "Upsert uses last-write-wins semantics, so disable concurrency for this runtime entity before using upsert.");
        }

        var sqlTransaction = (SqlTx)transaction;
        var result = new List<dynamic>();

        foreach (var source in data)
        {
            var row = PrepareInsert(definition, source);

            if (upsert)
            {
                await UpsertRow(entity, definition, row, sqlTransaction, ct);
            }
            else
            {
                await InsertRow(entity, row, sqlTransaction, ct);
            }

            result.Add(ToDynamic(row));
        }

        return result;
    }

    private static async Task InsertRow(
        string entity,
        Dictionary<string, object?> row,
        SqlTx transaction,
        CancellationToken ct)
    {
        var keys = row.Keys.ToList();
        await using var command = transaction.Connection.CreateCommand();
        command.Transaction = transaction.Transaction;
        command.CommandText =
            $"INSERT INTO [{Esc(entity)}] " +
            $"({string.Join(",", keys.Select(x => $"[{Esc(x)}]"))}) " +
            $"VALUES ({string.Join(",", keys.Select((_, i) => "@p" + i))})";

        for (var index = 0; index < keys.Count; index++)
        {
            command.Parameters.AddWithValue("@p" + index, row[keys[index]] ?? DBNull.Value);
        }

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task UpsertRow(
        string entity,
        RuntimeEntityDefinition definition,
        Dictionary<string, object?> row,
        SqlTx transaction,
        CancellationToken ct)
    {
        var code = Convert.ToString(row["Code"], CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("Runtime upsert requires a Code.");

        await using var exists = transaction.Connection.CreateCommand();
        exists.Transaction = transaction.Transaction;
        exists.Parameters.AddWithValue("@code", code);
        var scope = Scope(definition, exists);
        exists.CommandText = $"SELECT COUNT_BIG(1) FROM [{Esc(entity)}] WHERE [Code]=@code{scope}";

        var count = Convert.ToInt64(await exists.ExecuteScalarAsync(ct) ?? 0, CultureInfo.InvariantCulture);
        if (count == 0)
        {
            await InsertRow(entity, row, transaction, ct);
            return;
        }

        var changes = row
            .Where(item => IsUserField(definition, item.Key))
            .ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);

        await Update(entity, code, changes, transaction, ct);
    }

    public async Task Update(string entity, string code, IDictionary<string, object?> data, IDBTransaction transaction, CancellationToken ct = default)
    {
        var d = await Required(entity, ct);
        var tx = (SqlTx)transaction;
        var allowed = data.Where(x => IsUserField(d, x.Key)).ToList();
        if (allowed.Count == 0) return;
        await using var cmd = tx.Connection.CreateCommand();
        cmd.Transaction = tx.Transaction;
        var sets = new List<string>();
        for (int i = 0; i < allowed.Count; i++)
        {
            sets.Add($"[{Esc(allowed[i].Key)}]=@p{i}");
            cmd.Parameters.AddWithValue("@p" + i, allowed[i].Value ?? DBNull.Value);
        }
        sets.Add("[UpdatedAt]=@updated");
        if (d.ConcurrencyEnabled) sets.Add("[Version]=[Version]+1");
        cmd.Parameters.AddWithValue("@updated", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@code", code);
        var scope = Scope(d, cmd);
        cmd.CommandText = $"UPDATE [{Esc(entity)}] SET {string.Join(",", sets)} WHERE [Code]=@code{scope}";
        await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task Delete(string entity, string code, IDBTransaction transaction, CancellationToken ct = default)
    {
        var d = await Required(entity, ct);
        var tx = (SqlTx)transaction;
        await using var cmd = tx.Connection.CreateCommand();
        cmd.Transaction = tx.Transaction;
        cmd.Parameters.AddWithValue("@code", code);
        var scope = Scope(d, cmd);
        cmd.CommandText = d.SoftDelete ? $"UPDATE [{Esc(entity)}] SET [DeletedAt]=@deleted,[UpdatedAt]=@deleted{(d.ConcurrencyEnabled ? ",[Version]=[Version]+1" : "")} WHERE [Code]=@code{scope}" : $"DELETE FROM [{Esc(entity)}] WHERE [Code]=@code{scope}";
        if (d.SoftDelete) cmd.Parameters.AddWithValue("@deleted", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    public async Task<long> Update(
        string entity,
        SearchParam search,
        IDictionary<string, object?> data,
        IDBTransaction transaction,
        CancellationToken ct = default)
    {
        var definition = await Required(entity, ct);
        var sqlTransaction = (SqlTx)transaction;
        var allowed = data.Where(item => IsUserField(definition, item.Key)).ToList();

        if (allowed.Count == 0)
        {
            return 0;
        }

        var (where, parameters) = Where(definition, search);
        await using var command = sqlTransaction.Connection.CreateCommand();
        command.Transaction = sqlTransaction.Transaction;

        var sets = new List<string>();
        for (var index = 0; index < allowed.Count; index++)
        {
            sets.Add($"[{Esc(allowed[index].Key)}]=@u{index}");
            command.Parameters.AddWithValue("@u" + index, allowed[index].Value ?? DBNull.Value);
        }

        sets.Add("[UpdatedAt]=@updated");
        sets.Add("[UpdatedBy]=@updatedBy");
        command.Parameters.AddWithValue("@updated", DateTime.UtcNow);
        command.Parameters.AddWithValue("@updatedBy", (object?)_user.GetUserCode() ?? DBNull.Value);

        if (definition.ConcurrencyEnabled)
        {
            sets.Add("[Version]=[Version]+1");
        }

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        command.CommandText =
            $"UPDATE [{Esc(entity)}] SET {string.Join(",", sets)} {where}";

        return await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<long> Delete(
        string entity,
        SearchParam search,
        IDBTransaction transaction,
        CancellationToken ct = default)
    {
        var definition = await Required(entity, ct);
        var sqlTransaction = (SqlTx)transaction;
        var (where, parameters) = Where(definition, search);

        await using var command = sqlTransaction.Connection.CreateCommand();
        command.Transaction = sqlTransaction.Transaction;

        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        if (definition.SoftDelete)
        {
            command.Parameters.AddWithValue("@deleted", DateTime.UtcNow);
            command.Parameters.AddWithValue("@updatedBy", (object?)_user.GetUserCode() ?? DBNull.Value);
            command.CommandText =
                $"UPDATE [{Esc(entity)}] SET [DeletedAt]=@deleted,[UpdatedAt]=@deleted,[UpdatedBy]=@updatedBy" +
                (definition.ConcurrencyEnabled ? ",[Version]=[Version]+1 " : " ") +
                where;
        }
        else
        {
            command.CommandText = $"DELETE FROM [{Esc(entity)}] {where}";
        }

        return await command.ExecuteNonQueryAsync(ct);
    }

    private async Task EnsureMetadata(CancellationToken ct)
    {
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await Exec(c, null, $"IF OBJECT_ID(N'[{MetadataTable}]',N'U') IS NULL CREATE TABLE [{MetadataTable}] ([Name] nvarchar(128) NOT NULL PRIMARY KEY,[Definition] nvarchar(max) NOT NULL,[UpdatedAt] datetime2 NOT NULL);", ct);
    }
    private async Task SaveMetadata(SqlConnection c, SqlTransaction? tx, RuntimeEntityDefinition d, CancellationToken ct)
    {
        await Exec(c, tx, $"INSERT INTO [{MetadataTable}]([Name],[Definition],[UpdatedAt]) VALUES(@name,@json,@now);", ct, new("@name", d.Name), new("@json", JsonSerializer.Serialize(d)), new("@now", DateTime.UtcNow));
    }
    private async Task UpdateMetadata(RuntimeEntityDefinition d, CancellationToken ct)
    {
        await EnsureMetadata(ct);
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await Exec(c, null, $"UPDATE [{MetadataTable}] SET [Definition]=@json,[UpdatedAt]=@now WHERE [Name]=@name", ct, new("@json", JsonSerializer.Serialize(d)), new("@now", DateTime.UtcNow), new("@name", d.Name));
    }
    private async Task<RuntimeEntityDefinition> Required(string e, CancellationToken ct) => await GetEntity(e, ct) ?? throw new InvalidOperationException($"Runtime entity '{e}' does not exist.");
    private (string, List<SqlParameter>) Where(RuntimeEntityDefinition d, SearchParam q)
    {
        var parts = new List<string>();
        var ps = new List<SqlParameter>();
        foreach (var f in q.Filters)
        {
            var n = ResolveField(d, f.Field);
            var p = "@p" + ps.Count;
            switch (f.Operator)
            {
                case SearchOperator.EQ:
                    parts.Add($"{FieldExpression(d, n)}={p}");
                    ps.Add(new(p, f.Value ?? DBNull.Value));
                    break;
                case SearchOperator.NEQ:
                    parts.Add($"{FieldExpression(d, n)}<>{p}");
                    ps.Add(new(p, f.Value ?? DBNull.Value));
                    break;
                case SearchOperator.GT:
                case SearchOperator.GTE:
                case SearchOperator.LT:
                case SearchOperator.LTE:
                    var op = f.Operator switch { SearchOperator.GT => ">", SearchOperator.GTE => ">=", SearchOperator.LT => "<", _ => "<=" };
                    parts.Add($"{FieldExpression(d, n)}{op}{p}");
                    ps.Add(new(p, f.Value ?? DBNull.Value));
                    break;
                case SearchOperator.Contains:
                case SearchOperator.StartsWith:
                case SearchOperator.EndsWith:
                    var v = f.Operator == SearchOperator.Contains ? $"%{f.Value}%" : f.Operator == SearchOperator.StartsWith ? $"{f.Value}%" : $"%{f.Value}";
                    parts.Add($"{FieldExpression(d, n)} LIKE {p}");
                    ps.Add(new(p, v));
                    break;
                case SearchOperator.IsNull:
                    parts.Add($"{FieldExpression(d, n)} IS NULL");
                    break;
                case SearchOperator.IsNotNull:
                    parts.Add($"{FieldExpression(d, n)} IS NOT NULL");
                    break;
                case SearchOperator.In:
                case SearchOperator.NotIn:
                    {
                        var vals = AsValues(f.Value);
                        if (vals.Count == 0)
                        {
                            parts.Add(f.Operator == SearchOperator.In ? "1=0" : "1=1");
                            break;
                        }
                        var names = new List<string>();
                        foreach (var item in vals)
                        {
                            var pn = "@p" + ps.Count;
                            names.Add(pn);
                            ps.Add(new(pn, item ?? DBNull.Value));
                        }
                        parts.Add($"{FieldExpression(d, n)} {(f.Operator == SearchOperator.In ? "IN" : "NOT IN")} ({string.Join(",", names)})");
                        break;
                    }
                case SearchOperator.Between:
                case SearchOperator.NotBetween:
                    {
                        var vals = AsValues(f.Value);
                        if (vals.Count != 2) throw new InvalidOperationException("Between requires exactly two values.");
                        var a = "@p" + ps.Count;
                        ps.Add(new(a, vals[0] ?? DBNull.Value));
                        var z = "@p" + ps.Count;
                        ps.Add(new(z, vals[1] ?? DBNull.Value));
                        parts.Add($"{FieldExpression(d, n)} {(f.Operator == SearchOperator.NotBetween ? "NOT " : "")}BETWEEN {a} AND {z}");
                        break;
                    }
                default: throw new NotSupportedException($"Runtime SQL filter operator '{f.Operator}' is not supported yet.");
            }
        }
        var userClause = parts.Count == 0 ? null : "(" + string.Join(q.Condition == SearchCondition.Or ? " OR " : " AND ", parts) + ")";
        var scopes = new List<string>();
        if (!d.Global)
        {
            var t = _tenant.GetTenant() ?? throw new InvalidOperationException("A tenant is required for this runtime entity.");
            scopes.Add("[Tenant]=@tenant");
            ps.Add(new("@tenant", t));
        }
        if (d.SoftDelete && !q.IncludeDeleted) scopes.Add("[DeletedAt] IS NULL");
        var all = new List<string>();
        if (userClause is not null) all.Add(userClause);
        all.AddRange(scopes);
        return (all.Count == 0 ? "" : "WHERE " + string.Join(" AND ", all), ps);
    }
    private string Order(RuntimeEntityDefinition d, SearchParam q) => q.OrderBy.Count == 0 ? "ORDER BY [Code] ASC" : "ORDER BY " + string.Join(",", q.OrderBy.Select(x => $"{FieldExpression(d, x.Field)} {(x.Descending ? "DESC" : "ASC")}"));
    private Dictionary<string, object?> PrepareInsert(RuntimeEntityDefinition d, IDictionary<string, object?> source)
    {
        var now = DateTime.UtcNow;
        var r = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in d.Fields)
        {
            source.TryGetValue(f.Name, out var v);
            if (v is null && f.Required && f.DefaultValue is null) throw new InvalidOperationException($"Runtime field '{f.Name}' is required.");
            r[f.Name] = v ?? f.DefaultValue;
        }
        r["Code"] = source.TryGetValue("Code", out var code) && code is string s && !string.IsNullOrWhiteSpace(s) ? s : GenerateCode(d);
        r["Tenant"] = d.Global ? null : _tenant.GetTenant() ?? throw new InvalidOperationException("A tenant is required for this runtime entity.");
        r["Version"] = 1L;
        r["CreatedAt"] = now;
        r["UpdatedAt"] = now;
        r["DeletedAt"] = null;
        r["CreatedBy"] = _user.GetUserCode();
        r["UpdatedBy"] = null;
        return r;
    }
    private static string GenerateCode(RuntimeEntityDefinition d)
    {
        var p = string.IsNullOrWhiteSpace(d.CodePrefix) ? d.Name[..Math.Min(3, d.Name.Length)].ToUpperInvariant() : d.CodePrefix.Trim().ToUpperInvariant();
        return $"{p}-{Guid.NewGuid():N}"[..Math.Min(p.Length + 11, p.Length + 33)];
    }
    private string Scope(RuntimeEntityDefinition d, SqlCommand cmd)
    {
        if (d.Global) return "";
        var t = _tenant.GetTenant() ?? throw new InvalidOperationException("A tenant is required for this runtime entity.");
        cmd.Parameters.AddWithValue("@tenant", t);
        return " AND [Tenant]=@tenant";
    }
    private static List<object?> AsValues(object? value)
    {
        if (value is System.Collections.IEnumerable e && value is not string)
        {
            var r = new List<object?>();
            foreach (var x in e) r.Add(x);
            return r;
        }
        return value is null ? [] : [value];
    }
    private static bool IsUserField(RuntimeEntityDefinition d, string n) => d.Fields.Any(x => Eq(x.Name, n));
    private static string ResolveField(RuntimeEntityDefinition definition, string field)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        var system = new[]
        {
        "Code", "Tenant", "Version", "CreatedAt", "UpdatedAt",
        "DeletedAt", "CreatedBy", "UpdatedBy"
    };

        if (system.Any(x => Eq(x, field)))
        {
            return field;
        }

        var separator = field.IndexOf('.');
        var root = separator < 0 ? field : field[..separator];
        var runtimeField = definition.Fields.FirstOrDefault(x => Eq(x.Name, root));

        if (runtimeField is null)
        {
            throw new InvalidOperationException(
                $"Runtime field '{root}' is not defined on '{definition.Name}'.");
        }

        if (separator < 0)
        {
            return runtimeField.Name;
        }

        if (runtimeField.DataType != RuntimeDataType.Json)
        {
            throw new InvalidOperationException(
                $"Runtime field '{root}' on '{definition.Name}' is not a JSON field and cannot use a dotted path.");
        }

        return runtimeField.Name + field[separator..];
    }

    private static string FieldExpression(RuntimeEntityDefinition definition, string field)
    {
        var resolved = ResolveField(definition, field);
        var separator = resolved.IndexOf('.');

        if (separator < 0)
        {
            return $"[{Esc(resolved)}]";
        }

        var root = resolved[..separator];
        var path = resolved[(separator + 1)..].Replace("'", "''", StringComparison.Ordinal);
        return $"JSON_VALUE([{Esc(root)}], '$.{path}')";
    }
    private static void ValidateDefinition(RuntimeEntityDefinition d)
    {
        ArgumentNullException.ThrowIfNull(d);
        ArgumentException.ThrowIfNullOrWhiteSpace(d.Name);
        if (d.Fields.Select(x => x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != d.Fields.Count) throw new InvalidOperationException("Runtime field names must be unique.");
        foreach (var f in d.Fields) ValidateField(f);
        foreach (var i in d.Indexes) ValidateIndex(d, i);
    }
    private static void ValidateField(RuntimeFieldDefinition f)
    {
        ArgumentNullException.ThrowIfNull(f);
        ArgumentException.ThrowIfNullOrWhiteSpace(f.Name);
        if (f.Size <= 0) throw new InvalidOperationException("Runtime field size must be greater than zero.");
    }
    private static void ValidateIndex(RuntimeEntityDefinition d, RuntimeIndexDefinition i)
    {
        ArgumentNullException.ThrowIfNull(i);
        ArgumentException.ThrowIfNullOrWhiteSpace(i.Name);
        if (i.Fields.Count == 0) throw new InvalidOperationException("Runtime index must contain at least one field.");
        foreach (var f in i.Fields) ResolveField(d, f.Name);
    }
    private static string FieldSql(RuntimeFieldDefinition f)
    {
        var type = f.DataType switch { RuntimeDataType.String => $"nvarchar({(f.Size is > 0 ? f.Size.Value : 50)})", RuntimeDataType.Int32 => "int", RuntimeDataType.Int64 => "bigint", RuntimeDataType.Decimal => "decimal(18,4)", RuntimeDataType.Double => "float", RuntimeDataType.Boolean => "bit", RuntimeDataType.DateTime => "datetime2", RuntimeDataType.Guid => "uniqueidentifier", RuntimeDataType.Json => "nvarchar(max)", _ => throw new ArgumentOutOfRangeException() };
        return $"[{Esc(f.Name)}] {type} {(f.Required ? "NOT NULL" : "NULL")}";
    }
    private async Task<IReadOnlyList<dynamic>> Read(string sql, List<SqlParameter> ps, CancellationToken ct)
    {
        var r = new List<dynamic>();
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(ps.ToArray());
        await using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
        {
            IDictionary<string, object?> x = new ExpandoObject();
            for (int i = 0; i < rd.FieldCount; i++) x[rd.GetName(i)] = await rd.IsDBNullAsync(i, ct) ? null : rd.GetValue(i);
            r.Add((ExpandoObject)x);
        }
        return r;
    }
    private async Task<object?> Scalar(string sql, List<SqlParameter> ps, CancellationToken ct)
    {
        await using var c = CreateConnection();
        await c.OpenAsync(ct);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(ps.ToArray());
        return await cmd.ExecuteScalarAsync(ct);
    }
    private static async Task Exec(SqlConnection c, SqlTransaction? tx, string sql, CancellationToken ct, params SqlParameter[] ps)
    {
        await using var cmd = c.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = sql;
        if (ps.Length > 0) cmd.Parameters.AddRange(ps);
        await cmd.ExecuteNonQueryAsync(ct);
    }
    private SqlConnection CreateConnection()
    {
        var x = _options.Connection;
        if (!string.IsNullOrWhiteSpace(x.ConnectionString))
        {
            var configured = new SqlConnectionStringBuilder(x.ConnectionString);
            if (string.IsNullOrWhiteSpace(configured.InitialCatalog))
            {
                if (string.IsNullOrWhiteSpace(x.DatabaseName))
                {
                    throw new InvalidOperationException(
                        "FlowORM SQL Server requires a DatabaseName. Set Connection.DatabaseName or include Initial Catalog/Database in Connection.ConnectionString.");
                }

                configured.InitialCatalog = x.DatabaseName;
            }

            return new SqlConnection(configured.ConnectionString);
        }

        if (string.IsNullOrWhiteSpace(x.DatabaseName))
        {
            throw new InvalidOperationException(
                "FlowORM SQL Server requires Connection.DatabaseName when ConnectionString is not supplied.");
        }

        var b = new SqlConnectionStringBuilder { DataSource = x.Port > 0 ? $"{x.Host},{x.Port}" : x.Host, InitialCatalog = x.DatabaseName, TrustServerCertificate = true };
        if (string.IsNullOrWhiteSpace(x.Username)) b.IntegratedSecurity = true;
        else
        {
            b.UserID = x.Username;
            b.Password = x.Password ?? "";
        }
        return new SqlConnection(b.ConnectionString);
    }
    private static dynamic ToDynamic(IDictionary<string, object?> source)
    {
        IDictionary<string, object?> x = new ExpandoObject();
        foreach (var p in source) x[p.Key] = p.Value;
        return (ExpandoObject)x;
    }
    private static bool Eq(string a, string b) => a.Equals(b, StringComparison.OrdinalIgnoreCase);
    private static string Esc(string s) => s.Replace("]", "]]", StringComparison.Ordinal);
    private static string Safe(string s) => new string(s.Where(char.IsLetterOrDigit).ToArray());
}
