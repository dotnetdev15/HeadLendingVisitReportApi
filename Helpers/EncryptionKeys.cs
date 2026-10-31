namespace HeadLendingVisitReportApi.Helpers;

/// <summary>
/// Keys passed to EncryptionHelper.Decrypt for the encrypted "Credntials" values in appsettings.json.
/// Set these to the SAME keys that were used to encrypt the values (userSalt / dbSalt in PDL.ManagementService).
/// </summary>
public static class EncryptionKeys
{
    /// <summary>Key used to decrypt Credntials:Username.</summary>
    public const string UserSalt = "PUT_USER_SALT_HERE";

    /// <summary>Key used to decrypt Credntials:Password and Credntials:Database.</summary>
    public const string DbSalt = "PUT_DB_SALT_HERE";
}
