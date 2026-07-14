namespace PosAdminTool.Domain.Models;

public sealed class ClientDbConfig
{
    public string Server { get; set; } = string.Empty;

    public string User { get; set; } = "sa";

    public string Password { get; set; } = string.Empty;

    public string Database { get; set; } = string.Empty;

    public ClientDbConfig Clone()
    {
        return new ClientDbConfig
        {
            Server = Server,
            User = User,
            Password = Password,
            Database = Database
        };
    }
}
