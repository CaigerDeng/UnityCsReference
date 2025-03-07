// Unity C# reference source
// Copyright (c) Unity Technologies. For terms of use, see
// https://unity3d.com/legal/licenses/Unity_Reference_Only_License

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Bindings;
using UnityEngineInternal;
using uei = UnityEngine.Internal;
using Object = UnityEngine.Object;
using UnityEngine.Scripting;
using UnityEditor.Experimental;
using UnityEngine.Internal;
using UnityEditor.AssetImporters;

namespace UnityEditor
{
    public enum RemoveAssetOptions
    {
        MoveAssetToTrash = 0,
        DeleteAssets = 2
    }

    // Subset of C++ UpdateAssetOptions in AssetDatabaseStructs.h
    [Flags]
    public enum ImportAssetOptions
    {
        Default                     = 0,       // Default import options.
        ForceUpdate                 = 1 <<  0, // User initiated asset import.
        ForceSynchronousImport      = 1 <<  3, // Import all assets synchronously.
        ImportRecursive             = 1 <<  8, // When a folder is imported, import all its contents as well.
        DontDownloadFromCacheServer = 1 << 13, // Force a full reimport but don't download the assets from the cache server.
        ForceUncompressedImport     = 1 << 14, // Forces asset import as uncompressed for edition facilities.
    }

    public enum StatusQueryOptions
    {
        ForceUpdate         = 0, // Always ask version control for the true status of the file and wait for the response. Recommended for operations that will open a file for edit, or revert, or update a file from version control where you need to know the status of the file accurately.
        UseCachedIfPossible = 1, // Use the cached status of the asset in version control. The version control system will be queried for the first request and then periodically for subsequent requests. Cached status can be queried very quickly, so is recommended for any UI operations where accuracy is not strictly necessary.
        UseCachedAsync = 2, // Use the cached status of the asset in version control. Similar to UseCachedIfPossible, except that it doesn't await a response and will submit a query and return immediately if no cached status is available.
    }

    public enum ForceReserializeAssetsOptions
    {
        ReserializeAssets = 1 << 0,
        ReserializeMetadata = 1 << 1,
        ReserializeAssetsAndMetadata = ReserializeAssets | ReserializeMetadata
    }

    public enum AssetPathToGUIDOptions
    {
        IncludeRecentlyDeletedAssets = 0, // Return a GUID if an asset has been recently deleted.
        OnlyExistingAssets = 1, // Return a GUID only if the asset exists on disk.
    }

    internal enum ImportPackageOptions
    {
        Default = 0,
        NoGUI = 1 << 0,
        ImportDelayed = 1 << 1
    }

    // keep in sync with AssetDatabasePreventExecutionChecks in AssetDatabasePreventExecution.h
    internal enum AssetDatabasePreventExecution
    {
        kNoAssetDatabaseRestriction = 0,
        kImportingAsset = 1 << 0,
        kImportingInWorkerProcess = 1 << 1,
        kPreventCustomDependencyChanges = 1 << 2,
        kGatheringDependenciesFromSourceFile = 1 << 3,
        kPreventForceReserializeAssets = 1 << 4,
        kDomainBackup = 1 << 5,
    }

    public struct CacheServerConnectionChangedParameters
    {
    }

    [RequiredByNativeCode]
    internal class AssetDatabaseLoadOperationHelper
    {
        // When the load operation completes this is invoked to hold the result so that it doesn't
        // get garbage collected
        [RequiredByNativeCode]
        public static void SetAssetDatabaseLoadObjectResult(AssetDatabaseLoadOperation op, UnityEngine.Object result)
        {
            op.m_Result = result;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    [RequiredByNativeCode]
    public class AssetDatabaseLoadOperation : AsyncOperation
    {
        internal Object m_Result;
        public UnityEngine.Object LoadedObject { get { return m_Result; } }
    }

    [NativeHeader("Modules/AssetDatabase/Editor/Public/AssetDatabase.h")]
    [NativeHeader("Modules/AssetDatabase/Editor/Public/AssetDatabaseUtility.h")]
    [NativeHeader("Modules/AssetDatabase/Editor/ScriptBindings/AssetDatabase.bindings.h")]
    [NativeHeader("Runtime/Core/PreventExecutionInState.h")]
    [NativeHeader("Modules/AssetDatabase/Editor/Public/AssetDatabasePreventExecution.h")]
    [NativeHeader("Editor/Src/PackageUtility.h")]
    [NativeHeader("Editor/Src/VersionControl/VC_bindings.h")]
    [NativeHeader("Editor/Src/Application/ApplicationFunctions.h")]
    [StaticAccessor("AssetDatabaseBindings", StaticAccessorType.DoubleColon)]
    public partial class AssetDatabase
    {
        extern internal static bool CanGetAssetMetaInfo(string path);
        extern internal static void RegisterAssetFolder(string path, bool immutable, string guid);
        extern internal static void UnregisterAssetFolder(string path);

        // used by integration tests
        extern internal static void RegisterRedirectedAssetFolder(string mountPoint, string folder, string physicalPath, bool immutable, string guid);
        extern internal static void UnregisterRedirectedAssetFolder(string mountPoint, string folder);

        // This will return all registered roots, i.e. Assets/, Packages/** (all registered package roots), Workspaces/, etc.
        [FreeFunction("AssetDatabase::GetAssetRootFolders")]
        extern internal static string[] GetAssetRootFolders();

        // returns true if the folder is known by the asset database
        // rootFolder is true if the path is a registered root folder
        // immutable is true when the root of the path was registered with the immutable flag (e.g. shared package)
        // asset folders marked immutable are not modified by the asset database
        extern internal static bool GetAssetFolderInfo(string path, out bool rootFolder, out bool immutable);

        public static bool Contains(Object obj) { return Contains(obj.GetInstanceID()); }
        extern public static bool Contains(int instanceID);

        extern public static string CreateFolder(string parentFolder, string newFolderName);

        public static bool IsMainAsset(Object obj) { return IsMainAsset(obj.GetInstanceID()); }
        [FreeFunction("AssetDatabase::IsMainAsset")]
        extern public static bool IsMainAsset(int instanceID);

        public static bool IsSubAsset(Object obj) { return IsSubAsset(obj.GetInstanceID()); }
        [FreeFunction("AssetDatabase::IsSubAsset")]
        extern public static bool IsSubAsset(int instanceID);

        public static bool IsForeignAsset(Object obj)
        {
            if (obj == null)
                throw new ArgumentNullException("obj is null");
            return IsForeignAsset(obj.GetInstanceID());
        }

        extern public static bool IsForeignAsset(int instanceID);

        public static bool IsNativeAsset(Object obj)
        {
            if (obj == null)
                throw new ArgumentNullException("obj is null");
            return IsNativeAsset(obj.GetInstanceID());
        }

        extern public static bool IsNativeAsset(int instanceID);

        [FreeFunction()]
        extern public static string GetCurrentCacheServerIp();

        extern public static string GenerateUniqueAssetPath(string path);

        [FreeFunction("AssetDatabase::StartAssetImporting")]
        [PreventExecutionInState(AssetDatabasePreventExecution.kImportingInWorkerProcess, PreventExecutionSeverity.PreventExecution_ManagedException)]
        [PreventExecutionInState(AssetDatabasePreventExecution.kImportingAsset, PreventExecutionSeverity.PreventExecution_Error)]
        extern public static void StartAssetEditing();

        [FreeFunction("AssetDatabase::StopAssetImporting")]
        [PreventExecutionInState(AssetDatabasePreventExecution.kImportingInWorkerProcess, PreventExecutionSeverity.PreventExecution_ManagedException)]
        [PreventExecutionInState(AssetDatabasePreventExecution.kImportingAsset, PreventExecutionSeverity.PreventExecution_Error)]
        extern public static void StopAssetEditing();

        // A class used for Starting/Stopping asset editing. Let's a user start/stop using RAII
        public class AssetEditingScope : IDisposable
        {
            private bool disposed = false;

            public AssetEditingScope()
            {
                AssetDatabase.StartAssetEditing();
            }

            ~AssetEditingScope()
            {
                Dispose(false);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool disposing)
            {
                // We've already been disposed.
                if (this.disposed)
                    return;

                // disposing is only false when the user forgot to Dispose() it properly
                // so we should warn them about not properly disposing it as it will freeze the editor.
                if (!disposing) {
                    // It would be cool to inform the user about where new AssetEditingScope was called,
                    // but I'm unsure how to do that correctly/efficiently, so we'll just warn the user.
                    Debug.LogWarning(
                        "AssetEditingScope.Dispose() was never called on an instance of AssetEditingScope. " +
                        "This could freeze the editor for a short while. Check out the documentation for more info."
                    );
                } else {
                    // StopAssetEditing isn't threadsafe, so we don't call it from the finalizer (as that is in a different thread)
                    AssetDatabase.StopAssetEditing();
                }
            }
        }

        [FreeFunction("AssetDatabase::UnloadAllFileStreams")]
        extern public static void ReleaseCachedFileHandles();

        extern public static string ValidateMoveAsset(string oldPath, string newPath);
        extern public static string MoveAsset(string oldPath, string newPath);
        [NativeThrows]
        extern public static string ExtractAsset(Object asset, string newPath);
        extern public static string RenameAsset(string pathName, string newName);
        extern public static bool MoveAssetToTrash(string path);

        extern private static bool DeleteAssetsCommon(string[] paths, object outFailedPaths, bool moveAssetsToTrash);

        public static bool MoveAssetsToTrash(string[] paths, List<string> outFailedPaths)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));
            if (outFailedPaths == null)
                throw new ArgumentNullException(nameof(outFailedPaths));
            return DeleteAssetsCommon(paths, outFailedPaths, true);
        }

        extern public static bool DeleteAsset(string path);

        public static bool DeleteAssets(string[] paths, List<string> outFailedPaths)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));
            if (outFailedPaths == null)
                throw new ArgumentNullException(nameof(outFailedPaths));
            return DeleteAssetsCommon(paths, outFailedPaths, false);
        }

        [uei.ExcludeFromDocs] public static void ImportAsset(string path) { ImportAsset(path, ImportAssetOptions.Default); }
        extern public static void ImportAsset(string path, [uei.DefaultValue("ImportAssetOptions.Default")] ImportAssetOptions options);
        extern public static bool CopyAsset(string path, string newPath);
        extern public static bool WriteImportSettingsIfDirty(string path);
        [NativeThrows]
        extern public static string[] GetSubFolders([NotNull] string path);

        [FreeFunction("AssetDatabase::IsFolderAsset")]
        extern public static bool IsValidFolder(string path);

        [NativeThrows]
        [PreventExecutionInState(AssetDatabasePreventExecution.kGatheringDependenciesFromSourceFile, PreventExecutionSeverity.PreventExecution_ManagedException, "Assets may not be created during gathering of import dependencies")]
        [PreventExecutionInState(AssetDatabasePreventExecution.kImportingAsset, PreventExecutionSeverity.PreventExecution_Warning, "AssetDatabase.CreateAsset() was called as part of running an import. Please make sure this function is not called from ScriptedImporters or PostProcessors, as it is a source of non-determinism and will be disallowed in a forthcoming release.")]
        extern public static void CreateAsset([NotNull] Object asset, string path);
        [NativeThrows]
        extern static internal void CreateAssetFromObjects(Object[] assets, string path);
        [NativeThrows]
        extern public static void AddObjectToAsset([NotNull] Object objectToAdd, string path);

        static public void AddObjectToAsset(Object objectToAdd, Object assetObject) { AddObjectToAsset_Obj(objectToAdd, assetObject); }
        [NativeThrows]
        extern private static void AddObjectToAsset_Obj([NotNull] Object newAsset, [NotNull] Object sameAssetFile);

        extern static internal void AddInstanceIDToAssetWithRandomFileId(int instanceIDToAdd, Object assetObject, bool hide);
        [NativeThrows]
        extern public static void SetMainObject([NotNull] Object mainObject, string assetPath);
        extern public static string GetAssetPath(Object assetObject);

        public static string GetAssetPath(int instanceID) { return GetAssetPathFromInstanceID(instanceID); }
        [FreeFunction("::GetAssetPathFromInstanceID")]
        extern private static string GetAssetPathFromInstanceID(int instanceID);

        extern internal static int GetMainAssetInstanceID(string assetPath);
        extern internal static int GetMainAssetOrInProgressProxyInstanceID(string assetPath);

        [FreeFunction("::GetAssetOrScenePath")]
        extern public static string GetAssetOrScenePath(Object assetObject);

        [FreeFunction("AssetDatabase::TextMetaFilePathFromAssetPath")]
        extern public static string GetTextMetaFilePathFromAssetPath(string path);

        [FreeFunction("AssetDatabase::AssetPathFromTextMetaFilePath")]
        extern public static string GetAssetPathFromTextMetaFilePath(string path);

        [NativeThrows]
        [TypeInferenceRule(TypeInferenceRules.TypeReferencedBySecondArgument)]
        [PreventExecutionInState(AssetDatabasePreventExecution.kGatheringDependenciesFromSourceFile, PreventExecutionSeverity.PreventExecution_ManagedException, "Assets may not be loaded while dependencies are being gathered, as these assets may not have been imported yet.")]
        [PreventExecutionInState(AssetDatabasePreventExecution.kDomainBackup, PreventExecutionSeverity.PreventExecution_ManagedException, "Assets may not be loaded while domain backup is running, as this will change the underlying state.")]
        extern public static Object LoadAssetAtPath(string assetPath, Type type);

        public static T LoadAssetAtPath<T>(string assetPath) where T : Object
        {
            return (T)LoadAssetAtPath(assetPath, typeof(T));
        }

        [PreventExecutionInState(AssetDatabasePreventExecution.kGatheringDependenciesFromSourceFile, PreventExecutionSeverity.PreventExecution_ManagedException, "Assets may not be loaded while dependencies are being gathered, as these assets may not have been imported yet.")]
        extern public static Object LoadMainAssetAtPath(string assetPath);

        [FreeFunction("AssetDatabase::GetMainAssetObject")]
        [PreventExecutionInState(AssetDatabasePreventExecution.kGatheringDependenciesFromSourceFile, PreventExecutionSeverity.PreventExecution_ManagedException, "Assets may not be loaded while dependencies are being gathered, as these assets may not have been imported yet.")]
        extern internal static Object LoadMainAssetAtGUID(GUID assetGUID);

        [FreeFunction("AssetDatabase::InstanceIDsToGUIDs")]
        extern internal static void InstanceIDsToGUIDs(IntPtr instanceIDsPtr, IntPtr guidsPtr, int len);

        public unsafe static void InstanceIDsToGUIDs(NativeArray<int> instanceIDs, NativeArray<GUID> guidsOut)
        {
            if (!instanceIDs.IsCreated)
                throw new ArgumentException("NativeArray is uninitialized", nameof(instanceIDs));

            if (!guidsOut.IsCreated)
                throw new ArgumentException("NativeArray is uninitialized", nameof(guidsOut));

            if (instanceIDs.Length != guidsOut.Length)
                throw new ArgumentException("instanceIDs and guidsOut size mismatch!");

            InstanceIDsToGUIDs((IntPtr)instanceIDs.GetUnsafeReadOnlyPtr(), (IntPtr)guidsOut.GetUnsafePtr(), instanceIDs.Length);
        }

        extern public static System.Type GetMainAssetTypeAtPath(string assetPath);

        extern public static System.Type GetTypeFromPathAndFileID(string assetPath, long localIdentifierInFile);
        extern public static bool IsMainAssetAtPathLoaded(string assetPath);

        [PreventExecutionInState(AssetDatabasePreventExecution.kGatheringDependenciesFromSourceFile, PreventExecutionSeverity.PreventExecution_ManagedException, "Assets may not be loaded while dependencies are being gathered, as these assets may not have been imported yet.")]
        extern public static Object[] LoadAllAssetRepresentationsAtPath(string assetPath);

        [PreventExecutionInState(AssetDatabasePreventExecution.kGatheringDependenciesFromSourceFile, PreventExecutionSeverity.PreventExecution_ManagedException, "Assets may not be loaded while dependencies are being gathered, as these assets may not have been imported yet.")]
        extern public static Object[] LoadAllAssetsAtPath(string assetPath);
        extern public static string[] GetAllAssetPaths();

        [System.Obsolete("Please use AssetDatabase.Refresh instead", true)]
        public static void RefreshDelayed(ImportAssetOptions options) {}

        [System.Obsolete("Please use AssetDatabase.Refresh instead", true)]
        public static void RefreshDelayed() {}

        [uei.ExcludeFromDocs] public static void Refresh() { Refresh(ImportAssetOptions.Default); }

        [PreventExecutionInState(AssetDatabasePreventExecution.kImportingInWorkerProcess, PreventExecutionSeverity.PreventExecution_ManagedException)]
        [PreventExecutionInState(AssetDatabasePreventExecution.kImportingAsset, PreventExecutionSeverity.PreventExecution_Error)]
        extern public static void Refresh([uei.DefaultValue("ImportAssetOptions.Default")] ImportAssetOptions options);

        [FreeFunction("::CanOpenAssetInEditor")]
        extern public static bool CanOpenAssetInEditor(int instanceID);

        [uei.ExcludeFromDocs] public static bool OpenAsset(int instanceID) { return OpenAsset(instanceID, -1); }
        public static bool OpenAsset(int instanceID, [uei.DefaultValue("-1")] int lineNumber) { return OpenAsset(instanceID, lineNumber, -1); }
        [FreeFunction("::OpenAsset")]
        extern public static bool OpenAsset(int instanceID, int lineNumber, int columnNumber);

        [uei.ExcludeFromDocs] public static bool OpenAsset(Object target) { return OpenAsset(target, -1); }
        public static bool OpenAsset(Object target, [uei.DefaultValue("-1")] int lineNumber) { return OpenAsset(target, lineNumber, -1); }

        static public bool OpenAsset(Object target, int lineNumber, int columnNumber)
        {
            if (target)
                return OpenAsset(target.GetInstanceID(), lineNumber, columnNumber);
            else
                return false;
        }

        static public bool OpenAsset(Object[] objects)
        {
            bool allOpened = true;
            foreach (Object obj in objects)
                if (!OpenAsset(obj))
                    allOpened = false;
            return allOpened;
        }

        extern internal static string GUIDToAssetPath_Internal(GUID guid);
        extern internal static GUID AssetPathToGUID_Internal(string path);

        public static string GUIDToAssetPath(string guid)
        {
            return GUIDToAssetPath_Internal(new GUID(guid));
        }

        public static string GUIDToAssetPath(GUID guid)
        {
            return GUIDToAssetPath_Internal(guid);
        }

        public static GUID GUIDFromAssetPath(string path)
        {
            return AssetPathToGUID_Internal(path);
        }

        public static string AssetPathToGUID(string path)
        {
            return AssetPathToGUID(path, AssetPathToGUIDOptions.IncludeRecentlyDeletedAssets);
        }

        public static string AssetPathToGUID(string path, [DefaultValue("AssetPathToGUIDOptions.IncludeRecentlyDeletedAssets")] AssetPathToGUIDOptions options)
        {
            GUID guid;

            switch (options)
            {
                case AssetPathToGUIDOptions.OnlyExistingAssets:
                    guid = GUIDFromExistingAssetPath(path);
                    break;
                default:
                    guid = AssetPathToGUID_Internal(path);
                    break;
            }

            return guid.Empty() ? "" : guid.ToString();
        }

        extern public static bool AssetPathExists(string path);

        extern public static Hash128 GetAssetDependencyHash(GUID guid);

        public static Hash128 GetAssetDependencyHash(string path)
        {
            return GetAssetDependencyHash(GUIDFromAssetPath(path));
        }

        extern internal static Hash128 GetSourceAssetFileHash(string guid);
        extern internal static Hash128 GetSourceAssetMetaFileHash(string guid);

        [FreeFunction("AssetDatabase::SaveAssets")]
        [PreventExecutionInState(AssetDatabasePreventExecution.kImportingInWorkerProcess, PreventExecutionSeverity.PreventExecution_ManagedException)]
        [PreventExecutionInState(AssetDatabasePreventExecution.kImportingAsset, PreventExecutionSeverity.PreventExecution_Error)]
        extern public static void SaveAssets();

        [FreeFunction("AssetDatabase::SaveAssetIfDirty")]
        extern public static void SaveAssetIfDirty(GUID guid);

        public static void SaveAssetIfDirty(Object obj)
        {
            string guidString;
            long localID;

            if (TryGetGUIDAndLocalFileIdentifier(obj.GetInstanceID(), out guidString, out localID))
                SaveAssetIfDirty(new GUID(guidString));
        }

        extern public static Texture GetCachedIcon(string path);
        extern public static void SetLabels(Object obj, string[] labels);
        extern private static void GetAllLabelsImpl(object labelsList, object scoresList);

        internal static Dictionary<string, float> GetAllLabels()
        {
            var labelsList = new List<string>();
            var scoresList = new List<float>();
            GetAllLabelsImpl(labelsList, scoresList);

            Dictionary<string, float> res = new Dictionary<string, float>(labelsList.Count);
            for (int i = 0; i < labelsList.Count; ++i)
            {
                res[labelsList[i]] = scoresList[i];
            }
            return res;
        }

        [FreeFunction("AssetDatabase::GetLabels")]
        extern private static string[] GetLabelsInternal(GUID guid);
        public static string[] GetLabels(GUID guid)
        {
            return GetLabelsInternal(guid);
        }

        extern public static string[] GetLabels(Object obj);
        extern public static void ClearLabels(Object obj);

        extern public static string[] GetAllAssetBundleNames();

        [System.Obsolete("Method GetAssetBundleNames has been deprecated. Use GetAllAssetBundleNames instead.")] public string[] GetAssetBundleNames() { return GetAllAssetBundleNames(); }

        extern internal static string[] GetAllAssetBundleNamesWithoutVariant();
        extern internal static string[] GetAllAssetBundleVariants();
        extern public static string[] GetUnusedAssetBundleNames();

        [FreeFunction("AssetDatabase::RemoveAssetBundleByName")]
        extern public static bool RemoveAssetBundleName(string assetBundleName, bool forceRemove);

        [FreeFunction("AssetDatabase::RemoveUnusedAssetBundleNames")]
        extern public static void RemoveUnusedAssetBundleNames();

        extern public static string[] GetAssetPathsFromAssetBundle(string assetBundleName);
        extern public static string[] GetAssetPathsFromAssetBundleAndAssetName(string assetBundleName, string assetName);
        [NativeThrows]
        extern public static string GetImplicitAssetBundleName(string assetPath);
        [NativeThrows]
        extern public static string GetImplicitAssetBundleVariantName(string assetPath);
        extern public static string[] GetAssetBundleDependencies(string assetBundleName, bool recursive);

        public static string[] GetDependencies(string pathName) { return GetDependencies(pathName, true); }
        public static string[] GetDependencies(string pathName, bool recursive)
        {
            string[] input = new string[1];
            input[0] = pathName;
            return GetDependencies(input, recursive);
        }

        public static string[] GetDependencies(string[] pathNames) { return GetDependencies(pathNames, true); }
        extern public static string[] GetDependencies(string[] pathNames, bool recursive);

        public static void ExportPackage(string assetPathName, string fileName)
        {
            string[] input = new string[1];
            input[0] = assetPathName;
            ExportPackage(input, fileName, ExportPackageOptions.Default);
        }

        public static void ExportPackage(string assetPathName, string fileName, ExportPackageOptions flags)
        {
            string[] input = new string[1];
            input[0] = assetPathName;
            ExportPackage(input, fileName, flags);
        }

        [uei.ExcludeFromDocs] public static void ExportPackage(string[] assetPathNames, string fileName) { ExportPackage(assetPathNames, fileName, ExportPackageOptions.Default); }
        [NativeThrows]
        extern public static void ExportPackage(string[] assetPathNames, string fileName, [uei.DefaultValue("ExportPackageOptions.Default")] ExportPackageOptions flags);

        extern internal static string GetUniquePathNameAtSelectedPath(string fileName);

        [uei.ExcludeFromDocs]
        public static bool CanOpenForEdit(UnityEngine.Object assetObject)
        {
            return CanOpenForEdit(assetObject, StatusQueryOptions.UseCachedIfPossible);
        }

        public static bool CanOpenForEdit(UnityEngine.Object assetObject, [uei.DefaultValue("StatusQueryOptions.UseCachedIfPossible")] StatusQueryOptions statusOptions)
        {
            string assetPath = GetAssetOrScenePath(assetObject);
            return CanOpenForEdit(assetPath, statusOptions);
        }

        [uei.ExcludeFromDocs]
        public static bool CanOpenForEdit(string assetOrMetaFilePath)
        {
            return CanOpenForEdit(assetOrMetaFilePath, StatusQueryOptions.UseCachedIfPossible);
        }

        public static bool CanOpenForEdit(string assetOrMetaFilePath, [uei.DefaultValue("StatusQueryOptions.UseCachedIfPossible")] StatusQueryOptions statusOptions)
        {
            string message;
            return CanOpenForEdit(assetOrMetaFilePath, out message, statusOptions);
        }

        [uei.ExcludeFromDocs]
        public static bool CanOpenForEdit(UnityEngine.Object assetObject, out string message)
        {
            return CanOpenForEdit(assetObject, out message, StatusQueryOptions.UseCachedIfPossible);
        }

        public static bool CanOpenForEdit(UnityEngine.Object assetObject, out string message, [uei.DefaultValue("StatusQueryOptions.UseCachedIfPossible")] StatusQueryOptions statusOptions)
        {
            string assetPath = GetAssetOrScenePath(assetObject);
            return CanOpenForEdit(assetPath, out message, statusOptions);
        }

        [uei.ExcludeFromDocs]
        public static bool CanOpenForEdit(string assetOrMetaFilePath, out string message)
        {
            return CanOpenForEdit(assetOrMetaFilePath, out message, StatusQueryOptions.UseCachedIfPossible);
        }

        public static bool CanOpenForEdit(string assetOrMetaFilePath, out string message, [uei.DefaultValue("StatusQueryOptions.UseCachedIfPossible")] StatusQueryOptions statusOptions)
        {
            return AssetModificationProcessorInternal.CanOpenForEdit(assetOrMetaFilePath, out message, statusOptions);
        }

        [uei.ExcludeFromDocs] public static bool IsOpenForEdit(UnityEngine.Object assetObject)
        {
            return IsOpenForEdit(assetObject, StatusQueryOptions.UseCachedIfPossible);
        }

        public static bool IsOpenForEdit(UnityEngine.Object assetObject, [uei.DefaultValue("StatusQueryOptions.UseCachedIfPossible")] StatusQueryOptions statusOptions)
        {
            string assetPath = GetAssetOrScenePath(assetObject);
            return IsOpenForEdit(assetPath, statusOptions);
        }

        [uei.ExcludeFromDocs] public static bool IsOpenForEdit(string assetOrMetaFilePath)
        {
            return IsOpenForEdit(assetOrMetaFilePath, StatusQueryOptions.UseCachedIfPossible);
        }

        public static bool IsOpenForEdit(string assetOrMetaFilePath, [uei.DefaultValue("StatusQueryOptions.UseCachedIfPossible")] StatusQueryOptions statusOptions)
        {
            string message;
            return IsOpenForEdit(assetOrMetaFilePath, out message, statusOptions);
        }

        [uei.ExcludeFromDocs] public static bool IsOpenForEdit(UnityEngine.Object assetObject, out string message)
        {
            return IsOpenForEdit(assetObject, out message, StatusQueryOptions.UseCachedIfPossible);
        }

        public static bool IsOpenForEdit(UnityEngine.Object assetObject, out string message, [uei.DefaultValue("StatusQueryOptions.UseCachedIfPossible")] StatusQueryOptions statusOptions)
        {
            string assetPath = GetAssetOrScenePath(assetObject);
            return IsOpenForEdit(assetPath, out message, statusOptions);
        }

        [uei.ExcludeFromDocs] public static bool IsOpenForEdit(string assetOrMetaFilePath, out string message)
        {
            return IsOpenForEdit(assetOrMetaFilePath, out message, StatusQueryOptions.UseCachedIfPossible);
        }

        public static bool IsOpenForEdit(string assetOrMetaFilePath, out string message, [uei.DefaultValue("StatusQueryOptions.UseCachedIfPossible")] StatusQueryOptions statusOptions)
        {
            return AssetModificationProcessorInternal.IsOpenForEdit(assetOrMetaFilePath, out message, statusOptions);
        }

        [uei.ExcludeFromDocs] public static bool IsMetaFileOpenForEdit(UnityEngine.Object assetObject)
        {
            return IsMetaFileOpenForEdit(assetObject, StatusQueryOptions.UseCachedIfPossible);
        }

        public static bool IsMetaFileOpenForEdit(UnityEngine.Object assetObject, [uei.DefaultValue("StatusQueryOptions.UseCachedIfPossible")] StatusQueryOptions statusOptions)
        {
            string message;
            return IsMetaFileOpenForEdit(assetObject, out message, statusOptions);
        }

        [uei.ExcludeFromDocs] public static bool IsMetaFileOpenForEdit(UnityEngine.Object assetObject, out string message)
        {
            return IsMetaFileOpenForEdit(assetObject, out message, StatusQueryOptions.UseCachedIfPossible);
        }

        public static bool IsMetaFileOpenForEdit(UnityEngine.Object assetObject, out string message, [uei.DefaultValue("StatusQueryOptions.UseCachedIfPossible")] StatusQueryOptions statusOptions)
        {
            string assetPath = GetAssetOrScenePath(assetObject);
            string metaPath = AssetDatabase.GetTextMetaFilePathFromAssetPath(assetPath);
            return IsOpenForEdit(metaPath, out message, statusOptions);
        }

        public static T GetBuiltinExtraResource<T>(string path) where T : Object
        {
            return (T)GetBuiltinExtraResource(typeof(T), path);
        }

        [NativeThrows]
        [TypeInferenceRule(TypeInferenceRules.TypeReferencedByFirstArgument)]
        extern public static Object GetBuiltinExtraResource(Type type, string path);

        [NativeThrows]
        extern internal static string[] CollectAllChildren(string guid, string[] collection);

        internal extern static string assetFolderGUID
        {
            [FreeFunction("AssetDatabaseBindings::GetAssetFolderGUID")]
            get;
        }

        [FreeFunction("AssetDatabase::IsV1Enabled")]
        extern internal static bool IsV1Enabled();
        [FreeFunction("AssetDatabase::IsV2Enabled")]
        extern internal static bool IsV2Enabled();
        [FreeFunction("AssetDatabase::CloseCachedFiles")]
        extern internal static void CloseCachedFiles();

        [NativeThrows]
        extern internal static string[] GetSourceAssetImportDependenciesAsGUIDs(string path);
        [NativeThrows]
        extern internal static string[] GetImportedAssetImportDependenciesAsGUIDs(string path);
        [NativeThrows]
        extern internal static string[] GetGuidOfPathLocationImportDependencies(string path);

        [FreeFunction("AssetDatabase::ReSerializeAssetsForced")]
        [PreventExecutionInState(AssetDatabasePreventExecution.kPreventForceReserializeAssets, PreventExecutionSeverity.PreventExecution_ManagedException, "Consider calling ForceReserializeAssets from menu style entry point.")]
        extern private static void ForceReserializeAssets(GUID[] guids, ForceReserializeAssetsOptions options);


        public static void ForceReserializeAssets(IEnumerable<string> assetPaths, ForceReserializeAssetsOptions options = ForceReserializeAssetsOptions.ReserializeAssetsAndMetadata)
        {
            if (EditorApplication.isPlaying)
                throw new Exception("AssetDatabase.ForceReserializeAssets cannot be used when in play mode");

            HashSet<GUID> guidList = new HashSet<GUID>();

            foreach (string path in assetPaths)
            {
                if (path == "")
                    continue;

                if (InternalEditorUtility.IsUnityExtensionRegistered(path))
                    continue;

                bool rootFolder, readOnly;
                bool validPath = GetAssetFolderInfo(path, out rootFolder, out readOnly);
                if (validPath && (rootFolder || readOnly))
                    continue;

                GUID guid = GUIDFromAssetPath(path);

                if (!guid.Empty())
                {
                    guidList.Add(guid);
                }
                else
                {
                    if (File.Exists(path))
                    {
                        Debug.LogWarningFormat("Cannot reserialize file \"{0}\": the file is not in the AssetDatabase. Skipping.", path);
                    }
                    else
                    {
                        Debug.LogWarningFormat("Cannot reserialize file \"{0}\": the file does not exist. Skipping.", path);
                    }
                }
            }

            GUID[] guids = new GUID[guidList.Count];
            guidList.CopyTo(guids);
            ForceReserializeAssets(guids, options);
        }

        extern internal static System.Type GetTypeFromVisibleGUIDAndLocalFileIdentifier(GUID guid, long localId);

        [FreeFunction("AssetDatabase::GetGUIDAndLocalIdentifierInFile")]
        extern private static bool GetGUIDAndLocalIdentifierInFile(int instanceID, out GUID outGuid, out long outLocalId);

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Please use the overload of this function that uses a long data type for the localId parameter, because this version can return a localID that has overflowed. This can happen when called on objects that are part of a Prefab.",  true)]
        public static bool TryGetGUIDAndLocalFileIdentifier(Object obj, out string guid, out int localId)
        {
            return TryGetGUIDAndLocalFileIdentifier(obj.GetInstanceID(), out guid, out localId);
        }

        [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        [Obsolete("Please use the overload of this function that uses a long data type for the localId parameter, because this version can return a localID that has overflowed. This can happen when called on objects that are part of a Prefab.",  true)]
        public static bool TryGetGUIDAndLocalFileIdentifier(int instanceID, out string guid, out int localId)
        {
            throw new NotSupportedException("Use the overload of this function that uses a long data type for the localId parameter, because this version can return a localID that has overflowed. This can happen when called on objects that are part of a Prefab.");
        }

        public static bool TryGetGUIDAndLocalFileIdentifier(Object obj, out string guid, out long localId)
        {
            return TryGetGUIDAndLocalFileIdentifier(obj.GetInstanceID(), out guid, out localId);
        }

        public static bool TryGetGUIDAndLocalFileIdentifier(int instanceID, out string guid, out long localId)
        {
            GUID uguid;
            bool res = GetGUIDAndLocalIdentifierInFile(instanceID, out uguid, out localId);
            guid = uguid.ToString();
            return res;
        }

        public static bool TryGetGUIDAndLocalFileIdentifier<T>(LazyLoadReference<T> assetRef, out string guid, out long localId) where T : UnityEngine.Object
        {
            return TryGetGUIDAndLocalFileIdentifier(assetRef.instanceID, out guid, out localId);
        }

        public static void ForceReserializeAssets()
        {
            ForceReserializeAssets(GetAllAssetPaths());
        }

        [FreeFunction("AssetDatabase::RemoveObjectFromAsset")]
        extern public static void RemoveObjectFromAsset([NotNull] Object objectToRemove);

        [PreventExecutionInState(AssetDatabasePreventExecution.kGatheringDependenciesFromSourceFile, PreventExecutionSeverity.PreventExecution_ManagedException, "Cannot call AssetDatabase.LoadObjectAsync during the gathering of import dependencies.")]
        [PreventExecutionInState(AssetDatabasePreventExecution.kImportingAsset, PreventExecutionSeverity.PreventExecution_ManagedException, "Cannot use AssetDatabase.LoadObjectAsync while assets are importing.")]
        extern public static AssetDatabaseLoadOperation LoadObjectAsync(string assetPath, long localId);

        [FreeFunction("AssetDatabase::GUIDFromExistingAssetPath")]
        extern internal static GUID GUIDFromExistingAssetPath(string path);

        [FreeFunction("::ImportPackage")]
        extern private static bool ImportPackage(string packagePath, ImportPackageOptions options);
        //TODO: This API should be Obsoleted when there is time available to update all the uses of it in Package Manager packages
        public static void ImportPackage(string packagePath, bool interactive)
        {
            ImportPackage(packagePath, ImportPackageOptions.ImportDelayed | (interactive ? ImportPackageOptions.Default : ImportPackageOptions.NoGUI));
        }

        internal static bool ImportPackageImmediately(string packagePath)
        {
            return ImportPackage(packagePath, ImportPackageOptions.NoGUI);
        }

        [FreeFunction("ApplicationDisallowAutoRefresh")]
        public static extern void DisallowAutoRefresh();

        [FreeFunction("ApplicationAllowAutoRefresh")]
        public static extern void AllowAutoRefresh();

        public extern static UInt32 GlobalArtifactDependencyVersion
        {
            [FreeFunction("AssetDatabase::GetGlobalArtifactDependencyVersion")] get;
        }

        public extern static UInt32 GlobalArtifactProcessedVersion
        {
            [FreeFunction("AssetDatabase::GetGlobalArtifactProcessedVersion")] get;
        }

        [NativeThrows]
        private extern static ArtifactInfo[] GetArtifactInfos_Internal(GUID guid);

        private extern static ArtifactInfo[] GetCurrentRevisions_Internal(GUID[] guids);

        private extern static ArtifactInfo[] GetImportActivityWindowStartupData_Internal(ImportActivityWindowStartupData dataType);

        internal static ArtifactInfo[] GetCurrentRevisions(GUID[] guids)
        {
            var artifactInfos = GetCurrentRevisions_Internal(guids);
            return artifactInfos;
        }

        internal static ArtifactInfo[] GetImportActivityWindowStartupData(ImportActivityWindowStartupData dataType)
        {
            return GetImportActivityWindowStartupData_Internal(dataType);
        }

        internal static ArtifactInfo[] GetArtifactInfos(GUID guid)
        {
            var artifactInfos = GetArtifactInfos_Internal(guid);
            return artifactInfos;
        }

        [FreeFunction("AssetDatabase::ClearImporterOverride")]
        extern public static void ClearImporterOverride(string path);

        [FreeFunction("AssetDatabase::IsCacheServerEnabled")]
        public extern static bool IsCacheServerEnabled();

        [FreeFunction("AssetDatabase::SetImporterOverride")]
        extern internal static void SetImporterOverrideInternal(string path, System.Type importer);

        public static void SetImporterOverride<T>(string path)
            where T : AssetImporter
        {
            if (GUIDFromExistingAssetPath(path).Empty())
            {
                Debug.LogError(
                    $"Cannot set Importer override at \"{path}\". No Asset found at that path.");
                return;
            }

            var availableImporters = GetAvailableImporters(path);
            if (availableImporters.Contains(typeof(T)))
            {
                SetImporterOverrideInternal(path, typeof(T));
            }
            else
            {
                if (GetDefaultImporter(path) == typeof(T))
                {
                    ClearImporterOverride(path);
                    Debug.LogWarning("This usage is deprecated. Use ClearImporterOverride to revert to the default Importer instead.");
                }
                else
                {
                    Debug.LogError(
                        $"Cannot set Importer override at {path} because {typeof(T).Name} is not a valid Importer for this asset.");
                }
            }
        }

        [FreeFunction("AssetDatabase::GetImporterOverride")]
        extern public static System.Type GetImporterOverride(string path);

        [Obsolete("GetAvailableImporterTypes() has been deprecated. Use GetAvailableImporters() instead (UnityUpgradable) -> GetAvailableImporters(*)")]
        public static Type[] GetAvailableImporterTypes(string path)
        {
            return GetAvailableImporters(path);
        }

        [FreeFunction("AssetDatabase::GetAvailableImporters")]
        extern public static Type[] GetAvailableImporters(string path);

        [FreeFunction("AssetDatabase::GetDefaultImporter")]
        extern public static Type GetDefaultImporter(string path);

        [FreeFunction("AcceleratorClientCanConnectTo")]
        public extern static bool CanConnectToCacheServer(string ip, UInt16 port);

        [FreeFunction("RefreshSettings")]
        private extern static void _RefreshSettings();
        public static void RefreshSettings() => _RefreshSettings();

        public static event Action<CacheServerConnectionChangedParameters> cacheServerConnectionChanged;
        [RequiredByNativeCode]
        private static void OnCacheServerConnectionChanged()
        {
            if (cacheServerConnectionChanged != null)
            {
                CacheServerConnectionChangedParameters param;
                cacheServerConnectionChanged(param);
            }
        }

        [FreeFunction("AcceleratorClientIsConnected")]
        private extern static bool _IsConnectedToCacheServer();
        public static bool IsConnectedToCacheServer() => _IsConnectedToCacheServer();

        [FreeFunction("AcceleratorClientResetReconnectTimer")]
        public extern static void ResetCacheServerReconnectTimer();

        [FreeFunction("AcceleratorClientCloseConnection")]
        public extern static void CloseCacheServerConnection();

        [FreeFunction()]
        public extern static string GetCacheServerAddress();

        [FreeFunction()]
        public extern static UInt16 GetCacheServerPort();

        [FreeFunction("AssetDatabase::GetCacheServerNamespacePrefix")]
        public extern static string GetCacheServerNamespacePrefix();

        [FreeFunction("AssetDatabase::GetCacheServerEnableDownload")]
        public extern static bool GetCacheServerEnableDownload();

        [FreeFunction("AssetDatabase::GetCacheServerEnableUpload")]
        public extern static bool GetCacheServerEnableUpload();

        [FreeFunction("AssetDatabase::WaitForPendingCacheServerRequestsToComplete")]
        private extern static void _WaitForPendingCacheServerRequestsToComplete();
        internal static void WaitForPendingCacheServerRequestsToComplete() => _WaitForPendingCacheServerRequestsToComplete();

        [FreeFunction("AssetDatabase::IsDirectoryMonitoringEnabled")]
        public extern static bool IsDirectoryMonitoringEnabled();

        [FreeFunction("AssetDatabase::RegisterCustomDependency")]
        [PreventExecutionInState(AssetDatabasePreventExecution.kPreventCustomDependencyChanges, PreventExecutionSeverity.PreventExecution_ManagedException, "Custom dependencies can only be removed when the assetdatabase is not importing.")]
        public extern static void RegisterCustomDependency(string dependency, Hash128 hashOfValue);

        [FreeFunction("AssetDatabase::UnregisterCustomDependencyPrefixFilter")]
        [PreventExecutionInState(AssetDatabasePreventExecution.kPreventCustomDependencyChanges, PreventExecutionSeverity.PreventExecution_ManagedException, "Custom dependencies can only be removed when the assetdatabase is not importing.")]
        public extern static UInt32 UnregisterCustomDependencyPrefixFilter(string prefixFilter);

        [FreeFunction("AssetDatabase::IsAssetImportProcess")]
        public extern static bool IsAssetImportWorkerProcess();

        [FreeFunction("AssetDatabase::GetImporterType")]
        public extern static Type GetImporterType(GUID guid);

        [FreeFunction("AssetDatabase::GetImporterTypes")]
        private static extern unsafe Type[] GetImporterTypes_Internal([Span("guidsLength", isReadOnly: true)]GUID* guids, int guidsLength);

        private static unsafe Type[] GetImporterTypesUnsafe_Internal(ReadOnlySpan<GUID> guids)
        {
            fixed (GUID* guidsPtr = guids)
            {
                return GetImporterTypes_Internal(guidsPtr, guids.Length);
            }
        }

        public static Type[] GetImporterTypes(ReadOnlySpan<GUID> guids)
        {
            return GetImporterTypesUnsafe_Internal(guids);
        }

        //Since extern method overloads are not supported
        //this is the name we pick, but users end up being able
        //to call either of the overloads
        [FreeFunction("AssetDatabase::GetImporterTypesAtPaths")]
        private static extern Type[] GetImporterTypesAtPaths(string[] paths);

        public static Type GetImporterType(string assetPath)
        {
            return GetImporterTypeAtPath(assetPath);
        }

        //Since extern method overloads are not supported
        //this is the name we pick, but users end up being able
        //to call either of the overloads
        [FreeFunction("AssetDatabase::GetImporterTypeAtPath")]
        private static extern Type GetImporterTypeAtPath(string assetPath);

        public static Type[] GetImporterTypes(string[] paths)
        {
            return GetImporterTypesAtPaths(paths);
        }

        [RequiredByNativeCode]
        static string[] OnSourceAssetsModified(string[] changedAssets, string[] addedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var assetMoveInfo = new AssetMoveInfo[movedAssets.Length];
            Debug.Assert(movedAssets.Length == movedFromAssetPaths.Length);
            for (int i = 0; i < movedAssets.Length; i++)
                assetMoveInfo[i] = new AssetMoveInfo(movedFromAssetPaths[i], movedAssets[i]);

            var assetsReportedChanged = new HashSet<string>();

            foreach (Type type in TypeCache.GetTypesDerivedFrom<AssetsModifiedProcessor>())
            {
                var assetPostprocessor = Activator.CreateInstance(type) as AssetsModifiedProcessor;
                assetPostprocessor.assetsReportedChanged = assetsReportedChanged;
                assetPostprocessor.Internal_OnAssetsModified(changedAssets, addedAssets, deletedAssets, assetMoveInfo);
                assetPostprocessor.assetsReportedChanged = null;
            }

            return assetsReportedChanged.ToArray();
        }

        public enum RefreshImportMode
        {
            InProcess = 0,
            OutOfProcessPerQueue = 1
        }
        public extern static RefreshImportMode ActiveRefreshImportMode
        {
            [FreeFunction("AssetDatabase::GetRefreshImportMode")]
            get;
            [FreeFunction("AssetDatabase::SetRefreshImportMode")]
            set;
        }

        public extern static int DesiredWorkerCount
        {
            [FreeFunction("AssetDatabase::GetDesiredWorkerCount")]
            get;
            [FreeFunction("AssetDatabase::SetDesiredWorkerCount")]
            set;
        }

        [FreeFunction("AssetDatabase::ForceToDesiredWorkerCount")]
        public extern static void ForceToDesiredWorkerCount();

        [NativeHeader("Modules/AssetDatabase/Editor/Public/AssetDatabaseTypes.h")]
        [RequiredByNativeCode]
        [StructLayout(LayoutKind.Sequential)]
        internal struct WorkerStats
        {
            public int desiredWorkerCount;
            public int idleWorkerCount;
            public int importingWorkerCount;
            public int connectingWorkerCount;
            public int operationalWorkerCount;
            public int suspendedWorkerCount;
        }

        internal extern static WorkerStats GetWorkerStats();
    }
}
