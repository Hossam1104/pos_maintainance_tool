using System.Runtime.InteropServices;

namespace PosAdminTool.Infrastructure.Smb;

/// <summary>
/// Establishes a scoped, authenticated SMB connection to a UNC share root for the lifetime
/// of the instance, so unpackaged/non-domain client machines can read the remote backup folder
/// with the configured RDB credentials without a persistent `net use` mapping.
/// </summary>
public sealed class SmbConnectionScope : IDisposable
{
    private readonly string? _connectedRoot;

    private SmbConnectionScope(string? connectedRoot)
    {
        _connectedRoot = connectedRoot;
    }

    public static SmbConnectionScope Connect(string shareRoot, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return new SmbConnectionScope(null);
        }

        var netResource = new NetResource
        {
            dwType = ResourceTypeDisk,
            lpRemoteName = shareRoot
        };

        var result = WNetAddConnection2(ref netResource, password, username, 0);
        if (result != NoError && result != ErrorAlreadyAssigned)
        {
            throw new IOException($"Failed to connect to '{shareRoot}' (Win32 error {result}).");
        }

        return new SmbConnectionScope(shareRoot);
    }

    public void Dispose()
    {
        if (_connectedRoot is not null)
        {
            WNetCancelConnection2(_connectedRoot, 0, true);
        }
    }

    private const int ResourceTypeDisk = 0x00000001;
    private const int NoError = 0;
    private const int ErrorAlreadyAssigned = 85;

    [StructLayout(LayoutKind.Sequential)]
    private struct NetResource
    {
        public int dwScope;
        public int dwType;
        public int dwDisplayType;
        public int dwUsage;
        public string? lpLocalName;
        public string? lpRemoteName;
        public string? lpComment;
        public string? lpProvider;
    }

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetAddConnection2(ref NetResource netResource, string? password, string? username, int flags);

    [DllImport("mpr.dll", CharSet = CharSet.Unicode)]
    private static extern int WNetCancelConnection2(string name, int flags, bool force);
}
