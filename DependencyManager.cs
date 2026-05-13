using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;
using UnityEngine;

namespace DependencyManagement {

    public abstract class DependencyManager : AssetPostprocessor {

        internal const string CompilableDefineConstraint      = "DEPENDENCY_MANAGER_BLOCK_ASMDEF_COMPILATION";
        internal const string ForceProjectRecompilationDefine = "DEPENDENCY_MANAGER_FORCE_COMPLETE_RECOMPILATION";

        private static bool attemptedFix;

        [InitializeOnLoadMethod]
        private static void LoadAsmdefDependenciesManagers() {
            CompilationPipeline.compilationStarted += _ => attemptedFix = false;
            CompilationPipeline.assemblyCompilationFinished += (asmPath, messages) => {
                if (attemptedFix)
                    return;

                if (messages.All(msg => msg.type != CompilerMessageType.Error))
                    return;

#if DEBUG_DEPENDENCY_MANAGEMENT
                Debug.unityLogger.logEnabled = true;
                Debug.Log("[DEPENDENCY MANAGER] Compilation failed, attempting fix by forcing dependency re-referencing");
#endif
                try {
                    ForceReferenceRegisteredDependencies();
                }
                finally {
                    attemptedFix = true;
                }
            };
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths) =>
            ForceReferenceRegisteredDependencies();

        #region MenuItem Tools/

        [MenuItem("Tools/Dependency Management/Force Reference Registered Dependencies", false, 0)]
        public static void ForceReferenceRegisteredDependencies() {
            foreach (string dependencyManagerPath in ScanForDependencyManagersPaths()) {
#if DEBUG_DEPENDENCY_MANAGEMENT
                Debug.unityLogger.logEnabled = true;
                Debug.Log($"[DEPENDENCY MANAGER] ReferenceAsmdefDependenciesAtPath({dependencyManagerPath})");
#endif
                ReferenceAsmdefDependenciesAtPath(dependencyManagerPath);
            }

            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Dependency Management/Force Project Recompilation", false, 1)]
        public static void ForceProjectRecompilation() {
            NamedBuildTarget targetGroup = NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
            PlayerSettings.GetScriptingDefineSymbols(targetGroup, out string[] d);
            List<string> defines = new(d);

            if (defines.Contains(ForceProjectRecompilationDefine))
                defines.Remove(ForceProjectRecompilationDefine);
            else
                defines.Add(ForceProjectRecompilationDefine);

            PlayerSettings.SetScriptingDefineSymbols(targetGroup, defines.ToArray());

            AssetDatabase.Refresh();
        }

        #endregion

        #region MenuItem Assets/

        [MenuItem("Assets/Dependency Management/Force Reference Dependencies", false, secondaryPriority = 0)]
        private static void ForceReferenceSelectedAsmdefDependencies() {
            ClearSelectedAsmdefReferencedDependencies();
            ReferenceAsmdefDependenciesAtPath(AssetDatabase.GetAssetPath(Selection.activeObject));
            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/Dependency Management/Clear Referenced Dependencies", false, secondaryPriority = 1)]
        private static void ClearSelectedAsmdefReferencedDependencies() {
            ClearReferences(AssetDatabase.GetAssetPath(Selection.activeObject));
            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/Dependency Management/Create Dependency Manager", false, secondaryPriority = 2)]
        private static void CreateDependencyManagerForSelectedAsmdef() {
            // @formatter:off
            const string json = "{\n" +
                                "  \"hardAsmdefDependencies\": [],\n" +
                                "  \"softAsmdefDependencies\": [\n" +
                                "    {\n" +
                                "      \"define\": \"A_UNIQUE_DEFINE_YOURASSEMBLYNAME_THEDEPENDENCYNAME\",\n" +
                                "      \"dependencies\": [\n" +
                                "        \"YourSoftDependencyHere\"\n" +
                                "      ]\n" +
                                "    },\n" +
                                "    {\n" +
                                "      \"define\": \"ANOTHER_UNIQUE_DEFINE_YOURASSEMBLYNAME_THEDEPENDENCYNAME\",\n" +
                                "      \"dependencies\": [\n" +
                                "        \"AlsoSupports.dll\",\n" +
                                "        \"AndMultipleEntries\"\n" +
                                "      ]\n" +
                                "    }\n" +
                                "  ]\n" +
                                "}";
            // @formatter:on

            File.WriteAllText(AssetDatabase.GetAssetPath(Selection.activeObject) + ".json", json, Encoding.UTF8);

            AssetDatabase.Refresh();
        }

        [MenuItem("Assets/Dependency Management/Toggle asmdef compilability", false, secondaryPriority = 3)]
        public static void ToggleSelectedAsmdefCompilability() {
            AsmdefData asmdefData = new(AssetDatabase.GetAssetPath(Selection.activeObject));

            if (asmdefData.defineConstraints.Contains(CompilableDefineConstraint))
                asmdefData.defineConstraints.Remove(CompilableDefineConstraint);
            else
                asmdefData.defineConstraints.Add(CompilableDefineConstraint);

            asmdefData.WriteToFile();

            AssetDatabase.Refresh();
        }

        // Validators

        [MenuItem("Assets/Dependency Management/Force Reference Dependencies", true)]
        [MenuItem("Assets/Dependency Management/Clear Referenced Dependencies", true)]
        // [MenuItem("Assets/Dependency Management/Toggle asmdef compilability", true)]
        private static bool SelectedAsmdefValidatorAndHasDependencyManagerJson() {
            string selectionPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            return Path.GetFileName(selectionPath).Contains(".asmdef") && File.Exists(selectionPath + ".json");
        }

        [MenuItem("Assets/Dependency Management/Create Dependency Manager", true)]
        private static bool SelectedAsmdefValidatorAndHasNoDependencyManagerJson() {
            string selectionPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            return Path.GetFileName(selectionPath).Contains(".asmdef") && !File.Exists(selectionPath + ".json");
        }

        #endregion

        private static void ReferenceAsmdefDependenciesAtPath(string dependencyManagerPath) =>
            JsonUtility.FromJson<AsmdefDependencies>(File.ReadAllText(dependencyManagerPath)).ReferenceDependencies(dependencyManagerPath);

        private static void ClearReferences(string filePath) {
            AsmdefData asmdef = new(filePath);
            asmdef.ClearReferencesAndDefines();
            asmdef.WriteToFile();
        }

        private static IEnumerable<string> ScanForDependencyManagersPaths() {
            // Assets
            foreach (string filePath in Directory.EnumerateFiles(Application.dataPath, "*.asmdef.json", SearchOption.AllDirectories))
                yield return filePath;

            // Packages
            foreach (string filePath in Directory.EnumerateFiles(Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Packages"), "*.asmdef.json", SearchOption.AllDirectories))
                yield return filePath;
        }

    }

}