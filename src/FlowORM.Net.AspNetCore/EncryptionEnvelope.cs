namespace FlowORM.Net.AspNetCore;

internal sealed record EncryptionEnvelope(string Key, string Iv, string Data);
