namespace PosAdminTool.Contracts.V1.Services;

public enum ServiceRuntimeState
{
    Unknown,
    Running,
    Stopped,
    Transitioning,
    NotFound,
}
