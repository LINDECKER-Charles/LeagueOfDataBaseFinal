# Top kernel pool tags by bytes (what poolmon shows), via NtQuerySystemInformation
# (SystemPoolTagInformation = 22). Usage: pool-tags.ps1 [-Top 15]
param([int]$Top = 15)
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class PoolTags {
  [DllImport("ntdll.dll")]
  static extern int NtQuerySystemInformation(int infoClass, IntPtr buffer, int length, out int returned);
  public static object[] Read() {
    int size = 1 << 20;
    while (true) {
      IntPtr buffer = Marshal.AllocHGlobal(size);
      try {
        int returned;
        int status = NtQuerySystemInformation(22, buffer, size, out returned);
        if (status == unchecked((int)0xC0000004)) { size = Math.Max(size * 2, returned + 4096); continue; }
        if (status != 0) throw new Exception("NtQuerySystemInformation status 0x" + status.ToString("X8"));
        int count = Marshal.ReadInt32(buffer);
        var result = new object[count];
        for (int i = 0; i < count; i++) {
          IntPtr entry = IntPtr.Add(buffer, 8 + i * 40);
          byte[] tag = new byte[4];
          Marshal.Copy(entry, tag, 0, 4);
          long paged = Marshal.ReadInt64(entry, 16);
          long nonPaged = Marshal.ReadInt64(entry, 32);
          result[i] = new object[] { System.Text.Encoding.ASCII.GetString(tag), paged, nonPaged };
        }
        return result;
      } finally { Marshal.FreeHGlobal(buffer); }
    }
  }
}
'@
[PoolTags]::Read() |
  ForEach-Object { [pscustomobject]@{ Tag = $_[0]; PagedMB = [int]($_[1] / 1MB); NonPagedMB = [int]($_[2] / 1MB) } } |
  Sort-Object { $_.PagedMB + $_.NonPagedMB } -Descending |
  Select-Object -First $Top |
  Format-Table -AutoSize | Out-String
