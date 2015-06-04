﻿namespace AoMEngineLibrary.Graphics.Brg
{
    using AoMEngineLibrary.Graphics.Model;
    using MiscUtil.Conversion;
    using System;
    using System.Collections.Generic;

    public class BrgFile : ModelFile<BrgMesh, BrgMaterial>
    {
        public string FileName { get; set; }

        public BrgHeader Header { get; set; }
        public BrgAsetHeader AsetHeader { get; set; }

        public BrgFile(System.IO.FileStream fileStream)
            : base()
        {
            using (BrgBinaryReader reader = new BrgBinaryReader(new LittleEndianBitConverter(), fileStream))
            {
                this.FileName = fileStream.Name;
                this.Header = new BrgHeader(reader);
                if (this.Header.Magic != "BANG")
                {
                    throw new Exception("This is not a BRG file!");
                }
                this.AsetHeader = new BrgAsetHeader();

                int asetCount = 0;
                this.Meshes = new List<BrgMesh>(this.Header.NumMeshes);
                this.Materials = new List<BrgMaterial>();
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    string magic = reader.ReadString(4);
                    if (magic == "ASET")
                    {
                        this.AsetHeader = new BrgAsetHeader(reader);
                        ++asetCount;
                    }
                    else if (magic == "MESI")
                    {
                        if (this.Meshes.Count > 0)
                        {
                            this.Meshes[0].MeshAnimations.Add(new BrgMesh(reader, this));
                        }
                        else
                        {
                            this.Meshes.Add(new BrgMesh(reader, this));
                        }
                    }
                    else if (magic == "MTRL")
                    {
                        BrgMaterial mat = new BrgMaterial(reader, this);
                        if (!ContainsMaterialID(mat.id))
                        {
                            Materials.Add(mat);
                        }
                        else
                        {
                            //throw new Exception("Duplicate material ids!");
                        }
                    }
                    else
                    {
                        throw new Exception("The type tag " +/* magic +*/ " is not recognized!");
                    }
                }

                if (asetCount > 1)
                {
                    //throw new Exception("Multiple ASETs!");
                }

                if (Header.NumMeshes < Meshes.Count)
                {
                    throw new Exception("Inconsistent mesh count!");
                }

                if (Header.NumMaterials < Materials.Count)
                {
                    throw new Exception("Inconsistent material count!");
                }

                if (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    throw new Exception("The end of stream was not reached!");
                }

                this.Animation.Duration = this.Meshes[0].ExtendedHeader.AnimationLength;
                this.Animation.TimeStep = this.Meshes[0].ExtendedHeader.AnimationLength / (float)(this.Meshes[0].MeshAnimations.Count + 1);
                if (this.Meshes[0].Header.AnimationType.HasFlag(BrgMeshAnimType.NONUNIFORM))
                {
                    for (int i = 0; i <= this.Meshes[0].MeshAnimations.Count; ++i)
                    {
                        this.Animation.MeshChannel.MeshTimes.Add(this.Meshes[0].NonUniformKeys[i] * this.Animation.Duration);
                    }
                }
                else if (this.Meshes[0].MeshAnimations.Count > 0)
                {
                    for (int i = 0; i <= this.Meshes[0].MeshAnimations.Count; ++i)
                    {
                        this.Animation.MeshChannel.MeshTimes.Add((float)i / ((float)this.Meshes[0].MeshAnimations.Count) * this.Animation.Duration);
                    }
                }
                else
                {
                    this.Animation.MeshChannel.MeshTimes.Add(0);
                }
            }
        }
        public BrgFile()
            : base()
        {
            this.FileName = string.Empty;
            this.Header = new BrgHeader();
            this.Header.Unknown03 = 1999922179;
            this.AsetHeader = new BrgAsetHeader();
        }

        public void Write(System.IO.FileStream fileStream)
        {
            using (BrgBinaryWriter writer = new BrgBinaryWriter(new LittleEndianBitConverter(), fileStream))
            {
                this.FileName = fileStream.Name;

                this.Header.NumMeshes = this.Meshes[0].MeshAnimations.Count + 1;
                this.Header.NumMaterials = this.Materials.Count;
                this.Header.Write(writer);

                if (this.Header.NumMeshes > 1)
                {
                    updateAsetHeader();
                    writer.Write(1413829441); // magic "ASET"
                    this.AsetHeader.Write(writer);
                }

                for (int i = 0; i <= this.Meshes[0].MeshAnimations.Count; i++)
                {
                    writer.Write(1230193997); // magic "MESI"
                    if (i > 0)
                    {
                        ((BrgMesh)this.Meshes[0].MeshAnimations[i - 1]).Write(writer);
                    }
                    else
                    {
                        this.Meshes[i].Write(writer);
                    }
                }

                for (int i = 0; i < this.Materials.Count; i++)
                {
                    writer.Write(1280463949); // magic "MTRL"
                    this.Materials[i].Write(writer);
                }
            }
        }

        public bool ContainsMaterialID(int id)
        {
            for (int i = 0; i < Materials.Count; i++)
            {
                if (Materials[i].id == id)
                {
                    return true;
                }
            }

            return false;
        }
        private void updateAsetHeader()
        {
            AsetHeader.numFrames = this.Animation.MeshChannel.MeshTimes.Count;
            AsetHeader.frameStep = 1f / (float)AsetHeader.numFrames;
            AsetHeader.animTime = this.Animation.Duration;
            AsetHeader.frequency = 1f / (float)AsetHeader.animTime;
            AsetHeader.spf = AsetHeader.animTime / (float)AsetHeader.numFrames;
            AsetHeader.fps = (float)AsetHeader.numFrames / AsetHeader.animTime;
        }

        public void UpdateMeshSettings(BrgMeshFlag flags, BrgMeshFormat format, BrgMeshAnimType animType, byte interpolationType, float exportedScaleFactor)
        {
            for (int i = 0; i <= this.Meshes[0].MeshAnimations.Count; i++)
            {
                UpdateMeshSettings(i, flags, format, animType, interpolationType, exportedScaleFactor);
            }
        }
        public void UpdateMeshSettings(int meshIndex, BrgMeshFlag flags, BrgMeshFormat format, BrgMeshAnimType animType, byte interpolationType, float exportedScaleFactor)
        {
            if (meshIndex > 0)
            {
                ((BrgMesh)this.Meshes[0].MeshAnimations[meshIndex - 1]).Header.Flags = flags;
                ((BrgMesh)this.Meshes[0].MeshAnimations[meshIndex - 1]).Header.Format = format;
                ((BrgMesh)this.Meshes[0].MeshAnimations[meshIndex - 1]).Header.AnimationType = animType;
                ((BrgMesh)this.Meshes[0].MeshAnimations[meshIndex - 1]).Header.InterpolationType = interpolationType;
                ((BrgMesh)this.Meshes[0].MeshAnimations[meshIndex - 1]).ExtendedHeader.ExportedScaleFactor = exportedScaleFactor;
                ((BrgMesh)this.Meshes[0].MeshAnimations[meshIndex - 1]).Header.Flags |= BrgMeshFlag.SECONDARYMESH;
                ((BrgMesh)this.Meshes[0].MeshAnimations[meshIndex - 1]).Header.AnimationType &= ~BrgMeshAnimType.NONUNIFORM;
            }
            else
            {
                this.Meshes[meshIndex].Header.Flags = flags;
                this.Meshes[meshIndex].Header.Format = format;
                this.Meshes[meshIndex].Header.AnimationType = animType;
                this.Meshes[meshIndex].Header.InterpolationType = interpolationType;
                this.Meshes[meshIndex].ExtendedHeader.ExportedScaleFactor = exportedScaleFactor;
            }
        }
    }
}