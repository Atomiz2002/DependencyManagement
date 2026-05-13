using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DependencyManagement {

    [Serializable]
    public class AsmdefDependencies {

        private const string GUID_Prefix = "GUID:";

        // [NonSerialized]
        // public string asmdefName;

        public List<AsmdefDependency> hardAsmdefDependencies = new();
        public List<AsmdefDependency> softAsmdefDependencies = new();

        // public AsmdefDependencies(string asmdefName) {
        //     this.asmdefName = asmdefName;
        // }

        public void ReferenceDependencies(string asmdefPath) {
// #if DEBUG_DEPENDENCY_MANAGEMENT
//             Debug.Log(asmdefName);
// #endif

            AsmdefData asmdefData = new(asmdefPath);

            bool modified = false;

            if (asmdefData.references?.Count > 0 || asmdefData.precompiledReferences?.Count > 0)
                modified = true;

            asmdefData.ClearReferencesAndDefines();

            foreach (AsmdefDependency hardDependency in hardAsmdefDependencies)
                ReferenceHardDependency(asmdefData, ref modified, hardDependency);

            foreach (AsmdefDependency softDependency in softAsmdefDependencies)
                ReferenceSoftDependency(asmdefData, ref modified, softDependency);

            if (modified)
                asmdefData.WriteToFile();
        }

        private static void ReferenceHardDependency(AsmdefData asmdefData, ref bool modified, AsmdefDependency hardAsmdefDependency) {
            asmdefData.defineConstraints.Add(hardAsmdefDependency.define);

            foreach (string dependency in hardAsmdefDependency.dependencies) {
                AsmdefData.VersionDefine required = AsmdefData.VersionDefine.Invalid(hardAsmdefDependency.define, dependency, "Requires");

                if (hardAsmdefDependency.LocateDependency(dependency)) {
                    asmdefData.versionDefines.RemoveAll(vd => vd.define == required.define);

                    List<string> references = dependency.EndsWith(".dll")
                        ? asmdefData.precompiledReferences
                        : asmdefData.references;

                    if (!references.Select(reference => reference.StartsWith(GUID_Prefix)
                            ? JsonUtility.FromJson<AsmdefData>(File.ReadAllText(AssetDatabase.GUIDToAssetPath(reference[GUID_Prefix.Length..]), Encoding.UTF8)).name
                            : reference).Contains(dependency))
                        references.Add(dependency);

                    asmdefData.versionDefines.Add(AsmdefData.VersionDefine.Located(hardAsmdefDependency.define));

                    modified = true;
                }
                else {
                    if (asmdefData.versionDefines.RemoveAll(vd => vd.define == hardAsmdefDependency.define) > 0)
                        modified = true;

                    if (asmdefData.versionDefines.Any(vd => vd.define == required.define))
                        continue;

                    asmdefData.versionDefines.Insert(0, required);
                }
            }
        }

        private static void ReferenceSoftDependency(AsmdefData asmdefData, ref bool modified, AsmdefDependency softAsmdefDependency) {
            foreach (string dependency in softAsmdefDependency.dependencies) {
                AsmdefData.VersionDefine missing = AsmdefData.VersionDefine.Invalid(softAsmdefDependency.define, dependency, "Missing");

                if (softAsmdefDependency.LocateDependency(dependency)) {
                    asmdefData.versionDefines.RemoveAll(vd => vd.define == missing.define);

                    List<string> references = dependency.EndsWith(".dll")
                        ? asmdefData.precompiledReferences
                        : asmdefData.references;

                    if (!references.Select(reference => reference.StartsWith(GUID_Prefix)
                            ? JsonUtility.FromJson<AsmdefData>(File.ReadAllText(AssetDatabase.GUIDToAssetPath(reference[GUID_Prefix.Length..]), Encoding.UTF8)).name
                            : reference).Contains(dependency))
                        references.Add(dependency);

                    asmdefData.versionDefines.Add(AsmdefData.VersionDefine.Located(softAsmdefDependency.define));

                    modified = true;
                }
                else {
                    if (asmdefData.versionDefines.RemoveAll(vd => vd.define == softAsmdefDependency.define) > 0)
                        modified = true;

                    if (asmdefData.versionDefines.Any(vd => vd.define == missing.define))
                        continue;

                    asmdefData.versionDefines.Insert(0, missing);
                }
            }
        }

        [Serializable]
        public class AsmdefDependency : AssetPostprocessor {

            public string       define;
            public List<string> dependencies;

            private static List<string> compiledAssemblies;

            public AsmdefDependency(string define, string dependency, params string[] dependencies) {
                this.define       = define;
                this.dependencies = new(dependencies) { dependency };
            }

            private static void OnPostprocessAllAssets(string[] _, string[] _1, string[] _2, string[] _3) {
                compiledAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Select(a => a.FullName.Split(',')[0])
                    .ToList();
            }

            public bool LocateDependency(string name) => compiledAssemblies.Contains(name.Replace(".dll", "").Replace(".asmdef", ""));

            public override string ToString() => $"{define} {string.Join(", ", dependencies)} ({string.Join(",", compiledAssemblies)})";

        }

        private class DependencyManagerException : Exception {

            public DependencyManagerException(string message) : base(message) {}

        }

    }

}