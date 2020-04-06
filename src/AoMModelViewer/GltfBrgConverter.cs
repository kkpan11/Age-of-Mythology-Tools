﻿using AoMEngineLibrary.Graphics.Brg;
using AoMEngineLibrary.Graphics.Model;
using SharpGLTF.Runtime;
using SharpGLTF.Schema2;
using SharpGLTF.Transforms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

using GltfMesh = SharpGLTF.Schema2.Mesh;
using GltfMaterial = SharpGLTF.Schema2.Material;
using GltfAnimation = SharpGLTF.Schema2.Animation;
using GltfMeshBuilder = SharpGLTF.Geometry.MeshBuilder<
    AoMModelViewer.NullableGltfMaterial,
    SharpGLTF.Geometry.VertexTypes.VertexPositionNormal,
    SharpGLTF.Geometry.VertexTypes.VertexColor1Texture1,
    SharpGLTF.Geometry.VertexTypes.VertexEmpty>;
using GltfPrimitiveBuilder = SharpGLTF.Geometry.PrimitiveBuilder<
    AoMModelViewer.NullableGltfMaterial,
    SharpGLTF.Geometry.VertexTypes.VertexPositionNormal,
    SharpGLTF.Geometry.VertexTypes.VertexColor1Texture1,
    SharpGLTF.Geometry.VertexTypes.VertexEmpty>;
using System.IO;
using SharpGLTF.Geometry.VertexTypes;

namespace AoMModelViewer
{
    internal struct NullableGltfMaterial
    {
        public GltfMaterial m;
    }

    public class GltfBrgConverter
    {
        public GltfBrgConverter()
        {

        }

        public BrgFile Convert(ModelRoot gltfFile)
        {
            BrgFile brg = new BrgFile();
            var model = gltfFile;

            // We'll only export the default scene and first animation in the list
            var scene = model.DefaultScene;
            var anim = model.LogicalAnimations.Count > 0 ? model.LogicalAnimations[0] : null;

            // Figure out animation duration and keys
            const int fps = 15;
            const int fpsm1 = fps - 1;
            if (anim == null)
            {
                brg.Header.NumMeshes = 1;
            }
            else
            {
                brg.Animation.Duration = anim.Duration;
                brg.Header.NumMeshes = (int)(anim.Duration * fps);
                brg.Animation.TimeStep = anim.Duration / brg.Header.NumMeshes;
            }

            for (int i = 0; i < brg.Header.NumMeshes; ++i)
            {
                brg.Animation.MeshKeys.Add(i / fpsm1);
            }

            // Convert meshes, materials and attachpoints
            ConvertMeshes(scene, anim, brg);

            // Perform final processing
            BrgMesh mesh = brg.Meshes[0];
            HashSet<int> diffFaceMats = mesh.Faces.Select(f => (int)f.MaterialIndex).ToHashSet();
            if (diffFaceMats.Count > 0)
            {
                mesh.ExtendedHeader.NumMaterials = (byte)(diffFaceMats.Count - 1);
                mesh.ExtendedHeader.NumUniqueMaterials = diffFaceMats.Count;
            }

            brg.Materials = brg.Materials.Where(m => diffFaceMats.Contains(m.Id)).ToList();
            brg.Header.NumMaterials = brg.Materials.Count;

            return brg;
        }

        private void ConvertMeshes(Scene gltfScene, GltfAnimation? gltfAnimation, BrgFile brg)
        {
            Dictionary<int, int> matIdMapping = new Dictionary<int, int>();

            var sceneTemplate = SceneTemplate.Create(gltfScene, false);
            var instance = sceneTemplate.CreateInstance();

            Predicate<Node> noDummy = n => !n.Name.StartsWith("dummy_", StringComparison.InvariantCultureIgnoreCase);

            // Mesh Animations
            for (int i = 0; i < brg.Header.NumMeshes; i++)
            {
                // Compute the data of the scene at time
                if (gltfAnimation == null)
                {
                    instance.SetPoseTransforms();
                }
                else
                {
                    instance.SetAnimationFrame(gltfAnimation.Name, brg.Animation.MeshKeys[i]);
                }

                // Evaluate the entire gltf scene
                var gltfMeshData = GetMeshBuilder(gltfScene, instance, noDummy);

                // We only care about primitves with a material
                var prims = gltfMeshData.MeshBuilder.Primitives.Where(p => p.Material.m != null);

                // Create brg materials and a way to map their ids
                var gltfMats = prims.Select(p => p.Material.m);
                ConvertMaterials(gltfMats, brg, matIdMapping);

                // Add a new mesh
                var mesh = new BrgMesh(brg);
                mesh.Header.Format = BrgMeshFormat.HASFACENORMALS | BrgMeshFormat.ANIMATED;
                mesh.ExtendedHeader.AnimationLength = brg.Animation.Duration;
                if (i > 0)
                {
                    mesh.Header.Flags |= BrgMeshFlag.SECONDARYMESH;
                }
                brg.Meshes.Add(mesh);

                // Convert all the attachpoints in the scene
                ConvertAttachpoints(gltfScene, instance, mesh);

                // ConvertMesh will set proper flags to the brg mesh which we then want to validate
                ConvertMesh(prims, gltfMeshData.HasTexCoords, mesh, matIdMapping);
                brg.UpdateMeshSettings(i, brg.Meshes[0].Header.Flags, brg.Meshes[0].Header.Format, brg.Meshes[0].Header.AnimationType, brg.Meshes[0].Header.InterpolationType);
            }
        }

        private (GltfMeshBuilder MeshBuilder, bool HasTexCoords) GetMeshBuilder(Scene scene, SceneInstance instance, Predicate<Node> nodeSelector)
        {
            var meshes = scene.LogicalParent.LogicalMeshes;

            // TODO: Implement nodeSelector
            var tris = instance
                .DrawableReferences
                .SelectMany(item => meshes[item.MeshIndex].EvaluateTriangles(item.Transform));

            var mb = new GltfMeshBuilder();
            bool hasTexCoords = false;
            foreach (var tri in tris)
            {
                if (!hasTexCoords)
                {
                    hasTexCoords = tri.A.GetMaterial().MaxTextCoords > 0;
                }

                var mat = new NullableGltfMaterial() { m = tri.Material };
                mb.UsePrimitive(mat).AddTriangle(tri.A, tri.B, tri.C);
            }

            return (mb, hasTexCoords);
        }

        private void ConvertMesh(IEnumerable<GltfPrimitiveBuilder> primitives, bool hasTexCoords, BrgMesh mesh, Dictionary<int, int> matIdMapping)
        {
            mesh.Header.Version = 22;
            mesh.Header.ExtendedHeaderSize = 40;

            // Export Vertices and Normals
            Vector3 centerPos = new Vector3();
            Vector3 minExtent = new Vector3(float.MaxValue);
            Vector3 maxExtent = new Vector3(float.MinValue);
            foreach (var p in primitives)
            {
                foreach (var v in p.Vertices)
                {
                    var pos = v.Position;
                    var norm = v.Geometry.Normal;
                    mesh.Vertices.Add(new Vector3(-pos.X, pos.Y, pos.Z));
                    mesh.Normals.Add(new Vector3(-norm.X, norm.Y, norm.Z));

                    centerPos += pos;
                    minExtent.X = Math.Min(pos.X, minExtent.X);
                    minExtent.Y = Math.Min(pos.Y, minExtent.Y);
                    minExtent.Z = Math.Min(pos.Z, minExtent.Z);
                    maxExtent.X = Math.Max(pos.X, maxExtent.X);
                    maxExtent.Y = Math.Max(pos.Y, maxExtent.Y);
                    maxExtent.Z = Math.Max(pos.Z, maxExtent.Z);
                }
            }

            // Update header values
            centerPos /= mesh.Vertices.Count;
            Vector3 bbox = (maxExtent - minExtent) / 2;
            mesh.Header.CenterPosition = new Vector3(-centerPos.X, centerPos.Y, centerPos.Z);
            mesh.Header.MinimumExtent = -bbox;
            mesh.Header.MaximumExtent = bbox;
            mesh.Header.CenterRadius = Math.Max(Math.Max(bbox.X, bbox.Y), bbox.Z);

            // Check if MeshBuilder has texcoords
            //if (!mesh.Header.Flags.HasFlag(BrgMeshFlag.SECONDARYMESH) || mesh.Header.Flags.HasFlag(BrgMeshFlag.ANIMTEXCOORDS))
            //{
            //    hasTexCoords = gltfMesh.Primitives.Any(p => p.Vertices.Any(v => v.Material.TexCoord != Vector2.Zero));
            //}

            // Export TexCoords
            if (hasTexCoords)
            {
                mesh.Header.Flags |= BrgMeshFlag.TEXCOORDSA;
                foreach (var p in primitives)
                {
                    foreach (var v in p.Vertices)
                    {
                        var tc = v.Material.TexCoord;
                        mesh.TextureCoordinates.Add(new Vector3(tc.X, 1 - tc.Y, 0f));
                    }
                }
            }

            // Check if MeshBuilder has colors
            // TODO: Add support for vertex colors
            //bool hasColors = gltfMesh.Primitives.Any(p => p.Vertices.Any(v => v.Material.Color != Vector4.Zero));

            // Export faces
            if (!mesh.Header.Flags.HasFlag(BrgMeshFlag.SECONDARYMESH))
            {
                mesh.Header.Flags |= BrgMeshFlag.MATERIAL;
                if (mesh.Header.Flags.HasFlag(BrgMeshFlag.MATERIAL))
                {
                    mesh.VertexMaterials.AddRange(new Int16[mesh.Vertices.Count]);
                }

                int baseVertexIndex = 0;
                foreach (var p in primitives)
                {
                    Face f = new Face();
                    mesh.Faces.Add(f);

                    if (mesh.Header.Flags.HasFlag(BrgMeshFlag.MATERIAL))
                    {
                        int faceMatId = p.Material.m.LogicalIndex;
                        if (matIdMapping.ContainsKey(faceMatId))
                        {
                            f.MaterialIndex = (Int16)matIdMapping[faceMatId];
                        }
                        else
                        {
                            throw new InvalidDataException("A face has an invalid material id " + faceMatId + ".");
                        }
                    }

                    foreach (var t in p.Triangles)
                    {
                        var a = t.A + baseVertexIndex;
                        var b = t.B + baseVertexIndex;
                        var c = t.C + baseVertexIndex;

                        f.Indices.Add((Int16)a);
                        f.Indices.Add((Int16)c);
                        f.Indices.Add((Int16)b);

                        if (mesh.Header.Flags.HasFlag(BrgMeshFlag.MATERIAL))
                        {
                            mesh.VertexMaterials[f.Indices[0]] = f.MaterialIndex;
                            mesh.VertexMaterials[f.Indices[1]] = f.MaterialIndex;
                            mesh.VertexMaterials[f.Indices[2]] = f.MaterialIndex;
                        }
                    }

                    baseVertexIndex += p.Vertices.Count;
                }
            }

            mesh.Header.NumFaces = (short)mesh.Faces.Count;
        }

        private void ConvertAttachpoints(Scene scene, SceneInstance sceneInstance, BrgMesh mesh)
        {
            // Convert Attachpoints
            var dummies = sceneInstance.LogicalNodes.Where(n => n.Name.StartsWith("dummy_", StringComparison.InvariantCultureIgnoreCase));

            if (dummies.Any())
            {
                mesh.Header.Flags |= BrgMeshFlag.ATTACHPOINTS;
                foreach (var dummy in dummies)
                {
                    string aName = dummy.Name;
                    if (!BrgAttachpoint.TryGetIdByName(aName.Substring(6), out int nameId))
                    {
                        // Check if it's hotspot
                        if (aName.Equals("Dummy_hotspot", StringComparison.InvariantCultureIgnoreCase))
                        {
                            var trans = dummy.WorldMatrix.Translation;
                            mesh.Header.HotspotPosition = new Vector3(-trans.X, trans.Y, trans.Z);
                        }
                        continue;
                    }

                    BrgAttachpoint att = new BrgAttachpoint();
                    att.NameId = nameId;
                    var transform = dummy.WorldMatrix;

                    att.Up.X = transform.M21;
                    att.Up.Y = transform.M22;
                    att.Up.Z = transform.M23;

                    att.Forward.X = transform.M31;
                    att.Forward.Y = transform.M32;
                    att.Forward.Z = transform.M33;

                    att.Right.X = transform.M13;
                    att.Right.Y = transform.M13;
                    att.Right.Z = transform.M13;

                    att.Position.X = -transform.M41;
                    att.Position.Z = transform.M42;
                    att.Position.Y = transform.M43;

                    // TODO: Calculate dummy bounding box
                    var bBox = new Vector3(0.5f, 0.5f, 0.5f);
                    att.BoundingBoxMin.X = -bBox.X;
                    att.BoundingBoxMin.Z = -bBox.Y;
                    att.BoundingBoxMin.Y = -bBox.Z;
                    att.BoundingBoxMax.X = bBox.X;
                    att.BoundingBoxMax.Z = bBox.Y;
                    att.BoundingBoxMax.Y = bBox.Z;

                    mesh.Attachpoints.Add(att);
                }
            }
        }

        private void ConvertMaterials(IEnumerable<GltfMaterial> gltfMats, BrgFile brg, Dictionary<int, int> matIdMapping)
        {
            foreach (var gltfMat in gltfMats)
            {
                if (gltfMat == null) continue;

                BrgMaterial mat = new BrgMaterial(brg);
                mat.Id = brg.Materials.Count + 1;
                ConvertMaterial(gltfMat, mat);

                int matListIndex = brg.Materials.IndexOf(mat);
                int actualMatId = gltfMat.LogicalIndex;
                if (matListIndex >= 0)
                {
                    if (!matIdMapping.ContainsKey(actualMatId))
                    {
                        matIdMapping.Add(actualMatId, brg.Materials[matListIndex].Id);
                    }
                }
                else
                {
                    brg.Materials.Add(mat);
                    if (matIdMapping.ContainsKey(actualMatId))
                    {
                        matIdMapping[actualMatId] = mat.Id;
                    }
                    else
                    {
                        matIdMapping.Add(actualMatId, mat.Id);
                    }
                }
            }
        }

        #region Old
        private void Convert(ModelRoot model, BrgFile brg)
        {
            var modelTemplate = SharpGLTF.Runtime.SceneTemplate.Create(model.DefaultScene, true);
            var inst = modelTemplate.CreateInstance();

            ConvertMaterials(model, modelTemplate, brg, out Dictionary<int, int> matIdMapping);

            inst.GetAnimationDuration(0);
            int frames = (int)Math.Round(30 * inst.GetAnimationDuration(0));
            for (int i = 0; i < frames; ++i)
            {
                float time = i / 30.0f;

                BrgMesh mesh = new BrgMesh(brg);
                inst.SetAnimationFrame(0, time);
                foreach (var drawable in inst.DrawableReferences)
                {
                    var glMesh = model.LogicalMeshes[drawable.MeshIndex];

                    if (glMesh.Name.Contains("dummy", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    ConvertMesh(glMesh, mesh, drawable.Transform, matIdMapping);
                }
            }
        }

        private void ConvertMesh(GltfMesh glMesh, BrgMesh mesh, IGeometryTransform xform, Dictionary<int, int> matIdMapping)
        {
            foreach (var prim in glMesh.Primitives)
            {
                ConvertPrimitive(prim, mesh, xform, matIdMapping);
            }
        }

        private void ConvertPrimitive(MeshPrimitive prim, BrgMesh mesh, IGeometryTransform xform, Dictionary<int, int> matIdMapping)
        {
            if (prim.DrawPrimitiveType == SharpGLTF.Schema2.PrimitiveType.POINTS) return;
            if (prim.DrawPrimitiveType == SharpGLTF.Schema2.PrimitiveType.LINES) return;
            if (prim.DrawPrimitiveType == SharpGLTF.Schema2.PrimitiveType.LINE_LOOP) return;
            if (prim.DrawPrimitiveType == SharpGLTF.Schema2.PrimitiveType.LINE_STRIP) return;

            var verts = prim.GetVertexAccessor("POSITION")?.AsVector3Array();
            var normals = prim.GetVertexAccessor("NORMAL")?.AsVector3Array();

            if (verts == null || normals == null) return;
            if (verts.Count < 3) return;

            var tc = prim.GetVertexAccessor("TEXCOORD_0")?.AsVector2Array();

            int numTargets = prim.MorphTargetsCount;
            Vector3[][] posTargets = Enumerable.Range(0, verts.Count).Select(_ => new Vector3[numTargets]).ToArray();
            Vector3[][] normTargets = Enumerable.Range(0, verts.Count).Select(_ => new Vector3[numTargets]).ToArray();
            for (int i = 0; i < numTargets; ++i)
            {
                var targetAccessors = prim.GetMorphTargetAccessors(i);
                Vector3[] targets = new Vector3[numTargets];

                if (targetAccessors.ContainsKey("POSITION"))
                {
                    var acc = targetAccessors["POSITION"];
                    var accData = acc.AsVector3Array();

                    for (int j = 0; j < accData.Count; ++j)
                    {
                        posTargets[j][i] = accData[j];
                    }
                }

                if (targetAccessors.ContainsKey("NORMAL"))
                {
                    var acc = targetAccessors["NORMAL"];
                    var accData = acc.AsVector3Array();

                    for (int j = 0; j < accData.Count; ++j)
                    {
                        normTargets[j][i] = accData[j];
                    }
                }
            }

            var joints0 = prim.GetVertexAccessor("JOINTS_0")?.AsVector4Array();
            var joints1 = prim.GetVertexAccessor("JOINTS_1")?.AsVector4Array();
            var weights0 = prim.GetVertexAccessor("WEIGHTS_0")?.AsVector4Array();
            var weights1 = prim.GetVertexAccessor("WEIGHTS_1")?.AsVector4Array();

            if (xform is SharpGLTF.Transforms.RigidTransform statXform)
            {
                SparseWeight8 weight = new SparseWeight8();
                for (int i = 0; i < verts.Count; ++i)
                {
                    verts[i] = statXform.TransformPosition(verts[i], posTargets[i], in weight);
                    normals[i] = statXform.TransformNormal(normals[i], normTargets[i], in weight);
                }
            }

            if (xform is SharpGLTF.Transforms.SkinnedTransform skinXform)
            {
                if (joints0 == null || weights0 == null)
                    throw new InvalidOperationException("Cannot apply a skin transform to a primitive withough joint and weight data.");

                for (int i = 0; i < verts.Count; ++i)
                {
                    var jt = joints0[i]; var jt1 = joints1?[i] ?? Vector4.Zero;
                    var wt = weights0[i]; var wt1 = weights1?[i] ?? Vector4.Zero;
                    var skinWeights = new SparseWeight8(in jt, in jt1, in wt, in wt1);
                    verts[i] = skinXform.TransformPosition(verts[i], posTargets[i], in skinWeights);
                    normals[i] = skinXform.TransformNormal(normals[i], normTargets[i], in skinWeights);
                }
            }

            int currVertCount = mesh.Vertices.Count;
            mesh.Vertices.AddRange(verts);
            mesh.Normals.AddRange(normals);
            if (tc != null)
            {
                mesh.TextureCoordinates.AddRange(tc.Select(t => new Vector3(t.X, t.Y, 0.0f)));
            }

            foreach (var tri in prim.GetTriangleIndices())
            {
                Face f = new Face();
                f.Indices.Add((short)(tri.A + currVertCount));
                f.Indices.Add((short)(tri.B + currVertCount));
                f.Indices.Add((short)(tri.C + currVertCount));
                mesh.Faces.Add(f);
            }
        }

        private void ConvertMaterials(ModelRoot model, SceneTemplate modelTemplate, BrgFile brg, out Dictionary<int, int> matIdMapping)
        {
            matIdMapping = new Dictionary<int, int>();
            foreach (var id in modelTemplate.LogicalMeshIds)
            {
                var mesh = model.LogicalMeshes[id];
                foreach (var prim in mesh.Primitives)
                {
                    var gltfMat = prim.Material;
                    if (gltfMat == null) continue;

                    BrgMaterial mat = new BrgMaterial(brg);
                    mat.Id = brg.Materials.Count + 1;
                    ConvertMaterial(gltfMat, mat);

                    int matListIndex = brg.Materials.IndexOf(mat);
                    int actualMatId = gltfMat.LogicalIndex;
                    if (matListIndex >= 0)
                    {
                        if (!matIdMapping.ContainsKey(actualMatId))
                        {
                            matIdMapping.Add(actualMatId, brg.Materials[matListIndex].Id);
                        }
                    }
                    else
                    {
                        brg.Materials.Add(mat);
                        if (matIdMapping.ContainsKey(actualMatId))
                        {
                            matIdMapping[actualMatId] = mat.Id;
                        }
                        else
                        {
                            matIdMapping.Add(actualMatId, mat.Id);
                        }
                    }
                }
            }
        }
        #endregion

        private void ConvertMaterial(GltfMaterial glMat, BrgMaterial mat)
        {
            mat.DiffuseColor = GetDiffuseColor(glMat);
            mat.AmbientColor = mat.DiffuseColor;
            mat.SpecularColor = GetSpecularColor(glMat);
            mat.EmissiveColor = GetEmissiveColor(glMat);

            mat.SpecularExponent = GetSpecularPower(glMat);
            if (mat.SpecularExponent > 0)
            {
                mat.Flags |= BrgMatFlag.SpecularExponent;
            }

            mat.Opacity = GetAlphaLevel(glMat);
            if (mat.Opacity < 1f)
            {
                mat.Flags |= BrgMatFlag.Alpha;
            }

            //int opacityType = Maxscript.QueryInteger("mat.opacityType");
            //if (opacityType == 1)
            //{
            //    mat.Flags |= BrgMatFlag.SubtractiveBlend;
            //}
            //else if (opacityType == 2)
            //{
            //    mat.Flags |= BrgMatFlag.AdditiveBlend;
            //}

            if (glMat.DoubleSided)
            {
                mat.Flags |= BrgMatFlag.TwoSided;
            }

            //if (Maxscript.QueryBoolean("mat.faceMap"))
            //{
            //    mat.Flags |= BrgMatFlag.FaceMap;
            //}

            //if (Maxscript.QueryBoolean("(classof mat.reflectionMap) == BitmapTexture"))
            //{
            //    mat.Flags |= BrgMatFlag.WrapUTx1 | BrgMatFlag.WrapVTx1 | BrgMatFlag.REFLECTIONTEXTURE;
            //    BrgMatSFX sfxMap = new BrgMatSFX();
            //    sfxMap.Id = 30;
            //    sfxMap.Name = Maxscript.QueryString("getFilenameFile(mat.reflectionMap.filename)") + ".cub";
            //    mat.sfx.Add(sfxMap);
            //}
            //if (Maxscript.QueryBoolean("(classof mat.bumpMap) == BitmapTexture"))
            //{
            //    mat.BumpMap = Maxscript.QueryString("getFilenameFile(mat.bumpMap.filename)");
            //    if (mat.BumpMap.Length > 0)
            //    {
            //        mat.Flags |= BrgMatFlag.WrapUTx3 | BrgMatFlag.WrapVTx3 | BrgMatFlag.BumpMap;
            //    }
            //}
            mat.DiffuseMap = GetDiffuseTexture(glMat, out BrgMatFlag wrapFlags);
            int parenthIndex = mat.DiffuseMap.IndexOf('(');
            if (parenthIndex > 0)
            {
                mat.DiffuseMap = mat.DiffuseMap.Remove(parenthIndex);
            }
            if (mat.DiffuseMap.Length > 0)
            {
                mat.Flags |= wrapFlags;
                if (glMat.Alpha == AlphaMode.MASK)
                {
                    mat.Flags |= BrgMatFlag.PixelXForm1;
                }
            }

            this.GetMaterialFlagsFromName(glMat, mat);
        }
        private static Color3D GetDiffuseColor(GltfMaterial srcMaterial)
        {
            var diffuse = srcMaterial.FindChannel("Diffuse");

            if (diffuse == null) diffuse = srcMaterial.FindChannel("BaseColor");

            if (diffuse == null) return new Color3D(1.0f);

            return new Color3D(diffuse.Value.Parameter.X, diffuse.Value.Parameter.Y, diffuse.Value.Parameter.Z);
        }
        private static Color3D GetSpecularColor(GltfMaterial srcMaterial)
        {
            var mr = srcMaterial.FindChannel("MetallicRoughness");

            if (mr == null) return new Color3D(1.0f); // default value 16

            var diff = GetDiffuseColor(srcMaterial);
            var diffuse = new Vector3(diff.R, diff.G, diff.B);
            var metallic = mr.Value.Parameter.X;
            var roughness = mr.Value.Parameter.Y;

            var k = Vector3.Zero;
            k += Vector3.Lerp(diffuse, Vector3.Zero, roughness);
            k += Vector3.Lerp(diffuse, Vector3.One, metallic);
            k *= 0.5f;

            return new Color3D(k.X, k.Y, k.Z);
        }
        private static Color3D GetEmissiveColor(GltfMaterial srcMaterial)
        {
            var emissive = srcMaterial.FindChannel("Emissive");

            if (emissive == null) return new Color3D(1.0f);

            return new Color3D(emissive.Value.Parameter.X, emissive.Value.Parameter.Y, emissive.Value.Parameter.Z);
        }
        private static float GetSpecularPower(GltfMaterial srcMaterial)
        {
            var mr = srcMaterial.FindChannel("MetallicRoughness");

            if (mr == null) return 0; // default value = 16

            var metallic = mr.Value.Parameter.X;
            var roughness = mr.Value.Parameter.Y;

            return 4 + 16 * metallic;
        }
        private static float GetAlphaLevel(GltfMaterial srcMaterial)
        {
            if (srcMaterial.Alpha == AlphaMode.OPAQUE) return 1;

            var baseColor = srcMaterial.FindChannel("BaseColor");

            if (baseColor == null) return 1;

            return baseColor.Value.Parameter.W;
        }
        private string GetDiffuseTexture(GltfMaterial srcMaterial, out BrgMatFlag wrapFlags)
        {
            var diffuse = srcMaterial.FindChannel("Diffuse");
            wrapFlags = BrgMatFlag.WrapUTx1 | BrgMatFlag.WrapVTx1;

            if (diffuse == null) diffuse = srcMaterial.FindChannel("BaseColor");
            if (diffuse == null) return string.Empty;
            if (diffuse.Value.Texture == null) return string.Empty;
            if (diffuse.Value.Texture.PrimaryImage == null) return string.Empty;

            var tex = diffuse.Value.Texture;
            if (tex.Sampler != null)
            {
                wrapFlags = 0;
                wrapFlags |= tex.Sampler.WrapS == TextureWrapMode.CLAMP_TO_EDGE ? 0 : BrgMatFlag.WrapUTx1;
                wrapFlags |= tex.Sampler.WrapT == TextureWrapMode.CLAMP_TO_EDGE ? 0 : BrgMatFlag.WrapVTx1;
            }

            string name = tex.PrimaryImage.Name ?? tex.PrimaryImage.LogicalIndex.ToString();
            int parenthIndex = name.IndexOf('(');
            if (parenthIndex > 0)
            {
                name = name.Remove(parenthIndex);
            }

            return name;
        }
        private void GetMaterialFlagsFromName(GltfMaterial glMat, BrgMaterial mat)
        {
            string? flags = glMat.Name?.ToLower();
            if (flags == null) return;

            StringComparison cmp = StringComparison.InvariantCultureIgnoreCase;
            if (flags.Contains("colorxform1", cmp))
            {
                mat.Flags |= BrgMatFlag.PlayerXFormColor1;
            }

            if (flags.Contains("2-sided", cmp) ||
                flags.Contains("2 sided", cmp) ||
                flags.Contains("2sided", cmp))
            {
                mat.Flags |= BrgMatFlag.TwoSided;
            }

            if (flags.Contains("pixelxform1", cmp))
            {
                mat.Flags |= BrgMatFlag.PixelXForm1;
            }
        }
    }
}