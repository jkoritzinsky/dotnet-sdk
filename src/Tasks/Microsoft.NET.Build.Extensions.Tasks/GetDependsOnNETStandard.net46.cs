﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NETFRAMEWORK
using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace Microsoft.NET.Build.Tasks
{
    public partial class GetDependsOnNETStandard
    {
        private static readonly Guid s_importerGuid = new("7DAC8207-D3AE-4c75-9B67-92801A497D44");

        // This method cross-compiles for desktop to avoid using System.Reflection.Metadata (SRM).
        // We do this because we don't want the following:
        //  - additional size of SRM and its closure
        //  - load / JIT cost of SRM and its closure
        //  - deal with bindingRedirects/unification needed to load SRM's closure.

        internal static bool GetFileDependsOnNETStandard(string filePath)
        {
            // Ported from Microsoft.Build.Tasks.AssemblyInformation
            if (Environment.OSVersion.Platform == PlatformID.Unix || Environment.OSVersion.Platform == PlatformID.MacOSX)
            {
                // on Unix/Mac (mono) use ReflectionOnlyLoadFrom to examine dependencies
                // known issue: since this doesn't create an isolated app domain this will fail if more than one
                // assembly with the same name is analyzed by this task.  The same issue exists in ResolveAssemblyReferences
                // so we are not attempting to fix it here.
                var assembly = Assembly.ReflectionOnlyLoadFrom(filePath);

                foreach(var referencedAssembly in assembly.GetReferencedAssemblies())
                {
                    if (referencedAssembly.Name.Equals(NetStandardAssemblyName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
            else
            {
                // on Windows use CLR's unmanaged metadata API.
                // Create the metadata dispenser and open scope on the source file.
                var filePathAbsolute = Path.GetFullPath(filePath);
                var metadataDispenser = (IMetaDataDispenser)new CorMetaDataDispenser();
                var assemblyImport = (IMetaDataAssemblyImport)metadataDispenser.OpenScope(filePathAbsolute, 0, s_importerGuid);

                var asmRefEnum = IntPtr.Zero;
                var asmRefTokens = new uint[16];
                uint fetched;

                var assemblyMD = new ASSEMBLYMETADATA()
                {
                    rpLocale = IntPtr.Zero,
                    cchLocale = 0,
                    rpProcessors = IntPtr.Zero,
                    cProcessors = 0,
                    rOses = IntPtr.Zero,
                    cOses = 0
                };

                // Ensure the enum handle is closed.
                try
                {
                    // Enum chunks of refs in 16-ref blocks until we run out.
                    do
                    {
                        assemblyImport.EnumAssemblyRefs(
                            ref asmRefEnum,
                            asmRefTokens,
                            (uint)asmRefTokens.Length,
                            out fetched);

                        for (uint i = 0; i < fetched; i++)
                        {
                            // Determine the length of the string to contain the name first.
                            IntPtr hashDataPtr, pubKeyPtr;
                            uint hashDataLength, pubKeyBytes, asmNameLength, flags;
                            assemblyImport.GetAssemblyRefProps(
                                asmRefTokens[i],
                                out pubKeyPtr,
                                out pubKeyBytes,
                                null,
                                0,
                                out asmNameLength,
                                ref assemblyMD,
                                out hashDataPtr,
                                out hashDataLength,
                                out flags);

                            // Allocate assembly name buffer.
                            StringBuilder assemblyNameBuffer = new((int)asmNameLength + 1);

                            // Retrieve the assembly reference properties.
                            assemblyImport.GetAssemblyRefProps(
                                asmRefTokens[i],
                                out pubKeyPtr,
                                out pubKeyBytes,
                                assemblyNameBuffer,
                                (uint)assemblyNameBuffer.Capacity,
                                out asmNameLength,
                                ref assemblyMD,
                                out hashDataPtr,
                                out hashDataLength,
                                out flags);

                            var assemblyName = assemblyNameBuffer.ToString();

                            if (assemblyName.Equals(NetStandardAssemblyName, StringComparison.Ordinal))
                            {
                                return true;
                            }

                            if (assemblyName.Equals(SystemRuntimeAssemblyName, StringComparison.Ordinal))
                            {
                                var assemblyVersion = new Version(assemblyMD.usMajorVersion, assemblyMD.usMinorVersion, assemblyMD.usBuildNumber, assemblyMD.usRevisionNumber);

                                if (assemblyVersion >= SystemRuntimeMinVersion)
                                {
                                    return true;
                                }
                            }
                        }
                    } while (fetched > 0);
                }
                finally
                {
                    if (asmRefEnum != IntPtr.Zero)
                    {
                        assemblyImport.CloseEnum(asmRefEnum);
                    }

                    if (assemblyImport != null)
                    {
                        Marshal.ReleaseComObject(assemblyImport);
                    }

                    if (metadataDispenser != null)
                    {
                        Marshal.ReleaseComObject(metadataDispenser);
                    }
                }
            }
            return false;
        }

        #region Interop
        
        [ComImport]
        [Guid("E5CB7A31-7512-11d2-89CE-0080C792E5D8")]
        [TypeLibType(TypeLibTypeFlags.FCanCreate)]
        [ClassInterface(ClassInterfaceType.None)]
        internal class CorMetaDataDispenser
        {
        }

        [ComImport]
        [Guid("809c652e-7396-11d2-9771-00a0c9b4d50c")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown /*0x0001*/)]
        [TypeLibType(TypeLibTypeFlags.FRestricted /*0x0200*/)]
        internal interface IMetaDataDispenser
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            object DefineScope([In] ref Guid rclsid, [In] uint dwCreateFlags, [In] ref Guid riid);

            [return: MarshalAs(UnmanagedType.Interface)]
            object OpenScope([In][MarshalAs(UnmanagedType.LPWStr)]  string szScope, [In] uint dwOpenFlags, [In] ref Guid riid);

            [return: MarshalAs(UnmanagedType.Interface)]
            object OpenScopeOnMemory([In] IntPtr pData, [In] uint cbData, [In] uint dwOpenFlags, [In] ref Guid riid);
        }
        
        [ComImport]
        [Guid("EE62470B-E94B-424e-9B7C-2F00C9249F93")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface IMetaDataAssemblyImport
        {
            void GetAssemblyProps(uint mdAsm, out IntPtr pPublicKeyPtr, out uint ucbPublicKeyPtr, out uint uHashAlg, StringBuilder strName, uint cchNameIn, out uint cchNameRequired, IntPtr amdInfo, out uint dwFlags);
            void GetAssemblyRefProps(uint mdAsmRef, out IntPtr ppbPublicKeyOrToken, out uint pcbPublicKeyOrToken, StringBuilder? strName, uint cchNameIn, out uint pchNameOut, ref ASSEMBLYMETADATA amdInfo, out IntPtr ppbHashValue, out uint pcbHashValue, out uint pdwAssemblyRefFlags);
            void GetFileProps([In] uint mdFile, StringBuilder strName, uint cchName, out uint cchNameRequired, out IntPtr bHashData, out uint cchHashBytes, out uint dwFileFlags);
            void GetExportedTypeProps();
            void GetManifestResourceProps();
            void EnumAssemblyRefs([In, Out] ref IntPtr phEnum, [MarshalAs(UnmanagedType.LPArray), Out] uint[] asmRefs, uint asmRefCount, out uint iFetched);
            void EnumFiles([In, Out] ref IntPtr phEnum, [MarshalAs(UnmanagedType.LPArray), Out] uint[] fileRefs, uint fileRefCount, out uint iFetched);
            void EnumExportedTypes();
            void EnumManifestResources();
            void GetAssemblyFromScope(out uint mdAsm);
            void FindExportedTypeByName();
            void FindManifestResourceByName();
            // PreserveSig because this method is an exception that 
            // actually returns void, not HRESULT.
            [PreserveSig]
            void CloseEnum([In] IntPtr phEnum);
            void FindAssembliesByName();
        }


        /*
        From cor.h:
            typedef struct
            {
                USHORT      usMajorVersion;         // Major Version.
                USHORT      usMinorVersion;         // Minor Version.
                USHORT      usBuildNumber;          // Build Number.
                USHORT      usRevisionNumber;       // Revision Number.
                LPWSTR      szLocale;               // Locale.
                ULONG       cbLocale;               // [IN/OUT] Size of the buffer in wide chars/Actual size.
                DWORD       *rProcessor;            // Processor ID array.
                ULONG       ulProcessor;            // [IN/OUT] Size of the Processor ID array/Actual # of entries filled in.
                OSINFO      *rOS;                   // OSINFO array.
                ULONG       ulOS;                   // [IN/OUT]Size of the OSINFO array/Actual # of entries filled in.
            } ASSEMBLYMETADATA;
        */
        [StructLayout(LayoutKind.Sequential)]
        internal struct ASSEMBLYMETADATA
        {
            public ushort usMajorVersion;
            public ushort usMinorVersion;
            public ushort usBuildNumber;
            public ushort usRevisionNumber;
            public IntPtr rpLocale;
            public uint cchLocale;
            public IntPtr rpProcessors;
            public uint cProcessors;
            public IntPtr rOses;
            public uint cOses;
        }

        #endregion
    }
}

#endif
