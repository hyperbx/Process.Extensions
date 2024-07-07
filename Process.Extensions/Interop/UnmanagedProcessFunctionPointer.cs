using ProcessExtensions.Enums;
using ProcessExtensions.Exceptions;
using ProcessExtensions.Extensions.Internal;
using ProcessExtensions.Logger;
using ProcessExtensions.Interop.Context;
using ProcessExtensions.Interop.Events;
using ProcessExtensions.Interop.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Vanara.PInvoke;

#pragma warning disable CA1416 // Validate platform compatibility

namespace ProcessExtensions.Interop
{
    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="T">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, use <see cref="UnmanagedProcessFunctionPointer"/>.</para>
    /// </typeparam>
    public class UnmanagedProcessFunctionPointer<T> : IDisposable where T : unmanaged
    {
        private Process? _process;
        private nint _address;

        private Kernel32.SafeEventHandle? _eventHandle;
        private bool _isEventSignaled = false;

        private nint _isWoW64ThreadFinishedAddr;
        private nint _threadHandleAddr;
        private nint _returnValueAddr;
        private nint _wrapperAddr;

        public Process? Process
        {
            get => _process;

            set
            {
                Dispose();

                _process = value;
            }
        }

        public nint Address
        {
            get => _address;

            set
            {
                Dispose();

                _address = value;
            }
        }

        public ECallingConvention CallingConvention { get; set; }

        public bool IsVariadicArgs { get; set; }

        public ETypeCode[]? ArgumentTypes { get; set; }

        public bool IsThrowOnProcessExit { get; set; } = true;

        public event SetContextEventHandler? Prefix;
        public event GetContextEventHandler? Postfix;

        /// <summary>
        /// Creates a pointer to an unmanaged function in a native process.
        /// </summary>
        /// <param name="in_process">The target process for execution.</param>
        /// <param name="in_address">The remote address of the function to execute.</param>
        /// <param name="in_callingConvention">The calling convention of the function.</param>
        /// <param name="in_isVariadicArgs">Determines whether the function uses variadic arguments.</param>
        public UnmanagedProcessFunctionPointer(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        {
            Process = in_process;
            Address = in_address;
            CallingConvention = in_callingConvention;

            if (CallingConvention == ECallingConvention.Windows)
            {
                CallingConvention = in_process.Is64Bit()
                    ? ECallingConvention.FastCall
                    : ECallingConvention.StdCall;
            }
            else if (CallingConvention != ECallingConvention.UserCall)
            {
                // x86-64 is always __fastcall.
                if (in_process.Is64Bit())
                    CallingConvention = ECallingConvention.FastCall;
            }

            IsVariadicArgs = in_isVariadicArgs;
        }

        /// <summary>
        /// Creates a pointer to an unmanaged function in a native process.
        /// </summary>
        /// <param name="in_process">The target process for execution.</param>
        /// <param name="in_address">The remote address of the function to execute.</param>
        /// <param name="in_callingConvention">The calling convention of the function.</param>
        /// <param name="in_isVariadicArgs">Determines whether the function uses variadic arguments.</param>
        /// <param name="in_argumentTypes">The type codes for each argument.</param>
        public UnmanagedProcessFunctionPointer(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false, params ETypeCode[] in_argumentTypes)
            : this(in_process, in_address, in_callingConvention, in_isVariadicArgs)
        {
            ArgumentTypes = in_argumentTypes;
        }

        private void Execute(params object[] in_args)
        {
            if (Process == null)
                return;

            var is64Bit = Process.Is64Bit();

            if (_eventHandle == null)
            {
                _eventHandle = Kernel32.CreateEvent(null, true, false, null);

                if (_eventHandle == 0)
                    throw new VerboseWin32Exception($"Failed to create event.");

                if (!is64Bit)
                    _isWoW64ThreadFinishedAddr = Process.Alloc(1);

                _threadHandleAddr = Process.Alloc(8);
            }

            var threadHandle = Kernel32.CreateRemoteThread(Process.Handle, null, 0, Address, 0, Kernel32.CREATE_THREAD_FLAGS.CREATE_SUSPENDED, out _);

            if (threadHandle == 0)
                throw new VerboseWin32Exception($"Failed to create remote thread.");

            if (is64Bit)
                Process.Write(_threadHandleAddr, Process.InheritHandle(threadHandle.DangerousGetHandle()));

            if (_wrapperAddr == 0)
            {
                var rax = Process.GetFullWidthRegister(EBaseRegister.RAX);
                var rcx = Process.GetFullWidthRegister(EBaseRegister.RCX);

                var isVoid = typeof(T).Equals(typeof(UnmanagedVoid));

                var returnValueInstr = isVoid
                    ? string.Empty
                    : $"{Assembly.GetMovOpcodeByType<T>()} [{rcx}], {Process.GetReturnRegisterByType<T>()}";

                _returnValueAddr = isVoid
                    ? 0
                    : Process.Alloc(Marshal.SizeOf<T>());

                _wrapperAddr = Process.WriteBytes
                (
                    Process.Assemble
                    (
                        $@"
                            ; Call unmanaged function.
                            mov  {rax}, {Address}
                            call {rax}
                            mov  {rcx}, {_returnValueAddr}
                            {returnValueInstr}
                        "
                        +
                        (
                            is64Bit
                                ? $@"
                                    ; Call SetEvent to resume execution of the C# process.
                                    mov  {rcx}, {Process.InheritHandle(_eventHandle.DangerousGetHandle())}
                                    mov  {rax}, {Process.GetProcedureAddress("kernel32", "SetEvent")}
                                    call {rax}

                                    ; Call SuspendThread to safely terminate the thread in the C# process.
                                    mov  {rcx}, {_threadHandleAddr}
                                    mov  {rcx}, [{rcx}]
                                    mov  {rax}, {Process.GetProcedureAddress("kernel32", "SuspendThread")}
                                    call {rax}

                                    int  3
                                "
                                : $@"
                                    ; Set thread finished signal.
                                    mov  {rcx}, {_isWoW64ThreadFinishedAddr}
                                    mov  byte ptr [{rcx}], 1
                                wait:
                                    ; Wait indefinitely until C# process resumes execution.
                                    nop
                                    jmp  wait
                                "
                        )
                    )
                );

#if DEBUG
                LoggerService.Utility($"Emitted wrapper at 0x{_wrapperAddr:X}.");
#endif
            }

            var context = new ContextFactory(Process, threadHandle, CallingConvention);

            if (CallingConvention != ECallingConvention.UserCall)
            {
                // Set remote thread context.
                context.Set(_wrapperAddr, IsVariadicArgs, in_args);
            }
            else if (Prefix != null)
            {
                var contextWrapper = new ContextWrapper(Process, threadHandle);
                {
                    contextWrapper.SetGPR(EBaseRegister.RIP, _wrapperAddr);
                }

                Prefix?.Invoke(this, contextWrapper);
            }
            else
            {
                throw new NotImplementedException("Invalid context. Subscribe to the Prefix event to set the thread context when using UserCall.");
            }

            // Reset 32-bit thread state.
            if (!is64Bit)
                Process.Write(_isWoW64ThreadFinishedAddr, false);

            if (Kernel32.ResumeThread(threadHandle) == 0xFFFFFFFF)
                throw new VerboseWin32Exception("Failed to resume thread.");

            if (is64Bit)
            {
                var processTerminatedEventThread = new Thread
                (
                    delegate()
                    {
                        while (!_isEventSignaled && !Process.HasExited)
                            Thread.Sleep(20);

                        /* Resume main thread execution if the target
                           process was terminated unexpectedly. */
                        if (Process.HasExited)
                            Kernel32.SetEvent(_eventHandle);

                        _isEventSignaled = false;
                    }
                );

                processTerminatedEventThread.Start();

                Kernel32.WaitForSingleObject(_eventHandle, Kernel32.INFINITE);
                Kernel32.ResetEvent(_eventHandle);

                _isEventSignaled = true;
            }
            else
            {
                // Wait for thread to notify its completion.
                while (!Process.HasExited && !Process.Read<bool>(_isWoW64ThreadFinishedAddr))
                    Thread.Sleep(20);
            }

            if (Process.HasExited)
            {
                if (IsThrowOnProcessExit)
                    throw new AggregateException("The target process has terminated unexpectedly.");
            }
            else
            {
                if (Kernel32.SuspendThread(threadHandle) == 0xFFFFFFFF)
                    throw new VerboseWin32Exception("Failed to suspend thread.");

                Postfix?.Invoke(this, new ContextWrapper(Process, threadHandle));

                if (!Kernel32.TerminateThread(threadHandle, 0))
                    throw new VerboseWin32Exception("Failed to terminate thread.");
            }

            context.Clean();

            threadHandle.Close();
        }

        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_args">The arguments to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="AggregateException"/>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        /// <exception cref="VerboseWin32Exception"/>
        public virtual T Invoke(params object[] in_args)
        {
            if (Process == null)
                return default;

            if (ArgumentTypes != null)
            {
                var receivedLength = in_args.Length;
                var expectedLength = ArgumentTypes.Length;

                if (receivedLength < expectedLength)
                    throw new ArgumentException($"Not enough arguments provided. Expected: {expectedLength}. Received: {receivedLength}.");

                if (receivedLength > expectedLength)
                    throw new ArgumentException($"Too many arguments provided. Expected: {expectedLength}. Received: {receivedLength}.");

                for (int i = 0; i < receivedLength; i++)
                {
                    var argType = ArgumentTypes[i];

                    if (in_args[i].GetType().IsMatchingTypeCode(argType))
                        throw new InvalidCastException($"Argument {i} is not of type {argType}.");
                }
            }

            Execute(in_args);

            if (_returnValueAddr == 0)
                return default;

            return Process.Read<T>(_returnValueAddr);
        }

        public void Dispose()
        {
            _eventHandle?.Close();
            _eventHandle = null;

            Process?.Free(_isWoW64ThreadFinishedAddr);
            Process?.Free(_threadHandleAddr);
            Process?.Free(_returnValueAddr);
            Process?.Free(_wrapperAddr);

            _isWoW64ThreadFinishedAddr = 0;
            _threadHandleAddr = 0;
            _returnValueAddr = 0;
            _wrapperAddr = 0;
        }
    }

    #region Generic Types

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    public class UnmanagedProcessFunctionPointer(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<UnmanagedVoid>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
    {
        public UnmanagedProcessFunctionPointer(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, params ETypeCode[] in_argumentTypes)
            : this(in_process, in_address, in_callingConvention)
        {
            ArgumentTypes = in_argumentTypes;
        }

        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_args">The arguments to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public new void Invoke(params object[] in_args)
        {
            base.Invoke(in_args);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T">The type of the first argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T       : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T in_a1)
        {
            return (TResult)(object)base.Invoke(in_a1);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5, T6>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
            where T6      : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <param name="in_a6">The sixth argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5, T6 in_a6)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5, in_a6);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T7">The type of the seventh argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5, T6, T7>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
            where T6      : unmanaged
            where T7      : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <param name="in_a6">The sixth argument to pass into the unmanaged function.</param>
        /// <param name="in_a7">The seventh argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5, T6 in_a6, T7 in_a7)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5, in_a6, in_a7);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T7">The type of the seventh argument of the unmanaged function.</typeparam>
    /// <typeparam name="T8">The type of the eighth argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5, T6, T7, T8>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
            where T6      : unmanaged
            where T7      : unmanaged
            where T8      : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <param name="in_a6">The sixth argument to pass into the unmanaged function.</param>
        /// <param name="in_a7">The seventh argument to pass into the unmanaged function.</param>
        /// <param name="in_a8">The eighth argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5, T6 in_a6, T7 in_a7, T8 in_a8)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5, in_a6, in_a7, in_a8);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T7">The type of the seventh argument of the unmanaged function.</typeparam>
    /// <typeparam name="T8">The type of the eighth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T9">The type of the ninth argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
            where T6      : unmanaged
            where T7      : unmanaged
            where T8      : unmanaged
            where T9      : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <param name="in_a6">The sixth argument to pass into the unmanaged function.</param>
        /// <param name="in_a7">The seventh argument to pass into the unmanaged function.</param>
        /// <param name="in_a8">The eighth argument to pass into the unmanaged function.</param>
        /// <param name="in_a9">The ninth argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5, T6 in_a6, T7 in_a7, T8 in_a8, T9 in_a9)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5, in_a6, in_a7, in_a8, in_a9);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T7">The type of the seventh argument of the unmanaged function.</typeparam>
    /// <typeparam name="T8">The type of the eighth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T9">The type of the ninth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T10">The type of the tenth argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
            where T6      : unmanaged
            where T7      : unmanaged
            where T8      : unmanaged
            where T9      : unmanaged
            where T10     : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <param name="in_a6">The sixth argument to pass into the unmanaged function.</param>
        /// <param name="in_a7">The seventh argument to pass into the unmanaged function.</param>
        /// <param name="in_a8">The eighth argument to pass into the unmanaged function.</param>
        /// <param name="in_a9">The ninth argument to pass into the unmanaged function.</param>
        /// <param name="in_a10">The 10th argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5, T6 in_a6, T7 in_a7, T8 in_a8, T9 in_a9, T10 in_a10)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5, in_a6, in_a7, in_a8, in_a9, in_a10);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T7">The type of the seventh argument of the unmanaged function.</typeparam>
    /// <typeparam name="T8">The type of the eighth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T9">The type of the ninth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T10">The type of the 10th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T11">The type of the 11th argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
            where T6      : unmanaged
            where T7      : unmanaged
            where T8      : unmanaged
            where T9      : unmanaged
            where T10     : unmanaged
            where T11     : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <param name="in_a6">The sixth argument to pass into the unmanaged function.</param>
        /// <param name="in_a7">The seventh argument to pass into the unmanaged function.</param>
        /// <param name="in_a8">The eighth argument to pass into the unmanaged function.</param>
        /// <param name="in_a9">The ninth argument to pass into the unmanaged function.</param>
        /// <param name="in_a10">The 10th argument to pass into the unmanaged function.</param>
        /// <param name="in_a11">The 11th argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5, T6 in_a6, T7 in_a7, T8 in_a8, T9 in_a9, T10 in_a10, T11 in_a11)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5, in_a6, in_a7, in_a8, in_a9, in_a10, in_a11);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T7">The type of the seventh argument of the unmanaged function.</typeparam>
    /// <typeparam name="T8">The type of the eighth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T9">The type of the ninth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T10">The type of the 10th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T11">The type of the 11th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T12">The type of the 12th argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
            where T6      : unmanaged
            where T7      : unmanaged
            where T8      : unmanaged
            where T9      : unmanaged
            where T10     : unmanaged
            where T11     : unmanaged
            where T12     : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <param name="in_a6">The sixth argument to pass into the unmanaged function.</param>
        /// <param name="in_a7">The seventh argument to pass into the unmanaged function.</param>
        /// <param name="in_a8">The eighth argument to pass into the unmanaged function.</param>
        /// <param name="in_a9">The ninth argument to pass into the unmanaged function.</param>
        /// <param name="in_a10">The 10th argument to pass into the unmanaged function.</param>
        /// <param name="in_a11">The 11th argument to pass into the unmanaged function.</param>
        /// <param name="in_a12">The 12th argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5, T6 in_a6, T7 in_a7, T8 in_a8, T9 in_a9, T10 in_a10, T11 in_a11, T12 in_a12)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5, in_a6, in_a7, in_a8, in_a9, in_a10, in_a11, in_a12);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T7">The type of the seventh argument of the unmanaged function.</typeparam>
    /// <typeparam name="T8">The type of the eighth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T9">The type of the ninth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T10">The type of the 10th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T11">The type of the 11th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T12">The type of the 12th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T13">The type of the 13th argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
            where T6      : unmanaged
            where T7      : unmanaged
            where T8      : unmanaged
            where T9      : unmanaged
            where T10     : unmanaged
            where T11     : unmanaged
            where T12     : unmanaged
            where T13     : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <param name="in_a6">The sixth argument to pass into the unmanaged function.</param>
        /// <param name="in_a7">The seventh argument to pass into the unmanaged function.</param>
        /// <param name="in_a8">The eighth argument to pass into the unmanaged function.</param>
        /// <param name="in_a9">The ninth argument to pass into the unmanaged function.</param>
        /// <param name="in_a10">The 10th argument to pass into the unmanaged function.</param>
        /// <param name="in_a11">The 11th argument to pass into the unmanaged function.</param>
        /// <param name="in_a12">The 12th argument to pass into the unmanaged function.</param>
        /// <param name="in_a13">The 13th argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5, T6 in_a6, T7 in_a7, T8 in_a8, T9 in_a9, T10 in_a10, T11 in_a11, T12 in_a12, T13 in_a13)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5, in_a6, in_a7, in_a8, in_a9, in_a10, in_a11, in_a12, in_a13);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T7">The type of the seventh argument of the unmanaged function.</typeparam>
    /// <typeparam name="T8">The type of the eighth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T9">The type of the ninth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T10">The type of the 10th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T11">The type of the 11th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T12">The type of the 12th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T13">The type of the 13th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T14">The type of the 14th argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
            where T6      : unmanaged
            where T7      : unmanaged
            where T8      : unmanaged
            where T9      : unmanaged
            where T10     : unmanaged
            where T11     : unmanaged
            where T12     : unmanaged
            where T13     : unmanaged
            where T14     : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <param name="in_a6">The sixth argument to pass into the unmanaged function.</param>
        /// <param name="in_a7">The seventh argument to pass into the unmanaged function.</param>
        /// <param name="in_a8">The eighth argument to pass into the unmanaged function.</param>
        /// <param name="in_a9">The ninth argument to pass into the unmanaged function.</param>
        /// <param name="in_a10">The 10th argument to pass into the unmanaged function.</param>
        /// <param name="in_a11">The 11th argument to pass into the unmanaged function.</param>
        /// <param name="in_a12">The 12th argument to pass into the unmanaged function.</param>
        /// <param name="in_a13">The 13th argument to pass into the unmanaged function.</param>
        /// <param name="in_a14">The 14th argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5, T6 in_a6, T7 in_a7, T8 in_a8, T9 in_a9, T10 in_a10, T11 in_a11, T12 in_a12, T13 in_a13, T14 in_a14)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5, in_a6, in_a7, in_a8, in_a9, in_a10, in_a11, in_a12, in_a13, in_a14);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T7">The type of the seventh argument of the unmanaged function.</typeparam>
    /// <typeparam name="T8">The type of the eighth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T9">The type of the ninth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T10">The type of the 10th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T11">The type of the 11th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T12">The type of the 12th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T13">The type of the 13th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T14">The type of the 14th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T15">The type of the 15th argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
            where T6      : unmanaged
            where T7      : unmanaged
            where T8      : unmanaged
            where T9      : unmanaged
            where T10     : unmanaged
            where T11     : unmanaged
            where T12     : unmanaged
            where T13     : unmanaged
            where T14     : unmanaged
            where T15     : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <param name="in_a6">The sixth argument to pass into the unmanaged function.</param>
        /// <param name="in_a7">The seventh argument to pass into the unmanaged function.</param>
        /// <param name="in_a8">The eighth argument to pass into the unmanaged function.</param>
        /// <param name="in_a9">The ninth argument to pass into the unmanaged function.</param>
        /// <param name="in_a10">The 10th argument to pass into the unmanaged function.</param>
        /// <param name="in_a11">The 11th argument to pass into the unmanaged function.</param>
        /// <param name="in_a12">The 12th argument to pass into the unmanaged function.</param>
        /// <param name="in_a13">The 13th argument to pass into the unmanaged function.</param>
        /// <param name="in_a14">The 14th argument to pass into the unmanaged function.</param>
        /// <param name="in_a15">The 15th argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5, T6 in_a6, T7 in_a7, T8 in_a8, T9 in_a9, T10 in_a10, T11 in_a11, T12 in_a12, T13 in_a13, T14 in_a14, T15 in_a15)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5, in_a6, in_a7, in_a8, in_a9, in_a10, in_a11, in_a12, in_a13, in_a14, in_a15);
        }
    }

    /// <summary>
    /// Defines a pointer to an unmanaged function in a native process.
    /// <para>For more than 16 arguments, use <see cref="UnmanagedProcessFunctionPointer{T}"/> and define the argument types using <see cref="UnmanagedProcessFunctionPointer{T}.ArgumentTypes"/>.</para>
    /// </summary>
    /// <typeparam name="TResult">
    ///     The return type of the unmanaged function.
    ///     <para>If there is no return value, set this to <see cref="UnmanagedVoid"/>.</para>
    /// </typeparam>
    /// <typeparam name="T1">The type of the first argument of the unmanaged function.</typeparam>
    /// <typeparam name="T2">The type of the second argument of the unmanaged function.</typeparam>
    /// <typeparam name="T3">The type of the third argument of the unmanaged function.</typeparam>
    /// <typeparam name="T4">The type of the fourth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T5">The type of the fifth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T6">The type of the sixth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T7">The type of the seventh argument of the unmanaged function.</typeparam>
    /// <typeparam name="T8">The type of the eighth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T9">The type of the ninth argument of the unmanaged function.</typeparam>
    /// <typeparam name="T10">The type of the 10th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T11">The type of the 11th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T12">The type of the 12th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T13">The type of the 13th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T14">The type of the 14th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T15">The type of the 15th argument of the unmanaged function.</typeparam>
    /// <typeparam name="T16">The type of the 16th argument of the unmanaged function.</typeparam>
    public class UnmanagedProcessFunctionPointer<TResult, T1, T2, T3, T4, T5, T6, T7, T8, T9, T10, T11, T12, T13, T14, T15, T16>(Process in_process, nint in_address, ECallingConvention in_callingConvention = ECallingConvention.FastCall, bool in_isVariadicArgs = false)
        : UnmanagedProcessFunctionPointer<TResult>(in_process, in_address, in_callingConvention, in_isVariadicArgs)
            where TResult : unmanaged
            where T1      : unmanaged
            where T2      : unmanaged
            where T3      : unmanaged
            where T4      : unmanaged
            where T5      : unmanaged
            where T6      : unmanaged
            where T7      : unmanaged
            where T8      : unmanaged
            where T9      : unmanaged
            where T10     : unmanaged
            where T11     : unmanaged
            where T12     : unmanaged
            where T13     : unmanaged
            where T14     : unmanaged
            where T15     : unmanaged
            where T16     : unmanaged
    {
        /// <summary>
        /// Emits a wrapper to call the unmanaged function and invokes it.
        /// </summary>
        /// <param name="in_a1">The first argument to pass into the unmanaged function.</param>
        /// <param name="in_a2">The second argument to pass into the unmanaged function.</param>
        /// <param name="in_a3">The third argument to pass into the unmanaged function.</param>
        /// <param name="in_a4">The fourth argument to pass into the unmanaged function.</param>
        /// <param name="in_a5">The fifth argument to pass into the unmanaged function.</param>
        /// <param name="in_a6">The sixth argument to pass into the unmanaged function.</param>
        /// <param name="in_a7">The seventh argument to pass into the unmanaged function.</param>
        /// <param name="in_a8">The eighth argument to pass into the unmanaged function.</param>
        /// <param name="in_a9">The ninth argument to pass into the unmanaged function.</param>
        /// <param name="in_a10">The 10th argument to pass into the unmanaged function.</param>
        /// <param name="in_a11">The 11th argument to pass into the unmanaged function.</param>
        /// <param name="in_a12">The 12th argument to pass into the unmanaged function.</param>
        /// <param name="in_a13">The 13th argument to pass into the unmanaged function.</param>
        /// <param name="in_a14">The 14th argument to pass into the unmanaged function.</param>
        /// <param name="in_a15">The 15th argument to pass into the unmanaged function.</param>
        /// <param name="in_a16">The 16th argument to pass into the unmanaged function.</param>
        /// <returns>The return value of the unmanaged function.</returns>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="InvalidCastException"/>
        public TResult Invoke(T1 in_a1, T2 in_a2, T3 in_a3, T4 in_a4, T5 in_a5, T6 in_a6, T7 in_a7, T8 in_a8, T9 in_a9, T10 in_a10, T11 in_a11, T12 in_a12, T13 in_a13, T14 in_a14, T15 in_a15, T16 in_a16)
        {
            return (TResult)(object)base.Invoke(in_a1, in_a2, in_a3, in_a4, in_a5, in_a6, in_a7, in_a8, in_a9, in_a10, in_a11, in_a12, in_a13, in_a14, in_a15, in_a16);
        }
    }

    #endregion
}

#pragma warning restore CA1416 // Validate platform compatibility