/*
 * Windows Service interop functions courtesy of the brilliant Martin Andreas Ullrich 
 * https://github.com/dasMulli/dotnet-win32-service - MIT license
 * The original code has been reduced for brevity and bare basic functionality.
 */

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CM.Daemon {


    public interface IWin32Service {
        string ServiceName { get; }
        void Start(string[] startupArguments, Action serviceStoppedCallback);
        void Stop();
    }

    internal class Win32Service {

        private readonly string serviceName;

        private readonly IWin32ServiceStateMachine stateMachine;

        private readonly TaskCompletionSource<int> stopTaskCompletionSource = new TaskCompletionSource<int>();

        private uint checkpointCounter = 1;

        private ServiceControlHandler handleServiceControlCommand;

        private ServiceStatus serviceStatus;

        private Handle serviceStatusHandle;


        internal Win32Service(IWin32Service service) {
            if (service == null) {
                throw new ArgumentNullException(nameof(service));
            }
            serviceStatus = new ServiceStatus() {
                serviceType = ServiceType.Win32OwnProcess,
                state = ServiceState.StartPending,
                acceptedControlCommands = ServiceAcceptedControlCommandsFlags.Stop
            };
            serviceName = service.ServiceName;
            stateMachine = new SimpleServiceStateMachine(service);
            handleServiceControlCommand = new ServiceControlHandler(HandleServiceControlCommand);
        }

        private delegate void ServiceControlHandler(ServiceControlCommand control, uint eventType, IntPtr eventData, IntPtr eventContext);

        private delegate void ServiceStatusReportCallback(ServiceState state, ServiceAcceptedControlCommandsFlags acceptedControlCommands, int win32ExitCode, uint waitHint);

        private delegate int ServiceMainFunctionDel(int numArs, IntPtr argPtrPtr);

        private interface IWin32ServiceStateMachine {
            void OnCommand(ServiceControlCommand command, uint commandSpecificEventType);
            void OnStart(string[] startupArguments, ServiceStatusReportCallback statusReportCallback);
        }

        public int Run() {
            return RunAsync().Result;
        }

        public Task<int> RunAsync() {
            var serviceTable = new ServiceTableEntry[2]; // second one is null/null to indicate termination
            serviceTable[0].serviceName = serviceName;
            serviceTable[0].serviceMainFunction = Marshal.GetFunctionPointerForDelegate<ServiceMainFunctionDel>(ServiceMainFunction);

            try {
                // StartServiceCtrlDispatcherW call returns when ServiceMainFunction exits
                if (!StartServiceCtrlDispatcherW(serviceTable)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            } catch (DllNotFoundException dllException) {
                throw new PlatformNotSupportedException(nameof(Win32Service) + " is only supported on Windows with service management API set.",
                    dllException);
            }
            return stopTaskCompletionSource.Task;
        }


        #region Install/Uninstall static methods
        public static bool TryDeleteService(string serviceName, out string error) {
            error = null;
            try {
                using (var mgr = ServiceControlManager.Connect(null, null, ServiceControlManagerAccessRights.All)) {
                    using (var svc = mgr.OpenService(serviceName, ServiceControlAccessRights.All)) {
                        svc.Delete();
                    }
                }
                return true;
            } catch (Exception ex) {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryInstallService(string name, string displayName, string description, string command, string user, string pass, out string error) {
            error = null;
            try {
                using (var mgr = ServiceControlManager.Connect(null, null, ServiceControlManagerAccessRights.All)) {
                    using (var service = mgr.CreateService(name, displayName, command, ServiceType.Win32OwnProcess,
                            ServiceStartType.AutoStart, ErrorSeverity.Normal, user, pass)) {
                        if (!String.IsNullOrEmpty(description))
                            service.SetDescription(description);
                        service.Start();
                    }
                }
                return true;
            } catch (Exception ex) {
                error = ex.Message;
                return false;
            }
        }
        #endregion

        #region Interop

        private const string DllServiceCore_L1_1_0 = "api-ms-win-service-core-l1-1-0.dll";

        private const string DllServiceManagement_L1_1_0 = "api-ms-win-service-management-l1-1-0.dll";

        private const string DllServiceManagement_L2_1_0 = "api-ms-win-service-management-l2-1-0.dll";

        [DllImport(DllServiceManagement_L2_1_0, ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool ChangeServiceConfig2W(ServiceHandle service, ServiceConfigInfoTypeLevel infoTypeLevel, IntPtr info);

        [DllImport(DllServiceManagement_L1_1_0, ExactSpelling = true, SetLastError = true)]
        private static extern bool CloseServiceHandle(IntPtr handle);

        [DllImport(DllServiceManagement_L1_1_0, ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ServiceHandle CreateServiceW(
            ServiceControlManager serviceControlManager,
            string serviceName,
            string displayName,
            ServiceControlAccessRights desiredControlAccess,
            ServiceType serviceType,
            ServiceStartType startType,
            ErrorSeverity errorSeverity,
            string binaryPath,
            string loadOrderGroup,
            IntPtr outUIntTagId,
            string dependencies,
            string serviceUserName,
            string servicePassword);

        [DllImport(DllServiceManagement_L1_1_0, ExactSpelling = true, SetLastError = true)]
        private static extern bool DeleteService(ServiceHandle service);

        [DllImport(DllServiceManagement_L1_1_0, ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ServiceControlManager OpenSCManagerW(string machineName, string databaseName, ServiceControlManagerAccessRights dwAccess);

        [DllImport(DllServiceManagement_L1_1_0, ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern ServiceHandle OpenServiceW(ServiceControlManager serviceControlManager, string serviceName,
            ServiceControlAccessRights desiredControlAccess);

        private static string[] ParseArguments(int numArgs, IntPtr argPtrPtr) {
            if (numArgs <= 0) {
                return Array.Empty<string>();
            }
            // skip first parameter because it is the name of the service
            var args = new string[numArgs - 1];
            for (var i = 0; i < numArgs - 1; i++) {
                argPtrPtr = IntPtr.Add(argPtrPtr, IntPtr.Size);
                var argPtr = Marshal.PtrToStructure<IntPtr>(argPtrPtr);
                args[i] = Marshal.PtrToStringUni(argPtr);
            }
            return args;
        }

        [DllImport(DllServiceCore_L1_1_0, ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern Handle RegisterServiceCtrlHandlerExW(string serviceName, ServiceControlHandler serviceControlHandler, IntPtr context);

        [DllImport(DllServiceCore_L1_1_0, ExactSpelling = true, SetLastError = true)]
        private static extern bool SetServiceStatus(Handle statusHandle, ref ServiceStatus pServiceStatus);

        [DllImport(DllServiceCore_L1_1_0, ExactSpelling = true, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool StartServiceCtrlDispatcherW([MarshalAs(UnmanagedType.LPArray)] ServiceTableEntry[] serviceTable);

        [DllImport(DllServiceManagement_L1_1_0, ExactSpelling = true, SetLastError = true)]
        private static extern bool StartServiceW(ServiceHandle service, uint argc, IntPtr wargv);

        private void HandleServiceControlCommand(ServiceControlCommand command, uint eventType, IntPtr eventData, IntPtr eventContext) {
            try {
                stateMachine.OnCommand(command, eventType);
            } catch {
                ReportServiceStatus(ServiceState.Stopped, ServiceAcceptedControlCommandsFlags.None, win32ExitCode: -1, waitHint: 0);
            }
        }

        private void ReportServiceStatus(ServiceState state, ServiceAcceptedControlCommandsFlags acceptedControlCommands, int win32ExitCode, uint waitHint) {
            if (serviceStatus.state == ServiceState.Stopped) {
                // we refuse to leave or alter the final state
                return;
            }

            serviceStatus.state = state;
            serviceStatus.win32ExitCode = win32ExitCode;
            serviceStatus.waitHint = waitHint;

            serviceStatus.acceptedControlCommands = state == ServiceState.Stopped
                ? ServiceAcceptedControlCommandsFlags.None // since we enforce "Stopped" as final state, no longer accept control messages
                : acceptedControlCommands;

            serviceStatus.checkPoint = state == ServiceState.Running || state == ServiceState.Stopped || state == ServiceState.Paused
                ? 0 // MSDN: This value is not valid and should be zero when the service does not have a start, stop, pause, or continue operation pending.
                : checkpointCounter++;

            SetServiceStatus(serviceStatusHandle, ref serviceStatus);

            if (state == ServiceState.Stopped) {
                stopTaskCompletionSource.TrySetResult(win32ExitCode);
            }
        }

        private int ServiceMainFunction(int numArgs, IntPtr argPtrPtr) {
            var startupArguments = ParseArguments(numArgs, argPtrPtr);

            serviceStatusHandle = RegisterServiceCtrlHandlerExW(serviceName, handleServiceControlCommand, IntPtr.Zero);

            if (serviceStatusHandle.IsInvalid) {
                stopTaskCompletionSource.SetException(new Win32Exception(Marshal.GetLastWin32Error()));
                return 0;
            }

            ReportServiceStatus(ServiceState.StartPending, ServiceAcceptedControlCommandsFlags.None, win32ExitCode: 0, waitHint: 3000);

            try {
                stateMachine.OnStart(startupArguments, ReportServiceStatus);
            } catch {
                ReportServiceStatus(ServiceState.Stopped, ServiceAcceptedControlCommandsFlags.None, win32ExitCode: -1, waitHint: 0);
            }

            return 0;
        }

        private class Handle : SafeHandle {
            internal Handle() : base(IntPtr.Zero, ownsHandle: true) {
            }

            public override bool IsInvalid {
                [System.Security.SecurityCritical]
                get {
                    return handle == IntPtr.Zero;
                }
            }

            protected override bool ReleaseHandle() {
                return CloseServiceHandle(handle);
            }
        }

        private class ServiceControlManager : Handle {

            internal ServiceControlManager() {
            }

            public ServiceHandle CreateService(string serviceName, string displayName, string binaryPath, ServiceType serviceType, ServiceStartType startupType, ErrorSeverity errorSeverity,
                string user, string pass) {
                var service = CreateServiceW(this, serviceName, displayName, ServiceControlAccessRights.All, serviceType, startupType, errorSeverity,
                    binaryPath, null,
                    IntPtr.Zero, null, user, pass);
                if (service.IsInvalid) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return service;
            }

            public ServiceHandle OpenService(string serviceName, ServiceControlAccessRights desiredControlAccess) {
                var service = OpenServiceW(this, serviceName, desiredControlAccess);
                if (service.IsInvalid) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
                return service;
            }

            internal static ServiceControlManager Connect(string machineName, string databaseName, ServiceControlManagerAccessRights desiredAccessRights) {
                var mgr = OpenSCManagerW(machineName, databaseName, desiredAccessRights);
                if (mgr.IsInvalid) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }

                return mgr;
            }

        }

        private class ServiceHandle : Handle {

            internal ServiceHandle() {
            }

            public virtual void Delete() {
                if (!DeleteService(this)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }

            public virtual void SetDescription(string description) {
                var descriptionInfo = new ServiceDescriptionInfo(description ?? string.Empty);
                var lpDescriptionInfo = Marshal.AllocHGlobal(Marshal.SizeOf<ServiceDescriptionInfo>());
                try {
                    Marshal.StructureToPtr(descriptionInfo, lpDescriptionInfo, fDeleteOld: false);
                    if (!ChangeServiceConfig2W(this, ServiceConfigInfoTypeLevel.ServiceDescription, lpDescriptionInfo)) {
                        throw new Win32Exception(Marshal.GetLastWin32Error());
                    }
                } finally {
                    Marshal.FreeHGlobal(lpDescriptionInfo);
                }
            }

            public virtual void Start() {
                if (!StartServiceW(this, 0, IntPtr.Zero)) {
                    throw new Win32Exception(Marshal.GetLastWin32Error());
                }
            }
        }

        private sealed class SimpleServiceStateMachine : IWin32ServiceStateMachine {
            private readonly IWin32Service serviceImplementation;
            private ServiceStatusReportCallback statusReportCallback;

            public SimpleServiceStateMachine(IWin32Service serviceImplementation) {
                this.serviceImplementation = serviceImplementation;
            }

            public void OnCommand(ServiceControlCommand command, uint commandSpecificEventType) {
                if (command == ServiceControlCommand.Stop) {
                    statusReportCallback(ServiceState.StopPending, ServiceAcceptedControlCommandsFlags.None, win32ExitCode: 0, waitHint: 3000);

                    var win32ExitCode = 0;

                    try {
                        serviceImplementation.Stop();
                    } catch {
                        win32ExitCode = -1;
                    }

                    statusReportCallback(ServiceState.Stopped, ServiceAcceptedControlCommandsFlags.None, win32ExitCode, waitHint: 0);
                }
            }

            public void OnStart(string[] startupArguments, ServiceStatusReportCallback statusReportCallback) {
                this.statusReportCallback = statusReportCallback;

                try {
                    serviceImplementation.Start(startupArguments, HandleServiceImplementationStoppedOnItsOwn);

                    statusReportCallback(ServiceState.Running, ServiceAcceptedControlCommandsFlags.Stop, win32ExitCode: 0, waitHint: 0);
                } catch {
                    statusReportCallback(ServiceState.Stopped, ServiceAcceptedControlCommandsFlags.None, win32ExitCode: -1, waitHint: 0);
                }
            }

            private void HandleServiceImplementationStoppedOnItsOwn() {
                statusReportCallback(ServiceState.Stopped, ServiceAcceptedControlCommandsFlags.None, win32ExitCode: 0, waitHint: 0);
            }
        }

        #endregion

        #region Structures

        [StructLayout(LayoutKind.Sequential)]
        private struct ServiceDescriptionInfo {
            [MarshalAs(UnmanagedType.LPWStr)]
            public string Description;
            public ServiceDescriptionInfo(string serviceDescription) {
                Description = serviceDescription;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ServiceStatus {
            public ServiceType serviceType;
            public ServiceState state;
            public ServiceAcceptedControlCommandsFlags acceptedControlCommands;
            public int win32ExitCode;
            public uint serviceSpecificExitCode;
            public uint checkPoint;
            public uint waitHint;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ServiceTableEntry {
            [MarshalAs(UnmanagedType.LPWStr)]
            internal string serviceName;
            internal IntPtr serviceMainFunction;
        }


        #endregion

        #region Enums

        [Flags]
        private enum ServiceAcceptedControlCommandsFlags : uint {
            None = 0,
            Stop = 0x00000001,
            PauseContinue = 0x00000002,
            Shutdown = 0x00000004,
            ParamChange = 0x00000008,
            NetBindChange = 0x00000010,
            PreShutdown = 0x00000100,
            HardwareProfileChange = 0x00000020,
            PowerEvent = 0x00000040,
            SessionChange = 0x00000080
        }

        private enum ErrorSeverity : uint {
            Ignore = 0,
            Normal = 1,
            Severe = 2,
            Crititcal = 3
        }

        private enum ServiceConfigInfoTypeLevel : uint {
            ServiceDescription = 1,
            FailureActions = 2,
            DelayedAutoStartInfo = 3,
            FailureActionsFlag = 4,
            ServiceSidInfo = 5,
            RequiredPrivilegesInfo = 6,
            PreShutdownInfo = 7,
            TriggerInfo = 8,
            PreferredNode = 9,
            LaunchProtected = 12
        }

        [Flags]
        private enum ServiceControlAccessRights : uint {
            QueryConfig = 0x00001,
            ChangeConfig = 0x00002,
            QueryStatus = 0x00004,
            EnumerateDependents = 0x00008,
            Start = 0x00010,
            Stop = 0x00020,
            PauseContinue = 0x00040,
            Interrogate = 0x00080,
            UserDefinedControl = 0x00100,

            All = Win32AccessMask.StandardRightsRequired
                  | QueryConfig
                  | ChangeConfig
                  | QueryStatus
                  | EnumerateDependents
                  | Start
                  | Stop
                  | PauseContinue
                  | Interrogate
                  | UserDefinedControl
        }

        private enum ServiceControlCommand : uint {
            Stop = 0x00000001,
            Pause = 0x00000002,
            Continue = 0x00000003,
            Interrogate = 0x00000004,
            Shutdown = 0x00000005,
            Paramchange = 0x00000006,
            NetBindAdd = 0x00000007,
            NetBindRemoved = 0x00000008,
            NetBindEnable = 0x00000009,
            NetBindDisable = 0x0000000A,
            DeviceEvent = 0x0000000B,
            HardwareProfileChange = 0x0000000C,
            PowerEvent = 0x0000000D,
            SessionChange = 0x0000000E
        }

        [Flags]
        private enum ServiceControlManagerAccessRights : uint {
            Connect = 0x00001,
            CreateService = 0x00002,
            EnumerateServices = 0x00004,
            LockServiceDatabase = 0x00008,
            QueryLockStatus = 0x00010,
            ModifyBootConfig = 0x00020,

            All = Win32AccessMask.StandardRightsRequired |
                  Connect |
                  CreateService |
                  EnumerateServices |
                  LockServiceDatabase |
                  QueryLockStatus |
                  ModifyBootConfig,

            GenericRead = Win32AccessMask.StandardRightsRequired |
                          EnumerateServices |
                          QueryLockStatus,

            GenericWrite = Win32AccessMask.StandardRightsRequired |
                           CreateService |
                           ModifyBootConfig,

            GenericExecute = Win32AccessMask.StandardRightsRequired |
                             Connect |
                             LockServiceDatabase,

            GenericAll = All
        }

        private enum ServiceStartType : uint {
            StartOnBoot = 0,
            StartOnSystemStart = 1,
            AutoStart = 2,
            StartOnDemand = 3,
            Disabled = 4
        }

        private enum ServiceState : uint {
            Stopped = 0x00000001,
            StartPending = 0x00000002,
            StopPending = 0x00000003,
            Running = 0x00000004,
            ContinuePending = 0x00000005,
            PausePending = 0x00000006,
            Paused = 0x00000007
        }
        [Flags]
        private enum ServiceType : uint {
            FileSystemDriver = 0x00000002,
            KernelDriver = 0x00000001,
            Win32OwnProcess = 0x00000010,
            Win32ShareProcess = 0x00000020,
            InteractiveProcess = 0x00000100
        }

        [Flags]
        private enum Win32AccessMask : uint {
            Delete = 0x00010000,
            ReadControl = 0x00020000,
            WriteDac = 0x00040000,
            WriteOwner = 0x00080000,
            Synchronize = 0x00100000,

            StandardRightsRequired = 0x000F0000,

            StandardRightsRead = 0x00020000,
            StandardRightsWrite = 0x00020000,
            StandardRightsExecute = 0x00020000,

            StandardRightsAll = 0x001F0000,

            SpecificRightsAll = 0x0000FFFF,

            AccessSystemSecurity = 0x01000000,

            MaximumAllowed = 0x02000000,

            GenericRead = 0x80000000,
            GenericWrite = 0x40000000,
            GenericExecute = 0x20000000,
            GenericAll = 0x10000000,

            DesktopReadobjects = 0x00000001,
            DesktopCreatewindow = 0x00000002,
            DesktopCreatemenu = 0x00000004,
            DesktopHookcontrol = 0x00000008,
            DesktopJournalrecord = 0x00000010,
            DesktopJournalplayback = 0x00000020,
            DesktopEnumerate = 0x00000040,
            DesktopWriteobjects = 0x00000080,
            DesktopSwitchdesktop = 0x00000100,

            WinstaEnumdesktops = 0x00000001,
            WinstaReadattributes = 0x00000002,
            WinstaAccessclipboard = 0x00000004,
            WinstaCreatedesktop = 0x00000008,
            WinstaWriteattributes = 0x00000010,
            WinstaAccessglobalatoms = 0x00000020,
            WinstaExitwindows = 0x00000040,
            WinstaEnumerate = 0x00000100,
            WinstaReadscreen = 0x00000200,

            WinstaAllAccess = 0x0000037F
        }

        #endregion

    }
}