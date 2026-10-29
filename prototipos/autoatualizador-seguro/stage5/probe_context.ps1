# Read-only capability probe in a disposable runner. Never changes/duplicates/uses a token for execution.
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
public static class GfU5TokenCapability {
    public static int[] Read() {
        SafeAccessTokenHandle current;
        if(!OpenProcessToken(new IntPtr(-1),8,out current))throw new Win32Exception(Marshal.GetLastWin32Error());
        using(current) {
            int elevated,type,returned;
            if(!GetInt(current,20,out elevated,4,out returned))throw new Win32Exception(Marshal.GetLastWin32Error());
            if(!GetInt(current,18,out type,4,out returned))throw new Win32Exception(Marshal.GetLastWin32Error());
            IntPtr linked;
            bool ok=GetPointer(current,19,out linked,IntPtr.Size,out returned);
            int error=ok?0:Marshal.GetLastWin32Error(),linkedElevated=-1;
            if(ok) {
                using(var owned=new SafeAccessTokenHandle(linked)) {
                    if(!GetInt(owned,20,out linkedElevated,4,out returned))throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
            return new[]{elevated,type,ok?1:0,error,linkedElevated};
        }
    }
    [DllImport("advapi32.dll",SetLastError=true)]static extern bool OpenProcessToken(IntPtr process,int access,out SafeAccessTokenHandle token);
    [DllImport("advapi32.dll",EntryPoint="GetTokenInformation",SetLastError=true)]static extern bool GetInt(SafeAccessTokenHandle token,int kind,out int value,int length,out int returned);
    [DllImport("advapi32.dll",EntryPoint="GetTokenInformation",SetLastError=true)]static extern bool GetPointer(SafeAccessTokenHandle token,int kind,out IntPtr value,int length,out int returned);
}
'@
$facts = [GfU5TokenCapability]::Read()
$result = [ordered]@{
    schema = 1
    stage = 5
    probe = 'read_only_existing_token_capability'
    elevated = ($facts[0] -ne 0)
    elevation_type = $facts[1]
    linked_query_success = ($facts[2] -ne 0)
    linked_query_error = $facts[3]
    linked_elevated = $(if ($facts[2] -ne 0) { $facts[4] -ne 0 } else { $null })
    non_elevated_existing_token_available = (($facts[0] -eq 0) -or (($facts[2] -ne 0) -and ($facts[4] -eq 0)))
    non_elevated_positive_handshake_executed = $false
    foreign_session_user_handshake_executed = $false
    token_modified_or_duplicated = $false
    account_acl_uac_privilege_changed = $false
    processes_spawned_with_token = 0
    gf_executed = $false
}
'GF_U5_CONTEXT_REPORT ' + ($result | ConvertTo-Json -Compress)
