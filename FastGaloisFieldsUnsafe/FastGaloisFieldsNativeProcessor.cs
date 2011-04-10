using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using FastGaloisFields;
using System.Runtime.InteropServices;

namespace FastGaloisFieldsUnsafe
{
    public unsafe sealed class FastGaloisFieldsNativeProcessor : IFastGaloisFieldsProcessor
    {
        [DllImport("FastGaloisFieldsNative.dll", EntryPoint = "?Initialize@@YAXXZ")]
        public static extern void Initialize();

        [DllImport("FastGaloisFieldsNative.dll", EntryPoint = "?Multiply@@YAGGG@Z")]
        public static extern ushort MultiplyNative(ushort a, ushort b);

        [DllImport("FastGaloisFieldsNative.dll", EntryPoint = "?Divide@@YAGGG@Z")]
        public static extern ushort DivideNative(ushort a, ushort b);

        [DllImport("FastGaloisFieldsNative.dll", EntryPoint = "?Pow@@YAGGG@Z")]
        public static extern ushort PowNative(ushort a, ushort b);

        [DllImport("FastGaloisFieldsNative.dll", EntryPoint = "?Add@@YAGGG@Z")]
        public static extern ushort AddNative(ushort a, ushort b);

        [DllImport("FastGaloisFieldsNative.dll", EntryPoint = "?Minus@@YAGGG@Z")]
        public static extern ushort MinusNative(ushort a, ushort b);

        [DllImport("FastGaloisFieldsNative.dll", EntryPoint = "?GetLimit@@YAGXZ")]
        public static extern uint GetLimit();

        [DllImport("FastGaloisFieldsNative.dll", EntryPoint = "?GetAntiLog@@YAPEAGXZ")]
        public static extern ushort[] GetAntiLog();

        [DllImport("FastGaloisFieldsNative.dll", EntryPoint = "?GetAntiLogIndex@@YAGH@Z")]
        public static extern ushort GetAntiLog(int index);

        [DllImport("FastGaloisFieldsNative.dll", EntryPoint = "?InternalProcess@@YA_NGIPEAG0HI@Z", CallingConvention=CallingConvention.Cdecl)]
        public static extern bool InternalProcessNative(
            [MarshalAs(UnmanagedType.U2)]
            ushort factor,
            [MarshalAs(UnmanagedType.U4)]
            uint size,
            IntPtr inputbuffer,
            IntPtr outputbuffer,
            [MarshalAs(UnmanagedType.I4)]
            int startIndex,
            [MarshalAs(UnmanagedType.U4)]
            uint length);

        private FastGaloisFieldsNativeProcessor()
        {
            Initialize();
        }

        private static FastGaloisFieldsNativeProcessor instance = null;

        public static IFastGaloisFieldsProcessor GetInstance
        {
            get
            {
                if (instance == null)
                {
                    instance = new FastGaloisFieldsNativeProcessor();
                }

                return instance;
            }
        }

        public ushort Multiply(ushort a, ushort b)
        {
            return MultiplyNative(a,b);
        }

        public ushort Divide(ushort a, ushort b)
        {
            return DivideNative(a, b);
        }

        public ushort Pow(ushort a, ushort b)
        {
            return PowNative(a, b);
        }

        public ushort Add(ushort a, ushort b)
        {
            return AddNative(a, b);
        }

        public ushort Minus(ushort a, ushort b)
        {
            return MinusNative(a, b);
        }

        public bool InternalProcess(ushort factor, uint size, byte[] inputbuffer, byte[] outputbuffer, int startIndex, uint length)
        {
            GCHandle inputHandle = GCHandle.Alloc(inputbuffer, GCHandleType.Pinned);
            IntPtr inputPtr = inputHandle.AddrOfPinnedObject();

            GCHandle outputHandle = GCHandle.Alloc(outputbuffer, GCHandleType.Pinned);
            IntPtr outputPtr = outputHandle.AddrOfPinnedObject();

            return InternalProcessNative(factor, size, inputPtr, outputPtr, startIndex, length);
        }

        public uint limit
        {
            get { return GetLimit(); }
        }

        //public class AntiLog
        //{
        //     public ushort this[int index]
        //     {
        //         get
        //         {
        //             return GetAntiLog(index);
        //         }
        //     }
        //}

        //public AntiLog antilog = new AntiLog();

        public ushort GetAntilog(int index)
        {
            return GetAntiLog(index);
        }
    }
}
