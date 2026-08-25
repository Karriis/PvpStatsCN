using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace PvpStats.Services.Cloud;

internal static class CloudCredentialProtector {
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("PvpStatsCN.CloudUpload.v1");

    internal static string Protect(byte[] secret) {
        if(secret.Length < 32) {
            throw new ArgumentException("The upload secret must contain at least 32 bytes.", nameof(secret));
        }
        return Convert.ToBase64String(Transform(secret, true));
    }

    internal static byte[] Unprotect(string protectedSecret) {
        return Transform(Convert.FromBase64String(protectedSecret), false);
    }

    private static byte[] Transform(byte[] input, bool protect) {
        var inputBlob = CreateBlob(input);
        var entropyBlob = CreateBlob(Entropy);
        DataBlob outputBlob = default;
        try {
            var succeeded = protect
                ? CryptProtectData(ref inputBlob, null, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outputBlob)
                : CryptUnprotectData(ref inputBlob, IntPtr.Zero, ref entropyBlob, IntPtr.Zero, IntPtr.Zero, CryptProtectUiForbidden, out outputBlob);
            if(!succeeded) {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            var output = new byte[outputBlob.Length];
            Marshal.Copy(outputBlob.Data, output, 0, output.Length);
            return output;
        } finally {
            FreeBlob(ref inputBlob, false);
            FreeBlob(ref entropyBlob, false);
            FreeBlob(ref outputBlob, true);
        }
    }

    private static DataBlob CreateBlob(byte[] data) {
        var blob = new DataBlob { Length = data.Length, Data = Marshal.AllocHGlobal(data.Length) };
        Marshal.Copy(data, 0, blob.Data, data.Length);
        return blob;
    }

    private static void FreeBlob(ref DataBlob blob, bool localAlloc) {
        if(blob.Data == IntPtr.Zero) {
            return;
        }
        if(localAlloc) {
            LocalFree(blob.Data);
        } else {
            Marshal.FreeHGlobal(blob.Data);
        }
        blob.Data = IntPtr.Zero;
        blob.Length = 0;
    }

    private const uint CryptProtectUiForbidden = 0x1;

    [StructLayout(LayoutKind.Sequential)]
    private struct DataBlob {
        internal int Length;
        internal IntPtr Data;
    }

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptProtectData(ref DataBlob dataIn, string? description, ref DataBlob optionalEntropy, IntPtr reserved, IntPtr promptStruct, uint flags, out DataBlob dataOut);

    [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, ref DataBlob optionalEntropy, IntPtr reserved, IntPtr promptStruct, uint flags, out DataBlob dataOut);

    [DllImport("kernel32.dll")]
    private static extern IntPtr LocalFree(IntPtr memory);
}
