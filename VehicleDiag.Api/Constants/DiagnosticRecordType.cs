namespace VehicleDiag.Api.Constants;

public static class DiagnosticRecordType
{
    public const string READ_DTC = "READ_DTC";
    public const string READ_INFO = "READ_INFO";

    public static bool IsValid(string? recordType)
    {
        return string.Equals(recordType, READ_DTC, StringComparison.OrdinalIgnoreCase)
            || string.Equals(recordType, READ_INFO, StringComparison.OrdinalIgnoreCase);
    }

    public static string Normalize(string? recordType)
    {
        if (string.Equals(recordType, READ_DTC, StringComparison.OrdinalIgnoreCase))
            return READ_DTC;

        if (string.Equals(recordType, READ_INFO, StringComparison.OrdinalIgnoreCase))
            return READ_INFO;

        return string.Empty;
    }
}
