namespace PreschoolConnect.Application.Models;

public sealed class AuditPolicyException(string message) : InvalidOperationException(message);
