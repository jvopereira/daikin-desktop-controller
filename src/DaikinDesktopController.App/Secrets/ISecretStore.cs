using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Secrets;
public interface ISecretStore
{
    DeviceSecretKey? Read();
    void Save(DeviceSecretKey key);
    void Delete();
}
