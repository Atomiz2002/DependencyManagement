using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DependencyManagement {

    public class AsmdefDependencies {

        public readonly string asmdef;
        public readonly string definesPrefix; // PREFIXES THE DEPENDENCIES DEFINES

        public readonly List<HardAsmdefDependency> hardDependencies = new();
        public readonly List<SoftAsmdefDependency> softDependencies = new();

        public AsmdefDependencies(string asmdef, string definesPrefix) {
            this.asmdef        = asmdef;
            this.definesPrefix = definesPrefix;
        }

        public AsmdefDependencies SetHardDependencies(HardAsmdefDependency hardDependency, params HardAsmdefDependency[] hardDependencies) {
            this.hardDependencies.Clear();
            this.hardDependencies.AddRange(hardDependencies.Append(hardDependency));
            return this;
        }

        public AsmdefDependencies SetSoftDependencies(SoftAsmdefDependency softDependency, params SoftAsmdefDependency[] softDependencies) {
            this.softDependencies.Clear();
            this.softDependencies.AddRange(softDependencies.Append(softDependency));
            this.softDependencies.ForEach(d => d.define = definesPrefix + d.define);
            return this;
        }

        public void ReferenceDependencies() {
            string packageAsmdefPath = AssetDatabase.FindAssets($"t:AssemblyDefinitionAsset {Path.GetFileNameWithoutExtension(asmdef)}")
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => Path.GetFileName(p) == asmdef);

            if (string.IsNullOrEmpty(packageAsmdefPath))
                throw new DependencyManagerException($"Failed to find package asmdefs: {asmdef}");

            AsmdefData asmdefData = new(packageAsmdefPath);
            bool       modified   = false;

            if (asmdefData.references?.Count > 0 || asmdefData.precompiledReferences?.Count > 0)
                modified = true;

            asmdefData.ClearReferencesAndDefines();

            foreach (HardAsmdefDependency hardDependency in hardDependencies)
                ReferenceHardDependency(asmdefData, ref modified, hardDependency);

            foreach (SoftAsmdefDependency softDependency in softDependencies)
                ReferenceSoftDependency(asmdefData, ref modified, softDependency);

            if (modified)
                asmdefData.WriteToFile(packageAsmdefPath);
        }

        private static void ReferenceHardDependency(AsmdefData asmdefData, ref bool modified, HardAsmdefDependency hardDependency) {
            const string GUID_Prefix = "GUID:";

            foreach ((string dependency, bool located) in hardDependency.dependencies) {
                AsmdefData.VersionDefine missing = AsmdefData.VersionDefine.Required(dependency);

                if (located) {
                    asmdefData.versionDefines.RemoveAll(vd => vd.define == missing.define);

                    List<string> references = dependency.EndsWith(".dll")
                        ? asmdefData.precompiledReferences
                        : asmdefData.references;

                    if (!references.Select(reference => reference.StartsWith(GUID_Prefix)
                            ? JsonUtility.FromJson<AsmdefData>(File.ReadAllText(AssetDatabase.GUIDToAssetPath(reference[GUID_Prefix.Length..]), Encoding.UTF8)).name
                            : reference).Contains(dependency))
                        references.Add(dependency);

                    modified = true;
                }
                else {
                    // TODO test what code should be here (see ReferenceSoftDependencies)

                    asmdefData.versionDefines.Insert(0, missing);
                }
            }
        }

        private static void ReferenceSoftDependency(AsmdefData asmdefData, ref bool modified, SoftAsmdefDependency softDependency) {
            const string GUID_Prefix = "GUID:";

            bool foundAllDependencies = true;

            foreach ((string dependency, bool located) in softDependency.dependencies) {
                AsmdefData.VersionDefine missing = AsmdefData.VersionDefine.Missing(softDependency.define, dependency);

                if (located) {
                    asmdefData.versionDefines.RemoveAll(vd => vd.define == missing.define);

                    List<string> references = dependency.EndsWith(".dll")
                        ? asmdefData.precompiledReferences
                        : asmdefData.references;

                    if (!references.Select(reference => reference.StartsWith(GUID_Prefix)
                            ? JsonUtility.FromJson<AsmdefData>(File.ReadAllText(AssetDatabase.GUIDToAssetPath(reference[GUID_Prefix.Length..]), Encoding.UTF8)).name
                            : reference).Contains(dependency))
                        references.Add(dependency);

                    asmdefData.versionDefines.Add(AsmdefData.VersionDefine.Located(softDependency.define));

                    modified = true;
                }
                else {
                    if (asmdefData.versionDefines.RemoveAll(vd => vd.define == softDependency.define) > 0)
                        modified = true;

                    if (asmdefData.versionDefines.Any(vd => vd.define == missing.define))
                        continue;

                    asmdefData.versionDefines.Insert(0, missing);
                }
            }
        }

        public class AsmdefDependency {

            public readonly Dictionary<string, bool> dependencies;

            protected AsmdefDependency(string dependency, params string[] dependencies) {
                // TODO nonexistent still get "located" ...?
                this.dependencies = dependencies.Append(dependency).Select(dep =>
                        (dependency: dep, located: !string.IsNullOrEmpty(dep.EndsWith(".dll")
                            ? AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(dep))
                                .FirstOrDefault(guid => Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid)) == dep)
                            : AssetDatabase.FindAssets($"t:AssemblyDefinitionAsset {dep}")
                                .FirstOrDefault(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == dep))))
                    .ToDictionary(dep => dep.dependency, dep => dep.located);
            }

            public override string ToString() => string.Join(", ", dependencies.Select(d => $"{d.Key}: {d.Value}"));

        }

        public class HardAsmdefDependency : AsmdefDependency {

            public HardAsmdefDependency(string dependency, params string[] dependencies) : base(dependency, dependencies) {}

        }

        public class SoftAsmdefDependency : AsmdefDependency {

            public string define;

            public SoftAsmdefDependency(string define, string dependency, params string[] dependencies) : base(dependency, dependencies) {
                this.define = define;
            }

            public override string ToString() => $"{define} {string.Join(", ", dependencies.Select(d => $"{d.Key}: {d.Value}"))}";

        }

        private class DependencyManagerException : Exception {

            public DependencyManagerException(string message) : base(message) {}

        }

    }

}