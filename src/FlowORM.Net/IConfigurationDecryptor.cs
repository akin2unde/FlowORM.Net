using FlowORM.Net.Models;

using FlowORM.Net.Query;

namespace FlowORM.Net.Abstractions;

/// <summary>Decrypts ENC:-protected configuration values.</summary>
public interface IConfigurationDecryptor
{

    /// <summary>Decrypts.</summary>
    string Decrypt(string encryptedValue);

}
