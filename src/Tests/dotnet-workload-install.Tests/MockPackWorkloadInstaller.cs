// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.DotNet.Workloads.Workload.Install;
using static Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadResolver;

namespace Microsoft.DotNet.Cli.Workload.Install.Tests
{
    internal class MockPackWorkloadInstaller : IWorkloadPackInstaller
    {
        public IList<PackInfo> InstalledPacks = new List<PackInfo>();
        public IList<PackInfo> RolledBackPacks = new List<PackInfo>();
        public IList<WorkloadId> WorkloadInstallRecord = new List<WorkloadId>();
        public bool GarbageCollectionCalled = false;
        private readonly string FailingWorkload;

        public MockPackWorkloadInstaller(string failingWorkload = null)
        {
            FailingWorkload = failingWorkload;
        }

        public void InstallWorkloadPack(PackInfo packInfo, SdkFeatureBand sdkFeatureBand, bool useOfflineCache = false)
        {
            InstalledPacks.Add(packInfo);
        }

        public void RollBackWorkloadPackInstall(PackInfo packInfo, SdkFeatureBand sdkFeatureBand)
        {
            RolledBackPacks.Add(packInfo);
        }

        public void WriteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            WorkloadInstallRecord.Add(workloadId);
            if (workloadId.ToString().Equals(FailingWorkload))
            {
                throw new Exception($"Failing workload: {workloadId}");
            }
        }

        public void DeleteWorkloadInstallationRecord(WorkloadId workloadId, SdkFeatureBand sdkFeatureBand)
        {
            WorkloadInstallRecord.Remove(workloadId);
        }

        public void GarbageCollectInstalledWorkloadPacks()
        {
            GarbageCollectionCalled = true;
        }

        public InstallationUnit GetInstallationUnit()
        {
            return InstallationUnit.Packs;
        }

        public IWorkloadPackInstaller GetPackInstaller()
        {
            return this;
        }

        public void DownloadToOfflineCache(IEnumerable<string> manifests) => throw new System.NotImplementedException();
        public IEnumerable<SdkFeatureBand> GetFeatureBandsWithInstallationRecords() => throw new System.NotImplementedException();
        public IEnumerable<string> GetInstalledWorkloads(SdkFeatureBand sdkFeatureBand) => throw new System.NotImplementedException();
        public void InstallWorkloadManifest(ManifestId manifestId, ManifestVersion manifestVersion, SdkFeatureBand sdkFeatureBand) => throw new System.NotImplementedException();
        public IWorkloadInstaller GetWorkloadInstaller() => throw new NotImplementedException();
    }
}
