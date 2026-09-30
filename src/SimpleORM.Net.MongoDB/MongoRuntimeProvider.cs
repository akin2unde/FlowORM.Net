#pragma warning disable CS1591
using System.Dynamic;
using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using SimpleORM.Net.Abstractions;
using SimpleORM.Net.Configuration;
using SimpleORM.Net.Query;
using SimpleORM.Net.Runtime;
namespace SimpleORM.Net.MongoDB;
/// <summary>MongoDB runtime entity data and schema provider.</summary>
public sealed class MongoRuntimeProvider : IRuntimeDatabaseProvider, IRuntimeSchemaProvider
{
    private const string MetadataCollection="__SimpleOrmRuntimeEntities";
    private readonly IMongoDatabase _db;
    private readonly ITenantProvider _tenant;
    private readonly IUserProvider _user;
    public MongoRuntimeProvider(SimpleOrmOptions options,ITenantProvider tenant,IUserProvider user)
    {
        _tenant=tenant;
        _user=user;
        var connection = options.Connection;
        var connectionString = string.IsNullOrWhiteSpace(connection.ConnectionString)
            ? $"mongodb://{connection.Host}:{connection.Port}"
            : connection.ConnectionString;

        var databaseName = connection.DatabaseName;
        if (string.IsNullOrWhiteSpace(databaseName) && !string.IsNullOrWhiteSpace(connection.ConnectionString))
        {
            databaseName = MongoUrl.Create(connection.ConnectionString).DatabaseName;
        }

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "SimpleORM MongoDB requires a DatabaseName. Set Connection.DatabaseName or include the database name in Connection.ConnectionString.");
        }

        var client = new MongoClient(connectionString);
        _db = client.GetDatabase(databaseName);
    }
    private IMongoCollection<BsonDocument> Meta=>_db.GetCollection<BsonDocument>(MetadataCollection);
    private IMongoCollection<BsonDocument> Col(string e)=>_db.GetCollection<BsonDocument>(e);
    public async Task CreateEntity(RuntimeEntityDefinition d,CancellationToken ct=default)
    {
        Validate(d);
        if(await GetEntity(d.Name,ct)!=null)throw new InvalidOperationException($"Runtime entity '{d.Name}' already exists.");
        await _db.CreateCollectionAsync(d.Name,cancellationToken:ct);
        await Meta.InsertOneAsync(new BsonDocument{{"Name",d.Name},{"Definition",JsonSerializer.Serialize(d)},{"UpdatedAt",DateTime.UtcNow}},cancellationToken:ct);
        await CreateCodeIndex(d,ct);
        foreach(var i in d.Indexes)await CreateIndex(d.Name,i,ct);
    }
    public async Task DropEntity(string entity,bool allowDestructiveChange=false,CancellationToken ct=default)
    {
        if(!allowDestructiveChange)throw new InvalidOperationException("Dropping a runtime entity is destructive. Set allowDestructiveChange to true.");
        await _db.DropCollectionAsync(entity,ct);
        await Meta.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("Name",entity),ct);
    }
    public async Task AddField(string entity,RuntimeFieldDefinition field,CancellationToken ct=default)
    {
        var d=await Required(entity,ct);
        if(d.Fields.Any(x=>Eq(x.Name,field.Name)))throw new InvalidOperationException($"Field '{field.Name}' already exists.");
        d.Fields.Add(field);
        await Store(d,ct);
    }
    public async Task DropField(string entity,string field,bool allowDestructiveChange=false,CancellationToken ct=default)
    {
        if(!allowDestructiveChange)throw new InvalidOperationException("Dropping a runtime field is destructive. Set allowDestructiveChange to true.");
        var d=await Required(entity,ct);
        await Col(entity).UpdateManyAsync(FilterDefinition<BsonDocument>.Empty,Builders<BsonDocument>.Update.Unset(field),cancellationToken:ct);
        d.Fields.RemoveAll(x=>Eq(x.Name,field));
        d.Indexes.RemoveAll(x=>x.Fields.Any(f=>Eq(f.Name,field)));
        await Store(d,ct);
    }
    public async Task CreateIndex(string entity,RuntimeIndexDefinition index,CancellationToken ct=default)
    {
        var d=await Required(entity,ct);
        ValidateIndex(d,index);
        var keys=new BsonDocument();
        if(!d.Global && !index.Fields.Any(f=>Eq(f.Name,"Tenant")))keys["Tenant"]=1;
        foreach(var f in index.Fields)keys[f.Name]=f.Direction==IndexDirection.Descending?-1:1;
        await Col(entity).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(new BsonDocumentIndexKeysDefinition<BsonDocument>(keys),new CreateIndexOptions{Name=index.Name,Unique=index.Unique}),cancellationToken:ct);
        d.Indexes.RemoveAll(x=>Eq(x.Name,index.Name));
        d.Indexes.Add(index);
        await Store(d,ct);
    }
    public async Task DropIndex(string entity,string indexName,CancellationToken ct=default)
    {
        var d=await Required(entity,ct);
        await Col(entity).Indexes.DropOneAsync(indexName,ct);
        d.Indexes.RemoveAll(x=>Eq(x.Name,indexName));
        await Store(d,ct);
    }
    public async Task<RuntimeEntityDefinition?> GetEntity(string entity,CancellationToken ct=default)
    {
        var x=await Meta.Find(Builders<BsonDocument>.Filter.Eq("Name",entity)).FirstOrDefaultAsync(ct);
        return x is null?null:JsonSerializer.Deserialize<RuntimeEntityDefinition>(x["Definition"].AsString);
    }
    public async Task<IReadOnlyList<RuntimeEntityDefinition>> GetEntities(CancellationToken ct=default)
    {
        var docs=await Meta.Find(FilterDefinition<BsonDocument>.Empty).Sort(Builders<BsonDocument>.Sort.Ascending("Name")).ToListAsync(ct);
        return docs.Select(x=>JsonSerializer.Deserialize<RuntimeEntityDefinition>(x["Definition"].AsString)).Where(x=>x!=null).Cast<RuntimeEntityDefinition>().ToList();
    }
    public async Task<IReadOnlyList<dynamic>> Select(string entity,SearchParam q,int skip,int limit,CancellationToken ct=default)
    {
        var d=await Required(entity,ct);
        var find=Col(entity).Find(Filter(d,q)).Sort(Sort(d,q)).Skip(skip);
        if(limit>0)find=find.Limit(limit);
        if(q.Fields.Count>0){ var projection=Builders<BsonDocument>.Projection.Combine(q.Fields.Select(x=>Builders<BsonDocument>.Projection.Include(ResolveField(d,x))));
        find=find.Project<BsonDocument>(projection);
    }var docs=await find.ToListAsync(ct);
    return docs.Select(ToDynamic).ToList();
}
public async Task<dynamic?> SelectSingle(string entity,SearchParam q,CancellationToken ct=default)
{
    var d=await Required(entity,ct);
    var x=await Col(entity).Find(Filter(d,q)).Sort(Sort(d,q)).Limit(1).FirstOrDefaultAsync(ct);
    return x is null?null:ToDynamic(x);
}
public async Task<long> Count(string entity,SearchParam q,CancellationToken ct=default)
{
    var d=await Required(entity,ct);
    return await Col(entity).CountDocumentsAsync(Filter(d,q),cancellationToken:ct);
}
public async Task<object?> Sum(string entity,string field,SearchParam q,CancellationToken ct=default)
{
    var d=await Required(entity,ct);
    ResolveField(d,field);
    var x=await Col(entity).Aggregate().Match(Filter(d,q)).Group(new BsonDocument{{"_id",BsonNull.Value},{"value",new BsonDocument("$sum","$"+field)}}).FirstOrDefaultAsync(ct);
    return x is null?null:BsonTypeMapper.MapToDotNetValue(x["value"]);
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

    var session = ((MongoTx)transaction).Session;
    var documents = data.Select(item => Prepare(definition, item)).ToList();

    if (!upsert)
    {
        await Col(entity).InsertManyAsync(session, documents, cancellationToken: ct);
        return documents.Select(ToDynamic).ToList();
    }

    var writes = documents
        .Select(document => (WriteModel<BsonDocument>)new ReplaceOneModel<BsonDocument>(
            CodeScope(definition, document["Code"].AsString),
            document)
        {
            IsUpsert = true
        })
        .ToList();

    await Col(entity).BulkWriteAsync(session, writes, cancellationToken: ct);
    return documents.Select(ToDynamic).ToList();
}
public async Task Update(string entity,string code,IDictionary<string,object?> data,IDBTransaction transaction,CancellationToken ct=default)
{
    var d=await Required(entity,ct);
    var session=((MongoTx)transaction).Session;
    var updates=data.Where(x=>d.Fields.Any(f=>Eq(f.Name,x.Key))).Select(x=>Builders<BsonDocument>.Update.Set(x.Key,BsonValue.Create(x.Value))).ToList();
    updates.Add(Builders<BsonDocument>.Update.Set("UpdatedAt",DateTime.UtcNow));
    if(d.ConcurrencyEnabled)updates.Add(Builders<BsonDocument>.Update.Inc("Version",1));
    await Col(entity).UpdateOneAsync(session,CodeScope(d,code),Builders<BsonDocument>.Update.Combine(updates),cancellationToken:ct);
}
public async Task Delete(string entity,string code,IDBTransaction transaction,CancellationToken ct=default)
{
    var d=await Required(entity,ct);
    var session=((MongoTx)transaction).Session;
    if(d.SoftDelete){var u=Builders<BsonDocument>.Update.Set("DeletedAt",DateTime.UtcNow).Set("UpdatedAt",DateTime.UtcNow);
    if(d.ConcurrencyEnabled)u=u.Inc("Version",1);
    await Col(entity).UpdateOneAsync(session,CodeScope(d,code),u,cancellationToken:ct);
}else await Col(entity).DeleteOneAsync(session,CodeScope(d,code),cancellationToken:ct);
}
public async Task<long> Update(
    string entity,
    SearchParam search,
    IDictionary<string, object?> data,
    IDBTransaction transaction,
    CancellationToken ct = default)
{
    var definition = await Required(entity, ct);
    var session = ((MongoTx)transaction).Session;
    var updates = BuildUpdates(definition, data);

    if (updates.Count == 0)
    {
        return 0;
    }

    updates.Add(Builders<BsonDocument>.Update.Set("UpdatedAt", DateTime.UtcNow));
    updates.Add(Builders<BsonDocument>.Update.Set("UpdatedBy", BsonValue.Create(_user.GetUserCode())));

    if (definition.ConcurrencyEnabled)
    {
        updates.Add(Builders<BsonDocument>.Update.Inc("Version", 1));
    }

    var result = await Col(entity).UpdateManyAsync(
        session,
        Filter(definition, search),
        Builders<BsonDocument>.Update.Combine(updates),
        cancellationToken: ct);

    return result.ModifiedCount;
}

public async Task<long> Delete(
    string entity,
    SearchParam search,
    IDBTransaction transaction,
    CancellationToken ct = default)
{
    var definition = await Required(entity, ct);
    var session = ((MongoTx)transaction).Session;
    var filter = Filter(definition, search);

    if (!definition.SoftDelete)
    {
        var deleted = await Col(entity).DeleteManyAsync(session, filter, cancellationToken: ct);
        return deleted.DeletedCount;
    }

    var update = Builders<BsonDocument>.Update
        .Set("DeletedAt", DateTime.UtcNow)
        .Set("UpdatedAt", DateTime.UtcNow)
        .Set("UpdatedBy", BsonValue.Create(_user.GetUserCode()));

    if (definition.ConcurrencyEnabled)
    {
        update = update.Inc("Version", 1);
    }

    var result = await Col(entity).UpdateManyAsync(session, filter, update, cancellationToken: ct);
    return result.ModifiedCount;
}

private List<UpdateDefinition<BsonDocument>> BuildUpdates(
    RuntimeEntityDefinition definition,
    IDictionary<string, object?> data)
{
    var updates = new List<UpdateDefinition<BsonDocument>>();

    foreach (var item in data)
    {
        var field = ResolveField(definition, item.Key);

        if (IsSystemField(field))
        {
            continue;
        }

        updates.Add(Builders<BsonDocument>.Update.Set(field, BsonValue.Create(item.Value)));
    }

    return updates;
}

private FilterDefinition<BsonDocument> Filter(RuntimeEntityDefinition d,SearchParam q)
{
    var b=Builders<BsonDocument>.Filter;
    var fs=new List<FilterDefinition<BsonDocument>>();
    foreach(var f in q.Filters){var n=ResolveField(d,f.Field);
    var v=BsonValue.Create(f.Value);
    fs.Add(f.Operator switch{SearchOperator.EQ=>b.Eq(n,v),SearchOperator.NEQ=>b.Ne(n,v),SearchOperator.GT=>b.Gt(n,v),SearchOperator.GTE=>b.Gte(n,v),SearchOperator.LT=>b.Lt(n,v),SearchOperator.LTE=>b.Lte(n,v),SearchOperator.Contains=>b.Regex(n,new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(f.Value?.ToString()??""),"i")),SearchOperator.StartsWith=>b.Regex(n,new BsonRegularExpression("^"+System.Text.RegularExpressions.Regex.Escape(f.Value?.ToString()??""),"i")),SearchOperator.EndsWith=>b.Regex(n,new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(f.Value?.ToString()??"")+"$","i")),SearchOperator.IsNull=>b.Eq(n,BsonNull.Value),SearchOperator.IsNotNull=>b.Ne(n,BsonNull.Value),SearchOperator.In=>b.In(n,AsValues(f.Value).Select(BsonValue.Create)),SearchOperator.NotIn=>b.Nin(n,AsValues(f.Value).Select(BsonValue.Create)),SearchOperator.Between=>Between(b,n,f.Value,false),SearchOperator.NotBetween=>Between(b,n,f.Value,true),_=>throw new NotSupportedException($"Runtime MongoDB filter operator '{f.Operator}' is not supported yet.")});
}var all=new List<FilterDefinition<BsonDocument>>();
if(fs.Count>0)all.Add(q.Condition==SearchCondition.Or?b.Or(fs):b.And(fs));
if(!d.Global)all.Add(b.Eq("Tenant",_tenant.GetTenant()??throw new InvalidOperationException("A tenant is required for this runtime entity.")));
if(d.SoftDelete&&!q.IncludeDeleted)all.Add(b.Eq("DeletedAt",BsonNull.Value));
if(all.Count==0)return b.Empty;
return b.And(all);
}
private SortDefinition<BsonDocument> Sort(RuntimeEntityDefinition d,SearchParam q)
{
    var b=Builders<BsonDocument>.Sort;
    if(q.OrderBy.Count==0)return b.Ascending("Code");
    return b.Combine(q.OrderBy.Select(x=>x.Descending?b.Descending(ResolveField(d,x.Field)):b.Ascending(ResolveField(d,x.Field))));
}
private static FilterDefinition<BsonDocument> Between(FilterDefinitionBuilder<BsonDocument> b,string n,object? value,bool not)
{
    var v=AsValues(value);
    if(v.Count!=2)throw new InvalidOperationException("Between requires exactly two values.");
    var range=b.Gte(n,BsonValue.Create(v[0])) & b.Lte(n,BsonValue.Create(v[1]));
    return not?b.Not(range):range;
}
private static List<object?> AsValues(object? value)
{
    if(value is System.Collections.IEnumerable e && value is not string){var r=new List<object?>();
    foreach(var x in e)r.Add(x);
    return r;
}return value is null?[]:[value];
}
private FilterDefinition<BsonDocument> CodeScope(RuntimeEntityDefinition d,string code)
{
    var b=Builders<BsonDocument>.Filter;
    var f=b.Eq("Code",code);
    if(!d.Global)f&=b.Eq("Tenant",_tenant.GetTenant()??throw new InvalidOperationException("A tenant is required for this runtime entity."));
    return f;
}
private BsonDocument Prepare(RuntimeEntityDefinition d,IDictionary<string,object?> src)
{
    var x=new BsonDocument();
    foreach(var f in d.Fields){src.TryGetValue(f.Name,out var v);
    if(v is null&&f.Required&&f.DefaultValue is null)throw new InvalidOperationException($"Runtime field '{f.Name}' is required.");
    x[f.Name]=BsonValue.Create(v??f.DefaultValue);
}x["Code"]=src.TryGetValue("Code",out var code)&&code is string s&&!string.IsNullOrWhiteSpace(s)?s:GenerateCode(d);
x["Tenant"]=d.Global?BsonNull.Value:_tenant.GetTenant()??throw new InvalidOperationException("A tenant is required for this runtime entity.");
x["Version"]=1L;
x["CreatedAt"]=DateTime.UtcNow;
x["UpdatedAt"]=BsonNull.Value;
x["DeletedAt"]=BsonNull.Value;
x["CreatedBy"]=BsonValue.Create(_user.GetUserCode());
x["UpdatedBy"]=BsonNull.Value;
return x;
}
private async Task CreateCodeIndex(RuntimeEntityDefinition d,CancellationToken ct)
{
    var keys=new BsonDocument();
    if(!d.Global)keys["Tenant"]=1;
    keys["Code"]=1;
    await Col(d.Name).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(new BsonDocumentIndexKeysDefinition<BsonDocument>(keys),new CreateIndexOptions{Name="UX_Code",Unique=true}),cancellationToken:ct);
}
private async Task Store(RuntimeEntityDefinition d,CancellationToken ct)=>await Meta.ReplaceOneAsync(Builders<BsonDocument>.Filter.Eq("Name",d.Name),new BsonDocument{{"Name",d.Name},{"Definition",JsonSerializer.Serialize(d)},{"UpdatedAt",DateTime.UtcNow}},new ReplaceOptions{IsUpsert=true},ct);
private async Task<RuntimeEntityDefinition> Required(string e,CancellationToken ct)=>await GetEntity(e,ct)??throw new InvalidOperationException($"Runtime entity '{e}' does not exist.");
private static string ResolveField(RuntimeEntityDefinition definition, string field)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(field);

    if (IsSystemField(field))
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

private static bool IsSystemField(string field)
{
    var root = field.Split('.', 2)[0];
    var systemFields = new[]
    {
        "Code", "Tenant", "Version", "CreatedAt", "UpdatedAt",
        "DeletedAt", "CreatedBy", "UpdatedBy"
    };

    return systemFields.Any(x => Eq(x, root));
}
private static void Validate(RuntimeEntityDefinition d)
{
    ArgumentNullException.ThrowIfNull(d);
    ArgumentException.ThrowIfNullOrWhiteSpace(d.Name);
    if(d.Fields.Select(x=>x.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count()!=d.Fields.Count)throw new InvalidOperationException("Runtime field names must be unique.");
    foreach(var i in d.Indexes)ValidateIndex(d,i);
}
private static void ValidateIndex(RuntimeEntityDefinition d,RuntimeIndexDefinition i)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(i.Name);
    if(i.Fields.Count==0)throw new InvalidOperationException("Runtime index must contain at least one field.");
    foreach(var f in i.Fields)ResolveField(d,f.Name);
}
private static string GenerateCode(RuntimeEntityDefinition d)
{
    var p=string.IsNullOrWhiteSpace(d.CodePrefix)?d.Name[..Math.Min(3,d.Name.Length)].ToUpperInvariant():d.CodePrefix.Trim().ToUpperInvariant();
    return $"{p}-{Guid.NewGuid():N}"[..Math.Min(p.Length+11,p.Length+33)];
}
private static dynamic ToDynamic(BsonDocument doc)
{
    IDictionary<string,object?> x=new ExpandoObject();
    foreach(var e in doc)if(e.Name!="_id")x[e.Name]=BsonTypeMapper.MapToDotNetValue(e.Value);
    return (ExpandoObject)x;
} private static bool Eq(string a,string b)=>a.Equals(b,StringComparison.OrdinalIgnoreCase);
}
