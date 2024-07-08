using ProcessExtensions.Enums;
using ProcessExtensions.Exceptions;
using ProcessExtensions.Extensions;
using ProcessExtensions.Extensions.Internal;
using ProcessExtensions.Helpers.Internal;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Vanara.Extensions;
using Vanara.PInvoke;

#pragma warning disable CA1416 // Validate platform compatibility
#pragma warning disable CS9124 // Parameter is captured into the state of the enclosing type and its value is also used to initialize a field, property, or event.

namespace ProcessExtensions.Interop.Context
{
    public class ContextWrapper(Process in_process, Kernel32.SafeHTHREAD? in_threadHandle)
    {
        public Process Process { get; } = in_process;

        public Kernel32.SafeHTHREAD? ThreadHandle { get; } = in_threadHandle;

        private bool IsThreadSuspended()
        {
            if (ThreadHandle == null)
                return false;

            var id = Kernel32.GetThreadId(ThreadHandle);

            Process.Refresh();

            foreach (ProcessThread thread in Process.Threads)
            {
                if (thread.Id != id)
                    continue;

                if (thread.ThreadState != System.Diagnostics.ThreadState.Wait)
                    continue;

                if (thread.WaitReason == ThreadWaitReason.Suspended)
                    return true;
            }

            return false;
        }

        private void ThrowIfThreadNotSuspended()
        {
            if (IsThreadSuspended())
                return;

            throw new ThreadStateException("The thread is not suspended and its context cannot be determined.");
        }

        /// <summary>
        /// Gets the value of a general purpose register.
        /// </summary>
        /// <param name="in_register">The register containing the value.</param>
        /// <param name="in_type">The return type.</param>
        /// <param name="in_isRegisterPointerToValue">Determines whether the value of the register is a pointer and should be accessed to read the real value from its location.</param>
        /// <returns>The value of the specified register.</returns>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="VerboseWin32Exception"/>
        public object GetGPR(EBaseRegister in_register, Type in_type, bool in_isRegisterPointerToValue = false)
        {
            ThrowIfThreadNotSuspended();

            var registerName = in_register.ToString();

            if (registerName.StartsWith("ST") || registerName.StartsWith("XMM"))
                throw new NotSupportedException("Floating point registers are not supported by this method, use GetFPR instead.");

            object result;

            if (in_process.Is64Bit())
            {
                var context = Kernel32Helper.GetThreadContext64(ThreadHandle)
                    ?? throw new VerboseWin32Exception("Failed to get thread context.");

                result = in_register switch
                {
                    EBaseRegister.RAX => context.Rax,
                    EBaseRegister.RBX => context.Rbx,
                    EBaseRegister.RCX => context.Rcx,
                    EBaseRegister.RDX => context.Rdx,
                    EBaseRegister.RSI => context.Rsi,
                    EBaseRegister.RDI => context.Rdi,
                    EBaseRegister.RBP => context.Rbp,
                    EBaseRegister.RSP => context.Rsp,
                    EBaseRegister.R8  => context.R8,
                    EBaseRegister.R9  => context.R9,
                    EBaseRegister.R10 => context.R10,
                    EBaseRegister.R11 => context.R11,
                    EBaseRegister.R12 => context.R12,
                    EBaseRegister.R13 => context.R13,
                    EBaseRegister.R14 => context.R14,
                    EBaseRegister.R15 => context.R15,
                    EBaseRegister.RIP => context.Rip,
                    _                 => throw new NotSupportedException($"The specified register is not supported: {in_register}"),
                };
            }
            else
            {
                var context = Kernel32Helper.GetThreadContext(ThreadHandle)
                    ?? throw new VerboseWin32Exception("Failed to get thread context.");

                result = in_register switch
                {
                    EBaseRegister.RAX => context.Eax,
                    EBaseRegister.RBX => context.Ebx,
                    EBaseRegister.RCX => context.Ecx,
                    EBaseRegister.RDX => context.Edx,
                    EBaseRegister.RSI => context.Esi,
                    EBaseRegister.RDI => context.Edi,
                    EBaseRegister.RBP => context.Ebp,
                    EBaseRegister.RSP => context.Esp,
                    EBaseRegister.RIP => context.Eip,
                    _                 => throw new NotSupportedException($"The specified register is not supported: {in_register}"),
                };
            }

            if (in_isRegisterPointerToValue)
                result = MemoryHelper.ByteArrayToUnmanagedType(Process.ReadBytes((nint)result, Process.GetPointerSize()), in_type)!;

            return result;
        }

        /// <summary>
        /// Gets the value of a general purpose register.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to cast to.</typeparam>
        /// <param name="in_register">The register containing the value.</param>
        /// <param name="in_isRegisterPointerToValue">Determines whether the value of the register is a pointer and should be accessed to read the real value from its location.</param>
        /// <returns>The value of the specified register.</returns>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="VerboseWin32Exception"/>
        public T GetGPR<T>(EBaseRegister in_register, bool in_isRegisterPointerToValue = false) where T : unmanaged
        {
            var result = GetGPR(in_register, typeof(T), in_isRegisterPointerToValue);

            if (typeof(T).Equals(typeof(nint)))
                return (T)(object)new nint(Convert.ToInt64(result));

            return (T)result;
        }

        /// <summary>
        /// Gets the value of a general purpose register.
        /// </summary>
        /// <param name="in_register">The register containing the value.</param>
        /// <param name="in_isRegisterPointerToValue">Determines whether the value of the register is a pointer and should be accessed to read the real value from its location.</param>
        /// <returns>The value of the specified register.</returns>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="VerboseWin32Exception"/>
        public nint GetGPR(EBaseRegister in_register, bool in_isRegisterPointerToValue = false)
        {
            return GetGPR<nint>(in_register, in_isRegisterPointerToValue);
        }

        /// <summary>
        /// Sets the value of a general purpose register.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to cast to.</typeparam>
        /// <param name="in_register">The register containing the value.</param>
        /// <param name="in_value">The value to set the register to.</param>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="VerboseWin32Exception"/>
        public void SetGPR<T>(EBaseRegister in_register, T in_value) where T : unmanaged
        {
            ThrowIfThreadNotSuspended();

            var registerName = in_register.ToString();

            if (registerName.StartsWith("ST") || registerName.StartsWith("XMM"))
                throw new NotSupportedException("Floating point registers are not supported by this method, use SetFPR instead.");

            object o = in_value;

            if (o.GetType().Equals(typeof(nint)))
                o = ((nint)o).ToInt64();

            if (in_process.Is64Bit())
            {
                var context = Kernel32Helper.GetThreadContext64(ThreadHandle)
                    ?? throw new VerboseWin32Exception("Failed to get thread context.");

                var value = Convert.ToUInt64(o);

                switch (in_register)
                {
                    case EBaseRegister.RAX: context.Rax = value; break;
                    case EBaseRegister.RBX: context.Rbx = value; break;
                    case EBaseRegister.RCX: context.Rcx = value; break;
                    case EBaseRegister.RDX: context.Rdx = value; break;
                    case EBaseRegister.RSI: context.Rsi = value; break;
                    case EBaseRegister.RDI: context.Rdi = value; break;
                    case EBaseRegister.RBP: context.Rbp = value; break;
                    case EBaseRegister.RSP: context.Rsp = value; break;
                    case EBaseRegister.R8:  context.R8  = value; break;
                    case EBaseRegister.R9:  context.R9  = value; break;
                    case EBaseRegister.R10: context.R10 = value; break;
                    case EBaseRegister.R11: context.R11 = value; break;
                    case EBaseRegister.R12: context.R12 = value; break;
                    case EBaseRegister.R13: context.R13 = value; break;
                    case EBaseRegister.R14: context.R14 = value; break;
                    case EBaseRegister.R15: context.R15 = value; break;
                    case EBaseRegister.RIP: context.Rip = value; break;

                    default:
                        throw new NotSupportedException($"The specified register is not supported: {in_register}");
                }

                Kernel32Helper.SetThreadContext64(ThreadHandle, context);
            }
            else
            {
                var context = Kernel32Helper.GetThreadContext(ThreadHandle)
                    ?? throw new VerboseWin32Exception("Failed to get thread context.");

                var value = Convert.ToUInt32(o);

                switch (in_register)
                {
                    case EBaseRegister.RAX: context.Eax = value; break;
                    case EBaseRegister.RBX: context.Ebx = value; break;
                    case EBaseRegister.RCX: context.Ecx = value; break;
                    case EBaseRegister.RDX: context.Edx = value; break;
                    case EBaseRegister.RSI: context.Esi = value; break;
                    case EBaseRegister.RDI: context.Edi = value; break;
                    case EBaseRegister.RBP: context.Ebp = value; break;
                    case EBaseRegister.RSP: context.Esp = value; break;
                    case EBaseRegister.RIP: context.Eip = value; break;

                    default:
                        throw new NotSupportedException($"The specified register is not supported: {in_register}");
                }

                Kernel32Helper.SetThreadContext(ThreadHandle, context);
            }
        }

        /// <summary>
        /// Gets the value of a floating point register.
        /// </summary>
        /// <param name="in_register">The register containing the values.</param>
        /// <param name="in_type">The return type (constrained to <see cref="float"/>, <see cref="double"/> or an array of <see cref="byte"/>).</param>
        /// <param name="in_subIndex">
        ///     The index inside of the register containing the value.
        ///     <para>If <paramref name="in_type"/> is <see cref="float"/>, you may retrieve a float between sub-index 0 and 3.</para>
        ///     <para>If <paramref name="in_type"/> is <see cref="double"/>, you may retrieve a double between sub-index 0 and 1.</para>
        ///     <para>If <paramref name="in_type"/> is an array of <see cref="byte"/>, set this to zero to retrieve the full 128-bit vector.</para>
        /// </param>
        /// <returns>The value of the specified register.</returns>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="IndexOutOfRangeException"/>
        /// <exception cref="ArgumentOutOfRangeException"/>
        /// <exception cref="VerboseWin32Exception"/>
        public object GetFPR(EBaseRegister in_register, Type in_type, int in_subIndex = 0)
        {
            ThrowIfThreadNotSuspended();

            var registerName = in_register.ToString();

            if (registerName.StartsWith("ST"))
                throw new NotImplementedException("x87 FPU registers are not supported.");

            if (!registerName.StartsWith("XMM"))
                throw new NotSupportedException("General purpose registers are not supported by this method, use GetGPR instead.");

            var isFloat = in_type.Equals(typeof(float));
            var isDouble = in_type.Equals(typeof(double));
            var isByteArray = in_type.Equals(typeof(byte[]));

            if (!isFloat && !isDouble && !isByteArray)
                throw new ArgumentException("Type must be float, double or byte array.");

            var index = Convert.ToInt32(registerName[3..]);

            if (index < 0 || index >= (Process.Is64Bit() ? 16 : 8))
                throw new IndexOutOfRangeException("Invalid XMM register index.");

            if (isFloat && (in_subIndex < 0 || in_subIndex > 3))
                throw new ArgumentOutOfRangeException("Sub-index for float must be between 0 and 3.");

            if (isDouble && (in_subIndex < 0 || in_subIndex > 1))
                throw new ArgumentOutOfRangeException("Sub-index for double must be between 0 and 1.");

            if (isByteArray && in_subIndex != 0)
                throw new ArgumentOutOfRangeException("Sub-index for byte array must be zero.");

            var buffer = new byte[16];
            var length = Marshal.SizeOf(in_type);

            if (Process.Is64Bit())
            {
                var context = Kernel32Helper.GetThreadContext64(ThreadHandle)
                    ?? throw new VerboseWin32Exception("Failed to get thread context.");

                var m128 = MemoryHelper.UnmanagedTypeToByteArray(context.DUMMYUNIONNAME.XmmRegisters[index]);

                Array.Copy(m128, in_subIndex * length, buffer, 0, length);
            }
            else
            {
                if (index > 7)
                    throw new NotSupportedException("32-bit only has 8 XMM registers.");

                var context = Kernel32Helper.GetThreadContext(ThreadHandle)
                    ?? throw new VerboseWin32Exception("Failed to get thread context.");

                Array.Copy(context.ExtendedRegisters, (160 + index * 16) + in_subIndex * length, buffer, 0, length);
            }

            if (isByteArray)
                return buffer;

            return MemoryHelper.ByteArrayToUnmanagedType(buffer, in_type)!;
        }

        /// <summary>
        /// Gets the value of a floating point register.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to cast to (constrained to <see cref="float"/>, <see cref="double"/> or an array of <see cref="byte"/>).</typeparam>
        /// <param name="in_register">The register containing the values.</param>
        /// <param name="in_subIndex">
        ///     The index inside of the register containing the value.
        ///     <para>If <typeparamref name="T"/> is <see cref="float"/>, you may retrieve a float between sub-index 0 and 3.</para>
        ///     <para>If <typeparamref name="T"/> is <see cref="double"/>, you may retrieve a double between sub-index 0 and 1.</para>
        ///     <para>If <typeparamref name="T"/> is an array of <see cref="byte"/>, set this to zero to retrieve the full 128-bit vector.</para>
        /// </param>
        /// <returns>The value of the specified register.</returns>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="IndexOutOfRangeException"/>
        /// <exception cref="ArgumentOutOfRangeException"/>
        /// <exception cref="VerboseWin32Exception"/>
        public T GetFPR<T>(EBaseRegister in_register, int in_subIndex = 0) where T : unmanaged
        {
            return (T)GetFPR(in_register, typeof(T), in_subIndex);
        }

        /// <summary>
        /// Sets the value of a floating point register.
        /// </summary>
        /// <param name="in_register">The register containing the values.</param>
        /// <param name="in_subIndex">
        ///     The index inside of the register containing the value.
        ///     <para>If <paramref name="in_value"/> is a <see cref="float"/>, you may retrieve a float between sub-index 0 and 3.</para>
        ///     <para>If <paramref name="in_value"/> is a <see cref="double"/>, you may retrieve a double between sub-index 0 and 1.</para>
        ///     <para>If <paramref name="in_value"/> is an array of <see cref="byte"/>, set this to zero to retrieve the full 128-bit vector.</para>
        /// </param>
        /// <param name="in_value">
        ///     The value to set the register to.
        ///     <para>This must be a <see cref="float"/>, <see cref="double"/> or an array of <see cref="byte"/>.</para>
        /// </param>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="IndexOutOfRangeException"/>
        /// <exception cref="ArgumentOutOfRangeException"/>
        /// <exception cref="InternalBufferOverflowException"/>
        /// <exception cref="VerboseWin32Exception"/>
        public void SetFPR(EBaseRegister in_register, int in_subIndex, object in_value)
        {
            ThrowIfThreadNotSuspended();

            var registerName = in_register.ToString();

            if (registerName.StartsWith("ST"))
                throw new NotImplementedException("x87 FPU registers are not supported.");

            if (!registerName.StartsWith("XMM"))
                throw new NotSupportedException("General purpose registers are not supported by this method, use SetGPR instead.");

            var type = in_value.GetType();
            var isFloat = type.Equals(typeof(float));
            var isDouble = type.Equals(typeof(double));
            var isByteArray = type.Equals(typeof(byte[]));

            if (!isFloat && !isDouble && !isByteArray)
                throw new ArgumentException("Type must be float, double or byte array.");

            var index = Convert.ToInt32(registerName[3..]);

            if (index < 0 || index >= (Process.Is64Bit() ? 16 : 8))
                throw new IndexOutOfRangeException("Invalid XMM register index.");

            if (isFloat && (in_subIndex < 0 || in_subIndex > 3))
                throw new ArgumentOutOfRangeException("Sub-index for float must be between 0 and 3.");

            if (isDouble && (in_subIndex < 0 || in_subIndex > 1))
                throw new ArgumentOutOfRangeException("Sub-index for double must be between 0 and 1.");

            if (isByteArray && in_subIndex != 0)
                throw new ArgumentOutOfRangeException("Sub-index for byte array must be zero.");

            var buffer = isByteArray
                ? (byte[])in_value
                : isFloat
                    ? BitConverter.GetBytes(Convert.ToSingle(in_value))
                    : BitConverter.GetBytes(Convert.ToDouble(in_value));

            if (buffer.Length > 16)
            {
                throw new InternalBufferOverflowException(
                    $"The provided buffer cannot fit into the register. Expected: 16 bytes. Received: {buffer.Length} byte(s).");
            }

            if (Process.Is64Bit())
            {
                var context = Kernel32Helper.GetThreadContext64(ThreadHandle)
                    ?? throw new VerboseWin32Exception("Failed to get thread context.");

                var handle = GCHandle.Alloc(context.DUMMYUNIONNAME.XmmRegisters[index], GCHandleType.Pinned);

                try
                {
                    var ptr    = handle.AddrOfPinnedObject();
                    var regPtr = IntPtr.Add(ptr, index * Marshal.SizeOf<Kernel32.CONTEXT64.M128A>());
                    var subPtr = IntPtr.Add(regPtr, in_subIndex * Marshal.SizeOf(type));

                    Marshal.Copy(buffer, 0, subPtr, buffer.Length);
                }
                finally
                {
                    handle.Free();
                }

                Kernel32Helper.SetThreadContext64(ThreadHandle, context);
            }
            else
            {
                if (index > 7)
                    throw new NotSupportedException("32-bit only has 8 XMM registers.");

                var context = Kernel32Helper.GetThreadContext(ThreadHandle)
                    ?? throw new VerboseWin32Exception("Failed to get thread context.");

                Array.Copy(buffer, 0, context.ExtendedRegisters, 160 + index * buffer.Length, buffer.Length);

                Kernel32Helper.SetThreadContext(ThreadHandle, context);
            }
        }

        /// <summary>
        /// Sets the value of a floating point register.
        /// </summary>
        /// <param name="in_xmmRegisterIndex">The index of the XMM register containing the values.</param>
        /// <param name="in_subIndex">
        ///     The index inside of the register containing the value.
        ///     <para>If <paramref name="in_value"/> is a <see cref="float"/>, you may retrieve a float between sub-index 0 and 3.</para>
        ///     <para>If <paramref name="in_value"/> is a <see cref="double"/>, you may retrieve a double between sub-index 0 and 1.</para>
        ///     <para>If <paramref name="in_value"/> is an array of <see cref="byte"/>, set this to zero to retrieve the full 128-bit vector.</para>
        /// </param>
        /// <param name="in_value">
        ///     The value to set the register to.
        ///     <para>This must be a <see cref="float"/>, <see cref="double"/> or an array of <see cref="byte"/>.</para>
        /// </param>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="NotSupportedException"/>
        /// <exception cref="ArgumentException"/>
        /// <exception cref="IndexOutOfRangeException"/>
        /// <exception cref="ArgumentOutOfRangeException"/>
        /// <exception cref="InternalBufferOverflowException"/>
        /// <exception cref="VerboseWin32Exception"/>
        public void SetFPR(int in_xmmRegisterIndex, int in_subIndex, object in_value)
        {
            var xmmRegistersMax = (Process.Is64Bit() ? 16 : 8) - 1;

            if (!Enum.TryParse(typeof(EBaseRegister), $"XMM{in_xmmRegisterIndex}", true, out var out_register))
            {
                throw new ArgumentOutOfRangeException(
                    $"Could not determine register by index. Expected range: 0-{xmmRegistersMax}. Received: {in_xmmRegisterIndex}.");
            }

            SetFPR((EBaseRegister)out_register, in_subIndex, in_value);
        }

        /// <summary>
        /// Aligns the stack pointer by the specified amount.
        /// </summary>
        /// <param name="in_alignment">The alignment to use.</param>
        /// <returns>The new stack pointer.</returns>
        public nint AlignStackPointer(int in_alignment)
        {
            return SetStackPointer((nint)GetStackPointer().ToInt64().Align(in_alignment), false);
        }

        /// <summary>
        /// Aligns the stack pointer by the amount required by the remote process' architecture.
        /// </summary>
        /// <returns>The new stack pointer.</returns>
        public nint AlignStackPointer()
        {
            return AlignStackPointer(Process.Is64Bit() ? 16 : 4);
        }

        /// <summary>
        /// Gets the pointer to the current stack memory from the stack pointer register.
        /// </summary>
        public nint GetStackPointer()
        {
            return GetGPR<nint>(EBaseRegister.RSP);
        }

        /// <summary>
        /// Sets the stack pointer register to a new location.
        /// </summary>
        /// <param name="in_offset">The offset to set it to.</param>
        /// <param name="in_isAdditive">Determines whether to add <paramref name="in_offset"/> to the current stack pointer.</param>
        /// <returns>The new stack pointer.</returns>
        public nint SetStackPointer(nint in_offset, bool in_isAdditive = true)
        {
            var result = GetStackPointer();

            result = in_isAdditive
                ? result + in_offset
                : in_offset;

            SetGPR(EBaseRegister.RSP, result);

            return result;
        }

        /// <summary>
        /// Reads a buffer from the stack.
        /// </summary>
        /// <param name="in_length">The length of the buffer.</param>
        /// <param name="in_offset">
        ///     The offset of the buffer from the current stack pointer.
        ///     <para>This will read from the current stack pointer if <paramref name="in_offset"/> is zero.</para>
        /// </param>
        public byte[] StackReadBytes(int in_length, nint in_offset = 0)
        {
            ThrowIfThreadNotSuspended();

            return Process.ReadBytes(GetGPR<nint>(EBaseRegister.RSP) + in_offset, in_length);
        }

        /// <summary>
        /// Reads an object from the stack.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to read.</typeparam>
        /// <param name="in_offset">
        ///     The offset of the buffer from the current stack pointer.
        ///     <para>This will read from the current stack pointer if <paramref name="in_offset"/> is zero.</para>
        /// </param>
        public T StackRead<T>(nint in_offset = 0) where T : unmanaged
        {
            ThrowIfThreadNotSuspended();

            return (T)MemoryHelper.ByteArrayToUnmanagedType(StackReadBytes(Marshal.SizeOf<T>(), in_offset), typeof(T))!;
        }

        /// <summary>
        /// Writes a buffer to the stack.
        /// </summary>
        /// <param name="in_data">The buffer to write.</param>
        /// <param name="in_offset">
        ///     The offset of the buffer from the current stack pointer.
        ///     <para>This will write to the current stack pointer if <paramref name="in_offset"/> is zero.</para>
        /// </param>
        /// <returns>A pointer to the buffer on the stack.</returns>
        public nint StackWriteBytes(byte[] in_data, nint in_offset = 0)
        {
            ThrowIfThreadNotSuspended();

            // Allocate aligned stack memory for object.
            var rsp = SetStackPointer(-(in_data.Length.Align(Process.GetPointerSize()) + in_offset));

            Process.WriteBytes(rsp, in_data);

            return rsp;
        }

        /// <summary>
        /// Writes an object to the stack.
        /// </summary>
        /// <param name="in_data">The object to write.</param>
        /// <param name="in_offset">
        ///     The offset of the buffer from the current stack pointer.
        ///     <para>This will write to the current stack pointer if <paramref name="in_offset"/> is zero.</para>
        /// </param>
        /// <returns>A pointer to the unmanaged type on the stack.</returns>
        public nint StackWrite(object in_data, nint in_offset = 0)
        {
            ThrowIfThreadNotSuspended();

            return StackWriteBytes(MemoryHelper.UnmanagedTypeToByteArray(in_data), in_offset);
        }

        /// <summary>
        /// Writes an unmanaged type to the stack.
        /// </summary>
        /// <typeparam name="T">The unmanaged type to write.</typeparam>
        /// <param name="in_data">The value to write.</param>
        /// <param name="in_offset">
        ///     The offset of the buffer from the current stack pointer.
        ///     <para>This will write to the current stack pointer if <paramref name="in_offset"/> is zero.</para>
        /// </param>
        /// <returns>A pointer to the unmanaged type on the stack.</returns>
        public nint StackWrite<T>(T in_data, nint in_offset = 0) where T : unmanaged
        {
            ThrowIfThreadNotSuspended();

            return StackWrite((object)in_data, in_offset);
        }
    }
}

#pragma warning restore CS9124 // Parameter is captured into the state of the enclosing type and its value is also used to initialize a field, property, or event.
#pragma warning restore CA1416 // Validate platform compatibility