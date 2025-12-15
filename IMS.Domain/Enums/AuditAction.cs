namespace IMS.Domain.Enums
{
    public enum AuditAction
    {
        // Data operations
        Create = 1,
        Update = 2,
        Delete = 3,
        Read = 4,

        // Authentication
        Login = 5,
        Logout = 6,
        FailedLogin = 7,
        PasswordChanged = 8,

        // File operations
        Export = 9,
        Import = 10,

        // Workflow
        Approve = 11,
        Reject = 12,

        Failed = 13,
        Error = 14,
        Transfer = 15
    }
}
