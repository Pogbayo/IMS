namespace IMS.Domain.Enums
{
    public enum AuditAction
    {
        // Data operations
        Create = 1,
        Update = 2,
        Delete = 3,

        // Authentication
        Login = 4,
        Logout = 5,
        FailedLogin = 6,
        PasswordChanged = 7,

        // File operations
        Export = 8,
        Import = 9,

        // Workflow
        Approve = 10,
        Reject = 11
    }
}
