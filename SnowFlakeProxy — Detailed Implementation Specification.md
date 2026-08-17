# SnowFlakeProxy — Detailed Implementation Specification

## 1. Purpose of this document

This document is the **authoritative implementation specification** for a custom ASCOM FilterWheel proxy driver for the Wanderer Astro Snowflake filter wheel.

The implementation agent must follow this design closely.

Do **not** redesign the architecture unless an implementation detail is proven impossible.

Do **not** substitute Alpaca for COM.

Do **not** modify JustAHub.

Do **not** modify or patch the Wanderer executable.

Do **not** bypass the Wanderer driver and communicate directly with the Snowflake serial protocol.

The proxy exists specifically to correct defects and interoperability problems in the existing Wanderer ASCOM driver while preserving the vendor driver as the sole hardware interface.

The working directory is:

```text
C:\dev\Self\NINA\SnowFlakeProxy
```

This directory does not yet exist and must be created as part of initial setup.

---

## 1.1 Specification revision notes

This revision hardens the original specification for first-try implementation. The architecture is unchanged. The additions are:

- a mandatory ASCOM Platform 7 version gate (section 8.2);
- the exact identity of the required Visual Studio template extension (section 8.3);
- a defined fallback when the Visual Studio template wizard cannot be run non-interactively (section 11.1);
- explicit mapping of this design onto the template's generated three-level structure (section 11.2);
- Platform assembly reference rules (section 18.1);
- the LocalServer COM dispatch/apartment model and its consequences (section 22.1);
- an atomicity rule and a bounded wait for vendor setter acceptance (section 33);
- a timeout check in the move-monitor loop (section 34);
- defined recovery paths out of Faulted states (section 38.1);
- precise multi-client connection-concurrency semantics and a connect timeout (section 43.1);
- two additional stored settings (section 63);
- test-project access to internal types (section 75);
- a realistic latency carve-out for the multi-client integration script (section 88);
- a ConformU contingency for the conflicting-move rule (section 112).

---

# 2. Problem statement

The installed vendor filter-wheel driver is:

```text
ProgID:
ASCOM.WandererSnowflakeFilterWheel1.FilterWheel
```

The vendor driver reports:

```text
InterfaceVersion: 3
DriverVersion: 1.0
```

The managed assembly containing the COM LocalServer is:

```text
ASCOM.WandererSnowflakeFilterWheel1.exe
```

The assembly itself reports version:

```text
6.6.0.0
```

The driver is a 64-bit .NET LocalServer.

The proxy must treat this vendor driver as a **black box** and access it only through its public ASCOM COM interface.

---

# 3. Known Wanderer-driver defects that the proxy must mask

These are experimentally verified behaviors and are requirements for the proxy, not hypotheses.

## 3.1 Filter-name incompatibility

The Snowflake configuration contains simple filter names:

```text
L
R
G
B
H
S
O
D
```

The Wanderer ASCOM `Names` property instead exposes:

```text
Filter 1 (L)
Filter 2 (R)
Filter 3 (G)
Filter 4 (B)
Filter 5 (H)
Filter 6 (S)
Filter 7 (O)
Filter 8 (D)
```

Innovations Foresight SkyGuard fails to populate its Filter Settings list when presented with these decorated names.

A controlled test showed that SkyGuard works immediately when the same wheel instead exposes:

```text
L
R
G
B
H
S
O
D
```

The proxy therefore must normalize the Wanderer-generated names.

---

## 3.2 `Position` getter blocks during wheel movement

A direct ASCOM test of the untouched Wanderer driver produced:

```text
Starting position: 0
Commanding position: 1

0 ms -> 8446 ms   Position = 1   getter time = 8445 ms
```

The `Position` getter blocked for essentially the entire mechanical movement.

This violates the defined FilterWheel `Position` asynchronous model. ASCOM requires `Position` writes to initiate movement without waiting for completion, and requires reads to return `-1` while the wheel is moving. Returning `-1` during movement is explicitly mandatory.

The proxy must hide this blocking implementation completely from clients.

---

## 3.3 Multi-client access makes the vendor behavior substantially worse

Two simultaneous clients accessing the vendor through a multi-client hub produced behavior such as:

```text
02:37:02.216  command Position = 1
02:37:02.739  setter returned after 520 ms

02:37:11.752  Position=0  getter=9006 ms
02:37:13.395  Position=0  getter=1510 ms
02:37:15.037  Position=0  getter=1533 ms
02:37:16.680  Position=0  getter=1525 ms
02:37:18.323  Position=0  getter=1522 ms
02:37:19.965  Position=0  getter=1533 ms
02:37:21.051  Position=1  getter=967 ms
```

A second client simultaneously observed:

```text
02:37:02.741  Position=0  getter=1509 ms
02:37:12.299  Position=0  getter=9448 ms
02:37:13.941  Position=0  getter=1508 ms
02:37:15.584  Position=0  getter=1534 ms
02:37:17.227  Position=0  getter=1523 ms
02:37:18.870  Position=0  getter=1527 ms
02:37:19.957  Position=1  getter=978 ms
```

Thus the vendor driver can:

- block `Position` for many seconds;
- return the **old valid slot** during an active move;
- expose stale state for many seconds;
- behave substantially worse when multiple clients poll simultaneously.

The proxy must guarantee that this behavior never becomes visible through the proxy API.

---

# 4. ASCOM behavior the proxy must implement

The proxy itself will advertise:

```text
InterfaceVersion = 3
```

Therefore it must conform to `IFilterWheelV3`.

For `Position`, the required public behavior is:

```text
Position = target
        |
        v
movement successfully starts
        |
        v
setter returns

while moving:
Position -> -1
Position -> -1
Position -> -1

when stationary at target:
Position -> target
```

ASCOM explicitly defines `Position` as non-blocking and states that valid filter slot numbers must not be reported while the wheel is moving.

The proxy's **public `Position` getter must therefore never call the Wanderer `Position` getter**.

This is the most important architectural rule in the project.

---

# 5. High-level architecture

The runtime topology must be:

```text
                         NINA
                           |
                           |
SkyGuard -------- SnowFlakeProxy -------- other ASCOM clients
                           |
                           |
                 one serialized vendor
                    ASCOM connection
                           |
                           v
ASCOM.WandererSnowflakeFilterWheel1.FilterWheel
                           |
                           v
                 Wanderer Snowflake
```

There must be **no JustAHub in this runtime architecture**.

SnowFlakeProxy itself is the multi-client hub.

ASCOM's current executable LocalServer design specifically supports multiple clients through multiple COM driver instances while sharing a single hardware implementation. The ASCOM LocalServer template is intended for this architecture.

---

# 6. Mandatory implementation technology

Use:

```text
Language: C#
IDE: Visual Studio 2022
Driver architecture: ASCOM executable LocalServer
ASCOM device type: FilterWheel
Interface: IFilterWheelV3
```

Visual Studio 2022 is already installed.

ASCOM currently recommends Visual Studio 2022 and C# for Windows COM driver development, and Platform 7 supplies executable LocalServer templates rather than the old in-process DLL driver templates.

Do **not** create:

```text
an in-process COM DLL
a C++ COM server
an Alpaca-only driver
a Windows service
a standalone serial driver
```

---

# 7. Coding-style requirements

These rules apply to all new source code.

## 7.1 Variable naming

All variables, fields, parameters, and local variables must use:

```text
snake_case
```

Examples:

```csharp
short cached_position;
short target_position;
object state_lock;
int connection_count;
string[] cached_names;
TaskCompletionSource<bool> completion_source;
```

Do not write:

```csharp
cachedPosition
targetPosition
stateLock
connectionCount
```

ASCOM-mandated public property and method names such as:

```text
Position
Connected
Names
FocusOffsets
Connect
Disconnect
SetupDialog
```

must of course retain the names required by the interface.

Class names, enum names, and public non-interface method names may use normal C# PascalCase.

---

## 7.2 Braces

Use braces for **all control-flow bodies**, including single-statement bodies.

Correct:

```csharp
if (is_moving)
{
    return -1;
}

for (int index = 0; index < names.Length; index++)
{
    ...
}
```

Do not generate brace-less control flow.

---

## 7.3 PowerShell

Any project scripts must be compatible with:

```text
Windows PowerShell 5.1
```

Do not use PowerShell 7-only features.

---

# 8. Development environment setup

## 8.1 Verify the .NET desktop development workload

Open:

```text
Visual Studio Installer
```

Select the installed Visual Studio 2022 instance and verify that:

```text
.NET desktop development
```

is installed.

Do not change unrelated Visual Studio workloads.

---

## 8.2 Verify ASCOM Platform

A functioning ASCOM Platform is already present because the existing Wanderer driver and ASCOM Chooser are in use.

Do not gratuitously uninstall or replace it.

The development machine should use the current installed Platform 7 environment.

**Verify the installed Platform version explicitly before creating any project.** The installed Wanderer driver is a Platform 6.6-era build, so a working Chooser does not prove that Platform 7 is present.

```powershell
$util = New-Object -ComObject "ASCOM.Utilities.Util"

try {
    Write-Host "ASCOM Platform version:" $util.PlatformVersion
}
finally {
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($util)
}
```

If the reported version is less than 7, stop and install the current ASCOM Platform 7 release from ascom-standards.org before proceeding.

The Platform 7 project templates and the `IFilterWheelV3` interface do not exist on a Platform 6.x machine, and generated projects will not compile.

Platform 7 is backward compatible with installed Platform 6 drivers, so upgrading does not endanger the existing Wanderer driver.

ASCOM Platform 7 introduced the V3 FilterWheel interface additions, including asynchronous `Connect()` / `Disconnect()`, `Connecting`, and `DeviceState`.

---

## 8.3 Install the ASCOM Visual Studio project templates if necessary

In Visual Studio 2022:

```text
Extensions
    ->
Manage Extensions
```

Search for:

```text
ASCOM
```

The required extension is exactly:

```text
ASCOM Platform 7 Project Templates (VS2022/26)
```

published by Peter Simpson (the ASCOM Platform maintainer).

A similarly named extension also exists in the Marketplace:

```text
ASCOM Platform 6 Project Templates (VS2022)
```

Do **not** use the Platform 6 extension; it generates Platform 6 interface drivers without `IFilterWheelV3`.

Install the Platform 7 extension if it is not already installed.

Restart Visual Studio when requested.

The official ASCOM documentation states that current driver templates are distributed as Visual Studio extensions, and that Platform 7 supplies only LocalServer (executable) driver templates. Generated projects require an installed Platform 7 to compile.

---

# 9. Query the exact Wanderer driver identity before creating constants

Before coding the driver identity, run this PowerShell 5.1 script:

```powershell
$filter_wheel = New-Object -ComObject "ASCOM.WandererSnowflakeFilterWheel1.FilterWheel"

try {
    Write-Host "Name:" $filter_wheel.Name
    Write-Host "Description:" $filter_wheel.Description
    Write-Host "InterfaceVersion:" $filter_wheel.InterfaceVersion
    Write-Host "DriverVersion:" $filter_wheel.DriverVersion
    Write-Host "DriverInfo:" $filter_wheel.DriverInfo
}
finally {
    if (($null -ne $filter_wheel) -and
        [Runtime.InteropServices.Marshal]::IsComObject($filter_wheel)) {
        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($filter_wheel)
    }
}
```

Record the exact value returned by:

```text
Name
```

Call it:

```text
vendor_driver_name
```

The proxy's user-visible name must be exactly:

```text
vendor_driver_name + " Proxy"
```

For example, if the original reports:

```text
Wanderer Snowflake Filter Wheel
```

the proxy must report:

```text
Wanderer Snowflake Filter Wheel Proxy
```

Do not guess the vendor name.

Do not proceed with the final COM `ServedClassName` until the exact original `Name` is known.

---

# 10. Create the development folder and solution

Use Windows PowerShell 5.1:

```powershell
New-Item `
    -ItemType Directory `
    -Path "C:\dev\Self\NINA\SnowFlakeProxy" `
    -Force | Out-Null
```

The final root must be:

```text
C:\dev\Self\NINA\SnowFlakeProxy
```

The solution must ultimately live directly under that root.

Preferred layout:

```text
C:\dev\Self\NINA\SnowFlakeProxy\
    SnowFlakeProxy.sln
    src\
    tests\
    tools\
    docs\
```

If the ASCOM LocalServer project template insists on its own generated structure, preserve the ASCOM template infrastructure, but reorganize only after the first generated version builds and registers successfully.

Do not manually recreate the ASCOM LocalServer bootstrap code if the official template can generate it.

---

# 11. Initial ASCOM project creation

Create a new project using the official:

```text
ASCOM LocalServer
```

C# template.

Choose:

```text
Device type: FilterWheel
```

Use these project identity values unless the ASCOM wizard requires slightly different formatting:

```text
Solution name:
SnowFlakeProxy

Assembly name:
ASCOM.SnowFlakeProxy

Default namespace:
ASCOM.SnowFlakeProxy

Proxy ProgID:
ASCOM.SnowFlakeProxy.FilterWheel
```

The driver COM class must receive a newly generated GUID.

Never reuse:

```text
the Wanderer driver's CLSID
an ASCOM sample GUID
a template placeholder GUID
```

Allow the ASCOM template to create fresh GUIDs where possible.

---

# 11.1 Fallback if the template wizard cannot be run non-interactively

The ASCOM templates are ordinary Visual Studio project templates driven through the interactive New Project dialog.

If the implementing agent cannot operate the Visual Studio GUI, use these fallbacks in order:

1. Ask the user to run the New Project wizard once, supplying the exact identity values from section 11, then continue automated work against the generated solution.

2. Locate the installed extension's template packages on disk (under the Visual Studio extensions directory), expand the LocalServer FilterWheel template manually, and perform the substitutions the wizard would have performed (`$safeprojectname$` and similar template parameters, plus fresh GUIDs generated with `[guid]::NewGuid()`).

Do **not** hand-write the LocalServer bootstrap (COM class factories, registration/unregistration, garbage-collection support, main message loop) from memory.

Its details are subtle, and the current template version is documented to compile cleanly and pass ConformU out of the box. Start from that known-good code.

---

# 11.2 Mapping this specification onto the generated template structure

The current LocalServer template generates a three-level architecture:

```text
1. Driver instance class
   one instance per client COM connection

2. Hardware class
   a static singleton per served driver
   the single source of truth shared by all driver instances

3. Shared resources class
   a singleton shared by every driver served by the LocalServer
```

Map this specification onto that structure instead of fighting it:

- The generated static hardware class becomes a **thin delegation layer** over `SnowflakeProxyController` (section 21). Keep the static class as the template's integration point, but put all real logic in the instantiable controller so it is unit-testable against a fake underlying wheel.

- The generated driver instance class is the lightweight per-client object of section 22. It holds the `client_id` and lease flag and forwards everything else to the controller.

- The Platform 7 template already includes per-client connection tracking with unique client identity. Adapt that mechanism into the lease model of sections 39–41 rather than deleting it.

- `HardwareWorker`, `IUnderlyingFilterWheel`, and `WandererFilterWheelAdapter` are new classes owned by the controller.

---

# 12. LocalServer build configuration

Keep the Platform Target exactly as generated by the ASCOM LocalServer template.

The current template documentation specifically says to retain:

```text
x86
```

for the LocalServer project and explains that COM handles communication between 32-bit and 64-bit clients because the driver is an out-of-process executable.

Therefore:

**Do not change the ASCOM LocalServer to x64 just because the Wanderer LocalServer reports `Amd64`.**

The Wanderer driver itself is also out-of-process COM.

Cross-bitness COM activation is not the architectural problem we are solving.

---

# 13. Do not change the generated target framework unless required

Use the framework generated by the current official ASCOM LocalServer template.

Do not arbitrarily migrate the template to:

```text
.NET 8
.NET 9
.NET 10
```

or another framework.

Do not rewrite the LocalServer infrastructure using modern .NET hosting.

The first objective is a conforming ASCOM COM LocalServer, not modernization.

---

# 14. First build checkpoint

Before implementing proxy functionality:

1. Build the generated solution.
2. Ensure there are no compilation errors.
3. Do not run it directly.
4. Register it with `/regserver`.
5. Confirm the placeholder/new FilterWheel appears in ASCOM Chooser.

The official LocalServer template requires registration using:

```text
LocalServer.exe /regserver
```

from an elevated shell.

It uses:

```text
/unregserver
```

to remove registration.

Do **not** use `REGASM` on the LocalServer executable.

Create PowerShell 5.1 scripts later for these operations.

---

# 15. Desired source tree after scaffold cleanup

After the generated ASCOM template has been proven to build and register, evolve the project toward this structure:

```text
SnowFlakeProxy\
│
├── SnowFlakeProxy.sln
│
├── src\
│   └── ASCOM.SnowFlakeProxy\
│       │
│       ├── Driver\
│       │   └── FilterWheel.cs
│       │
│       ├── Core\
│       │   ├── SnowflakeProxyController.cs
│       │   ├── ProxyState.cs
│       │   ├── MoveState.cs
│       │   └── ConnectionState.cs
│       │
│       ├── Hardware\
│       │   ├── HardwareWorker.cs
│       │   ├── HardwareRequest.cs
│       │   ├── IUnderlyingFilterWheel.cs
│       │   └── WandererFilterWheelAdapter.cs
│       │
│       ├── Configuration\
│       │   ├── ProxySettings.cs
│       │   └── ProxySettingsStore.cs
│       │
│       ├── UI\
│       │   └── SetupDialogForm.cs
│       │
│       ├── Diagnostics\
│       │   └── ProxyLogger.cs
│       │
│       └── generated ASCOM LocalServer infrastructure
│
├── tests\
│   └── SnowFlakeProxy.Tests\
│       ├── FakeUnderlyingFilterWheel.cs
│       ├── ConnectionTests.cs
│       ├── PositionTests.cs
│       ├── ConcurrencyTests.cs
│       ├── NameNormalizationTests.cs
│       └── ErrorRecoveryTests.cs
│
├── tools\
│   ├── Register-Driver.ps1
│   ├── Unregister-Driver.ps1
│   ├── Query-Wanderer.ps1
│   ├── Test-ProxySingleClient.ps1
│   └── Test-ProxyMultiClient.ps1
│
└── docs\
    ├── Architecture.md
    ├── TestPlan.md
    └── BaselineMeasurements.md
```

Do not add additional abstraction layers unless a concrete need appears.

---

# 16. Critical architectural invariant

There must be exactly **one logical owner** of:

```text
ASCOM.DriverAccess.FilterWheel
```

for the Wanderer ProgID.

The object representing the Wanderer driver must:

- be constructed on the hardware worker thread;
- be used only on the hardware worker thread;
- be disconnected on the hardware worker thread;
- be disposed on the hardware worker thread.

No public ASCOM driver instance may hold its own Wanderer object.

No NINA connection gets its own Wanderer object.

No SkyGuard connection gets its own Wanderer object.

There is exactly one underlying vendor connection for the physical wheel.

---

# 17. Underlying driver adapter

Create:

```csharp
internal interface IUnderlyingFilterWheel
```

The interface should contain only the functionality the proxy actually needs.

Recommended surface:

```csharp
bool Connected { get; set; }

string Name { get; }

string Description { get; }

string DriverVersion { get; }

string DriverInfo { get; }

short InterfaceVersion { get; }

string[] Names { get; }

int[] FocusOffsets { get; }

short Position { get; set; }

void SetupDialog();

void Dispose();
```

Do not expose arbitrary COM members through this abstraction.

The concrete production implementation:

```text
WandererFilterWheelAdapter
```

must internally use:

```csharp
ASCOM.DriverAccess.FilterWheel
```

constructed with:

```text
ASCOM.WandererSnowflakeFilterWheel1.FilterWheel
```

The current ASCOM `DriverAccess.FilterWheel` wrapper supports the FilterWheel interface and resolves calls to the underlying COM driver.

---

# 18. Do not use `dynamic` for the underlying driver

Do not implement:

```csharp
dynamic wanderer;
```

Do not manually call:

```text
Type.GetTypeFromProgID
Activator.CreateInstance
```

unless the official `ASCOM.DriverAccess.FilterWheel` proves unusable.

Use the strongly typed ASCOM wrapper.

This makes exceptions, `Position`, `Names`, and other ASCOM types explicit.

---

# 18.1 Platform assembly references

Use only the references the template generates, which come from the installed ASCOM Platform, in particular:

```text
ASCOM.DriverAccess
ASCOM.DeviceInterfaces
ASCOM.Utilities
ASCOM.Exceptions (as referenced by the template)
```

Do **not** add NuGet packages that duplicate Platform functionality — in particular the cross-platform `ASCOM.Com`, `ASCOM.Tools`, `ASCOM.Common`, or `ASCOM.Alpaca` libraries.

Mixing the .NET Framework Platform assemblies with the cross-platform library set creates two incompatible copies of core ASCOM types and produces confusing type-identity and exception-mapping failures.

---

# 19. Hardware worker

Create exactly one dedicated hardware worker.

Recommended implementation:

```text
HardwareWorker
```

using:

```csharp
Thread
BlockingCollection<HardwareRequest>
```

The hardware thread must be created explicitly and must be the only thread that invokes vendor ASCOM methods.

Recommended thread properties:

```text
Name:
SnowFlakeProxy Hardware Worker

IsBackground:
true

Apartment:
STA
```

Create the underlying Wanderer ASCOM object **inside this thread**, not in the caller's thread and then pass it across.

Do not implement vendor calls as arbitrary:

```csharp
Task.Run(...)
```

operations.

Do not allow multiple background tasks to query Wanderer simultaneously.

That would merely recreate the existing multi-client failure on hidden threads.

Two threading notes for correctness:

- The STA apartment matters only because the vendor RCW is created and used exclusively on this one thread; no cross-apartment marshaling of the vendor object ever occurs.

- The worker blocks on `BlockingCollection.Take` while idle. This is safe on an STA thread because CLR managed blocking waits on STA threads perform a pumping wait. Do not add `Application.DoEvents` or a manual message loop to the worker.

---

# 20. Hardware-worker serialization rule

The following must always be true:

```text
maximum simultaneous underlying Wanderer calls = 1
```

This must be tested.

Not:

```text
usually 1
```

Not:

```text
1 per client
```

Not:

```text
1 Position getter plus one setter
```

Exactly:

```text
<= 1
```

for all vendor calls.

This is the central concurrency guarantee.

The ASCOM LocalServer documentation itself warns that multiple clients may instantiate driver code concurrently and that communications must be serialized when the hardware does not support concurrent operations.

---

# 21. Shared controller

Create:

```text
SnowflakeProxyController
```

as the one shared hardware-state authority for every COM FilterWheel instance.

Conceptually:

```text
FilterWheel COM instance A ----\
                                \
FilterWheel COM instance B ------ SnowflakeProxyController
                                /
FilterWheel COM instance C ----/
                                      |
                                      v
                               HardwareWorker
                                      |
                                      v
                              Wanderer driver
```

The controller must own:

```text
connection leases
physical connection state
cached filter names
cached focus offsets
cached stationary position
target position
move state
connection state
last hardware error
settings
```

---

# 22. Public driver instances must be lightweight

Every ASCOM client receives its own COM driver object from the LocalServer.

Each instance should contain little more than:

```text
client identifier
whether this client owns a connection lease
reference to shared controller
per-instance trace identity if needed
```

Do not duplicate device state in each instance.

There must be a **single source of truth** in the shared controller.

This matches the three-level architecture described by the ASCOM LocalServer model: client driver instances, a shared hardware implementation, and common resources.

---

# 22.1 LocalServer COM dispatch model

The template's LocalServer registers its COM class factories from the main thread, which is an STA running a message loop.

Consequently every public driver instance lives in that main STA, and **all client COM calls — from every connected client — are dispatched onto that one thread**.

This has three practical consequences:

1. The public `Position` getter must be nothing more than a lock acquisition and a memory read (section 27). Any delay in any public member delays every client.

2. The only synchronous waits permitted on public code paths are:

```text
the Position setter's bounded wait for vendor setter acceptance (section 33)

the legacy Connected = true / false waits (section 42)
```

Both must be bounded by the timeouts defined in this document.

3. Managed blocking waits on an STA thread perform a pumping wait, so incoming COM calls (for example another client's cached `Position` read) can still be dispatched re-entrantly while a setter is waiting. This re-entrancy is safe here **because `state_lock` is never held across any wait** (section 23). Do not "fix" the re-entrancy by holding locks across waits; that inverts the design and creates deadlocks.

Never introduce an unbounded wait, or a non-pumping native wait (such as a raw `WaitForSingleObject` P/Invoke), on a public COM code path.

---

# 23. Shared state locking

Use one clearly defined object such as:

```csharp
private readonly object state_lock;
```

for quick shared-state transitions.

Never hold `state_lock` while:

```text
calling the Wanderer driver
waiting for a worker Task
sleeping
displaying a dialog
writing a long log message
```

The pattern should be:

```text
lock
    read/update small state
unlock

perform potentially blocking work
```

This is mandatory to prevent deadlocks.

---

# 24. State model

Create explicit enums.

## Connection state

```csharp
internal enum ConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Disconnecting,
    Faulted
}
```

## Move state

```csharp
internal enum MoveState
{
    Idle,
    Starting,
    Moving,
    Faulted
}
```

Do not replace this with an assortment of loosely related booleans.

---

# 25. Shared state fields

The controller should maintain at least:

```text
ConnectionState connection_state

MoveState move_state

short cached_position

short target_position

string[] cached_names

int[] cached_focus_offsets

HashSet<Guid> connection_leases

Exception last_connection_error

Exception last_move_error

DateTime last_position_confirmation_utc
```

Optional useful diagnostics:

```text
long move_sequence
long vendor_position_read_count
long vendor_position_set_count
long public_position_get_count
long public_position_get_while_moving_count
```

---

# 26. Meaning of `cached_position`

`cached_position` means:

> The last physical slot positively confirmed while the wheel was stationary.

It must **never** be changed simply because a client requested a new target.

Example:

```text
cached_position = 0

client requests:
Position = 4
```

Do not immediately do:

```text
cached_position = 4
```

Instead:

```text
cached_position = 0
target_position = 4
move_state = Starting
```

Public `Position` then returns:

```text
-1
```

until the vendor worker confirms slot 4.

Only then:

```text
cached_position = 4
move_state = Idle
```

---

# 27. Public `Position` getter

This property is the primary reason the proxy exists.

The implementation must be constant-time and memory-only.

Conceptually:

```csharp
public short Position
{
    get
    {
        CheckConnected("Position");

        lock (state_lock)
        {
            if (move_state == MoveState.Starting ||
                move_state == MoveState.Moving)
            {
                return -1;
            }

            if (move_state == MoveState.Faulted)
            {
                throw CreateMoveDriverException();
            }

            return cached_position;
        }
    }

    set
    {
        ...
    }
}
```

Do not copy this sample blindly; integrate with the generated ASCOM class properly.

The invariant is:

```text
public Position GET
    NEVER calls Wanderer
    NEVER waits for HardwareWorker
    NEVER sleeps
    NEVER waits on serial I/O
```

---

# 28. Public `Position` getter performance target

While a filter move is physically underway, clients such as NINA and SkyGuard may poll frequently.

A public proxy `Position` call should normally complete in a few milliseconds.

Acceptance threshold for integration tests:

```text
no Position GET should take more than 100ms
```

on the local machine while a normal move is underway.

The target is much better than 100ms; the threshold simply avoids flaky timing tests.

---

# 29. Public `Position` setter validation

Before sending anything to hardware:

1. Confirm the proxy is connected.
2. Obtain the number of valid slots from:

```text
cached_names.Length
```

3. Validate:

```text
0 <= value < cached_names.Length
```

Invalid positions must throw:

```text
ASCOM.InvalidValueException
```

as required by the FilterWheel interface.

---

# 30. Setter behavior when requested position equals current position

If:

```text
move_state == Idle
```

and:

```text
value == cached_position
```

then return successfully without commanding the Wanderer driver.

Do not rotate the wheel unnecessarily.

---

# 31. Setter behavior while an identical move is already running

If:

```text
move_state == Starting || Moving
```

and:

```text
value == target_position
```

treat the write as idempotent.

Return successfully.

Do not submit a duplicate Wanderer move command.

---

# 32. Setter behavior for conflicting move requests

If:

```text
target_position == 4
```

and the wheel is already moving there, and another client requests:

```text
Position = 1
```

do not queue a second physical move.

Do not abort the current move.

Do not silently replace the target.

Throw an:

```text
ASCOM.DriverException
```

with a clear message such as:

```text
Filter wheel is currently moving to position 4; a new move to position 1 cannot be started until the current movement completes.
```

This keeps the state machine deterministic.

---

# 33. Starting a physical move

Once validation has completed:

Under the state lock:

```text
target_position = requested_position
move_state = Starting
last_move_error = null
move_sequence++
```

Release the lock.

Submit one hardware-worker operation that performs:

```csharp
underlying_filter_wheel.Position = requested_position;
```

The public setter needs to know whether the vendor accepted the operation.

Therefore the worker operation should signal a:

```text
TaskCompletionSource
```

as soon as the vendor setter returns successfully.

The public ASCOM setter may synchronously wait for **this setter-accepted result only**.

It must not wait for physical movement completion.

The ASCOM standard specifies that writing `Position` returns once the filter change has successfully started.

Two additional rules:

## Atomic validation and transition

Validation (sections 29–32) and the `Idle -> Starting` transition must occur inside **one** `state_lock` region, so two concurrent setters can never both pass validation and both enqueue a move.

## Bounded acceptance wait

The wait for setter acceptance must be bounded:

```text
setter_accept_timeout_ms
default 30000
```

If the vendor setter has not returned within this time:

- throw `ASCOM.DriverException` to the calling client stating that the vendor driver did not acknowledge the move command in time;
- leave the worker request running; when the vendor setter eventually returns, the worker continues the normal lifecycle (`Moving` then `Idle` on success, `Faulted` on failure).

The observed vendor setter latency is roughly half a second, so this timeout should never fire in normal operation. It exists so a pathological vendor stall cannot freeze every proxy client indefinitely through the shared STA dispatch thread (section 22.1).

---

# 34. Hardware-worker move sequence

One worker command can own the complete move lifecycle.

Pseudo-sequence:

```text
worker receives StartMove(target)

call Wanderer.Position = target

if setter throws:
    record failure
    state -> Idle or Faulted as specified
    notify caller of setter failure
    stop

setter succeeded:
    state -> Moving
    signal public setter success

then:

loop:
    if elapsed since setter success > move_timeout_ms:
        state -> Faulted
        record timeout error
        stop

    actual = Wanderer.Position

    if actual == target:
        cached_position = target
        state -> Idle
        stop

    if actual == -1:
        remain Moving
        delay retry interval
        continue

    if actual is another valid slot:
        treat as stale
        remain Moving
        delay retry interval
        continue

    otherwise:
        error
```

The public setter therefore returns while this worker continues executing the blocking vendor `Position` reads.

---

# 35. Handling stale vendor positions

This is explicitly required because stale valid positions have been observed.

Example:

```text
target = 1

vendor results:
0
0
0
1
```

The proxy must expose to clients:

```text
-1
-1
-1
1
```

It must never expose:

```text
0
```

during this move.

A valid non-target Wanderer position after movement has started is treated as:

```text
stale / not yet confirmed
```

not as a new stationary proxy position.

---

# 36. Position retry delay

Default:

```text
250ms
```

After a blocking Wanderer `Position` call returns stale state:

```text
wait 250ms
```

before another vendor `Position` call.

Make this configurable internally/settings, but use:

```text
250ms
```

as the default.

Do not busy-spin.

---

# 37. Move completion timeout

Default logical move timeout:

```text
60000ms
```

Track elapsed wall-clock time from successful vendor setter completion.

If the target has not been confirmed within 60000ms:

```text
move_state = Faulted
last_move_error = timeout exception
```

Subsequent proxy `Position` calls should throw an informative:

```text
ASCOM.DriverException
```

until recovery.

Important limitation:

A single vendor COM `Position` call is synchronous and cannot safely be forcibly cancelled.

Therefore the 60000ms timeout can only be acted upon between returned vendor calls unless a separate watchdog merely changes public proxy state.

**Do not implement forced thread termination.**

**Do not use `Thread.Abort`.**

**Do not add a helper process in V1.**

---

# 38. V1 permanent-hang limitation

If the vendor `Position` call itself never returns, the worker thread may remain blocked indefinitely.

This is acceptable as a documented V1 limitation.

Do not complicate V1 with:

```text
child-process isolation
COM surrogate process management
process killing
AppDomain unloading
thread termination
```

Actual measurements currently show slow returns rather than permanent hangs.

A future V2 may isolate the vendor driver in a helper process if required.

---

# 38.1 Recovery from Faulted states

`Faulted` must not be a terminal trap. Define these recovery paths and test them.

## Move fault recovery

While `move_state == Faulted`:

- `Position` GET throws the recorded move error wrapped in an informative `ASCOM.DriverException` (section 27).
- `Position` SET is permitted as the normal client-driven recovery path: it validates normally and, if the worker is idle, transitions `Faulted -> Starting` and issues a fresh move.
- If the fault was a move timeout (section 37) and the worker is still blocked inside a vendor call, reject a new `Position` SET with `ASCOM.DriverException` stating that the previous vendor operation has not yet returned.

## Late completion after a timeout fault

If the watchdog declared a timeout fault while the worker was still blocked, and the vendor call later returns showing the wheel stationary at the original target:

```text
cached_position = target
move_state = Idle
last_move_error = null
```

Log this as a late recovery. The wheel is in a known good state; do not stay Faulted.

## Connection fault recovery

If a physical connection attempt fails or times out:

- every lease request waiting on that attempt fails with a descriptive `ASCOM.DriverException`;
- no leases are retained from the failed attempt;
- the worker disposes any partially created vendor object;
- `connection_state` returns to `Disconnected`;
- a subsequent connect attempt is permitted and starts fresh.

---

# 39. Connection architecture

The proxy is a hub, so multiple COM clients may request connections.

Each public FilterWheel instance must have a unique ID:

```csharp
Guid client_id;
```

and track whether it currently owns a connection lease:

```csharp
bool connection_lease_held;
```

The shared controller maintains:

```text
HashSet<Guid> connection_leases
```

---

# 40. Hub `Connected` semantics

ASCOM explicitly describes hub behavior: physical `Connected` becomes true when the first driver connects and remains true until all drivers have disconnected. A client releasing its connection does not imply that the hardware connection becomes false if another client is still using it.

Implement that behavior.

Example:

```text
NINA connects
leases = 1
vendor Connected = true

SkyGuard connects
leases = 2
vendor remains connected

NINA disconnects
leases = 1
vendor remains connected

SkyGuard disconnects
leases = 0
vendor Connected = false
```

---

# 41. Repeated connection calls must be idempotent per client

If a client does:

```text
Connected = true
Connected = true
Connected = true
```

that client must contribute only one lease.

Likewise:

```text
Connected = false
Connected = false
```

must not decrement the shared count twice.

Never allow a negative connection count.

Use a set of client IDs rather than only an integer if practical.

---

# 42. Legacy synchronous `Connected` property

For backward compatibility, implement:

```text
Connected get/set
```

The Platform 7 documentation requires new drivers to retain the synchronous `Connected` mechanic while also supporting asynchronous `Connect()` and `Disconnect()`.

For:

```text
Connected = true
```

the caller may block until the physical connection is established.

For:

```text
Connected = false
```

the caller may block until its connection lease has been released and, if it was the last lease, the physical disconnection is complete.

---

# 43. V3 asynchronous `Connect()` and `Disconnect()`

Because this driver reports:

```text
InterfaceVersion = 3
```

implement the newer:

```text
Connect()
Disconnect()
Connecting
```

mechanism.

`Connect()` must initiate connection and return quickly.

`Disconnect()` must initiate disconnect and return quickly.

`Connecting` must remain true while the operation is in progress. Platform 7 added this asynchronous connection model specifically to prevent clients hanging during long device initialization.

Do not simply call the blocking `Connected = true` implementation inside `Connect()` and wait.

---

# 43.1 Connection concurrency details

These rules make the connection tests of section 81 deterministic.

## Per-client semantics of public members

- `Connected` GET returns true only when **this client instance** holds a lease and the physical connection state is `Connected`.
- `Connecting` reflects only this client's own pending `Connect()` or `Disconnect()` operation.
- `CheckConnected` (used by hardware-dependent members such as `Position`) verifies that **this client instance** holds a lease. A client that never connected must receive `ASCOM.NotConnectedException` even while another client keeps the hardware connected.

## Fast path when the hardware is already connected

Acquiring a lease while `connection_state == Connected` is a pure in-memory operation:

- do not enqueue any hardware-worker request;
- do not wait behind an active move;
- `Connect()` in this case may complete effectively synchronously. `Connecting` may never be observed true; that is permitted, because completion is defined as `Connecting` returning false.

## Coalescing simultaneous first connects

Exactly one physical connect operation may ever be in flight. Represent it as a single shared completion task:

- the first lease request transitions `Disconnected -> Connecting` and enqueues **one** Connect request carrying a `TaskCompletionSource`;
- any other client connecting while `connection_state == Connecting` waits on (or, for async `Connect()`, observes) the same completion task;
- never enqueue a second physical Connect request.

## Physical connect timeout

```text
connect_timeout_ms
default 60000
```

This covers the whole physical connect sequence of section 44, including the initial stationary-position loop of section 45. On timeout, follow the connection fault recovery of section 38.1.

## Disconnect while a move is in progress

- A non-last client releasing its lease is a pure in-memory operation.
- The last client releasing its lease enqueues the physical disconnect, which naturally queues behind the active move request on the serialized worker (section 46).

---

# 44. Physical connection sequence

When the first connection lease is acquired:

Hardware worker:

```text
create ASCOM.DriverAccess.FilterWheel using Wanderer ProgID
        |
        v
underlying.Connected = true
        |
        v
read vendor identity/configuration
        |
        v
read Names
read FocusOffsets
read Position
        |
        v
cache everything
        |
        v
physical state = Connected
```

All calls occur sequentially on the hardware worker.

---

# 45. Initial `Position` read

The initial Wanderer `Position` read may itself take seconds.

That is acceptable during first physical connection.

It must not become the behavior of normal public proxy `Position` polling.

If the initial vendor `Position` value is:

```text
0..N-1
```

cache it.

If it returns:

```text
-1
```

the hardware appears to be moving during connection.

Continue serialized checking, waiting `position_retry_delay_ms` between vendor reads, until a stationary valid position is obtained or the physical connect times out (`connect_timeout_ms`, section 43.1).

Do not expose a connected Idle state without a known position.

---

# 46. Disconnect behavior

When the final connection lease is released:

```text
connection_state = Disconnecting
```

Queue physical disconnection to the hardware worker.

The worker must:

```text
wait for any active move worker operation to finish
or allow the serialized queue to naturally reach disconnect

underlying.Connected = false

dispose underlying ASCOM.DriverAccess.FilterWheel

clear/retain caches as defined below
```

Recommended:

Keep filter names in memory for diagnostics if desired, but mark all hardware state invalid while disconnected.

On the next connection, reread everything.

---

# 47. Client `Dispose()`

A client's `Dispose()` must never disconnect hardware used by another client.

If this driver instance owns a connection lease:

```text
release only this client's lease
```

Do not dispose the shared controller.

Do not dispose the shared vendor object unless this release leaves zero leases.

ASCOM explicitly cautions driver authors not to allow one client's disposal to adversely affect other connected clients.

---

# 48. Filter-name normalization

At physical connection time, read:

```csharp
string[] vendor_names = underlying_filter_wheel.Names;
```

Normalize each entry once and cache the result.

Use a narrow transformation matching the known Wanderer format.

Recommended regex:

```regex
^Filter\s+([1-9][0-9]*)\s+\((.*)\)$
```

If a name matches:

```text
Filter 1 (L)
```

use:

```text
L
```

If it matches:

```text
Filter 5 (Ha 3nm)
```

use:

```text
Ha 3nm
```

Do not require a one-character inner name.

---

# 49. Do not over-normalize filter names

If a returned string does **not** match the specific Wanderer decoration format, return it unchanged.

Example:

```text
Luminance
```

must remain:

```text
Luminance
```

Do not strip arbitrary parentheses.

Do not trim user naming conventions beyond ordinary leading/trailing whitespace unless proven necessary.

---

# 50. Names array behavior

The proxy `Names` property returns:

```text
a copy of cached_names
```

Never return the internal mutable array directly.

For the current Snowflake it should produce:

```text
L
R
G
B
H
S
O
D
```

This is expected to solve the SkyGuard enumeration problem without changing the vendor driver.

---

# 51. FocusOffsets

On connection, read:

```csharp
vendor_focus_offsets = underlying_filter_wheel.FocusOffsets;
```

Validate that its length equals:

```text
cached_names.Length
```

Cache a copy.

Proxy `FocusOffsets` returns a copy.

Do not invent focus offsets.

If the vendor returns valid values, preserve them exactly.

If the vendor legitimately supplies all zeros, preserve all zeros.

If the returned length is inconsistent with `Names`, treat it as a vendor-driver error and log it clearly.

A conservative fallback to a same-length zero array may be allowed only if ConformU behavior and ASCOM requirements support that decision; document the reason in code.

---

# 52. `DeviceState`

Because the proxy advertises FilterWheel interface version 3, implement `DeviceState`.

The FilterWheel V3 specification requires the operational state collection to include:

```text
Position
TimeStamp
```

when known.

The Position in `DeviceState` must use the same cached proxy semantics as the `Position` property:

```text
Idle:
Position = cached_position

Starting/Moving:
Position = -1
```

Do not call the Wanderer driver from `DeviceState`.

---

# 53. `Name`

The proxy's public `Name` property must be a compile-time/stored constant derived during initial project setup:

```text
vendor Name + " Proxy"
```

Do not query Wanderer every time `Name` is read.

The name must work while disconnected.

The `ServedClassName` shown in ASCOM Chooser should use the same human-readable name unless template limitations require slightly different wording.

---

# 54. `Description`

Use a concise description such as:

```text
Multi-client ASCOM proxy for Wanderer Snowflake filter wheel
```

Keep it under the ASCOM description-length constraint.

Do not claim to be an official Wanderer product.

Do not use Wanderer branding in a way that implies vendor authorship.

---

# 55. `DriverVersion`

Initial proxy version:

```text
0.1
```

ASCOM `DriverVersion` should contain only the public major/minor representation.

Assembly/file versioning may use:

```text
0.1.0.0
```

during development.

Do not use wildcard assembly versions for COM registration-sensitive assembly metadata; the ASCOM template specifically warns against wildcard assembly version numbers.

---

# 56. `DriverInfo`

Return useful proxy information, for example:

```text
SnowFlakeProxy 0.1; proxies ASCOM.WandererSnowflakeFilterWheel1.FilterWheel
```

If desired, append the cached vendor DriverVersion once connected.

Do not require a hardware connection for basic proxy identification.

---

# 57. `SupportedActions`

V1 should return:

```text
empty collection
```

Do **not** blindly forward Wanderer `SupportedActions`.

Custom actions could bypass the proxy state machine and mutate the wheel without the proxy knowing.

The proxy's job is correctness, not transparent pass-through of every vendor extension.

---

# 58. `Action`

Since V1 advertises no actions, `Action` should throw the appropriate ASCOM action-not-implemented exception according to the generated template and interface documentation.

Do not forward arbitrary vendor actions.

---

# 59. `CommandBlind`, `CommandBool`, `CommandString`

Do not forward these to the Wanderer driver.

They can bypass position-state tracking and create unsynchronized hardware traffic.

Implement the standard ASCOM:

```text
MethodNotImplementedException
```

behavior for these optional methods unless the current template/interface requires another precise exception.

---

# 60. Setup dialog

Implement a small proxy SetupDialog.

Do not create a permanent device-control window.

ASCOM explicitly recommends using `SetupDialog()` for driver-specific configuration rather than exposing a permanent control panel.

The SetupDialog should contain approximately:

```text
SnowFlakeProxy

Underlying driver:
ASCOM.WandererSnowflakeFilterWheel1.FilterWheel

Normalize Wanderer filter names:
[x]

Move timeout:
[60000] ms

Stale-position retry delay:
[250] ms

Trace logging:
[x]

[Open Wanderer Setup...]

[OK] [Cancel]
```

---

# 61. Underlying driver ProgID is not user-selectable in V1

This proxy is specifically for:

```text
ASCOM.WandererSnowflakeFilterWheel1.FilterWheel
```

Do not add an ASCOM chooser to let users wrap arbitrary filter-wheel drivers.

That would turn this into a generic hub and expand scope considerably.

Display the underlying ProgID read-only.

---

# 62. Vendor Setup button

`Open Wanderer Setup...` should call the vendor's:

```text
SetupDialog()
```

through the hardware worker.

Never instantiate/use the vendor COM driver directly on the WinForms UI thread.

If the physical wheel is currently connected through the proxy, the vendor Setup button should be disabled or show:

```text
Disconnect all clients before opening the Wanderer setup dialog.
```

V1 should not attempt to open vendor Setup while active clients are moving or polling the proxy.

---

# 63. Settings storage

Use ASCOM Profile facilities generated/recommended by the template.

Do not create:

```text
custom registry locations
JSON settings files
XML files in Program Files
```

for simple driver settings unless absolutely necessary.

Store at least:

```text
trace_enabled
normalize_filter_names
move_timeout_ms
position_retry_delay_ms
connect_timeout_ms
setter_accept_timeout_ms
```

Defaults:

```text
trace_enabled = true during development
normalize_filter_names = true
move_timeout_ms = 60000
position_retry_delay_ms = 250
connect_timeout_ms = 60000
setter_accept_timeout_ms = 30000
```

`connect_timeout_ms` and `setter_accept_timeout_ms` do not need SetupDialog fields in V1; profile storage is sufficient.

---

# 64. Logging

Use ASCOM `TraceLogger` infrastructure.

The ASCOM LocalServer template already creates diagnostic logging under the standard:

```text
Documents\ASCOM\Logs yyyy-mm-dd
```

location.

Each log entry relating to a client should contain a client identifier where useful.

Each move should contain a move sequence ID.

Example:

```text
22:13:08.002 client=2 move=17 Position SET requested=4
22:13:08.003 move=17 state Idle -> Starting
22:13:08.004 move=17 vendor Position SET begin target=4
22:13:08.527 move=17 vendor Position SET completed duration_ms=523
22:13:08.528 move=17 state Starting -> Moving
22:13:08.529 move=17 vendor Position GET begin
22:13:16.941 move=17 vendor Position GET end result=4 duration_ms=8412
22:13:16.942 move=17 cached_position=4
22:13:16.943 move=17 state Moving -> Idle
```

---

# 65. Avoid log flooding from public `Position` polling

SkyGuard and NINA can poll rapidly.

Do not log every cached `Position` call at normal trace level.

Instead maintain counters.

For example, after a move:

```text
move=17 public_position_polls=126
move=17 moving_position_returns=121
move=17 max_public_getter_ms=2
```

A verbose/debug option may log individual calls when diagnosing a problem.

---

# 66. Exception handling

Never allow raw vendor COM exceptions to crash the LocalServer.

At the hardware boundary:

1. Log the complete exception including stack trace.
2. Preserve a valid ASCOM exception when appropriate.
3. Otherwise convert unexpected failures to:

```text
ASCOM.DriverException
```

with useful contextual text.

Example:

```text
Wanderer Position getter failed while monitoring move to position 4: <vendor message>
```

Do not return generic:

```text
Error
```

without context.

---

# 67. Underlying connection strategy

Although the vendor reports InterfaceVersion 3, use its known synchronous:

```text
Connected
```

property for physical connection unless a compelling test proves its `Connect()` implementation is better.

The proxy itself must expose correct V3 asynchronous connection semantics.

Do not unnecessarily depend on additional Wanderer async behavior when the proxy can provide that abstraction itself.

---

# 68. No idle Wanderer polling

When:

```text
move_state == Idle
```

do not continuously query Wanderer `Position`.

Public clients should receive:

```text
cached_position
```

This avoids the broken blocking vendor getter entirely during normal stationary operation.

The proxy knows about every supported movement because all clients are required to use the proxy.

---

# 69. Important operating rule

When using SnowFlakeProxy:

```text
NINA -> Proxy
SkyGuard -> Proxy
other astronomy applications -> Proxy
```

Nothing else should connect directly to:

```text
ASCOM.WandererSnowflakeFilterWheel1.FilterWheel
```

while the proxy owns it.

Direct vendor access bypasses serialization and invalidates proxy state.

Document this clearly in README.

---

# 70. External/manual movement is out of scope for V1

V1 does not have to detect a wheel movement initiated through:

```text
another application connected directly to Wanderer
vendor control software bypassing the proxy
```

That usage is unsupported.

Do not add constant idle polling merely to support unsupported bypass access.

---

# 71. Hardware command queue

Use one worker queue.

Recommended conceptual type:

```text
BlockingCollection<HardwareRequest>
```

Requests should support:

```text
Connect
Disconnect
StartMove
OpenVendorSetup
Shutdown
```

Do not create a public general-purpose:

```text
InvokeAnything(Func<dynamic,...>)
```

API that makes it easy for future code to bypass state rules.

Keep vendor operations explicit.

---

# 72. `StartMove` worker request details

The `StartMove` request should contain:

```text
requested target position
move sequence ID
TaskCompletionSource indicating vendor setter success/failure
```

Worker behavior:

```text
call vendor Position setter
signal setter result
monitor target until complete
update shared controller state
```

If possible, the same hardware-worker request should retain ownership through the entire movement.

This ensures no other vendor command can interleave with the movement-monitoring serial traffic.

---

# 73. Same-target move while worker is busy

Public proxy logic handles this from cached state.

It should never enqueue another request.

Example:

```text
move target = 4

NINA: Position=4
SkyGuard: Position=4
```

Both see success.

Vendor receives exactly:

```text
one Position=4 command
```

---

# 74. Different-target move while worker is busy

Reject before reaching the worker.

Example:

```text
target = 4

new request = 2
```

Return DriverException.

Do not queue.

This prevents an apparently asynchronous ASCOM setter from sitting in a work queue for 10 seconds before it can even start.

---

# 75. Unit-test architecture

Add a separate test project:

```text
SnowFlakeProxy.Tests
```

Prefer MSTest unless the generated ASCOM solution already uses another testing framework.

Avoid unnecessary third-party mocking packages.

Create a hand-written:

```text
FakeUnderlyingFilterWheel
```

implementing:

```text
IUnderlyingFilterWheel
```

The core controller and worker must be testable against this fake without real COM hardware.

The test project must target the same .NET Framework version as the generated driver project.

Grant the test assembly access to internal types by adding to the driver project:

```csharp
[assembly: InternalsVisibleTo("SnowFlakeProxy.Tests")]
```

`SnowflakeProxyController`, `HardwareWorker`, and `IUnderlyingFilterWheel` remain `internal`; do not make them public merely for testing.

---

# 76. Fake driver capabilities

The fake must be configurable to emulate:

```text
normal immediate Position getter
blocking Position getter
stale position responses
vendor setter latency
vendor getter latency
exceptions
connection failures
connection latency
move completion delays
names arrays
focus offset arrays
```

Expose instrumentation:

```text
concurrent_vendor_call_count
maximum_concurrent_vendor_call_count
position_get_call_count
position_set_call_count
connect_call_count
disconnect_call_count
```

---

# 77. Fake blocking behavior

Support a fake scenario such as:

```text
initial position = 0
target = 1
setter latency = 500ms

Position getter:
    block 8000ms
    return 1
```

Proxy client must experience:

```text
Position=-1 immediately throughout the 8000ms
```

not the 8000ms block.

---

# 78. Fake stale-position scenario

Support:

```text
initial = 0
target = 1

vendor Position responses:
0
0
0
1
```

Expected proxy responses while moving:

```text
-1
-1
-1
1
```

No client may ever see stale:

```text
0
```

after movement has started.

---

# 79. Required Position unit tests

Implement at minimum:

```text
Position_WhenIdle_ReturnsCachedPosition

Position_WhenStarting_ReturnsMinusOne

Position_WhenMoving_ReturnsMinusOne

Position_WhenMoveCompletes_ReturnsTarget

Position_DoesNotCallUnderlyingGetter

Position_DoesNotBlockOnUnderlyingMove

Position_InvalidTarget_ThrowsInvalidValueException

Position_WhenDisconnected_ThrowsNotConnectedException

Position_SameTargetDuringMove_DoesNotIssueSecondMove

Position_DifferentTargetDuringMove_ThrowsDriverException

Position_StaleVendorPositionNeverLeaksToClient

Position_VendorSetterFailureDoesNotLeaveMovingForever

Position_MonitorFailureTransitionsToFault

Position_SetAfterMoveFault_RecoversAndStartsNewMove

Position_LateVendorCompletionAfterTimeoutFault_RestoresIdle
```

---

# 80. Required concurrency tests

Create tests with at least:

```text
20 simultaneous client threads
```

repeatedly reading proxy `Position`.

During a fake 8000ms underlying block:

```text
every proxy Position call returns quickly
every proxy Position call returns -1
```

Assert:

```text
maximum_concurrent_vendor_call_count == 1
```

This assertion is mandatory.

---

# 81. Required connection tests

Implement:

```text
FirstClientConnect_ConnectsVendorOnce

SecondClientConnect_DoesNotReconnectVendor

RepeatedConnectBySameClient_IsIdempotent

OneClientDisconnect_LeavesVendorConnectedWhenAnotherLeaseExists

LastClientDisconnect_DisconnectsVendor

RepeatedDisconnect_IsIdempotent

DisposeOfOneClient_DoesNotDisconnectOtherClients

SimultaneousFirstConnects_CoalesceToOneVendorConnection

FailedPhysicalConnect_FailsAllWaitingLeases_AndAllowsRetry

ClientWithoutLease_ThrowsNotConnected_WhileAnotherClientIsConnected
```

---

# 82. Required filter-name tests

Test:

```text
Filter 1 (L) -> L
Filter 2 (R) -> R
Filter 8 (D) -> D
Filter 5 (Ha 3nm) -> Ha 3nm
```

Also verify untouched strings:

```text
Luminance -> Luminance
Ha (3nm) -> Ha (3nm)
Custom Filter -> Custom Filter
```

Return array copies and test mutation isolation.

---

# 83. Array defensive-copy tests

Example:

```text
names_a = proxy.Names

names_a[0] = "CORRUPTED"

names_b = proxy.Names
```

Expected:

```text
names_b[0] still equals original cached name
```

Repeat equivalent test for:

```text
FocusOffsets
```

---

# 84. Public getter latency tests

Against a fake vendor blocked for:

```text
10000ms
```

measure 100 or more public proxy `Position` calls.

Acceptance:

```text
none > 100ms
```

Ideally:

```text
typical < 10ms
```

Do not hard-fail tests at 10ms because CI/Windows scheduling can fluctuate.

---

# 85. Worker queue deadlock tests

Create scenarios involving:

```text
connection
move
disconnect
multiple public readers
```

and verify:

```text
no deadlocks
worker stays alive
public cached getters stay responsive
```

Never hold `state_lock` while synchronously waiting for a worker completion task.

Include a code comment near every synchronous worker wait reminding future maintainers of this invariant.

---

# 86. Integration PowerShell tools

Create all scripts in PowerShell 5.1 syntax.

## `Query-Wanderer.ps1`

Reports:

```text
Name
Description
DriverVersion
InterfaceVersion
DriverInfo
Names
FocusOffsets
Position
```

---

## `Register-Driver.ps1`

Find the built proxy EXE and invoke:

```powershell
& $exe_path "/regserver"
```

Check `$LASTEXITCODE` where meaningful.

Require elevation and print a useful error if not elevated.

Do not use REGASM.

---

## `Unregister-Driver.ps1`

Invoke:

```powershell
& $exe_path "/unregserver"
```

---

# 87. Single-client integration test script

Create:

```text
Test-ProxySingleClient.ps1
```

It should instantiate:

```text
ASCOM.SnowFlakeProxy.FilterWheel
```

and:

1. connect;
2. read current position;
3. choose a different target;
4. set target;
5. poll every 100ms;
6. timestamp every result;
7. measure getter latency;
8. stop when target is reached.

Expected output pattern:

```text
Position = -1   getter=2ms
Position = -1   getter=1ms
Position = -1   getter=2ms
...
Position = 1    getter=1ms
```

It must not show:

```text
Position = old_slot
```

during movement.

It must not show:

```text
getter=7000ms
```

---

# 88. Multi-client integration test script

Create:

```text
Test-ProxyMultiClient.ps1
```

Use two separate PowerShell 5.1 processes, as in the earlier diagnostic test.

Client A:

```text
polls Position every 100ms
```

Client B:

```text
commands a move
then polls Position
```

Expected:

```text
both clients:
-1 while moving
target when complete
```

All proxy getter calls:

```text
<100ms
```

One carve-out: getter calls that arrive during the brief window in which Client B's setter is still waiting for vendor acceptance (typically under one second) are dispatched on the same LocalServer STA thread (section 22.1) and may occasionally exceed 100ms.

The script should timestamp and report such samples separately rather than failing on them.

Every getter issued after the setter has returned must satisfy the strict <100ms requirement.

The physical move must be issued exactly once.

---

# 89. NINA integration test

With the vendor-original driver installed and Proxy registered:

Configure NINA to use:

```text
<vendor Name> Proxy
```

not the direct Wanderer driver.

Test:

```text
L -> R
R -> G
G -> B
B -> H
H -> S
S -> O
O -> D
D -> L
```

Expected:

- no NINA “Failed to move filter wheel” messages;
- NINA sees `-1` during movement where applicable;
- final NINA position becomes target;
- repeated moves remain stable.

---

# 90. SkyGuard integration test

Configure SkyGuard to use:

```text
<vendor Name> Proxy
```

Verify the filter list appears as:

```text
L
R
G
B
H
S
O
D
```

Verify SkyGuard can identify filter changes.

Verify that double-clicking directly on a filter name still changes the intended filter.

The proxy does not need to compensate for SkyGuard's row-hit-testing UI quirk.

---

# 91. NINA + SkyGuard simultaneous test

Both applications connect to:

```text
<vendor Name> Proxy
```

No JustAHub.

Use NINA to command:

```text
L -> H
```

Expected:

```text
NINA Position while moving = -1
SkyGuard Position while moving = -1
```

After physical completion:

```text
NINA = H
SkyGuard = H
```

No application should see stale L after the move has started.

---

# 92. Authority for filter changes during normal imaging

Operational recommendation:

```text
NINA controls filter changes.
SkyGuard observes them.
```

The proxy supports either client issuing a move, but sequence-control software should remain the normal authority.

This is an operational recommendation, not a driver-enforced restriction.

---

# 93. ConformU testing

Install and use the current official Conform Universal tool.

ConformU is ASCOM's current conformance checker and supersedes the older Windows Conform utility. The current release line supports Windows COM drivers and Platform 7 interfaces.

Obtain the current Windows release from the ASCOM Initiative GitHub releases for the `ConformU` project.

Run the complete FilterWheel conformance test against:

```text
ASCOM.SnowFlakeProxy.FilterWheel
```

The proxy must be tested against the physical Snowflake where required.

Save the full report under:

```text
docs\ConformU\
```

---

# 94. ConformU acceptance goal

The proxy must pass the FilterWheel checks relevant to:

```text
InterfaceVersion=3
Names
FocusOffsets
Position
Connected
Connect
Disconnect
Connecting
DeviceState
invalid positions
performance
```

In particular, ConformU should observe:

```text
Position setter initiates movement

Position GET -> -1
Position GET -> -1
...
Position GET -> final slot
```

rather than the Wanderer driver's blocking behavior.

---

# 95. Run ConformU before NINA/SkyGuard final testing

Required order:

```text
unit tests
    ->
single-client proxy integration
    ->
multi-client proxy integration
    ->
ConformU
    ->
NINA
    ->
SkyGuard
    ->
NINA + SkyGuard
```

Do not use NINA as the first debugging environment for basic driver correctness.

---

# 96. Release registration behavior

The LocalServer itself is responsible for COM and ASCOM registration using the generated template infrastructure.

Use:

```text
/regserver
/unregserver
```

ASCOM's LocalServer registration also registers the served class so it appears in the ASCOM Chooser.

Do not manually write COM registration keys during development unless diagnosing the generated template.

---

# 97. Do not build an installer initially

V1 development deliverables do not require a polished installer.

Development deployment may use:

```text
build
/regserver
```

After functional stability and ConformU compliance, an installer can be added as a separate phase.

Do not let packaging work delay the driver architecture.

---

# 98. No changes to the installed Wanderer binary

For production proxy development, restore and use the untouched vendor driver.

Do not rely on the previously modified name-patched binary.

SnowFlakeProxy itself must provide the simple filter names.

This is essential so the integration test demonstrates that the proxy fixes the behavior without altering vendor software.

---

# 99. Baseline documentation file

Create:

```text
docs\BaselineMeasurements.md
```

Record the known original-driver measurements:

```text
Direct vendor:
0 -> 1
Position GET blocked 8445ms
returned target

Single client through hub:
Position GET blocked approximately 7020ms

Two-client case:
first getters blocked approximately 9000ms
returned stale old slot

new slot became visible approximately 18s after command
```

Also record:

```text
vendor Names:
Filter 1 (L)
...
Filter 8 (D)

normalized Names:
L
R
G
B
H
S
O
D
```

This gives future maintainers a reason for every unusual piece of proxy logic.

---

# 100. Architecture documentation

Create:

```text
docs\Architecture.md
```

Include this diagram:

```text
                         clients
             +-------------+-------------+
             |             |             |
            NINA        SkyGuard       other
             |             |             |
             +-------------+-------------+
                           |
                           v
                  SnowFlakeProxy
                 public cached state
                           |
                           v
                  shared controller
                           |
                           v
                single hardware worker
                           |
                           v
        Wanderer ASCOM FilterWheel LocalServer
                           |
                           v
                       hardware
```

And emphasize:

```text
PUBLIC Position DOES NOT TOUCH HARDWARE.
```

That sentence should be prominent.

---

# 101. Code comments that must exist

Add comments at the critical places explaining **why** the design is unusual.

Above the public Position getter:

```text
IMPORTANT:
Never query the underlying Wanderer Position property here.
The vendor Position getter is synchronous and can block for several seconds.
ASCOM requires this proxy to return -1 immediately while movement is active.
```

Above the worker's vendor Position read:

```text
This is the only code path permitted to read the vendor Position property.
The call may block for many seconds.
```

Above the underlying adapter instance:

```text
The underlying ASCOM driver is thread-affine to the hardware worker.
Do not access this object from any other thread.
```

---

# 102. Things the coding agent must NOT do

Do not:

```text
modify JustAHub
fork JustAHub
use JustAHub inside the proxy
patch Wanderer's EXE
read Snowflake COM27 directly
reverse-engineer the serial protocol
create one Wanderer object per client
call Wanderer Position from proxy Position
poll Wanderer continuously while idle
use Task.Run for each Position query
allow concurrent underlying COM calls
optimistically set cached_position to target
return the previous valid slot while moving
queue conflicting movement requests
change the LocalServer Platform Target from the template's x86
use REGASM
migrate the template to another framework merely for modernization
add WPF
add dependency injection frameworks
add reactive frameworks
add a database
add network services
add telemetry
add automatic update checking
add unrelated UI
```

Keep V1 narrow.

---

# 103. Milestone 0 — Environment and baseline

Deliverables:

```text
C:\dev\Self\NINA\SnowFlakeProxy created
ASCOM Platform version verified to be 7 or later
ASCOM Platform 7 VS2022 template extension verified
vendor Name queried
vendor properties recorded
ConformU installed
baseline measurements documented
```

Stop if the vendor ProgID cannot be instantiated.

Do not code around a broken environment.

---

# 104. Milestone 1 — Empty LocalServer

Deliverables:

```text
official ASCOM LocalServer FilterWheel project generated
solution builds
proxy has unique CLSID
proxy has unique ProgID
/regserver succeeds
proxy appears in ASCOM Chooser
/unregserver succeeds
```

No hardware implementation yet.

Commit/checkpoint before continuing.

---

# 105. Milestone 2 — Underlying adapter

Implement:

```text
IUnderlyingFilterWheel
WandererFilterWheelAdapter
```

Create a temporary controlled test that connects through the adapter and reads:

```text
Name
Names
FocusOffsets
Position
```

All adapter calls must occur on the worker thread even at this stage.

---

# 106. Milestone 3 — Hardware worker

Implement the serialized worker.

Acceptance:

```text
vendor adapter created on worker
vendor adapter used only on worker
vendor adapter disposed on worker
maximum vendor concurrency == 1
```

No public movement logic yet.

---

# 107. Milestone 4 — Shared connection controller

Implement:

```text
connection leases
Connected
Connect()
Disconnect()
Connecting
first/last physical connection
```

Complete unit tests for connection behavior before implementing movement.

---

# 108. Milestone 5 — Cached static properties

Implement and test:

```text
Name
Description
DriverInfo
DriverVersion
InterfaceVersion
Names
FocusOffsets
SupportedActions
```

At this point SkyGuard name normalization should already be testable.

---

# 109. Milestone 6 — Position state machine

Implement:

```text
cached_position
target_position
Idle
Starting
Moving
Faulted
Position getter
Position setter
worker monitoring
stale result suppression
timeout handling
```

Complete all Position unit tests.

This is the most important milestone.

---

# 110. Milestone 7 — DeviceState and remaining V3 contract

Implement:

```text
DeviceState
Connect
Disconnect
Connecting
standard exceptions
SetupDialog
```

Run unit tests.

---

# 111. Milestone 8 — PowerShell hardware tests

Run:

```text
Test-ProxySingleClient.ps1
```

Then:

```text
Test-ProxyMultiClient.ps1
```

Do not proceed until the proxy eliminates the multi-second public getter behavior.

---

# 112. Milestone 9 — ConformU

Run full ConformU.

Fix proxy conformance issues rather than suppressing them.

Do not modify ConformU.

Do not argue around failures unless the published standard clearly permits the behavior.

ASCOM considers its master interface documentation definitive and recommends ConformU for driver validation.

One anticipated contingency: if ConformU is found to write a new `Position` while the previous move is still in progress and to treat the rejection of section 32 as a failure, relax the conflicting-move rule only as far as required by the ConformU log evidence, and record the decision in `docs\Architecture.md`. Do not relax it preemptively.

---

# 113. Milestone 10 — Application integration

Test:

```text
NINA only
SkyGuard only
NINA + SkyGuard
```

Use the **unmodified** Wanderer vendor driver behind the proxy.

---

# 114. Definition of Done

V1 is complete only when all of the following are true:

```text
[ ] Official ASCOM LocalServer architecture used.

[ ] Proxy appears in ASCOM Chooser with:
    <exact Wanderer Name> Proxy

[ ] Proxy ProgID is:
    ASCOM.SnowFlakeProxy.FilterWheel

[ ] Proxy reports InterfaceVersion 3.

[ ] Underlying vendor ProgID remains:
    ASCOM.WandererSnowflakeFilterWheel1.FilterWheel

[ ] No runtime dependency on JustAHub.

[ ] No patch to Wanderer executable required.

[ ] Only one Wanderer ASCOM object exists.

[ ] Only one hardware worker accesses it.

[ ] Maximum concurrent vendor ASCOM call count is 1.

[ ] Public Position getter never accesses vendor hardware.

[ ] Public Position returns -1 throughout physical movement.

[ ] Public Position getter remains below 100ms during a move.

[ ] Stale vendor positions never leak to clients.

[ ] Names return:
    L R G B H S O D
    for the current configuration.

[ ] SkyGuard filter list populates correctly.

[ ] NINA filter changes no longer produce the observed failure.

[ ] NINA and SkyGuard can be connected simultaneously.

[ ] One client disconnecting does not disconnect another.

[ ] Connect/Disconnect/Connecting implemented for V3.

[ ] DeviceState reports Position and TimeStamp.

[ ] Faulted move and connection states have tested recovery paths.

[ ] All public synchronous waits are bounded by configured timeouts.

[ ] Unit tests pass.

[ ] Multi-client stress tests pass.

[ ] ConformU report is acceptable.

[ ] Build and registration scripts are PowerShell 5.1 compatible.

[ ] Release build has no known architecture/concurrency warnings.
```

---

# 115. Final architectural summary

The defective path today is effectively:

```text
NINA --------\
              \
               hub -> Wanderer Position GET -> blocks for seconds
              /
SkyGuard ----/
```

SnowFlakeProxy must change the abstraction to:

```text
NINA --------\
              \
               SnowFlakeProxy -> cached state
              /       |
SkyGuard ----/        |
                      |
                      v
             one serialized worker
                      |
                      v
           blocking Wanderer driver
```

During an 8000ms Wanderer `Position` call:

```text
NINA:
Position -> -1 immediately

SkyGuard:
Position -> -1 immediately

NINA:
Position -> -1 immediately

SkyGuard:
Position -> -1 immediately
```

When the worker eventually confirms the destination:

```text
cached_position = target
move_state = Idle
```

and all clients then see:

```text
Position -> target
```

The Wanderer driver's blocking behavior remains confined to one private worker thread and is never exposed as ASCOM behavior to clients.

That is the entire purpose of this driver.

**Do not weaken this invariant during implementation.**