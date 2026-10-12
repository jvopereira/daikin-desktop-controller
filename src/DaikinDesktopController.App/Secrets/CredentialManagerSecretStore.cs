using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using DaikinDesktopController.Device;
namespace DaikinDesktopController.App.Secrets;
public sealed class CredentialManagerSecretStore : ISecretStore
{
    public const string Target = "DaikinDesktopController/DeviceSecretKey";
    private const int NotFound = 1168;
    public DeviceSecretKey? Read()
    {
        if (!CredRead(Target, 1, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == NotFound) return null;
            throw new Win32Exception(error);
        }
        try
        {
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.BlobSize > 88 || credential.BlobSize % 2 != 0) return null;
            var text = Marshal.PtrToStringUni(credential.Blob, (int)credential.BlobSize / 2);
            return DeviceSecretKey.TryParse(text, out var key) ? key : null;
        }
        finally { CredFree(pointer); }
    }
    public void Save(DeviceSecretKey key)
    {
        var bytes = Encoding.Unicode.GetBytes(key.Text);
        var blob = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential { Type = 1, TargetName = Target, BlobSize = (uint)bytes.Length,
                Blob = blob, Persist = 2, UserName = "DaikinDesktopController" };
            if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            Marshal.FreeHGlobal(blob);
        }
    }
    public void Delete()
    {
        if (!CredDelete(Target, 1, 0))
        {
            var error = Marshal.GetLastWin32Error();
            if (error != NotFound) throw new Win32Exception(error);
        }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        public string? TargetName;
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint BlobSize;
        public IntPtr Blob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string? TargetAlias;
        public string? UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr credential);
}
