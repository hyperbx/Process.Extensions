using Gee.External.Capstone.X86;
using Keystone;
using ProcessExtensions.Enums;
using ProcessExtensions.Interop.Generic;
using ProcessExtensions.Helpers.Internal;
using ProcessExtensions.Logger;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace ProcessExtensions
{
    public static class Assembly
    {
        private static Dictionary<nint, nint> _hooks = [];

        /// <summary>
        /// Assembles x86-64 assembly code.
        /// </summary>
        /// <param name="in_process">The target process to determine the architecture.</param>
        /// <param name="in_code">The x86-64 assembly code to assemble.</param>
        /// <param name="in_address">The remote address of the first instruction to encode (optional).</param>
        /// <returns>A byte array containing x86-64 bytecode.</returns>
        public static byte[] Assemble(this Process in_process, string in_code, nint in_address = 0)
        {
            if (in_process.HasExited)
                return [];

            in_code = RemoveComments(in_code);

            using (var assembler = new Engine(Keystone.Architecture.X86, in_process.Is64Bit() ? Mode.X64 : Mode.X32) { ThrowOnError = true })
                return assembler.Assemble(in_code, (ulong)in_address).Buffer;
        }

        /// <summary>
        /// Disassembles x86-64 bytecode.
        /// </summary>
        /// <param name="in_process">The target process to determine the architecture.</param>
        /// <param name="in_code">The bytecode to disassemble.</param>
        public static X86Instruction[] Disassemble(this Process in_process, byte[] in_code)
        {
            if (in_process.HasExited)
                return [];

            using (var disassembler = new CapstoneX86Disassembler(X86DisassembleMode.LittleEndian | (in_process.Is64Bit() ? X86DisassembleMode.Bit64 : X86DisassembleMode.Bit32)))
                return disassembler.Disassemble(in_code);
        }

        /// <summary>
        /// Disassembles x86-64 bytecode.
        /// </summary>
        /// <param name="in_process">The target process to determine the architecture.</param>
        /// <param name="in_address">The remote address of the first instruction to decode.</param>
        /// <param name="in_length">The length of the bytecode.</param>
        public static X86Instruction[] Disassemble(this Process in_process, nint in_address, int in_length)
        {
            return in_process.Disassemble(in_process.ReadBytes(in_address, in_length));
        }

        /// <summary>
        /// Reads the address of a subroutine from a <b>call</b> instruction.
        /// </summary>
        /// <param name="in_process">The target process to read from.</param>
        /// <param name="in_address">The remote address of the <b>call</b> instruction.</param>
        /// <returns>A pointer to the subroutine being called.</returns>
        public static nint ReadCall(this Process in_process, nint in_address)
        {
            if (in_process.HasExited || in_address == 0)
                return 0;

            return in_address + in_process.Read<int>(in_address + 0x01) + 0x05;
        }

        /// <summary>
        /// Reads the address being referenced by an <b>lea</b> or <b>mov</b> instruction.
        /// </summary>
        /// <param name="in_process">The target process to read from.</param>
        /// <param name="in_address">The remote address of the <b>lea</b> or <b>mov</b> instruction.</param>
        /// <returns>A pointer to the address being referenced.</returns>
        public static nint ReadEffectiveAddress(this Process in_process, nint in_address)
        {
            if (in_process.HasExited || in_address == 0)
                return 0;

            return in_address + in_process.Read<int>(in_address + 0x03) + 0x07;
        }

        /// <summary>
        /// Reads the opcode from a jump instruction.
        /// </summary>
        /// <param name="in_process">The target process to read from.</param>
        /// <param name="in_address">The remote address of the jump instruction.</param>
        /// <returns>An enum determining what kind of jump is at the specified location.</returns>
        public static EJumpType ReadJumpOpcode(this Process in_process, nint in_address)
        {
            EJumpType result = EJumpType.Unknown;

            if (in_process.HasExited || in_address == 0)
                return result;

            var opcode = in_process.Read<byte>(in_address);

            if ((opcode & 0xF0) == 0x70)
            {
                result = EJumpType.ShortCond;
            }
            else
            {
                switch (opcode)
                {
                    case 0xE3:
                    case 0xEB:
                        result = EJumpType.ShortCond;
                        break;

                    case 0xE9:
                        result = EJumpType.NearJmp;
                        break;

                    case 0x0F:
                        result = EJumpType.NearCond;
                        break;

                    case 0xFF:
                        result = EJumpType.LongJmp;
                        break;
                }
            }

            return result;
        }

        /// <summary>
        /// Reads the address from a jump instruction.
        /// <para>This supports <b>jmp</b> and all other conditional jump types of varying sizes.</para>
        /// </summary>
        /// <param name="in_process">The target address to read from.</param>
        /// <param name="in_address">The remote address of the jump instruction to read.</param>
        /// <returns></returns>
        public static nint ReadJump(this Process in_process, nint in_address)
        {
            if (in_process.HasExited || in_address == 0)
                return 0;

            switch (in_process.ReadJumpOpcode(in_address))
            {
                case EJumpType.ShortCond:
                    return in_address + in_process.Read<byte>(in_address + 0x01) + 0x02;

                case EJumpType.NearJmp:
                    return in_address + in_process.Read<int>(in_address + 0x01) + 0x05;

                case EJumpType.NearCond:
                    return in_address + in_process.Read<int>(in_address + 0x02) + 0x06;

                case EJumpType.LongJmp:
                    return (nint)in_process.Read<long>(in_address + 0x06);
            }

            return 0;
        }

        /// <summary>
        /// Reads a <b>call</b> instruction and then the <b>jmp</b> instruction that it calls to get the location of the real subroutine.
        /// </summary>
        /// <param name="in_process">The target process to read from.</param>
        /// <param name="in_address">The remote address of the <b>call</b> instruction that is calling a thunk method.</param>
        /// <param name="in_offset">The offset of the <b>jmp</b> instruction from the beginning of the thunk (in the event that the thunk passes an argument to the real function).</param>
        /// <returns>A pointer to the real subroutine being called.</returns>
        public static nint ReadThunk(this Process in_process, nint in_address, nint in_offset = 0)
        {
            return in_process.ReadJump(in_process.ReadCall(in_address) + in_offset);
        }

        /// <summary>
        /// Creates a mid-ASM hook for injecting custom code.
        /// <para>A hook will write a <b>jmp</b>/<b>call</b> instruction at the specified address to the provided assembly code.</para>
        /// <para>This instruction takes up <b>5</b> bytes in x86 and <b>14</b> bytes in x86-64.</para>
        /// <para>The instructions that are overwritten by the <b>jmp</b>/<b>call</b> instruction will not be copied over to the hook, please rewrite these manually.</para>
        /// </summary>
        /// <param name="in_process">The target process to inject into.</param>
        /// <param name="in_code">The x86-64 assembly code to assemble.</param>
        /// <param name="in_address">The address to start the hook from.</param>
        /// <param name="in_parameter">The method used to start the hook.</param>
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint)"/> later.</param>
        public static void WriteAsmHook(this Process in_process, string in_code, nint in_address, EHookParameter in_parameter = EHookParameter.Jump, bool in_isPreserved = true)
        {
            if (in_process.HasExited || string.IsNullOrEmpty(in_code) || in_address == 0)
                return;

            // Restore preserved memory before hooking again.
            in_process.RestoreMemory(in_address);

            var is64Bit = in_process.Is64Bit();
            var hookLength = 0;
            var minHookLength = is64Bit ? 14 : 5;
            var retAddr = in_address;

            using (var disassembler = new CapstoneX86Disassembler(X86DisassembleMode.LittleEndian | (is64Bit ? X86DisassembleMode.Bit64 : X86DisassembleMode.Bit32)))
            {
                var buffer = in_process.ReadBytes(in_address, 64);
                var instrs = disassembler.Disassemble(buffer);

                foreach (var instr in instrs)
                {
                    hookLength += instr.Bytes.Length;

                    if (hookLength >= minHookLength)
                        break;
                }

                if (in_isPreserved)
                    in_process.PreserveMemory(in_address, hookLength);

                // Kill existing instructions to safely inject our hook.
                for (nint i = in_address; i < in_address + hookLength; i++)
                    in_process.WriteNop(i);

                retAddr += hookLength;
            }

            using (var assembler = new Engine(Keystone.Architecture.X86, is64Bit ? Mode.X64 : Mode.X32) { ThrowOnError = true })
            {
                var buffer = assembler.Assemble(in_code, (ulong)in_address);
                var length = buffer.Buffer.Length + minHookLength;

                var asmPtr = in_process.WriteBytes(buffer.Buffer);
                var asmEnd = asmPtr + buffer.Buffer.Length;

                _hooks.Add(in_address, asmPtr);
#if DEBUG
                LoggerService.Utility($"Written mid-ASM hook code at 0x{asmPtr:X}.");
#endif
                // Write return instruction.
                if (in_parameter == EHookParameter.Jump)
                {
                    retAddr = is64Bit
                        ? retAddr
                        : retAddr - (asmEnd + 5);

                    var retBuffer = new byte[minHookLength];
                    var retAddrBuffer = MemoryHelper.UnmanagedTypeToByteArray(retAddr);

                    if (is64Bit)
                    {
                        retBuffer[0] = 0xFF;
                        retBuffer[1] = 0x25;

                        Array.Copy(retAddrBuffer, 0, retBuffer, 6, 8);
                    }
                    else
                    {
                        retBuffer[0] = 0xE9;

                        Array.Copy(retAddrBuffer, 0, retBuffer, 1, 4);
                    }

                    in_process.WriteBytes(asmEnd, retBuffer);
                }
                else
                {
                    in_process.Write<byte>(asmEnd, 0xC3);
                }

                // Write jump instruction.
                {
                    asmPtr = is64Bit
                        ? asmPtr
                        : asmPtr - (in_address + 5);

                    var jmpBuffer = new byte[minHookLength];
                    var jmpAddrBuffer = MemoryHelper.UnmanagedTypeToByteArray(asmPtr);

                    if (is64Bit)
                    {
                        jmpBuffer[0] = 0xFF;
                        jmpBuffer[1] = (byte)(in_parameter == EHookParameter.Jump ? 0x25 : 0x15);

                        Array.Copy(jmpAddrBuffer, 0, jmpBuffer, 6, 8);
                    }
                    else
                    {
                        jmpBuffer[0] = (byte)(in_parameter == EHookParameter.Jump ? 0xE9 : 0xE8);

                        Array.Copy(jmpAddrBuffer, 0, jmpBuffer, 1, 4);
                    }

                    in_process.WriteBytes(in_address, jmpBuffer);
                }
            }
        }

        /// <summary>
        /// Removes a mid-ASM hook.
        /// </summary>
        /// <param name="in_process">The target process the hook is in.</param>
        /// <param name="in_address">The remote address the hook was created at.</param>
        public static void RemoveAsmHook(this Process in_process, nint in_address)
        {
            if (in_process.HasExited || in_address == 0 || !_hooks.TryGetValue(in_address, out var out_hookAddr))
                return;

            in_process.RestoreMemory(in_address);
            in_process.Free(out_hookAddr);
        }

        /// <summary>
        /// Replaces a conditional jump instruction with <b>jmp</b>.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_address">The remote address of the conditional jump to replace.</param>
        public static void WriteForceJump(this Process in_process, nint in_address)
        {
            if (in_process.HasExited || in_address == 0)
                return;

            switch (in_process.ReadJumpOpcode(in_address))
            {
                case EJumpType.ShortCond:
                    in_process.WriteProtected<byte>(in_address, 0xEB);
                    break;

                case EJumpType.NearCond:
                    in_process.WriteProtected<byte>(in_address, 0xE9);
                    in_process.WriteProtected<int>(in_address + 0x01, in_process.Read<int>(in_address + 0x02) + 0x01);
                    break;
            }
        }

        /// <summary>
        /// Writes a no-operation (NOP) instruction.
        /// </summary>
        /// <param name="in_process">The target process to write to.</param>
        /// <param name="in_address">The address to write the instruction.</param>
        /// <param name="in_count">The amount of instructions to write.</param>
        /// <param name="in_isPreserved">Determines whether the original code will be preserved so it can be restored using <see cref="MemoryPreserver.RestoreMemory(Process, nint)"/> later.</param>
        public static void WriteNop(this Process in_process, nint in_address, int in_count = 1, bool in_isPreserved = true)
        {
            if (in_process.HasExited || in_address == 0)
                return;

            ArgumentOutOfRangeException.ThrowIfLessThan(in_count, 1);

            if (in_isPreserved)
                in_process.PreserveMemory(in_address, in_count);

            for (int i = 0; i < in_count; i++)
                in_process.WriteProtected<byte>(in_address + i, 0x90);
        }

        /// <summary>
        /// Gets the register that will be used to store an argument.
        /// </summary>
        /// <param name="in_index">The index of the associated register.</param>
        /// <returns>The register associated with the input index.</returns>
        public static EBaseRegister GetBaseRegisterByArgIndex(int in_index)
        {
            return in_index switch
            {
                0 => EBaseRegister.RCX,
                1 => EBaseRegister.RDX,
                2 => EBaseRegister.R8,
                3 => EBaseRegister.R9,
                _ => EBaseRegister.RAX // undefined
            };
        }

        /// <summary>
        /// Gets the largest register supported by the process architecture.
        /// </summary>
        /// <param name="in_process">The target process to determine the architecture.</param>
        /// <param name="in_baseRegister">The base register to expand (or shrink) to full width.</param>
        public static ERegister GetFullWidthRegister(this Process in_process, EBaseRegister in_baseRegister)
        {
            if (in_process.HasExited)
                return ERegister.RAX;

            return in_process.GetRegisterBySize(in_baseRegister, in_process.Is64Bit() ? 8 : 4);
        }

        /// <summary>
        /// Gets the correct opcode for the value of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The data type to get the <b>mov</b> opcode for.</typeparam>
        /// <returns><b>movss</b> if <typeparamref name="T"/> is a <see cref="float"/>, <b>movsd</b> if <typeparamref name="T"/> is a <see cref="double"/>; otherwise, returns <b>mov</b>.</returns>
        public static string GetMovOpcodeByType<T>() where T : unmanaged
        {
            if (typeof(T).Equals(typeof(float)))
            {
                return "movss";
            }
            else if (typeof(T).Equals(typeof(double)))
            {
                return "movsd";
            }

            return "mov";
        }

        /// <summary>
        /// Gets the correct named register that fits the input size.
        /// </summary>
        /// <param name="in_process">The target process to determine the architecture.</param>
        /// <param name="in_baseRegister">The full width register to determine which register to return.</param>
        /// <param name="in_size">The size of the object being fit into the register</param>
        public static ERegister GetRegisterBySize(this Process in_process, EBaseRegister in_baseRegister, int in_size)
        {
            if (in_process.HasExited)
                return ERegister.RAX;

            var is64Bit = in_process.Is64Bit();

            if (in_size > 4)
            {
                if (is64Bit)
                    return (ERegister)in_baseRegister;

                return in_baseRegister switch
                {
                    EBaseRegister.RAX => ERegister.EAX,
                    EBaseRegister.RBX => ERegister.EBX,
                    EBaseRegister.RCX => ERegister.ECX,
                    EBaseRegister.RDX => ERegister.EDX,
                    EBaseRegister.RSI => ERegister.ESI,
                    EBaseRegister.RDI => ERegister.EDI,
                    EBaseRegister.RBP => ERegister.EBP,
                    EBaseRegister.RSP => ERegister.ESP,
                    _                 => ERegister.EAX
                };
            }

            return in_baseRegister switch
            {
                EBaseRegister.RAX => in_size > 2 ? ERegister.EAX  : in_size > 1 ? ERegister.AX   : ERegister.AL,
                EBaseRegister.RBX => in_size > 2 ? ERegister.EBX  : in_size > 1 ? ERegister.BX   : ERegister.BL,
                EBaseRegister.RCX => in_size > 2 ? ERegister.ECX  : in_size > 1 ? ERegister.CX   : ERegister.CL,
                EBaseRegister.RDX => in_size > 2 ? ERegister.EDX  : in_size > 1 ? ERegister.DX   : ERegister.DL,
                EBaseRegister.RSI => in_size > 2 ? ERegister.ESI  : in_size > 1 ? ERegister.SI   : ERegister.SIL,
                EBaseRegister.RDI => in_size > 2 ? ERegister.EDI  : in_size > 1 ? ERegister.DI   : ERegister.DIL,
                EBaseRegister.RBP => in_size > 2 ? ERegister.EBP  : in_size > 1 ? ERegister.BP   : ERegister.BPL,
                EBaseRegister.RSP => in_size > 2 ? ERegister.ESP  : in_size > 1 ? ERegister.SP   : ERegister.SPL,
                EBaseRegister.R8  => in_size > 2 ? ERegister.R8D  : in_size > 1 ? ERegister.R8W  : ERegister.R8B,
                EBaseRegister.R9  => in_size > 2 ? ERegister.R9D  : in_size > 1 ? ERegister.R9W  : ERegister.R9B,
                EBaseRegister.R10 => in_size > 2 ? ERegister.R10D : in_size > 1 ? ERegister.R10W : ERegister.R10B,
                EBaseRegister.R11 => in_size > 2 ? ERegister.R11D : in_size > 1 ? ERegister.R11W : ERegister.R11B,
                EBaseRegister.R12 => in_size > 2 ? ERegister.R12D : in_size > 1 ? ERegister.R12W : ERegister.R12B,
                EBaseRegister.R13 => in_size > 2 ? ERegister.R13D : in_size > 1 ? ERegister.R13W : ERegister.R13B,
                EBaseRegister.R14 => in_size > 2 ? ERegister.R14D : in_size > 1 ? ERegister.R14W : ERegister.R14B,
                EBaseRegister.R15 => in_size > 2 ? ERegister.R15D : in_size > 1 ? ERegister.R15W : ERegister.R15B,
                _                 => is64Bit     ? ERegister.RAX  : ERegister.EAX
            };
        }

        /// <summary>
        /// Gets the correct opcode for the value of <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The data type to get the register for.</typeparam>
        /// <param name="in_process">The target process to determine the architecture.</param>
        /// <returns><see cref="ERegister.XMM0"/> if <typeparamref name="T"/> is a <see cref="float"/> or <see cref="double"/>; otherwise, returns <see cref="ERegister.RAX"/>.</returns>
        public static ERegister GetReturnRegisterByType<T>(this Process in_process) where T : unmanaged
        {
            if (in_process.HasExited)
                return ERegister.RAX;

            if (typeof(T).Equals(typeof(float)) || typeof(T).Equals(typeof(double)))
                return ERegister.XMM0;

            var size = typeof(T).Name.Contains("UnmanagedPointer")
                ? in_process.Is64Bit() ? 8 : 4
                : Marshal.SizeOf<T>();

            return in_process.GetRegisterBySize(EBaseRegister.RAX, size);
        }

        private static string RemoveComments(string in_code)
        {
            var lines = in_code.Split(new[] { Environment.NewLine }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                int commentIndex = lines[i].IndexOf(';');

                if (commentIndex >= 0)
                    lines[i] = lines[i][..commentIndex].TrimEnd();
            }

            return string.Join(Environment.NewLine, lines);
        }
    }
}
