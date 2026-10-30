# External read-only feasibility probe; never a READY/recovery witness.
param(
 [Parameter(Mandatory=$true)][long]$WindowHandle,
 [Parameter(Mandatory=$true)][int]$ExpectedPid,
 [Parameter(Mandatory=$true)][string]$ExpectedName
)
$ErrorActionPreference='Stop'
$r=[ordered]@{
 schema='gf-phase1-uia-discovery/1'; scope='WINDOWS_CI_READ_ONLY_EXTERNAL_UI_PROBE'
 window_owner_verified=$false;uia_available=$false;uia_root_owner_verified=$false
 uia_exact_name_descendants=0;uia_exact_visible_pid_matched=0
 uia_list_ancestor_qualified=$false;msaa_available=$false
 msaa_root_children_present=$false;positive_ui_proof=$false
 operational_ready=$false;automatic_recovery_authorized=$false;reason='UNAVAILABLE'
}
try {
 if ($env:GITHUB_ACTIONS -ne 'true' -or $ExpectedName -notmatch '^GF-PHASE1-INVENTORY-[A-F0-9]{24}(-ABSENT)?$') {
   $r.reason='SCOPE_REFUSED'
 } else {
   Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class GfPhase1UiNative {
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
 [DllImport("oleacc.dll",PreserveSig=true)]
 public static extern int AccessibleObjectFromWindow(IntPtr hwnd,uint objectId,
    ref Guid iid,[MarshalAs(UnmanagedType.Interface)] out object accessible);
}
'@ -ErrorAction Stop
   $hwnd=[IntPtr]::new($WindowHandle)
   [uint32]$owner=0
   [void][GfPhase1UiNative]::GetWindowThreadProcessId($hwnd,[ref]$owner)
   if ($owner -ne [uint32]$ExpectedPid -or $owner -eq 0) {
     $r.reason='OWNER_MISMATCH'
   } else {
     $r.window_owner_verified=$true
     try {
       Add-Type -AssemblyName UIAutomationClient -ErrorAction Stop
       Add-Type -AssemblyName UIAutomationTypes -ErrorAction Stop
       $root=[System.Windows.Automation.AutomationElement]::FromHandle($hwnd)
       if ($null -ne $root) {
         $r.uia_available=$true
         if ($root.Current.ProcessId -eq $ExpectedPid) {
           $r.uia_root_owner_verified=$true
           $condition=New-Object System.Windows.Automation.PropertyCondition(
             [System.Windows.Automation.AutomationElement]::NameProperty,$ExpectedName)
           $hits=$root.FindAll([System.Windows.Automation.TreeScope]::Descendants,$condition)
           $r.uia_exact_name_descendants=[Math]::Min(20,$hits.Count)
           for($i=0;$i -lt [Math]::Min(20,$hits.Count);$i++){
             $element=$hits.Item($i)
             try {
               $bounds=$element.Current.BoundingRectangle
               if($element.Current.ProcessId -eq $ExpectedPid -and
                  !$element.Current.IsOffscreen -and
                  $bounds.Width -gt 0 -and $bounds.Height -gt 0 -and
                  $element.Current.ControlType -ne [System.Windows.Automation.ControlType]::Edit) {
                 $r.uia_exact_visible_pid_matched++
               }
             } catch {}
           }
           # Owner-drawn DarkProgramList has no independently identified
           # item ancestor. Do not trust an unrelated matching UIA node.
           $r.reason='LIST_ITEM_ANCESTOR_NOT_QUALIFIED'
         } else {$r.reason='UIA_ROOT_OWNER_MISMATCH'}
       }
     } catch { $r.reason='UIA_UNAVAILABLE' }
     try {
       Add-Type -AssemblyName Accessibility -ErrorAction Stop
       [Guid]$iid=([Accessibility.IAccessible]).GUID
       [object]$access=$null
       $hr=[GfPhase1UiNative]::AccessibleObjectFromWindow(
         $hwnd,[uint32]4294967292,[ref]$iid,[ref]$access)
       if($hr -eq 0 -and $null -ne $access) {
         $r.msaa_available=$true
         $r.msaa_root_children_present=([Accessibility.IAccessible]$access).accChildCount -gt 0
         [void][System.Runtime.InteropServices.Marshal]::ReleaseComObject($access)
       }
     } catch {}
   }
 }
} catch { $r.reason='PROBE_EXCEPTION' }
# No user names, item names, PID, HWND, pixels or raw COM data in output.
$r | ConvertTo-Json -Compress -Depth 3
