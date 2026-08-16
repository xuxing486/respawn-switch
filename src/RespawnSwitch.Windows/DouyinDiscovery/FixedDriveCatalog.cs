namespace RespawnSwitch.Windows.DouyinDiscovery;

public sealed class FixedDriveCatalog : IFixedDriveCatalog
{
    public IReadOnlyList<string> GetFixedDriveRoots() =>
        DriveInfo.GetDrives()
            .Where(drive => IsEligible(drive.DriveType, SafeIsReady(drive)))
            .Select(drive => drive.RootDirectory.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    internal static bool IsEligible(DriveType driveType, bool isReady) =>
        driveType == DriveType.Fixed && isReady;

    private static bool SafeIsReady(DriveInfo drive)
    {
        try
        {
            return drive.IsReady;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
