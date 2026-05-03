using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DependencyManagement {

    public class AsmdefDependencies {

        private const string GUID_Prefix = "GUID:";

        public readonly string asmdef;
        public readonly string definesPrefix; // PREFIXES THE DEPENDENCIES DEFINES

        public readonly List<AsmdefDependency> hardDependencies = new();
        public readonly List<AsmdefDependency> softDependencies = new();

        public AsmdefDependencies(string asmdef, string definesPrefix) {
            this.asmdef        = asmdef;
            this.definesPrefix = definesPrefix;
        }

        /// Blocks the asmdef from compiling if any of these is missing
        public AsmdefDependencies SetHardDependencies(AsmdefDependency hardDependency, params AsmdefDependency[] hardDependencies) {
            SetDependencies(this.hardDependencies, hardDependency, hardDependencies);
            return this;
        }

        /// Allows the asmdef to still compile, but with supposedly limited by you functionality
        public AsmdefDependencies SetSoftDependencies(AsmdefDependency softDependency, params AsmdefDependency[] softDependencies) {
            SetDependencies(this.softDependencies, softDependency, softDependencies);
            return this;
        }

        private void SetDependencies(List<AsmdefDependency> container, AsmdefDependency softDependency, params AsmdefDependency[] softDependencies) {
            container.Clear();
            container.AddRange(softDependencies.Append(softDependency));
            container.ForEach(d => d.define = definesPrefix + d.define);
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

            foreach (AsmdefDependency hardDependency in hardDependencies)
                ReferenceHardDependency(asmdefData, ref modified, hardDependency);

            foreach (AsmdefDependency softDependency in softDependencies)
                ReferenceSoftDependency(asmdefData, ref modified, softDependency);

            if (modified)
                asmdefData.WriteToFile();
        }

        private static void ReferenceHardDependency(AsmdefData asmdefData, ref bool modified, AsmdefDependency hardDependency) {
            foreach ((string dependency, bool located) in hardDependency.dependencies) {
                asmdefData.defineConstraints.Add(hardDependency.define);

                AsmdefData.VersionDefine required = AsmdefData.VersionDefine.Invalid(hardDependency.define, dependency, "Required");

                if (located) {
                    asmdefData.versionDefines.RemoveAll(vd => vd.define == required.define);

                    List<string> references = dependency.EndsWith(".dll")
                        ? asmdefData.precompiledReferences
                        : asmdefData.references;

                    if (!references.Select(reference => reference.StartsWith(GUID_Prefix)
                            ? JsonUtility.FromJson<AsmdefData>(File.ReadAllText(AssetDatabase.GUIDToAssetPath(reference[GUID_Prefix.Length..]), Encoding.UTF8)).name
                            : reference).Contains(dependency))
                        references.Add(dependency);

                    asmdefData.versionDefines.Add(AsmdefData.VersionDefine.Located(hardDependency.define));

                    modified = true;
                }
                else {
                    if (asmdefData.versionDefines.RemoveAll(vd => vd.define == hardDependency.define) > 0)
                        modified = true;

                    if (asmdefData.versionDefines.Any(vd => vd.define == required.define))
                        continue;

                    asmdefData.versionDefines.Insert(0, required);
                }
            }
        }

        private static void ReferenceSoftDependency(AsmdefData asmdefData, ref bool modified, AsmdefDependency softDependency) {
            foreach ((string dependency, bool located) in softDependency.dependencies) {
                AsmdefData.VersionDefine missing = AsmdefData.VersionDefine.Invalid(softDependency.define, dependency, "Missing");

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

            public          string                   define;
            public readonly Dictionary<string, bool> dependencies;

            public AsmdefDependency(string define, string dependency, params string[] dependencies) {
                this.define = define;

                // TODO nonexistent still get "located" ...?
                this.dependencies = dependencies.Append(dependency).Select(dep =>
                        (dependency: dep, located: !string.IsNullOrEmpty(dep.EndsWith(".dll")
                            ? AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(dep))
                                .FirstOrDefault(guid => Path.GetFileName(AssetDatabase.GUIDToAssetPath(guid)) == dep)
                            : AssetDatabase.FindAssets($"t:AssemblyDefinitionAsset {dep}")
                                .FirstOrDefault(guid => Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)) == dep))))
                    .ToDictionary(dep => dep.dependency, dep => dep.located);
            }

            public override string ToString() => $"{define} {string.Join(", ", dependencies.Select(d => $"{d.Key}: {d.Value}"))}";

        }

        private class DependencyManagerException : Exception {

            public DependencyManagerException(string message) : base(message) {}

        }

    }

}